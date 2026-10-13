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
    public bool IsAlive { get; internal set; } = true;
}

public sealed record SettlementHistoryEvent(int Day, string Kind, int SettlementId, int OtherId = 0, int People = 0);

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
    public int Migrations { get; internal set; }
    public int PeakSettlements { get; internal set; }
    public int PeakPopulation { get; internal set; }
}

public sealed record SettlementSimulationOptions(double FoodMultiplier = 1, double ConflictMultiplier = 1);

public sealed class SettlementSimulation
{
    private const int FounderCount = 7;
    private static readonly int[] StagePopulation = [0, 8, 16, 30, 45];
    private static readonly int[] StageBuildings = [0, 3, 8, 18, 30];
    private DeterministicRandom _random;
    private readonly Dictionary<(int, int), double> _relations = new();
    private readonly SettlementSimulationOptions _options;
    private int _nextId = 1;

    public SettlementSimulation(ulong seed, SettlementSimulationOptions? options = null)
    {
        Seed = seed; _random = new(seed); _options = options ?? new();
        BuildSites();
        for (int i = 0; i < 3; i++) TryFoundSettlement(true);
    }

    public ulong Seed { get; }
    public int Day { get; private set; }
    public int Nomads { get; private set; } = 42;
    public List<SettlementSite> Sites { get; } = [];
    public List<EmergentSettlement> Settlements { get; } = [];
    public List<SettlementHistoryEvent> History { get; } = [];
    public SettlementStatistics Statistics { get; } = new();

    public void Advance(int days)
    {
        if (days < 0) throw new ArgumentOutOfRangeException(nameof(days));
        for (int i = 0; i < days; i++) AdvanceDay();
    }

    public IReadOnlyList<string> Audit()
    {
        var errors = new List<string>(); var occupied = new HashSet<int>();
        foreach (EmergentSettlement settlement in Settlements)
        {
            if (settlement.Population <= 0) errors.Add($"Settlement {settlement.Id} has no population.");
            if (settlement.Buildings < 0 || settlement.Food < 0 || settlement.Materials < 0) errors.Add($"Settlement {settlement.Id} has negative state.");
            if (!occupied.Add(settlement.SiteId)) errors.Add($"Site {settlement.SiteId} has multiple settlements.");
            if (Sites[settlement.SiteId].OccupiedBy != settlement.Id) errors.Add($"Site ownership mismatch for {settlement.Id}.");
        }
        return errors;
    }

    public ulong Fingerprint()
    {
        ulong hash = StableHash.Combine(Seed, (ulong)Day, (ulong)Nomads);
        foreach (EmergentSettlement settlement in Settlements.OrderBy(x => x.Id))
            hash = StableHash.Combine(hash, (ulong)settlement.Id, (ulong)settlement.SiteId, (ulong)settlement.Population, (ulong)settlement.Buildings, (ulong)settlement.Stage);
        hash = StableHash.Combine(hash, (ulong)Statistics.Founded, (ulong)Statistics.Destroyed, (ulong)Statistics.Battles);
        return hash;
    }

    private void BuildSites()
    {
        for (int index = 0; index < 18; index++)
        {
            double angle = Math.PI * 2 * index / 18 + Range(-.12, .12);
            double ring = 700 + index % 3 * 620 + Range(-120, 120);
            Sites.Add(new SettlementSite(index, new Vec2(Math.Cos(angle) * ring, Math.Sin(angle) * ring), Range(.35, 1), Range(.25, 1), Range(.2, 1), Range(.25, 1)));
        }
    }

    private void AdvanceDay()
    {
        Day++;
        foreach (EmergentSettlement settlement in Settlements.ToArray()) AdvanceSettlement(settlement);
        if (Day % 7 == 0) { AdvanceDiplomacy(); ResolveWars(); }
        if (Day % 14 == 0) RedistributePopulation();
        if (Day % 30 == 0) TryFoundSettlement(false);
        Settlements.RemoveAll(x => !x.IsAlive);
        RemoveDeadRelations(); UpdatePeaks();
    }

    private void AdvanceSettlement(EmergentSettlement settlement)
    {
        SettlementSite site = Sites[settlement.SiteId]; int population = settlement.Population;
        int workers = Math.Max(1, (int)(population * .66));
        double foodOutput = workers * (.92 + site.Fertility * .92) * _options.FoodMultiplier;
        settlement.Food += foodOutput - population * .72;
        settlement.Materials += workers * (.28 + site.Routes * .28);
        settlement.Prosperity = Math.Clamp(settlement.Prosperity + (foodOutput / Math.Max(1, population) - .78) * .12, 0, 100);
        if (settlement.Food < 0)
        {
            int deaths = Math.Min(population - 1, Math.Max(0, (int)Math.Ceiling(-settlement.Food / 8)));
            settlement.Population -= deaths; Statistics.Deaths += deaths; settlement.Food = 0;
        }
        else if (population < 90 && Chance(.013 + settlement.Prosperity * .00005)) settlement.Population++;
        double cost = 8 + settlement.Buildings * 1.45;
        if (settlement.Materials >= cost && settlement.Population >= settlement.Buildings + 2)
        {
            settlement.Materials -= cost; settlement.Buildings++;
            settlement.Defense += settlement.Stage < SettlementStage.Town ? .18 : .32;
        }
        UpdateStage(settlement);
    }

    private void UpdateStage(EmergentSettlement settlement)
    {
        int target = 0;
        for (int i = 1; i < StagePopulation.Length; i++)
            if (settlement.Population >= StagePopulation[i] && settlement.Buildings >= StageBuildings[i]) target = i;
        if (target <= (int)settlement.Stage) return;
        settlement.Stage = (SettlementStage)target; Statistics.Upgrades++;
        History.Add(new(Day, "upgrade", settlement.Id));
    }

    private bool TryFoundSettlement(bool initial)
    {
        if (Nomads < FounderCount) return false;
        var candidates = Sites.Where(x => x.OccupiedBy == 0 && Day >= x.RuinedUntilDay)
            .Select(x => (Site: x, Score: SiteScore(x) + Range(-.04, .04))).OrderByDescending(x => x.Score).ToArray();
        if (candidates.Length == 0) return false;
        int pick = initial ? 0 : _random.Next(Math.Min(3, candidates.Length)); SettlementSite site = candidates[pick].Site;
        var settlement = new EmergentSettlement(_nextId++, site.Id, Day) { Defense = site.Defense * 2 };
        Nomads -= FounderCount; site.OccupiedBy = settlement.Id; Settlements.Add(settlement);
        foreach (EmergentSettlement other in Settlements.Where(x => x != settlement))
            _relations[Key(settlement.Id, other.Id)] = Range(-24, 20) - 18 * Math.Max(0, _options.ConflictMultiplier - 1);
        Statistics.Founded++; if (site.PreviousOwner != 0) Statistics.Refounded++;
        History.Add(new(Day, "foundation", settlement.Id)); return true;
    }

    private double SiteScore(SettlementSite site)
    {
        double crowding = Settlements.Sum(x => Math.Max(0, 1 - Distance(site.Position, Sites[x.SiteId].Position) / 900));
        return site.Quality - crowding * .18;
    }

    private void AdvanceDiplomacy()
    {
        for (int i = 0; i < Settlements.Count; i++) for (int j = i + 1; j < Settlements.Count; j++)
        {
            EmergentSettlement first = Settlements[i], second = Settlements[j]; var key = Key(first.Id, second.Id);
            double previous = _relations.GetValueOrDefault(key); double distance = Distance(Sites[first.SiteId].Position, Sites[second.SiteId].Position);
            double trade = Math.Max(0, 1 - distance / 2800) * 1.2;
            double rivalry = Math.Max(0, 1 - distance / 1300) * 2.5 * _options.ConflictMultiplier;
            double current = Math.Clamp(previous + trade - rivalry - Math.Max(0, _options.ConflictMultiplier - 1) * .65 + Range(-2.8, 2.2) * _options.ConflictMultiplier, -100, 100);
            _relations[key] = current;
            if (previous < 55 && current >= 55) { Statistics.Alliances++; History.Add(new(Day, "alliance", first.Id, second.Id)); }
            if (previous > -45 && current <= -45) { Statistics.Wars++; History.Add(new(Day, "war", first.Id, second.Id)); }
        }
    }

    private void ResolveWars()
    {
        for (int i = 0; i < Settlements.Count; i++) for (int j = i + 1; j < Settlements.Count; j++)
        {
            EmergentSettlement first = Settlements[i], second = Settlements[j];
            if (_relations.GetValueOrDefault(Key(first.Id, second.Id)) > -45 || !Chance(Math.Min(.75, .32 * _options.ConflictMultiplier))) continue;
            ResolveBattle(first, second);
        }
    }

    private void ResolveBattle(EmergentSettlement first, EmergentSettlement second)
    {
        double firstPower = first.Population * .32 + first.Defense + Range(0, 8), secondPower = second.Population * .32 + second.Defense + Range(0, 8);
        EmergentSettlement winner = firstPower >= secondPower ? first : second, loser = winner == first ? second : first;
        int winnerLosses = Math.Min(winner.Population - 1, NextInclusive(0, 2)), loserLosses = Math.Min(loser.Population - 1, NextInclusive(2, 6));
        winner.Population -= winnerLosses; loser.Population -= loserLosses;
        loser.Buildings = Math.Max(0, loser.Buildings - NextInclusive(2, 6)); loser.Defense = Math.Max(0, loser.Defense - Range(1, 3));
        Statistics.Deaths += winnerLosses + loserLosses; Statistics.Battles++;
        History.Add(new(Day, "battle", winner.Id, loser.Id, winnerLosses + loserLosses));
        if (loser.Population < 5 || loser.Buildings <= 1) Destroy(loser, winner.Id);
    }

    private void Destroy(EmergentSettlement settlement, int attacker)
    {
        if (!settlement.IsAlive) return; settlement.IsAlive = false; Nomads += settlement.Population;
        Statistics.Migrations += settlement.Population; Statistics.Destroyed++;
        SettlementSite site = Sites[settlement.SiteId]; site.PreviousOwner = settlement.Id; site.OccupiedBy = 0; site.RuinedUntilDay = Day + NextInclusive(45, 120);
        History.Add(new(Day, "destruction", settlement.Id, attacker, settlement.Population));
    }

    private void RedistributePopulation()
    {
        foreach (EmergentSettlement settlement in Settlements)
            if (settlement.Population >= 42 && settlement.Food >= 55 && Nomads < 14 && Chance(.10))
            { settlement.Population -= FounderCount; Nomads += FounderCount; Statistics.Migrations += FounderCount; History.Add(new(Day, "migration", settlement.Id, People: FounderCount)); }
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
