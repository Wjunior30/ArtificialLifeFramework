using ArtificialLife.Core;
using Xunit;

public sealed class NpcIntelligenceTests
{
    [Fact]
    public void MemoryRingKeepsNewestAndCompactsOnDecay()
    {
        var store = new MemoryStore();
        for (int i = 0; i < 300; i++) store.Remember(new Memory(i, "k", "d", i % 2 == 0 ? 1 : 0.0005));
        Assert.Equal(256, store.All.Count);
        Assert.Equal(44, store.All[0].Tick);
        store.Decay(0);
        Assert.All(store.All, m => Assert.True(m.Importance > 0.001));
        Assert.Equal(128, store.All.Count);
    }

    [Fact]
    public void CommitmentBonusKeepsCurrentAction()
    {
        var world = new LifeWorld(1);
        var agent = Agent.Create(world.Ids.Next("agent"), "A");
        agent.Needs.Add("hunger", 0.4); agent.Needs.Add("thirst", 0.5);
        var brain = new UtilityBrain(new IUtilityAction[] { new SatisfyNeedAction("eat", "hunger", .1), new SatisfyNeedAction("drink", "thirst", .1) });
        var ctx = new DecisionContext(world, agent, 7);
        Assert.Equal("eat", brain.Choose(ctx).Id);
        brain.Choose(ctx).Start(ctx);
        agent.Needs["thirst"].Change(-0.15); // thirst now slightly more urgent
        Assert.Equal("drink", brain.Choose(ctx).Id);
        brain.CommitmentBonus = 0.1;
        Assert.Equal("eat", brain.Choose(ctx).Id);
    }

    [Fact]
    public void RandomTopNIsDeterministicPerSeed()
    {
        var world = new LifeWorld(1);
        var agent = Agent.Create(world.Ids.Next("agent"), "A");
        agent.Needs.Add("hunger", 0.5); agent.Needs.Add("thirst", 0.5);
        var brain = new UtilityBrain(new IUtilityAction[] { new SatisfyNeedAction("eat", "hunger", .1), new SatisfyNeedAction("drink", "thirst", .1) }) { RandomTopN = 2 };
        var picks = Enumerable.Range(0, 64).Select(s => brain.Choose(new DecisionContext(world, agent, (ulong)s)).Id).ToArray();
        Assert.Contains("eat", picks); Assert.Contains("drink", picks);
        Assert.Equal(picks, Enumerable.Range(0, 64).Select(s => brain.Choose(new DecisionContext(world, agent, (ulong)s)).Id));
    }

    [Fact]
    public void CurvesSpatialHashLodAndGoap()
    {
        Assert.True(new ResponseCurve(CurveKind.Logistic).Evaluate(.9) > .95);
        Assert.Equal(0, new ResponseCurve(CurveKind.Inverse).Evaluate(1));
        Assert.True(ResponseCurve.Combine(new[] { .9, .9, .9 }) > .9 * .9 * .9);

        var ids = new IdSequence(1);
        var a = ids.Next("agent"); var b = ids.Next("agent"); var c = ids.Next("agent");
        var hash = new SpatialHash(10);
        hash.Set(a, 0, 0); hash.Set(b, 5, 0); hash.Set(c, 100, 0);
        Assert.Equal(new[] { a, b }, hash.Query(1, 0, 20));
        hash.Set(b, 99, 0);
        Assert.Equal(new[] { c, b }, hash.Query(100, 0, 5));

        var lod = new AiLodScheduler();
        Assert.Equal(16, Enumerable.Range(0, 256).Count(t => lod.ShouldThink(a, t, 1_000)));
        Assert.Equal(256, Enumerable.Range(0, 256).Count(t => lod.ShouldThink(a, t, 1)));

        static IReadOnlyDictionary<string, bool> F(params string[] k) => k.ToDictionary(x => x, _ => true);
        var actions = new[]
        {
            new GoapAction("get_axe", 1, F(), F("has_axe")),
            new GoapAction("chop", 2, F("has_axe"), F("has_wood")),
            new GoapAction("buy_wood", 10, F(), F("has_wood")),
            new GoapAction("build", 3, F("has_wood"), F("has_house")),
        };
        var plan = GoapPlanner.Plan(F(), F("has_house"), actions);
        Assert.Equal(new[] { "get_axe", "chop", "build" }, plan!.Select(x => x.Id));
        Assert.Null(GoapPlanner.Plan(F(), F("flying"), actions));
    }
}
