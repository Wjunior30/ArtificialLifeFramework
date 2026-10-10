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

var marketEconomy = new Economy();
var buyer = Agent.Create(ids.Next("agent"), "Buyer"); var seller = Agent.Create(ids.Next("agent"), "Seller"); var treasury = ids.Next("treasury");
marketEconomy.Open(buyer.Id, new Money(10_000)); marketEconomy.Open(seller.Id, Money.Zero); marketEconomy.Open(treasury, Money.Zero);
seller.Inventory.Add("meal", 4);
new Market(marketEconomy, new EconomicPolicy { SalesTaxBasisPoints = 1_000 }, treasury).Trade(2, buyer, seller, "meal", 2, new Money(1_000));
Check(seller.Inventory.Quantity("meal") == 2 && buyer.Inventory.Quantity("meal") == 2, "Market must transfer physical goods.");
Check(marketEconomy.BalanceOf(seller.Id).Cents == 1_800 && marketEconomy.BalanceOf(treasury).Cents == 200, "Market must split price and tax.");

var firmId = ids.Next("firm"); var workerId = ids.Next("agent"); var firmEconomy = new Economy();
firmEconomy.Open(firmId, new Money(5_000)); firmEconomy.Open(workerId, Money.Zero);
var recipe = new Recipe("bakery", new Dictionary<string, long> { ["grain"] = 2 }, new Dictionary<string, long> { ["bread"] = 1 });
var firm = new Firm(firmId, seller.Id, "Bakery", recipe, new Money(500)); firm.Hire(workerId); firm.Inventory.Add("grain", 2);
Check(firm.RunProductionCycle(3, firmEconomy), "Firm production should run when inputs and payroll exist.");
Check(firm.Inventory.Quantity("bread") == 1 && firmEconomy.BalanceOf(workerId).Cents == 500, "Production and wages must be materialized.");

Console.WriteLine("ArtificialLife.Tests: 8 checks passed.");

