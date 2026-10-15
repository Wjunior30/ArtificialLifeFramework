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
