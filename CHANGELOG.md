# Changelog

## Unreleased

### 0.3.0 implementation

- Fix configurable migration groups emptying live settlements; add permanent regression cases.
- Separate settlement territory, population, migration, diplomacy, conflicts and audits; publish replaceable site, progression, conflict and daily-effect contracts.
- Add typed settlement events with non-mutating cursors, including construction, refugees and resettlement.
- Complete PortableSimulation persistence, including fractional time, profiles, aid, cargo and pending custom commands.
- Write regional save schema 2, migrate schema 1, test authentic old fixtures, and version the regional fingerprint as 3.
- Add opt-in Sustainable rules for extinction, bounded resources, spoilage, maintenance and negotiated diplomacy.
- Integrate emergent food transport with existing physical caravans and save cargo in transit.
- Migrate existing checks to xUnit and add independent feature, compatibility and integration tests.
- Expand backtest to 90 cases with daily audits, full-state replay, restore continuation and demographic metrics.
- Add four scale measurements, a public API contract, compatibility policy, engine adapter examples, package consumer and CI/release workflows.
- Prepare version 0.3.0 NuGet and symbols locally. No license is added, per owner instruction; no publication is implied.

### Earlier integrity work

- Reject invalid market operations before applying any payment or goods transfer, including insufficient total balance, missing accounts, invalid tax rates and inventory overflow.
- Support free trades and 100% sales tax without attempting zero-value ledger transfers.
- Prevent repeated deliveries for completed household care requests.
- Reject non-finite simulation time and settlement multipliers; limit portable advancement to 100,000 steps per call and multipliers to [0, 1000].
- Prevent destroyed settlements from participating in subsequent battles or daily migration/foundation.
- Track births, audit population conservation and finite numeric state, and verify every simulated day in the settlement backtest.
- Compare an independent deterministic replay and validate settlement event participants in the backtest.
- Include complete regional state in fingerprint version 2; update the reference report to schema 2.
- Avoid creating presentation snapshots when no observers are registered.
- Report the actual number of core checks and add regression coverage for the corrected failures.

Compatibility: settlement collections expose `IReadOnlyList<T>` instead of mutable `List<T>`. Callers that mutated these collections must stop doing so and rebuild against the new API. Regional fingerprints use version 3; save and API migration details are recorded in `docs/COMPATIBILITY.md`.
