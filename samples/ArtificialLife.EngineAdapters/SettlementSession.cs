using ArtificialLife.Core;

namespace ArtificialLife.Samples;

/// <summary>Shared glue for Unity and Godot: engine time becomes whole simulation days.</summary>
public sealed class SettlementSession
{
    private double _elapsed;
    private int _cursor;
    public SettlementSimulation Simulation { get; private set; } = new(42, rules: SettlementRules.Sustainable);
    public double SecondsPerDay { get; }
    public SettlementSession(double secondsPerDay = .1)
    {
        if (!double.IsFinite(secondsPerDay) || secondsPerDay <= 0) throw new ArgumentOutOfRangeException(nameof(secondsPerDay));
        SecondsPerDay = secondsPerDay;
    }
    public IReadOnlyList<SettlementEvent> Update(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        double total = _elapsed + seconds; double pending = total / SecondsPerDay;
        if (!double.IsFinite(pending) || pending > 10000) throw new ArgumentOutOfRangeException(nameof(seconds));
        int days = (int)Math.Floor(pending + 1e-9); _elapsed = Math.Max(0, total - days * SecondsPerDay);
        Simulation.Advance(days); var batch = Simulation.ReadEvents(_cursor); _cursor = batch.NextCursor; return batch.Events;
    }
    public SimulationSave Save() => Simulation.CaptureState();
    public void Load(SimulationSave save)
    {
        Simulation = SettlementSimulation.Restore(save); _cursor = Simulation.History.Count; _elapsed = 0;
        // The engine rebuilds its scene from Simulation.Settlements; old effects are not replayed.
    }
}
