using ArtificialLife.Core;
using Xunit;
using Xunit.Abstractions;

public sealed class IslandLifeTests
{
    readonly ITestOutputHelper output;
    public IslandLifeTests(ITestOutputHelper output) => this.output = output;

    // 20 cities (5 towns first, then 15) plus 3 mines, 2 fishing spots, 2 nests and ruins on a ring of roads, like the island generator.
    static IslandLife Create(ulong seed)
    {
        var kinds = Enumerable.Repeat("Town", 5).Concat(Enumerable.Repeat("Village", 13)).Concat(new[] { "Port", "Farm", "Mine", "Mine", "Mine", "Fishing", "Fishing", "Nest", "Nest", "Ruins" }).ToArray();
        var places = kinds.Select((k, i) => new WorldPlace(i, $"{k} {i}", k, Math.Cos(i * 2 * Math.PI / kinds.Length + (i < 5 ? 0 : .1)) * 4000 * (i < 5 ? 1 : .8 + i % 3 * .1), Math.Sin(i * 2 * Math.PI / kinds.Length) * 4000, k == "Town" ? 2 : i < 20 ? 1 : 0)).ToArray();
        var routes = places.Select(p => new WorldRoute(p.Id, (p.Id + 1) % places.Length, 900)).Concat(places.Where(p => p.Id % 4 == 0).Select(p => new WorldRoute(p.Id, (p.Id + 7) % places.Length, 3000))).ToArray();
        return new IslandLife(seed, places, routes);
    }

    static void Run(IslandLife life, double days) { for (int h = 0; h < days * 24 * 4; h++) life.Step(.25); }

    [Fact]
    public void FiveMilitaryFactionsStartWithFourCitiesAndALeader()
    {
        var life = Create(1);
        var military = life.Factions.Where(f => !f.Profile.Nomadic && !f.Profile.Monsters).ToList();
        Assert.Equal(5, military.Count);
        Assert.All(military, f => Assert.Equal(4, life.Cities.Count(c => c.IsCity && c.Owner == f.Id)));
        Assert.All(military, f => Assert.Single(life.Officers(f.Id), o => o.Rank == Rank.Leader));
        Assert.All(military, f => Assert.Single(life.Cities, c => c.CapitalOf == f.Id));
        Assert.Equal(0, life.Cities.Count(c => c.IsCity && c.Owner == life.Nomads.Id)); // nomads own no city
    }

    [Fact]
    public void SameSeedReplaysTheSameWar()
    {
        IslandLife a = Create(9), b = Create(9);
        Run(a, 10); Run(b, 10);
        Assert.Equal(a.Fingerprint(), b.Fingerprint());
    }

    [Fact]
    public void SavedWarLoadsFromJsonAndContinuesIdentically()
    {
        var json = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var a = Create(5); Run(a, 12);
        var text = System.Text.Json.JsonSerializer.Serialize(a.Save(), json);
        var b = IslandLife.Load(System.Text.Json.JsonSerializer.Deserialize<IslandLifeState>(text, json)!);
        Assert.Equal(text, System.Text.Json.JsonSerializer.Serialize(b.Save(), json));
        Run(a, 15); Run(b, 15);
        Assert.Equal(a.Fingerprint(), b.Fingerprint());
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(a.Save(), json), System.Text.Json.JsonSerializer.Serialize(b.Save(), json));
        output.WriteLine($"save {text.Length / 1024} KB, {a.AllPeople().Count()} people, {a.Events.Count} events");
    }

    [Fact]
    public void EventsCarryFactionsPlaceAndCanBeTranslated()
    {
        var life = Create(3); life.Localize = e => e.Kind == "capture" ? $"{life.Factions[e.FactionA].Profile.Name} took {life.Cities.First(c => c.Place.Id == e.Place).Place.Name}" : null;
        var all = new List<IslandEvent>(); life.EventRaised += all.Add;
        Run(life, 30);
        var captures = all.Where(e => e.Kind == "capture").ToList();
        Assert.NotEmpty(captures);
        Assert.All(captures, e => { Assert.True(e.FactionA >= 0 && e.FactionB >= 0 && e.Place >= 0); Assert.Contains(" took ", e.Text); });
        Assert.All(all.Where(e => e.Kind is "war" or "truce" or "pact"), e => Assert.True(e.FactionA >= 0 && e.FactionB >= 0));
    }

    [Fact]
    public void ThreatenedCitiesReceiveRelief()
    {
        // When raiders march on a city, its faction sends fighters to meet them (relief), across seeds.
        int relief = 0, raids = 0;
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5 })
        {
            var life = Create(seed);
            life.EventRaised += e => { if (e.Text.Contains("saem para socorrer")) relief++; if (e.Kind == "march") raids++; };
            Run(life, 60);
        }
        output.WriteLine($"raids={raids} relief={relief}");
        Assert.True(raids > 0 && relief > 0, $"raids={raids} relief={relief}");
    }

    [Fact]
    public void OfficersRememberThePlayer()
    {
        var life = Create(3);
        var captain = life.Officers(0).First(o => o.Rank == Rank.Captain);
        life.RecordPlayerEncounter(captain, officerWon: false);
        Assert.Contains("Vingativo", captain.Traits);
        Assert.Contains(captain, life.Nemeses);
        life.RecordPlayerEncounter(captain, officerWon: true); life.RecordPlayerEncounter(captain, officerWon: true);
        Assert.Equal(Rank.General, captain.Rank);
    }

    [Fact]
    public void HundredTwentyDayWarStaysAliveWithoutAnEarlyHegemon()
    {
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5 })
        {
            var life = Create(seed); var counts = new Dictionary<string, int>(); double maxShare = 0;
            life.EventRaised += e => counts[e.Kind] = counts.TryGetValue(e.Kind, out var c) ? c + 1 : 1;
            for (int day = 0; day < 120; day++)
            {
                Run(life, 1);
                var mil = life.Factions.Where(f => !f.Profile.Nomadic && !f.Profile.Monsters).ToList();
                double total = mil.Sum(f => f.Power); if (total > 0 && day > 5) maxShare = Math.Max(maxShare, mil.Max(f => f.Power) / total);
                Assert.True(life.Squads.Count <= life.MaxSquads + 2);
            }
            int K(string k) => counts.TryGetValue(k, out var v) ? v : 0;
            var alive = life.Factions.Where(f => !f.Profile.Nomadic && !f.Profile.Monsters && !f.Extinct).ToList();
            output.WriteLine($"seed {seed}: cities {string.Join("/", life.Factions.Take(5).Select(f => f.Extinct ? "x" : f.Cities.ToString()))} maxShare {maxShare:P0} capture {K("capture")} war {K("war")} truce {K("truce")} coalition {K("coalition")} extinct {K("extinct")} promotions {K("promotion")} trade {K("trade")}");
            Assert.True(K("capture") > 5, "Cities must change hands.");
            Assert.True(K("war") > 0 && K("truce") > 0, "Wars must be declared and ended.");
            Assert.True(alive.Count >= 2, "No single faction should conquer the island within 120 days.");
            Assert.True(life.Cities.Where(c => c.IsLair).All(c => c.Owner == life.Monsters.Id), "Lairs are hunted, never held.");
            Assert.True(K("trade") > 0, "Nomads must trade.");
        }
    }
}

public sealed class OtherWorldTests
{
    // A different game made only with data: a continent with three kingdoms, castles, hamlets and quarries, no nomads, no undead.
    public static IslandLifeConfig Continent() => new()
    {
        Factions = new[]
        {
            new FactionData { Id = 0, Name = "Reino do Norte", LeaderName = "Rei Hadran", Ideology = Ideology.Feudalism, Conquest = ConquestPolicy.Exploit, PreferredTargets = new[] { "Quarry" }, Culture = NameCulture.Iron },
            new FactionData { Id = 1, Name = "Liga Mercante", LeaderName = "Doge Ulvar", Ideology = Ideology.Capitalism, Conquest = ConquestPolicy.Integrate, PreferredTargets = new[] { "Hamlet" }, Culture = NameCulture.Tide },
            new FactionData { Id = 2, Name = "Comuna Livre", LeaderName = "Irmã Orvel", Ideology = Ideology.Anarchism, Conquest = ConquestPolicy.Liberate, SwornEnemy = 0, Culture = NameCulture.Free },
        },
        Places = new[]
        {
            new PlaceRule { Kind = "Castle", Production = 1, Capacity = 16, CapitalCandidate = true },
            new PlaceRule { Kind = "Hamlet", Production = 2 },
            new PlaceRule { Kind = "Quarry", Production = .4 },
        },
        Balance = new BalanceData { MaxSquads = 6, OverextensionCities = 4, CoalitionShare = .5 },
    };

    public static (WorldPlace[] Places, WorldRoute[] Routes) Map()
    {
        var kinds = new[] { "Castle", "Castle", "Castle", "Hamlet", "Hamlet", "Hamlet", "Hamlet", "Hamlet", "Hamlet", "Quarry", "Quarry", "Hamlet" };
        var places = kinds.Select((k, i) => new WorldPlace(i, $"{k} {i}", k, Math.Cos(i * 2 * Math.PI / kinds.Length) * 3000, Math.Sin(i * 2 * Math.PI / kinds.Length) * 3000, k == "Castle" ? 2 : k == "Hamlet" ? 1 : 0)).ToArray();
        var routes = places.Select(p => new WorldRoute(p.Id, (p.Id + 1) % places.Length, 1600)).ToArray();
        return (places, routes);
    }

    [Fact]
    public void ContinentWithThreeKingdomsRunsFromDataAlone()
    {
        var (places, routes) = Map();
        var life = new IslandLife(11, places, routes, Continent(), forgeNames: true);
        Assert.Equal(3, life.Factions.Count);
        Assert.All(life.Factions, f => Assert.Single(life.Cities, c => c.CapitalOf == f.Id && c.Place.Kind == "Castle"));
        var kinds = new Dictionary<string, int>(); life.EventRaised += e => kinds[e.Kind] = kinds.TryGetValue(e.Kind, out var n) ? n + 1 : 1;
        for (int h = 0; h < 60 * 24 * 4; h++) life.Step(.25);
        Assert.True(kinds.ContainsKey("war") || kinds.ContainsKey("fight") || kinds.ContainsKey("siege"), string.Join(",", kinds.Keys));
        Assert.Equal(16, life.Cities.First(c => c.Place.Kind == "Castle").Capacity);
        var state = IslandLife.Load(life.Save());
        for (int h = 0; h < 96; h++) { life.Step(.25); state.Step(.25); }
        Assert.Equal(life.Fingerprint(), state.Fingerprint());
    }
}

public sealed class LongRunTests
{
    // A year with no player: nobody is ever listed twice (exiles once duplicated people from finished squads),
    // and dead factions can come back through revolts so the island never freezes under one ruler.
    [Fact]
    public void YearWithoutPlayerKeepsPeopleUniqueAndFactionsCanReturn()
    {
        var kinds = typeof(IslandLifeTests).GetMethod("Create", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        int rebirths = 0;
        foreach (ulong seed in new ulong[] { 2, 5, 8 })
        {
            var life = (IslandLife)kinds.Invoke(null, new object[] { seed })!;
            life.EventRaised += e => { if (e.Kind == "rebirth") rebirths++; };
            for (int d = 0; d < 365; d++)
            {
                for (int h = 0; h < 96; h++) life.Step(.25);
                var people = life.Cities.SelectMany(c => c.Residents).ToList();
                Assert.Equal(people.Count, people.Distinct().Count());
            }
            Assert.True(life.Factions.Count(f => !f.Extinct && !f.Profile.Nomadic && !f.Profile.Monsters) >= 2, $"seed {seed}: one ruler left");
        }
        Assert.True(rebirths > 0);
    }
}

public sealed class YearBacktest
{
    readonly ITestOutputHelper output;
    public YearBacktest(ITestOutputHelper output) => this.output = output;

    // Measures what a player would feel over a year: churn of factions, how long wars last and how unequal wealth gets.
    [Fact]
    public void YearMetricsStayPlayable()
    {
        var create = typeof(IslandLifeTests).GetMethod("Create", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        foreach (ulong seed in new ulong[] { 2, 5, 8, 13, 21 })
        {
            var life = (IslandLife)create.Invoke(null, new object[] { seed })!;
            var counts = new Dictionary<string, int>();
            var warStart = new Dictionary<(int, int), double>();
            var wars = new List<double>();
            life.EventRaised += e =>
            {
                counts[e.Kind] = counts.GetValueOrDefault(e.Kind) + 1;
                var key = (Math.Min(e.FactionA, e.FactionB), Math.Max(e.FactionA, e.FactionB));
                if (e.Kind == "war") warStart.TryAdd(key, e.Hour);
                if (e.Kind is "truce" or "pact" && warStart.Remove(key, out var start)) wars.Add((e.Hour - start) / 24);
            };
            for (int h = 0; h < 365 * 96; h++) life.Step(.25);
            var alive = life.Factions.Where(f => !f.Extinct && !f.Profile.Nomadic && !f.Profile.Monsters).ToList();
            var cash = alive.Select(f => Math.Max(0, f.Treasury)).OrderBy(t => t).ToArray();
            double gini = cash.Length < 2 || cash.Sum() <= 0 ? 0 : cash.Select((t, i) => (2 * (i + 1) - cash.Length - 1) * t).Sum() / (cash.Length * cash.Sum());
            int extinct = counts.GetValueOrDefault("extinct"), reborn = counts.GetValueOrDefault("rebirth");
            output.WriteLine($"seed {seed}: extinct {extinct}, rebirth {reborn}, alive {alive.Count}, wars ended {wars.Count} median {(wars.Count > 0 ? wars.OrderBy(w => w).ElementAt(wars.Count / 2) : 0):F0}d, open {warStart.Count}, gini {gini:F2}, robbery {counts.GetValueOrDefault("robbery")}, trade {counts.GetValueOrDefault("trade")}, nemesis {counts.GetValueOrDefault("nemesis")}");
            Assert.True(alive.Count >= 2, $"seed {seed}: one ruler left");
        }
    }
}

public sealed class OutpostTests
{
    // A city only falls by assault once its owner holds none of the posts around it; posts survive save/load.
    [Fact]
    public void CitiesFallOnlyAfterTheirPosts()
    {
        var create = typeof(IslandLifeTests).GetMethod("Create", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        int captures = 0, posts = 0;
        foreach (ulong seed in new ulong[] { 3, 7 })
        {
            var life = (IslandLife)create.Invoke(null, new object[] { seed })!;
            Assert.All(life.Cities.Where(c => c.IsCity && !c.IsLair), c => Assert.Equal(3, c.Outposts.Count(o => o.Owner == c.Owner)));
            var held = new Dictionary<int, bool>();
            life.EventRaised += e =>
            {
                if (e.Kind == "outpost") { posts++; var p = life.Cities.First(c => c.Place.Id == e.Place); held[e.Place] = p.Outposts.Any(o => o.Owner == p.Owner); }
                if (e.Kind != "capture") return;
                var city = life.Cities.First(c => c.Place.Id == e.Place);
                // Exiles retaking their own city skip the ring; every other conquest needed it.
                if (city.OriginalOwner != e.FactionA) { Assert.False(held[e.Place], $"{city.Place.Name} fell with posts still held"); captures++; }
            };
            for (int h = 0; h < 120 * 96; h++)
            {
                foreach (var c in life.Cities) held[c.Place.Id] = c.Outposts.Any(o => o.Owner == c.Owner);
                life.Step(.25);
            }
            var copy = IslandLife.Load(life.Save());
            Assert.Equal(life.Fingerprint(), copy.Fingerprint());
            for (int h = 0; h < 10 * 96; h++) { life.Step(.25); copy.Step(.25); }
            Assert.Equal(life.Fingerprint(), copy.Fingerprint());
        }
        Assert.True(captures > 0 && posts > captures, $"captures {captures}, post fights {posts}");
    }
}

public sealed class PlayerOutpostTests
{
    [Fact]
    public void PlayerCanHandAPostToAFaction()
    {
        var create = typeof(IslandLifeTests).GetMethod("Create", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var life = (IslandLife)create.Invoke(null, new object[] { 4UL })!;
        var city = life.Cities.First(c => c.Outposts.Count > 0);
        int other = life.Factions.First(f => f.Id != city.Owner && !f.Profile.Monsters && !f.Profile.Nomadic).Id;
        Assert.True(life.PlayerTakeOutpost(city.Place.Id, 0, other));
        Assert.Equal(other, city.Outposts[0].Owner);
        Assert.False(life.PlayerTakeOutpost(city.Place.Id, 0, other)); // already theirs
    }
}
