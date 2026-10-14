using ArtificialLife.Core;
using System.Text.Json;
using Xunit;

public sealed class FrameworkTests
{
    [Theory]
    [InlineData(7, 42)]
    [InlineData(42, 126)]
    [InlineData(80, 240)]
    public void MigrationKeepsResidents(int founders, int population)
    {
        var sim = new SettlementSimulation(42, new(1, 0), new()
        { FounderCount = founders, InitialPopulation = population, BirthProbability = 0, ProsperityBirthBonus = 0 });
        for (int day = 0; day < 3650; day++) { sim.Advance(1); Assert.Empty(sim.Audit()); }
        Assert.All(sim.Settlements, town => Assert.True(town.Population > 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StarvationIsAnExplicitPolicy(bool extinction)
    {
        var sim = new SettlementSimulation(42, new(0, 0), new() { AllowStarvationExtinction = extinction });
        sim.Advance(3650); Assert.Empty(sim.Audit());
        if (extinction) { Assert.Empty(sim.Settlements); Assert.Equal(0, sim.Nomads); }
        else Assert.All(sim.Settlements, town => Assert.Equal(1, town.Population));
        Assert.Equal(42 + sim.Statistics.Births - sim.Statistics.Deaths, sim.Nomads + sim.Settlements.Sum(x => x.Population));
        Assert.Equal(sim.StateHash(), SettlementSimulation.Restore(sim.CaptureState()).StateHash());
    }

    [Theory]
    [InlineData(DiplomacyMode.Legacy)]
    [InlineData(DiplomacyMode.Negotiated)]
    public void SettlementRestoreResumesTheSameTrajectory(DiplomacyMode diplomacy)
    {
        var sim = new SettlementSimulation(7, rules: SettlementRules.Sustainable with { Diplomacy = diplomacy });
        sim.Advance(500); var save = sim.CaptureState();
        var restored = SettlementSimulation.Restore(JsonSerializer.Deserialize<SimulationSave>(JsonSerializer.Serialize(save))!);
        Assert.Equal(sim.StateHash(), restored.StateHash());
        sim.Advance(3150); restored.Advance(3150);
        Assert.Equal(sim.StateHash(), restored.StateHash()); Assert.Empty(restored.Audit());
    }

    [Theory]
    [InlineData("Legacy")]
    [InlineData("Negotiated")]
    public void SchemaOneFixturesMigrate(string mode)
    {
        var old = JsonSerializer.Deserialize<SimulationSave>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"settlements-v1-{mode}.json")))!;
        Assert.Equal(1, old.Schema);
        var migrated = SettlementSimulation.Restore(old); Assert.Equal(500, migrated.Day); Assert.Empty(migrated.Audit());
        Assert.False(migrated.Rules.AllowStarvationExtinction); Assert.Equal(3, migrated.CaptureState().Schema);
        var replay = SettlementSimulation.Restore(migrated.CaptureState());
        migrated.Advance(500); replay.Advance(500); Assert.Equal(migrated.StateHash(), replay.StateHash());
    }

    [Fact]
    public void CorruptionAndUnknownVersionsAreRejected()
    {
        var sim = new SettlementSimulation(42); var save = sim.CaptureState(); save.Payload[0] ^= 1;
        Assert.Throws<ArgumentException>(() => SettlementSimulation.Restore(save));
        save = sim.CaptureState(); save.Schema = 99;
        Assert.Throws<ArgumentException>(() => SettlementSimulation.Restore(save));
        save = sim.CaptureState(); save.Kind = "portable";
        Assert.Throws<ArgumentException>(() => SettlementSimulation.Restore(save));
    }

    [Fact]
    public void ResourceRulesBoundStocks()
    {
        var sim = new SettlementSimulation(42, new(1, 0), SettlementRules.Sustainable);
        sim.Advance(3650);
        Assert.All(sim.Settlements, town =>
        {
            Assert.True(town.Food <= town.Population * sim.Rules.FoodStoragePerPerson);
            Assert.True(town.Materials <= town.Population * sim.Rules.MaterialStoragePerPerson);
        });
        Assert.True(sim.Statistics.FoodLost > 0); Assert.True(sim.Statistics.PopulationDays > 0);
    }

    [Fact]
    public void EventCursorsDoNotChangeSimulationState()
    {
        var sim = new SettlementSimulation(1); var first = sim.ReadEvents(); Assert.Equal(3, first.Events.Count);
        Assert.All(first.Events, e => Assert.IsType<SettlementFounded>(e));
        sim.Advance(1000); string before = sim.StateHash(); var batch = sim.ReadEvents(first.NextCursor);
        Assert.Contains(batch.Events, e => e is BuildingCompleted); Assert.Contains(batch.Events, e => e is SettlementUpgraded);
        Assert.Empty(sim.ReadEvents(batch.NextCursor).Events); Assert.Equal(before, sim.StateHash());
        Assert.Equal(batch.Events.ToArray(), SettlementSimulation.Restore(sim.CaptureState()).ReadEvents(first.NextCursor).Events.ToArray());
    }

    private sealed class FixedSite : ISettlementSiteEvaluator
    {
        public ExtensionState CaptureState() => new("test.site.v1", Array.Empty<byte>());
        public double Evaluate(SettlementSite site, SettlementContext context) => -site.Id * 100;
    }
    [Fact]
    public void CustomRulesAreUsedAndRequireTheirCodecOnRestore()
    {
        var sim = new SettlementSimulation(42, policies: new() { SiteEvaluator = new FixedSite() });
        Assert.Equal(new[] { 0, 1, 2 }, sim.Settlements.Select(x => x.SiteId).ToArray());
        Assert.Throws<ArgumentException>(() => SettlementSimulation.Restore(sim.CaptureState()));
        var restored = SettlementSimulation.Restore(sim.CaptureState(), new() { SettlementPolicy = state => state.Kind == "test.site.v1" ? new FixedSite() : throw new ArgumentException() });
        sim.Advance(1000); restored.Advance(1000); Assert.Equal(sim.StateHash(), restored.StateHash());
    }

    private sealed record FoodCommand(EntityId Id) : IPersistentSimulationCommand
    {
        public void Apply(PortableSimulation simulation) => simulation.World.Agents[Id].Inventory.Add("food", 1);
        public ExtensionState CaptureState() => new("test.food.v1", BitConverter.GetBytes(Id.Value));
    }
    [Fact]
    public void PortableSaveIncludesRemainderCommandsProfilesCareAndCargo()
    {
        var sim = new PortableSimulation(42, .1);
        var a = Agent.Create(sim.World.Ids.Next("a"), "A"); a.Needs.Add("hunger", .5, .1); a.Inventory.Add("food", 5);
        var b = Agent.Create(sim.World.Ids.Next("b"), "B");
        var profile = new PersonProfile(a.Id) { Occupation = Occupation.Farmer }; profile.Learn("rain"); profile.SetSkill(Skill.Farming, .8);
        sim.Add(a, profile); sim.Add(b); sim.World.SetBrain(new(new IUtilityAction[] { new IdleAction(), new SatisfyNeedAction("eat", "hunger", .1) }));
        sim.World.Economy.Open(a.Id, new(500)); sim.World.Economy.Open(b.Id, new(0)); sim.World.Economy.Transfer(0, a.Id, b.Id, new(10), "gift");
        var request = sim.Care.Request(b.Id, "food", 1); Assert.True(sim.Care.Assign(request, a));
        var north = new Settlement("n", new(0, 0)); north.Stock.Add("food", 10);
        sim.Regions.Add(north); sim.Regions.Add(new("s", new(10, 0))); sim.Regions.Dispatch("n", "s", "food", 4); sim.Regions.Tick(3);
        sim.Advance(.25); sim.Enqueue(new FoodCommand(a.Id));
        var save = sim.CaptureState();
        Assert.Throws<ArgumentException>(() => PortableSimulation.Restore(save));
        var restored = PortableSimulation.Restore(save, new() { Command = state => new FoodCommand(new(BitConverter.ToUInt64(state.Payload, 0))) });
        Assert.Equal(sim.StateHash(), restored.StateHash());
        sim.Advance(10); restored.Advance(10); sim.Regions.Tick(7); restored.Regions.Tick(7);
        Assert.Equal(sim.StateHash(), restored.StateHash()); Assert.Equal(4, restored.Regions.Settlements["s"].Stock.Quantity("food"));
        Assert.Equal(sim.World.Ids.Next("next"), restored.World.Ids.Next("next"));
    }

    [Fact]
    public void CaravanBridgeConservesDispatchAndResumesDelivery()
    {
        var sim = new SettlementSimulation(42, new(1, 0)); var bridge = new SettlementTradeBridge(sim, 100);
        var from = sim.Settlements[0]; var to = sim.Settlements[1]; double before = from.Food;
        var cargo = bridge.DispatchFood(from.Id, to.Id, 5);
        Assert.Equal(before - 5, from.Food); Assert.Equal(5, cargo.Cargo.Quantity("food"));
        var restored = SettlementTradeBridge.Restore(bridge.CaptureState());
        bridge.Advance(40); restored.Advance(40);
        Assert.Empty(bridge.Network.Caravans); Assert.Equal(bridge.CaptureState().Checksum, restored.CaptureState().Checksum);
    }
}
