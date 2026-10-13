using ArtificialLife.Core;
using System.Text.Json;

int days = 3650;
foreach (string argument in args) if (argument.StartsWith("--days=")) days = Math.Clamp(int.Parse(argument[7..]), 90, 3650);
ulong[] seeds = [1, 7, 42, 100, 2026, 65535];
var scenarios = new Dictionary<string, SettlementSimulationOptions>
{
    ["baseline"] = new(), ["scarcity"] = new(.78, 1.1), ["hostile"] = new(.94, 1.65)
};
var rows = new List<object>(); var failures = new List<string>();
int founded = 0, upgrades = 0, alliances = 0, wars = 0, battles = 0, destroyed = 0, refounded = 0, cities = 0;
foreach (var scenario in scenarios) foreach (ulong seed in seeds)
{
    var simulation = new SettlementSimulation(seed, scenario.Value); simulation.Advance(days);
    var audit = simulation.Audit(); foreach (string error in audit) failures.Add($"{scenario.Key}/{seed}: {error}");
    if (simulation.Settlements.Count == 0) failures.Add($"{scenario.Key}/{seed}: no living settlement");
    if (simulation.Statistics.PeakSettlements < 3) failures.Add($"{scenario.Key}/{seed}: initial settlements were not sustained");
    var stages = simulation.Settlements.GroupBy(x => x.Stage).ToDictionary(x => x.Key.ToString(), x => x.Count());
    founded += simulation.Statistics.Founded; upgrades += simulation.Statistics.Upgrades; alliances += simulation.Statistics.Alliances;
    wars += simulation.Statistics.Wars; battles += simulation.Statistics.Battles; destroyed += simulation.Statistics.Destroyed;
    refounded += simulation.Statistics.Refounded; cities += stages.GetValueOrDefault(nameof(SettlementStage.City));
    rows.Add(new { scenario = scenario.Key, seed, days, living = simulation.Settlements.Count, nomads = simulation.Nomads, stages, simulation.Statistics, fingerprint = simulation.Fingerprint().ToString("X16") });
    Console.WriteLine($"{scenario.Key,-8} seed={seed,-5} living={simulation.Settlements.Count,-2} stages={string.Join(',', stages.Select(x => $"{x.Key}:{x.Value}"))}");
}
if (founded <= scenarios.Count * seeds.Length * 3) failures.Add("No settlements were founded after initialization.");
if (upgrades < scenarios.Count * seeds.Length * 2) failures.Add("Settlements rarely progressed beyond camps.");
if (alliances == 0 || wars == 0 || battles == 0 || destroyed == 0 || refounded == 0) failures.Add("One or more historical cycles never occurred.");
if (days >= 3650 && cities == 0) failures.Add("No settlement completed the camp-to-city lifecycle.");
var aggregate = new { founded, upgrades, alliances, wars, battles, destroyed, refounded, cities };
Directory.CreateDirectory("artifacts/settlement-backtest");
File.WriteAllText("artifacts/settlement-backtest/report.json", JsonSerializer.Serialize(new { schema = 1, days, seeds, scenarios, aggregate, failures, rows }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Settlement backtest: scenarios={rows.Count} aggregate={JsonSerializer.Serialize(aggregate)} failures={failures.Count}");
foreach (string failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;
