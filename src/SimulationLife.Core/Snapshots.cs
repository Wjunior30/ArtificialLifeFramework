using System.Security.Cryptography;
using System.Text;

namespace SimulationLife.Core;

public sealed class AgentSnapshot
{
    public int Id { get; set; }
    public Needs Needs { get; set; } = new();
    public Inventory Inventory { get; set; } = new();
    public AgentAction Action { get; set; }
    public double ActionAge { get; set; }
    public string DecisionReason { get; set; } = string.Empty;
    public bool Alive { get; set; }
    public double WorkPaymentAccumulator { get; set; }
}

public sealed class LedgerSnapshot
{
    public long InitialTotal { get; set; }
    public Dictionary<string, long> Accounts { get; set; } = new(StringComparer.Ordinal);
    public List<LedgerEntry> Entries { get; set; } = new();
}

public sealed class SimulationSnapshot
{
    public int Schema { get; set; } = 1;
    public uint WorldSeed { get; set; }
    public long Tick { get; set; }
    public double TotalMinutes { get; set; }
    public double Accumulator { get; set; }
    public uint RandomState { get; set; }
    public int FoodStock { get; set; }
    public int WaterStock { get; set; }
    public long EventSequence { get; set; }
    public List<AgentSnapshot> Agents { get; set; } = new();
    public LedgerSnapshot Ledger { get; set; } = new();
    public List<SimulationEvent> PendingEvents { get; set; } = new();
}

public sealed partial class Simulation
{
    public WorldDefinition WorldDefinition => WorldValidator.Clone(definition);

    public SimulationSnapshot CaptureSnapshot() => new()
    {
        WorldSeed = definition.Seed,
        Tick = Tick,
        TotalMinutes = TotalMinutes,
        Accumulator = accumulator,
        RandomState = Random.State,
        FoodStock = FoodStock,
        WaterStock = WaterStock,
        EventSequence = eventSequence,
        Agents = agents.Select(agent => new AgentSnapshot
        {
            Id = agent.Id,
            Needs = agent.Needs.Copy(),
            Inventory = agent.Inventory.Copy(),
            Action = agent.Action,
            ActionAge = agent.ActionAge,
            DecisionReason = agent.DecisionReason,
            Alive = agent.Alive,
            WorkPaymentAccumulator = agent.WorkPaymentAccumulator
        }).ToList(),
        Ledger = Ledger.CaptureSnapshot(),
        PendingEvents = pendingEvents.Select(CopyEvent).ToList()
    };

    public static Simulation Restore(WorldDefinition definition, SimulationSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        var simulation = new Simulation(definition);
        ValidateSnapshot(simulation, snapshot);

        simulation.Tick = snapshot.Tick;
        simulation.TotalMinutes = snapshot.TotalMinutes;
        simulation.accumulator = snapshot.Accumulator;
        simulation.Random.Restore(snapshot.RandomState);
        simulation.FoodStock = snapshot.FoodStock;
        simulation.WaterStock = snapshot.WaterStock;
        simulation.eventSequence = snapshot.EventSequence;
        foreach (var source in snapshot.Agents)
        {
            var target = simulation.agents.Single(agent => agent.Id == source.Id);
            CopyNeeds(source.Needs, target.Needs);
            CopyInventory(source.Inventory, target.Inventory);
            target.Action = source.Action;
            target.ActionAge = source.ActionAge;
            target.DecisionReason = source.DecisionReason;
            target.Alive = source.Alive;
            target.WorkPaymentAccumulator = source.WorkPaymentAccumulator;
        }
        simulation.Ledger.Restore(snapshot.Ledger);
        simulation.pendingEvents.Clear();
        simulation.pendingEvents.AddRange(snapshot.PendingEvents.Select(CopyEvent));
        return simulation;
    }

    public string StateHash() => SnapshotHasher.Compute(CaptureSnapshot());

    private static void ValidateSnapshot(Simulation simulation, SimulationSnapshot snapshot)
    {
        if (snapshot.Schema != 1 || snapshot.WorldSeed != simulation.definition.Seed)
            throw new ArgumentException("Snapshot schema or seed does not match the world.", nameof(snapshot));
        if (snapshot.Tick < 0 || !double.IsFinite(snapshot.TotalMinutes) || snapshot.TotalMinutes < 0 ||
            !double.IsFinite(snapshot.Accumulator) || snapshot.Accumulator < 0 ||
            snapshot.Accumulator >= simulation.definition.FixedStepSeconds ||
            snapshot.FoodStock < 0 || snapshot.WaterStock < 0 || snapshot.EventSequence < 0 ||
            snapshot.Agents is null || snapshot.Ledger is null || snapshot.PendingEvents is null)
            throw new ArgumentException("Snapshot contains invalid state.", nameof(snapshot));

        var expectedIds = simulation.agents.Select(agent => agent.Id).OrderBy(id => id).ToArray();
        var actualIds = snapshot.Agents.Where(agent => agent is not null).Select(agent => agent.Id).OrderBy(id => id).ToArray();
        if (snapshot.Agents.Any(agent => agent is null) || !expectedIds.SequenceEqual(actualIds))
            throw new ArgumentException("Snapshot agents do not match the world definition.", nameof(snapshot));

        foreach (var agent in snapshot.Agents)
        {
            if (agent.Needs is null || agent.Inventory is null || !WorldValidator.ValidNeeds(agent.Needs) ||
                agent.Inventory.Food < 0 || agent.Inventory.Water < 0 || agent.Inventory.Medicine < 0 ||
                !Enum.IsDefined(typeof(AgentAction), agent.Action) ||
                !double.IsFinite(agent.ActionAge) || agent.ActionAge < 0 ||
                !double.IsFinite(agent.WorkPaymentAccumulator) || agent.WorkPaymentAccumulator < 0 ||
                agent.DecisionReason is null || agent.DecisionReason.Length > 2_048)
                throw new ArgumentException($"Snapshot agent {agent.Id} is invalid.", nameof(snapshot));
        }

        var seenEvents = new HashSet<long>();
        foreach (var item in snapshot.PendingEvents)
        {
            if (item is null || item.Sequence <= 0 || item.Sequence > snapshot.EventSequence ||
                item.Tick < 0 || item.Tick > snapshot.Tick || string.IsNullOrWhiteSpace(item.Kind) ||
                item.Message is null || item.Data is null || !seenEvents.Add(item.Sequence) ||
                item.Data.Any(pair => pair.Key is null || pair.Value is null))
                throw new ArgumentException("Snapshot contains an invalid pending event.", nameof(snapshot));
        }
    }

    private static void CopyNeeds(Needs source, Needs target)
    {
        target.Hunger = source.Hunger;
        target.Thirst = source.Thirst;
        target.Energy = source.Energy;
        target.Safety = source.Safety;
        target.Social = source.Social;
        target.Comfort = source.Comfort;
    }

    private static void CopyInventory(Inventory source, Inventory target)
    {
        target.Food = source.Food;
        target.Water = source.Water;
        target.Medicine = source.Medicine;
    }

    private static SimulationEvent CopyEvent(SimulationEvent source) => new()
    {
        Sequence = source.Sequence,
        Tick = source.Tick,
        Kind = source.Kind,
        AgentId = source.AgentId,
        Message = source.Message,
        Data = new Dictionary<string, string>(source.Data, StringComparer.Ordinal)
    };
}

public static class SnapshotHasher
{
    public static string Compute(SimulationSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(snapshot.Schema);
            writer.Write(snapshot.WorldSeed);
            writer.Write(snapshot.Tick);
            WriteDouble(writer, snapshot.TotalMinutes);
            WriteDouble(writer, snapshot.Accumulator);
            writer.Write(snapshot.RandomState);
            writer.Write(snapshot.FoodStock);
            writer.Write(snapshot.WaterStock);
            writer.Write(snapshot.EventSequence);

            writer.Write(snapshot.Ledger.InitialTotal);
            writer.Write(snapshot.Ledger.Accounts.Count);
            foreach (var account in snapshot.Ledger.Accounts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.Write(account.Key);
                writer.Write(account.Value);
            }
            writer.Write(snapshot.Ledger.Entries.Count);
            foreach (var entry in snapshot.Ledger.Entries.OrderBy(entry => entry.Sequence))
            {
                writer.Write(entry.Sequence);
                writer.Write(entry.Tick);
                writer.Write(entry.Kind ?? string.Empty);
                writer.Write(entry.Payer ?? string.Empty);
                writer.Write(entry.Receiver ?? string.Empty);
                writer.Write(entry.Cents);
            }

            writer.Write(snapshot.Agents.Count);
            foreach (var agent in snapshot.Agents.OrderBy(agent => agent.Id))
            {
                writer.Write(agent.Id);
                WriteNeeds(writer, agent.Needs);
                writer.Write(agent.Inventory.Food);
                writer.Write(agent.Inventory.Water);
                writer.Write(agent.Inventory.Medicine);
                writer.Write((int)agent.Action);
                WriteDouble(writer, agent.ActionAge);
                writer.Write(agent.DecisionReason ?? string.Empty);
                writer.Write(agent.Alive);
                WriteDouble(writer, agent.WorkPaymentAccumulator);
            }

            writer.Write(snapshot.PendingEvents.Count);
            foreach (var item in snapshot.PendingEvents.OrderBy(item => item.Sequence))
            {
                writer.Write(item.Sequence);
                writer.Write(item.Tick);
                writer.Write(item.Kind ?? string.Empty);
                writer.Write(item.AgentId);
                writer.Write(item.Message ?? string.Empty);
                writer.Write(item.Data.Count);
                foreach (var pair in item.Data.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value);
                }
            }
        }
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void WriteNeeds(BinaryWriter writer, Needs needs)
    {
        WriteDouble(writer, needs.Hunger);
        WriteDouble(writer, needs.Thirst);
        WriteDouble(writer, needs.Energy);
        WriteDouble(writer, needs.Safety);
        WriteDouble(writer, needs.Social);
        WriteDouble(writer, needs.Comfort);
    }

    private static void WriteDouble(BinaryWriter writer, double value) =>
        writer.Write(BitConverter.DoubleToInt64Bits(value));
}
