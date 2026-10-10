namespace ArtificialLife.Core;

public readonly record struct Vec2(double X, double Y)
{
    public double DistanceTo(Vec2 other) { double x = X - other.X, y = Y - other.Y; return Math.Sqrt(x * x + y * y); }
}

public enum Occupation { Dependent, Farmer, WaterOperator, Cook, Merchant, Builder, Healer, Guard, Miner, Transporter, Teacher, Artisan }
public enum Gender { Unspecified, Female, Male, NonBinary }
public enum Skill { Farming, Cooking, Medicine, Construction, Trade, Security, Mining, Logistics, Teaching, Crafting, Endurance }
public enum BodyPart { Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg }
public enum InjurySeverity { None, Minor, Moderate, Severe, Critical }

public sealed class BodyState
{
    private readonly Dictionary<BodyPart, InjurySeverity> _injuries = new();
    public double Health { get; private set; } = 1;
    public IReadOnlyDictionary<BodyPart, InjurySeverity> Injuries => _injuries;
    public void Injure(BodyPart part, InjurySeverity severity)
    {
        _injuries[part] = severity;
        Health = Math.Max(0, Health - severity switch { InjurySeverity.Minor => .04, InjurySeverity.Moderate => .12, InjurySeverity.Severe => .28, InjurySeverity.Critical => .55, _ => 0 });
    }
    public void Heal(double amount)
    {
        Health = Math.Min(1, Health + Math.Max(0, amount));
        if (Health > .85) _injuries.Clear();
    }
    public double MovementMultiplier => Math.Max(.2, 1 - _injuries.Where(x => x.Key is BodyPart.LeftLeg or BodyPart.RightLeg).Sum(x => (int)x.Value * .12));
    public double WorkMultiplier => Math.Max(.15, Health * (1 - _injuries.Sum(x => (int)x.Value * .035)));
}

public sealed record Goal(string Id, double Priority, long ExpiresAtTick = long.MaxValue);

public sealed class PersonProfile
{
    private readonly Dictionary<Skill, double> _skills = new();
    private readonly Dictionary<string, double> _traits = new(StringComparer.Ordinal);
    private readonly HashSet<string> _knowledge = new(StringComparer.Ordinal);
    private readonly List<Goal> _goals = new();
    public PersonProfile(EntityId agentId) { AgentId = agentId; }
    public EntityId AgentId { get; }
    public Gender Gender { get; set; }
    public Occupation Occupation { get; set; }
    public int AgeYears { get; set; } = 18;
    public Vec2 Position { get; set; }
    public EntityId? HouseholdId { get; set; }
    public EntityId? EmployerId { get; set; }
    public EntityId? FactionId { get; set; }
    public bool IsCaptive { get; set; }
    public double CrimeRisk { get; set; }
    public BodyState Body { get; } = new();
    public IReadOnlyList<Goal> Goals => _goals;
    public IEnumerable<string> Knowledge => _knowledge.OrderBy(x => x, StringComparer.Ordinal);
    public double SkillLevel(Skill skill) => _skills.TryGetValue(skill, out var value) ? value : 0;
    public void SetSkill(Skill skill, double value) => _skills[skill] = Clamp01(value);
    public double Trait(string id) => _traits.TryGetValue(id, out var value) ? value : 0;
    public void SetTrait(string id, double value) => _traits[id] = Math.Max(-1, Math.Min(1, value));
    public void Learn(string fact) => _knowledge.Add(fact);
    public void AddGoal(Goal goal) { _goals.RemoveAll(x => x.Id == goal.Id); _goals.Add(goal); }
    public void RemoveExpiredGoals(long tick) => _goals.RemoveAll(x => x.ExpiresAtTick <= tick);
    private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));
}

public sealed record ActionEvaluation(string ActionId, double Score, IReadOnlyDictionary<string, double> Reasons);

public sealed class CommitmentState
{
    public string ActionId { get; private set; } = "idle";
    public long StartedAtTick { get; private set; }
    public long MinimumUntilTick { get; private set; }
    public void Start(string actionId, long tick, long minimumTicks) { ActionId = actionId; StartedAtTick = tick; MinimumUntilTick = tick + Math.Max(0, minimumTicks); }
    public bool CanSwitch(long tick, double currentScore, double candidateScore, double margin) => tick >= MinimumUntilTick && candidateScore >= currentScore + margin;
}

public sealed class AutonomousDecisionSystem
{
    public double EmergencyThreshold { get; set; } = .20;
    public double SwitchMargin { get; set; } = .12;
    public long MinimumCommitmentTicks { get; set; } = 20;

    public ActionEvaluation Evaluate(Agent agent, PersonProfile person, string actionId, IReadOnlyDictionary<string, double> needEffects, double opportunity, double danger = 0)
    {
        var reasons = new SortedDictionary<string, double>(StringComparer.Ordinal);
        double score = opportunity - danger * (1 + Math.Max(0, person.Trait("cautious")));
        foreach (var effect in needEffects.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!agent.Needs.TryGet(effect.Key, out var need)) continue;
            double urgency = need!.Urgency;
            double weighted = effect.Value * urgency * urgency;
            if (need.Value <= EmergencyThreshold) weighted *= 2.5;
            reasons[effect.Key] = weighted; score += weighted;
        }
        score += person.Body.WorkMultiplier - 1;
        reasons["opportunity"] = opportunity;
        reasons["danger"] = -danger;
        return new(actionId, score, reasons);
    }

    public ActionEvaluation Choose(IEnumerable<ActionEvaluation> candidates, CommitmentState commitment, long tick)
    {
        var ordered = candidates.OrderByDescending(x => x.Score).ThenBy(x => x.ActionId, StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0) throw new InvalidOperationException("No decisions available.");
        var current = ordered.FirstOrDefault(x => x.ActionId == commitment.ActionId);
        var best = ordered[0];
        if (current is not null && best.ActionId != current.ActionId && !commitment.CanSwitch(tick, current.Score, best.Score, SwitchMargin)) return current;
        if (best.ActionId != commitment.ActionId) commitment.Start(best.ActionId, tick, MinimumCommitmentTicks);
        return best;
    }
}
