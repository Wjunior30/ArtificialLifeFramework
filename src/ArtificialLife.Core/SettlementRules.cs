namespace ArtificialLife.Core;

public enum DiplomacyMode { Legacy, Negotiated }

/// <summary>Immutable rules captured with each save. Legacy diplomacy preserves permanent-hostility behavior.</summary>
public sealed record SettlementRules
{
    public int SiteCount { get; init; } = 18;
    public int InitialPopulation { get; init; } = 42;
    public int InitialSettlements { get; init; } = 3;
    public int FounderCount { get; init; } = 7;
    public int MaximumPopulation { get; init; } = 90;
    public int HamletPopulation { get; init; } = 8;
    public int VillagePopulation { get; init; } = 16;
    public int TownPopulation { get; init; } = 30;
    public int CityPopulation { get; init; } = 45;
    public int HamletBuildings { get; init; } = 3;
    public int VillageBuildings { get; init; } = 8;
    public int TownBuildings { get; init; } = 18;
    public int CityBuildings { get; init; } = 30;
    public int FoundationIntervalDays { get; init; } = 30;
    public int DiplomacyIntervalDays { get; init; } = 7;
    public int MigrationIntervalDays { get; init; } = 14;
    public int RuinMinimumDays { get; init; } = 45;
    public int RuinMaximumDays { get; init; } = 120;
    public double WorkerFraction { get; init; } = .66;
    public double FoodConsumptionPerPerson { get; init; } = .72;
    public double BirthProbability { get; init; } = .013;
    public double ProsperityBirthBonus { get; init; } = .00005;
    public double BuildingBaseCost { get; init; } = 8;
    public double BuildingCostGrowth { get; init; } = 1.45;
    public double WarThreshold { get; init; } = -45;
    public double AllianceThreshold { get; init; } = 55;
    public DiplomacyMode Diplomacy { get; init; } = DiplomacyMode.Legacy;
    public int MaximumWarDays { get; init; } = 180;
    public int TruceDays { get; init; } = 120;
    public double ExhaustionPerBattle { get; init; } = 18;
    public double ExhaustionRecoveryPerDay { get; init; } = .2;
    public double PeaceExhaustion { get; init; } = 60;
    public double BattleFoodCostPerPerson { get; init; } = .2;
    public double MinimumWarFoodPerPerson { get; init; } = .5;

    public int MigrationMinimumPopulation { get; init; } = 42;
    public int MigrationRemainingPopulation { get; init; } = 1;
    public int MigrationNomadLimit { get; init; } = 14;
    public double MigrationMinimumFood { get; init; } = 55;
    public double MigrationProbability { get; init; } = .10;
    public bool AllowStarvationExtinction { get; init; }
    public double FoodStoragePerPerson { get; init; }
    public double MaterialStoragePerPerson { get; init; }
    public double FoodSpoilagePerDay { get; init; }
    public double BuildingMaintenancePerDay { get; init; }

    /// <summary>Opt-in bounded resources and extinction; Legacy defaults remain available.</summary>
    public static SettlementRules Sustainable => new()
    {
        Diplomacy = DiplomacyMode.Negotiated, AllowStarvationExtinction = true,
        FoodStoragePerPerson = 120, MaterialStoragePerPerson = 80,
        FoodSpoilagePerDay = .001, BuildingMaintenancePerDay = .02,
        TruceDays = 240, MaximumWarDays = 90
    };

    internal int[] PopulationThresholds => new[] { 0, HamletPopulation, VillagePopulation, TownPopulation, CityPopulation };
    internal int[] BuildingThresholds => new[] { 0, HamletBuildings, VillageBuildings, TownBuildings, CityBuildings };

    public void Validate()
    {
        if (SiteCount < 1 || SiteCount > 512 || InitialPopulation < 1 || InitialPopulation > 1_000_000 ||
            FounderCount < 1 || FounderCount > MaximumPopulation || MaximumPopulation > 100_000 ||
            InitialSettlements < 1 || InitialSettlements > SiteCount || (long)InitialSettlements * FounderCount > InitialPopulation)
            throw new ArgumentException("Invalid settlement population or site limits.");
        foreach (int interval in new[] { FoundationIntervalDays, DiplomacyIntervalDays, MigrationIntervalDays, RuinMinimumDays, RuinMaximumDays, MaximumWarDays, TruceDays })
            if (interval < 1 || interval > 1_000_000) throw new ArgumentException("Invalid settlement interval.");
        if (RuinMinimumDays > RuinMaximumDays || !Enum.IsDefined(typeof(DiplomacyMode), Diplomacy)) throw new ArgumentException("Invalid diplomacy or ruin rules.");
        if (MigrationMinimumPopulation < 1 || MigrationRemainingPopulation < 1 || MigrationRemainingPopulation > MaximumPopulation ||
            MigrationNomadLimit < 0 || !double.IsFinite(MigrationProbability) || MigrationProbability < 0 || MigrationProbability > 1)
            throw new ArgumentException("Invalid migration rules.");
        foreach (double value in new[] { MigrationMinimumFood, FoodStoragePerPerson, MaterialStoragePerPerson, FoodSpoilagePerDay, BuildingMaintenancePerDay })
            if (!double.IsFinite(value) || value < 0 || value > 1_000_000) throw new ArgumentException("Invalid resource rule.");
        if (FoodSpoilagePerDay > 1) throw new ArgumentException("Invalid spoilage probability.");
        ValidateThresholds(PopulationThresholds); ValidateThresholds(BuildingThresholds);
        if (CityPopulation > MaximumPopulation || CityBuildings > MaximumPopulation - 1) throw new ArgumentException("City thresholds exceed population capacity.");
        foreach (double value in new[] { WorkerFraction, FoodConsumptionPerPerson, BirthProbability, ProsperityBirthBonus, BuildingBaseCost,
            BuildingCostGrowth, ExhaustionPerBattle, ExhaustionRecoveryPerDay, PeaceExhaustion, BattleFoodCostPerPerson, MinimumWarFoodPerPerson })
            if (!double.IsFinite(value) || value < 0 || value > 1000) throw new ArgumentException("Invalid finite settlement rule.");
        if (WorkerFraction > 1 || BirthProbability + 100 * ProsperityBirthBonus > 1 || BuildingBaseCost <= 0 ||
            PeaceExhaustion <= 0 || PeaceExhaustion > 100 || !double.IsFinite(WarThreshold) || !double.IsFinite(AllianceThreshold) ||
            WarThreshold < -100 || AllianceThreshold > 100 || WarThreshold >= AllianceThreshold)
            throw new ArgumentException("Invalid settlement probability, threshold or cost.");
    }
    private static void ValidateThresholds(int[] values)
    { for (int i = 1; i < values.Length; i++) if (values[i] <= values[i - 1] || values[i] > 100_000) throw new ArgumentException("Stage thresholds must increase."); }

    internal void WriteState(StateWriter w)
    {
        // Fixed field order forms part of save schema 1.
        foreach (int x in new[] { SiteCount, InitialPopulation, InitialSettlements, FounderCount, MaximumPopulation,
            HamletPopulation, VillagePopulation, TownPopulation, CityPopulation, HamletBuildings, VillageBuildings, TownBuildings, CityBuildings,
            FoundationIntervalDays, DiplomacyIntervalDays, MigrationIntervalDays, RuinMinimumDays, RuinMaximumDays }) w.Write(x);
        foreach (double x in new[] { WorkerFraction, FoodConsumptionPerPerson, BirthProbability, ProsperityBirthBonus, BuildingBaseCost,
            BuildingCostGrowth, WarThreshold, AllianceThreshold }) w.Write(x);
        w.Write((int)Diplomacy); w.Write(MaximumWarDays); w.Write(TruceDays);
        foreach (double x in new[] { ExhaustionPerBattle, ExhaustionRecoveryPerDay, PeaceExhaustion, BattleFoodCostPerPerson, MinimumWarFoodPerPerson }) w.Write(x);
    }
    internal void WriteAdditionalState(StateWriter w)
    {
        w.Write(MigrationMinimumPopulation); w.Write(MigrationRemainingPopulation); w.Write(MigrationNomadLimit);
        w.Write(MigrationMinimumFood); w.Write(MigrationProbability); w.Write(AllowStarvationExtinction);
        w.Write(FoodStoragePerPerson); w.Write(MaterialStoragePerPerson); w.Write(FoodSpoilagePerDay); w.Write(BuildingMaintenancePerDay);
    }
    internal SettlementRules ReadAdditionalState(StateReader r)
    {
        var rules = this with
        {
            MigrationMinimumPopulation = r.Int(1), MigrationRemainingPopulation = r.Int(1), MigrationNomadLimit = r.Int(),
            MigrationMinimumFood = r.Number(), MigrationProbability = r.Number(0, 1), AllowStarvationExtinction = r.ReadBoolean(),
            FoodStoragePerPerson = r.Number(), MaterialStoragePerPerson = r.Number(), FoodSpoilagePerDay = r.Number(0, 1), BuildingMaintenancePerDay = r.Number()
        };
        rules.Validate(); return rules;
    }
    internal static SettlementRules ReadState(StateReader r)
    {
        var rules = new SettlementRules
        {
            SiteCount = r.Int(), InitialPopulation = r.Int(), InitialSettlements = r.Int(), FounderCount = r.Int(), MaximumPopulation = r.Int(),
            HamletPopulation = r.Int(), VillagePopulation = r.Int(), TownPopulation = r.Int(), CityPopulation = r.Int(),
            HamletBuildings = r.Int(), VillageBuildings = r.Int(), TownBuildings = r.Int(), CityBuildings = r.Int(),
            FoundationIntervalDays = r.Int(), DiplomacyIntervalDays = r.Int(), MigrationIntervalDays = r.Int(), RuinMinimumDays = r.Int(), RuinMaximumDays = r.Int(),
            WorkerFraction = r.Number(), FoodConsumptionPerPerson = r.Number(), BirthProbability = r.Number(), ProsperityBirthBonus = r.Number(),
            BuildingBaseCost = r.Number(), BuildingCostGrowth = r.Number(), WarThreshold = r.Number(-100, 100), AllianceThreshold = r.Number(-100, 100),
            Diplomacy = r.EnumValue<DiplomacyMode>(), MaximumWarDays = r.Int(), TruceDays = r.Int(), ExhaustionPerBattle = r.Number(),
            ExhaustionRecoveryPerDay = r.Number(), PeaceExhaustion = r.Number(), BattleFoodCostPerPerson = r.Number(), MinimumWarFoodPerPerson = r.Number()
        };
        rules.Validate(); return rules;
    }
}

public sealed record ConflictSnapshot(int First, int Second, int StartedDay, int EndedDay, int TruceUntilDay, bool Active);

public sealed partial class SettlementSimulation
{
    private readonly Dictionary<(int, int), ConflictSnapshot> _conflicts = new();
    public IReadOnlyList<ConflictSnapshot> Conflicts => _conflicts.OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2).Select(x => x.Value).ToArray();

    private void DeclareWar(EmergentSettlement first, EmergentSettlement second)
    {
        var key = Key(first.Id, second.Id);
        if (_conflicts.TryGetValue(key, out var previous) && (previous.Active || Day < previous.TruceUntilDay)) return;
        _conflicts[key] = new(key.Item1, key.Item2, Day, 0, 0, true);
        Statistics.Wars++; _history.Add(new(Day, "war", first.Id, second.Id));
    }
    private void EndWar((int, int) key, bool truce)
    {
        if (!_conflicts.TryGetValue(key, out var war) || !war.Active) return;
        _conflicts[key] = war with { Active = false, EndedDay = Day, TruceUntilDay = truce ? Day + _rules.TruceDays : 0 };
        Statistics.CompletedWars++; Statistics.TotalWarDays += Day - war.StartedDay;
        if (truce)
        {
            Statistics.Peaces++; _relations[key] = 0;
            _history.Add(new(Day, "peace", key.Item1, key.Item2));
        }
    }
    private bool ShouldMakePeace(EmergentSettlement first, EmergentSettlement second, ConflictSnapshot war)
        => Day - war.StartedDay >= _rules.MaximumWarDays || first.WarExhaustion >= _rules.PeaceExhaustion || second.WarExhaustion >= _rules.PeaceExhaustion ||
           first.Food < first.Population * _rules.MinimumWarFoodPerPerson || second.Food < second.Population * _rules.MinimumWarFoodPerPerson;
}
