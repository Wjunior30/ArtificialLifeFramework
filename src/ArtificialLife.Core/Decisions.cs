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

    /// <summary>Score bonus for the action the agent is already doing (hysteresis). 0 keeps legacy behaviour.</summary>
    public double CommitmentBonus { get; set; }
    /// <summary>Picks randomly, weighted by score, among the best N actions. 1 keeps legacy behaviour.</summary>
    public int RandomTopN { get; set; } = 1;

    public IUtilityAction Choose(DecisionContext context)
    {
        var scored = new List<(IUtilityAction Action, double Score)>(_actions.Length);
        int best = -1;
        foreach (var action in _actions)
        {
            if (!action.CanStart(context)) continue;
            double score = action.Score(context);
            if (action.Id == context.Agent.CurrentAction) score += CommitmentBonus;
            scored.Add((action, score));
            if (best < 0 || score > scored[best].Score) best = scored.Count - 1;
        }
        if (best < 0) throw new InvalidOperationException("No available action.");
        if (RandomTopN <= 1) return scored[best].Action;
        var top = scored.OrderByDescending(x => x.Score).ThenBy(x => x.Action.Id, StringComparer.Ordinal).Take(RandomTopN).ToArray();
        double floor = top[top.Length - 1].Score, total = 0;
        foreach (var item in top) total += item.Score - floor + 1e-6;
        double roll = context.Random.NextDouble() * total;
        foreach (var item in top) { roll -= item.Score - floor + 1e-6; if (roll <= 0) return item.Action; }
        return top[0].Action;
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

