namespace ArtificialLife.Core;

/// <summary>Response curves for utility scoring; input and output clamped to [0,1].</summary>
public enum CurveKind { Linear, Quadratic, Exponential, Logistic, Inverse }

public readonly struct ResponseCurve
{
    public ResponseCurve(CurveKind kind, double steepness = 1, double midpoint = .5) { Kind = kind; Steepness = steepness; Midpoint = midpoint; }
    public CurveKind Kind { get; }
    public double Steepness { get; }
    public double Midpoint { get; }
    public double Evaluate(double x)
    {
        x = Math.Max(0, Math.Min(1, x));
        double y = Kind switch
        {
            CurveKind.Quadratic => Math.Pow(x, Math.Max(.01, Steepness) * 2),
            CurveKind.Exponential => Math.Abs(Steepness) < 1e-9 ? x : (Math.Exp(Steepness * x) - 1) / (Math.Exp(Steepness) - 1),
            CurveKind.Logistic => 1 / (1 + Math.Exp(-Steepness * 10 * (x - Midpoint))),
            CurveKind.Inverse => 1 - x,
            _ => x,
        };
        return Math.Max(0, Math.Min(1, y));
    }

    /// <summary>Multiplies considerations with compensation so many factors do not collapse the score.</summary>
    public static double Combine(IEnumerable<double> considerations)
    {
        double product = 1; int n = 0;
        foreach (double c in considerations) { product *= Math.Max(0, Math.Min(1, c)); n++; }
        if (n == 0) return 0;
        double modification = 1 - 1.0 / n;
        return product + (1 - product) * modification * product;
    }
}

/// <summary>Deterministic uniform-grid spatial hash for perception queries.</summary>
public sealed class SpatialHash
{
    private readonly double _cell;
    private readonly Dictionary<(int, int), List<EntityId>> _cells = new();
    private readonly Dictionary<EntityId, (double X, double Z)> _positions = new();
    public SpatialHash(double cellSize) => _cell = cellSize > 0 ? cellSize : throw new ArgumentOutOfRangeException(nameof(cellSize));
    public int Count => _positions.Count;
    public bool TryGetPosition(EntityId id, out double x, out double z) { bool ok = _positions.TryGetValue(id, out var p); x = p.X; z = p.Z; return ok; }

    public void Set(EntityId id, double x, double z)
    {
        Remove(id);
        _positions[id] = (x, z);
        var key = Key(x, z);
        if (!_cells.TryGetValue(key, out var list)) _cells[key] = list = new List<EntityId>();
        list.Add(id);
    }

    public bool Remove(EntityId id)
    {
        if (!_positions.TryGetValue(id, out var p)) return false;
        var key = Key(p.X, p.Z); var list = _cells[key]; list.Remove(id);
        if (list.Count == 0) _cells.Remove(key);
        return _positions.Remove(id);
    }

    /// <summary>Entities within radius, nearest first, ties by id.</summary>
    public IReadOnlyList<EntityId> Query(double x, double z, double radius)
    {
        var found = new List<(EntityId Id, double D)>();
        double r2 = radius * radius; var (minX, minZ) = Key(x - radius, z - radius); var (maxX, maxZ) = Key(x + radius, z + radius);
        for (int cz = minZ; cz <= maxZ; cz++) for (int cx = minX; cx <= maxX; cx++)
        {
            if (!_cells.TryGetValue((cx, cz), out var list)) continue;
            foreach (var id in list) { var p = _positions[id]; double d = (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z); if (d <= r2) found.Add((id, d)); }
        }
        return found.OrderBy(f => f.D).ThenBy(f => f.Id).Select(f => f.Id).ToArray();
    }

    private (int, int) Key(double x, double z) => ((int)Math.Floor(x / _cell), (int)Math.Floor(z / _cell));
}

/// <summary>Level-of-detail for AI: far agents think less often, staggered by id so load is spread.</summary>
public sealed class AiLodScheduler
{
    public double NearDistance { get; set; } = 50;
    public double MidDistance { get; set; } = 200;
    public int MidInterval { get; set; } = 4;
    public int FarInterval { get; set; } = 16;
    public int IntervalFor(double distance) => distance <= NearDistance ? 1 : distance <= MidDistance ? Math.Max(1, MidInterval) : Math.Max(1, FarInterval);
    public bool ShouldThink(EntityId id, long tick, double distance)
    {
        int interval = IntervalFor(distance);
        return interval == 1 || (StableHash.Mix(id.Value) + (ulong)tick) % (ulong)interval == 0;
    }
}

/// <summary>Goal-oriented action for multi-step plans. Facts are boolean symbols.</summary>
public sealed class GoapAction
{
    public GoapAction(string id, double cost, IReadOnlyDictionary<string, bool> preconditions, IReadOnlyDictionary<string, bool> effects)
    { Id = id; Cost = Math.Max(0.0001, cost); Preconditions = preconditions; Effects = effects; }
    public string Id { get; }
    public double Cost { get; }
    public IReadOnlyDictionary<string, bool> Preconditions { get; }
    public IReadOnlyDictionary<string, bool> Effects { get; }
}

public static class GoapPlanner
{
    /// <summary>Cheapest action sequence (A*) reaching the goal, or null. Deterministic.</summary>
    public static IReadOnlyList<GoapAction>? Plan(IReadOnlyDictionary<string, bool> state, IReadOnlyDictionary<string, bool> goal, IEnumerable<GoapAction> actions, int maxNodes = 4096)
    {
        var ordered = actions.OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();
        var open = new SortedSet<(double F, long Seq)>();
        var nodes = new Dictionary<long, (SortedDictionary<string, bool> State, double G, long Parent, GoapAction? Action)>();
        var closed = new HashSet<string>(StringComparer.Ordinal);
        long seq = 0;
        var start = new SortedDictionary<string, bool>(StringComparer.Ordinal);
        foreach (var fact in state) start[fact.Key] = fact.Value;
        nodes[seq] = (start, 0, -1, null); open.Add((Unmet(start, goal), seq++));
        while (open.Count > 0 && nodes.Count < maxNodes)
        {
            var current = open.Min; open.Remove(current);
            var node = nodes[current.Seq];
            if (Unmet(node.State, goal) == 0)
            {
                var plan = new List<GoapAction>();
                for (long i = current.Seq; nodes[i].Action is { } a; i = nodes[i].Parent) plan.Add(a);
                plan.Reverse(); return plan;
            }
            if (!closed.Add(Key(node.State))) continue;
            foreach (var action in ordered)
            {
                if (Unmet(node.State, action.Preconditions) > 0) continue;
                var next = new SortedDictionary<string, bool>(node.State, StringComparer.Ordinal);
                foreach (var e in action.Effects) next[e.Key] = e.Value;
                if (closed.Contains(Key(next))) continue;
                double g = node.G + action.Cost;
                nodes[seq] = (next, g, current.Seq, action); open.Add((g + Unmet(next, goal), seq++));
            }
        }
        return null;
    }

    private static int Unmet(IReadOnlyDictionary<string, bool> state, IReadOnlyDictionary<string, bool> wanted)
    { int n = 0; foreach (var w in wanted) if ((state.TryGetValue(w.Key, out bool v) && v) != w.Value) n++; return n; }
    private static string Key(SortedDictionary<string, bool> s) => string.Join(";", s.Select(x => x.Key + "=" + (x.Value ? 1 : 0)));
}
