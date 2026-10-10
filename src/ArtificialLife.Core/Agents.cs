namespace ArtificialLife.Core;

public sealed class Need
{
    public Need(string id, double value = 1, double decayPerHour = 0.02)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Need id is required.", nameof(id)) : id;
        Value = Clamp01(value);
        DecayPerHour = Math.Max(0, decayPerHour);
    }

    public string Id { get; }
    public double Value { get; private set; }
    public double DecayPerHour { get; }
    public double Urgency => 1 - Value;
    public void Tick(double hours) => Value = Clamp01(Value - DecayPerHour * Math.Max(0, hours));
    public void Change(double delta) => Value = Clamp01(Value + delta);
    private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));
}

public sealed class NeedSet
{
    private readonly Dictionary<string, Need> _items = new(StringComparer.Ordinal);
    public IEnumerable<Need> All => _items.Values.OrderBy(x => x.Id, StringComparer.Ordinal);
    public Need this[string id] => _items.TryGetValue(id, out var need) ? need : throw new KeyNotFoundException(id);
    public Need Add(string id, double value = 1, double decayPerHour = 0.02)
    {
        var need = new Need(id, value, decayPerHour);
        _items.Add(id, need);
        return need;
    }
    public bool TryGet(string id, out Need? need) => _items.TryGetValue(id, out need);
    public void Tick(double hours) { foreach (var need in _items.Values) need.Tick(hours); }
}

public sealed class Memory
{
    public Memory(long tick, string kind, string description, double importance, EntityId? subject = null)
    { Tick = tick; Kind = kind; Description = description; Importance = Math.Max(0, Math.Min(1, importance)); Subject = subject; }
    public long Tick { get; }
    public string Kind { get; }
    public string Description { get; }
    public double Importance { get; private set; }
    public EntityId? Subject { get; }
    internal void Fade(double amount) => Importance = Math.Max(0, Importance - amount);
}

public sealed class MemoryStore
{
    private readonly List<Memory> _items = new();
    public IReadOnlyList<Memory> All => _items;
    public void Remember(Memory memory)
    {
        _items.Add(memory ?? throw new ArgumentNullException(nameof(memory)));
        if (_items.Count > 256) _items.RemoveAt(0);
    }
    public void Decay(double amount)
    {
        foreach (var memory in _items) memory.Fade(amount);
        _items.RemoveAll(x => x.Importance <= 0.001);
    }
}

public sealed class EmotionalState
{
    private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);
    public double this[string emotion] => _values.TryGetValue(emotion, out double value) ? value : 0;
    public void Affect(string emotion, double amount) => _values[emotion] = Math.Max(0, Math.Min(1, this[emotion] + amount));
    public void Decay(double hours)
    {
        foreach (string key in _values.Keys.ToArray()) _values[key] = Math.Max(0, _values[key] - 0.025 * hours);
    }
}

public sealed class RelationshipSet
{
    private readonly Dictionary<EntityId, double> _opinions = new();
    public double OpinionOf(EntityId other) => _opinions.TryGetValue(other, out double value) ? value : 0;
    public void Change(EntityId other, double delta) => _opinions[other] = Math.Max(-1, Math.Min(1, OpinionOf(other) + delta));
}

public sealed class Stimulus
{
    public Stimulus(string kind, string description, double strength, EntityId? source = null)
    { Kind = kind; Description = description; Strength = Math.Max(0, Math.Min(1, strength)); Source = source; }
    public string Kind { get; }
    public string Description { get; }
    public double Strength { get; }
    public EntityId? Source { get; }
}

public sealed class Agent
{
    private Agent(EntityId id, string name) { Id = id; Name = name; }
    public EntityId Id { get; }
    public string Name { get; }
    public bool IsAlive { get; set; } = true;
    public NeedSet Needs { get; } = new();
    public MemoryStore Memories { get; } = new();
    public EmotionalState Emotions { get; } = new();
    public RelationshipSet Relationships { get; } = new();
    public Inventory Inventory { get; } = new();
    public string CurrentAction { get; internal set; } = "idle";
    public static Agent Create(EntityId id, string name) => new(id, string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.") : name);

    public void Perceive(Stimulus stimulus, long tick)
    {
        Memories.Remember(new Memory(tick, stimulus.Kind, stimulus.Description, stimulus.Strength, stimulus.Source));
        if (stimulus.Kind == "danger")
        {
            Emotions.Affect("fear", stimulus.Strength);
            if (Needs.TryGet("safety", out var safety)) safety!.Change(-stimulus.Strength);
        }
    }
}

