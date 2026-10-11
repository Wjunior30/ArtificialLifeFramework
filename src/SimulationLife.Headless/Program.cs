using System.Text.Json;
using SimulationLife.Core;
using SimulationLife.Protocol;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    PropertyNameCaseInsensitive = true
};

WorldDefinition definition = WorldDefinition.Demo();
var worldIndex = Array.IndexOf(args, "--world");
if (worldIndex >= 0)
{
    if (worldIndex + 1 >= args.Length)
        throw new ArgumentException("--world requires a JSON file path.");
    var source = File.ReadAllText(args[worldIndex + 1]);
    definition = JsonSerializer.Deserialize<WorldDefinition>(source, jsonOptions)
        ?? throw new InvalidOperationException("The world file is empty.");
}

var simulation = new Simulation(definition);
var host = new ProtocolHost(simulation);
var demoIndex = Array.IndexOf(args, "--demo");
if (demoIndex >= 0)
{
    if (demoIndex + 1 >= args.Length || !double.TryParse(args[demoIndex + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
        throw new ArgumentException("--demo requires a duration in seconds.");
    simulation.Advance(seconds);
    Console.WriteLine(host.SerializeSnapshot(indented: true));
    return;
}

string? line;
while ((line = Console.ReadLine()) is not null)
{
    if (!string.IsNullOrWhiteSpace(line))
        Console.WriteLine(host.Handle(line));
}
