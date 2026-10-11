# Migration matrix

This repository is the engine-neutral destination for simulation code.

| Source | Migrated capability | Destination |
|---|---|---|
| ArtificialLife | needs, memory, emotions, relationships, stimuli, utility AI | `Agents.cs`, `Decisions.cs` |
| SimulationLife `npc.gd` | occupations, skills, traits, body/injuries, goals, knowledge, action commitment | `AdvancedAgents.cs` |
| SimulationLife `world.gd` | fixed-step autonomous decisions, emergencies, scoring and switching margin | `AdvancedAgents.cs`, `Simulation.cs` |
| SimulationLife economy | cent ledger, goods, recipes, firms, payroll, markets, taxes, credit, arrears, default, benefits and inheritance | `Economy.cs`, `EconomicSystems.cs`, `AdvancedEconomy.cs` |
| SimulationLife society | households, physical aid delivery, labor/training, social tasks | `Society.cs`, `SocialSystems.cs` |
| SimulationLife regions | settlements, cargo caravans and physical imports/exports | `SocialSystems.cs` |
| SimulationLife experiments | paired seeds, policy models and comparable metrics | `SocialSystems.cs` |

Rendering, Godot nodes, UI, file dialogs and engine physics remain adapters owned by each game.

## Final consolidation

The complete portable snapshot, validation, JSON protocol and headless host from `SimulationLifeCore` were copied into this repository without changing their public contracts. This makes `ArtificialLifeFramework` the single preservation point for both the rich artificial-life model and the hardened cross-engine process protocol. The former `SimulationLifeCore` repository is now redundant after this commit is backed up remotely.
