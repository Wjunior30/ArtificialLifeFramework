namespace ArtificialLife.Core;

public struct DeterministicRandom
{
    private ulong _state;
    internal ulong State => _state;
    internal static DeterministicRandom FromState(ulong state) => new() { _state = state };

    public DeterministicRandom(ulong seed) => _state = StableHash.Mix(seed);

    public ulong NextUInt64()
    {
        ulong z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    public int Next(int maximum) => maximum > 0 ? (int)(NextUInt64() % (uint)maximum) : throw new ArgumentOutOfRangeException(nameof(maximum));
}

public static class StableHash
{
    public static ulong Mix(ulong value)
    {
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    public static ulong Text(string value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        ulong hash = 14695981039346656037UL;
        foreach (char c in value) { hash ^= c; hash *= 1099511628211UL; }
        return Mix(hash);
    }

    public static ulong Combine(ulong seed, params ulong[] values)
    {
        ulong result = Mix(seed);
        foreach (ulong value in values) result = Mix(result ^ Mix(value));
        return result;
    }
}

public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
{
    public EntityId(ulong value) => Value = value;
    public ulong Value { get; }
    public bool Equals(EntityId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(EntityId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString("X16");
}

public sealed class IdSequence
{
    private readonly ulong _seed;
    private ulong _counter;
    public IdSequence(ulong seed, ulong next = 1) { _seed = seed; _counter = next; }
    public ulong NextValue => _counter;
    public EntityId Next(string domain) => new(StableHash.Combine(_seed, StableHash.Text(domain), _counter++));
}

