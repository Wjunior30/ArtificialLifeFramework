namespace ArtificialLife.Core;

public interface ISimulationCommand { void Apply(PortableSimulation simulation); }
public interface ISimulationObserver { void OnStepCompleted(SimulationSnapshot snapshot); }

public sealed record AgentSnapshot(EntityId Id, string Name, bool Alive, string Action, IReadOnlyDictionary<string, double> Needs, long MoneyCents);
public sealed record SimulationSnapshot(long Tick, ulong Fingerprint, IReadOnlyList<AgentSnapshot> Agents, IReadOnlyList<LifeEvent> Events);

public sealed class PortableSimulation
{
    private readonly SortedDictionary<EntityId, PersonProfile> _profiles = new();
    private readonly Queue<ISimulationCommand> _commands = new();
    private readonly List<ISimulationObserver> _observers = new();
    private double _accumulator;
    public PortableSimulation(ulong seed, double fixedStepHours = 1d / 600)
    {
        World = new LifeWorld(seed); FixedStepHours = fixedStepHours > 0 ? fixedStepHours : throw new ArgumentOutOfRangeException(nameof(fixedStepHours));
    }
    public LifeWorld World { get; }
    public double FixedStepHours { get; }
    public HouseholdCareNetwork Care { get; } = new();
    public RegionalNetworkSystem Regions { get; } = new();
    public IReadOnlyDictionary<EntityId, PersonProfile> Profiles => _profiles;
    public void Add(Agent agent, PersonProfile? profile = null)
    {
        World.AddAgent(agent); _profiles.Add(agent.Id, profile ?? new PersonProfile(agent.Id));
    }
    public void Enqueue(ISimulationCommand command) => _commands.Enqueue(command ?? throw new ArgumentNullException(nameof(command)));
    public void Observe(ISimulationObserver observer) => _observers.Add(observer ?? throw new ArgumentNullException(nameof(observer)));
    public int Advance(double elapsedHours)
    {
        if (elapsedHours < 0) throw new ArgumentOutOfRangeException(nameof(elapsedHours));
        _accumulator += elapsedHours; int steps = 0;
        while (_accumulator + 1e-12 >= FixedStepHours)
        {
            while (_commands.Count > 0) _commands.Dequeue().Apply(this);
            World.Step(FixedStepHours); _accumulator -= FixedStepHours; steps++;
            var snapshot = Snapshot(); foreach (var observer in _observers.ToArray()) observer.OnStepCompleted(snapshot);
        }
        return steps;
    }
    public SimulationSnapshot Snapshot()
    {
        var agents = World.Agents.Values.Select(a => new AgentSnapshot(a.Id, a.Name, a.IsAlive, a.CurrentAction,
            a.Needs.All.ToDictionary(n => n.Id, n => n.Value, StringComparer.Ordinal), World.Economy.HasAccount(a.Id) ? World.Economy.BalanceOf(a.Id).Cents : 0)).ToArray();
        return new(World.Tick, World.Fingerprint(), agents, World.Events.ToArray());
    }
}

public interface IArtificialLifeAdapter
{
    void Spawn(AgentSnapshot agent);
    void Despawn(EntityId id);
    void Present(SimulationSnapshot snapshot);
}

public sealed class SimulationPresenter : ISimulationObserver
{
    private readonly IArtificialLifeAdapter _adapter;
    private readonly HashSet<EntityId> _visible = new();
    public SimulationPresenter(IArtificialLifeAdapter adapter) => _adapter = adapter;
    public void OnStepCompleted(SimulationSnapshot snapshot)
    {
        var current = snapshot.Agents.Where(x => x.Alive).Select(x => x.Id).ToHashSet();
        foreach (var agent in snapshot.Agents.Where(x => x.Alive && !_visible.Contains(x.Id))) _adapter.Spawn(agent);
        foreach (var id in _visible.Where(x => !current.Contains(x)).ToArray()) _adapter.Despawn(id);
        _visible.Clear(); foreach (var id in current) _visible.Add(id); _adapter.Present(snapshot);
    }
}
