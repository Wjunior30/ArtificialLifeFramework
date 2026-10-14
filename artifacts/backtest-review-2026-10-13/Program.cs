using ArtificialLife.Core;
using System.Diagnostics;
using System.Text.Json;

const int days = 3650;
ulong[] seeds = [1, 7, 42, 100, 2026, 65535];
var scenarios = new Dictionary<string, SettlementSimulationOptions>
{
    ["baseline"] = new(), ["scarcity"] = new(.78, 1.1), ["hostile"] = new(.94, 1.65),
    ["no_food"] = new(0, 0), ["no_conflict"] = new(1, 0)
};
var rows = new List<object>();
var failures = new List<string>();
var stopwatch = Stopwatch.StartNew();
int restoreChecks = 0, replayChecks = 0, dailyAudits = 0;
foreach (var mode in args.Contains("--custom-only") ? Array.Empty<DiplomacyMode>() : Enum.GetValues<DiplomacyMode>())
foreach (var scenario in scenarios)
foreach (var seed in seeds)
{
    string label = $"{mode}/{scenario.Key}/{seed}";
    var rules = new SettlementRules { Diplomacy = mode };
    var simulation = new SettlementSimulation(seed, scenario.Value, rules);
    var checkpoints = new List<(int Day, SimulationSave Save)>();
    double maxFood = 0, maxMaterials = 0;
    int minimumPopulation = rules.InitialPopulation;
    for (int day = 1; day <= days; day++)
    {
        simulation.Advance(1);
        dailyAudits++;
        foreach (var error in simulation.Audit()) failures.Add($"{label}/day={day}: {error}");
        int population = simulation.Nomads + simulation.Settlements.Sum(x => x.Population);
        minimumPopulation = Math.Min(minimumPopulation, population);
        foreach (var town in simulation.Settlements)
        {
            maxFood = Math.Max(maxFood, town.Food);
            maxMaterials = Math.Max(maxMaterials, town.Materials);
            if (!double.IsFinite(town.WarExhaustion) || town.WarExhaustion < 0 || town.WarExhaustion > 100)
                failures.Add($"{label}/day={day}: invalid exhaustion");
        }
        if (day is 500 or 1825) checkpoints.Add((day, simulation.CaptureState()));
    }
    string hash = simulation.StateHash();
    var replay = new SettlementSimulation(seed, scenario.Value, rules);
    replay.Advance(days); replayChecks++;
    if (replay.StateHash() != hash) failures.Add($"{label}: full-state replay diverged");
    foreach (var checkpoint in checkpoints)
    {
        restoreChecks++;
        try
        {
            var restored = SettlementSimulation.Restore(checkpoint.Save);
            if (restored.StateHash() != checkpoint.Save.Checksum) failures.Add($"{label}/{checkpoint.Day}: restore changed state");
            restored.Advance(days - checkpoint.Day);
            if (restored.StateHash() != hash) failures.Add($"{label}/{checkpoint.Day}: resumed simulation diverged");
        }
        catch (Exception exception) { failures.Add($"{label}/{checkpoint.Day}: {exception.GetType().Name}: {exception.Message}"); }
    }
    var active = new HashSet<int>();
    foreach (var entry in simulation.History)
    {
        if (entry.Kind == "foundation")
        { if (!active.Add(entry.SettlementId)) failures.Add($"{label}: duplicate foundation"); continue; }
        if (!active.Contains(entry.SettlementId) || (entry.OtherId != 0 && !active.Contains(entry.OtherId)))
            failures.Add($"{label}/{entry.Day}: inactive participant in {entry.Kind}");
        if (entry.Kind == "destruction") active.Remove(entry.SettlementId);
    }
    if (!active.SetEquals(simulation.Settlements.Select(x => x.Id))) failures.Add($"{label}: history differs from state");
    foreach (var conflict in simulation.Conflicts.Where(x => x.Active))
        if (!active.Contains(conflict.First) || !active.Contains(conflict.Second)) failures.Add($"{label}: dead war participant");
    var stats = simulation.Statistics;
    rows.Add(new
    {
        mode = mode.ToString(), scenario = scenario.Key, seed, days,
        living = simulation.Settlements.Count, population = simulation.Nomads + simulation.Settlements.Sum(x => x.Population),
        nomads = simulation.Nomads, minimumPopulation,
        cities = simulation.Settlements.Count(x => x.Stage == SettlementStage.City),
        stats.Founded, stats.Destroyed, stats.Births, stats.Deaths, stats.StarvationDeaths,
        stats.HungryPersonDays, stats.Wars, stats.Battles, stats.Peaces, stats.CompletedWars,
        meanCompletedWarDays = stats.CompletedWars == 0 ? 0 : (double)stats.TotalWarDays / stats.CompletedWars,
        maxFood, maxMaterials, saveBytes = simulation.CaptureState().Payload.Length, hash
    });
    Console.WriteLine($"{label}: living={simulation.Settlements.Count}, population={simulation.Nomads + simulation.Settlements.Sum(x => x.Population)}, peace={stats.Peaces}, hungerDeaths={stats.StarvationDeaths}");
}
var customProbes = new List<object>();
if (args.Contains("--custom-only"))
{
    var customRules = new SettlementRules
    {
        FounderCount = 80, InitialPopulation = 240, BirthProbability = 0, ProsperityBirthBonus = 0
    };
    customRules.Validate();
    var custom = new SettlementSimulation(42, new(1, 0), customRules);
    for (int day = 1; day <= days; day++)
    {
        custom.Advance(1);
        var errors = custom.Audit();
        if (errors.Count == 0) continue;
        customProbes.Add(new { name = "large_founder_group", day, rules = customRules,
            settlements = custom.Settlements.Select(x => new { x.Id, x.Population }).ToArray(), errors });
        failures.Add($"large_founder_group/day={day}: {string.Join("; ", errors)}");
        break;
    }
}
var output = new
{
    date = "2026-10-13", days, seeds, scenarios, dailyAudits, replayChecks, restoreChecks,
    elapsedSeconds = stopwatch.Elapsed.TotalSeconds, failures, rows, customProbes
};
string outputName = args.Contains("--custom-only") ? "custom-rules.json" : "diagnostics.json";
File.WriteAllText($"artifacts/backtest-review-2026-10-13/{outputName}", JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Diagnostics: rows={rows.Count}, dailyAudits={dailyAudits}, replayChecks={replayChecks}, restoreChecks={restoreChecks}, failures={failures.Count}");
foreach (var failure in failures.Take(30)) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;
