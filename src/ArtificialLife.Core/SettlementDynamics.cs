namespace ArtificialLife.Core;

public enum SettlementStage { Camp, Hamlet, Village, Town, City }

public sealed record SettlementSite(int Id, Vec2 Position, double Water, double Fertility, double Defense, double Routes)
{
    public double Quality => Water * .34 + Fertility * .31 + Defense * .17 + Routes * .18;
    public int OccupiedBy { get; internal set; }
    public int PreviousOwner { get; internal set; }
    public int RuinedUntilDay { get; internal set; }
}

public sealed class EmergentSettlement
{
    internal EmergentSettlement(int id, int siteId, int day)
    { Id = id; Name = $"Settlement {id}"; SiteId = siteId; FoundedDay = day; }
    public int Id { get; }
    public string Name { get; }
    public int SiteId { get; }
    public int FoundedDay { get; }
    public int Population { get; internal set; } = 7;
    public int Buildings { get; internal set; } = 1;
    public SettlementStage Stage { get; internal set; }
    public double Food { get; internal set; } = 26;
    public double Materials { get; internal set; } = 5;
    public double Prosperity { get; internal set; } = 45;
    public double Defense { get; internal set; }
    public double WarExhaustion { get; internal set; }
    public bool IsAlive { get; internal set; } = true;
}

public sealed record SettlementHistoryEvent(int Day, string Kind, int SettlementId, int OtherId = 0, int People = 0, int Detail = 0);

public sealed class SettlementStatistics
{
    public int Founded { get; internal set; }
    public int Upgrades { get; internal set; }
    public int Alliances { get; internal set; }
    public int Wars { get; internal set; }
    public int Battles { get; internal set; }
    public int Destroyed { get; internal set; }
    public int Refounded { get; internal set; }
    public int Deaths { get; internal set; }
    public int Births { get; internal set; }
    public int Migrations { get; internal set; }
    public int PeakSettlements { get; internal set; }
    public int PeakPopulation { get; internal set; }
    public long PopulationDays { get; internal set; }
    public long ActiveWarDays { get; internal set; }
    public double FoodLost { get; internal set; }
    public double MaterialsLost { get; internal set; }
    public int Peaces { get; internal set; }
    public int CompletedWars { get; internal set; }
    public long TotalWarDays { get; internal set; }
    public int StarvationDeaths { get; internal set; }
    public long HungryPersonDays { get; internal set; }
    public int ExternalArrivals { get; internal set; }
}

public sealed record SettlementSimulationOptions(double FoodMultiplier = 1, double ConflictMultiplier = 1)
{
    public const double MaximumMultiplier = 1_000;
    public void Validate()
    {
        if (!double.IsFinite(FoodMultiplier) || FoodMultiplier < 0 || FoodMultiplier > MaximumMultiplier)
            throw new ArgumentOutOfRangeException(nameof(FoodMultiplier));
        if (!double.IsFinite(ConflictMultiplier) || ConflictMultiplier < 0 || ConflictMultiplier > MaximumMultiplier)
            throw new ArgumentOutOfRangeException(nameof(ConflictMultiplier));
    }
}

public sealed partial class SettlementSimulation
{
    private SettlementRules _rules;
    private SettlementPolicies _policies;
    private DeterministicRandom _random;
    private readonly Dictionary<(int, int), double> _relations = new();
    private readonly SettlementSimulationOptions _options;
    private readonly List<SettlementSite> _sites = new();
    private readonly List<EmergentSettlement> _settlements = new();
    private readonly List<SettlementHistoryEvent> _history = new();
    private int _nextId = 1;

    public SettlementSimulation(ulong seed, SettlementSimulationOptions? options = null, SettlementRules? rules = null, SettlementPolicies? policies = null)
    {
        Seed = seed; _random = new(seed); _options = options ?? new();
        _options.Validate(); _rules = rules ?? new(); _rules.Validate();
        _policies = policies ?? new(); _policies.Validate();
        _policies = _policies.Freeze();
        Nomads = _rules.InitialPopulation;
        Sites = _sites.AsReadOnly(); Settlements = _settlements.AsReadOnly(); History = _history.AsReadOnly();
        BuildSites();
        for (int i = 0; i < _rules.InitialSettlements; i++) TryFoundSettlement(true);
        UpdatePeaks();
    }

    public ulong Seed { get; }
    public SettlementRules Rules => _rules;
    public int Day { get; private set; }
    public int Nomads { get; private set; } = 42;
    public IReadOnlyList<SettlementSite> Sites { get; }
    public IReadOnlyList<EmergentSettlement> Settlements { get; }
    public IReadOnlyList<SettlementHistoryEvent> History { get; }
    public SettlementStatistics Statistics { get; } = new();

    public void Advance(int days)
    {
        if (days < 0 || (long)Day + days > int.MaxValue - Math.Max(_rules.RuinMaximumDays, _rules.TruceDays)) throw new ArgumentOutOfRangeException(nameof(days));
        for (int i = 0; i < days; i++) AdvanceDay();
    }

    /// <summary>Adds population supplied by the host, allowing an extinct region to be repopulated without violating audits.</summary>
    public void AdmitExternalMigrants(int people)
    {
        if (people < 1 || (long)Nomads + people > 1_000_000) throw new ArgumentOutOfRangeException(nameof(people));
        Nomads = checked(Nomads + people); Statistics.ExternalArrivals = checked(Statistics.ExternalArrivals + people); UpdatePeaks();
    }

    public const int FingerprintVersion = 3;
    public ulong Fingerprint() => Convert.ToUInt64(StateHash().Substring(0, 16), 16);

    private void AdvanceDay()
    {
        Day++;
        Statistics.PopulationDays += Nomads + Settlements.Sum(x => (long)x.Population);
        Statistics.ActiveWarDays += _conflicts.Values.LongCount(x => x.Active);
        foreach (EmergentSettlement settlement in Settlements.ToArray())
        {
            ApplyDailyRules(settlement);
            if (settlement.IsAlive) AdvanceSettlement(settlement);
        }
        _settlements.RemoveAll(x => !x.IsAlive);
        RemoveDeadRelations();
        if (Day % _rules.DiplomacyIntervalDays == 0) { AdvanceDiplomacy(); ResolveWars(); }
        _settlements.RemoveAll(x => !x.IsAlive);
        RemoveDeadRelations();
        if (Day % _rules.MigrationIntervalDays == 0) RedistributePopulation();
        if (Day % _rules.FoundationIntervalDays == 0) TryFoundSettlement(false);
        foreach (var town in Settlements) ApplyStorageLimits(town);
        UpdatePeaks();
    }

    private void RemoveDeadRelations()
    {
        var living = Settlements.Select(x => x.Id).ToHashSet();
        foreach (var key in _relations.Keys.Where(x => !living.Contains(x.Item1) || !living.Contains(x.Item2)).ToArray()) _relations.Remove(key);
    }

    private void UpdatePeaks()
    {
        Statistics.PeakSettlements = Math.Max(Statistics.PeakSettlements, Settlements.Count);
        Statistics.PeakPopulation = Math.Max(Statistics.PeakPopulation, Nomads + Settlements.Sum(x => x.Population));
    }

    private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);
    private bool Chance(double probability) => _random.NextDouble() < probability;
    private double Range(double minimum, double maximum) => minimum + _random.NextDouble() * (maximum - minimum);
    private int NextInclusive(int minimum, int maximum) => minimum + _random.Next(maximum - minimum + 1);
    private static double Distance(Vec2 a, Vec2 b) { double x = a.X - b.X, y = a.Y - b.Y; return Math.Sqrt(x * x + y * y); }
}
