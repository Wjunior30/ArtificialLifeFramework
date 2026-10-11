namespace SimulationLife.Core;

public sealed class LedgerEntry
{
    public long Sequence { get; set; }
    public long Tick { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Payer { get; set; } = string.Empty;
    public string Receiver { get; set; } = string.Empty;
    public long Cents { get; set; }
}

public sealed class Ledger
{
    private readonly Dictionary<string, long> accounts = new(StringComparer.Ordinal);
    private readonly List<LedgerEntry> entries = new();
    private long initialTotal;
    private bool sealedInitialState;

    public IReadOnlyDictionary<string, long> Accounts => accounts;
    public IReadOnlyList<LedgerEntry> Entries => entries;
    public long CurrentTotal => accounts.Values.Sum();
    public long InitialTotal => initialTotal;

    public void CreateAccount(string id, long cents)
    {
        if (sealedInitialState)
            throw new InvalidOperationException("Initial accounts are already sealed.");
        if (string.IsNullOrWhiteSpace(id) || cents < 0 || accounts.ContainsKey(id))
            throw new ArgumentException("Invalid or duplicate account.", nameof(id));
        accounts[id] = cents;
    }

    public void SealInitialState()
    {
        initialTotal = CurrentTotal;
        sealedInitialState = true;
    }

    public bool Transfer(long tick, string payer, string receiver, long cents, string kind)
    {
        if (!sealedInitialState || cents < 0 || string.IsNullOrWhiteSpace(kind) ||
            !accounts.ContainsKey(payer) || !accounts.ContainsKey(receiver))
            return false;
        if (accounts[payer] < cents)
            return false;
        accounts[payer] -= cents;
        accounts[receiver] += cents;
        entries.Add(new LedgerEntry
        {
            Sequence = entries.Count + 1,
            Tick = tick,
            Kind = kind,
            Payer = payer,
            Receiver = receiver,
            Cents = cents
        });
        return CurrentTotal == initialTotal;
    }

    public bool IsBalanced() =>
        sealedInitialState && CurrentTotal == initialTotal && accounts.Values.All(value => value >= 0);

    internal LedgerSnapshot CaptureSnapshot() => new()
    {
        InitialTotal = initialTotal,
        Accounts = new Dictionary<string, long>(accounts, StringComparer.Ordinal),
        Entries = entries.Select(CopyEntry).ToList()
    };

    internal void Restore(LedgerSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.InitialTotal < 0 || snapshot.Accounts is null || snapshot.Entries is null ||
            snapshot.Accounts.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0) ||
            snapshot.Entries.Any(entry => entry is null))
            throw new ArgumentException("Snapshot contains an invalid ledger.", nameof(snapshot));

        for (var index = 0; index < snapshot.Entries.Count; index++)
        {
            var entry = snapshot.Entries[index];
            if (entry.Sequence != index + 1 || entry.Tick < 0 || entry.Cents < 0 ||
                string.IsNullOrWhiteSpace(entry.Kind) || string.IsNullOrWhiteSpace(entry.Payer) ||
                string.IsNullOrWhiteSpace(entry.Receiver) || !snapshot.Accounts.ContainsKey(entry.Payer) ||
                !snapshot.Accounts.ContainsKey(entry.Receiver))
                throw new ArgumentException("Snapshot contains an invalid ledger entry.", nameof(snapshot));
        }

        accounts.Clear();
        foreach (var pair in snapshot.Accounts)
            accounts[pair.Key] = pair.Value;
        entries.Clear();
        entries.AddRange(snapshot.Entries.Select(CopyEntry));
        initialTotal = snapshot.InitialTotal;
        sealedInitialState = true;
        if (!IsBalanced())
            throw new ArgumentException("Snapshot ledger is not balanced.", nameof(snapshot));
    }

    private static LedgerEntry CopyEntry(LedgerEntry source) => new()
    {
        Sequence = source.Sequence,
        Tick = source.Tick,
        Kind = source.Kind,
        Payer = source.Payer,
        Receiver = source.Receiver,
        Cents = source.Cents
    };
}
