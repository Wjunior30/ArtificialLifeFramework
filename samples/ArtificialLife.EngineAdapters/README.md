# Engine integration examples

Build `ArtificialLife.Core` and `ArtificialLife.EngineAdapters` in Release. Reference the two DLLs in a C# game. Unity: copy UnitySettlementBehaviour.cs into Assets and attach it to a GameObject. Godot .NET: copy GodotSettlementNode.cs into the project, define GODOT if the project does not provide it, and attach it to a Node. Supply actual scene creation/removal in response to typed events.

SettlementSession compiles and is tested without engine dependencies. The conditional wrappers are provided as integration source: this repository does not install Unity or Godot, and those wrappers require validation inside the target editor. Use a C# language version that supports the sample syntax, or consume the compiled session DLL and adapt the small wrapper to the editor version.

Save/load uses the game's filesystem layer; the core never opens files. On loading, rebuild the scene from SettlementSession.Simulation.Settlements. The example resets its wall-clock remainder and event cursor, avoiding duplicate visual effects. For an exact host clock save use PortableSimulation and persist the adapter cursor separately.
