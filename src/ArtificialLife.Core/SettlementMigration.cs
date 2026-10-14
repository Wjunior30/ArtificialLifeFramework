namespace ArtificialLife.Core;

public sealed partial class SettlementSimulation
{
    private void RedistributePopulation()
    {
        foreach (EmergentSettlement settlement in Settlements)
            if (settlement.Population >= _rules.MigrationMinimumPopulation &&
                (long)settlement.Population >= (long)_rules.FounderCount + _rules.MigrationRemainingPopulation &&
                settlement.Food >= _rules.MigrationMinimumFood && Nomads < _rules.MigrationNomadLimit && Chance(_rules.MigrationProbability))
            { settlement.Population -= _rules.FounderCount; Nomads += _rules.FounderCount; Statistics.Migrations += _rules.FounderCount; _history.Add(new(Day, "migration", settlement.Id, People: _rules.FounderCount)); }
    }

}
