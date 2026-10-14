namespace ArtificialLife.Core;

/// <summary>Immutable input to game-owned rules. Rules must use only supplied state and random values.</summary>
public sealed record SettlementState(int Id, int SiteId, int Population, int Buildings, SettlementStage Stage, double Food, double Materials, double Defense)
{
    internal static SettlementState From(EmergentSettlement town) => new(town.Id, town.SiteId, town.Population, town.Buildings, town.Stage, town.Food, town.Materials, town.Defense);
}
public sealed record SettlementContext(int Day, IReadOnlyList<SettlementSite> Sites, IReadOnlyList<SettlementState> Settlements, SettlementRules Rules);
public sealed record ConflictContext(int Day, SettlementState First, SettlementState Second, SettlementRules Rules);
public sealed record ConflictResult(bool FirstWins, int WinnerDeaths, int LoserDeaths, int BuildingsLost, double DefenseLost);

/// <summary>Return an application-owned, versioned codec ID and independent payload for saves.</summary>
public interface ISettlementExtension { ExtensionState CaptureState(); }
public interface ISettlementSiteEvaluator : ISettlementExtension { double Evaluate(SettlementSite site, SettlementContext context); }
public interface IConflictResolver : ISettlementExtension { ConflictResult Resolve(ConflictContext context, ref DeterministicRandom random); }
public interface ISettlementProgression : ISettlementExtension { SettlementStage Evaluate(SettlementState settlement, SettlementRules rules); }
/// <summary>Game-owned daily effects such as climate, disease, culture or government; effects are audited by the core.</summary>
public interface ISettlementDailyRule : ISettlementExtension
{
    SettlementEffect Evaluate(SettlementState settlement, SettlementContext context, ref DeterministicRandom random);
}
public sealed record SettlementEffect(double Food = 0, double Materials = 0, double Prosperity = 0, int Births = 0, int Deaths = 0);

public sealed class DefaultSettlementSiteEvaluator : ISettlementSiteEvaluator
{
    public ExtensionState CaptureState() => new("core.site.v1", Array.Empty<byte>());
    public double Evaluate(SettlementSite site, SettlementContext context)
    {
        double crowding = context.Settlements.Sum(x => Math.Max(0, 1 - site.Position.DistanceTo(context.Sites[x.SiteId].Position) / 900));
        return site.Quality - crowding * .18;
    }
}
public sealed class DefaultConflictResolver : IConflictResolver
{
    public ExtensionState CaptureState() => new("core.conflict.v1", Array.Empty<byte>());
    public ConflictResult Resolve(ConflictContext context, ref DeterministicRandom random)
    {
        double a = context.First.Population * .32 + context.First.Defense + random.NextDouble() * 8;
        double b = context.Second.Population * .32 + context.Second.Defense + random.NextDouble() * 8;
        bool firstWins = a >= b;
        var winner = firstWins ? context.First : context.Second;
        var loser = firstWins ? context.Second : context.First;
        return new(firstWins, Math.Min(winner.Population - 1, random.Next(3)), Math.Min(loser.Population - 1, 2 + random.Next(5)), 2 + random.Next(5), 1 + random.NextDouble() * 2);
    }
}
public sealed class DefaultSettlementProgression : ISettlementProgression
{
    public ExtensionState CaptureState() => new("core.progression.v1", Array.Empty<byte>());
    public SettlementStage Evaluate(SettlementState settlement, SettlementRules rules)
    {
        int stage = 0; var population = rules.PopulationThresholds; var buildings = rules.BuildingThresholds;
        for (int i = 1; i < population.Length; i++) if (settlement.Population >= population[i] && settlement.Buildings >= buildings[i]) stage = i;
        return (SettlementStage)stage;
    }
}

/// <summary>Replaceable policies are serialized with the simulation, including their private state.</summary>
public sealed class SettlementPolicies
{
    public ISettlementSiteEvaluator SiteEvaluator { get; init; } = new DefaultSettlementSiteEvaluator();
    public IConflictResolver ConflictResolver { get; init; } = new DefaultConflictResolver();
    public ISettlementProgression Progression { get; init; } = new DefaultSettlementProgression();
    public IReadOnlyList<ISettlementDailyRule> DailyRules { get; init; } = Array.Empty<ISettlementDailyRule>();
    internal void Validate()
    {
        if (SiteEvaluator is null || ConflictResolver is null || Progression is null || DailyRules is null || DailyRules.Count > 1000 || DailyRules.Any(x => x is null)) throw new ArgumentException("All policies are required; at most 1000 daily rules are supported.");
    }
    internal SettlementPolicies Freeze() => new() { SiteEvaluator = SiteEvaluator, ConflictResolver = ConflictResolver, Progression = Progression, DailyRules = Array.AsReadOnly(DailyRules.ToArray()) };
    internal void WriteState(StateWriter w)
    {
        w.Extension(SiteEvaluator.CaptureState()); w.Extension(ConflictResolver.CaptureState()); w.Extension(Progression.CaptureState());
        w.Write(DailyRules.Count); foreach (var rule in DailyRules) w.Extension(rule.CaptureState());
    }
    internal static SettlementPolicies ReadState(StateReader r, RestoreHandlers? handlers)
    {
        ISettlementExtension Decode(ExtensionState state)
        {
            if (state.Kind.StartsWith("core.", StringComparison.Ordinal) && state.Payload.Length != 0) throw new ArgumentException("Invalid built-in policy payload.");
            return state.Kind switch
            {
                "core.site.v1" => new DefaultSettlementSiteEvaluator(),
                "core.conflict.v1" => new DefaultConflictResolver(),
                "core.progression.v1" => new DefaultSettlementProgression(),
                _ => handlers?.SettlementPolicy?.Invoke(state) ?? throw new ArgumentException("A settlement policy restore handler is required.")
            };
        }
        var site = Decode(r.Extension()) as ISettlementSiteEvaluator ?? throw new ArgumentException("Wrong site policy type.");
        var conflict = Decode(r.Extension()) as IConflictResolver ?? throw new ArgumentException("Wrong conflict policy type.");
        var progression = Decode(r.Extension()) as ISettlementProgression ?? throw new ArgumentException("Wrong progression policy type.");
        int count = r.Int(0, 1000); var daily = new List<ISettlementDailyRule>();
        for (int i = 0; i < count; i++) daily.Add(Decode(r.Extension()) as ISettlementDailyRule ?? throw new ArgumentException("Wrong daily policy type."));
        return new()
        {
            SiteEvaluator = site, ConflictResolver = conflict, Progression = progression, DailyRules = daily.AsReadOnly()
        };
    }
}
