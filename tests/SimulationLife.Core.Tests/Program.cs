using System.Text.Json;
using SimulationLife.Core;
using SimulationLife.Protocol;

var checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}

var left = new Simulation(WorldDefinition.Demo());
var right = new Simulation(WorldDefinition.Demo());
left.Advance(10);
for (var index = 0; index < 100; index++) right.Advance(0.1);
Check(left.Tick == right.Tick, "Frame partition changed the tick count.");
Check(Math.Abs(left.TotalMinutes - right.TotalMinutes) < 0.000001, "Frame partition changed world time.");
Check(left.StateHash() == right.StateHash(), "Frame partition changed the deterministic state hash.");
for (var index = 0; index < left.Agents.Count; index++)
{
    Check(left.Agents[index].Action == right.Agents[index].Action, "Frame partition changed a decision.");
    Check(Math.Abs(left.Agents[index].Needs.Thirst - right.Agents[index].Needs.Thirst) < 0.000001, "Frame partition changed needs.");
}

var urgent = new Simulation(WorldDefinition.Demo());
Check(urgent.SetNeed(1, "thirst", 5), "Could not set a valid need.");
urgent.Advance(0.1);
Check(urgent.Agents[0].Action == AgentAction.Drink, "Critical thirst did not select available water.");
urgent.Advance(1.1);
Check(urgent.Agents[0].Needs.Thirst > 50 && urgent.Agents[0].Inventory.Water == 0, "Drinking did not consume and restore exactly once.");

var ledger = new Ledger();
ledger.CreateAccount("a", 100);
ledger.CreateAccount("b", 50);
ledger.SealInitialState();
Check(ledger.Transfer(1, "a", "b", 25, "test"), "Valid transfer failed.");
Check(ledger.Accounts["a"] == 75 && ledger.Accounts["b"] == 75 && ledger.IsBalanced(), "Transfer did not conserve money.");
Check(!ledger.Transfer(2, "a", "b", 100, "invalid") && ledger.IsBalanced(), "Invalid transfer changed the ledger.");

var beforeSave = new Simulation(WorldDefinition.Demo());
beforeSave.Advance(23.7);
var snapshot = beforeSave.CaptureSnapshot();
var restored = Simulation.Restore(WorldDefinition.Demo(), snapshot);
Check(beforeSave.StateHash() == restored.StateHash(), "Snapshot restore changed the state hash.");
beforeSave.Advance(12.3);
restored.Advance(12.3);
Check(beforeSave.StateHash() == restored.StateHash(), "Restored simulation diverged after continuing.");

var host = new ProtocolHost(new Simulation(WorldDefinition.Demo()));
var response = host.Handle("""{"protocol":1,"kind":"advance","id":"test-1","payload":{"seconds":1.0}}""");
using (var responseJson = JsonDocument.Parse(response))
{
    Check(responseJson.RootElement.GetProperty("kind").GetString() == "result", "Headless protocol rejected a valid command.");
    var payload = responseJson.RootElement.GetProperty("payload");
    Check(payload.GetProperty("balanced").GetBoolean(), "Protocol snapshot lost the audit result.");
    Check(payload.GetProperty("stateHash").GetString()?.Length == 64, "Protocol snapshot omitted the deterministic hash.");
}

var badResponse = host.Handle("""{"protocol":1,"kind":"set_need","id":"bad-7","payload":{"agentId":999,"need":"thirst","value":5}}""");
using (var badJson = JsonDocument.Parse(badResponse))
{
    Check(badJson.RootElement.GetProperty("id").GetString() == "bad-7" &&
          badJson.RootElement.GetProperty("payload").GetProperty("code").GetString() == "invalid_need",
          "Protocol error lost the command ID.");
}

var exportResponse = host.Handle("""{"protocol":1,"kind":"export_snapshot","id":"save-1","payload":{}}""");
using var exportJson = JsonDocument.Parse(exportResponse);
var exportedSnapshot = exportJson.RootElement.GetProperty("payload").GetProperty("snapshot").GetRawText();
var restoreCommand = JsonSerializer.Serialize(new { protocol = 1, kind = "restore", id = "load-1", payload = new { snapshot = JsonSerializer.Deserialize<JsonElement>(exportedSnapshot) } });
var restoreResponse = host.Handle(restoreCommand);
using (var restoreJson = JsonDocument.Parse(restoreResponse))
    Check(restoreJson.RootElement.GetProperty("kind").GetString() == "result", "Protocol could not restore its exported snapshot.");

var portableWorld = WorldDefinition.Demo();
portableWorld.Agents[0].Inventory.Food = 0;
portableWorld.Agents[0].Needs.Hunger = 5;
var portableSimulation = new Simulation(portableWorld);
portableSimulation.Advance(0.6);
var portableSnapshot = portableSimulation.CaptureSnapshot();
Check(portableSnapshot.Ledger.Entries.Count > 0, "Portable snapshot omitted ledger history.");
Check(portableSnapshot.PendingEvents.Count > 0, "Portable snapshot omitted pending events.");
var snapshotJson = JsonSerializer.Serialize(portableSnapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
var decodedSnapshot = JsonSerializer.Deserialize<SimulationSnapshot>(snapshotJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
var roundTripSimulation = Simulation.Restore(portableWorld, decodedSnapshot);
Check(portableSimulation.StateHash() == roundTripSimulation.StateHash(), "JSON snapshot round trip changed full state.");
Check(roundTripSimulation.Ledger.Entries.Count == portableSimulation.Ledger.Entries.Count, "JSON round trip lost ledger entries.");
Check(roundTripSimulation.CaptureSnapshot().PendingEvents.Count == portableSnapshot.PendingEvents.Count, "JSON round trip lost events.");

var mutableWorld = WorldDefinition.Demo();
var isolatedSimulation = new Simulation(mutableWorld);
mutableWorld.Market.FoodStock = 0;
mutableWorld.Agents[0].Needs.Thirst = 0;
var exposedWorld = isolatedSimulation.WorldDefinition;
exposedWorld.Market.WaterStock = 0;
Check(isolatedSimulation.FoodStock > 0 && isolatedSimulation.WaterStock > 0 && isolatedSimulation.Agents[0].Needs.Thirst > 0,
    "External world mutation leaked into a running simulation.");

var tamperedSnapshot = isolatedSimulation.CaptureSnapshot();
tamperedSnapshot.Ledger.Accounts["agent:1"]++;
var tamperedRejected = false;
try { _ = Simulation.Restore(isolatedSimulation.WorldDefinition, tamperedSnapshot); }
catch (ArgumentException) { tamperedRejected = true; }
Check(tamperedRejected, "Unbalanced snapshot ledger was accepted.");
var invalid = WorldDefinition.Demo();
invalid.Market.FoodStock = -1;
var rejected = false;
try { _ = new Simulation(invalid); }
catch (ArgumentException) { rejected = true; }
Check(rejected, "Invalid negative stock was accepted.");

var hugeAdvanceRejected = false;
try { new Simulation(WorldDefinition.Demo()).Advance(20_000); }
catch (ArgumentOutOfRangeException) { hugeAdvanceRejected = true; }
Check(hugeAdvanceRejected, "An unsafe advance request was accepted.");

Console.WriteLine($"Portable core tests: {checks} checks, 0 failures");
