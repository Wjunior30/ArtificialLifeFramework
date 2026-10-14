using System.Globalization;

namespace ArtificialLife.Core;

/// <summary>Opt-in food transport using the existing physical caravan system. Owns advancement of its simulation.</summary>
public sealed class SettlementTradeBridge
{
    public SettlementSimulation Simulation { get; }
    public RegionalNetworkSystem Network { get; } = new();
    public double DistancePerDay { get; }
    public SettlementTradeBridge(SettlementSimulation simulation, double distancePerDay = 100)
    {
        Simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        if (!double.IsFinite(distancePerDay) || distancePerDay <= 0) throw new ArgumentOutOfRangeException(nameof(distancePerDay));
        DistancePerDay = distancePerDay; Synchronize();
    }
    private static string Key(int id) => id.ToString(CultureInfo.InvariantCulture);
    private void Synchronize()
    {
        foreach (var town in Simulation.Settlements)
            if (!Network.Settlements.ContainsKey(Key(town.Id))) Network.Add(new Settlement(Key(town.Id), Simulation.Sites[town.SiteId].Position));
    }
    public Caravan DispatchFood(int from, int to, long quantity)
    {
        if (quantity <= 0 || from == to) throw new ArgumentOutOfRangeException(nameof(quantity));
        var source = Simulation.Settlements.Single(x => x.Id == from);
        _ = Simulation.Settlements.Single(x => x.Id == to);
        if (source.Food < quantity) throw new InvalidOperationException("Insufficient food.");
        Synchronize();
        var stock = Network.Settlements[Key(from)].Stock;
        stock.Add("food", quantity);
        Caravan cargo;
        try { cargo = Network.Dispatch(Key(from), Key(to), "food", quantity); }
        catch { stock.Add("food", -quantity); throw; }
        source.Food -= quantity;
        return cargo;
    }
    public void Advance(int days)
    {
        if (days < 0 || days > 100_000) throw new ArgumentOutOfRangeException(nameof(days));
        for (int i = 0; i < days; i++)
        {
            Simulation.Advance(1); Synchronize(); Network.Tick(DistancePerDay);
            foreach (var destination in Network.Settlements.Values)
            {
                long quantity = destination.Stock.Quantity("food");
                if (quantity == 0) continue;
                var town = Simulation.Settlements.SingleOrDefault(x => Key(x.Id) == destination.Id);
                if (town is null) Simulation.Statistics.FoodLost += quantity;
                else
                {
                    double capacity = Simulation.Rules.FoodStoragePerPerson == 0 ? double.MaxValue : town.Population * Simulation.Rules.FoodStoragePerPerson;
                    double received = Math.Min(quantity, Math.Max(0, capacity - town.Food));
                    town.Food += received; Simulation.Statistics.FoodLost += quantity - received;
                }
                destination.Stock.Add("food", -quantity);
            }
            Network.RemoveDeliveredCaravans();
        }
    }
    public SimulationSave CaptureState() => SaveFormat.Capture("settlement-trade", w =>
    {
        w.Write(DistancePerDay); var save = Simulation.CaptureState();
        w.Write(save.Schema); w.Extension(new(save.Kind, save.Payload)); w.Write(save.Checksum); Network.WriteState(w);
    });
    public static SettlementTradeBridge Restore(SimulationSave save, RestoreHandlers? handlers = null)
        => SaveFormat.Restore(save, "settlement-trade", r =>
        {
            double distance = r.Number(double.Epsilon); int schema = r.Int(1); var state = r.Extension(); string hash = r.Name();
            var sim = SettlementSimulation.Restore(new() { Schema = schema, Kind = state.Kind, Payload = state.Payload, Checksum = hash }, handlers);
            var bridge = new SettlementTradeBridge(sim, distance);
            bridge.Network.ClearForRestore(); bridge.Network.ReadState(r);
            foreach (var town in sim.Settlements)
                if (!bridge.Network.Settlements.TryGetValue(Key(town.Id), out var entry) || entry.Position != sim.Sites[town.SiteId].Position)
                    throw new ArgumentException("Transport network does not match settlements.");
            return bridge;
        });
}

public sealed partial class RegionalNetworkSystem
{
    internal void RemoveDeliveredCaravans() => _caravans.RemoveAll(x => x.Arrived && !x.Cargo.All.Any());
    internal void ClearForRestore() { _caravans.Clear(); _settlements.Clear(); }
}
