using ArtificialLife.Core;
using ArtificialLife.Samples;
using System.Security.Cryptography;
using Xunit;

public sealed class IntegrationTests
{
    private sealed class Harvest : ISettlementDailyRule
    {
        public ExtensionState CaptureState() => new("test.harvest.v1", Array.Empty<byte>());
        public SettlementEffect Evaluate(SettlementState state, SettlementContext context, ref DeterministicRandom random) => new(Food: 2);
    }
    [Fact]
    public void DailyExtensionEffectsAndCodecsPreserveReplay()
    {
        var policies = new SettlementPolicies { DailyRules = new[] { new Harvest() } };
        var sim = new SettlementSimulation(42, policies: policies); var control = new SettlementSimulation(42);
        sim.Advance(1); control.Advance(1); Assert.Equal(control.Settlements[0].Food + 2, sim.Settlements[0].Food, 8);
        var restored = SettlementSimulation.Restore(sim.CaptureState(), new() { SettlementPolicy = state => new Harvest() });
        sim.Advance(100); restored.Advance(100); Assert.Empty(sim.Audit()); Assert.Equal(sim.StateHash(), restored.StateHash());
    }
    [Fact]
    public void AdapterFramePartitionPreservesSimulation()
    {
        var a = new SettlementSession(); var b = new SettlementSession();
        a.Update(10);
        for (int i = 0; i < 1000; i++) b.Update(.01);
        Assert.Equal(100, a.Simulation.Day); Assert.Equal(a.Simulation.StateHash(), b.Simulation.StateHash());
        b.Load(a.Save()); Assert.Empty(b.Update(0));
    }

    [Fact]
    public void SavePayloadIsIndependentAndMalformedStateIsRejected()
    {
        var sim = new SettlementSimulation(42); var saved = sim.CaptureState(); string before = sim.StateHash();
        // The first numeric option (food multiplier) starts immediately after the seed.
        BitConverter.GetBytes(double.NaN).CopyTo(saved.Payload, 8);
        saved.Checksum = Convert.ToHexString(SHA256.HashData(saved.Payload)).ToLowerInvariant();
        Assert.Equal(before, sim.StateHash()); Assert.Throws<ArgumentException>(() => SettlementSimulation.Restore(saved));
    }

    [Fact]
    public void SustainableStocksStayBoundedAfterConflictAndMigration()
    {
        var sim = new SettlementSimulation(42, new(.94, 1.65), SettlementRules.Sustainable);
        for (int day = 0; day < 3650; day++)
        {
            sim.Advance(1); Assert.Empty(sim.Audit());
            foreach (var town in sim.Settlements)
            {
                Assert.True(town.Food <= town.Population * sim.Rules.FoodStoragePerPerson + 1e-8);
                Assert.True(town.Materials <= town.Population * sim.Rules.MaterialStoragePerPerson + 1e-8);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidMigrationRemainderIsRejected(int minimum)
        => Assert.Throws<ArgumentException>(() => new SettlementSimulation(42, rules: new() { MigrationRemainingPopulation = minimum }));

    private sealed class PeacefulConflict : IConflictResolver
    {
        public ExtensionState CaptureState() => new("test.conflict.v1", Array.Empty<byte>());
        public ConflictResult Resolve(ConflictContext context, ref DeterministicRandom random) => new(true, 0, 0, 0, 0);
    }
    private sealed class CampOnly : ISettlementProgression
    {
        public ExtensionState CaptureState() => new("test.camp.v1", Array.Empty<byte>());
        public SettlementStage Evaluate(SettlementState settlement, SettlementRules rules) => SettlementStage.Camp;
    }
    [Fact]
    public void ConflictAndProgressionPoliciesCanBeReplaced()
    {
        var sim = new SettlementSimulation(42, new(1, 2), new() { InitialPopulation = 180, FounderCount = 60 },
            new() { ConflictResolver = new PeacefulConflict(), Progression = new CampOnly() });
        sim.Advance(3650); Assert.True(sim.Statistics.Battles > 0);
        Assert.All(sim.Settlements, town => Assert.Equal(SettlementStage.Camp, town.Stage));
        Assert.Equal(sim.Statistics.StarvationDeaths, sim.Statistics.Deaths);
    }
}
