using System.Text.Json;
using System.Text.Json.Serialization;
using SimulationLife.Core;

namespace SimulationLife.Protocol;

public sealed class ProtocolEnvelope
{
    [JsonPropertyName("protocol")] public int Protocol { get; set; } = 1;
    [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("payload")] public JsonElement Payload { get; set; }
}

public sealed class ProtocolHost
{
    private Simulation simulation;
    private readonly WorldDefinition definition;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public ProtocolHost(Simulation simulation)
    {
        this.simulation = simulation;
        definition = simulation.WorldDefinition;
    }

    public string Handle(string json)
    {
        var commandId = string.Empty;
        try
        {
            var command = JsonSerializer.Deserialize<ProtocolEnvelope>(json, jsonOptions)
                ?? throw new JsonException("Empty command.");
            commandId = command.Id;
            if (command.Protocol != 1 || string.IsNullOrWhiteSpace(command.Id))
                return Error(command.Id, "invalid_envelope");
            switch (command.Kind)
            {
                case "advance":
                    simulation.Advance(command.Payload.GetProperty("seconds").GetDouble());
                    return Result(command.Id, SnapshotPayload());
                case "set_need":
                    if (!simulation.SetNeed(
                        command.Payload.GetProperty("agentId").GetInt32(),
                        command.Payload.GetProperty("need").GetString() ?? string.Empty,
                        command.Payload.GetProperty("value").GetDouble()))
                        return Error(command.Id, "invalid_need");
                    return Result(command.Id, SnapshotPayload());
                case "snapshot":
                    return Result(command.Id, SnapshotPayload());
                case "export_snapshot":
                {
                    var events = simulation.DrainEvents();
                    var snapshot = simulation.CaptureSnapshot();
                    return Result(command.Id, new
                    {
                        stateHash = SnapshotHasher.Compute(snapshot),
                        snapshot,
                        events
                    });
                }
                case "restore":
                {
                    var snapshot = command.Payload.GetProperty("snapshot").Deserialize<SimulationSnapshot>(jsonOptions)
                        ?? throw new JsonException("Missing snapshot.");
                    simulation = Simulation.Restore(definition, snapshot);
                    return Result(command.Id, SnapshotPayload());
                }
                default:
                    return Error(command.Id, "unsupported_command");
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return Error(commandId, "advance_too_large");
        }
        catch (ArgumentException)
        {
            return Error(commandId, "invalid_state");
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return Error(commandId, "invalid_json");
        }
    }

    public string SerializeSnapshot(bool indented = false)
    {
        var options = new JsonSerializerOptions(jsonOptions) { WriteIndented = indented };
        return JsonSerializer.Serialize(SnapshotPayload(), options);
    }

    private object SnapshotPayload()
    {
        var events = simulation.DrainEvents();
        var snapshot = simulation.CaptureSnapshot();
        return new
        {
            stateHash = SnapshotHasher.Compute(snapshot),
            tick = simulation.Tick,
            totalMinutes = simulation.TotalMinutes,
            randomState = simulation.Random.State,
            market = new { food = simulation.FoodStock, water = simulation.WaterStock },
            accounts = simulation.Ledger.Accounts,
            balanced = simulation.Ledger.IsBalanced(),
            agents = simulation.Agents.Select(agent => new
            {
                agent.Id,
                agent.Name,
                agent.Age,
                alive = agent.Alive,
                action = agent.Action.ToString(),
                reason = agent.DecisionReason,
                needs = agent.Needs,
                inventory = agent.Inventory,
                moneyCents = simulation.Ledger.Accounts[agent.Account]
            }),
            events
        };
    }

    private string Result(string id, object payload) => JsonSerializer.Serialize(new
    {
        protocol = 1,
        kind = "result",
        id,
        payload
    }, jsonOptions);

    private string Error(string id, string code) => JsonSerializer.Serialize(new
    {
        protocol = 1,
        kind = "error",
        id,
        payload = new { code }
    }, jsonOptions);
}
