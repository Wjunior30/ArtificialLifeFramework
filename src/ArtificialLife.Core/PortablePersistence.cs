namespace ArtificialLife.Core;

public sealed partial class HouseholdCareNetwork
{
    internal void WriteState(StateWriter w)
    {
        w.Write(_nextId); w.Write(_requests.Count);
        foreach (var x in _requests)
        { w.Write(x.Id); w.Write(x.Requester.Value); w.Write(x.Good); w.Write(x.Quantity); w.Write(x.Priority); w.OptionalId(x.AssignedHelper); w.Write(x.Completed); }
    }
    internal void ReadState(StateReader r, LifeWorld world)
    {
        _nextId = r.Long(); int count = r.Count(); var ids = new HashSet<long>();
        for (int i = 0; i < count; i++)
        {
            var request = new CareRequest(r.Long(1), new(r.ReadUInt64()), r.Name(), r.Long(1), r.ReadInt32())
            { AssignedHelper = r.OptionalId(), Completed = r.ReadBoolean() };
            if (request.Id > _nextId || !ids.Add(request.Id) || !world.Agents.ContainsKey(request.Requester) ||
                (request.AssignedHelper.HasValue && !world.Agents.ContainsKey(request.AssignedHelper.Value))) throw new ArgumentException("Invalid care request.");
            _requests.Add(request);
        }
    }
}
public sealed partial class Caravan
{
    internal void RestoreProgress(double value) => Progress = value;
}
public sealed partial class RegionalNetworkSystem
{
    internal void WriteState(StateWriter w)
    {
        w.Write(_nextCaravan); w.Write(_settlements.Count);
        foreach (var x in _settlements.Values)
        { w.Write(x.Id); w.Write(x.Position.X); w.Write(x.Position.Y); w.Write(x.Guards); w.Write(x.Bandits); w.Goods(x.Stock); }
        w.Write(_caravans.Count);
        foreach (var x in _caravans)
        { w.Write(x.Id); w.Write(x.From); w.Write(x.To); w.Write(x.Distance); w.Write(x.Progress); w.Goods(x.Cargo); }
    }
    internal void ReadState(StateReader r)
    {
        _nextCaravan = r.Long(); int count = r.Count();
        for (int i = 0; i < count; i++)
        {
            var town = new Settlement(r.Name(), new(r.Number(-double.MaxValue), r.Number(-double.MaxValue))) { Guards = r.Int(), Bandits = r.Int() };
            r.Goods(town.Stock); Add(town);
        }
        count = r.Count(); var ids = new HashSet<long>();
        for (int i = 0; i < count; i++)
        {
            var caravan = new Caravan(r.Long(1), r.Name(), r.Name(), r.Number());
            caravan.RestoreProgress(r.Number(0, 1)); r.Goods(caravan.Cargo);
            if (caravan.Id > _nextCaravan || !ids.Add(caravan.Id) || !_settlements.ContainsKey(caravan.From) || !_settlements.ContainsKey(caravan.To) ||
                (caravan.Arrived && caravan.Cargo.All.Any())) throw new ArgumentException("Invalid caravan.");
            _caravans.Add(caravan);
        }
    }
}

public sealed partial class PortableSimulation
{
    private PortableSimulation(LifeWorld world, double step, double accumulator)
    { World = world; FixedStepHours = step; _accumulator = accumulator; }

    /// <summary>Saves clock remainder, world, profiles, aid, cargo and pending commands. Observers are engine-owned.</summary>
    public SimulationSave CaptureState() => SaveFormat.Capture("portable", w =>
    {
        w.Write(FixedStepHours); w.Write(_accumulator); World.WriteState(w);
        w.Write(_profiles.Count); foreach (var profile in _profiles.Values) profile.WriteState(w);
        Care.WriteState(w); Regions.WriteState(w);
        w.Write(_commands.Count);
        foreach (var command in _commands)
        {
            if (command is not IPersistentSimulationCommand persistent) throw new InvalidOperationException("Queued commands must implement IPersistentSimulationCommand.");
            w.Extension(persistent.CaptureState());
        }
    });
    public string StateHash() => CaptureState().Checksum;
    public static PortableSimulation Restore(SimulationSave save, RestoreHandlers? handlers = null)
        => SaveFormat.Restore(save, "portable", r =>
        {
            double step = r.Number(double.Epsilon), remainder = r.Number();
            if (remainder >= step) throw new ArgumentException("Invalid clock remainder.");
            var sim = new PortableSimulation(LifeWorld.ReadState(r, handlers), step, remainder);
            int count = r.Count();
            for (int i = 0; i < count; i++)
            {
                var profile = PersonProfile.ReadState(r);
                if (!sim.World.Agents.ContainsKey(profile.AgentId)) throw new ArgumentException("Profile references missing agent.");
                sim._profiles.Add(profile.AgentId, profile);
            }
            if (sim._profiles.Count != sim.World.Agents.Count) throw new ArgumentException("Incomplete profiles.");
            sim.Care.ReadState(r, sim.World); sim.Regions.ReadState(r);
            count = r.Count();
            for (int i = 0; i < count; i++) sim.Enqueue(handlers?.Command?.Invoke(r.Extension()) ?? throw new ArgumentException("A command restore handler is required."));
            return sim;
        });
}
