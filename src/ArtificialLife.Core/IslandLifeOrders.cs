namespace ArtificialLife.Core;

// Orders a game can give the war from outside (STALKER-like reactions): a faction under attack calls reinforcements
// from its strongest nearby city; they march along the routes like any other squad.
public sealed partial class IslandLife
{
    /// <summary>Sends a patrol from the faction's best-garrisoned city (other than the target) to the place under attack.
    /// Keeps half of that city's capacity at home; returns the squad, or null when nobody can be spared.</summary>
    public IslandSquad? SendReinforcements(int faction, int placeId)
    {
        if (faction < 0 || faction >= Factions.Count) return null;
        var f = Factions[faction]; var target = Cities.FirstOrDefault(c => c.Place.Id == placeId);
        if (f.Extinct || f.Profile.Monsters || target == null || _squads.Count(s => !s.Done) >= MaxSquads + 4) return null;
        var home = Cities.Where(c => c != target && c.Owner == faction && c.IsCity)
            .OrderByDescending(c => c.Residents.Count(r => r.Alive && !r.Wounded && r.Faction == faction) - Distance(c.Place, target.Place.X, target.Place.Z) / 1500)
            .FirstOrDefault();
        if (home == null) return null;
        var ready = home.Residents.Where(r => r.Alive && !r.Wounded && r.Faction == faction && r.Rank != Rank.Leader).ToList();
        if (ready.Count - 3 < home.Capacity / 2) return null; // the garrison stays
        int before = _squadIds;
        Launch(f, home, target, IslandMission.Patrol, ready, null, 0);
        return _squadIds != before ? _squads.LastOrDefault() : null;
    }
}
