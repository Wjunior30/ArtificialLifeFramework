using ArtificialLife.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

bool smoke = args.Contains("--smoke");
var rows = new List<object>(); var failures = new List<string>();
void Measure(string name, int steps, Action advance, Func<SimulationSave> capture, Func<SimulationSave, SimulationSave> restore, Action audit)
{
    GC.Collect(); long memoryBefore = GC.GetTotalMemory(true), allocatedBefore = GC.GetTotalAllocatedBytes(true);
    var timer = Stopwatch.StartNew(); advance(); timer.Stop(); double seconds = timer.Elapsed.TotalSeconds;
    long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
    audit(); timer.Restart(); var save = capture(); double snapshotMs = timer.Elapsed.TotalMilliseconds;
    timer.Restart(); var restored = restore(save); double restoreAndCaptureMs = timer.Elapsed.TotalMilliseconds;
    if (save.Checksum != restored.Checksum) failures.Add($"{name}: snapshot roundtrip diverged");
    // Guardrails detect catastrophic regressions, not universal performance promises.
    if (seconds > 120 || save.Payload.Length > 128 * 1024 * 1024) failures.Add($"{name}: execution or snapshot budget exceeded");
    rows.Add(new { name, steps, seconds, stepsPerSecond = steps / Math.Max(seconds, 1e-9), allocatedBytes = allocated,
        memoryBefore, retainedBytes = GC.GetTotalMemory(true), snapshotMs, restoreAndCaptureMs, snapshotBytes = save.Payload.Length });
    Console.WriteLine($"{name}: {steps} steps in {seconds:F3}s, snapshot={save.Payload.Length} bytes");
}
var warmup = new SettlementSimulation(42); warmup.Advance(10); _ = SettlementSimulation.Restore(warmup.CaptureState());
void Region(string name, SettlementRules rules, int days)
{
    var sim = new SettlementSimulation(42, new(1, 0), rules);
    Measure(name, days, () => sim.Advance(days), sim.CaptureState, s => SettlementSimulation.Restore(s).CaptureState(),
        () => { foreach (var error in sim.Audit()) failures.Add($"{name}: {error}"); });
}
Region("100-settlements", new() { SiteCount = 100, InitialSettlements = 100, InitialPopulation = 700 }, smoke ? 30 : 365);
Region("100000-aggregated-agents", new() { SiteCount = 100, InitialSettlements = 100, InitialPopulation = 100000, FounderCount = 1000, MaximumPopulation = 2000 }, smoke ? 30 : 365);
Region("1000-simulated-years", new(), smoke ? 365 : 365000);
var world = new LifeWorld(42);
for (int i = 0; i < 10000; i++)
{
    var agent = Agent.Create(world.Ids.Next("agent"), $"Agent {i}"); agent.Needs.Add("food", 1, .01); world.AddAgent(agent);
}
world.SetBrain(new(new IUtilityAction[] { new IdleAction(), new SatisfyNeedAction("eat", "food", .02) }));
int ticks = smoke ? 10 : 100;
Measure("10000-individual-agents", ticks, () => { for (int i = 0; i < ticks; i++) world.Step(.1); }, world.CaptureState,
    s => LifeWorld.Restore(s).CaptureState(), world.Economy.Audit);
Directory.CreateDirectory("artifacts/benchmarks");
File.WriteAllText($"artifacts/benchmarks/{(smoke ? "smoke" : "full")}.json", JsonSerializer.Serialize(new
{ runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription, processors = Environment.ProcessorCount,
    mode = smoke ? "smoke" : "full", budgetSecondsPerCase = 120, rows, failures }, new JsonSerializerOptions { WriteIndented = true }));
foreach (string failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;
