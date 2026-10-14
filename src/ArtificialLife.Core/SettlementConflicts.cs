namespace ArtificialLife.Core;

public sealed partial class SettlementSimulation
{
    private void ResolveWars()
    {
        for (int i = 0; i < Settlements.Count; i++) for (int j = i + 1; j < Settlements.Count; j++)
        {
            EmergentSettlement first = Settlements[i], second = Settlements[j];
            if (!first.IsAlive || !second.IsAlive) continue;
            var key = Key(first.Id, second.Id);
            if (_rules.Diplomacy == DiplomacyMode.Negotiated)
            {
                if (!_conflicts.TryGetValue(key, out var war) || !war.Active) continue;
                if (ShouldMakePeace(first, second, war)) { EndWar(key, true); continue; }
            }
            if (_relations.GetValueOrDefault(key) > _rules.WarThreshold || !Chance(Math.Min(.75, .32 * _options.ConflictMultiplier))) continue;
            ResolveBattle(first, second);
        }
    }

    private void ResolveBattle(EmergentSettlement first, EmergentSettlement second)
    {
        if (_rules.Diplomacy == DiplomacyMode.Negotiated)
        {
            first.Food = Math.Max(0, first.Food - first.Population * _rules.BattleFoodCostPerPerson);
            second.Food = Math.Max(0, second.Food - second.Population * _rules.BattleFoodCostPerPerson);
            first.WarExhaustion = Math.Min(100, first.WarExhaustion + _rules.ExhaustionPerBattle);
            second.WarExhaustion = Math.Min(100, second.WarExhaustion + _rules.ExhaustionPerBattle);
        }
        var result = _policies.ConflictResolver.Resolve(new ConflictContext(Day, SettlementState.From(first), SettlementState.From(second), _rules), ref _random);
        EmergentSettlement winner = result.FirstWins ? first : second, loser = result.FirstWins ? second : first;
        if (result.WinnerDeaths < 0 || result.WinnerDeaths >= winner.Population || result.LoserDeaths < 0 || result.LoserDeaths >= loser.Population ||
            result.BuildingsLost < 0 || !double.IsFinite(result.DefenseLost) || result.DefenseLost < 0)
            throw new InvalidOperationException("Conflict resolver returned an invalid result.");
        int winnerLosses = result.WinnerDeaths, loserLosses = result.LoserDeaths;
        winner.Population -= winnerLosses; loser.Population -= loserLosses;
        loser.Buildings = Math.Max(0, loser.Buildings - result.BuildingsLost); loser.Defense = Math.Max(0, loser.Defense - result.DefenseLost);
        Statistics.Deaths += winnerLosses + loserLosses; Statistics.Battles++;
        _history.Add(new(Day, "battle", winner.Id, loser.Id, winnerLosses + loserLosses));
        if (loser.Population < 5 || loser.Buildings <= 1) Destroy(loser, winner.Id);
    }

    private void Destroy(EmergentSettlement settlement, int attacker)
    {
        if (!settlement.IsAlive) return; settlement.IsAlive = false; Nomads += settlement.Population;
        foreach (var key in _conflicts.Keys.Where(x => x.Item1 == settlement.Id || x.Item2 == settlement.Id).ToArray()) EndWar(key, false);
        Statistics.Migrations += settlement.Population; Statistics.Destroyed++;
        SettlementSite site = Sites[settlement.SiteId]; site.PreviousOwner = settlement.Id; site.OccupiedBy = 0; site.RuinedUntilDay = Day + NextInclusive(_rules.RuinMinimumDays, _rules.RuinMaximumDays);
        if (settlement.Population > 0) _history.Add(new(Day, "refugees", settlement.Id, People: settlement.Population));
        _history.Add(new(Day, "destruction", settlement.Id, attacker, settlement.Population));
    }

}
