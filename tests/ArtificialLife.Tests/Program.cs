using ArtificialLife.Core;

using Xunit;

public sealed class ExistingChecks
{
    [Fact]
    public void ExistingDeterminismAndEconomicChecks()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }

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



        var profile = new PersonProfile(giver.Id) { Occupation = Occupation.Farmer, Position = new Vec2(2, 3) };
        profile.SetSkill(Skill.Farming, .7); profile.Body.Injure(BodyPart.LeftLeg, InjurySeverity.Moderate);
        Check(profile.Body.MovementMultiplier < 1 && profile.SkillLevel(Skill.Farming) == .7, "Body and skill model must preserve person state.");

        var commitment = new CommitmentState(); commitment.Start("work", 0, 20);
        var autonomous = new AutonomousDecisionSystem();
        var kept = autonomous.Choose(new[] { new ActionEvaluation("work", .5, new Dictionary<string,double>()), new ActionEvaluation("rest", .58, new Dictionary<string,double>()) }, commitment, 5);
        Check(kept.ActionId == "work", "Small utility changes must not cause action thrashing.");

        var publicEconomy = new Economy(); var estate = ids.Next("estate"); var heir = ids.Next("heir"); var publicTreasury = ids.Next("treasury");
        publicEconomy.Open(estate, new Money(901)); publicEconomy.Open(heir, Money.Zero); publicEconomy.Open(publicTreasury, Money.Zero);
        new PublicFinance(publicEconomy, publicTreasury, new SocialEconomicPolicy()).Inherit(4, estate, new[] { heir });
        Check(publicEconomy.BalanceOf(heir).Cents == 901, "Private inheritance must transfer the complete estate.");

        var creditEconomy = new Economy(); var lender = ids.Next("lender"); var borrower = ids.Next("borrower"); creditEconomy.Open(lender, Money.Zero); creditEconomy.Open(borrower, Money.Zero);
        var credit = new CreditAgreement(lender, borrower, new Money(500), 10, 2); credit.TryCollect(1, creditEconomy, new Money(100)); credit.TryCollect(2, creditEconomy, new Money(100));
        Check(credit.Defaulted, "Repeated arrears must lead to default.");

        var care = new HouseholdCareNetwork(); giver.Inventory.Add("water", 2); var request = care.Request(receiver.Id, "water", 2, 10);
        Check(care.Assign(request, giver) && care.Deliver(request, giver, receiver, 4), "Household aid must be assigned and physically delivered.");

        var regions = new RegionalNetworkSystem(); var north = new Settlement("north", new Vec2(0,0)); var south = new Settlement("south", new Vec2(3,4)); north.Stock.Add("food", 4); regions.Add(north); regions.Add(south);
        regions.Dispatch("north", "south", "food", 3); regions.Tick(5);
        Check(south.Stock.Quantity("food") == 3, "Regional trade must move physical cargo.");



        var portable = new PortableSimulation(99, .1); var portableAgent = Agent.Create(portable.World.Ids.Next("agent"), "Portable"); portableAgent.Needs.Add("energy", .5, .01); portable.Add(portableAgent);
        Check(portable.Advance(.35) == 3 && portable.Snapshot().Tick == 3, "Portable fixed-step host must be independent from frame rate.");

        RegressionChecks.Run(Check);
        Console.WriteLine($"ArtificialLife.Tests: {checks} checks, 0 failures.");

    }
}
