using ArtificialLife.Core;

internal static class RegressionChecks
{
    public static void Run(Action<bool, string> check)
    {
        void Reject<T>(Action action, string message) where T : Exception
        {
            bool rejected = false;
            try { action(); }
            catch (T) { rejected = true; }
            check(rejected, message);
        }

        var buyer = Agent.Create(new EntityId(1), "Buyer");
        var seller = Agent.Create(new EntityId(2), "Seller");
        var treasury = new EntityId(3);
        var economy = new Economy();
        economy.Open(buyer.Id, new Money(950)); economy.Open(seller.Id, Money.Zero);
        economy.Open(treasury, Money.Zero);
        seller.Inventory.Add("food", 2);
        var policy = new EconomicPolicy { SalesTaxBasisPoints = 1000 };
        var market = new Market(economy, policy, treasury);

        void UnchangedSale()
        {
            check(economy.BalanceOf(buyer.Id).Cents == 950 && economy.BalanceOf(seller.Id).Cents == 0 &&
                  economy.BalanceOf(treasury).Cents == 0 && economy.Ledger.Count == 0,
                  "Rejected sale changed balances or ledger.");
            check(seller.Inventory.Quantity("food") == 2 && buyer.Inventory.Quantity("food") == 0,
                  "Rejected sale changed goods.");
            economy.Audit();
        }

        Reject<InvalidOperationException>(() => market.Trade(1, buyer, seller, "food", 1, new Money(1000)),
            "Sale must reject a balance that only covers the net price.");
        UnchangedSale();
        Reject<KeyNotFoundException>(() => new Market(economy, policy, new EntityId(99)).Trade(1, buyer, seller, "food", 1, new Money(900)),
            "Sale must validate treasury before paying the seller.");
        UnchangedSale();
        foreach (int rate in new[] { -1, 10001 })
        {
            policy.SalesTaxBasisPoints = rate;
            Reject<ArgumentOutOfRangeException>(() => market.Trade(1, buyer, seller, "food", 1, new Money(900)), "Invalid tax accepted.");
            UnchangedSale();
        }
        policy.SalesTaxBasisPoints = 1000;
        buyer.Inventory.Add("food", long.MaxValue);
        Reject<OverflowException>(() => market.Trade(1, buyer, seller, "food", 1, new Money(900)), "Inventory overflow accepted.");
        check(buyer.Inventory.Quantity("food") == long.MaxValue, "Overflow changed buyer inventory.");
        buyer.Inventory.Add("food", -long.MaxValue);
        UnchangedSale();
        market.Trade(1, buyer, seller, "food", 1, Money.Zero);
        check(buyer.Inventory.Quantity("food") == 1 && economy.Ledger.Count == 0, "Free goods must transfer without zero-value payments.");
        policy.SalesTaxBasisPoints = 10000;
        market.Trade(2, buyer, seller, "food", 1, new Money(950));
        check(economy.BalanceOf(treasury).Cents == 950 && economy.BalanceOf(buyer.Id).Cents == 0 &&
              buyer.Inventory.Quantity("food") == 2, "A 100% sales tax must transfer the price and deliver goods.");
        economy.Audit();

        var source = new Inventory(); var destination = new Inventory();
        source.Add("food", 1); destination.Add("food", long.MaxValue);
        Reject<OverflowException>(() => source.TransferTo(destination, "food", 1), "Overflowing transfer must fail.");
        check(source.Quantity("food") == 1 && destination.Quantity("food") == long.MaxValue, "Failed inventory transfer lost goods.");

        var helper = Agent.Create(new EntityId(4), "Helper");
        var receiver = Agent.Create(new EntityId(5), "Receiver");
        helper.Inventory.Add("water", 2);
        var care = new HouseholdCareNetwork(); var request = care.Request(receiver.Id, "water", 1);
        check(care.Assign(request, helper) && care.Deliver(request, helper, receiver, 1), "First aid delivery failed.");
        check(!care.Deliver(request, helper, receiver, 2) && !care.Assign(request, helper), "Completed aid was reused.");
        check(request.Completed && helper.Inventory.Quantity("water") == 1 && receiver.Inventory.Quantity("water") == 1,
            "Duplicate delivery changed goods or completion state.");

        foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d })
        {
            var portable = new PortableSimulation(42, .1);
            portable.Advance(.05);
            Reject<ArgumentOutOfRangeException>(() => portable.Advance(value), "Invalid elapsed time accepted.");
            check(portable.World.Tick == 0 && portable.Advance(.05) == 1, "Rejected time changed the accumulator.");
            Reject<ArgumentOutOfRangeException>(() => new PortableSimulation(42, value), "Invalid fixed step accepted.");
            var world = new LifeWorld(42);
            Reject<ArgumentOutOfRangeException>(() => world.Step(value), "Invalid world time accepted.");
            check(world.Tick == 0, "Rejected world step changed tick.");
            Reject<ArgumentOutOfRangeException>(() => new SettlementSimulation(42, new(value, 1)), "Invalid food multiplier accepted.");
            Reject<ArgumentOutOfRangeException>(() => new SettlementSimulation(42, new(1, value)), "Invalid conflict multiplier accepted.");
        }
        Reject<ArgumentOutOfRangeException>(() => new PortableSimulation(42, 0), "Zero fixed step accepted.");
        Reject<ArgumentOutOfRangeException>(() => new SettlementSimulation(42, new(double.MaxValue, 1)), "Unsafe finite multiplier accepted.");
        Reject<ArgumentOutOfRangeException>(() => new SettlementSimulation(42, new(1, double.MaxValue)), "Unsafe finite conflict accepted.");
        var zeroMultipliers = new SettlementSimulation(42, new(0, 0)); zeroMultipliers.Advance(30);
        check(zeroMultipliers.Audit().Count == 0, "Zero multipliers must remain valid.");
        var bounded = new PortableSimulation(42, .1);
        Reject<ArgumentOutOfRangeException>(() => bounded.Advance((PortableSimulation.MaximumStepsPerAdvance + 1) * .1), "Excessive advance accepted.");
        check(bounded.World.Tick == 0 && bounded.Advance(.1) == 1, "Excessive advance mutated state.");
        check(bounded.Advance(PortableSimulation.MaximumStepsPerAdvance * .1) == PortableSimulation.MaximumStepsPerAdvance,
            "The documented advance limit must be accepted.");
        check(bounded.Advance(.099999999) == 0 && bounded.Advance(.000000001) == 1,
            "A large advance accumulated rounding error into the next step.");
        var tiny = new PortableSimulation(42, 1e-15);
        check(tiny.Advance(0) == 0 && tiny.Advance(1e-15) == 1, "Tiny fixed steps must not fabricate time.");
        var continuous = new PortableSimulation(42, .1); var partitioned = new PortableSimulation(42, .1);
        continuous.Advance(10);
        for (int i = 0; i < 1000; i++) partitioned.Advance(.01);
        check(continuous.World.Tick == 100 && continuous.World.Fingerprint() == partitioned.World.Fingerprint(),
            "Frame partition changed the portable simulation.");

        var settlement = new SettlementSimulation(42);
        check(settlement.Statistics.PeakPopulation == 42 && settlement.Audit().Count == 0, "Initial population audit failed.");
        Reject<NotSupportedException>(() => ((IList<SettlementSite>)settlement.Sites).Clear(), "Sites can be removed externally.");
        Reject<NotSupportedException>(() => ((IList<EmergentSettlement>)settlement.Settlements).Clear(), "Settlements can be removed externally.");
        Reject<NotSupportedException>(() => ((IList<SettlementHistoryEvent>)settlement.History).Clear(), "History can be removed externally.");
        var town = settlement.Settlements[0];
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            town.Food = invalid;
            check(settlement.Audit().Count > 0, "Audit accepted non-finite food.");
        }
        town.Food = 26;
        ulong before = settlement.Fingerprint();
        town.Food += .01;
        check(before != settlement.Fingerprint(), "Fingerprint ignored food.");
        town.Food = 26; before = settlement.Fingerprint(); town.Materials++;
        check(before != settlement.Fingerprint(), "Fingerprint ignored materials.");
        before = settlement.Fingerprint(); town.Prosperity++;
        check(before != settlement.Fingerprint(), "Fingerprint ignored prosperity.");
        before = settlement.Fingerprint(); town.Defense++;
        check(before != settlement.Fingerprint(), "Fingerprint ignored defense.");
        before = settlement.Fingerprint(); settlement.Sites[0].RuinedUntilDay++;
        check(before != settlement.Fingerprint(), "Fingerprint ignored ruins.");
        before = settlement.Fingerprint(); settlement.Statistics.Migrations++;
        check(before != settlement.Fingerprint(), "Fingerprint ignored statistics.");
        town.Population++;
        check(settlement.Audit().Any(x => x.Contains("Population conservation")), "Audit missed unaccounted population.");

        var normal = new SettlementSimulation(42); var scarce = new SettlementSimulation(42, new(.78, 1));
        normal.Advance(1); scarce.Advance(1);
        check(normal.Fingerprint() != scarce.Fingerprint(), "Different food policies produced the same fingerprint.");

        // This seed previously allowed a settlement destroyed on day 609 to fight again.
        var historySimulation = new SettlementSimulation(1); historySimulation.Advance(630);
        var destroyed = new HashSet<int>(); int battles = 0;
        foreach (var entry in historySimulation.History)
        {
            if (entry.Kind == "battle")
            {
                battles++;
                check(!destroyed.Contains(entry.SettlementId) && !destroyed.Contains(entry.OtherId), "Destroyed settlement participated in battle.");
            }
            if (entry.Kind == "destruction") destroyed.Add(entry.SettlementId);
        }
        check(battles > 0 && destroyed.Count > 0 && historySimulation.Audit().Count == 0, "War regression did not exercise destruction.");
    }
}
