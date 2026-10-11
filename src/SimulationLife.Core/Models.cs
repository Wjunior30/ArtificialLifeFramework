namespace SimulationLife.Core;

public enum AgentAction
{
    Idle,
    Eat,
    Drink,
    Sleep,
    BuyFood,
    BuyWater,
    Work,
    Socialize
}

public sealed class Needs
{
    public double Hunger { get; set; } = 85;
    public double Thirst { get; set; } = 85;
    public double Energy { get; set; } = 85;
    public double Safety { get; set; } = 100;
    public double Social { get; set; } = 65;
    public double Comfort { get; set; } = 70;

    public Needs Copy() => new()
    {
        Hunger = Hunger,
        Thirst = Thirst,
        Energy = Energy,
        Safety = Safety,
        Social = Social,
        Comfort = Comfort
    };
}

public sealed class Inventory
{
    public int Food { get; set; }
    public int Water { get; set; }
    public int Medicine { get; set; }

    public Inventory Copy() => new() { Food = Food, Water = Water, Medicine = Medicine };
}

public sealed class AgentDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = "Agent";
    public int Age { get; set; } = 18;
    public string EmployerAccount { get; set; } = "firm:market";
    public long InitialMoneyCents { get; set; } = 10_000;
    public Needs Needs { get; set; } = new();
    public Inventory Inventory { get; set; } = new();
}

public sealed class MarketDefinition
{
    public string Account { get; set; } = "firm:market";
    public long InitialMoneyCents { get; set; } = 30_000;
    public int FoodStock { get; set; } = 30;
    public int WaterStock { get; set; } = 30;
    public long FoodPriceCents { get; set; } = 400;
    public long WaterPriceCents { get; set; } = 200;
}

public sealed class WorldDefinition
{
    public int Schema { get; set; } = 1;
    public uint Seed { get; set; } = 173_133_397;
    public double FixedStepSeconds { get; set; } = 0.1;
    public double WorldMinutesPerSecond { get; set; } = 10;
    public double StartMinute { get; set; } = 480;
    public string Distribution { get; set; } = "market";
    public MarketDefinition Market { get; set; } = new();
    public List<AgentDefinition> Agents { get; set; } = new();

    public static WorldDefinition Demo() => new()
    {
        Agents = new List<AgentDefinition>
        {
            new()
            {
                Id = 1,
                Name = "João",
                Age = 31,
                InitialMoneyCents = 10_000,
                Needs = new Needs { Hunger = 75, Thirst = 75, Energy = 70, Social = 60 },
                Inventory = new Inventory { Food = 2, Water = 1 }
            },
            new()
            {
                Id = 2,
                Name = "Maria",
                Age = 38,
                InitialMoneyCents = 10_000,
                Needs = new Needs { Hunger = 82, Thirst = 80, Energy = 78, Social = 68 },
                Inventory = new Inventory { Food = 2, Water = 2 }
            }
        }
    };
}

public sealed class AgentState
{
    public int Id { get; }
    public string Name { get; }
    public int Age { get; }
    public string Account => $"agent:{Id}";
    public string EmployerAccount { get; }
    public Needs Needs { get; }
    public Inventory Inventory { get; }
    public AgentAction Action { get; internal set; }
    public double ActionAge { get; internal set; }
    public string DecisionReason { get; internal set; } = "Inicializando";
    public bool Alive { get; internal set; } = true;
    internal double WorkPaymentAccumulator { get; set; }

    internal AgentState(AgentDefinition definition)
    {
        Id = definition.Id;
        Name = definition.Name;
        Age = definition.Age;
        EmployerAccount = definition.EmployerAccount;
        Needs = definition.Needs.Copy();
        Inventory = definition.Inventory.Copy();
    }
}

public sealed class SimulationEvent
{
    public long Sequence { get; set; }
    public long Tick { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int AgentId { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> Data { get; set; } = new();
}
