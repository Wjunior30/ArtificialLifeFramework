namespace SimulationLife.Core;

public sealed partial class Simulation
{
    private const double Critical = 20;
    private const double SwitchMargin = 12;
    private const double MinimumCommitmentSeconds = 2;
    private readonly WorldDefinition definition;
    private readonly List<AgentState> agents;
    private readonly List<SimulationEvent> pendingEvents = new();
    private double accumulator;
    private long eventSequence;

    public long Tick { get; private set; }
    public double TotalMinutes { get; private set; }
    public DeterministicRandom Random { get; }
    public Ledger Ledger { get; } = new();
    public IReadOnlyList<AgentState> Agents => agents;
    public int FoodStock { get; private set; }
    public int WaterStock { get; private set; }
    public double FixedStepSeconds => definition.FixedStepSeconds;

    public Simulation(WorldDefinition definition)
    {
        this.definition = Validate(WorldValidator.Clone(definition));
        Random = new DeterministicRandom(this.definition.Seed);
        TotalMinutes = this.definition.StartMinute;
        FoodStock = this.definition.Market.FoodStock;
        WaterStock = this.definition.Market.WaterStock;
        agents = this.definition.Agents.OrderBy(agent => agent.Id).Select(agent => new AgentState(agent)).ToList();
        Ledger.CreateAccount(this.definition.Market.Account, this.definition.Market.InitialMoneyCents);
        foreach (var source in this.definition.Agents.OrderBy(agent => agent.Id))
            Ledger.CreateAccount($"agent:{source.Id}", source.InitialMoneyCents);
        Ledger.SealInitialState();
    }

    private static WorldDefinition Validate(WorldDefinition value)
    {
        return WorldValidator.Validate(value);
    }

    public void Advance(double realSeconds)
    {
        if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds <= 0)
            return;
        if (realSeconds / definition.FixedStepSeconds > 100_000)
            throw new ArgumentOutOfRangeException(nameof(realSeconds), "Advance request exceeds 100,000 fixed steps.");
        accumulator += realSeconds;
        while (accumulator + 1e-10 >= definition.FixedStepSeconds)
        {
            Step();
            accumulator = Math.Max(0, accumulator - definition.FixedStepSeconds);
        }
        if (accumulator < definition.FixedStepSeconds * 1e-9)
            accumulator = 0;
    }

    public bool SetNeed(int agentId, string need, double value)
    {
        var agent = agents.FirstOrDefault(candidate => candidate.Id == agentId);
        if (agent is null)
            return false;
        var clamped = Math.Clamp(value, 0, 100);
        switch (need.ToLowerInvariant())
        {
            case "hunger": agent.Needs.Hunger = clamped; break;
            case "thirst": agent.Needs.Thirst = clamped; break;
            case "energy": agent.Needs.Energy = clamped; break;
            case "safety": agent.Needs.Safety = clamped; break;
            case "social": agent.Needs.Social = clamped; break;
            case "comfort": agent.Needs.Comfort = clamped; break;
            default: return false;
        }
        return true;
    }

    public IReadOnlyList<SimulationEvent> DrainEvents()
    {
        var copy = pendingEvents.ToArray();
        pendingEvents.Clear();
        return copy;
    }

    private void Step()
    {
        Tick++;
        TotalMinutes += definition.FixedStepSeconds * definition.WorldMinutesPerSecond;
        foreach (var agent in agents)
        {
            if (!agent.Alive)
                continue;
            DecayNeeds(agent);
            ChooseAction(agent);
            ExecuteAction(agent);
            agent.ActionAge += definition.FixedStepSeconds;
        }
    }

    private void DecayNeeds(AgentState agent)
    {
        var step = definition.FixedStepSeconds;
        agent.Needs.Hunger = Math.Max(0, agent.Needs.Hunger - step * 0.38);
        agent.Needs.Thirst = Math.Max(0, agent.Needs.Thirst - step * 0.54);
        agent.Needs.Energy = Math.Max(0, agent.Needs.Energy - step * 0.23);
        agent.Needs.Social = Math.Max(0, agent.Needs.Social - step * 0.045);
        agent.Needs.Comfort = Math.Max(0, agent.Needs.Comfort - step * 0.03);
    }

    private void ChooseAction(AgentState agent)
    {
        var candidates = new Dictionary<AgentAction, double>
        {
            [AgentAction.Idle] = 5,
            [AgentAction.Drink] = agent.Inventory.Water > 0 ? Urgency(agent.Needs.Thirst) * 1.4 + (agent.Needs.Thirst < Critical ? 35 : 0) : 0,
            [AgentAction.BuyWater] = agent.Inventory.Water == 0 && WaterStock > 0 ? Urgency(agent.Needs.Thirst) * 1.35 : 0,
            [AgentAction.Eat] = agent.Inventory.Food > 0 ? Urgency(agent.Needs.Hunger) * 1.35 + (agent.Needs.Hunger < Critical ? 35 : 0) : 0,
            [AgentAction.BuyFood] = agent.Inventory.Food == 0 && FoodStock > 0 ? Urgency(agent.Needs.Hunger) * 1.3 : 0,
            [AgentAction.Sleep] = Urgency(agent.Needs.Energy) * 1.16,
            [AgentAction.Socialize] = Urgency(agent.Needs.Social) * 0.75,
            [AgentAction.Work] = IsWorkHour() && agent.Needs.Energy > 25 && agent.Needs.Hunger > 20 && agent.Needs.Thirst > 20 ? 36 : 0
        };
        var choice = candidates.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).First();
        var currentScore = candidates.TryGetValue(agent.Action, out var score) ? score : 0;
        var urgent = Math.Min(agent.Needs.Hunger, Math.Min(agent.Needs.Thirst, agent.Needs.Energy)) < Critical;
        if (!urgent && agent.ActionAge < MinimumCommitmentSeconds && choice.Value < currentScore + SwitchMargin)
            return;
        if (choice.Key == agent.Action)
            return;
        agent.Action = choice.Key;
        agent.ActionAge = 0;
        agent.DecisionReason = Reason(choice.Key, agent);
        Emit("intent_changed", agent, agent.DecisionReason, new Dictionary<string, string> { ["action"] = choice.Key.ToString() });
    }

    private void ExecuteAction(AgentState agent)
    {
        var step = definition.FixedStepSeconds;
        switch (agent.Action)
        {
            case AgentAction.Drink when agent.ActionAge + step >= 1:
                if (agent.Inventory.Water > 0)
                {
                    agent.Inventory.Water--;
                    agent.Needs.Thirst = Math.Min(100, agent.Needs.Thirst + 55);
                    Complete(agent, "Bebeu água");
                }
                break;
            case AgentAction.Eat when agent.ActionAge + step >= 1:
                if (agent.Inventory.Food > 0)
                {
                    agent.Inventory.Food--;
                    agent.Needs.Hunger = Math.Min(100, agent.Needs.Hunger + 48);
                    Complete(agent, "Comeu alimento");
                }
                break;
            case AgentAction.BuyWater when agent.ActionAge + step >= 0.5:
                if (Acquire(agent, false)) Complete(agent, "Comprou água");
                break;
            case AgentAction.BuyFood when agent.ActionAge + step >= 0.5:
                if (Acquire(agent, true)) Complete(agent, "Comprou alimento");
                break;
            case AgentAction.Sleep:
                agent.Needs.Energy = Math.Min(100, agent.Needs.Energy + step * 14);
                agent.Needs.Comfort = Math.Min(100, agent.Needs.Comfort + step * 5);
                break;
            case AgentAction.Socialize:
                agent.Needs.Social = Math.Min(100, agent.Needs.Social + step * 4);
                break;
            case AgentAction.Work:
                agent.WorkPaymentAccumulator += step;
                if (agent.WorkPaymentAccumulator + 1e-10 >= 10)
                {
                    agent.WorkPaymentAccumulator -= 10;
                    if (Ledger.Transfer(Tick, agent.EmployerAccount, agent.Account, 20, "wage"))
                        Emit("transaction_posted", agent, "Salário pago", new Dictionary<string, string> { ["cents"] = "20" });
                }
                break;
        }
    }

    private bool Acquire(AgentState agent, bool food)
    {
        var hasStock = food ? FoodStock > 0 : WaterStock > 0;
        if (!hasStock)
            return false;
        var price = food ? definition.Market.FoodPriceCents : definition.Market.WaterPriceCents;
        var monetary = string.Equals(definition.Distribution, "market", StringComparison.OrdinalIgnoreCase);
        if (monetary && !Ledger.Transfer(Tick, agent.Account, definition.Market.Account, price, food ? "food_purchase" : "water_purchase"))
            return false;
        if (food)
        {
            FoodStock--;
            agent.Inventory.Food++;
        }
        else
        {
            WaterStock--;
            agent.Inventory.Water++;
        }
        Emit("inventory_changed", agent, food ? "Alimento adquirido" : "Água adquirida", new Dictionary<string, string> { ["good"] = food ? "food" : "water", ["quantity"] = "1" });
        return true;
    }

    private void Complete(AgentState agent, string message)
    {
        Emit("activity_completed", agent, message);
        agent.Action = AgentAction.Idle;
        agent.ActionAge = 0;
        agent.DecisionReason = message;
    }

    private void Emit(string kind, AgentState agent, string message, Dictionary<string, string>? data = null)
    {
        pendingEvents.Add(new SimulationEvent
        {
            Sequence = ++eventSequence,
            Tick = Tick,
            Kind = kind,
            AgentId = agent.Id,
            Message = message,
            Data = data ?? new Dictionary<string, string>()
        });
    }

    private bool IsWorkHour()
    {
        var hour = (TotalMinutes / 60) % 24;
        return hour >= 6 && hour < 17;
    }

    private static double Urgency(double value) => 100 - Math.Clamp(value, 0, 100);

    private static string Reason(AgentAction action, AgentState agent) => action switch
    {
        AgentAction.Drink => "Hidratação em " + agent.Needs.Thirst.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
        AgentAction.BuyWater => "Sem água pessoal; procurando oferta",
        AgentAction.Eat => "Saciedade em " + agent.Needs.Hunger.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
        AgentAction.BuyFood => "Sem alimento pessoal; procurando oferta",
        AgentAction.Sleep => "Energia em " + agent.Needs.Energy.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
        AgentAction.Work => "Expediente e necessidades estáveis",
        AgentAction.Socialize => "Necessidade social em " + agent.Needs.Social.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
        _ => "Nenhuma ação mais urgente"
    };
}
