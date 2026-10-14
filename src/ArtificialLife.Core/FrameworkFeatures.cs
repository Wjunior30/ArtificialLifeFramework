using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace ArtificialLife.Core;

/// <summary>Small entry point for the common framework scenarios.</summary>
public static class ArtificialLife
{
    public static LifeWorld CreateWorld(ulong seed) => new(seed);
    public static SettlementSimulation CreateSettlements(ulong seed, Action<SettlementConfiguration>? configure = null)
    {
        var configuration = new SettlementConfiguration();
        configure?.Invoke(configuration);
        return configuration.Build(seed);
    }
}

/// <summary>Mutable composition object used only while a settlement simulation is being created.</summary>
public sealed class SettlementConfiguration
{
    public SettlementRules Rules { get; set; } = new();
    public SettlementSimulationOptions Options { get; set; } = new();
    public SettlementPolicies Policies { get; set; } = new();
    public SettlementSimulation Build(ulong seed) => new(seed, Options, Rules, Policies);
}

/// <summary>Container-neutral registrations for optional dependency-injection composition roots.</summary>
public sealed class ArtificialLifeServiceCatalog
{
    private readonly Dictionary<Type, Func<object>> _factories = new();
    public ArtificialLifeServiceCatalog AddSingleton<T>(T instance) where T : class
    { if (instance is null) throw new ArgumentNullException(nameof(instance)); _factories[typeof(T)] = () => instance; return this; }
    public ArtificialLifeServiceCatalog AddTransient<T>(Func<T> factory) where T : class
    { if (factory is null) throw new ArgumentNullException(nameof(factory)); _factories[typeof(T)] = () => factory(); return this; }
    public T GetRequired<T>() where T : class
        => _factories.TryGetValue(typeof(T), out var factory) ? (T)factory() : throw new InvalidOperationException($"Service {typeof(T).FullName} is not registered.");
}

public enum DiagnosticSeverity { Trace, Information, Warning, Error }
public sealed record SimulationDiagnostic(int Day, string Code, DiagnosticSeverity Severity, string Message);
public interface ISimulationDiagnostics { void Report(SimulationDiagnostic diagnostic); }

/// <summary>Thread-safe bounded diagnostic sink suitable for tools and engine inspectors.</summary>
public sealed class DiagnosticBuffer : ISimulationDiagnostics
{
    private readonly object _gate = new();
    private readonly Queue<SimulationDiagnostic> _items = new();
    public DiagnosticBuffer(int capacity = 256) { if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity)); Capacity = capacity; }
    public int Capacity { get; }
    public IReadOnlyList<SimulationDiagnostic> Items { get { lock (_gate) return _items.ToArray(); } }
    public void Report(SimulationDiagnostic diagnostic)
    {
        if (diagnostic is null) throw new ArgumentNullException(nameof(diagnostic));
        lock (_gate) { while (_items.Count >= Capacity) _items.Dequeue(); _items.Enqueue(diagnostic); }
    }
}

public sealed record SimulationMetric(string Name, double Value, string Unit);
public sealed record SimulationObservation(string Operation, TimeSpan Elapsed, IReadOnlyList<SimulationMetric> Metrics);
public interface ISimulationTelemetryObserver { void Observe(SimulationObservation observation); }

/// <summary>Measures host work without becoming part of deterministic simulation state.</summary>
public static class SimulationTelemetry
{
    public static void Measure(string operation, Action action, ISimulationTelemetryObserver observer, params SimulationMetric[] metrics)
    {
        if (string.IsNullOrWhiteSpace(operation)) throw new ArgumentException("Operation is required.", nameof(operation));
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (observer is null) throw new ArgumentNullException(nameof(observer));
        var watch = Stopwatch.StartNew();
        try { action(); }
        finally { watch.Stop(); observer.Observe(new(operation, watch.Elapsed, Array.AsReadOnly(metrics ?? Array.Empty<SimulationMetric>()))); }
    }
}

public sealed record ScheduledSystem(string Id, int EveryDays, int Order, Action<int> Execute);

/// <summary>Deterministic scheduler: due systems run by order and then by stable identifier.</summary>
public sealed class SimulationScheduler
{
    private readonly List<ScheduledSystem> _systems = new();
    public IReadOnlyList<ScheduledSystem> Systems => _systems.OrderBy(x => x.Order).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
    public void Add(ScheduledSystem system)
    {
        if (system is null) throw new ArgumentNullException(nameof(system));
        if (string.IsNullOrWhiteSpace(system.Id) || system.EveryDays < 1 || _systems.Any(x => x.Id == system.Id)) throw new ArgumentException("Invalid or duplicate scheduled system.", nameof(system));
        _systems.Add(system);
    }
    public bool Remove(string id) => _systems.RemoveAll(x => x.Id == id) != 0;
    public void Run(int day)
    {
        if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
        foreach (var system in Systems) if (day % system.EveryDays == 0) system.Execute(day);
    }
}

/// <summary>An aggregate population that can be expanded into agents near the player and collapsed again.</summary>
public sealed record PopulationCohort(string Id, int Population, IReadOnlyDictionary<string, long> Goods);
public sealed record MaterializedPopulation(PopulationCohort Source, IReadOnlyList<EntityId> Agents);

public static class PopulationMaterialization
{
    public static MaterializedPopulation Expand(PopulationCohort cohort, LifeWorld world, int count)
    {
        if (cohort is null) throw new ArgumentNullException(nameof(cohort)); if (world is null) throw new ArgumentNullException(nameof(world));
        if (count < 1 || count > cohort.Population) throw new ArgumentOutOfRangeException(nameof(count));
        var ids = new List<EntityId>(count); var remainingGoods = cohort.Goods.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            var agent = Agent.Create(world.Ids.Next("cohort:" + cohort.Id), $"{cohort.Id} {i + 1}");
            foreach (var good in cohort.Goods.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                long share = good.Value / cohort.Population + (i < good.Value % cohort.Population ? 1 : 0);
                if (share > 0) { agent.Inventory.Add(good.Key, share); remainingGoods[good.Key] -= share; }
            }
            world.AddAgent(agent); ids.Add(agent.Id);
        }
        return new(cohort with { Population = cohort.Population - count, Goods = remainingGoods }, ids.AsReadOnly());
    }

    public static PopulationCohort Collapse(MaterializedPopulation materialized, LifeWorld world)
    {
        if (materialized is null) throw new ArgumentNullException(nameof(materialized)); if (world is null) throw new ArgumentNullException(nameof(world));
        int survivors = 0; var goods = materialized.Source.Goods.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        foreach (var id in materialized.Agents)
        {
            if (!world.TryRemoveAgent(id, out var agent)) continue;
            if (agent!.IsAlive) survivors++;
            foreach (var good in agent.Inventory.All) goods[good.Key] = checked(goods.GetValueOrDefault(good.Key) + good.Value);
        }
        return materialized.Source with { Population = materialized.Source.Population + survivors, Goods = goods };
    }
}

public sealed partial class LifeWorld
{
    public bool TryRemoveAgent(EntityId id, out Agent? agent) => _agents.Remove(id, out agent);
}

public enum WarGoal { Survival, Territory, Resources, Domination }
public sealed record DiplomaticFactor(string Name, double Value);
public sealed record DiplomaticExplanation(int First, int Second, double Relation, IReadOnlyList<DiplomaticFactor> Factors);
public sealed record WarPlan(int First, int Second, WarGoal Goal, int StartedDay);

public sealed partial class SettlementSimulation
{
    /// <summary>Explains the current score using stable, inspectable factors; it never advances random state.</summary>
    public DiplomaticExplanation ExplainRelationship(int first, int second)
    {
        var key = Key(first, second);
        if (!_relations.TryGetValue(key, out double relation)) throw new ArgumentException("The settlements do not have a relationship.");
        var a = Settlements.SingleOrDefault(x => x.Id == key.Item1) ?? throw new ArgumentException("Unknown settlement.");
        var b = Settlements.SingleOrDefault(x => x.Id == key.Item2) ?? throw new ArgumentException("Unknown settlement.");
        double distance = Distance(Sites[a.SiteId].Position, Sites[b.SiteId].Position);
        var factors = new[]
        {
            new DiplomaticFactor("proximity-trade", Math.Max(0, 1 - distance / 2800) * 1.2),
            new DiplomaticFactor("territorial-rivalry", -Math.Max(0, 1 - distance / 1300) * 2.5 * _options.ConflictMultiplier),
            new DiplomaticFactor("conflict-pressure", -Math.Max(0, _options.ConflictMultiplier - 1) * .65)
        };
        return new(key.Item1, key.Item2, relation, Array.AsReadOnly(factors));
    }

    public WarPlan? GetWarPlan(int first, int second)
    {
        var key = Key(first, second);
        if (!_conflicts.TryGetValue(key, out var conflict) || !conflict.Active) return null;
        var a = Settlements.Single(x => x.Id == key.Item1); var b = Settlements.Single(x => x.Id == key.Item2);
        WarGoal goal = a.Food + b.Food < (a.Population + b.Population) * 2 ? WarGoal.Resources :
            Sites[a.SiteId].Quality + .1 < Sites[b.SiteId].Quality ? WarGoal.Territory : WarGoal.Domination;
        return new(key.Item1, key.Item2, goal, conflict.StartedDay);
    }
}

/// <summary>Compact deterministic checkpoint: base snapshot plus days to replay and an expected result hash.</summary>
public sealed record IncrementalSettlementSnapshot(SimulationSave Base, int Days, string ResultHash);

/// <summary>Creates and restores deterministic incremental settlement checkpoints.</summary>
public static class SettlementSnapshots
{
    public static IncrementalSettlementSnapshot CaptureIncrement(SettlementSimulation basis, int days)
    {
        if (basis is null) throw new ArgumentNullException(nameof(basis));
        if (days < 0) throw new ArgumentOutOfRangeException(nameof(days));
        var save = basis.CaptureState(); var replay = SettlementSimulation.Restore(Clone(save)); replay.Advance(days);
        return new(Clone(save), days, replay.StateHash());
    }
    public static SettlementSimulation RestoreIncrement(IncrementalSettlementSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        var simulation = SettlementSimulation.Restore(Clone(snapshot.Base)); simulation.Advance(snapshot.Days);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(simulation.StateHash()), Encoding.ASCII.GetBytes(snapshot.ResultHash)))
            throw new ArgumentException("Incremental snapshot result does not match its expected state.", nameof(snapshot));
        return simulation;
    }
    private static SimulationSave Clone(SimulationSave save) => new() { Schema = save.Schema, Kind = save.Kind, Payload = (byte[])save.Payload.Clone(), Checksum = save.Checksum };
}

public sealed record AuthenticatedSimulationSave(SimulationSave Save, string AuthenticationCode);

/// <summary>Adds optional HMAC-SHA256 origin authentication; the framework never stores the secret key.</summary>
public static class SnapshotAuthentication
{
    public static AuthenticatedSimulationSave Sign(SimulationSave save, byte[] key)
    {
        Validate(save, key); return new(Clone(save), Compute(save, key));
    }
    public static SimulationSave Verify(AuthenticatedSimulationSave authenticated, byte[] key)
    {
        if (authenticated is null) throw new ArgumentNullException(nameof(authenticated)); Validate(authenticated.Save, key);
        byte[] expected = DecodeHex(Compute(authenticated.Save, key));
        byte[] actual; try { actual = DecodeHex(authenticated.AuthenticationCode); } catch (FormatException ex) { throw new ArgumentException("Invalid snapshot authentication code.", nameof(authenticated), ex); }
        if (!CryptographicOperations.FixedTimeEquals(expected, actual)) throw new ArgumentException("Snapshot authentication failed.", nameof(authenticated));
        return Clone(authenticated.Save);
    }
    private static string Compute(SimulationSave save, byte[] key)
    {
        using var hmac = new HMACSHA256(key); using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(save.Schema); writer.Write(save.Kind ?? ""); writer.Write(save.Payload.Length); writer.Write(save.Payload); writer.Write(save.Checksum ?? ""); writer.Flush();
        return BitConverter.ToString(hmac.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
    }
    private static void Validate(SimulationSave save, byte[] key) { if (save is null) throw new ArgumentNullException(nameof(save)); if (key is null || key.Length < 16) throw new ArgumentException("Authentication keys must contain at least 16 bytes.", nameof(key)); }
    private static SimulationSave Clone(SimulationSave save) => new() { Schema = save.Schema, Kind = save.Kind, Payload = (byte[])save.Payload.Clone(), Checksum = save.Checksum };
    private static byte[] DecodeHex(string value)
    {
        if (value is null || value.Length % 2 != 0) throw new FormatException();
        var bytes = new byte[value.Length / 2];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
        return bytes;
    }
}
