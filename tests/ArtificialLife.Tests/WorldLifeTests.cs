#pragma warning disable CS0618 // WorldLife is obsolete but still covered
using ArtificialLife.Core;
using Xunit;
using Xunit.Abstractions;

public sealed class WorldLifeTests
{
    readonly ITestOutputHelper output;
    public WorldLifeTests(ITestOutputHelper output) => this.output = output;

    // Twenty places like the island generator makes, on a 10 km map joined by a ring of roads plus spokes.
    static WorldLife Create(ulong seed)
    {
        string[] kinds = { "Town", "Town", "Village", "Village", "Village", "Village", "Port", "Farm", "Farm", "Fishing", "Fishing", "Mine", "Mine", "HuntersCamp", "HuntersCamp", "Outpost", "Outpost", "BanditCamp", "Ruins", "Nest" };
        var places = kinds.Select((k, i) => new WorldPlace(i, $"{k} {i}", k, Math.Cos(i * .314) * 4000, Math.Sin(i * .314) * 4000, k == "Town" ? 2 : k is "Village" or "Port" or "Farm" or "Outpost" ? 1 : 0)).ToArray();
        var routes = places.Select(p => new WorldRoute(p.Id, (p.Id + 1) % places.Length, 1300)).Concat(places.Where(p => p.Id % 5 == 0).Select(p => new WorldRoute(p.Id, (p.Id + 10) % places.Length, 8000))).ToArray();
        return new WorldLife(seed, places, routes);
    }

    static void Run(WorldLife life, double days) { for (int h = 0; h < days * 24 * 4; h++) life.Step(.25); }

    [Fact]
    public void SameSeedReplaysTheSameWorld()
    {
        WorldLife a = Create(7), b = Create(7), c = Create(8);
        Run(a, 5); Run(b, 5); Run(c, 5);
        Assert.Equal(a.Fingerprint(), b.Fingerprint());
        Assert.NotEqual(a.Fingerprint(), c.Fingerprint());
    }

    [Fact]
    public void SquadBudgetIsNeverExceeded()
    {
        var life = Create(3);
        for (int h = 0; h < 24 * 4 * 10; h++) { life.Step(.25); Assert.True(life.Squads.Count <= life.MaxSquads + 1, $"{life.Squads.Count} squads"); }
    }

    [Fact]
    public void ExternallyControlledSquadsAreLeftToTheEngine()
    {
        var life = Create(5);
        while (life.Squads.Count == 0) life.Step(.25);
        var squad = life.Squads[0]; squad.ControlledExternally = true;
        double x = squad.X, z = squad.Z;
        for (int i = 0; i < 20; i++) life.Step(.25);
        Assert.Equal((x, z), (squad.X, squad.Z));
    }

    [Fact]
    public void ThirtyDayBacktestKeepsTheWorldAliveAndEventful()
    {
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5 })
        {
            var life = Create(seed);
            var initialSkill = life.Settlements.SelectMany(s => s.Residents).Average(p => p.Combat);
            Run(life, 30);
            var people = life.Settlements.SelectMany(s => s.Residents).Concat(life.Squads.SelectMany(s => s.Members)).Where(p => p.Alive).ToList();
            var kinds = life.Events.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count());
            var owners = life.Settlements.GroupBy(s => s.Owner).Select(g => $"{g.Key}:{g.Count()}");
            output.WriteLine($"seed {seed}: {people.Count} alive, skill {initialSkill:0.0}->{people.Average(p => p.Combat):0.0}, bounties {people.Count(p => p.Bounty > 0)}, owners {string.Join(" ", owners)}, events {string.Join(" ", kinds.Select(k => $"{k.Key}:{k.Value}"))}");
            Assert.True(people.Count(p => p.Faction != WorldFaction.Mutants) > 60, "Human population must not collapse.");
            Assert.True(kinds.ContainsKey("mission") && kinds.ContainsKey("fight"), "Squads must leave towns and meet enemies.");
            Assert.True(life.Settlements.Count(s => s.Owner == WorldFaction.Villagers) >= 6, "Villagers must keep a share of the island.");
            Assert.True(people.Max(p => p.Combat) > 50, "Fighters must improve with use.");
        }
    }
}
