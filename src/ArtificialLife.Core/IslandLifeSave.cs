namespace ArtificialLife.Core;

// Save/load for IslandLife. The state is plain public fields and 1-D arrays so any engine serializer can write it
// (Unity JsonUtility, System.Text.Json with IncludeFields, binary formatters...). It is self-contained: config, places
// and routes travel with it, so IslandLife.Load(state) needs nothing else and continues exactly where it stopped.

[Serializable]
public sealed class IslandLifeState
{
    public int Version = 1;
    public ulong Seed, RandomState, NextId;
    public double Hours, CycleClock, DayClock, LastCoalition;
    public int SquadIds, FairPlace;
    public IslandLifeConfig Config = new();
    public PlaceState[] Places = Array.Empty<PlaceState>();
    public RouteState[] Routes = Array.Empty<RouteState>();
    /// <summary>Faction-by-faction matrices flattened row by row (index a * count + b).</summary>
    public double[] Relations = Array.Empty<double>(), TruceUntil = Array.Empty<double>(), LastPact = Array.Empty<double>();
    public bool[] AtWar = Array.Empty<bool>();
    public double[] EmbargoUntil = Array.Empty<double>();
    public FactionState[] Factions = Array.Empty<FactionState>();
    public CityState[] Cities = Array.Empty<CityState>();
    public PersonState[] People = Array.Empty<PersonState>();
    public SquadState[] Squads = Array.Empty<SquadState>();
    public EventState[] Events = Array.Empty<EventState>();
    public string[] UsedPeople = Array.Empty<string>(), UsedTitles = Array.Empty<string>(), UsedPlaces = Array.Empty<string>();
}

[Serializable] public sealed class PlaceState { public int Id, Tier; public string Name = "", Kind = ""; public double X, Z; }
[Serializable] public sealed class RouteState { public int From, To; public double Length; }
[Serializable] public sealed class FactionState { public double Treasury, Morale, WarWeariness, PlayerInfluence, Power; public int VassalOf = -1, Cities; public bool Extinct, Exiled; }

[Serializable]
public sealed class CityState
{
    public int Place, Owner, OriginalOwner, PreviousOwner, CapitalOf, Capacity;
    public bool Lair;
    public double Food, Unrest, NextRaid, HuntedUntil, CalmUntil, NextPurge;
}

[Serializable]
public sealed class PersonState
{
    public ulong Id;
    public string Name = "", Title = "", Strength = "", Weakness = "";
    public int Faction, Home, Kills;
    /// <summary>Index of the city where the person lives, or -1 when only in a squad (squads list their members by Id).</summary>
    public int City = -1;
    public bool Female;
    public Rank Rank;
    public double Health, Combat, Gear, Loyalty, Ambition, SubjugatedUntil;
    public long Bounty;
    public string[] Traits = Array.Empty<string>(), Memory = Array.Empty<string>();
}

[Serializable]
public sealed class SquadState
{
    public int Id, Faction, Origin, Target, Prey = -1;
    public IslandMission Mission;
    public double X, Z, Cargo;
    public bool Returning, Done, ControlledExternally;
    public int[] Path = Array.Empty<int>();
    public ulong[] Members = Array.Empty<ulong>();
}

[Serializable]
public sealed class EventState
{
    public double Hour, X, Z;
    public string Kind = "", Text = "";
    public string? Subject;
    public int Priority, FactionA, FactionB, Place;
}

public sealed partial class IslandLife
{
    /// <summary>Captures the whole simulation (deterministic: Load(Save()) continues with the same results).</summary>
    public IslandLifeState Save()
    {
        int n = Factions.Count;
        var s = new IslandLifeState
        {
            Seed = _seed, RandomState = _random.State, NextId = _ids.NextValue, Hours = Hours, CycleClock = _cycleClock, DayClock = _dayClock,
            LastCoalition = _lastCoalition, SquadIds = _squadIds, FairPlace = FairPlace, Config = Config,
            Places = _places.Select(p => new PlaceState { Id = p.Id, Name = p.Name, Kind = p.Kind, X = p.X, Z = p.Z, Tier = p.Tier }).ToArray(),
            Routes = _routes.Select(r => new RouteState { From = r.From, To = r.To, Length = r.Length }).ToArray(),
            Relations = Flatten(_relations, n), TruceUntil = Flatten(_truceUntil, n), LastPact = Flatten(_lastPact, n),
            AtWar = Enumerable.Range(0, n * n).Select(i => _atWar[i / n, i % n]).ToArray(),
            EmbargoUntil = (double[])_embargoUntil.Clone(),
            Factions = Factions.Select(f => new FactionState
            {
                Treasury = f.Treasury, Morale = f.Morale, WarWeariness = f.WarWeariness, PlayerInfluence = f.PlayerInfluence, Power = f.Power,
                VassalOf = f.VassalOf ?? -1, Cities = f.Cities, Extinct = f.Extinct, Exiled = f.Exiled,
            }).ToArray(),
            Cities = Cities.Select(c => new CityState
            {
                Place = c.Place.Id, Owner = c.Owner, OriginalOwner = c.OriginalOwner, PreviousOwner = c.PreviousOwner, CapitalOf = c.CapitalOf, Capacity = c.Capacity, Lair = c.IsLair,
                Food = c.Food, Unrest = c.Unrest, NextRaid = c.NextRaid, HuntedUntil = c.HuntedUntil, CalmUntil = c.CalmUntil, NextPurge = c.NextPurge,
            }).ToArray(),
            People = Cities.SelectMany((c, i) => c.Residents.Select(p => (p, i))).Concat(_squads.SelectMany(q => q.Members.Select(p => (p, -1))))
                .GroupBy(x => x.p).Select(g => SavePerson(g.Key, g.First().Item2)).ToArray(),
            Squads = _squads.Select(q => new SquadState
            {
                Id = q.Id, Faction = q.Faction, Origin = q.Origin, Target = q.Target, Prey = q.Prey?.Id ?? -1, Mission = q.Mission,
                X = q.X, Z = q.Z, Cargo = q.Cargo, Returning = q.Returning, Done = q.Done, ControlledExternally = q.ControlledExternally, Path = q.Path.ToArray(),
                Members = q.Members.Select(m => m.Id.Value).ToArray(),
            }).ToArray(),
            Events = _events.Select(e => new EventState
            {
                Hour = e.Hour, Kind = e.Kind, Text = e.Text, X = e.X, Z = e.Z, Priority = e.Priority, FactionA = e.FactionA, FactionB = e.FactionB, Place = e.Place, Subject = e.Subject,
            }).ToArray(),
        };
        var used = _names.ExportUsed(); s.UsedPeople = used[0]; s.UsedTitles = used[1]; s.UsedPlaces = used[2];
        return s;
    }

    /// <summary>Rebuilds a simulation saved with Save().</summary>
    public static IslandLife Load(IslandLifeState state) => new(state ?? throw new ArgumentNullException(nameof(state)));

    IslandLife(IslandLifeState s)
    {
        if (s.Version != 1) throw new NotSupportedException($"IslandLife save version {s.Version} is not supported.");
        Config = s.Config; _seed = s.Seed;
        _places = s.Places.Select(p => new WorldPlace(p.Id, p.Name, p.Kind, p.X, p.Z, p.Tier)).ToArray();
        _routes = s.Routes.Select(r => new WorldRoute(r.From, r.To, r.Length)).ToArray();
        _random = DeterministicRandom.FromState(s.RandomState); _ids = new IdSequence(s.Seed, s.NextId);
        Hours = s.Hours; _cycleClock = s.CycleClock; _dayClock = s.DayClock; _lastCoalition = s.LastCoalition; _squadIds = s.SquadIds; FairPlace = s.FairPlace;
        Factions = Config.Factions.OrderBy(f => f.Id).Select(f => new IslandFaction(f.ToProfile())).ToList();
        int n = Factions.Count;
        _relations = Unflatten(s.Relations, n); _truceUntil = Unflatten(s.TruceUntil, n); _lastPact = Unflatten(s.LastPact, n);
        _atWar = new bool[n, n]; for (int i = 0; i < n * n; i++) _atWar[i / n, i % n] = s.AtWar[i];
        _embargoUntil = (double[])s.EmbargoUntil.Clone();
        for (int i = 0; i < n; i++)
        {
            var f = Factions[i]; var d = s.Factions[i];
            f.Treasury = d.Treasury; f.Morale = d.Morale; f.WarWeariness = d.WarWeariness; f.PlayerInfluence = d.PlayerInfluence; f.Power = d.Power;
            f.VassalOf = d.VassalOf >= 0 ? d.VassalOf : null; f.Cities = d.Cities; f.Extinct = d.Extinct; f.Exiled = d.Exiled;
        }
        for (int i = 0; i < s.Cities.Length; i++)
        {
            var d = s.Cities[i];
            Cities.Add(new IslandCity(Array.Find(_places, p => p.Id == d.Place)!, d.Owner, d.Capacity)
            {
                OriginalOwner = d.OriginalOwner, PreviousOwner = d.PreviousOwner, CapitalOf = d.CapitalOf, IsLair = d.Lair, Food = d.Food, Unrest = d.Unrest,
                NextRaid = d.NextRaid, HuntedUntil = d.HuntedUntil, CalmUntil = d.CalmUntil, NextPurge = d.NextPurge, Now = Hours, SubjugatedPower = B.SubjugatedPower,
            });
        }
        var squads = new Dictionary<int, IslandSquad>();
        foreach (var d in s.Squads)
        {
            var q = new IslandSquad(d.Id, d.Faction, d.Mission, d.Origin, d.Target) { X = d.X, Z = d.Z, Cargo = d.Cargo, Returning = d.Returning, Done = d.Done, ControlledExternally = d.ControlledExternally };
            q.Path.AddRange(d.Path); _squads.Add(q); squads[q.Id] = q;
        }
        foreach (var d in s.Squads) if (d.Prey >= 0 && squads.TryGetValue(d.Prey, out var prey)) squads[d.Id].Prey = prey;
        var people = new Dictionary<ulong, IslandPerson>();
        foreach (var d in s.People)
        {
            var p = new IslandPerson(new EntityId(d.Id), d.Name, d.Faction, d.Home)
            {
                Female = d.Female, Health = d.Health, Combat = d.Combat, Gear = d.Gear, Rank = d.Rank, Title = d.Title, Strength_ = d.Strength, Weakness = d.Weakness,
                Loyalty = d.Loyalty, Ambition = d.Ambition, Kills = d.Kills, Bounty = d.Bounty, SubjugatedUntil = d.SubjugatedUntil,
            };
            p.Traits.AddRange(d.Traits); p.Memory.AddRange(d.Memory);
            people[d.Id] = p; if (d.City >= 0) Cities[d.City].Residents.Add(p);
        }
        foreach (var d in s.Squads) squads[d.Id].Members.AddRange(d.Members.Select(id => people[id]));
        _events.AddRange(s.Events.Select(e => new IslandEvent(e.Hour, e.Kind, e.Text, e.X, e.Z, e.Priority, e.FactionA, e.FactionB, e.Place, e.Subject)));
        _names.ImportUsed(new[] { s.UsedPeople, s.UsedTitles, s.UsedPlaces });
    }

    static PersonState SavePerson(IslandPerson p, int city) => new()
    {
        Id = p.Id.Value, Name = p.Name, Title = p.Title, Strength = p.Strength_, Weakness = p.Weakness, Faction = p.Faction, Home = p.Home, Kills = p.Kills,
        City = city, Female = p.Female, Rank = p.Rank, Health = p.Health, Combat = p.Combat, Gear = p.Gear, Loyalty = p.Loyalty,
        Ambition = p.Ambition, SubjugatedUntil = p.SubjugatedUntil, Bounty = p.Bounty, Traits = p.Traits.ToArray(), Memory = p.Memory.ToArray(),
    };

    static double[] Flatten(double[,] m, int n) => Enumerable.Range(0, n * n).Select(i => m[i / n, i % n]).ToArray();
    static double[,] Unflatten(double[] a, int n) { var m = new double[n, n]; for (int i = 0; i < n * n; i++) m[i / n, i % n] = a[i]; return m; }
}
