namespace ArtificialLife.Core;

public sealed partial class SettlementSimulation
{
    private void ApplyDailyRules(EmergentSettlement town)
    {
        foreach (var rule in _policies.DailyRules)
        {
            var context = new SettlementContext(Day, Sites, Settlements.Where(x => x.IsAlive).Select(SettlementState.From).ToArray(), _rules);
            var effect = rule.Evaluate(SettlementState.From(town), context, ref _random);
            if (effect is null || effect.Births < 0 || effect.Deaths < 0 || effect.Deaths > town.Population ||
                (long)town.Population + effect.Births - effect.Deaths > _rules.MaximumPopulation ||
                !double.IsFinite(effect.Food) || !double.IsFinite(effect.Materials) || !double.IsFinite(effect.Prosperity) ||
                !double.IsFinite(town.Food + effect.Food) || !double.IsFinite(town.Materials + effect.Materials) ||
                town.Food + effect.Food < 0 || town.Materials + effect.Materials < 0)
                throw new InvalidOperationException("Daily policy returned an invalid effect.");
            town.Food += effect.Food; town.Materials += effect.Materials; town.Prosperity = Math.Clamp(town.Prosperity + effect.Prosperity, 0, 100);
            town.Population += effect.Births - effect.Deaths; Statistics.Births += effect.Births; Statistics.Deaths += effect.Deaths;
            if (town.Population == 0) { Destroy(town, 0); return; }
        }
    }
}
