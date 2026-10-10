namespace ArtificialLife.Core;

public enum SocialTaskKind { Deliver, Repair, Conversation, Visit, Leisure, Train, Patrol, Trade }
public enum SocialTaskStatus { Planned, InProgress, Completed, Cancelled }

public sealed class SocialTask
{
    public SocialTask(long id, SocialTaskKind kind, EntityId actor, EntityId? target = null) { Id = id; Kind = kind; Actor = actor; Target = target; }
    public long Id { get; }
    public SocialTaskKind Kind { get; }
    public EntityId Actor { get; }
    public EntityId? Target { get; }
    public string? Good { get; set; }
    public long Quantity { get; set; }
    public SocialTaskStatus Status { get; private set; }
    public void Start() { if (Status != SocialTaskStatus.Planned) throw new InvalidOperationException(); Status = SocialTaskStatus.InProgress; }
    public void Complete() { if (Status != SocialTaskStatus.InProgress) throw new InvalidOperationException(); Status = SocialTaskStatus.Completed; }
    public void Cancel(Agent? carrier = null, Inventory? origin = null)
    {
        if (carrier is not null && origin is not null && Good is not null && Quantity > 0 && carrier.Inventory.Quantity(Good) >= Quantity)
            carrier.Inventory.TransferTo(origin, Good, Quantity);
        Status = SocialTaskStatus.Cancelled;
    }
}

public sealed class CareRequest
{
    public CareRequest(long id, EntityId requester, string good, long quantity, int priority) { Id = id; Requester = requester; Good = good; Quantity = quantity; Priority = priority; }
    public long Id { get; }
    public EntityId Requester { get; }
    public string Good { get; }
    public long Quantity { get; }
    public int Priority { get; }
    public EntityId? AssignedHelper { get; internal set; }
    public bool Completed { get; internal set; }
}

public sealed class HouseholdCareNetwork
{
    private long _nextId;
    private readonly List<CareRequest> _requests = new();
    public IReadOnlyList<CareRequest> Requests => _requests;
    public CareRequest Request(EntityId requester, string good, long quantity, int priority = 0)
    { var request = new CareRequest(++_nextId, requester, good, quantity, priority); _requests.Add(request); return request; }
    public bool Assign(CareRequest request, Agent helper) { if (request.Completed || helper.Inventory.Quantity(request.Good) < request.Quantity) return false; request.AssignedHelper = helper.Id; return true; }
    public bool Deliver(CareRequest request, Agent helper, Agent receiver, long tick)
    { if (!request.AssignedHelper.HasValue || !request.AssignedHelper.Value.Equals(helper.Id) || !request.Requester.Equals(receiver.Id)) return false; request.Completed = HouseholdCare.TryDeliver(helper, receiver, request.Good, request.Quantity, tick); return request.Completed; }
}

public sealed record JobOpening(EntityId Employer, Occupation Occupation, Skill Skill, double MinimumSkill, Money Wage);

public static class LaborMarket
{
    public static JobOpening? BestFor(PersonProfile person, IEnumerable<JobOpening> jobs)
        => jobs.Where(x => person.SkillLevel(x.Skill) >= x.MinimumSkill)
            .OrderByDescending(x => x.Wage.Cents * (.5 + person.SkillLevel(x.Skill)))
            .ThenBy(x => x.Employer).FirstOrDefault();
    public static void Train(PersonProfile person, Skill skill, double hours) => person.SetSkill(skill, person.SkillLevel(skill) + Math.Max(0, hours) * .004);
}

public sealed class Settlement
{
    public Settlement(string id, Vec2 position) { Id = id; Position = position; }
    public string Id { get; }
    public Vec2 Position { get; }
    public Inventory Stock { get; } = new();
    public int Guards { get; set; }
    public int Bandits { get; set; }
}

public sealed class Caravan
{
    public Caravan(long id, string from, string to, double distance) { Id = id; From = from; To = to; Distance = distance; }
    public long Id { get; }
    public string From { get; }
    public string To { get; }
    public double Distance { get; }
    public double Progress { get; private set; }
    public Inventory Cargo { get; } = new();
    public bool Arrived => Progress >= 1;
    public void Travel(double distance) => Progress = Math.Min(1, Progress + Math.Max(0, distance) / Math.Max(.001, Distance));
}

public sealed class RegionalNetworkSystem
{
    private long _nextCaravan;
    private readonly SortedDictionary<string, Settlement> _settlements = new(StringComparer.Ordinal);
    private readonly List<Caravan> _caravans = new();
    public IReadOnlyDictionary<string, Settlement> Settlements => _settlements;
    public IReadOnlyList<Caravan> Caravans => _caravans;
    public void Add(Settlement settlement) => _settlements.Add(settlement.Id, settlement);
    public Caravan Dispatch(string from, string to, string good, long quantity)
    {
        var a = _settlements[from]; var b = _settlements[to];
        var caravan = new Caravan(++_nextCaravan, from, to, a.Position.DistanceTo(b.Position));
        a.Stock.TransferTo(caravan.Cargo, good, quantity); _caravans.Add(caravan); return caravan;
    }
    public void Tick(double travelDistance)
    {
        foreach (var caravan in _caravans.Where(x => !x.Arrived))
        {
            caravan.Travel(travelDistance);
            if (caravan.Arrived) foreach (var good in caravan.Cargo.All.ToArray()) caravan.Cargo.TransferTo(_settlements[caravan.To].Stock, good.Key, good.Value);
        }
    }
}

public sealed record ExperimentResult(ulong Seed, string Policy, EconomicMetrics Metrics, ulong Fingerprint);

public sealed class EconomicExperimentRunner
{
    public IReadOnlyList<ExperimentResult> Run(IEnumerable<ulong> seeds, IEnumerable<SocialEconomicPolicy> policies, Func<ulong, SocialEconomicPolicy, (EconomicMetrics Metrics, ulong Fingerprint)> simulation)
    {
        var results = new List<ExperimentResult>();
        foreach (ulong seed in seeds.OrderBy(x => x))
            foreach (var policy in policies.OrderBy(x => x.Name, StringComparer.Ordinal))
            { policy.Validate(); var result = simulation(seed, policy); results.Add(new(seed, policy.Name, result.Metrics, result.Fingerprint)); }
        return results;
    }
}

