namespace ArtificialLife.Core;

// Everything a game tunes about the war simulation, as plain serializable data (public fields, arrays), so any engine
// can load it from JSON (Unity JsonUtility, System.Text.Json with IncludeFields, Godot...). Default() is the island game.

[Serializable]
public sealed class FactionData
{
    public int Id;
    public string Name = "", LeaderName = "", Story = "";
    public Ideology Ideology;
    public ConquestPolicy Conquest;
    /// <summary>Place kinds this faction prefers to attack (e.g. "Port", "Mine").</summary>
    public string[] PreferredTargets = Array.Empty<string>();
    public bool Nomadic, Monsters, AlliedWithMonsters;
    public int SwornEnemy = -1;
    public NameCulture Culture = NameCulture.Common;

    public FactionProfile ToProfile() => new(Id, Name, LeaderName, Ideology, Conquest, Story, PreferredTargets, Nomadic, Monsters, SwornEnemy, AlliedWithMonsters, Culture);
    public static FactionData From(FactionProfile p) => new()
    {
        Id = p.Id, Name = p.Name, LeaderName = p.LeaderName, Story = p.Story, Ideology = p.Ideology, Conquest = p.Conquest, PreferredTargets = p.PreferredTargets,
        Nomadic = p.Nomadic, Monsters = p.Monsters, AlliedWithMonsters = p.AlliedWithMonsters, SwornEnemy = p.SwornEnemy, Culture = NameForge.CultureFor(p),
    };
}

/// <summary>What a kind of place is in the simulation (matched by WorldPlace.Kind).</summary>
[Serializable]
public sealed class PlaceRule
{
    public string Kind = "";
    /// <summary>Food per hour before ideology multipliers.</summary>
    public double Production;
    /// <summary>Residents it can hold (0 = by tier: 18 capital, 10 city, 5 other).</summary>
    public int Capacity;
    /// <summary>Monster lair: hunted, never held by people.</summary>
    public bool Lair;
    /// <summary>Preferred kind for faction capitals.</summary>
    public bool CapitalCandidate;
}

[Serializable]
public sealed class BalanceData
{
    public int MaxSquads = 10, SquadsPerFaction = 3, MonsterSquads = 2;
    public double CycleHours = .5, SquadSpeedKmh = 4.5, EncounterMetres = 200;
    public double RaidChance = .3;
    /// <summary>Minimum share of a city's capacity that must stay home before a raid.</summary>
    public double GarrisonShare = .5;
    public double MonsterRaidCooldownHours = 18, LairHuntCooldownHours = 24;
    public double ConsumptionPerResident = .04, Growth = .045, MonsterGrowth = .08;
    public double SubjugationDays = 15, SerfdomDays = 30, SubjugatedPower = .3;
    public int OverextensionCities = 6;
    public double CoalitionShare = .4, CoalitionCooldownDays = 10, CoalitionTruceDays = 15;
    public double TruceDays = 10, TruceWeariness = 2;
    /// <summary>Days a war lasts before war weariness alone can bring a truce.</summary>
    public double MinWarDays = 8;
    /// <summary>Posts around each city; a city can only be besieged once none of them belongs to its owner.</summary>
    public int OutpostsPerCity = 3;
    public double OutpostGarrison = 2, OutpostRadius = 600, OutpostRegenPerDay = .5, OutpostRetakeChance = .15;
    public double PactCooldownDays = 8, PactFoodRatio = 1.6;
    public double EmbargoDays = 10, PurgeCooldownDays = 7, RevoltCalmDays = 20;
    public double RecruitCost = 40, TaxScale = 10, VassalTribute = .3;
    public int Generals = 2, Captains = 6, NomadCaptains = 3, NomadGuards = 8, PromotionKills = 6;
    public double DuelChance = .04, DefectChance = .15;
    /// <summary>Days after dying out before a revolt in one of its original cities can bring a faction back.</summary>
    public double RebirthDays = 60; // only 5 days when a single military faction is left, so the island never freezes under one ruler
    /// <summary>Days of truce a reborn faction gets with everyone (monsters excluded).</summary>
    public double RebirthGraceDays = 30;
}

[Serializable]
public sealed class IslandLifeConfig
{
    public FactionData[] Factions = Array.Empty<FactionData>();
    public PlaceRule[] Places = Array.Empty<PlaceRule>();
    public BalanceData Balance = new();

    public PlaceRule Rule(string kind) => Array.Find(Places, p => p.Kind == kind) ?? new PlaceRule { Kind = kind };

    /// <summary>The island game: five military factions, nomads and the dead; towns, villages, farms, ports, mines, lairs.</summary>
    public static IslandLifeConfig Default() => new()
    {
        Factions = FactionProfile.Default.Select(FactionData.From).ToArray(),
        Places = new[]
        {
            new PlaceRule { Kind = "Town", Production = .9, CapitalCandidate = true },
            new PlaceRule { Kind = "Village", Production = 1.1 },
            new PlaceRule { Kind = "Farm", Production = 3 },
            new PlaceRule { Kind = "Port", Production = 2.2 },
            new PlaceRule { Kind = "Fishing", Production = 2.2 },
            new PlaceRule { Kind = "Mine", Production = .5 },
            new PlaceRule { Kind = "Nest", Lair = true, Capacity = 8 },
            new PlaceRule { Kind = "Ruins", Lair = true, Capacity = 8 },
        },
    };
}
