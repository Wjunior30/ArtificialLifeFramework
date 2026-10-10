using ArtificialLife.Core;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

ulong Run(ulong seed)
{
    var world = new LifeWorld(seed);
    world.SetBrain(new UtilityBrain(new IUtilityAction[] { new SatisfyNeedAction("eat", "hunger", 0.3), new IdleAction() }));
    var a = Agent.Create(world.Ids.Next("agent"), "A");
    a.Needs.Add("hunger", 0.1, 0.04);
    a.Needs.Add("safety", 1, 0.01);
    world.AddAgent(a);
    world.Economy.Open(a.Id, new Money(5_000));
    for (int i = 0; i < 20; i++) world.Step(0.5);
    return world.Fingerprint();
}

Check(Run(42) == Run(42), "Same seed must reproduce the same state.");
Check(Run(42) != Run(43), "Different seeds must produce different identities/state.");

var ids = new IdSequence(1);
var economy = new Economy();
var aId = ids.Next("agent"); var bId = ids.Next("agent");
economy.Open(aId, new Money(1_000)); economy.Open(bId, Money.Zero);
economy.Transfer(1, aId, bId, new Money(275), "trade"); economy.Audit();
Check(economy.Accounts.Sum(x => x.Balance.Cents) == 1_000, "Money must be conserved.");

var giver = Agent.Create(ids.Next("agent"), "Giver");
var receiver = Agent.Create(ids.Next("agent"), "Receiver");
giver.Inventory.Add("food", 3);
Check(HouseholdCare.TryDeliver(giver, receiver, "food", 2, 1), "Care delivery should succeed.");
Check(giver.Inventory.Quantity("food") == 1 && receiver.Inventory.Quantity("food") == 2, "Goods must move physically.");

Console.WriteLine("ArtificialLife.Tests: 4 checks passed.");

