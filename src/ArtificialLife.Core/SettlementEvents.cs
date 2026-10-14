namespace ArtificialLife.Core;

public abstract record SettlementEvent(int Day, int SettlementId);
public sealed record SettlementFounded(int Day, int SettlementId) : SettlementEvent(Day, SettlementId);
public sealed record SettlementUpgraded(int Day, int SettlementId, SettlementStage? Stage) : SettlementEvent(Day, SettlementId);
public sealed record BuildingCompleted(int Day, int SettlementId, int BuildingCount) : SettlementEvent(Day, SettlementId);
public sealed record AllianceFormed(int Day, int SettlementId, int OtherId) : SettlementEvent(Day, SettlementId);
public sealed record WarDeclared(int Day, int SettlementId, int OtherId) : SettlementEvent(Day, SettlementId);
public sealed record PeaceAgreed(int Day, int SettlementId, int OtherId) : SettlementEvent(Day, SettlementId);
public sealed record BattleResolved(int Day, int SettlementId, int OtherId, int Deaths) : SettlementEvent(Day, SettlementId);
public sealed record SettlementDestroyed(int Day, int SettlementId, int AttackerId) : SettlementEvent(Day, SettlementId);
public sealed record RefugeeGroupCreated(int Day, int SettlementId, int People) : SettlementEvent(Day, SettlementId);
public sealed record RuinsResettled(int Day, int SettlementId, int PreviousOwner) : SettlementEvent(Day, SettlementId);
public sealed record PopulationMigrated(int Day, int SettlementId, int People) : SettlementEvent(Day, SettlementId);
public sealed record SettlementEventBatch(int NextCursor, IReadOnlyList<SettlementEvent> Events);

public sealed partial class SettlementSimulation
{
    /// <summary>Read events without changing deterministic state. Persist the cursor in the engine adapter.</summary>
    public SettlementEventBatch ReadEvents(int cursor = 0)
    {
        if (cursor < 0 || cursor > History.Count) throw new ArgumentOutOfRangeException(nameof(cursor));
        var events = new List<SettlementEvent>();
        for (int i = cursor; i < History.Count; i++)
        {
            var e = History[i];
            events.Add(e.Kind switch
            {
                "foundation" => new SettlementFounded(e.Day, e.SettlementId),
                "upgrade" => new SettlementUpgraded(e.Day, e.SettlementId, e.Detail < 0 ? null : (SettlementStage)e.Detail),
                "building" => new BuildingCompleted(e.Day, e.SettlementId, e.Detail),
                "alliance" => new AllianceFormed(e.Day, e.SettlementId, e.OtherId),
                "war" => new WarDeclared(e.Day, e.SettlementId, e.OtherId),
                "peace" => new PeaceAgreed(e.Day, e.SettlementId, e.OtherId),
                "battle" => new BattleResolved(e.Day, e.SettlementId, e.OtherId, e.People),
                "destruction" => new SettlementDestroyed(e.Day, e.SettlementId, e.OtherId),
                "refugees" => new RefugeeGroupCreated(e.Day, e.SettlementId, e.People),
                "resettled" => new RuinsResettled(e.Day, e.SettlementId, e.Detail),
                "migration" => new PopulationMigrated(e.Day, e.SettlementId, e.People),
                _ => throw new InvalidOperationException("Unknown settlement history event.")
            });
        }
        return new(History.Count, events.AsReadOnly());
    }
}
