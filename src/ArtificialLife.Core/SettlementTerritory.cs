namespace ArtificialLife.Core;

public sealed partial class SettlementSimulation
{
    private bool TryFoundSettlement(bool initial)
    {
        if (Nomads < _rules.FounderCount) return false;
        var candidates = Sites.Where(x => x.OccupiedBy == 0 && Day >= x.RuinedUntilDay)
            .Select(x => (Site: x, Score: SiteScore(x) + Range(-.04, .04))).OrderByDescending(x => x.Score).ToArray();
        if (candidates.Length == 0) return false;
        int pick = initial ? 0 : _random.Next(Math.Min(3, candidates.Length)); SettlementSite site = candidates[pick].Site;
        var settlement = new EmergentSettlement(_nextId++, site.Id, Day) { Defense = site.Defense * 2, Population = _rules.FounderCount };
        Nomads -= _rules.FounderCount; site.OccupiedBy = settlement.Id; _settlements.Add(settlement);
        foreach (EmergentSettlement other in Settlements.Where(x => x != settlement))
            _relations[Key(settlement.Id, other.Id)] = Range(-24, 20) - 18 * Math.Max(0, _options.ConflictMultiplier - 1);
        Statistics.Founded++; if (site.PreviousOwner != 0) Statistics.Refounded++;
        _history.Add(new(Day, "foundation", settlement.Id));
        if (site.PreviousOwner != 0) _history.Add(new(Day, "resettled", settlement.Id, Detail: site.PreviousOwner));
        return true;
    }

    private double SiteScore(SettlementSite site)
    {
        double score = _policies.SiteEvaluator.Evaluate(site, new SettlementContext(Day, Sites, Settlements.Select(SettlementState.From).ToArray(), _rules));
        if (!double.IsFinite(score)) throw new InvalidOperationException("Site evaluator returned a non-finite score.");
        return score;
    }

}
