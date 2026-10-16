# IslandLife por dados

Uma guerra de facções inteira (facções, tipos de lugar e equilíbrio) descrita só em JSON. O mesmo motor roda a ilha do SimulationLife e qualquer outro mundo.

| Arquivo | O que é |
|---|---|
| `island-default.json` | A ilha do jogo: 5 facções militares, nômades e mortos-vivos (igual a `IslandLifeConfig.Default()`). |
| `continent-three-kingdoms.json` | Outro jogo: continente com 3 reinos, castelos, aldeias e pedreiras, sem nômades nem mortos-vivos. É testado em `OtherWorldTests`. |

## Usar

```csharp
// .NET: System.Text.Json precisa de IncludeFields
var config = JsonSerializer.Deserialize<IslandLifeConfig>(json, new JsonSerializerOptions { IncludeFields = true });
// Unity: JsonUtility lê direto (campos públicos e [Serializable])
var config = JsonUtility.FromJson<IslandLifeConfig>(json);

var life = new IslandLife(seed, places, routes, config, forgeNames: true);
```

No SimulationLife basta salvar o arquivo como `Assets/Resources/IslandLifeConfig.json`. O `FactionSystem` usa esse arquivo no lugar do padrão.

Cada `WorldPlace.Kind` procura sua regra em `Places`:
- `Production`: comida por hora;
- `Capacity`: 0 usa o tier (18, 10 ou 5);
- `Lair`: ninho de monstros;
- `CapitalCandidate`: pode ser capital.

## Enums (gravados como números, como o JsonUtility exige)

- **Ideology:**
  - 0 Capitalism
  - 1 Socialism
  - 2 Communism
  - 3 Theocracy
  - 4 Feudalism
  - 5 Anarchism
  - 6 Authoritarian
  - 7 Mercantile
  - 8 Monstrous
- **ConquestPolicy:**
  - 0 Integrate
  - 1 Exploit
  - 2 Convert
  - 3 Execute
  - 4 Liberate
- **NameCulture:**
  - 0 Iron
  - 1 Ash
  - 2 Tide
  - 3 Free
  - 4 Veil
  - 5 Road
  - 6 Common

## Salvar e carregar

`life.Save()` devolve um `IslandLifeState`, que é autocontido: guarda a config, os lugares, as rotas, as pessoas, os grupos e o gerador aleatório. `IslandLife.Load(state)` continua a simulação exatamente de onde parou. Os testes `SavedWarLoadsFromJsonAndContinuesIdentically` e `ContinentWithThreeKingdomsRunsFromDataAlone` confirmam isso.

## Eventos traduzíveis

Cada `IslandEvent` traz `Kind`, `FactionA`, `FactionB`, `Place` e `Subject`. O texto padrão está em português. Para outro idioma, defina `life.Localize = e => ...`; quando a função devolve `null`, o texto padrão é mantido.
