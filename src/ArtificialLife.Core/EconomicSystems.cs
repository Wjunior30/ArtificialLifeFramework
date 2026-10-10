namespace ArtificialLife.Core;

public sealed class EconomicPolicy
{
    public int SalesTaxBasisPoints { get; set; } = 500;
    public int IncomeTaxBasisPoints { get; set; } = 800;
    public Money BasicBenefit { get; set; } = Money.Zero;
    public Money ApplyRate(Money value, int basisPoints) => new(checked(value.Cents * Math.Max(0, basisPoints) / 10_000));
}

public sealed class Market
{
    private readonly Economy _economy;
    private readonly EntityId? _treasury;
    private readonly EconomicPolicy _policy;

    public Market(Economy economy, EconomicPolicy? policy = null, EntityId? treasury = null)
    { _economy = economy; _policy = policy ?? new EconomicPolicy(); _treasury = treasury; }

    public void Trade(long tick, Agent buyer, Agent seller, string good, long quantity, Money unitPrice)
    {
        if (quantity <= 0 || unitPrice.Cents < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (seller.Inventory.Quantity(good) < quantity) throw new InvalidOperationException($"Seller lacks {good}.");
        Money gross = new(checked(unitPrice.Cents * quantity));
        Money tax = _treasury.HasValue ? _policy.ApplyRate(gross, _policy.SalesTaxBasisPoints) : Money.Zero;
        Money net = gross - tax;
        _economy.Transfer(tick, buyer.Id, seller.Id, net, $"sale:{good}");
        if (tax.Cents > 0) _economy.Transfer(tick, buyer.Id, _treasury!.Value, tax, $"sales-tax:{good}");
        seller.Inventory.TransferTo(buyer.Inventory, good, quantity);
        _economy.Audit();
    }
}

public sealed class Firm
{
    private readonly SortedSet<EntityId> _workers = new();
    public Firm(EntityId id, EntityId owner, string name, Recipe recipe, Money wagePerCycle)
    { Id = id; Owner = owner; Name = name; Recipe = recipe; WagePerCycle = wagePerCycle; }
    public EntityId Id { get; }
    public EntityId Owner { get; }
    public string Name { get; }
    public Recipe Recipe { get; }
    public Money WagePerCycle { get; set; }
    public Inventory Inventory { get; } = new();
    public IReadOnlyCollection<EntityId> Workers => _workers;
    public void Hire(EntityId worker) => _workers.Add(worker);
    public void Fire(EntityId worker) => _workers.Remove(worker);

    public bool RunProductionCycle(long tick, Economy economy)
    {
        if (!Recipe.CanProduce(Inventory)) return false;
        long payroll = checked(WagePerCycle.Cents * _workers.Count);
        if (economy.BalanceOf(Id).Cents < payroll) return false;
        Recipe.Produce(Inventory);
        foreach (EntityId worker in _workers) economy.Transfer(tick, Id, worker, WagePerCycle, $"wage:{Name}");
        economy.Audit();
        return true;
    }
}

public sealed class DebtContract
{
    public DebtContract(EntityId lender, EntityId borrower, Money principal, int interestBasisPoints)
    { Lender = lender; Borrower = borrower; Outstanding = principal; InterestBasisPoints = Math.Max(0, interestBasisPoints); }
    public EntityId Lender { get; }
    public EntityId Borrower { get; }
    public Money Outstanding { get; private set; }
    public int InterestBasisPoints { get; }
    public void AccrueInterest() => Outstanding += new Money(checked(Outstanding.Cents * InterestBasisPoints / 10_000));
    public void Pay(long tick, Economy economy, Money amount)
    {
        if (amount.Cents <= 0 || amount.Cents > Outstanding.Cents) throw new ArgumentOutOfRangeException(nameof(amount));
        economy.Transfer(tick, Borrower, Lender, amount, "debt-payment"); Outstanding -= amount;
    }
}

public static class EconomicAudit
{
    public static IReadOnlyDictionary<string, long> CountGoods(IEnumerable<Inventory> inventories)
    {
        var totals = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var inventory in inventories)
            foreach (var pair in inventory.All)
                totals[pair.Key] = checked((totals.TryGetValue(pair.Key, out long total) ? total : 0) + pair.Value);
        return totals;
    }
}

