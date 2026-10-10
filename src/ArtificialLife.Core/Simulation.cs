namespace ArtificialLife.Core;

public sealed class LifeEvent
{
    public LifeEvent(long tick, string kind, EntityId subject, string message)
    { Tick = tick; Kind = kind; Subject = subject; Message = message; }
    public long Tick { get; }
    public string Kind { get; }
    public EntityId Subject { get; }
    public string Message { get; }
}

public sealed class LifeWorld
{
    private readonly SortedDictionary<EntityId, Agent> _agents = new();
    private readonly List<LifeEvent> _events = new();
    private readonly ulong _seed;
    private UtilityBrain? _brain;

    public LifeWorld(ulong seed, long initialTick = 0)
    {
        _seed = StableHash.Mix(seed);
        Tick = initialTick;
        Ids = new IdSequence(_seed);
    }

    public long Tick { get; private set; }
    public IdSequence Ids { get; }
    public Economy Economy { get; } = new();
    public IReadOnlyDictionary<EntityId, Agent> Agents => _agents;
    public IReadOnlyList<LifeEvent> Events => _events;
    public void SetBrain(UtilityBrain brain) => _brain = brain ?? throw new ArgumentNullException(nameof(brain));
    public void AddAgent(Agent agent) => _agents.Add(agent.Id, agent);

    public void Step(double hours)
    {
        if (hours <= 0) throw new ArgumentOutOfRangeException(nameof(hours));
        Tick++;
        foreach (var agent in _agents.Values)
        {
            if (!agent.IsAlive) continue;
            agent.Needs.Tick(hours);
            agent.Emotions.Decay(hours);
            agent.Memories.Decay(0.001 * hours);
            if (_brain is null) continue;
            ulong decisionSeed = StableHash.Combine(_seed, (ulong)Tick, agent.Id.Value);
            var context = new DecisionContext(this, agent, decisionSeed);
            IUtilityAction action = _brain.Choose(context);
            action.Start(context);
            ActionStatus status = action.Tick(context, hours);
            _events.Add(new LifeEvent(Tick, "decision", agent.Id, $"{action.Id}:{status}"));
        }
        Economy.Audit();
    }

    public ulong Fingerprint()
    {
        ulong hash = StableHash.Combine(_seed, (ulong)Tick);
        foreach (var agent in _agents.Values)
        {
            hash = StableHash.Combine(hash, agent.Id.Value, StableHash.Text(agent.CurrentAction));
            foreach (var need in agent.Needs.All)
                hash = StableHash.Combine(hash, StableHash.Text(need.Id), (ulong)Math.Round(need.Value * 1_000_000));
        }
        foreach (var account in Economy.Accounts)
            hash = StableHash.Combine(hash, account.Owner.Value, (ulong)account.Balance.Cents);
        return hash;
    }
}

