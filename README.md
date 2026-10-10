# Artificial Life Framework

Biblioteca deterministica e independente de engine para agentes, economia e sociedade.

## Origem dos sistemas

- **ArtificialLife:** necessidades, memoria, emocoes, relacionamentos, percepcao e Utility AI.
- **SimulationLife:** economia contabil, producao, familias, ajuda social e simulacao em passos fixos.

O nucleo nao referencia Unity, Godot, renderizacao, fisica ou sistema de arquivos. Engines integram por comandos, eventos e snapshots.

## Estrutura

- `src/ArtificialLife.Core`: biblioteca portatil `netstandard2.1`.
- `samples/ArtificialLife.Demo`: simulacao executavel de exemplo.
- `tests/ArtificialLife.Tests`: verificacoes deterministicas e invariantes economicas.

## Uso

```csharp
var world = new LifeWorld(42);
var agent = Agent.Create(world.Ids.Next("agent"), "Ana");
world.AddAgent(agent);
world.Step(1);
```

Dinheiro usa centavos inteiros. Necessidades, bens e acoes usam identificadores textuais para que cada jogo possa adicionar regras sem alterar a biblioteca.


## API integrada

Use `PortableSimulation` como relogio fixo e fronteira da engine. `SimulationPresenter` envia snapshots para um adaptador de Godot, Unity, Unreal ou engine propria. Veja `MIGRATION.md` para a origem de cada sistema.

