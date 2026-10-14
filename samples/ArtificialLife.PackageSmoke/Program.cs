using ArtificialLife.Core;

var sim = new SettlementSimulation(42, rules: SettlementRules.Sustainable);
sim.Advance(500); var restored = SettlementSimulation.Restore(sim.CaptureState());
sim.Advance(500); restored.Advance(500);
if (sim.StateHash() != restored.StateHash() || restored.Audit().Count != 0) return 1;
Console.WriteLine($"Installed package validated: day={restored.Day}, events={restored.ReadEvents().Events.Count}");
return 0;
