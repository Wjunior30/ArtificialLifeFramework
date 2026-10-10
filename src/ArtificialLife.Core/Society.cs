namespace ArtificialLife.Core;

public sealed class Household
{
    private readonly List<EntityId> _members = new();
    public Household(EntityId id) => Id = id;
    public EntityId Id { get; }
    public IReadOnlyList<EntityId> Members => _members;
    public Inventory SharedInventory { get; } = new();
    public void Add(EntityId member) { if (!_members.Contains(member)) _members.Add(member); }
}

public static class HouseholdCare
{
    public static bool TryDeliver(Agent giver, Agent receiver, string good, long quantity, long tick)
    {
        if (quantity <= 0 || giver.Inventory.Quantity(good) < quantity) return false;
        giver.Inventory.TransferTo(receiver.Inventory, good, quantity);
        giver.Relationships.Change(receiver.Id, 0.03);
        receiver.Relationships.Change(giver.Id, 0.08);
        receiver.Memories.Remember(new Memory(tick, "care", $"Received {quantity} {good}", 0.7, giver.Id));
        return true;
    }
}

public sealed class RegionalLink
{
    public RegionalLink(string from, string to, double distance, double risk)
    { From = from; To = to; Distance = Math.Max(0, distance); Risk = Math.Max(0, Math.Min(1, risk)); }
    public string From { get; }
    public string To { get; }
    public double Distance { get; }
    public double Risk { get; }
    public double TravelCost(double cargoWeight) => Distance * (1 + Risk) * Math.Max(0, cargoWeight);
}

