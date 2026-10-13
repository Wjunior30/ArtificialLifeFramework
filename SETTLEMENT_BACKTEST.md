# Emergent settlement backtest

Deterministic, engine-neutral validation of the settlement lifecycle: site selection, camps, construction, urban stages, diplomacy, war, destruction, refugees and resettlement.

The default matrix runs six seeds in baseline, food-scarcity and high-hostility conditions for 3,650 simulated days. It audits non-negative state, unique site occupation, ownership consistency, survival, historical cycles and at least one complete camp-to-city progression.

Run:

```powershell
dotnet run --project tests/ArtificialLife.SettlementBacktest -c Release -- --days=3650
```

The JSON report is written to `artifacts/settlement-backtest/report.json`. This is a synthetic game-rule backtest, not a model of real history.

## Validated result

The reference run completed 18 scenarios and 65,700 simulated days with no invariant failures:

| Event | Count |
| --- | ---: |
| Settlements founded | 242 |
| Stage upgrades | 362 |
| Alliances | 88 |
| Wars | 237 |
| Battles | 396 |
| Settlements destroyed | 146 |
| Ruined sites resettled | 78 |
| Living cities at the end | 24 |

High hostility often converges to only one or two surviving settlements. Games using this module should therefore expose permanent hostility as an exceptional condition rather than the default diplomatic state.
