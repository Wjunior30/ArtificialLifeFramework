namespace ArtificialLife.Core;

// Island politics and war (STALKER A-Life + Kenshi + Shadow of War Nemesis):
// five military factions hold four cities each and live as data on the road graph; ideologies drive their economy,
// unrest and treatment of the conquered; diplomacy forms coalitions against the strongest, resource pacts, truces and
// betrayals; officers (leader, generals, captains) have traits, remember the player, get promoted, duel and defect.
// A faction losing all its cities becomes exiles and, if they fail, goes extinct. Nomad merchants own no city and trade
// between everyone; monsters raise waves from their lairs. Everything is deterministic per seed.

public enum Ideology { Capitalism, Socialism, Communism, Theocracy, Feudalism, Anarchism, Authoritarian, Mercantile, Monstrous }
public enum ConquestPolicy { Integrate, Exploit, Convert, Execute, Liberate }
public enum Rank { Soldier, Captain, General, Leader }
public enum Stance { War, Hostile, Neutral, Truce, Alliance, Vassal }
public enum IslandMission { Raid, Patrol, Hunt, Trade, Reconquest }

/// <summary>Faction data: replace Default with the game's own story (names, ideology, goals, how they treat the defeated).</summary>
public sealed record FactionProfile(int Id, string Name, string LeaderName, Ideology Ideology, ConquestPolicy Conquest, string Story,
    string[] PreferredTargets, bool Nomadic = false, bool Monsters = false, int SwornEnemy = -1, bool AlliedWithMonsters = false, NameCulture? Culture = null)
{
    public static IReadOnlyList<FactionProfile> Default { get; } = new[]
    {
        new FactionProfile(0, "Guarda de Ferro", "Comandante Varga", Ideology.Authoritarian, ConquestPolicy.Exploit, "Restos do antigo exército; ordem e tributo pelas rodovias.", new[] { "Town", "Village" }),
        new FactionProfile(1, "Irmandade da Cinza", "Abadessa Ilmar", Ideology.Theocracy, ConquestPolicy.Convert, "Purificadores que queimam a praga dos mortos.", new[] { "Nest", "Ruins" }, SwornEnemy: 4),
        new FactionProfile(2, "Corsários do Porto Velho", "Capitã Rosa Negra", Ideology.Capitalism, ConquestPolicy.Integrate, "Marinheiros presos na ilha que vivem do comércio e do saque.", new[] { "Port", "Fishing", "Farm" }),
        new FactionProfile(3, "Os Livres", "Dorran, o Ruivo", Ideology.Communism, ConquestPolicy.Liberate, "Mineiros revoltados contra o tributo; o que é de um é de todos.", new[] { "Mine", "Village" }, SwornEnemy: 0),
        new FactionProfile(4, "Culto do Véu", "O Velado", Ideology.Feudalism, ConquestPolicy.Execute, "Senhores necromantes que guiam os mortos como servos.", new[] { "Village", "Farm" }, SwornEnemy: 1, AlliedWithMonsters: true),
        new FactionProfile(5, "Nômades da Estrada", "Mestra Sahra", Ideology.Mercantile, ConquestPolicy.Integrate, "Caravanas sem cidade; neutras, compram, vendem e levam recados.", Array.Empty<string>(), Nomadic: true),
        new FactionProfile(6, "Mortos-vivos", "-", Ideology.Monstrous, ConquestPolicy.Execute, "A praga que se levanta dos ninhos e das ruínas.", Array.Empty<string>(), Monsters: true),
    };
}

/// <summary>How an ideology runs a society (multipliers around 1).</summary>
public sealed record IdeologyRules(double Production, double Tax, double Growth, double Recruit, double Unrest, double Trade, bool Purges)
{
    public static IdeologyRules For(Ideology i) => i switch
    {
        Ideology.Capitalism => new(1.25, .15, 1.0, .8, .25, 1.4, false),   // productive and trading; inequality breeds unrest
        Ideology.Socialism => new(1.0, .35, 1.15, .9, .05, 1.0, false),    // redistribution keeps people content
        Ideology.Communism => new(.95, .45, 1.0, 1.35, .15, .6, true),      // planned economy, mass levies, purges
        Ideology.Theocracy => new(.9, .3, 1.1, 1.1, .05, .7, false),        // zeal and conversion
        Ideology.Feudalism => new(.95, .4, .9, .9, .2, .8, false),          // lords, serfs and tribute
        Ideology.Anarchism => new(.9, .05, 1.0, .85, 0, 1.0, false),        // free communes: resilient, weak offence
        Ideology.Authoritarian => new(1.05, .4, .9, 1.4, .3, .7, true),     // war economy, oppression
        Ideology.Mercantile => new(1, .1, 1, .6, 0, 2, false),
        _ => new(0, 0, 1, 1, 0, 0, false),
    };
    // Position on two axes (economy left-right, authority) used for ideological affinity.
    public static (double Economy, double Authority) Axes(Ideology i) => i switch
    {
        Ideology.Capitalism => (1, .2), Ideology.Mercantile => (.8, 0), Ideology.Feudalism => (.4, .8), Ideology.Authoritarian => (.2, 1),
        Ideology.Theocracy => (0, .9), Ideology.Socialism => (-.5, .1), Ideology.Communism => (-1, .6), Ideology.Anarchism => (-.8, -1), _ => (0, 0),
    };
}

public sealed class IslandPerson
{
    internal IslandPerson(EntityId id, string name, int faction, int home) { Id = id; Name = name; Faction = faction; Home = home; }
    public EntityId Id { get; }
    public string Name { get; internal set; }
    public int Faction { get; internal set; }
    public int Home { get; internal set; }
    public bool Female { get; internal set; }
    public double Health { get; internal set; } = 1;
    public double Combat { get; internal set; }
    public double Gear { get; internal set; } = .3;
    public Rank Rank { get; internal set; }
    public string Title { get; internal set; } = "";
    public List<string> Traits { get; } = new();
    public string Strength_ { get; internal set; } = "";
    public string Weakness { get; internal set; } = "";
    public double Loyalty { get; internal set; } = .7;
    public double Ambition { get; internal set; } = .3;
    public int Kills { get; internal set; }
    /// <summary>What this officer remembers about the player (Nemesis).</summary>
    public List<string> Memory { get; } = new();
    public long Bounty { get; internal set; }
    /// <summary>Conquered and not yet loyal: fights at a fraction of its power until this hour.</summary>
    public double SubjugatedUntil { get; internal set; }
    public bool Alive => Health > 0;
    public bool Wounded => Alive && Health < .5;
    public bool Officer => Rank != Rank.Soldier;
    public string DisplayName => Officer ? $"{Name} {Title}".Trim() : Name;
    public double Power => Alive ? Health * (.5 + Combat / 100) * (.6 + Gear) * (Rank switch { Rank.Leader => 2.2, Rank.General => 1.7, Rank.Captain => 1.3, _ => 1 }) : 0;
}

public sealed class IslandCity
{
    internal IslandCity(WorldPlace place, int owner, int capacity) { Place = place; Owner = owner; OriginalOwner = owner; PreviousOwner = owner; Capacity = capacity; }
    public WorldPlace Place { get; internal set; }
    public int Owner { get; internal set; }
    public int OriginalOwner { get; internal set; }
    public int PreviousOwner { get; internal set; }
    /// <summary>Faction whose capital this is (-1 for ordinary cities).</summary>
    public int CapitalOf { get; internal set; } = -1;
    public bool IsCity => Place.Tier >= 1;
    public bool IsLair { get; internal set; }
    public int Capacity { get; }
    public List<IslandPerson> Residents { get; } = new();
    public double Food { get; internal set; } = 30;
    public double Unrest { get; internal set; }
    internal double NextRaid;
    internal double HuntedUntil, CalmUntil, NextPurge;
    internal double Now, SubjugatedPower = .3;
    public double Defence => Residents.Where(r => r.Alive && r.Faction == Owner).Sum(r => r.Power * (r.SubjugatedUntil > Now ? SubjugatedPower : 1)) * (1 + Place.Tier * .5 + (CapitalOf >= 0 ? .5 : 0));
    /// <summary>Posts around the city. While any belongs to the owner, attackers must take them before besieging the city.</summary>
    public List<IslandOutpost> Outposts { get; } = new();
}

/// <summary>A post on the approach to a city (watchtower, antenna... the game decides what it looks like).</summary>
public sealed class IslandOutpost
{
    internal IslandOutpost(int index, double x, double z, int owner, double garrison) { Index = index; X = x; Z = z; Owner = owner; Garrison = garrison; }
    public int Index { get; }
    public double X { get; internal set; }
    public double Z { get; internal set; }
    public int Owner { get; internal set; }
    public double Garrison { get; internal set; }
}

public sealed class IslandFaction
{
    internal IslandFaction(FactionProfile profile) { Profile = profile; Rules = IdeologyRules.For(profile.Ideology); }
    public FactionProfile Profile { get; }
    public IdeologyRules Rules { get; }
    public int Id => Profile.Id;
    public double Treasury { get; internal set; } = 300;
    public double Morale { get; internal set; } = 1;
    public double WarWeariness { get; internal set; }
    /// <summary>How much the player has helped (raises morale, money and recruitment).</summary>
    public double PlayerInfluence { get; internal set; }
    public int? VassalOf { get; internal set; }
    public bool Extinct { get; internal set; }
    /// <summary>Hour the faction died out (-1 while it exists); after BalanceData.RebirthDays a revolt in one of its old cities can bring it back.</summary>
    public double ExtinctSince { get; internal set; } = -1;
    public bool Exiled { get; internal set; }
    public double Power { get; internal set; }
    public int Cities { get; internal set; }
}

public sealed class IslandSquad
{
    internal IslandSquad(int id, int faction, IslandMission mission, int origin, int target) { Id = id; Faction = faction; Mission = mission; Origin = origin; Target = target; }
    public int Id { get; }
    public int Faction { get; }
    public IslandMission Mission { get; }
    public int Origin { get; internal set; }
    public int Target { get; internal set; }
    public List<IslandPerson> Members { get; } = new();
    public IslandPerson? Leader => Members.Where(m => m.Alive && m.Officer).OrderByDescending(m => m.Rank).FirstOrDefault();
    public double X { get; internal set; }
    public double Z { get; internal set; }
    public double Cargo { get; internal set; }
    public IslandSquad? Prey { get; internal set; }
    public bool Returning { get; internal set; }
    public bool Done { get; internal set; }
    public bool ControlledExternally { get; set; }
    internal readonly List<int> Path = new();
    public int NextPlaceId => Path.Count > 0 ? Path[0] : -1;
    public int Alive => Members.Count(m => m.Alive);
    public double Strength => Members.Sum(m => m.Power);
    public void SetPosition(double x, double z) { X = x; Z = z; }
}

/// <summary>A world event. Text is the built-in Portuguese line; FactionA/FactionB/Place/Subject let a game build its own (see IslandLife.Localize).</summary>
public sealed record IslandEvent(double Hour, string Kind, string Text, double X, double Z, int Priority = 1, int FactionA = -1, int FactionB = -1, int Place = -1, string? Subject = null);

public sealed partial class IslandLife
{
    static readonly string[] TraitPool = { "Corajoso", "Covarde", "Cruel", "Honrado", "Vingativo", "Ambicioso", "Leal", "Astuto" };
    static readonly string[] StrengthPool = { "Mestre do arco", "Pele de ferro", "Implacável", "Estrategista", "Veloz" };
    static readonly string[] WeaknessPool = { "Medo de fogo", "Foge se o líder cair", "Arrogante", "Bebe demais", "Lento" };

    readonly WorldPlace[] _places; readonly WorldRoute[] _routes; readonly double[,] _relations; readonly double[,] _truceUntil; readonly double[,] _lastPact; readonly double[,] _warSince;
    readonly NameForge _names = new();
    readonly ulong _seed; readonly IdSequence _ids; DeterministicRandom _random; int _squadIds; double _cycleClock, _dayClock;
    readonly List<IslandSquad> _squads = new(); readonly List<IslandEvent> _events = new();
    readonly bool[,] _atWar;
    double _lastCoalition = -1e9;
    double[] _embargoUntil;
    /// <summary>True while the nomads refuse to trade with this faction (after it robbed a caravan).</summary>
    public bool Embargoed(int faction) => Hours < _embargoUntil[faction];

    /// <summary>Builds the island war; with forgeNames, places get fictional names fitting their kind and owner culture.</summary>
    public IslandLife(ulong seed, IEnumerable<WorldPlace> places, IEnumerable<WorldRoute> routes, IReadOnlyList<FactionProfile>? profiles = null, bool forgeNames = false)
        : this(seed, places, routes, ConfigFor(profiles), forgeNames) { }

    static IslandLifeConfig ConfigFor(IReadOnlyList<FactionProfile>? profiles)
    {
        var config = IslandLifeConfig.Default();
        if (profiles != null) config.Factions = profiles.Select(FactionData.From).ToArray();
        return config;
    }

    /// <summary>Builds the war from data: factions, place rules and balance come from the config (start from IslandLifeConfig.Default()).</summary>
    public IslandLife(ulong seed, IEnumerable<WorldPlace> places, IEnumerable<WorldRoute> routes, IslandLifeConfig config, bool forgeNames = false)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        _places = places.OrderBy(p => p.Id).ToArray(); _routes = routes.ToArray();
        if (_places.Length == 0) throw new ArgumentException("At least one place is required.", nameof(places));
        _seed = seed; _random = new DeterministicRandom(seed); _ids = new IdSequence(seed);
        Factions = Config.Factions.OrderBy(f => f.Id).Select(f => new IslandFaction(f.ToProfile())).ToList();
        int f = Factions.Count; _relations = new double[f, f]; _truceUntil = new double[f, f]; _lastPact = new double[f, f]; _warSince = new double[f, f]; _atWar = new bool[f, f]; _embargoUntil = new double[f];
        for (int a = 0; a < f; a++) for (int b = 0; b < f; b++) _relations[a, b] = a == b ? 1 : BaseRelation(Factions[a].Profile, Factions[b].Profile);
        AssignTerritory();
        if (forgeNames)
            for (int i = 0; i < Cities.Count; i++)
            {
                var c = Cities[i]; var culture = c.Owner >= 0 ? NameForge.CultureFor(Factions[c.Owner].Profile) : NameCulture.Common;
                c.Place = c.Place with { Name = _names.Place(c.Place.Kind, culture, ref _random) };
                _places[Array.FindIndex(_places, p => p.Id == c.Place.Id)] = c.Place;
            }
        foreach (var city in Cities) for (int i = 0; i < city.Capacity * 3 / 4; i++) city.Residents.Add(Born(city, city.Owner));
        AppointOfficers();
        foreach (var a in Factions) foreach (var b in Factions) if (a.Id < b.Id && Relation(a.Id, b.Id) < -.5) { _atWar[a.Id, b.Id] = _atWar[b.Id, a.Id] = true; }
        UpdatePower();
    }

    public double Hours { get; private set; }
    public IslandLifeConfig Config { get; }
    BalanceData B => Config.Balance;
    public int MaxSquads { get => B.MaxSquads; set => B.MaxSquads = value; }
    public double CycleHours { get => B.CycleHours; set => B.CycleHours = value; }
    public double SquadSpeedKmh { get => B.SquadSpeedKmh; set => B.SquadSpeedKmh = value; }
    public double EncounterMetres { get => B.EncounterMetres; set => B.EncounterMetres = value; }
    public List<IslandFaction> Factions { get; }
    public List<IslandCity> Cities { get; } = new();
    public IReadOnlyList<IslandSquad> Squads => _squads;
    public IReadOnlyList<IslandEvent> Events => _events;
    public event Action<IslandEvent>? EventRaised;
    /// <summary>The undead faction (throws when the config has none; check HasMonsters).</summary>
    public IslandFaction Monsters => Factions.First(f => f.Profile.Monsters);
    /// <summary>The nomad faction (throws when the config has none; check HasNomads).</summary>
    public IslandFaction Nomads => Factions.First(f => f.Profile.Nomadic);
    public bool HasMonsters => MonsterId >= 0;
    public bool HasNomads => NomadId >= 0;
    int MonsterId => Factions.FindIndex(f => f.Profile.Monsters);
    int NomadId => Factions.FindIndex(f => f.Profile.Nomadic);
    /// <summary>Place where the nomads hold their neutral fair (moves every week).</summary>
    public int FairPlace { get; private set; }

    public double Relation(int a, int b) => _relations[a, b];
    public bool AtWar(int a, int b) => a != b && (_atWar[a, b] || IsMonsterPair(a, b));
    bool IsMonsterPair(int a, int b)
    {
        bool am = Factions[a].Profile.Monsters, bm = Factions[b].Profile.Monsters;
        if (!(am ^ bm)) return false;
        return !(am ? Factions[b] : Factions[a]).Profile.AlliedWithMonsters; // the Cult walks with the dead
    }
    public Stance StanceOf(int a, int b)
    {
        if (Factions[a].VassalOf == b || Factions[b].VassalOf == a) return Stance.Vassal;
        if (AtWar(a, b)) return Stance.War;
        if (_truceUntil[a, b] > Hours) return Stance.Truce;
        double r = Relation(a, b);
        return r > .55 ? Stance.Alliance : r < -.15 ? Stance.Hostile : Stance.Neutral;
    }
    public IEnumerable<IslandPerson> Officers(int faction) => AllPeople().Where(p => p.Alive && p.Faction == faction && p.Officer).OrderByDescending(p => p.Rank).ThenByDescending(p => p.Combat);
    // Finished squads are skipped: their survivors already moved into a city, and counting them again duplicated exiles.
    public IEnumerable<IslandPerson> AllPeople() => Cities.SelectMany(c => c.Residents).Concat(_squads.Where(s => !s.Done).SelectMany(s => s.Members)).Distinct();
    public IEnumerable<IslandPerson> Nemeses => AllPeople().Where(p => p.Alive && p.Memory.Count > 0).OrderByDescending(p => p.Memory.Count);

    // ---------- setup ----------
    static double BaseRelation(FactionProfile a, FactionProfile b)
    {
        if (a.Monsters || b.Monsters) return a.AlliedWithMonsters || b.AlliedWithMonsters ? .6 : -1;
        if (a.Nomadic || b.Nomadic) return .35;
        if (a.SwornEnemy == b.Id || b.SwornEnemy == a.Id) return -.8;
        var (ea, aa) = IdeologyRules.Axes(a.Ideology); var (eb, ab) = IdeologyRules.Axes(b.Ideology);
        return Math.Max(-.45, .25 - .35 * Math.Abs(ea - eb) - .35 * Math.Abs(aa - ab)); // only sworn enemies start at war
    }

    void AssignTerritory()
    {
        var military = Factions.Where(f => !f.Profile.Nomadic && !f.Profile.Monsters).ToList();
        foreach (var place in _places)
        {
            var rule = Config.Rule(place.Kind);
            int capacity = rule.Capacity > 0 ? rule.Capacity : place.Tier >= 2 ? 18 : place.Tier == 1 ? 10 : 5;
            Cities.Add(new IslandCity(place, -1, capacity) { IsLair = rule.Lair });
        }
        var capitals = Cities.Where(c => !c.IsLair && Config.Rule(c.Place.Kind).CapitalCandidate).Take(military.Count).ToList();
        foreach (var c in Cities.Where(c => c.IsCity).OrderByDescending(c => c.Place.Tier)) { if (capitals.Count >= military.Count) break; if (!capitals.Contains(c)) capitals.Add(c); }
        for (int i = 0; i < capitals.Count; i++) { capitals[i].CapitalOf = military[i].Id; SetOwner(capitals[i], military[i].Id, true); }
        int quota = Math.Max(1, Cities.Count(c => c.IsCity) / Math.Max(1, military.Count));
        var count = military.ToDictionary(f => f.Id, f => 1);
        // Each remaining city goes to the nearest capital that still has room (four cities per faction).
        foreach (var city in Cities.Where(c => c.IsCity && c.Owner < 0).OrderBy(c => capitals.Min(k => Distance(k.Place, c.Place))))
        {
            var cap = capitals.Where(k => count[k.CapitalOf] < quota).OrderBy(k => Distance(k.Place, city.Place)).FirstOrDefault() ?? capitals.OrderBy(k => Distance(k.Place, city.Place)).First();
            SetOwner(city, cap.CapitalOf, true); count[cap.CapitalOf]++;
        }
        foreach (var site in Cities.Where(c => c.Owner < 0))
            SetOwner(site, site.IsLair && HasMonsters ? Monsters.Id : Cities.Where(c => c.IsCity).OrderBy(c => Distance(c.Place, site.Place)).First().Owner, true);
        FairPlace = Cities.Where(c => c.IsCity).OrderBy(c => c.Place.X * c.Place.X + c.Place.Z * c.Place.Z).First().Place.Id;
        foreach (var city in Cities) CreateOutposts(city, B.OutpostGarrison);
    }

    /// <summary>Moves a city's posts onto spots chosen by the world generator (flat, levelled ground); extra posts are dropped.</summary>
    public void PlaceOutposts(int placeId, IReadOnlyList<(double X, double Z)> spots)
    {
        var city = Cities.FirstOrDefault(c => c.Place.Id == placeId); if (city == null || spots == null || spots.Count == 0) return;
        for (int k = 0; k < city.Outposts.Count && k < spots.Count; k++) { city.Outposts[k].X = spots[k].X; city.Outposts[k].Z = spots[k].Z; }
        if (spots.Count < city.Outposts.Count) city.Outposts.RemoveRange(spots.Count, city.Outposts.Count - spots.Count);
    }

    // Evenly spaced on a ring; the rotation comes from the place id so it never consumes the random stream.
    void CreateOutposts(IslandCity city, double garrison)
    {
        city.Outposts.Clear();
        if (!city.IsCity || city.IsLair) return;
        double turn = city.Place.Id * 2.399963;
        for (int i = 0; i < B.OutpostsPerCity; i++)
        {
            double a = turn + i * 2 * Math.PI / B.OutpostsPerCity;
            city.Outposts.Add(new IslandOutpost(i, city.Place.X + Math.Cos(a) * B.OutpostRadius, city.Place.Z + Math.Sin(a) * B.OutpostRadius, city.Owner, garrison));
        }
    }

    void SetOwner(IslandCity city, int owner, bool initial = false)
    {
        if (!initial) city.PreviousOwner = city.Owner;
        city.Owner = owner;
        foreach (var post in city.Outposts) { post.Owner = owner; post.Garrison = B.OutpostGarrison * .5; } // the new owner holds the ring, thinly
        if (initial) city.OriginalOwner = owner;
    }

    IslandPerson Born(IslandCity home, int faction)
    {
        var profile = Factions[faction].Profile;
        bool female = !profile.Monsters && _random.NextDouble() < .45;
        string name = profile.Monsters ? "Morto-vivo" : _names.Person(NameForge.CultureFor(profile), female, ref _random);
        var p = new IslandPerson(_ids.Next("person"), name, faction, home.Place.Id)
        { Female = female, Combat = (profile.Monsters ? 25 : profile.Nomadic ? 18 : 22) + _random.NextDouble() * 15 };
        return p;
    }

    // Leader at the capital, two generals and six captains spread over the faction's cities.
    void AppointOfficers()
    {
        foreach (var faction in Factions.Where(f => !f.Profile.Monsters))
        {
            var homes = Cities.Where(c => c.Owner == faction.Id).ToList();
            var capital = homes.FirstOrDefault(c => c.CapitalOf == faction.Id) ?? homes.FirstOrDefault();
            if (faction.Profile.Nomadic) { capital = Cities.First(c => c.Place.Id == FairPlace); }
            if (capital is null) continue;
            var leader = MakeOfficer(capital, faction.Id, Rank.Leader); leader.Title = "";
            leader.Name = faction.Profile.LeaderName;
            if (faction.Profile.Nomadic) for (int g = 0; g < B.NomadGuards; g++) capital.Residents.Add(Born(capital, faction.Id)); // caravan guards
            for (int g = 0; g < B.Generals; g++) MakeOfficer(homes.Count > 0 ? homes[(g + 1) % homes.Count] : capital, faction.Id, Rank.General);
            for (int c = 0; c < (faction.Profile.Nomadic ? B.NomadCaptains : B.Captains); c++) MakeOfficer(homes.Count > 0 ? homes[c % homes.Count] : capital, faction.Id, Rank.Captain);
        }
    }

    IslandPerson MakeOfficer(IslandCity home, int faction, Rank rank)
    {
        var p = Born(home, faction); Promote(p, rank, quiet: true); home.Residents.Add(p); return p;
    }

    void Promote(IslandPerson p, Rank rank, bool quiet = false)
    {
        p.Rank = rank;
        if (p.Traits.Count == 0)
        {
            p.Traits.Add(TraitPool[_random.Next(TraitPool.Length)]);
            p.Strength_ = StrengthPool[_random.Next(StrengthPool.Length)]; p.Weakness = WeaknessPool[_random.Next(WeaknessPool.Length)];
            p.Title = rank == Rank.Leader ? "" : _names.Title(p.Female, ref _random);
            p.Ambition = p.Traits.Contains("Ambicioso") ? .8 : .2 + _random.NextDouble() * .4;
            p.Loyalty = p.Traits.Contains("Leal") ? .95 : p.Traits.Contains("Ambicioso") ? .35 : .5 + _random.NextDouble() * .4;
        }
        p.Combat = Math.Max(p.Combat, rank switch { Rank.Leader => 60, Rank.General => 50, Rank.Captain => 40, _ => p.Combat });
        if (!quiet) Raise("promotion", $"{p.DisplayName} foi promovido(a) a {RankLabel(rank)} de {Factions[p.Faction].Profile.Name}.", 0, 0, a: p.Faction, subject: p.DisplayName);
    }

    // ---------- main loop ----------
    public void Step(double hours)
    {
        if (!(hours > 0) || double.IsInfinity(hours)) throw new ArgumentOutOfRangeException(nameof(hours));
        Hours += hours;
        foreach (var c in Cities) { c.Now = Hours; c.SubjugatedPower = B.SubjugatedPower; }
        foreach (var squad in _squads.Where(s => !s.Done && !s.ControlledExternally).OrderBy(s => s.Id).ToList()) Travel(squad, hours);
        Encounters();
        foreach (var c in Cities) Upkeep(c, hours);
        _cycleClock += hours;
        while (_cycleClock >= CycleHours) { _cycleClock -= CycleHours; Cycle(); }
        _dayClock += hours;
        while (_dayClock >= 24) { _dayClock -= 24; Daily(); }
        _squads.RemoveAll(s => s.Done);
    }

    void Cycle()
    {
        for (int k = 0; k < 3 && _squads.Count < MaxSquads; k++)
        {
            var faction = Factions[_random.Next(Factions.Count)];
            if (faction.Extinct || _random.NextDouble() > .45 * Math.Max(.3, faction.Morale)) continue;
            if (_squads.Count(s => !s.Done && s.Faction == faction.Id) >= (faction.Profile.Monsters ? B.MonsterSquads : B.SquadsPerFaction)) continue;
            TryMission(faction);
        }
    }

    void TryMission(IslandFaction faction)
    {
        var homes = Cities.Where(c => c.Owner == faction.Id && (faction.Profile.Monsters ? c.IsLair : true)).ToList();
        if (faction.Profile.Nomadic) homes = Cities.Where(c => c.Place.Id == FairPlace).ToList();
        if (faction.Exiled) { Reconquest(faction); return; }
        IslandCity? home = homes.Count == 0 ? null : homes[_random.Next(homes.Count)];
        if (home is null) return;
        var ready = home.Residents.Where(r => r.Alive && !r.Wounded && r.Faction == faction.Id).ToList();
        if (ready.Count < 3 || (faction.Profile.Monsters && Hours < home.NextRaid)) return;
        IslandMission kind; IslandCity? target = null; IslandSquad? prey = null; double cargo = 0;
        if (faction.Profile.Nomadic)
        {   // Buy where food is plenty, sell where it is scarce, never in a place at war with the nomads.
            var seller = Cities.Where(c => c.IsCity && c.Food > 35 && !AtWar(faction.Id, c.Owner) && !Embargoed(c.Owner)).OrderByDescending(c => c.Food).FirstOrDefault();
            target = Cities.Where(c => c.IsCity && c != seller && c.Food < 30 && !AtWar(faction.Id, c.Owner) && !Embargoed(c.Owner)).OrderBy(c => c.Food).FirstOrDefault();
            if (seller != null && target != null && seller.Food - target.Food < 15) target = null; // only where prices differ
            if (seller != null && target != null) { double bought = Math.Min(30, seller.Food * .3); seller.Food -= bought; Factions[seller.Owner].Treasury += bought * 2; cargo = bought; }
            else
            {   // No famine anywhere: carry goods (tools, cloth, news) to a peaceful city.
                var markets = Cities.Where(c => c.IsCity && c.Place.Id != FairPlace && c.Owner >= 0 && !AtWar(faction.Id, c.Owner) && !Embargoed(c.Owner)).ToList();
                if (markets.Count == 0) return;
                target = markets[_random.Next(markets.Count)];
            }
            kind = IslandMission.Trade;
        }
        else if (faction.Profile.Monsters)
        {
            kind = IslandMission.Raid; home.NextRaid = Hours + B.MonsterRaidCooldownHours;
            target = Cities.Where(c => c.IsCity && AtWar(faction.Id, c.Owner)).OrderBy(c => Distance(c.Place, home.Place)).FirstOrDefault();
        }
        else
        {
            // Intercept a hostile squad near home first; otherwise follow the faction's goals.
            prey = _squads.Where(s => !s.Done && s.Alive > 0 && AtWar(faction.Id, s.Faction) && Distance(home.Place, s.X, s.Z) < 3000).OrderBy(s => Distance(home.Place, s.X, s.Z)).FirstOrDefault();
            var enemies = Cities.Where(c => c.Owner >= 0 && !c.IsLair && AtWar(faction.Id, c.Owner) && !Factions[c.Owner].Profile.Nomadic && !Factions[c.Owner].Profile.Monsters).ToList();
            int garrison = home.Residents.Count(r => r.Alive && r.Faction == faction.Id);
            if (garrison - 4 < Math.Floor(home.Capacity * B.GarrisonShare)) enemies.Clear(); // keep a garrison at home before marching
            if (prey is not null) { kind = IslandMission.Hunt; target = home; }
            else if (enemies.Count > 0 && _random.NextDouble() < B.RaidChance * faction.Morale)
            {
                kind = IslandMission.Raid;
                // Score: close, weakly defended, matching the faction's preferred targets, sworn enemy first.
                target = enemies.OrderByDescending(c =>
                    -Distance(home.Place, c.Place) / 1000 - c.Defence / 4
                    + (faction.Profile.PreferredTargets.Contains(c.Place.Kind) ? 2 : 0)
                    + (c.Owner == faction.Profile.SwornEnemy ? 2 : 0)
                    + (c.CapitalOf >= 0 ? -1.5 : 0)).FirstOrDefault();
            }
            else if ((faction.Profile.PreferredTargets.Contains("Nest") || _random.NextDouble() < .25)
                && HasMonsters && Cities.FirstOrDefault(c => c.IsLair && c.Owner == MonsterId && c.Residents.Count(r => r.Alive) >= 3 && c.HuntedUntil < Hours) is IslandCity lair)
            { kind = IslandMission.Hunt; target = lair; lair.HuntedUntil = Hours + B.LairHuntCooldownHours; } // one hunt per lair per day
            else { kind = IslandMission.Patrol; var own = Cities.Where(c => c.Owner == faction.Id && c != home).ToList(); target = own.Count > 0 ? own[_random.Next(own.Count)] : null; }
        }
        if (target is null) return;
        Launch(faction, home, target, kind, ready, prey, cargo);
    }

    void Launch(IslandFaction faction, IslandCity home, IslandCity target, IslandMission kind, List<IslandPerson> ready, IslandSquad? prey, double cargo)
    {
        var squad = new IslandSquad(++_squadIds, faction.Id, kind, home.Place.Id, target.Place.Id) { X = home.Place.X, Z = home.Place.Z, Prey = prey, Cargo = cargo };
        if (prey is null) squad.Path.AddRange(ShortestPath(home.Place.Id, target.Place.Id));
        int size = Math.Min(ready.Count, 3 + _random.Next(kind == IslandMission.Raid ? 5 : 3));
        // A captain leads most squads; raids on big targets may bring a general.
        var picked = ready.Where(p => !p.Officer).OrderByDescending(p => p.Combat).Take(size - 1).ToList();
        var officer = ready.Where(p => p.Officer && p.Rank != Rank.Leader && (p.Rank == Rank.Captain || kind == IslandMission.Raid)).OrderByDescending(p => p.Rank).FirstOrDefault();
        if (officer != null) picked.Add(officer); else picked.AddRange(ready.Where(p => !p.Officer && !picked.Contains(p)).Take(1));
        foreach (var p in picked) { squad.Members.Add(p); home.Residents.Remove(p); }
        _squads.Add(squad);
        string who = squad.Leader != null ? $"{squad.Leader.DisplayName} lidera" : "Um grupo de";
        Raise(kind == IslandMission.Raid ? "march" : "mission", kind switch
        {
            IslandMission.Raid => $"{who} {Factions[faction.Id].Profile.Name} rumo a {target.Place.Name} ({Factions[target.Owner].Profile.Name}).",
            IslandMission.Trade => $"Caravana nômade leva {cargo:0} de comida para {target.Place.Name}.",
            IslandMission.Hunt when prey != null => $"{Factions[faction.Id].Profile.Name} saem para interceptar {Factions[prey.Faction].Profile.Name}.",
            IslandMission.Hunt => $"{Factions[faction.Id].Profile.Name} caçam mortos-vivos em {target.Place.Name}.",
            _ => $"{Factions[faction.Id].Profile.Name} patrulham rumo a {target.Place.Name}.",
        }, squad.X, squad.Z, kind is IslandMission.Patrol or IslandMission.Trade ? 0 : 1);
    }

    void Reconquest(IslandFaction faction)
    {
        var exiles = _squads.FirstOrDefault(s => !s.Done && s.Faction == faction.Id);
        if (exiles != null) return;
        faction.Exiled = false; faction.Extinct = true; faction.ExtinctSince = Hours;
        Raise("extinct", $"{faction.Profile.Name} deixou de existir.", 0, 0, a: faction.Id);
    }

    // ---------- movement & combat ----------
    static double Distance(WorldPlace a, WorldPlace b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
    static double Distance(WorldPlace a, double x, double z) => Math.Sqrt((a.X - x) * (a.X - x) + (a.Z - z) * (a.Z - z));
    WorldPlace Place(int id) => _places.First(p => p.Id == id);
    IslandCity City(int id) => Cities.First(c => c.Place.Id == id);
    public WorldPlace NearestPlace(double x, double z) => _places.OrderBy(p => (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z)).First();

    List<int> ShortestPath(int from, int to)
    {
        if (from == to) return new List<int> { to };
        var dist = _places.ToDictionary(p => p.Id, _ => double.MaxValue); var prev = new Dictionary<int, int>(); var open = new List<int> { from };
        dist[from] = 0;
        while (open.Count > 0)
        {
            int id = open.OrderBy(i => dist[i]).First(); open.Remove(id);
            if (id == to) break;
            foreach (var r in _routes)
            {
                if (r.From != id && r.To != id) continue;
                int next = r.From == id ? r.To : r.From; double nd = dist[id] + r.Length;
                if (!dist.ContainsKey(next) || nd >= dist[next]) continue;
                dist[next] = nd; prev[next] = id; if (!open.Contains(next)) open.Add(next);
            }
        }
        if (!prev.ContainsKey(to)) return new List<int> { to };
        var path = new List<int>(); for (int c = to; c != from; c = prev[c]) path.Add(c);
        path.Reverse(); return path;
    }

    void Travel(IslandSquad squad, double hours)
    {
        double metres = SquadSpeedKmh * 1000 * hours;
        if (squad.Prey is not null && !squad.Returning)
        {
            var prey = squad.Prey;
            if (prey.Done || prey.Alive == 0 || Distance(Place(squad.Origin), prey.X, prey.Z) > 4500) { squad.Prey = null; GoHome(squad); return; }
            double dx = prey.X - squad.X, dz = prey.Z - squad.Z, left = Math.Sqrt(dx * dx + dz * dz), move = Math.Min(left, metres * 1.2);
            if (left > 1) { squad.X += dx / left * move; squad.Z += dz / left * move; }
            return;
        }
        if (Factions[squad.Faction].Profile.Nomadic && !squad.Returning) TryRobCaravan(squad, hours);
        while (metres > 0 && squad.Path.Count > 0)
        {
            var next = Place(squad.Path[0]);
            double dx = next.X - squad.X, dz = next.Z - squad.Z, left = Math.Sqrt(dx * dx + dz * dz);
            if (left <= metres) { squad.X = next.X; squad.Z = next.Z; metres -= left; squad.Path.RemoveAt(0); }
            else { squad.X += dx / left * metres; squad.Z += dz / left * metres; metres = 0; }
        }
        if (squad.Path.Count == 0) Arrive(squad);
    }

    void Arrive(IslandSquad squad)
    {
        var city = City(squad.Returning ? squad.Origin : squad.Target);
        if (squad.Returning) { Disband(squad, city); return; }
        var faction = Factions[squad.Faction];
        switch (squad.Mission)
        {
            case IslandMission.Raid when AtWar(squad.Faction, city.Owner):
                Siege(squad, city);
                if (squad.Done) return;
                break;
            case IslandMission.Reconquest when city.Owner != squad.Faction:
                Siege(squad, city);
                if (Cities.Any(c => c.Owner == squad.Faction && c.IsCity)) { faction.Exiled = false; Raise("return", $"{faction.Profile.Name} retomou {city.Place.Name} e renasce!", city.Place.X, city.Place.Z, a: faction.Id, place: city.Place.Id); }
                if (squad.Done) return;
                break;
            case IslandMission.Hunt when HasMonsters && city.Owner == MonsterId:
                int culled = 0; Train(squad.Members, 2);
                foreach (var r in city.Residents.Where(r => r.Alive).Take(1 + (int)(squad.Strength / 1.2))) { Wound(r, .6 + _random.NextDouble() * .6, squad.Leader); if (!r.Alive) culled++; }
                foreach (var m in squad.Members.Where(m => m.Alive).Take(_random.Next(2))) Wound(m, .2 + _random.NextDouble() * .4, null);
                Raise("hunt", $"{faction.Profile.Name} abateram {culled} mortos-vivos em {city.Place.Name}.", city.Place.X, city.Place.Z, a: faction.Id, place: city.Place.Id);
                break;
            case IslandMission.Trade when squad.Cargo <= 0 && city.Owner >= 0:
                double profit = 25 * faction.Rules.Trade; faction.Treasury += profit; Factions[city.Owner].Treasury += profit * .6;
                if (city.Owner != faction.Id) ChangeRelation(faction.Id, city.Owner, .01);
                Raise("trade", $"Nômades venderam mercadorias em {city.Place.Name}.", city.Place.X, city.Place.Z, 0, place: city.Place.Id);
                break;
            case IslandMission.Trade when squad.Cargo > 0:
                city.Food += squad.Cargo; double price = squad.Cargo * 3 * faction.Rules.Trade;
                if (city.Owner >= 0) Factions[city.Owner].Treasury -= price; faction.Treasury += price; squad.Cargo = 0;
                if (city.Owner >= 0 && city.Owner != faction.Id) ChangeRelation(faction.Id, city.Owner, .02);
                Raise("trade", $"Nômades venderam comida em {city.Place.Name}.", city.Place.X, city.Place.Z, 0, place: city.Place.Id);
                break;
        }
        GoHome(squad);
    }

    // Skirmish for one post; the city itself is untouched until its whole ring is lost.
    void AttackOutpost(IslandSquad squad, IslandCity city, IslandOutpost post)
    {
        var attacker = Factions[squad.Faction]; var defender = Factions[post.Owner];
        double defence = post.Garrison * (.7 + _random.NextDouble() * .6) * Math.Max(.4, defender.Morale);
        double attack = squad.Strength * (.7 + _random.NextDouble() * .6) * Math.Max(.4, attacker.Morale);
        Train(squad.Members, 1);
        int left = city.Outposts.Count(o => o.Owner == defender.Id);
        if (attack <= defence)
        {
            foreach (var m in squad.Members.Where(m => m.Alive).Take(1 + _random.Next(2))) Wound(m, .2 + _random.NextDouble() * .4, null);
            attacker.WarWeariness += .25; post.Garrison = Math.Max(.5, post.Garrison - .3);
            Raise("outpost", $"Um posto de {defender.Profile.Name} perto de {city.Place.Name} repeliu {attacker.Profile.Name}.", post.X, post.Z, 0, a: attacker.Id, b: defender.Id, place: city.Place.Id);
            return;
        }
        foreach (var m in squad.Members.Where(m => m.Alive).Take(_random.Next(2))) Wound(m, .1 + _random.NextDouble() * .3, null);
        post.Owner = attacker.Id; post.Garrison = B.OutpostGarrison * .5;
        Raise("outpost", left > 1 ? $"{attacker.Profile.Name} tomaram um posto perto de {city.Place.Name} (faltam {left - 1})." : $"{attacker.Profile.Name} tomaram o último posto de {city.Place.Name}: a cidade está exposta.",
            post.X, post.Z, left > 1 ? 1 : 2, a: attacker.Id, b: defender.Id, place: city.Place.Id);
    }

    // Owners refill their posts; enemy-held posts are lost on peace or may be retaken by a strong garrison.
    void TendOutposts()
    {
        foreach (var city in Cities)
            foreach (var post in city.Outposts)
            {
                if (post.Owner == city.Owner) { post.Garrison = Math.Min(B.OutpostGarrison, post.Garrison + B.OutpostRegenPerDay); continue; }
                if (city.Owner < 0 || Factions[post.Owner].Extinct || !AtWar(post.Owner, city.Owner))
                { post.Owner = city.Owner; post.Garrison = B.OutpostGarrison * .5; continue; }
                if (city.Defence > post.Garrison * 3 && _random.NextDouble() < B.OutpostRetakeChance)
                {
                    int lost = post.Owner; post.Owner = city.Owner; post.Garrison = B.OutpostGarrison * .5;
                    Raise("outpost", $"{Factions[city.Owner].Profile.Name} retomaram um posto perto de {city.Place.Name}.", post.X, post.Z, 0, a: city.Owner, b: lost, place: city.Place.Id);
                }
            }
    }

    // Battle for a settlement; on victory the attacker applies its conquest policy.
    void Siege(IslandSquad squad, IslandCity city)
    {
        var attacker = Factions[squad.Faction]; int defenderId = city.Owner; var defender = Factions[defenderId];
        if (city.IsLair && !attacker.Profile.Monsters) return; // lairs are hunted, never held
        // A city falls in stages: its posts first. Exiles (insiders) and the dead (raiders) go straight in.
        if (squad.Mission == IslandMission.Raid && !attacker.Profile.Monsters
            && city.Outposts.Where(o => o.Owner == defenderId).OrderBy(o => (o.X - squad.X) * (o.X - squad.X) + (o.Z - squad.Z) * (o.Z - squad.Z)).FirstOrDefault() is IslandOutpost post)
        { AttackOutpost(squad, city, post); return; }
        double defence = city.Defence * (.7 + _random.NextDouble() * .6) * Math.Max(.4, defender.Morale);
        double attack = squad.Strength * (.7 + _random.NextDouble() * .6) * Math.Max(.4, attacker.Morale);
        Train(squad.Members, 3); Train(city.Residents, 3);
        if (attack <= defence)
        {
            foreach (var m in squad.Members.Where(m => m.Alive)) Wound(m, .3 + _random.NextDouble() * .5, city.Residents.FirstOrDefault(r => r.Alive));
            foreach (var r in city.Residents.Where(r => r.Alive).Take(_random.Next(3))) Wound(r, .4 + _random.NextDouble() * .7, squad.Leader);
            attacker.WarWeariness += .5; defender.Morale = Math.Min(1.3, defender.Morale + .05);
            Raise("siege", $"{city.Place.Name} resistiu ao ataque de {attacker.Profile.Name}.", city.Place.X, city.Place.Z, a: attacker.Id, b: city.Owner, place: city.Place.Id);
            return;
        }
        var killer = squad.Leader ?? squad.Members.FirstOrDefault(m => m.Alive);
        foreach (var r in city.Residents.Where(r => r.Alive && r.Faction == defenderId && (r.Officer || _random.NextDouble() < .5))) Wound(r, .7 + _random.NextDouble() * .6, killer);
        if (attacker.Profile.Monsters)
        {   // The dead kill and plunder but never hold a place.
            city.Food *= .6; city.Unrest += .2;
            Raise("massacre", $"Mortos-vivos devastaram {city.Place.Name}.", city.Place.X, city.Place.Z, place: city.Place.Id);
            return;
        }
        bool wasCapital = city.CapitalOf == defenderId;
        var policy = attacker.Profile.Conquest;
        if (policy == ConquestPolicy.Liberate && city.PreviousOwner != defenderId && city.PreviousOwner >= 0 && !Factions[city.PreviousOwner].Extinct && city.PreviousOwner != squad.Faction)
        {
            SetOwner(city, city.PreviousOwner);
            Raise("liberation", $"{attacker.Profile.Name} libertaram {city.Place.Name}; volta para {Factions[city.Owner].Profile.Name}.", city.Place.X, city.Place.Z, a: attacker.Id, b: city.Owner, place: city.Place.Id);
        }
        else
        {
            SetOwner(city, squad.Faction);
            foreach (var r in city.Residents.Where(r => r.Alive && r.Faction == defenderId).ToList())
            {
                switch (policy)
                {
                    case ConquestPolicy.Execute: r.Health = 0; break;
                    case ConquestPolicy.Convert: r.Faction = squad.Faction; r.Rank = Rank.Soldier; r.SubjugatedUntil = Hours + 24 * B.SubjugationDays; break;
                    case ConquestPolicy.Integrate: r.Faction = squad.Faction; r.Rank = Rank.Soldier; r.SubjugatedUntil = Hours + 24 * B.SubjugationDays; break;
                    default: r.Faction = squad.Faction; r.SubjugatedUntil = Hours + 24 * B.SerfdomDays; city.Unrest += .05; break; // exploited serfs
                }
            }
            city.Residents.RemoveAll(r => !r.Alive);
            city.Unrest = Math.Min(1.5, city.Unrest + (policy == ConquestPolicy.Exploit ? .5 : .25));
            foreach (var m in squad.Members.Where(m => m.Alive)) { m.Home = city.Place.Id; city.Residents.Add(m); }
            squad.Done = true;
            Raise("capture", $"{attacker.Profile.Name} tomaram {city.Place.Name} de {defender.Profile.Name} ({PolicyLabel(policy)}).", city.Place.X, city.Place.Z, a: attacker.Id, b: defender.Id, place: city.Place.Id);
        }
        ChangeRelation(squad.Faction, defenderId, -.25);
        defender.Morale = Math.Max(.2, defender.Morale - (wasCapital ? .5 : .15)); attacker.Morale = Math.Min(1.3, attacker.Morale + .1);
        if (wasCapital) CapitalFell(defender, city);
        CheckExtinction(defender, attacker);
    }

    void CapitalFell(IslandFaction faction, IslandCity lost)
    {
        lost.CapitalOf = -1;
        var next = Cities.Where(c => c.Owner == faction.Id && c.IsCity).OrderByDescending(c => c.Residents.Count).FirstOrDefault();
        if (next != null) next.CapitalOf = faction.Id;
        Raise("crisis", $"A capital de {faction.Profile.Name} caiu! {(next != null ? $"Nova capital: {next.Place.Name}." : "Sem capital.")}", lost.Place.X, lost.Place.Z, a: faction.Id, place: lost.Place.Id);
        // Vassalage: a faction reduced to one city submits to a strong neighbour that exploits or rules by hierarchy.
        int left = Cities.Count(c => c.Owner == faction.Id && c.IsCity);
        if (left == 1 && faction.VassalOf is null)
        {
            var lord = Factions.Where(f => f.Id != faction.Id && AtWar(f.Id, faction.Id) && !f.Profile.Monsters && f.Profile.Conquest is ConquestPolicy.Exploit or ConquestPolicy.Integrate).OrderByDescending(f => f.Power).FirstOrDefault();
            if (lord != null) { faction.VassalOf = lord.Id; SetWar(faction.Id, lord.Id, false); _relations[faction.Id, lord.Id] = _relations[lord.Id, faction.Id] = .3; Raise("vassal", $"{faction.Profile.Name} jurou vassalagem a {lord.Profile.Name}.", 0, 0, a: faction.Id, b: lord.Id); }
        }
    }

    void CheckExtinction(IslandFaction faction, IslandFaction conqueror)
    {
        if (faction.Profile.Monsters || faction.Profile.Nomadic || Cities.Any(c => c.Owner == faction.Id && c.IsCity)) return;
        // Officers may swear to the conqueror (absorption); the rest become exiles trying to retake a city.
        foreach (var o in AllPeople().Where(p => p.Alive && p.Faction == faction.Id && p.Officer).ToList())
            if (o.Loyalty < .5 && !o.Traits.Contains("Honrado") && _random.NextDouble() < .6) Defect(o, conqueror.Id);
        var survivors = AllPeople().Where(p => p.Alive && p.Faction == faction.Id).ToList();
        foreach (var site in Cities.Where(c => c.Owner == faction.Id)) SetOwner(site, conqueror.Id);
        if (survivors.Count < 3) { faction.Extinct = true; faction.ExtinctSince = Hours; Raise("extinct", $"{faction.Profile.Name} foi extinta por {conqueror.Profile.Name}.", 0, 0, a: faction.Id, b: conqueror.Id); return; }
        faction.Exiled = true;
        var target = Cities.Where(c => c.IsCity && c.OriginalOwner == faction.Id).OrderBy(c => c.Defence).First();
        var exiles = new IslandSquad(++_squadIds, faction.Id, IslandMission.Reconquest, target.Place.Id, target.Place.Id) { X = survivors[0].Home >= 0 ? Place(survivors[0].Home).X : 0, Z = survivors[0].Home >= 0 ? Place(survivors[0].Home).Z : 0 };
        foreach (var s in survivors) { foreach (var c in Cities) c.Residents.Remove(s); foreach (var q in _squads) q.Members.Remove(s); exiles.Members.Add(s); }
        exiles.Path.Add(target.Place.Id); _squads.Add(exiles);
        Raise("exile", $"{faction.Profile.Name} perdeu todas as cidades; exilados marcham para retomar {target.Place.Name}.", exiles.X, exiles.Z, a: faction.Id, place: target.Place.Id);
    }

    void GoHome(IslandSquad squad)
    {
        squad.Returning = true;
        var origin = City(squad.Origin);
        if (Factions[squad.Faction].Profile.Nomadic) { squad.Origin = FairPlace; origin = City(FairPlace); }
        if (origin.Owner != squad.Faction && !Factions[squad.Faction].Profile.Nomadic)
        {
            var refuge = Cities.Where(c => c.Owner == squad.Faction).OrderBy(c => Distance(c.Place, squad.X, squad.Z)).FirstOrDefault();
            if (refuge is null) { if (squad.Mission != IslandMission.Reconquest) squad.Done = true; return; }
            squad.Origin = refuge.Place.Id;
        }
        var current = NearestPlace(squad.X, squad.Z);
        squad.Path.Clear(); squad.Path.AddRange(ShortestPath(current.Id, squad.Origin));
    }

    void Disband(IslandSquad squad, IslandCity home)
    {
        home.Food += squad.Cargo; squad.Cargo = 0;
        foreach (var m in squad.Members.Where(m => m.Alive)) if (home.Residents.Count < home.Capacity * 1.3 || m.Officer) { m.Home = home.Place.Id; home.Residents.Add(m); }
        squad.Done = true;
    }

    void Encounters()
    {
        var active = _squads.Where(s => !s.Done && !s.ControlledExternally && s.Alive > 0).OrderBy(s => s.Id).ToList();
        for (int i = 0; i < active.Count; i++) for (int j = i + 1; j < active.Count; j++)
        {
            IslandSquad a = active[i], b = active[j];
            if (a.Done || b.Done) continue;
            bool hostile = AtWar(a.Faction, b.Faction) || (b.Mission == IslandMission.Trade && RaidsNomads(a.Faction)) || (a.Mission == IslandMission.Trade && RaidsNomads(b.Faction));
            if (!hostile) continue;
            double dx = a.X - b.X, dz = a.Z - b.Z;
            if (dx * dx + dz * dz > EncounterMetres * EncounterMetres) continue;
            Fight(a, b);
        }
    }

    // Aggressive regimes sometimes rob nomad caravans when relations are poor.
    bool RaidsNomads(int faction) => HasNomads && Factions[faction].Profile.Ideology is Ideology.Authoritarian or Ideology.Feudalism && Relation(faction, Nomads.Id) < .2;

    /// <summary>Resolves a fight between two squads (off-screen, or reported by the engine).</summary>
    public void Fight(IslandSquad a, IslandSquad b)
    {
        int startA = a.Alive, startB = b.Alive; double holdA = Nerve(a), holdB = Nerve(b);
        for (int round = 0; round < 6 && a.Alive > 0 && b.Alive > 0; round++)
        {
            Volley(a, b); Volley(b, a);
            if (a.Alive < startA * holdA || b.Alive < startB * holdB) break; // morale breaks: a coward leader pulls out early, a brave one holds
        }
        var (winner, loser) = a.Strength >= b.Strength ? (a, b) : (b, a);
        int dead = loser.Members.Count(m => !m.Alive);
        foreach (var m in winner.Members.Where(m => m.Alive)) m.Gear = Math.Min(1, m.Gear + .03);
        if (loser.Cargo > 0) { winner.Cargo += loser.Cargo; loser.Cargo = 0; if (Factions[loser.Faction].Profile.Nomadic) NomadsRobbed(winner.Faction); }
        ChangeRelation(a.Faction, b.Faction, -.05);
        Factions[loser.Faction].WarWeariness += dead * .2;
        if (winner.Leader is IslandPerson hero) { hero.Kills += dead; ConsiderPromotion(hero); }
        Raise("fight", $"{Factions[winner.Faction].Profile.Name}{(winner.Leader != null ? $" ({winner.Leader.DisplayName})" : "")} venceram {Factions[loser.Faction].Profile.Name} perto de {NearestPlace(a.X, a.Z).Name} ({dead} mortos).", a.X, a.Z, a: winner.Faction, b: loser.Faction, place: NearestPlace(a.X, a.Z).Id);
        if (loser.Alive == 0) loser.Done = true; else GoHome(loser);
        if (winner.Alive * 2 < winner.Members.Count) GoHome(winner);
    }

    // Share of the squad that must still stand for it to keep fighting, set by its leader's temper.
    static double Nerve(IslandSquad s) => s.Leader?.Traits is { } t ? t.Contains("Covarde") ? .75 : t.Contains("Corajoso") ? .25 : .5 : .5;

    // Caravans crossing the land of a predatory or broke regime may be held up (the nomads answer with an embargo).
    void TryRobCaravan(IslandSquad caravan, double hours)
    {
        var city = Cities.Where(c => c.IsCity && c.Owner >= 0 && Distance(c.Place, caravan.X, caravan.Z) < 1200).OrderBy(c => Distance(c.Place, caravan.X, caravan.Z)).FirstOrDefault();
        if (city is null) return;
        var robber = Factions[city.Owner];
        if (robber.Profile.Nomadic || robber.Profile.Monsters || Embargoed(robber.Id) || Relation(robber.Id, Nomads.Id) > .45) return;
        double greed = Aggression(robber.Profile.Ideology) * .8 + (robber.Treasury < 150 ? .03 : 0) + (robber.Profile.Ideology == Ideology.Capitalism && robber.Treasury < 300 ? .015 : 0);
        if (_random.NextDouble() > greed * hours) return;
        double loot = caravan.Cargo; caravan.Cargo = 0; robber.Treasury += 60 + loot * 2;
        foreach (var m in caravan.Members.Where(m => m.Alive).Take(1 + _random.Next(2))) Wound(m, .3 + _random.NextDouble() * .5, null);
        Raise("robbery", $"{robber.Profile.Name} assaltaram uma caravana nômade perto de {city.Place.Name}.", caravan.X, caravan.Z, a: robber.Id, place: city.Place.Id);
        NomadsRobbed(robber.Id); GoHome(caravan);
    }

    void NomadsRobbed(int robber)
    {
        if (!HasNomads) return;
        _embargoUntil[robber] = Hours + 24 * B.EmbargoDays;
        ChangeRelation(robber, Nomads.Id, -.3);
        foreach (var f in Factions.Where(f => f.Id != robber && !f.Profile.Monsters && !f.Profile.Nomadic)) ChangeRelation(robber, f.Id, -.05);
        Raise("embargo", $"{Factions[robber].Profile.Name} roubaram uma caravana nômade; os nômades cortam o comércio com eles.", 0, 0, a: robber);
    }

    void Volley(IslandSquad shooters, IslandSquad targets)
    {
        foreach (var s in shooters.Members.Where(m => m.Alive))
        {
            var target = targets.Members.Where(m => m.Alive).OrderBy(m => m.Health).FirstOrDefault();
            if (target is null) return;
            double hit = .25 + s.Combat / 200 - target.Combat / 400 + (s.Strength_ == "Mestre do arco" ? .1 : 0);
            if (_random.NextDouble() < hit) Wound(target, (.25 + _random.NextDouble() * .35 * (.6 + s.Gear)) * (target.Strength_ == "Pele de ferro" ? .7 : 1), s);
            s.Combat = Math.Min(100, s.Combat + .4 * (1 - s.Combat / 100));
        }
    }

    void Train(IEnumerable<IslandPerson> people, double amount) { foreach (var p in people.Where(p => p.Alive)) p.Combat = Math.Min(100, p.Combat + amount * (1 - p.Combat / 100)); }

    void Wound(IslandPerson victim, double damage, IslandPerson? attacker)
    {
        if (!victim.Alive) return;
        victim.Health = Math.Max(0, victim.Health - damage);
        if (victim.Alive) return;
        if (attacker != null && !Factions[attacker.Faction].Profile.Monsters) { attacker.Kills++; if (victim.Officer) attacker.Bounty += 200; }
        if (victim.Officer) OfficerDied(victim, attacker);
    }

    // ---------- Nemesis ----------
    void OfficerDied(IslandPerson officer, IslandPerson? killer)
    {
        var faction = Factions[officer.Faction];
        Raise("death", $"{officer.DisplayName}, {RankLabel(officer.Rank)} de {faction.Profile.Name}, caiu{(killer != null ? $" diante de {killer.DisplayName}" : "")}.", 0, 0, a: faction.Id, subject: officer.DisplayName);
        if (officer.Rank == Rank.Leader) Succession(faction);
        else if (officer.Rank == Rank.General) FillRank(faction, Rank.General);
        else FillRank(faction, Rank.Captain);
    }

    // The strongest general takes the lead; an ambitious rival may challenge to a duel.
    void Succession(IslandFaction faction)
    {
        faction.Morale = Math.Max(.2, faction.Morale - .3);
        var generals = Officers(faction.Id).Where(o => o.Rank == Rank.General).OrderByDescending(o => o.Power).ToList();
        if (generals.Count == 0) { FillRank(faction, Rank.General); generals = Officers(faction.Id).Where(o => o.Rank == Rank.General).ToList(); }
        if (generals.Count == 0) return;
        var heir = generals[0];
        if (generals.Count > 1 && generals[1].Ambition > .6) heir = Duel(generals[0], generals[1]);
        Promote(heir, Rank.Leader);
        Raise("succession", $"{heir.DisplayName} é o novo líder de {faction.Profile.Name}.", 0, 0, a: faction.Id, subject: heir.DisplayName);
        FillRank(faction, Rank.General);
    }

    IslandPerson Duel(IslandPerson a, IslandPerson b)
    {
        var (winner, loser) = a.Power * (.7 + _random.NextDouble() * .6) >= b.Power * (.7 + _random.NextDouble() * .6) ? (a, b) : (b, a);
        loser.Health = 0; winner.Kills++;
        Raise("duel", $"Duelo em {Factions[a.Faction].Profile.Name}: {winner.DisplayName} matou {loser.DisplayName}.", 0, 0, a: a.Faction, subject: winner.DisplayName);
        return winner;
    }

    void FillRank(IslandFaction faction, Rank rank)
    {
        int wanted = rank == Rank.General ? B.Generals : faction.Profile.Nomadic ? B.NomadCaptains : B.Captains;
        int have = Officers(faction.Id).Count(o => o.Rank == rank);
        for (int i = have; i < wanted; i++)
        {
            var below = AllPeople().Where(p => p.Alive && p.Faction == faction.Id && p.Rank == rank - 1).OrderByDescending(p => p.Kills * 5 + p.Combat).FirstOrDefault();
            if (below is null) return;
            Promote(below, rank);
        }
    }

    void ConsiderPromotion(IslandPerson hero)
    {
        if (hero.Rank == Rank.Captain && hero.Kills >= B.PromotionKills && Officers(hero.Faction).Count(o => o.Rank == Rank.General) < B.Generals) Promote(hero, Rank.General);
    }

    void Defect(IslandPerson officer, int to, bool quiet = false)
    {
        int from = officer.Faction; officer.Faction = to; officer.Loyalty = .5; if (officer.Rank > Rank.Captain) officer.Rank = Rank.Captain;
        if (!quiet) Raise("betrayal", $"{officer.DisplayName} trocou {Factions[from].Profile.Name} por {Factions[to].Profile.Name}.", 0, 0, a: from, b: to, subject: officer.DisplayName);
        FillRank(Factions[from], Rank.Captain);
    }

    /// <summary>Records a fight between the player and an officer; the officer remembers (Nemesis).</summary>
    public void RecordPlayerEncounter(IslandPerson officer, bool officerWon)
    {
        if (officerWon)
        {
            officer.Memory.Add($"Derrotou o jogador (dia {Hours / 24:0})"); officer.Kills++;
            if (officer.Rank == Rank.Captain && officer.Memory.Count(m => m.StartsWith("Derrotou")) >= 2) Promote(officer, Rank.General);
            Raise("nemesis", $"{officer.DisplayName} se gaba de ter derrotado você.", 0, 0, a: officer.Faction, subject: officer.DisplayName);
        }
        else if (officer.Alive)
        {
            officer.Memory.Add($"Fugiu ferido do jogador (dia {Hours / 24:0})");
            if (!officer.Traits.Contains("Vingativo")) officer.Traits.Add("Vingativo");
            if (!officer.Traits.Contains("Cicatriz")) officer.Traits.Add("Cicatriz");
            Raise("nemesis", $"{officer.DisplayName} sobreviveu e jurou vingança contra você.", 0, 0, a: officer.Faction, subject: officer.DisplayName);
        }
    }

    /// <summary>The player helped a faction (missions, kills of its enemies, deliveries): it grows stronger.</summary>
    /// <summary>The player cleared a post and handed it to <paramref name="faction"/> (a mission for that faction).</summary>
    public bool PlayerTakeOutpost(int place, int index, int faction)
    {
        var city = Cities.FirstOrDefault(c => c.Place.Id == place);
        if (city == null || index < 0 || index >= city.Outposts.Count || city.Outposts[index].Owner == faction) return false;
        var post = city.Outposts[index]; int lost = post.Owner;
        post.Owner = faction; post.Garrison = B.OutpostGarrison * .5;
        Raise("outpost", $"Um mercenário tomou um posto perto de {city.Place.Name} para {Factions[faction].Profile.Name}.", post.X, post.Z, 2, a: faction, b: lost, place: place);
        return true;
    }

    public void HelpFaction(int faction, double amount)
    {
        var f = Factions[faction]; f.PlayerInfluence += amount; f.Morale = Math.Min(1.4, f.Morale + amount * .05); f.Treasury += amount * 40;
    }

    // ---------- economy, politics and diplomacy ----------
    void Upkeep(IslandCity c, double hours)
    {
        if (c.Owner < 0) return;
        var f = Factions[c.Owner];
        double production = Config.Rule(c.Place.Kind).Production * f.Rules.Production;
        if (f.Profile.Monsters) production = 0;
        c.Food = Math.Max(0, c.Food + (production - B.ConsumptionPerResident * c.Residents.Count(r => r.Alive)) * hours);
        foreach (var r in c.Residents.Where(r => r.Alive)) r.Health = Math.Min(1, r.Health + (c.Food > 0 ? .02 : .005) * hours);
        c.Residents.RemoveAll(r => !r.Alive);
        double growth = hours * (f.Profile.Monsters ? B.MonsterGrowth : c.Food > 10 ? B.Growth * f.Rules.Growth : 0);
        if (c.Residents.Count < c.Capacity && _random.NextDouble() < growth) c.Residents.Add(Born(c, c.Owner));
    }

    void Daily()
    {
        TendOutposts();
        DailyFactions();
    }

    void DailyFactions()
    {
        foreach (var f in Factions.Where(f => !f.Extinct && !f.Profile.Monsters))
        {
            var mine = Cities.Where(c => c.Owner == f.Id).ToList();
            // Taxes fill the treasury; the treasury hires soldiers (more under levy-heavy ideologies).
            f.Treasury += mine.Sum(c => c.Residents.Count) * 2 * f.Rules.Tax * B.TaxScale;
            if (f.VassalOf is int lord) { double tribute = f.Treasury * B.VassalTribute; f.Treasury -= tribute; Factions[lord].Treasury += tribute; }
            foreach (var c in mine.Where(c => c.IsCity && c.Residents.Count < c.Capacity))
                if (f.Treasury > B.RecruitCost) { f.Treasury -= B.RecruitCost / f.Rules.Recruit; c.Residents.Add(Born(c, f.Id)); }
            f.WarWeariness *= .9; f.Morale += (1 - f.Morale) * .05 + f.PlayerInfluence * .002;
            int over = Math.Max(0, Cities.Count(c => c.Owner == f.Id && c.IsCity) - B.OverextensionCities); // overextension: big empires are hard to hold
            f.Morale = Math.Max(.2, f.Morale - over * .02);
            // Unrest: ideology, hunger, conquest; revolts hand the city back or break free.
            foreach (var c in mine.Where(c => c.IsCity))
            {
                c.Unrest = Math.Max(0, c.Unrest * .93 + f.Rules.Unrest * .05 + (c.Food <= 0 ? .15 : 0) + f.WarWeariness * .01 - (c.Food > 40 ? .03 : 0) + over * .025 + (c.Residents.Any(r => r.SubjugatedUntil > Hours) ? .04 : 0));
                if (f.Rules.Purges && c.Unrest > .6 && c.Residents.Count > 3 && Hours >= c.NextPurge) { c.NextPurge = Hours + 24 * B.PurgeCooldownDays; var victim = c.Residents.Where(r => !r.Officer).OrderBy(r => r.Loyalty).FirstOrDefault(); if (victim != null) victim.Health = 0; c.Unrest -= .3; Raise("purge", $"Expurgo em {c.Place.Name}: dissidentes executados por {f.Profile.Name}.", c.Place.X, c.Place.Z, 0, a: f.Id, place: c.Place.Id); }
                if (c.Unrest > 1 && Hours >= c.CalmUntil) Revolt(c);
            }
            // Ambitious, disloyal officers may defect to a stronger enemy.
            foreach (var o in Officers(f.Id).Where(o => o.Rank != Rank.Leader && o.Loyalty < .3 && o.Ambition > .7 && !o.Traits.Contains("Honrado")).ToList())
            {
                var stronger = Factions.Where(x => x.Id != f.Id && AtWar(x.Id, f.Id) && !x.Profile.Monsters && x.Power > f.Power * 1.3).OrderByDescending(x => x.Power).FirstOrDefault();
                if (stronger != null && _random.NextDouble() < B.DefectChance * (o.Traits.Contains("Ambicioso") ? 2 : 1)) Defect(o, stronger.Id);
            }
            // Rival captains settle rank by duel now and then.
            var captains = Officers(f.Id).Where(o => o.Rank == Rank.Captain && o.Ambition > .6).ToList();
            if (captains.Count >= 2 && _random.NextDouble() < B.DuelChance) { var w = Duel(captains[0], captains[1]); if (Officers(f.Id).Count(o => o.Rank == Rank.General) < 2) Promote(w, Rank.General); else FillRank(f, Rank.Captain); }
        }
        if (HasNomads && Nomads is var nomads && City(FairPlace) is var fair && fair.Residents.Count(p => p.Faction == nomads.Id) < 14 && nomads.Treasury > 30) { nomads.Treasury -= 30; fair.Residents.Add(Born(fair, nomads.Id)); } // caravan recruits
        // Vassals rebel when their lord weakens or is overstretched by wars.
        foreach (var v in Factions.Where(x => x.VassalOf is int && !x.Extinct))
        {
            var lord = Factions[v.VassalOf!.Value];
            int lordWars = Factions.Count(x => x != lord && !x.Profile.Monsters && _atWar[lord.Id, x.Id]);
            if ((lord.Extinct || lord.Power < v.Power * 1.3 || lordWars >= 2) && _random.NextDouble() < .12)
            {
                v.VassalOf = null; SetWar(v.Id, lord.Id, true); _relations[v.Id, lord.Id] = _relations[lord.Id, v.Id] = -.7;
                Raise("betrayal", $"{v.Profile.Name} rompeu a vassalagem e se rebelou contra {lord.Profile.Name}.", 0, 0, a: v.Id, b: lord.Id);
            }
        }
        if (HasNomads) foreach (var f in Factions.Where(x => !x.Profile.Nomadic && !x.Profile.Monsters)) if (Relation(f.Id, Nomads.Id) < .35 && Hours >= _embargoUntil[f.Id]) ChangeRelation(f.Id, Nomads.Id, .01); // old grudges fade
        UpdatePower();
        Diplomacy();
        if ((int)(Hours / 24) % 7 == 0) MoveFair();
        foreach (var f in Factions.Where(f => f.Exiled)) Reconquest(f);
    }

    void Revolt(IslandCity c)
    {
        int ruler = c.Owner; c.Unrest = .3;
        // A people whose old faction died out may raise its banner again (keeps a conquered island from freezing under one ruler).
        var origin = c.OriginalOwner >= 0 && c.OriginalOwner != ruler ? Factions[c.OriginalOwner] : null;
        if (origin is { Extinct: true } && !origin.Profile.Monsters && !origin.Profile.Nomadic && Hours - origin.ExtinctSince >= 24 * (Factions.Count(x => !x.Extinct && !x.Profile.Monsters && !x.Profile.Nomadic) <= 1 ? 5 : B.RebirthDays)) { Reborn(origin, c, ruler); return; }
        int to = c.PreviousOwner != ruler && c.PreviousOwner >= 0 && !Factions[c.PreviousOwner].Extinct && !Factions[c.PreviousOwner].Profile.Monsters ? c.PreviousOwner
            : Factions.Where(f => f.Id != ruler && !f.Extinct && !f.Profile.Monsters && !f.Profile.Nomadic).OrderByDescending(f => Relation(f.Id, ruler) < 0 ? f.Power : 0).Select(f => f.Id).DefaultIfEmpty(-1).First();
        if (to < 0) return; // nobody left to turn to: the unrest simmers on
        c.CalmUntil = Hours + 24 * B.RevoltCalmDays; // a city that just revolted needs time before the next uprising
        foreach (var r in c.Residents.Where(r => r.Officer && r.Faction == ruler)) r.Health = 0;
        c.Residents.RemoveAll(r => !r.Alive);
        foreach (var r in c.Residents) r.Faction = to;
        SetOwner(c, to); bool wasCapital = c.CapitalOf == ruler;
        Raise("revolt", $"Revolta em {c.Place.Name}: o povo expulsou {Factions[ruler].Profile.Name} e se juntou a {Factions[to].Profile.Name}.", c.Place.X, c.Place.Z, a: ruler, b: to, place: c.Place.Id);
        if (wasCapital) CapitalFell(Factions[ruler], c);
        CheckExtinction(Factions[ruler], Factions[to]);
    }

    void Reborn(IslandFaction f, IslandCity c, int ruler)
    {
        c.CalmUntil = Hours + 24 * B.RevoltCalmDays;
        foreach (var r in c.Residents.Where(r => r.Officer && r.Faction == ruler)) r.Health = 0;
        c.Residents.RemoveAll(r => !r.Alive);
        foreach (var r in c.Residents) { r.Faction = f.Id; r.SubjugatedUntil = 0; }
        bool wasCapital = c.CapitalOf == ruler;
        SetOwner(c, f.Id); c.CapitalOf = f.Id;
        f.Extinct = false; f.Exiled = false; f.ExtinctSince = -1; f.VassalOf = null; f.Morale = 1; f.WarWeariness = 0; f.Treasury = Math.Max(f.Treasury, 200);
        var leader = MakeOfficer(c, f.Id, Rank.Leader); leader.Title = "";
        for (int g = 0; g < B.Generals; g++) MakeOfficer(c, f.Id, Rank.General);
        for (int k = 0; k < B.Captains / 2; k++) MakeOfficer(c, f.Id, Rank.Captain);
        while (c.Residents.Count(r => r.Alive && r.Faction == f.Id) < c.Capacity / 2) c.Residents.Add(Born(c, f.Id));
        // A grace period lets the reborn faction hold its city; without it most died again within days.
        foreach (var o in Factions.Where(o => o.Id != f.Id && !o.Profile.Monsters)) { SetWar(f.Id, o.Id, false); _truceUntil[f.Id, o.Id] = _truceUntil[o.Id, f.Id] = Hours + 24 * B.RebirthGraceDays; }
        _relations[f.Id, ruler] = _relations[ruler, f.Id] = -.7;
        Raise("rebirth", $"{f.Profile.Name} renasce em {c.Place.Name}: {leader.DisplayName} lidera o levante contra {Factions[ruler].Profile.Name}.", c.Place.X, c.Place.Z, 2, a: f.Id, b: ruler, place: c.Place.Id, subject: leader.DisplayName);
        if (wasCapital) CapitalFell(Factions[ruler], c);
        CheckExtinction(Factions[ruler], f);
    }

    void UpdatePower()
    {
        foreach (var f in Factions)
        {
            f.Cities = Cities.Count(c => c.Owner == f.Id && c.IsCity);
            f.Power = f.Cities * 3 + AllPeople().Where(p => p.Alive && p.Faction == f.Id).Sum(p => p.Power) * .3 + Math.Max(0, f.Treasury) / 300 + f.PlayerInfluence * .5;
        }
    }

    void Diplomacy()
    {
        var military = Factions.Where(f => !f.Extinct && !f.Profile.Monsters && !f.Profile.Nomadic).ToList();
        double total = military.Sum(f => f.Power);
        var top = military.OrderByDescending(f => f.Power).FirstOrDefault();
        // Balance of power: the others close ranks against a faction holding over 35% of the island's power.
        if (top != null && total > 0 && top.Power / total > B.CoalitionShare && Hours - _lastCoalition > 24 * B.CoalitionCooldownDays)
        {
            // Grand coalition: everyone else makes peace among themselves and turns on the hegemon.
            _lastCoalition = Hours;
            foreach (var a in military.Where(f => f != top && f.VassalOf != top.Id)) { SetWar(a.Id, top.Id, true); _relations[a.Id, top.Id] = _relations[top.Id, a.Id] = -.8; }
            foreach (var a in military.Where(f => f != top)) foreach (var b in military.Where(f => f != top && f.Id > a.Id))
                if (AtWar(a.Id, b.Id)) { SetWar(a.Id, b.Id, false); _truceUntil[a.Id, b.Id] = _truceUntil[b.Id, a.Id] = Hours + 24 * B.CoalitionTruceDays; _relations[a.Id, b.Id] = _relations[b.Id, a.Id] = .2; }
            Raise("coalition", $"Grande coalizão contra {top.Profile.Name}: as outras facções selam a paz entre si.", 0, 0, b: top.Id);
        }
        if (top != null && total > 0 && top.Power / total > .3)
            foreach (var a in military.Where(f => f != top))
            {
                ChangeRelation(a.Id, top.Id, -.04);
                foreach (var b in military.Where(f => f != top && f.Id > a.Id))
                {
                    bool wasAllied = StanceOf(a.Id, b.Id) == Stance.Alliance;
                    ChangeRelation(a.Id, b.Id, .03);
                    if (!wasAllied && StanceOf(a.Id, b.Id) == Stance.Alliance) Raise("coalition", $"{a.Profile.Name} e {b.Profile.Name} se aliam contra {top.Profile.Name}.", 0, 0, a: a.Id, b: b.Id);
                }
            }
        foreach (var a in military) foreach (var b in military.Where(f => f.Id > a.Id))
        {
            var stance = StanceOf(a.Id, b.Id);
            // Resource pacts: a well-fed faction sells food to a hungry one (per head, not in total); mine owners sell ore.
            int popA = Math.Max(1, Cities.Where(c => c.Owner == a.Id).Sum(c => c.Residents.Count)), popB = Math.Max(1, Cities.Where(c => c.Owner == b.Id).Sum(c => c.Residents.Count));
            double foodA = Cities.Where(c => c.Owner == a.Id).Sum(c => c.Food), foodB = Cities.Where(c => c.Owner == b.Id).Sum(c => c.Food);
            double perA = foodA / popA, perB = foodB / popB;
            bool oreTrade = Cities.Any(c => c.Owner == a.Id && c.Place.Kind == "Mine") != Cities.Any(c => c.Owner == b.Id && c.Place.Kind == "Mine");
            if (stance != Stance.War && Hours - _lastPact[a.Id, b.Id] > 24 * B.PactCooldownDays && (Math.Max(perA, perB) > Math.Min(perA, perB) * B.PactFoodRatio || (oreTrade && _random.NextDouble() < .2)))
            {
                _lastPact[a.Id, b.Id] = Hours; ChangeRelation(a.Id, b.Id, .12);
                var (rich, poor) = perA >= perB ? (a, b) : (b, a);
                var richStore = Cities.Where(c => c.Owner == rich.Id).OrderByDescending(c => c.Food).FirstOrDefault();
                var poorStore = Cities.Where(c => c.Owner == poor.Id).OrderBy(c => c.Food).FirstOrDefault();
                double food = richStore != null && poorStore != null ? Math.Min(40, richStore.Food * .3) : 0;
                if (food > 0 && !oreTrade) { richStore!.Food -= food; poorStore!.Food += food; poor.Treasury -= food * 2; rich.Treasury += food * 2; }
                else { a.Treasury += 60; b.Treasury += 60; }
                Raise("pact", oreTrade ? $"Pacto de minério: {a.Profile.Name} e {b.Profile.Name} trocam ferro por prata." : $"Pacto comercial: {rich.Profile.Name} vende {food:0} de comida a {poor.Profile.Name}.", 0, 0, a: a.Id, b: b.Id);
            }
            // Defence pacts: a common enemy brings two factions together.
            var common = military.FirstOrDefault(c => c != a && c != b && AtWar(a.Id, c.Id) && AtWar(b.Id, c.Id));
            if (common != null && stance is Stance.Neutral or Stance.Hostile or Stance.Truce)
            {
                ChangeRelation(a.Id, b.Id, .06);
                if (StanceOf(a.Id, b.Id) == Stance.Alliance) Raise("pact", $"Pacto de defesa: {a.Profile.Name} e {b.Profile.Name} unem forças contra {common.Profile.Name}.", 0, 0, a: a.Id, b: b.Id);
            }
            // War drivers: sworn enemies resume after a truce; strong expansionist regimes invade weaker neighbours; hunger covets food.
            if (stance is Stance.Neutral or Stance.Hostile)
            {
                bool sworn = a.Profile.SwornEnemy == b.Id || b.Profile.SwornEnemy == a.Id;
                var (strong, weak) = a.Power >= b.Power ? (a, b) : (b, a);
                double hunger = Cities.Where(c => c.Owner == strong.Id).Sum(c => c.Food) < 60 && Cities.Where(c => c.Owner == weak.Id).Sum(c => c.Food) > 150 ? .08 : 0;
                double appetite = Aggression(strong.Profile.Ideology) * strong.Morale + hunger;
                if (sworn || (strong.Power > weak.Power * 1.25 && Relation(a.Id, b.Id) < .15 && _random.NextDouble() < appetite))
                {
                    SetWar(a.Id, b.Id, true); _relations[a.Id, b.Id] = _relations[b.Id, a.Id] = Math.Min(Relation(a.Id, b.Id), -.6);
                    Raise("war", sworn ? $"A trégua acabou: {a.Profile.Name} e {b.Profile.Name} voltam à guerra." : $"{strong.Profile.Name} invade {weak.Profile.Name}{(hunger > 0 ? " em busca de comida" : "")}.", 0, 0, a: a.Id, b: b.Id);
                    continue;
                }
            }
            // War weariness brings truces; a broken balance brings declarations and betrayals.
            // Weariness is per faction, so a minimum length keeps a second front from ending the day after it opens.
            if (stance == Stance.War && Hours - _warSince[a.Id, b.Id] >= 24 * B.MinWarDays && a.WarWeariness > B.TruceWeariness && b.WarWeariness > B.TruceWeariness)
            {
                SetWar(a.Id, b.Id, false); _truceUntil[a.Id, b.Id] = _truceUntil[b.Id, a.Id] = Hours + 24 * B.TruceDays; _relations[a.Id, b.Id] = _relations[b.Id, a.Id] = -.2;
                Raise("truce", $"Trégua de {B.TruceDays:0} dias entre {a.Profile.Name} e {b.Profile.Name}.", 0, 0, a: a.Id, b: b.Id);
            }
            else if (stance != Stance.War && stance != Stance.Truce && stance != Stance.Vassal && Relation(a.Id, b.Id) < -.5)
            { SetWar(a.Id, b.Id, true); Raise("war", $"{a.Profile.Name} declarou guerra a {b.Profile.Name}.", 0, 0, a: a.Id, b: b.Id); }
            else if (stance == Stance.Alliance)
            {
                var (big, small) = a.Power >= b.Power ? (a, b) : (b, a);
                var bigLeader = Officers(big.Id).FirstOrDefault(o => o.Rank == Rank.Leader);
                // Opportunism: an ambitious or expansionist ally turns on a weaker partner, more so once their common enemy is gone.
                bool sharedWar = military.Any(c => c != a && c != b && AtWar(a.Id, c.Id) && AtWar(b.Id, c.Id));
                double temptation = ((bigLeader?.Ambition ?? 0) > .5 ? .05 : 0) + Aggression(big.Profile.Ideology) + (sharedWar ? 0 : .04);
                if (small.Power < big.Power * .75 && _random.NextDouble() < temptation)
                { _relations[a.Id, b.Id] = _relations[b.Id, a.Id] = -.8; SetWar(a.Id, b.Id, true); Raise("betrayal", $"Traição! {big.Profile.Name} rompeu a aliança e atacou {small.Profile.Name}.", 0, 0, a: big.Id, b: small.Id); }
            }
        }
    }

    // Daily chance that a stronger regime of this ideology starts a war of conquest.
    static double Aggression(Ideology i) => i switch { Ideology.Authoritarian => .07, Ideology.Feudalism => .05, Ideology.Communism => .04, Ideology.Theocracy => .03, Ideology.Capitalism => .025, Ideology.Socialism => .015, Ideology.Anarchism => .008, _ => 0 };

    void SetWar(int a, int b, bool war) { if (war && !_atWar[a, b]) _warSince[a, b] = _warSince[b, a] = Hours; _atWar[a, b] = _atWar[b, a] = war; }

    void MoveFair()
    {
        if (!HasNomads) return;
        var peaceful = Cities.Where(c => c.IsCity && c.Owner >= 0 && !AtWar(Nomads.Id, c.Owner)).ToList();
        if (peaceful.Count == 0) return;
        FairPlace = peaceful[_random.Next(peaceful.Count)].Place.Id;
        var fair = City(FairPlace);
        foreach (var n in Cities.SelectMany(c => c.Residents).Where(p => p.Faction == Nomads.Id).ToList()) { foreach (var c in Cities) c.Residents.Remove(n); fair.Residents.Add(n); n.Home = FairPlace; }
        Raise("fair", $"A feira nômade agora acontece em {fair.Place.Name}.", fair.Place.X, fair.Place.Z, 0, place: fair.Place.Id);
    }

    void ChangeRelation(int a, int b, double delta)
    {
        if (a == b) return;
        double v = Math.Max(-1, Math.Min(1, _relations[a, b] + delta));
        _relations[a, b] = _relations[b, a] = v;
    }

    /// <summary>Optional translator: return the text for an event in your language (null keeps the built-in text).</summary>
    public Func<IslandEvent, string?>? Localize { get; set; }

    void Raise(string kind, string text, double x, double z, int priority = 1, int a = -1, int b = -1, int place = -1, string? subject = null)
    {
        var e = new IslandEvent(Math.Round(Hours, 2), kind, text, x, z, priority, a, b, place, subject);
        if (Localize?.Invoke(e) is string local) e = e with { Text = local };
        _events.Add(e); if (_events.Count > 500) _events.RemoveAt(0);
        EventRaised?.Invoke(e);
    }

    /// <summary>Engines report damage dealt in an on-screen fight (player or bodies).</summary>
    public void ReportDamage(IslandPerson victim, double damage, IslandPerson? attacker) => Wound(victim, damage, attacker);

    public ulong Fingerprint()
    {
        ulong h = StableHash.Combine((ulong)(Hours * 1000), (ulong)_squadIds);
        foreach (var c in Cities) h = StableHash.Combine(h, (ulong)(c.Owner + 1), (ulong)c.Residents.Count, (ulong)(c.Food * 100));
        foreach (var o in Cities.SelectMany(c => c.Outposts)) h = StableHash.Combine(h, (ulong)(o.Owner + 1), (ulong)(o.Garrison * 100));
        foreach (var e in _events) h = StableHash.Combine(h, StableHash.Text(e.Text));
        return h;
    }

    public static string RankLabel(Rank r) => r switch { Rank.Leader => "líder", Rank.General => "general", Rank.Captain => "capitão", _ => "soldado" };
    public static string PolicyLabel(ConquestPolicy p) => p switch { ConquestPolicy.Integrate => "integrou os moradores", ConquestPolicy.Exploit => "escravizou os moradores", ConquestPolicy.Convert => "converteu os moradores", ConquestPolicy.Execute => "executou os moradores", _ => "libertou" };
    public static string IdeologyLabel(Ideology i) => i switch
    {
        Ideology.Capitalism => "Capitalismo", Ideology.Socialism => "Socialismo", Ideology.Communism => "Comunismo", Ideology.Theocracy => "Teocracia",
        Ideology.Feudalism => "Feudalismo", Ideology.Anarchism => "Anarquismo", Ideology.Authoritarian => "Regime autoritário", Ideology.Mercantile => "Mercantil", _ => "Praga",
    };
}
