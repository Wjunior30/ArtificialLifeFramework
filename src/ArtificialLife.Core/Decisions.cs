namespace ArtificialLife.Core;

public enum ActionStatus { Running, Succeeded, Failed, Cancelled }

public sealed class DecisionContext
{
    public DecisionContext(LifeWorld world, Agent agent, ulong randomSeed)
    { World = world; Agent = agent; Random = new DeterministicRandom(randomSeed); }
    public LifeWorld World { get; }
    public Agent Agent { get; }
    public DeterministicRandom Random;
}

public interface IUtilityAction
{
    string Id { get; }
    bool CanStart(DecisionContext context);
    double Score(DecisionContext context);
    void Start(DecisionContext context);
    ActionStatus Tick(DecisionContext context, double hours);
    void Cancel(DecisionContext context);
}

public sealed partial class UtilityBrain
{
    private readonly IUtilityAction[] _actions;
    public UtilityBrain(IEnumerable<IUtilityAction> actions)
    {
        _actions = actions?.OrderBy(a => a.Id, StringComparer.Ordinal).ToArray() ?? throw new ArgumentNullException(nameof(actions));
        if (_actions.Length == 0) throw new ArgumentException("At least one action is required.", nameof(actions));
    }

    public IUtilityAction Choose(DecisionContext context)
    {
        IUtilityAction? winner = null;
        double best = double.NegativeInfinity;
        foreach (var action in _actions)
        {
            if (!action.CanStart(context)) continue;
            double score = action.Score(context);
            if (score > best) { best = score; winner = action; }
        }
        return winner ?? throw new InvalidOperationException("No available action.");
    }
}

public sealed partial class SatisfyNeedAction : IUtilityAction
{
    private readonly string _need;
    private readonly double _amount;
    public SatisfyNeedAction(string id, string need, double amount) { Id = id; _need = need; _amount = amount; }
    public string Id { get; }
    public bool CanStart(DecisionContext context) => context.Agent.Needs.TryGet(_need, out _);
    public double Score(DecisionContext context) => context.Agent.Needs[_need].Urgency;
    public void Start(DecisionContext context) => context.Agent.CurrentAction = Id;
    public ActionStatus Tick(DecisionContext context, double hours)
    {
        var need = context.Agent.Needs[_need];
        double before = need.Urgency;
        need.Change(_amount * hours);
        context.Agent.Memories.Remember(new Memory(context.World.Tick, "action", Id, before));
        return ActionStatus.Succeeded;
    }
    public void Cancel(DecisionContext context) { }
}

public sealed class IdleAction : IUtilityAction
{
    public string Id => "idle";
    public bool CanStart(DecisionContext context) => true;
    public double Score(DecisionContext context) => 0.01;
    public void Start(DecisionContext context) => context.Agent.CurrentAction = Id;
    public ActionStatus Tick(DecisionContext context, double hours) => ActionStatus.Succeeded;
    public void Cancel(DecisionContext context) { }
}

