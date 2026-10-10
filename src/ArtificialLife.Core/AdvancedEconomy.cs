namespace ArtificialLife.Core;

public enum OwnershipModel { Private, Workers, Community }
public enum DistributionModel { Market, Need, Ration }
public enum PricingModel { Flexible, CostPlus, Regulated, None }
public enum InheritanceModel { Private, Community }

public sealed class SocialEconomicPolicy
{
    public string Name { get; set; } = "mixed";
    public OwnershipModel Ownership { get; set; } = OwnershipModel.Private;
    public DistributionModel Distribution { get; set; } = DistributionModel.Market;
    public PricingModel Pricing { get; set; } = PricingModel.Flexible;
    public InheritanceModel Inheritance { get; set; } = InheritanceModel.Private;
    public int IncomeTaxBasisPoints { get; set; } = 800;
    public int SalesTaxBasisPoints { get; set; } = 500;
    public int WealthTaxBasisPoints { get; set; }
    public int DailyInterestBasisPoints { get; set; } = 5;
    public int ProfitDistributionBasisPoints { get; set; } = 2500;
    public Money BasicBenefit { get; set; } = Money.Zero;
    public bool PublicHealth { get; set; }
    public void Validate()
    {
        foreach (int rate in new[] { IncomeTaxBasisPoints, SalesTaxBasisPoints, WealthTaxBasisPoints, DailyInterestBasisPoints, ProfitDistributionBasisPoints })
            if (rate < 0 || rate > 10_000) throw new InvalidOperationException("Policy rate must be between 0 and 10000 basis points.");
    }
}

public sealed class CreditAgreement
{
    public CreditAgreement(EntityId lender, EntityId borrower, Money principal, int dailyInterestBasisPoints, int maximumArrears)
    { Lender = lender; Borrower = borrower; Outstanding = principal; DailyInterestBasisPoints = Math.Max(0, dailyInterestBasisPoints); MaximumArrears = Math.Max(1, maximumArrears); }
    public EntityId Lender { get; }
    public EntityId Borrower { get; }
    public Money Outstanding { get; private set; }
    public int DailyInterestBasisPoints { get; }
    public int MaximumArrears { get; }
    public int Arrears { get; private set; }
    public bool Defaulted { get; private set; }
    public void Accrue() { if (!Defaulted) Outstanding += new Money(Outstanding.Cents * DailyInterestBasisPoints / 10_000); }
    public bool TryCollect(long tick, Economy economy, Money requested)
    {
        if (Defaulted || Outstanding.Cents == 0) return false;
        long available = economy.BalanceOf(Borrower).Cents;
        long cents = Math.Min(Math.Min(requested.Cents, Outstanding.Cents), available);
        if (cents <= 0) { if (++Arrears >= MaximumArrears) Defaulted = true; return false; }
        economy.Transfer(tick, Borrower, Lender, new Money(cents), "credit-payment");
        Outstanding -= new Money(cents); Arrears = 0; return true;
    }
}

public sealed class PublicFinance
{
    private readonly Economy _economy;
    public PublicFinance(Economy economy, EntityId treasury, SocialEconomicPolicy policy) { _economy = economy; Treasury = treasury; Policy = policy; }
    public EntityId Treasury { get; }
    public SocialEconomicPolicy Policy { get; }
    public Money TaxIncome(long tick, EntityId payer, Money gross)
    {
        long cents = gross.Cents * Policy.IncomeTaxBasisPoints / 10_000;
        if (cents > 0) _economy.Transfer(tick, payer, Treasury, new Money(cents), "income-tax");
        return new Money(cents);
    }
    public bool PayBenefit(long tick, EntityId recipient)
    {
        Money value = Policy.BasicBenefit;
        if (value.Cents <= 0 || _economy.BalanceOf(Treasury).Cents < value.Cents) return false;
        _economy.Transfer(tick, Treasury, recipient, value, "basic-benefit"); return true;
    }
    public void Inherit(long tick, EntityId estate, IReadOnlyList<EntityId> heirs)
    {
        long balance = _economy.BalanceOf(estate).Cents;
        if (balance == 0) return;
        if (Policy.Inheritance == InheritanceModel.Community || heirs.Count == 0) { _economy.Transfer(tick, estate, Treasury, new Money(balance), "community-inheritance"); return; }
        long each = balance / heirs.Count;
        foreach (var heir in heirs.OrderBy(x => x)) if (each > 0) _economy.Transfer(tick, estate, heir, new Money(each), "inheritance");
        long remainder = _economy.BalanceOf(estate).Cents;
        if (remainder > 0) _economy.Transfer(tick, estate, heirs.OrderBy(x => x).First(), new Money(remainder), "inheritance-remainder");
    }
}

public sealed record EconomicMetrics(double Gini, double Unemployment, double BasicGoodsAccess, long TotalMoney, IReadOnlyDictionary<string, long> Goods);

public static class EconomicMetricsCalculator
{
    public static EconomicMetrics Measure(IEnumerable<Account> accounts, IEnumerable<Inventory> inventories, int employed, int workforce)
    {
        long[] wealth = accounts.Select(x => x.Balance.Cents).OrderBy(x => x).ToArray();
        long total = wealth.Sum(); double weighted = 0;
        for (int i = 0; i < wealth.Length; i++) weighted += (2d * (i + 1) - wealth.Length - 1) * wealth[i];
        double gini = total <= 0 || wealth.Length == 0 ? 0 : weighted / (wealth.Length * total);
        var goods = EconomicAudit.CountGoods(inventories);
        long food = goods.TryGetValue("food", out var f) ? f : 0, water = goods.TryGetValue("water", out var w) ? w : 0;
        double access = inventories.Any() ? Math.Min(1, Math.Min(food, water) / (double)Math.Max(1, inventories.Count())) : 0;
        return new(gini, workforce <= 0 ? 0 : 1 - employed / (double)workforce, access, total, goods);
    }
}
