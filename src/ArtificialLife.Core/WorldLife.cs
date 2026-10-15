namespace ArtificialLife.Core;

// A-Life in the spirit of STALKER and Kenshi: settlements keep their people as data, a world clock runs in cycles,
// some settlements send squads on missions along the road graph, squads meet rivals and mutants and fight,
// people get wounded, heal, improve their skills with use and earn bounties. Engines give bodies only to what is near
// the player and can take control of a squad (ControlledExternally) while it is on screen.

public enum WorldFaction { Villagers, Hunters, Militia, Bandits, Mutants }
public enum MissionKind { Patrol, Caravan, Hunt, Raid, Scavenge }

/// <summary>A place on the map; Kind is the generator's site kind name (Town, Village, Farm, Mine, Nest...).</summary>
public sealed record WorldPlace(int Id, string Name, string Kind, double X, double Z, int Tier);
/// <summary>Road between two places; Length in metres.</summary>
public sealed record WorldRoute(int From, int To, double Length);
public sealed record WorldEvent(double Hour, string Kind, string Text, double X, double Z);

public sealed class WorldPerson
{
    internal WorldPerson(EntityId id, string name, WorldFaction faction, int home, double combat, double survival)
    { Id = id; Name = name; Faction = faction; Home = home; Combat = combat; Survival = survival; }
    public EntityId Id { get; }
    public string Name { get; }
    public WorldFaction Faction { get; }
    public int Home { get; internal set; }
    public double Health { get; internal set; } = 1;
    /// <summary>0-100; grows a little with every fight (Kenshi-style learning by use).</summary>
    public double Combat { get; internal set; }
    /// <summary>0-100; grows while travelling.</summary>
    public double Survival { get; internal set; }
    public double Gear { get; internal set; } = .3;
    /// <summary>Money on this person's head, raised by killing villagers or militia.</summary>
    public long Bounty { get; internal set; }
    public bool Alive => Health > 0;
    public bool Wounded => Alive && Health < .5;
    public double Strength => Alive ? Health * (.5 + Combat / 100) * (.6 + Gear) : 0;
}

public sealed class WorldSettlement
{
    internal WorldSettlement(WorldPlace place, WorldFaction owner, int capacity) { Place = place; Owner = owner; Capacity = capacity; }
    public WorldPlace Place { get; }
    public WorldFaction Owner { get; internal set; }
    public int Capacity { get; }
    public List<WorldPerson> Residents { get; } = new();
    public double Food { get; internal set; } = 20;
    public double Wealth { get; internal set; } = 100;
    public double Defence => Residents.Where(r => r.Alive && r.Faction == Owner).Sum(r => r.Strength);
}

public sealed class WorldSquad
{
    internal WorldSquad(int id, WorldFaction faction, MissionKind mission, int origin, int target) { Id = id; Faction = faction; Mission = mission; Origin = origin; Target = target; }
    public int Id { get; }
    public WorldFaction Faction { get; }
    public MissionKind Mission { get; }
    public int Origin { get; }
    public int Target { get; internal set; }
    public List<WorldPerson> Members { get; } = new();
    public bool Returning { get; internal set; }
    /// <summary>Set by the engine while the squad has bodies near the player: the simulation stops moving and fighting it.</summary>
    public bool ControlledExternally { get; set; }
    public double X { get; internal set; }
    public double Z { get; internal set; }
    internal readonly List<int> Path = new();
    /// <summary>Next place on the route (-1 when stopped); engines use it to orient bodies.</summary>
    public int NextPlaceId => Path.Count > 0 ? Path[0] : -1;
    internal double EdgeProgress;
    public int Alive => Members.Count(m => m.Alive);
    public double Strength => Members.Sum(m => m.Strength);
    public bool Done { get; internal set; }
    /// <summary>Lets the engine move a controlled squad; the simulation continues from there.</summary>
    public void SetPosition(double x, double z) { X = x; Z = z; }
}

public sealed class WorldLife
{
    readonly WorldPlace[] _places; readonly WorldRoute[] _routes; readonly double[,] _relations;
    readonly IdSequence _ids; DeterministicRandom _random; int _squadIds; double _cycleClock;
    readonly List<WorldSquad> _squads = new(); readonly List<WorldEvent> _events = new();
    static readonly string[] Syllables = { "ka", "ro", "mi", "ta", "le", "vo", "na", "ri", "dus", "an", "be", "ok", "sel", "ma", "tor", "vi" };

    public WorldLife(ulong seed, IEnumerable<WorldPlace> places, IEnumerable<WorldRoute> routes)
    {
        _places = places.OrderBy(p => p.Id).ToArray(); _routes = routes.ToArray();
        if (_places.Length == 0) throw new ArgumentException("At least one place is required.", nameof(places));
        _random = new DeterministicRandom(seed); _ids = new IdSequence(seed);
        int f = Enum.GetValues(typeof(WorldFaction)).Length; _relations = new double[f, f];
        for (int a = 0; a < f; a++) for (int b = 0; b < f; b++) _relations[a, b] = a == b ? 1 : BaseRelation((WorldFaction)a, (WorldFaction)b);
        foreach (var place in _places)
        {
            var settlement = new WorldSettlement(place, OwnerFor(place.Kind), place.Tier switch { >= 2 => 18, 1 => 10, _ => 6 });
            Settlements.Add(settlement);
            for (int i = 0; i < settlement.Capacity * 3 / 4; i++) settlement.Residents.Add(Born(settlement));
        }
    }

    public double Hours { get; private set; }
    public int MaxSquads { get; set; } = 8;
    /// <summary>World clock period: missions and random events are considered once per cycle, not every step.</summary>
    public double CycleHours { get; set; } = .5;
    public double SquadSpeedKmh { get; set; } = 4;
    public double EncounterMetres { get; set; } = 300;
    public List<WorldSettlement> Settlements { get; } = new();
    public IReadOnlyList<WorldSquad> Squads => _squads;
    public IReadOnlyList<WorldEvent> Events => _events;
    public event Action<WorldEvent>? EventRaised;

    public double Relation(WorldFaction a, WorldFaction b) => _relations[(int)a, (int)b];
    public bool Hostile(WorldFaction a, WorldFaction b) => a != b && Relation(a, b) < -.3;

    static WorldFaction OwnerFor(string kind) => kind switch
    {
        "Outpost" => WorldFaction.Militia, "HuntersCamp" => WorldFaction.Hunters, "BanditCamp" => WorldFaction.Bandits,
        "Nest" or "Ruins" => WorldFaction.Mutants, _ => WorldFaction.Villagers,
    };

    static double BaseRelation(WorldFaction a, WorldFaction b)
    {
        if (a == WorldFaction.Mutants || b == WorldFaction.Mutants) return -1;
        if (a == WorldFaction.Bandits || b == WorldFaction.Bandits) return -.7;
        return .5;
    }

    WorldPerson Born(WorldSettlement home)
    {
        string name = home.Owner == WorldFaction.Mutants ? "Morto-vivo" : Capital(Syllables[_random.Next(Syllables.Length)] + Syllables[_random.Next(Syllables.Length)]) + " " + Capital(Syllables[_random.Next(Syllables.Length)] + Syllables[_random.Next(Syllables.Length)]);
        double baseCombat = home.Owner switch { WorldFaction.Militia => 35, WorldFaction.Bandits => 30, WorldFaction.Hunters => 28, WorldFaction.Mutants => 25, _ => 12 };
        return new WorldPerson(_ids.Next("person"), name, home.Owner, home.Place.Id, baseCombat + _random.NextDouble() * 15, 10 + _random.NextDouble() * 20);
        static string Capital(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    public void Step(double hours)
    {
        if (!(hours > 0) || double.IsInfinity(hours)) throw new ArgumentOutOfRangeException(nameof(hours));
        Hours += hours;
        foreach (var squad in _squads.Where(s => !s.Done && !s.ControlledExternally).OrderBy(s => s.Id)) Travel(squad, hours);
        Encounters();
        foreach (var s in Settlements) Upkeep(s, hours);
        _cycleClock += hours;
        while (_cycleClock >= CycleHours) { _cycleClock -= CycleHours; Cycle(); }
        _squads.RemoveAll(s => s.Done);
    }

    // One world-clock cycle: a few settlements (not all) consider a mission, plus a chance of a random event.
    void Cycle()
    {
        for (int k = 0; k < 3 && _squads.Count < MaxSquads; k++)
        {
            var home = Settlements[_random.Next(Settlements.Count)];
            if (_random.NextDouble() < .35) TryMission(home);
        }
        if (_random.NextDouble() < .08) RandomEvent();
    }

    void TryMission(WorldSettlement home)
    {
        var ready = home.Residents.Where(r => r.Alive && !r.Wounded && r.Faction == home.Owner).ToList();
        if (ready.Count < 3 || _squads.Any(s => s.Origin == home.Place.Id && !s.Done)) return;
        WorldSettlement? target = null; MissionKind kind;
        switch (home.Owner)
        {
            case WorldFaction.Bandits:
                kind = MissionKind.Raid; // the weakest of the three closest hostile places
                target = Settlements.Where(s => s != home && Hostile(WorldFaction.Bandits, s.Owner) && s.Owner != WorldFaction.Mutants).OrderBy(s => Distance(home.Place, s.Place)).Take(3).OrderBy(s => s.Defence * (1 + s.Place.Tier * .5)).FirstOrDefault(); break;
            case WorldFaction.Mutants:
                kind = MissionKind.Raid; target = Nearest(home, s => s.Owner != WorldFaction.Mutants); break;
            case WorldFaction.Militia:
                // Retake places held by bandits first; otherwise patrol friendly ground.
                target = Nearest(home, s => s.Owner == WorldFaction.Bandits && s.Place.Kind != "BanditCamp");
                kind = target is null ? MissionKind.Patrol : MissionKind.Raid;
                target ??= Pick(s => s.Owner is WorldFaction.Villagers or WorldFaction.Militia && s != home); break;
            case WorldFaction.Hunters:
                kind = MissionKind.Hunt; target = Nearest(home, s => s.Owner == WorldFaction.Mutants) ?? Pick(s => s != home); break;
            default:
                if (home.Food < 15) { kind = MissionKind.Caravan; target = Settlements.Where(s => s != home && !Hostile(home.Owner, s.Owner)).OrderByDescending(s => s.Food).FirstOrDefault(); }
                else { kind = MissionKind.Scavenge; target = Pick(s => s != home && s.Place.Kind is "Ruins" or "Mine"); }
                break;
        }
        if (target is null) return;
        var path = ShortestPath(home.Place.Id, target.Place.Id);
        if (path is null) return;
        var squad = new WorldSquad(++_squadIds, home.Owner, kind, home.Place.Id, target.Place.Id) { X = home.Place.X, Z = home.Place.Z };
        squad.Path.AddRange(path);
        int size = Math.Min(ready.Count, 2 + _random.Next(4));
        foreach (var person in ready.OrderByDescending(p => p.Combat).Take(size)) { squad.Members.Add(person); home.Residents.Remove(person); }
        _squads.Add(squad);
        Raise("mission", $"{Label(home.Owner)} de {home.Place.Name} saem em {Label(kind)} rumo a {target.Place.Name}.", squad.X, squad.Z);
    }

    WorldSettlement? Nearest(WorldSettlement from, Func<WorldSettlement, bool> filter) => Settlements.Where(s => s != from && filter(s))
        .OrderBy(s => Distance(from.Place, s.Place)).ThenBy(s => s.Place.Id).FirstOrDefault();
    WorldSettlement? Pick(Func<WorldSettlement, bool> filter) { var list = Settlements.Where(filter).ToList(); return list.Count == 0 ? null : list[_random.Next(list.Count)]; }
    static double Distance(WorldPlace a, WorldPlace b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    // Dijkstra on the road graph (a few dozen nodes); straight line when the places are not connected.
    List<int>? ShortestPath(int from, int to)
    {
        var dist = _places.ToDictionary(p => p.Id, _ => double.MaxValue); var prev = new Dictionary<int, int>(); var open = new SortedSet<(double D, int Id)>();
        dist[from] = 0; open.Add((0, from));
        while (open.Count > 0)
        {
            var (d, id) = open.Min; open.Remove(open.Min);
            if (id == to) break;
            foreach (var r in _routes.Where(r => r.From == id || r.To == id))
            {
                int next = r.From == id ? r.To : r.From; double nd = d + r.Length;
                if (!dist.ContainsKey(next) || nd >= dist[next]) continue;
                open.Remove((dist[next], next)); dist[next] = nd; prev[next] = id; open.Add((nd, next));
            }
        }
        if (from == to) return new List<int> { to };
        if (!prev.ContainsKey(to)) return new List<int> { to };
        var path = new List<int>(); for (int c = to; c != from; c = prev[c]) path.Add(c);
        path.Reverse(); return path;
    }

    WorldPlace Place(int id) => _places.First(p => p.Id == id);

    void Travel(WorldSquad squad, double hours)
    {
        double metres = SquadSpeedKmh * 1000 * hours;
        while (metres > 0 && squad.Path.Count > 0)
        {
            var next = Place(squad.Path[0]);
            double dx = next.X - squad.X, dz = next.Z - squad.Z, left = Math.Sqrt(dx * dx + dz * dz);
            if (left <= metres) { squad.X = next.X; squad.Z = next.Z; metres -= left; squad.Path.RemoveAt(0); }
            else { squad.X += dx / left * metres; squad.Z += dz / left * metres; metres = 0; }
        }
        foreach (var m in squad.Members.Where(m => m.Alive)) m.Survival = Math.Min(100, m.Survival + hours * .05 * (1 - m.Survival / 100));
        if (squad.Path.Count == 0) Arrive(squad);
    }

    void Arrive(WorldSquad squad)
    {
        var place = Settlements.First(s => s.Place.Id == (squad.Returning ? squad.Origin : squad.Target));
        if (squad.Returning) { Disband(squad, place); return; }
        switch (squad.Mission)
        {
            case MissionKind.Raid when Hostile(squad.Faction, place.Owner):
                double defence = place.Defence * (1 + place.Place.Tier * .5) * (.7 + _random.NextDouble() * .6), attack = squad.Strength * (.7 + _random.NextDouble() * .6); // bigger places have walls
                Train(squad.Members, 3); Train(place.Residents, 3);
                if (attack > defence && squad.Faction == WorldFaction.Mutants)
                {
                    // Mutants kill and plunder but never hold a settlement.
                    int before = place.Residents.Count(r => r.Alive);
                    foreach (var r in place.Residents.Where(r => r.Alive).Take(2 + _random.Next(3))) Wound(r, .5 + _random.NextDouble() * .7, null);
                    place.Food *= .6;
                    Raise("attack", $"Mortos-vivos atacaram {place.Place.Name}: {before - place.Residents.Count(r => r.Alive)} mortos.", place.Place.X, place.Place.Z);
                    break;
                }
                if (attack > defence)
                {
                    foreach (var r in place.Residents.Where(r => r.Alive && r.Faction == place.Owner)) Wound(r, .6 + _random.NextDouble() * .6, squad.Members.FirstOrDefault(m => m.Alive));
                    Raise("capture", $"{Label(squad.Faction)} tomaram {place.Place.Name} de {Label(place.Owner)}!", place.Place.X, place.Place.Z);
                    ChangeRelation(squad.Faction, place.Owner, -.2);
                    place.Owner = squad.Faction; place.Residents.RemoveAll(r => !r.Alive || r.Faction != squad.Faction);
                    foreach (var m in squad.Members.Where(m => m.Alive)) { m.Home = place.Place.Id; place.Residents.Add(m); }
                    squad.Done = true; return;
                }
                foreach (var m in squad.Members.Where(m => m.Alive)) Wound(m, .3 + _random.NextDouble() * .5, place.Residents.FirstOrDefault(r => r.Alive));
                foreach (var r in place.Residents.Where(r => r.Alive).Take(_random.Next(3))) Wound(r, .4 + _random.NextDouble() * .7, squad.Members.FirstOrDefault(m => m.Alive));
                Raise("raid", $"{place.Place.Name} repeliu o ataque de {Label(squad.Faction)}.", place.Place.X, place.Place.Z);
                break;
            case MissionKind.Caravan:
                double cargo = Math.Min(20, place.Food * .4); place.Food -= cargo; place.Wealth += cargo * 2;
                Settlements.First(s => s.Place.Id == squad.Origin).Food += cargo;
                Raise("trade", $"Caravana de {Place(squad.Origin).Name} comprou {cargo:0} de comida em {place.Place.Name}.", place.Place.X, place.Place.Z);
                break;
            case MissionKind.Scavenge:
                foreach (var m in squad.Members.Where(m => m.Alive)) m.Gear = Math.Min(1, m.Gear + _random.NextDouble() * .1);
                Raise("loot", $"Grupo de {Place(squad.Origin).Name} vasculhou {place.Place.Name}.", place.Place.X, place.Place.Z);
                break;
        }
        GoHome(squad);
    }

    void GoHome(WorldSquad squad)
    {
        squad.Returning = true;
        var origin = Settlements.First(s => s.Place.Id == squad.Origin);
        if (Hostile(squad.Faction, origin.Owner)) { squad.Done = true; Raise("lost", $"{Label(squad.Faction)} perderam o lar {origin.Place.Name} e se dispersaram.", squad.X, squad.Z); return; }
        var current = _places.OrderBy(p => (p.X - squad.X) * (p.X - squad.X) + (p.Z - squad.Z) * (p.Z - squad.Z)).First();
        squad.Path.Clear(); squad.Path.AddRange(ShortestPath(current.Id, squad.Origin) ?? new List<int> { squad.Origin });
    }

    void Disband(WorldSquad squad, WorldSettlement home)
    {
        foreach (var m in squad.Members.Where(m => m.Alive)) home.Residents.Add(m);
        squad.Done = true;
    }

    // Squads close to each other: hostile ones fight; morale breaks when half the squad is down.
    void Encounters()
    {
        var active = _squads.Where(s => !s.Done && !s.ControlledExternally && s.Alive > 0).OrderBy(s => s.Id).ToList();
        for (int i = 0; i < active.Count; i++) for (int j = i + 1; j < active.Count; j++)
        {
            WorldSquad a = active[i], b = active[j];
            if (a.Done || b.Done || !Hostile(a.Faction, b.Faction)) continue;
            double dx = a.X - b.X, dz = a.Z - b.Z;
            if (dx * dx + dz * dz > EncounterMetres * EncounterMetres) continue;
            Fight(a, b);
        }
    }

    /// <summary>Resolves a fight between two squads (also usable by engines for off-screen clashes).</summary>
    public void Fight(WorldSquad a, WorldSquad b)
    {
        int startA = a.Alive, startB = b.Alive;
        for (int round = 0; round < 6 && a.Alive > 0 && b.Alive > 0; round++)
        {
            Volley(a, b); Volley(b, a);
            if (a.Alive * 2 < startA || b.Alive * 2 < startB) break; // morale breaks
        }
        var (winner, loser) = a.Strength >= b.Strength ? (a, b) : (b, a);
        foreach (var m in winner.Members.Where(m => m.Alive)) m.Gear = Math.Min(1, m.Gear + .03);
        ChangeRelation(a.Faction, b.Faction, -.05);
        Raise("fight", $"{Label(winner.Faction)} venceram {Label(loser.Faction)} ({loser.Members.Count(m => !m.Alive)} mortos) perto de {NearestPlace(a.X, a.Z).Name}.", a.X, a.Z);
        if (loser.Alive == 0) loser.Done = true; else GoHome(loser);
        if (winner.Alive * 2 < winner.Members.Count) GoHome(winner);
    }

    void Volley(WorldSquad shooters, WorldSquad targets)
    {
        foreach (var s in shooters.Members.Where(m => m.Alive))
        {
            var target = targets.Members.Where(m => m.Alive).OrderBy(m => m.Health).FirstOrDefault();
            if (target is null) return;
            double hit = .25 + s.Combat / 200 - target.Combat / 400;
            if (_random.NextDouble() < hit) Wound(target, .25 + _random.NextDouble() * .35 * (.6 + s.Gear), s);
            s.Combat = Math.Min(100, s.Combat + .4 * (1 - s.Combat / 100)); // learning by use
        }
    }

    void Train(IEnumerable<WorldPerson> people, double amount) { foreach (var p in people.Where(p => p.Alive)) p.Combat = Math.Min(100, p.Combat + amount * (1 - p.Combat / 100)); }

    void Wound(WorldPerson victim, double damage, WorldPerson? attacker)
    {
        victim.Health = Math.Max(0, victim.Health - damage);
        if (victim.Alive || attacker is null) return;
        if (victim.Faction is WorldFaction.Villagers or WorldFaction.Militia && attacker.Faction != WorldFaction.Mutants) attacker.Bounty += 100;
    }

    void Upkeep(WorldSettlement s, double hours)
    {
        double production = s.Place.Kind switch { "Farm" => 3, "Fishing" or "Port" => 2, "HuntersCamp" => 1.5, "Village" => 1, "Town" => .6, _ => .3 };
        if (s.Owner == WorldFaction.Mutants) production = 0;
        s.Food = Math.Max(0, s.Food + (production - .05 * s.Residents.Count(r => r.Alive)) * hours);
        foreach (var r in s.Residents.Where(r => r.Alive)) r.Health = Math.Min(1, r.Health + (s.Food > 0 ? .02 : .005) * hours); // rest heals
        s.Residents.RemoveAll(r => !r.Alive);
        // Slow regrowth: newcomers arrive while there is food (mutant nests always spawn).
        double chance = hours * (s.Owner == WorldFaction.Mutants ? .08 : s.Food > 10 ? .03 : 0);
        if (s.Residents.Count < s.Capacity && _random.NextDouble() < chance) s.Residents.Add(Born(s));
    }

    void RandomEvent()
    {
        var route = _routes.Length > 0 ? _routes[_random.Next(_routes.Length)] : null;
        if (route is null) return;
        WorldPlace a = Place(route.From), b = Place(route.To); double t = _random.NextDouble();
        double x = a.X + (b.X - a.X) * t, z = a.Z + (b.Z - a.Z) * t;
        if (_random.NextDouble() < .6 && _squads.Count < MaxSquads)
        {
            var nest = Settlements.FirstOrDefault(s => s.Owner == WorldFaction.Mutants);
            if (nest is null) return;
            var pack = new WorldSquad(++_squadIds, WorldFaction.Mutants, MissionKind.Raid, nest.Place.Id, (_random.NextDouble() < .5 ? a : b).Id) { X = x, Z = z };
            for (int i = 0; i < 2 + _random.Next(3); i++) pack.Members.Add(Born(nest));
            pack.Path.Add(pack.Target); _squads.Add(pack);
            Raise("mutants", $"Bando de mutantes surgiu na estrada entre {a.Name} e {b.Name}!", x, z);
        }
        else
        {
            var finder = Settlements.Where(s => s.Owner != WorldFaction.Mutants).OrderBy(s => (s.Place.X - x) * (s.Place.X - x) + (s.Place.Z - z) * (s.Place.Z - z)).FirstOrDefault();
            if (finder is null) return;
            finder.Wealth += 50; Raise("cache", $"Esconderijo encontrado perto de {finder.Place.Name}: +50 de riqueza.", x, z);
        }
    }

    public WorldPlace NearestPlace(double x, double z) => _places.OrderBy(p => (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z)).First();
    public IEnumerable<WorldSquad> SquadsNear(double x, double z, double radius) => _squads.Where(s => !s.Done && (s.X - x) * (s.X - x) + (s.Z - z) * (s.Z - z) <= radius * radius);

    /// <summary>Engines report deaths and wounds that happened in an on-screen fight.</summary>
    public void ReportDamage(WorldPerson victim, double damage, WorldPerson? attacker) => Wound(victim, damage, attacker);

    void ChangeRelation(WorldFaction a, WorldFaction b, double delta)
    {
        if (a == b) return;
        double v = Math.Max(-1, Math.Min(1, _relations[(int)a, (int)b] + delta));
        _relations[(int)a, (int)b] = _relations[(int)b, (int)a] = v;
    }

    void Raise(string kind, string text, double x, double z)
    {
        var e = new WorldEvent(Math.Round(Hours, 2), kind, text, x, z);
        _events.Add(e); if (_events.Count > 300) _events.RemoveAt(0);
        EventRaised?.Invoke(e);
    }

    /// <summary>Deterministic digest of the world state (same seed and steps give the same value).</summary>
    public ulong Fingerprint()
    {
        ulong h = StableHash.Combine((ulong)(Hours * 1000), (ulong)_squadIds);
        foreach (var s in Settlements) h = StableHash.Combine(h, (ulong)s.Owner, (ulong)s.Residents.Count, (ulong)(s.Food * 100));
        foreach (var e in _events) h = StableHash.Combine(h, StableHash.Text(e.Text));
        return h;
    }

    public static string Label(WorldFaction f) => f switch { WorldFaction.Villagers => "Aldeões", WorldFaction.Hunters => "Caçadores", WorldFaction.Militia => "Milícia", WorldFaction.Bandits => "Bandidos", _ => "Mortos-vivos" };
    public static string Label(MissionKind m) => m switch { MissionKind.Patrol => "patrulha", MissionKind.Caravan => "caravana", MissionKind.Hunt => "caçada", MissionKind.Raid => "ataque", _ => "busca de recursos" };
}
