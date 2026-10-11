namespace SimulationLife.Core;

internal static class WorldValidator
{
    private const int MaximumAgents = 100_000;
    private static readonly HashSet<string> Distributions = new(StringComparer.OrdinalIgnoreCase)
    {
        "market", "need"
    };

    public static WorldDefinition Validate(WorldDefinition value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (value.Schema != 1)
            throw new ArgumentException("Unsupported world schema.", nameof(value));
        if (!FinitePositive(value.FixedStepSeconds) || value.FixedStepSeconds > 1 ||
            !FinitePositive(value.WorldMinutesPerSecond) || value.WorldMinutesPerSecond > 1_000 ||
            !double.IsFinite(value.StartMinute) || value.StartMinute < 0)
            throw new ArgumentException("Invalid clock configuration.", nameof(value));
        if (value.Market is null || string.IsNullOrWhiteSpace(value.Market.Account) ||
            value.Market.Account.Length > 128 || value.Market.InitialMoneyCents < 0 ||
            value.Market.FoodStock < 0 || value.Market.WaterStock < 0 ||
            value.Market.FoodPriceCents < 0 || value.Market.WaterPriceCents < 0)
            throw new ArgumentException("Invalid market configuration.", nameof(value));
        if (string.IsNullOrWhiteSpace(value.Distribution) || !Distributions.Contains(value.Distribution))
            throw new ArgumentException("Unsupported distribution mode.", nameof(value));
        if (value.Agents is null || value.Agents.Count == 0 || value.Agents.Count > MaximumAgents ||
            value.Agents.Any(agent => agent is null) ||
            value.Agents.Select(agent => agent.Id).Distinct().Count() != value.Agents.Count)
            throw new ArgumentException("The world needs a bounded set of agents with unique IDs.", nameof(value));

        foreach (var agent in value.Agents)
        {
            if (agent.Id <= 0 || string.IsNullOrWhiteSpace(agent.Name) || agent.Name.Length > 256 ||
                agent.Age < 0 || agent.Age > 200 || agent.InitialMoneyCents < 0 ||
                agent.Needs is null || agent.Inventory is null ||
                string.IsNullOrWhiteSpace(agent.EmployerAccount) || agent.EmployerAccount.Length > 128 ||
                !string.Equals(agent.EmployerAccount, value.Market.Account, StringComparison.Ordinal) ||
                agent.Inventory.Food < 0 || agent.Inventory.Water < 0 || agent.Inventory.Medicine < 0 ||
                !ValidNeeds(agent.Needs))
                throw new ArgumentException($"Invalid agent configuration for ID {agent.Id}.", nameof(value));
        }
        return value;
    }

    public static WorldDefinition Clone(WorldDefinition source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return new WorldDefinition
        {
            Schema = source.Schema,
            Seed = source.Seed,
            FixedStepSeconds = source.FixedStepSeconds,
            WorldMinutesPerSecond = source.WorldMinutesPerSecond,
            StartMinute = source.StartMinute,
            Distribution = source.Distribution,
            Market = source.Market is null ? null! : new MarketDefinition
            {
                Account = source.Market.Account,
                InitialMoneyCents = source.Market.InitialMoneyCents,
                FoodStock = source.Market.FoodStock,
                WaterStock = source.Market.WaterStock,
                FoodPriceCents = source.Market.FoodPriceCents,
                WaterPriceCents = source.Market.WaterPriceCents
            },
            Agents = source.Agents is null ? null! : source.Agents.Select(agent => agent is null ? null! : new AgentDefinition
            {
                Id = agent.Id,
                Name = agent.Name,
                Age = agent.Age,
                EmployerAccount = agent.EmployerAccount,
                InitialMoneyCents = agent.InitialMoneyCents,
                Needs = agent.Needs?.Copy()!,
                Inventory = agent.Inventory?.Copy()!
            }).ToList()
        };
    }

    public static bool ValidNeeds(Needs needs) =>
        needs is not null && InRange(needs.Hunger) && InRange(needs.Thirst) && InRange(needs.Energy) &&
        InRange(needs.Safety) && InRange(needs.Social) && InRange(needs.Comfort);

    private static bool InRange(double value) => double.IsFinite(value) && value >= 0 && value <= 100;
    private static bool FinitePositive(double value) => double.IsFinite(value) && value > 0;
}
