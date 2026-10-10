namespace ArtificialLife.Core;

public readonly struct Money : IEquatable<Money>, IComparable<Money>
{
    public Money(long cents) => Cents = cents;
    public long Cents { get; }
    public static Money Zero => new(0);
    public static Money operator +(Money a, Money b) => new(checked(a.Cents + b.Cents));
    public static Money operator -(Money a, Money b) => new(checked(a.Cents - b.Cents));
    public bool Equals(Money other) => Cents == other.Cents;
    public override bool Equals(object? obj) => obj is Money other && Equals(other);
    public override int GetHashCode() => Cents.GetHashCode();
    public int CompareTo(Money other) => Cents.CompareTo(other.Cents);
    public override string ToString() => (Cents / 100m).ToString("0.00");
}

public sealed class Inventory
{
    private readonly Dictionary<string, long> _goods = new(StringComparer.Ordinal);
    public IEnumerable<KeyValuePair<string, long>> All => _goods.OrderBy(x => x.Key, StringComparer.Ordinal);
    public long Quantity(string good) => _goods.TryGetValue(good, out long quantity) ? quantity : 0;
    public void Add(string good, long quantity)
    {
        if (string.IsNullOrWhiteSpace(good)) throw new ArgumentException("Good id is required.", nameof(good));
        long next = checked(Quantity(good) + quantity);
        if (next < 0) throw new InvalidOperationException($"Insufficient {good}.");
        if (next == 0) _goods.Remove(good); else _goods[good] = next;
    }
    public void TransferTo(Inventory destination, string good, long quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Add(good, -quantity);
        destination.Add(good, quantity);
    }
}

public sealed class Account
{
    internal Account(EntityId owner, Money openingBalance) { Owner = owner; Balance = openingBalance; }
    public EntityId Owner { get; }
    public Money Balance { get; internal set; }
}

public sealed class LedgerEntry
{
    public LedgerEntry(long tick, EntityId from, EntityId to, Money amount, string reason)
    { Tick = tick; From = from; To = to; Amount = amount; Reason = reason; }
    public long Tick { get; }
    public EntityId From { get; }
    public EntityId To { get; }
    public Money Amount { get; }
    public string Reason { get; }
}

public sealed class Economy
{
    private readonly Dictionary<EntityId, Account> _accounts = new();
    private readonly List<LedgerEntry> _ledger = new();
    private long _issuedCents;
    public IReadOnlyList<LedgerEntry> Ledger => _ledger;
    public IEnumerable<Account> Accounts => _accounts.Values.OrderBy(x => x.Owner);

    public Account Open(EntityId owner, Money openingBalance)
    {
        if (openingBalance.Cents < 0) throw new ArgumentOutOfRangeException(nameof(openingBalance));
        var account = new Account(owner, openingBalance);
        _accounts.Add(owner, account);
        _issuedCents = checked(_issuedCents + openingBalance.Cents);
        return account;
    }

    public void Transfer(long tick, EntityId from, EntityId to, Money amount, string reason)
    {
        if (amount.Cents <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Account source = _accounts[from], destination = _accounts[to];
        if (source.Balance.Cents < amount.Cents) throw new InvalidOperationException("Insufficient funds.");
        source.Balance -= amount;
        destination.Balance += amount;
        _ledger.Add(new LedgerEntry(tick, from, to, amount, reason));
    }

    public void Audit()
    {
        long sum = 0;
        foreach (var account in _accounts.Values)
        {
            if (account.Balance.Cents < 0) throw new InvalidOperationException("Negative account balance.");
            sum = checked(sum + account.Balance.Cents);
        }
        if (sum != _issuedCents) throw new InvalidOperationException($"Money conservation failed: expected {_issuedCents}, found {sum}.");
    }
}

public sealed class Recipe
{
    public Recipe(string id, IReadOnlyDictionary<string, long> inputs, IReadOnlyDictionary<string, long> outputs)
    { Id = id; Inputs = inputs; Outputs = outputs; }
    public string Id { get; }
    public IReadOnlyDictionary<string, long> Inputs { get; }
    public IReadOnlyDictionary<string, long> Outputs { get; }
    public bool CanProduce(Inventory inventory) => Inputs.All(x => inventory.Quantity(x.Key) >= x.Value);
    public void Produce(Inventory inventory)
    {
        if (!CanProduce(inventory)) throw new InvalidOperationException($"Missing inputs for {Id}.");
        foreach (var input in Inputs.OrderBy(x => x.Key, StringComparer.Ordinal)) inventory.Add(input.Key, -input.Value);
        foreach (var output in Outputs.OrderBy(x => x.Key, StringComparer.Ordinal)) inventory.Add(output.Key, output.Value);
    }
}

