namespace ArtificialLife.Core;

public sealed partial class SettlementSimulation
{
    private void AdvanceDiplomacy()
    {
        for (int i = 0; i < Settlements.Count; i++) for (int j = i + 1; j < Settlements.Count; j++)
        {
            EmergentSettlement first = Settlements[i], second = Settlements[j]; var key = Key(first.Id, second.Id);
            double previous = _relations.GetValueOrDefault(key); double distance = Distance(Sites[first.SiteId].Position, Sites[second.SiteId].Position);
            double trade = Math.Max(0, 1 - distance / 2800) * 1.2;
            double rivalry = Math.Max(0, 1 - distance / 1300) * 2.5 * _options.ConflictMultiplier;
            double current = Math.Clamp(previous + trade - rivalry - Math.Max(0, _options.ConflictMultiplier - 1) * .65 + Range(-2.8, 2.2) * _options.ConflictMultiplier, -100, 100);
            _relations[key] = current;
            if (_rules.Diplomacy == DiplomacyMode.Negotiated && _conflicts.TryGetValue(key, out var war))
            {
                if (war.Active && (current > _rules.WarThreshold || ShouldMakePeace(first, second, war))) { EndWar(key, true); continue; }
                if (!war.Active && Day < war.TruceUntilDay) continue;
            }
            if (previous < _rules.AllianceThreshold && current >= _rules.AllianceThreshold) { Statistics.Alliances++; _history.Add(new(Day, "alliance", first.Id, second.Id)); }
            if (_rules.Diplomacy == DiplomacyMode.Negotiated)
            { if (current <= _rules.WarThreshold) DeclareWar(first, second); }
            else
            {
                if (previous > _rules.WarThreshold && current <= _rules.WarThreshold) DeclareWar(first, second);
                else if (previous <= _rules.WarThreshold && current > _rules.WarThreshold) EndWar(key, false);
            }
        }
    }

}
