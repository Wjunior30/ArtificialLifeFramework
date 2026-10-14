using ArtificialLife.Core;
using System.Text.Json;

int days = 3650;
foreach (string argument in args)
    if (argument.StartsWith("--days=", StringComparison.Ordinal)) days = Math.Clamp(int.Parse(argument[7..]), 90, 3650);
ulong[] seeds = [1, 7, 42, 100, 2026, 65535];
var conditions = new Dictionary<string, SettlementSimulationOptions>
{
    ["baseline"] = new(), ["scarcity"] = new(.78, 1.1), ["hostile"] = new(.94, 1.65),
    ["no_food"] = new(0, 0), ["no_conflict"] = new(1, 0)
};
var policies = new Dictionary<string, SettlementRules>
{
    ["legacy"] = new(), ["negotiated"] = new() { Diplomacy = DiplomacyMode.Negotiated },
    ["sustainable"] = SettlementRules.Sustainable
};
var rows = new List<object>(); var failures = new List<string>(); var warnings = new List<string>(); var compatibilityNotes = new List<string>();
int audits = 0, replays = 0, restores = 0;
foreach (var policy in policies)
foreach (var condition in conditions)
foreach (ulong seed in seeds)
{
    string label = $"{policy.Key}/{condition.Key}/{seed}";
    var simulation = new SettlementSimulation(seed, condition.Value, policy.Value);
    var checkpoints = new List<(int Day, SimulationSave Save)>();
    var timeline = new List<object>(); int? extinctionDay = null;
    for (int day = 1; day <= days; day++)
    {
        simulation.Advance(1); audits++;
        if (!extinctionDay.HasValue && simulation.Settlements.Count == 0 && simulation.Nomads == 0) extinctionDay = day;
        foreach (string error in simulation.Audit()) failures.Add($"{label}/day={day}: {error}");
        if (day == days / 2 || day == Math.Min(500, days / 3)) checkpoints.Add((day, simulation.CaptureState()));
        if (day % 365 == 0 || day == days)
            timeline.Add(new { day, living = simulation.Settlements.Count, population = simulation.Nomads + simulation.Settlements.Sum(x => x.Population),
                food = simulation.Settlements.Sum(x => x.Food), materials = simulation.Settlements.Sum(x => x.Materials),
                simulation.Statistics.Deaths, simulation.Statistics.StarvationDeaths, simulation.Statistics.Battles });
    }
    var replay = new SettlementSimulation(seed, condition.Value, policy.Value); replay.Advance(days); replays++;
    if (simulation.StateHash() != replay.StateHash()) failures.Add($"{label}: deterministic replay diverged");
    foreach (var checkpoint in checkpoints)
    {
        restores++;
        try
        {
            var resumed = SettlementSimulation.Restore(checkpoint.Save);
            if (resumed.StateHash() != checkpoint.Save.Checksum) failures.Add($"{label}/{checkpoint.Day}: restore changed state");
            resumed.Advance(days - checkpoint.Day);
            if (resumed.StateHash() != simulation.StateHash()) failures.Add($"{label}/{checkpoint.Day}: resume diverged");
        }
        catch (Exception error) { failures.Add($"{label}/{checkpoint.Day}: restore failed: {error.Message}"); }
    }
    var active = new HashSet<int>();
    foreach (var entry in simulation.History)
    {
        if (entry.Kind == "foundation") { if (!active.Add(entry.SettlementId)) failures.Add($"{label}: duplicate foundation"); continue; }
        if (!active.Contains(entry.SettlementId) || (entry.OtherId != 0 && !active.Contains(entry.OtherId))) failures.Add($"{label}/{entry.Day}: inactive participant in {entry.Kind}");
        if (entry.Kind == "destruction") active.Remove(entry.SettlementId);
    }
    if (!active.SetEquals(simulation.Settlements.Select(x => x.Id))) failures.Add($"{label}: history differs from live state");
    if (simulation.ReadEvents().Events.Count != simulation.History.Count) failures.Add($"{label}: typed events incomplete");
    if (condition.Key == "no_conflict" && simulation.Statistics.Battles != 0) failures.Add($"{label}: battle without conflict");
    if (condition.Key == "no_food" && policy.Value.AllowStarvationExtinction && days >= 3650 && (simulation.Settlements.Count != 0 || simulation.Nomads != 0))
        failures.Add($"{label}: extinction did not complete");
    if (condition.Key == "baseline" && days >= 3650 && !simulation.Settlements.Any(x => x.Stage == SettlementStage.City)) failures.Add($"{label}: no city after ten years");
    var stages = simulation.Settlements.GroupBy(x => x.Stage).ToDictionary(x => x.Key.ToString(), x => x.Count());
    var stats = simulation.Statistics;
    double mortalityPercent = 100d * stats.Deaths / Math.Max(1, policy.Value.InitialPopulation + stats.Births + stats.ExternalArrivals);
    if (policy.Key == "legacy" && condition.Key == "no_food" && simulation.Settlements.All(x => x.Population == 1))
        compatibilityNotes.Add($"{label}: legacy starvation protection retained one inhabitant per settlement");
    if (policy.Key == "legacy" && condition.Key == "hostile" && simulation.Settlements.Count <= 1)
        compatibilityNotes.Add($"{label}: legacy hostile balance left at most one settlement");
    if (policy.Key == "sustainable" && condition.Key == "hostile" && simulation.Settlements.Count <= 1)
        warnings.Add($"{label}: recommended hostile balance left at most one settlement");
    rows.Add(new { policy = policy.Key, scenario = condition.Key, seed, days, living = simulation.Settlements.Count,
        population = simulation.Nomads + simulation.Settlements.Sum(x => x.Population), nomads = simulation.Nomads, stages,
        statistics = stats, mortalityPercent, extinctionDay,
        deathsPerThousandPersonYears = extinctionDay.HasValue ? (double?)null : stats.PopulationDays == 0 ? 0 : stats.Deaths * 365_000d / stats.PopulationDays,
        meanCompletedWarDays = stats.CompletedWars == 0 ? 0 : (double)stats.TotalWarDays / stats.CompletedWars,
        stateHash = simulation.StateHash(), timeline });
    Console.WriteLine($"{label}: living={simulation.Settlements.Count}, deaths={stats.Deaths}, peace={stats.Peaces}");
}
Directory.CreateDirectory("artifacts/settlement-backtest");
File.WriteAllText("artifacts/settlement-backtest/report.json", JsonSerializer.Serialize(new
{
    schema = 3, fingerprintVersion = SettlementSimulation.FingerprintVersion, days, seeds, conditions, policies,
    audits, replays, restores, failures, warnings, compatibilityNotes, rows
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Settlement backtest: scenarios={rows.Count}, audits={audits}, replays={replays}, restores={restores}, failures={failures.Count}");
Console.WriteLine($"Balance warnings: {warnings.Count}");
foreach (string failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;
