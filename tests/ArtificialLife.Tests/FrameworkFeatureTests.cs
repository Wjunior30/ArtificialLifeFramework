using ArtificialLife.Core;
using Xunit;

public sealed class FrameworkFeatureTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void SimpleFacadeAndSchedulerAreDeterministic()
    {
        var simulation = global::ArtificialLife.Core.ArtificialLife.CreateSettlements(42, x => x.Rules = SettlementRules.Sustainable);
        var calls = new List<string>(); var scheduler = new SimulationScheduler();
        scheduler.Add(new("b", 2, 0, day => calls.Add($"b:{day}")));
        scheduler.Add(new("a", 2, 0, day => calls.Add($"a:{day}")));
        scheduler.Run(2);
        Assert.Equal(new[] { "a:2", "b:2" }, calls);
        Assert.Equal(3, simulation.Settlements.Count);
        var services = new ArtificialLifeServiceCatalog().AddSingleton(simulation).AddTransient(() => new DiagnosticBuffer());
        Assert.Same(simulation, services.GetRequired<SettlementSimulation>());
        Assert.NotSame(services.GetRequired<DiagnosticBuffer>(), services.GetRequired<DiagnosticBuffer>());
    }

    [Fact]
    [Trait("Category", "Compatibility")]
    public void StableFacadeHasXmlDocumentation()
    {
        string xmlPath = Path.Combine(AppContext.BaseDirectory, "ArtificialLife.Core.xml");
        string xml = File.ReadAllText(xmlPath);
        foreach (string type in new[] { "ArtificialLife", "ArtificialLifeServiceCatalog", "SimulationScheduler", "SettlementSnapshots", "SnapshotAuthentication" })
            Assert.Contains($"T:ArtificialLife.Core.{type}", xml, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public void IncrementAndAuthenticationRejectTampering()
    {
        var simulation = new SettlementSimulation(42, rules: SettlementRules.Sustainable);
        var increment = SettlementSnapshots.CaptureIncrement(simulation, 60);
        var restored = SettlementSnapshots.RestoreIncrement(increment);
        Assert.Equal(increment.ResultHash, restored.StateHash());

        byte[] key = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
        var signed = SnapshotAuthentication.Sign(simulation.CaptureState(), key);
        Assert.Equal(simulation.StateHash(), SnapshotAuthentication.Verify(signed, key).Checksum);
        signed.Save.Payload[0] ^= 1;
        Assert.Throws<ArgumentException>(() => SnapshotAuthentication.Verify(signed, key));
    }

    [Fact]
    [Trait("Category", "Diagnostics")]
    public void DiagnosticsAndTelemetryAreBoundedAndExternalToState()
    {
        var buffer = new DiagnosticBuffer(2);
        buffer.Report(new(1, "one", DiagnosticSeverity.Information, "first"));
        buffer.Report(new(2, "two", DiagnosticSeverity.Warning, "second"));
        buffer.Report(new(3, "three", DiagnosticSeverity.Error, "third"));
        Assert.Equal(new[] { "two", "three" }, buffer.Items.Select(x => x.Code));
        var observer = new RecordingTelemetry();
        SimulationTelemetry.Measure("advance", () => { }, observer, new SimulationMetric("days", 1, "day"));
        Assert.Equal("advance", observer.Last!.Operation);
    }

    [Fact]
    [Trait("Category", "Diplomacy")]
    public void DiplomacyCanBeExplainedWithoutChangingState()
    {
        var simulation = new SettlementSimulation(42);
        string before = simulation.StateHash();
        var explanation = simulation.ExplainRelationship(simulation.Settlements[0].Id, simulation.Settlements[1].Id);
        Assert.Equal(3, explanation.Factors.Count);
        Assert.Equal(before, simulation.StateHash());
    }

    [Fact]
    [Trait("Category", "Population")]
    public void AggregatePopulationRoundTripConservesPeopleAndGoods()
    {
        var world = new LifeWorld(7);
        var cohort = new PopulationCohort("farmers", 10, new Dictionary<string, long> { ["food"] = 25 });
        var materialized = PopulationMaterialization.Expand(cohort, world, 4);
        Assert.Equal(4, world.Agents.Count);
        var collapsed = PopulationMaterialization.Collapse(materialized, world);
        Assert.Equal(10, collapsed.Population);
        Assert.Equal(25, collapsed.Goods["food"]);
        Assert.Empty(world.Agents);
    }

    [Fact]
    [Trait("Category", "Population")]
    public void ExternalMigrationCanRestartAnExtinctRegionAndRoundTrips()
    {
        var simulation = new SettlementSimulation(42, new(0, 0), SettlementRules.Sustainable);
        simulation.Advance(3650);
        Assert.Empty(simulation.Settlements);
        simulation.AdmitExternalMigrants(simulation.Rules.FounderCount);
        Assert.Empty(simulation.Audit());
        var restored = SettlementSimulation.Restore(simulation.CaptureState());
        Assert.Equal(simulation.StateHash(), restored.StateHash());
        Assert.Equal(simulation.Rules.FounderCount, restored.Statistics.ExternalArrivals);
    }

    private sealed class RecordingTelemetry : ISimulationTelemetryObserver
    {
        public SimulationObservation? Last { get; private set; }
        public void Observe(SimulationObservation observation) => Last = observation;
    }
}
