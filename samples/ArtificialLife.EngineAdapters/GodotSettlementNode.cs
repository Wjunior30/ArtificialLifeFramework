#if GODOT
using ArtificialLife.Core;
using Godot;
using System.Text.Json;

namespace ArtificialLife.Samples;

public partial class GodotSettlementNode : Node
{
    private readonly SettlementSession _session = new();
    public override void _Process(double delta)
    {
        foreach (var item in _session.Update(delta))
            if (item is SettlementFounded founded) GD.Print($"Settlement founded: {founded.SettlementId}");
    }
    public void SaveGame(string path) => System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(path), JsonSerializer.Serialize(_session.Save()));
    public void LoadGame(string path) => _session.Load(JsonSerializer.Deserialize<SimulationSave>(System.IO.File.ReadAllText(ProjectSettings.GlobalizePath(path)))!);
}
#endif
