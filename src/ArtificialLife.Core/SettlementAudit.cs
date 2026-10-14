namespace ArtificialLife.Core;

public sealed partial class SettlementSimulation
{
    public IReadOnlyList<string> Audit()
    {
        var errors = new List<string>(); var occupied = new HashSet<int>();
        if (Nomads < 0) errors.Add("Negative nomad population.");
        if ((long)Nomads + Settlements.Sum(x => (long)x.Population) != (long)_rules.InitialPopulation + Statistics.ExternalArrivals + Statistics.Births - Statistics.Deaths)
            errors.Add("Population conservation failed.");
        foreach (EmergentSettlement settlement in Settlements)
        {
            if (!settlement.IsAlive) errors.Add($"Destroyed settlement {settlement.Id} remains active.");
            if (settlement.Population <= 0 || settlement.Population > _rules.MaximumPopulation) errors.Add($"Settlement {settlement.Id} has population outside the permitted range.");
            if (settlement.Buildings < 0 || settlement.Food < 0 || settlement.Materials < 0) errors.Add($"Settlement {settlement.Id} has negative state.");
            if (!double.IsFinite(settlement.Food) || !double.IsFinite(settlement.Materials) ||
                !double.IsFinite(settlement.Prosperity) || !double.IsFinite(settlement.Defense) ||
                settlement.Prosperity < 0 || settlement.Prosperity > 100 || settlement.Defense < 0)
                errors.Add($"Settlement {settlement.Id} has invalid numeric state.");
            if (!occupied.Add(settlement.SiteId)) errors.Add($"Site {settlement.SiteId} has multiple settlements.");
            if (settlement.SiteId < 0 || settlement.SiteId >= Sites.Count || Sites[settlement.SiteId].OccupiedBy != settlement.Id)
                errors.Add($"Site ownership mismatch for {settlement.Id}.");
        }
        foreach (SettlementSite site in Sites)
            if (site.OccupiedBy != 0 && !Settlements.Any(x => x.Id == site.OccupiedBy && x.SiteId == site.Id))
                errors.Add($"Site {site.Id} references a missing settlement.");
        foreach (var town in Settlements)
            if (!double.IsFinite(town.WarExhaustion) || town.WarExhaustion < 0 || town.WarExhaustion > 100) errors.Add("Invalid war exhaustion.");
        var living = Settlements.Select(x => x.Id).ToHashSet();
        foreach (var war in _conflicts.Values)
            if (war.Active && (!living.Contains(war.First) || !living.Contains(war.Second) || war.EndedDay != 0)) errors.Add("Invalid active conflict.");
        if (_relations.Keys.Any(x => !living.Contains(x.Item1) || !living.Contains(x.Item2)) || _relations.Count != living.Count * (living.Count - 1) / 2)
            errors.Add("Incomplete or stale diplomatic relations.");
        if (!double.IsFinite(Statistics.FoodLost) || !double.IsFinite(Statistics.MaterialsLost)) errors.Add("Invalid resource statistics.");
        if (_relations.Values.Any(x => !double.IsFinite(x))) errors.Add("Invalid diplomatic relation.");
        return errors;
    }

}
