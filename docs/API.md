# Public API and extension contracts

All exported types in `ArtificialLife.Core` are recorded in [PublicApi.txt](PublicApi.txt). Serialization readers/writers, mutable engine internals and lifecycle methods remain internal/private. Domain objects expose read-only regional collections. Mutable agent profiles and game-owned inventories remain intentional application inputs.

- `LifeWorld`: deterministic individual agents, decisions, money, bounded events and world snapshots.
- `PortableSimulation`: fixed-step clock, profiles, aid, regional cargo, queued commands, presentation observers and complete host persistence.
- `SettlementSimulation`: regional aggregated settlements; territory, population/construction, migration, diplomacy, conflicts and audits live in separate implementation files.
- `SettlementRules`: immutable, validated configuration, captured in saves. `Sustainable` enables explicit resource and extinction rules.
- `SettlementPolicies`: replace site scoring, progression and conflict resolution with game implementations. Inputs are snapshot records; use the supplied deterministic generator and never wall-clock time or external randomness. Policy state travels through `ExtensionState` and `RestoreHandlers.SettlementPolicy`.
- `ISettlementDailyRule`: inject ordered daily effects for game-owned climate, demographic, production, disease, culture or government rules. Food/material changes cannot make stocks negative; births/deaths must preserve population bounds. The core counts births/deaths and performs extinction cleanup. Rules run before default daily production; disable default births through configuration if the game supplies its own demographic model. Rules may own state, must not be shared between simulations when mutable, and must encode that state for restoration.
- `SettlementTradeBridge`: optional physical food shipments using the existing `RegionalNetworkSystem`. Advance through the bridge when using shipments. Destroyed destinations lose cargo on arrival and count it in FoodLost; delivery observes the configured storage cap. Pending cargo survives bridge saves. Routing and dispatch decisions belong to the game.
- `ReadEvents(cursor)`: typed foundation, upgrade, construction, alliance, war, peace, battle, destruction, refugee, resettlement and migration events. Read the returned NextCursor on the next call. Poll only after complete simulation steps; retain the cursor with engine state.

```csharp
var sim = new SettlementSimulation(42, rules: SettlementRules.Sustainable);
sim.Advance(500);
SimulationSave save = sim.CaptureState();
var restored = SettlementSimulation.Restore(save);
restored.Advance(500);
var batch = restored.ReadEvents();
foreach (var e in batch.Events)
    if (e is SettlementFounded founded) Console.WriteLine(founded.SettlementId);
```

Persist `SimulationSave` using your own JSON/binary/file layer. Never write into a live simulation to emulate restoration. If queued commands or actions are custom, supply their handlers. Unsupported or incomplete state is rejected instead of silently dropping commands.

Regenerate the public surface only after reviewing compatibility: `dotnet run --project tools/ApiSurface -c Release`. CI and tests compare it with the checked-in contract. The namespace remains flat to preserve source compatibility; splitting implementation files does not force games to change imports.
