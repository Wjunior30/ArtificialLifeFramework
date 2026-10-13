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

## Assentamentos emergentes

`SettlementSimulation` oferece uma camada regional determinística e independente de engine para escolha de território, fundação de acampamentos, construção gradual, evolução até cidade, alianças, guerras, destruição, refugiados e reocupação de ruínas.

```csharp
var simulation = new SettlementSimulation(42);
simulation.Advance(3650);
simulation.Audit();
```

O backtest executa 18 cenários por dez anos simulados:

```powershell
dotnet run --project tests/ArtificialLife.SettlementBacktest -c Release
```

Detalhes: [`SETTLEMENT_BACKTEST.md`](SETTLEMENT_BACKTEST.md).


## API integrada

Use `PortableSimulation` como relogio fixo e fronteira da engine. `SimulationPresenter` envia snapshots para um adaptador de Godot, Unity, Unreal ou engine propria. Veja `MIGRATION.md` para a origem de cada sistema.


## Protocolo, snapshots e execução headless

O mesmo repositório também preserva o núcleo de compatibilidade validado do SimulationLife:

- `src/SimulationLife.Core`: simulação determinística, snapshots completos, restauração e hash SHA-256;
- `src/SimulationLife.Protocol`: contrato JSON versionado para qualquer linguagem ou engine;
- `src/SimulationLife.Headless`: processo independente que recebe um comando JSON por linha;
- `protocol/simulation-protocol.schema.json`: esquema público do protocolo;
- `examples`: mundo e comandos prontos para integração.

`ArtificialLife.Core` é a API recomendada para novos jogos. Os módulos `SimulationLife.*` preservam integrações e saves existentes enquanto seus contratos são incorporados gradualmente à API principal. Todo o código agora vive neste único repositório.

## Caminho para um framework completo

O projeto atual já é um núcleo determinístico reutilizável. Para se tornar um framework maduro, distribuível e seguro para terceiros, o desenvolvimento deve priorizar contratos estáveis, extensibilidade, persistência e distribuição antes de ampliar a quantidade de mecânicas.

### Arquitetura pública

- Definir quais tipos pertencem à API oficial e manter detalhes de implementação internos.
- Separar estado, regras e execução de agentes, economia, sociedade, assentamentos, diplomacia e conflitos.
- Dividir `SettlementSimulation` em sistemas menores para território, fundação, construção, migração, diplomacia e guerra.
- Preservar `LifeWorld`, `Agent`, `Economy`, `UtilityBrain`, snapshots e eventos como pontos de integração documentados.

Estrutura desejada:

```text
ArtificialLife.Core
├── Agents
├── Decisions
├── Economy
├── Society
├── Settlements
├── Diplomacy
├── Conflicts
├── Persistence
└── Diagnostics
```

### Extensibilidade

Jogos devem conseguir substituir regras sem modificar o núcleo:

```csharp
public interface ISettlementSiteEvaluator
{
    double Evaluate(SettlementSite site, SettlementContext context);
}

public interface IConflictResolver
{
    ConflictResult Resolve(ConflictContext context);
}

public interface ISettlementProgression
{
    SettlementStage Evaluate(SettlementState settlement);
}
```

As implementações padrão devem ser configuráveis e substituíveis. Isso também se aplica a demografia, produção, clima, migração, construção, governo, cultura e doenças.

### Regras configuráveis

Limiares e probabilidades não devem permanecer fixos no código:

```csharp
var rules = new SettlementRules
{
    FounderCount = 7,
    HamletPopulation = 8,
    VillagePopulation = 16,
    TownPopulation = 30,
    CityPopulation = 45,
    WarThreshold = -45,
};
```

O núcleo recebe objetos de configuração; aplicações externas podem carregá-los de JSON, banco de dados ou ferramentas de autoria.

### Persistência determinística

Snapshots precisam incluir agentes, assentamentos, territórios, relações, guerras, ruínas, histórico, próximo identificador e estado do gerador aleatório. A seguinte equivalência deve possuir teste automatizado:

```text
1.000 passos contínuos
        =
500 passos → salvar → restaurar → 500 passos
```

Snapshots devem ter versão de esquema e migração entre versões compatíveis.

### Eventos para engines

Godot, Unity, Unreal e processos headless devem integrar pelo mesmo fluxo de eventos tipados:

- `SettlementFounded`
- `SettlementUpgraded`
- `BuildingCompleted`
- `AllianceFormed`
- `WarDeclared`
- `BattleResolved`
- `SettlementDestroyed`
- `RefugeeGroupCreated`
- `RuinsResettled`

A engine apresenta o estado; as regras continuam independentes de renderização, física e sistema de arquivos.

### Testes e escala

- Adotar xUnit, NUnit ou MSTest para testes unitários rápidos.
- Manter backtests longos separados da suíte unitária.
- Testar determinismo, conservação de dinheiro, bens e população, ocupação territorial exclusiva, persistência e compatibilidade de snapshots.
- Medir 100 assentamentos, 10 mil agentes individuais, 100 mil agentes agregados e mil anos simulados.
- Registrar uso de memória, passos por segundo, tempo de snapshot e tempo de restauração.

### Distribuição

- Manter o framework em repositório Git próprio, com pull requests, tags e histórico.
- Adicionar `LICENSE`, `CHANGELOG.md`, versionamento semântico e política de compatibilidade.
- Produzir pacote NuGet com DLL, README, documentação XML, símbolos, licença e metadados do repositório.
- Configurar CI para compilação, testes, backtest curto, determinismo, benchmarks e criação do pacote.
- Manter exemplos de console, servidor headless, Godot e Unity.

### Ordem recomendada

1. Criar ou formalizar o repositório Git do framework.
2. Definir a API pública e ocultar detalhes internos.
3. Dividir a simulação de assentamentos em sistemas menores.
4. Introduzir regras configuráveis e interfaces de extensão.
5. Implementar snapshots completos e restauração determinística.
6. Adicionar eventos tipados para integração com engines.
7. Migrar testes unitários para um framework formal.
8. Criar benchmarks de escala e orçamentos de desempenho.
9. Adicionar licença, changelog e versionamento semântico.
10. Validar o pacote NuGet e exemplos de integração.
11. Automatizar releases e publicar a primeira versão `0.1.0`.

O próximo salto não depende de mais mecânicas. Ele depende principalmente de contratos públicos estáveis, modularidade, persistência e uma disciplina de distribuição verificável.
