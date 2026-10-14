namespace ArtificialLife.Core;

public sealed partial class SettlementSimulation
{
    private void AdvanceSettlement(EmergentSettlement settlement)
    {
        settlement.WarExhaustion = Math.Max(0, settlement.WarExhaustion - _rules.ExhaustionRecoveryPerDay);
        SettlementSite site = Sites[settlement.SiteId]; int population = settlement.Population;
        int workers = Math.Max(1, (int)(population * _rules.WorkerFraction));
        double foodOutput = workers * (.92 + site.Fertility * .92) * _options.FoodMultiplier;
        settlement.Food += foodOutput - population * _rules.FoodConsumptionPerPerson;
        settlement.Materials += workers * (.28 + site.Routes * .28);
        settlement.Prosperity = Math.Clamp(settlement.Prosperity + (foodOutput / Math.Max(1, population) - .78) * .12, 0, 100);
        if (settlement.Food < 0)
        {
            Statistics.HungryPersonDays += population;
            int deaths = Math.Min(population - (_rules.AllowStarvationExtinction ? 0 : 1), Math.Max(0, (int)Math.Ceiling(-settlement.Food / 8)));
            settlement.Population -= deaths; Statistics.Deaths += deaths; settlement.Food = 0;
            Statistics.StarvationDeaths += deaths;
            if (settlement.Population == 0) { Destroy(settlement, 0); return; }
        }
        else if (population < _rules.MaximumPopulation && Chance(_rules.BirthProbability + settlement.Prosperity * _rules.ProsperityBirthBonus))
        { settlement.Population++; Statistics.Births++; }
        double spoiled = settlement.Food * _rules.FoodSpoilagePerDay;
        settlement.Food -= spoiled; Statistics.FoodLost += spoiled;
        double maintenance = Math.Min(settlement.Materials, settlement.Buildings * _rules.BuildingMaintenancePerDay);
        settlement.Materials -= maintenance; Statistics.MaterialsLost += maintenance;
        double cost = _rules.BuildingBaseCost + settlement.Buildings * _rules.BuildingCostGrowth;
        if (settlement.Materials >= cost && settlement.Population >= settlement.Buildings + 2)
        {
            settlement.Materials -= cost; settlement.Buildings++;
            settlement.Defense += settlement.Stage < SettlementStage.Town ? .18 : .32;
            _history.Add(new(Day, "building", settlement.Id, Detail: settlement.Buildings));
        }
        ApplyStorageLimits(settlement);
        UpdateStage(settlement);
    }

    private void ApplyStorageLimits(EmergentSettlement settlement)
    {
        if (_rules.FoodStoragePerPerson > 0)
        {
            double excess = Math.Max(0, settlement.Food - settlement.Population * _rules.FoodStoragePerPerson);
            settlement.Food -= excess; Statistics.FoodLost += excess;
        }
        if (_rules.MaterialStoragePerPerson > 0)
        {
            double excess = Math.Max(0, settlement.Materials - settlement.Population * _rules.MaterialStoragePerPerson);
            settlement.Materials -= excess; Statistics.MaterialsLost += excess;
        }
    }

    private void UpdateStage(EmergentSettlement settlement)
    {
        int target = (int)_policies.Progression.Evaluate(SettlementState.From(settlement), _rules);
        if (target < 0 || target > (int)SettlementStage.City) throw new InvalidOperationException("Progression returned an invalid stage.");
        if (target <= (int)settlement.Stage) return;
        settlement.Stage = (SettlementStage)target; Statistics.Upgrades++;
        _history.Add(new(Day, "upgrade", settlement.Id, Detail: target));
    }

}
