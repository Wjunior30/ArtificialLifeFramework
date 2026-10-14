namespace ArtificialLife.Core;

public sealed partial class SettlementSimulation
{
    public SimulationSave CaptureState() => SaveFormat.Capture("settlements", WriteState, 2);
    /// <summary>Accepts schemas 1 and 2. Schema 1 uses legacy resource defaults and built-in policies.</summary>
    public static SettlementSimulation Restore(SimulationSave save, RestoreHandlers? handlers = null)
        => SaveFormat.Restore(save, "settlements", r => ReadState(r, save.Schema, handlers), 2);
    public string StateHash() => CaptureState().Checksum;

    internal void WriteState(StateWriter w)
    {
        w.Write(Seed); w.Write(_options.FoodMultiplier); w.Write(_options.ConflictMultiplier); _rules.WriteState(w);
        w.Write(Day); w.Write(Nomads); w.Write(_nextId); w.Write(_random.State);
        w.Write(Sites.Count);
        foreach (var site in Sites)
        {
            w.Write(site.Id); w.Write(site.Position.X); w.Write(site.Position.Y);
            w.Write(site.Water); w.Write(site.Fertility); w.Write(site.Defense); w.Write(site.Routes);
            w.Write(site.OccupiedBy); w.Write(site.PreviousOwner); w.Write(site.RuinedUntilDay);
        }
        w.Write(Settlements.Count);
        foreach (var town in Settlements)
        {
            w.Write(town.Id); w.Write(town.SiteId); w.Write(town.FoundedDay); w.Write(town.Population); w.Write(town.Buildings);
            w.Write((int)town.Stage); w.Write(town.Food); w.Write(town.Materials); w.Write(town.Prosperity); w.Write(town.Defense);
            w.Write(town.IsAlive); w.Write(town.WarExhaustion);
        }
        w.Write(_relations.Count);
        foreach (var pair in _relations.OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2))
        { w.Write(pair.Key.Item1); w.Write(pair.Key.Item2); w.Write(pair.Value); }
        w.Write(_conflicts.Count);
        foreach (var war in Conflicts)
        { w.Write(war.First); w.Write(war.Second); w.Write(war.StartedDay); w.Write(war.EndedDay); w.Write(war.TruceUntilDay); w.Write(war.Active); }
        foreach (int value in new[] { Statistics.Founded, Statistics.Upgrades, Statistics.Alliances, Statistics.Wars, Statistics.Battles,
            Statistics.Destroyed, Statistics.Refounded, Statistics.Deaths, Statistics.Births, Statistics.Migrations,
            Statistics.PeakSettlements, Statistics.PeakPopulation, Statistics.Peaces, Statistics.CompletedWars, Statistics.StarvationDeaths }) w.Write(value);
        w.Write(Statistics.TotalWarDays); w.Write(Statistics.HungryPersonDays);
        w.Write(History.Count);
        foreach (var entry in History)
        { w.Write(entry.Day); w.Write(entry.Kind); w.Write(entry.SettlementId); w.Write(entry.OtherId); w.Write(entry.People); w.Write(entry.Detail); }
        _rules.WriteAdditionalState(w); _policies.WriteState(w);
        w.Write(Statistics.PopulationDays); w.Write(Statistics.ActiveWarDays); w.Write(Statistics.FoodLost); w.Write(Statistics.MaterialsLost);
    }

    internal static SettlementSimulation ReadState(StateReader r, int schema, RestoreHandlers? handlers)
    {
        ulong seed = r.ReadUInt64(); var options = new SettlementSimulationOptions(r.Number(0, 1000), r.Number(0, 1000));
        var rules = SettlementRules.ReadState(r);
        var sim = new SettlementSimulation(seed, options, rules);
        sim.Day = r.Int(0, int.MaxValue - Math.Max(rules.RuinMaximumDays, rules.TruceDays));
        sim.Nomads = r.Int(); sim._nextId = r.Int(1); sim._random = DeterministicRandom.FromState(r.ReadUInt64());
        sim._sites.Clear(); sim._settlements.Clear(); sim._relations.Clear(); sim._history.Clear();
        int count = r.Int(1, 512);
        if (count != rules.SiteCount) throw new ArgumentException("Site count does not match rules.");
        for (int i = 0; i < count; i++)
        {
            int id = r.Int(); if (id != i) throw new ArgumentException("Invalid site order.");
            sim._sites.Add(new(id, new(r.Number(-double.MaxValue), r.Number(-double.MaxValue)), r.Number(0, 1), r.Number(0, 1), r.Number(0, 1), r.Number(0, 1))
            { OccupiedBy = r.Int(), PreviousOwner = r.Int(), RuinedUntilDay = r.Int() });
        }
        count = r.Int(0, rules.SiteCount); var ids = new HashSet<int>();
        for (int i = 0; i < count; i++)
        {
            var town = new EmergentSettlement(r.Int(1, sim._nextId - 1), r.Int(0, rules.SiteCount - 1), r.Int(0, sim.Day))
            {
                Population = r.Int(1, rules.MaximumPopulation), Buildings = r.Int(), Stage = r.EnumValue<SettlementStage>(),
                Food = r.Number(), Materials = r.Number(), Prosperity = r.Number(0, 100), Defense = r.Number(),
                IsAlive = r.ReadBoolean(), WarExhaustion = r.Number(0, 100)
            };
            if (!ids.Add(town.Id)) throw new ArgumentException("Duplicate settlement."); sim._settlements.Add(town);
        }
        count = r.Count();
        for (int i = 0; i < count; i++)
        {
            int first = r.Int(1), second = r.Int(1); double value = r.Number(-100_000, 100);
            if (first >= second || !ids.Contains(first) || !ids.Contains(second)) throw new ArgumentException("Invalid relation participants.");
            sim._relations.Add((first, second), value);
        }
        if (sim._relations.Count != ids.Count * (ids.Count - 1) / 2) throw new ArgumentException("Incomplete diplomatic relations.");
        count = r.Count();
        for (int i = 0; i < count; i++)
        {
            var war = new ConflictSnapshot(r.Int(1, sim._nextId - 1), r.Int(1, sim._nextId - 1), r.Int(0, sim.Day), r.Int(0, sim.Day), r.Int(), r.ReadBoolean());
            if (war.First >= war.Second || (war.Active && (!ids.Contains(war.First) || !ids.Contains(war.Second) || war.EndedDay != 0)) ||
                (!war.Active && war.EndedDay < war.StartedDay)) throw new ArgumentException("Invalid conflict state.");
            sim._conflicts.Add((war.First, war.Second), war);
        }
        var stats = sim.Statistics;
        stats.Founded = r.Int(); stats.Upgrades = r.Int(); stats.Alliances = r.Int(); stats.Wars = r.Int(); stats.Battles = r.Int();
        stats.Destroyed = r.Int(); stats.Refounded = r.Int(); stats.Deaths = r.Int(); stats.Births = r.Int(); stats.Migrations = r.Int();
        stats.PeakSettlements = r.Int(); stats.PeakPopulation = r.Int(); stats.Peaces = r.Int(); stats.CompletedWars = r.Int(); stats.StarvationDeaths = r.Int();
        stats.TotalWarDays = r.Long(); stats.HungryPersonDays = r.Long();
        count = r.Count(); var active = new HashSet<int>(); var allIds = new HashSet<int>(); int previousDay = 0;
        for (int i = 0; i < count; i++)
        {
            var entry = new SettlementHistoryEvent(r.Int(previousDay, sim.Day), r.Name(), r.Int(1, sim._nextId - 1), r.Int(0, sim._nextId - 1), r.Int(), schema >= 2 ? r.Int(-1) : 0);
            if (schema == 1 && entry.Kind == "upgrade") entry = entry with { Detail = -1 };
            if (entry.Kind == "upgrade" && entry.Detail > (int)SettlementStage.City) throw new ArgumentException("Invalid historical stage.");
            if (!new[] { "foundation", "upgrade", "building", "alliance", "war", "peace", "battle", "destruction", "refugees", "resettled", "migration" }.Contains(entry.Kind))
                throw new ArgumentException("Unknown history event.");
            if (entry.Kind == "foundation")
            { if (!allIds.Add(entry.SettlementId)) throw new ArgumentException("Duplicate foundation."); active.Add(entry.SettlementId); }
            else if (!active.Contains(entry.SettlementId) || (entry.OtherId != 0 && !active.Contains(entry.OtherId))) throw new ArgumentException("Invalid history participants.");
            if (entry.Kind == "destruction") active.Remove(entry.SettlementId);
            previousDay = entry.Day; sim._history.Add(entry);
        }
        if (schema >= 2)
        {
            sim._rules = rules.ReadAdditionalState(r);
            sim._policies = SettlementPolicies.ReadState(r, handlers);
            stats.PopulationDays = r.Long(); stats.ActiveWarDays = r.Long(); stats.FoodLost = r.Number(); stats.MaterialsLost = r.Number();
        }
        if (!active.SetEquals(ids) || stats.Founded != allIds.Count || stats.Founded - stats.Destroyed != ids.Count ||
            allIds.Count != sim._nextId - 1 || sim.Audit().Count != 0) throw new ArgumentException("Snapshot settlement audit failed.");
        return sim;
    }
}
