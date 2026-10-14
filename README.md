# Artificial Life Framework

Biblioteca determinística e independente de engine para agentes, economia e sociedade. A API principal usa .NET Standard 2.1; ferramentas e testes usam .NET 10, e o protocolo de compatibilidade usa .NET 8.

## Estado atual — 0.3.0

Implementação local do plano de evolução do framework, incluindo os achados do backtest. O pacote é preparado localmente; publicação e validação dentro dos editores de engines são etapas externas separadas. O projeto permanece **sem licença**, por decisão do proprietário.

| Frente do plano | Implementação |
| --- | --- |
| Repositório próprio | Repositório Git existente preservado |
| API pública | Contrato em `docs/PublicApi.txt`, documentação e teste de mudanças |
| Modularidade | Território, população/construção, migração, diplomacia, conflitos e auditoria separados |
| Extensibilidade | Interfaces para território, progressão, conflitos e efeitos diários de regras do jogo |
| Configuração | Regras imutáveis validadas; migração, demografia, recursos e diplomacia configuráveis |
| Persistência | Mundo, host portátil, assentamentos e transporte; codecs de extensões; migração regional dos esquemas 1 e 2 para 3 |
| Eventos para engines | Eventos tipados com leitura por cursor, sem mudar o estado da simulação |
| Testes | xUnit, regressões antigas preservadas, saves antigos de referência e backtest separado |
| Escala | Cenários para 100 assentamentos, 10 mil agentes individuais, 100 mil agregados e mil anos |
| Distribuição | Metadados, XML, símbolos, changelog, pacote e exemplo que consome o pacote instalado |
| Automação | Workflows Windows/Linux, comparação de determinismo e preparação/publicação manual de release |
| Licença | Não adicionada, conforme escolha do proprietário |

## Estrutura

- `src/ArtificialLife.Core`: API principal, sem referências a Unity, Godot, renderização ou sistema de arquivos.
- `src/SimulationLife.Core`, `Protocol`, `Headless`: contratos preservados para integrações e saves existentes.
- `samples/ArtificialLife.Demo`: exemplo de console.
- `samples/ArtificialLife.EngineAdapters`: sessão comum compilável e exemplos de integração Unity/Godot.
- `samples/ArtificialLife.PackageSmoke`: validação consumindo o pacote NuGet real.
- `tests/ArtificialLife.Tests`, `SimulationLife.Core.Tests`: testes xUnit.
- `tests/ArtificialLife.SettlementBacktest`: cenários longos, auditoria diária e restauração.
- `tests/ArtificialLife.Benchmarks`: medições de memória, velocidade e snapshots.

## Uso

```csharp
var world = new LifeWorld(42);
var agent = Agent.Create(world.Ids.Next("agent"), "Ana");
world.AddAgent(agent);
world.Step(1);
```

Dinheiro usa centavos inteiros. Necessidades, bens e ações usam identificadores textuais. `PortableSimulation` fornece relógio fixo, perfis, comandos e snapshots; `SimulationPresenter` conecta a apresentação da engine.

## Assentamentos, regras e extensões

```csharp
var sim = new SettlementSimulation(42, rules: SettlementRules.Sustainable);
sim.Advance(500);
var save = sim.CaptureState();
var restored = SettlementSimulation.Restore(save);
restored.Advance(500);
var batch = restored.ReadEvents();
```

`new SettlementRules()` preserva diplomacia Legacy, estoques ilimitados e proteção do último habitante contra fome para compatibilidade. Para novos jogos, o preset recomendado `Sustainable` habilita diplomacia negociada, extinção por fome, limites de estoque, perdas, manutenção, guerras mais curtas e tréguas maiores. Regiões extintas podem receber população externa explicitamente com `AdmitExternalMigrants`, mantendo a conservação populacional auditável.

A migração exige moradores suficientes para o grupo e para a população remanescente. Os limites e probabilidades são configuráveis. `SettlementPolicies` permite substituir `ISettlementSiteEvaluator`, `IConflictResolver` e `ISettlementProgression`. `ISettlementDailyRule` permite efeitos determinísticos adicionais de clima, demografia, produção, doenças, cultura e governo sem incorporar essas mecânicas obrigatoriamente ao núcleo. As extensões devem fornecer estado serializável e codecs de restauração.

`SettlementTradeBridge` integra comida dos assentamentos emergentes às caravanas físicas de `RegionalNetworkSystem`. O jogo decide rotas e despachos; o núcleo conserva a carga no despacho, acompanha a entrega e contabiliza perdas em destinos destruídos ou sem capacidade. Use o avanço do bridge enquanto houver transporte em curso.

## Persistência e compatibilidade

`CaptureState`/`Restore` salvam o mundo, assentamentos ou host portátil. O host inclui acumulador de tempo, perfis, ajuda social, caravanas e comandos pendentes. Observadores e cenas pertencem à engine e devem ser reconectados após carregar.

Saves regionais usam esquema 3 e migram os esquemas 1 e 2. Fingerprints regionais usam versão 3; hashes antigos não são comparáveis. Estado de extensões exige handlers. O checksum detecta corrupção acidental, sem autenticar a origem do arquivo. Consulte [API](docs/API.md), [compatibilidade](docs/COMPATIBILITY.md) e [matriz de migração](MIGRATION.md).

Compras validam contas, saldo, imposto e capacidade antes da transferência. Impostos de venda aceitam 0–10.000 pontos-base; transações gratuitas e imposto de 100% são suportados. Ajuda concluída não pode ser entregue novamente. Tempos e multiplicadores precisam ser finitos. `PortableSimulation.Advance` aceita até 100 mil passos por chamada; entradas recusadas não alteram o acumulador. Coleções regionais são somente para leitura.

## Capacidades de framework

A fachada `ArtificialLife.CreateWorld` / `CreateSettlements` cobre a inicializacao comum sem esconder os construtores avancados. `SettlementConfiguration` permite configurar regras, opcoes e politicas em um unico ponto.

O nucleo tambem oferece:

- `SimulationScheduler`, com ordem deterministica e frequencias independentes;
- `DiagnosticBuffer` e `SimulationTelemetry`, opcionais e fora do estado deterministico;
- `ExplainRelationship` e `GetWarPlan`, para interfaces explicarem diplomacia e objetivos de guerra;
- `PopulationMaterialization`, para expandir coortes em agentes individuais e agrega-los novamente conservando populacao e bens;
- `SettlementSnapshots`, para checkpoints incrementais por replay deterministico;
- `SnapshotAuthentication`, para HMAC-SHA256 opcional com chave mantida pelo jogo;
- categorias de teste (`Hosting`, `Persistence`, `Diagnostics` e `Diplomacy`) e contrato publico revisado em `docs/PublicApi.txt`.

Esses servicos sao dependency-free: podem ser registrados em qualquer container de injecao de dependencia por factories, sem obrigar o consumidor a instalar um container especifico. Unity e Godot usam a sessao comum em `samples/ArtificialLife.EngineAdapters`; a compilacao e os testes automatizados validam essa fronteira. A execucao dentro dos editores continua sendo uma validacao externa, pois os SDKs dos editores nao fazem parte deste repositorio.

## Roadmap — mundo realmente vivo

O framework já sustenta um mundo emergente: assentamentos nascem, crescem, negociam, entram em guerra, são destruídos e podem ser reconstruídos sem roteiro fixo. As atividades abaixo representam as próximas camadas necessárias para que pessoas, organizações e cidades também transmitam identidade, memória e intenção ao jogador.

### Prioridade 1 — vida individual e continuidade

- [ ] Implementar idade, nascimento, infância, vida adulta, velhice e morte natural.
- [ ] Implementar famílias, parentesco, casamento, separação, herança e descendência.
- [ ] Implementar profissões, habilidades, experiência, local de trabalho e mudança de ocupação.
- [ ] Implementar objetivos pessoais de curto e longo prazo, com prioridades concorrentes.
- [ ] Fazer necessidades, personalidade, emoções, relações e memórias influenciarem decisões reais.
- [ ] Preservar identidade e estado ao converter uma população agregada em agentes individuais e novamente em agregado.

### Prioridade 2 — sociedade e organizações

- [ ] Implementar grupos, guildas, empresas, exércitos, partidos, famílias influentes e organizações religiosas.
- [ ] Implementar entrada, saída, cargos, hierarquia, patrimônio, objetivos e conflitos internos das organizações.
- [ ] Implementar governos, líderes, eleições, sucessão, golpes e disputas pelo poder.
- [ ] Implementar leis, impostos, propriedade, crimes, punições, guardas, prisões e reputação pública.
- [ ] Implementar facções, revoltas, protestos, guerras civis e independência de territórios.

### Prioridade 3 — economia viva

- [ ] Implementar empregos, salários, contratos, empresas e produção baseada em trabalhadores e ferramentas.
- [ ] Implementar preços formados por oferta, procura, estoque, distância, risco e impostos.
- [ ] Implementar mercados locais, comércio entre cidades, caravanas, bloqueios e rotas comerciais.
- [ ] Implementar propriedade de casas, terras, oficinas, fazendas e recursos naturais.
- [ ] Implementar pobreza, riqueza, dívida, falência, desigualdade e mobilidade econômica.
- [ ] Garantir conservação auditável de população, dinheiro, mercadorias e propriedade.

### Prioridade 4 — cultura, conhecimento e história

- [ ] Implementar culturas, idiomas, religiões, costumes, valores e identidades regionais.
- [ ] Implementar transmissão cultural entre gerações e influência entre cidades vizinhas.
- [ ] Implementar conhecimento, pesquisa, invenções, educação e evolução tecnológica.
- [ ] Fazer eventos históricos criarem memórias coletivas, feriados, rivalidades e alianças duradouras.
- [ ] Gerar nomes, biografias e crônicas determinísticas para pessoas, famílias, cidades e organizações.

### Prioridade 5 — política, diplomacia e guerra

- [ ] Implementar território contínuo, fronteiras, reivindicações e ocupação de áreas do mapa.
- [ ] Implementar tratados, pactos defensivos, comércio, tributos, vassalagem, sanções e garantias.
- [ ] Transformar objetivos de guerra em condições concretas de vitória, negociação e paz.
- [ ] Implementar exércitos, recrutamento, logística, moral, comandantes, cercos e controle territorial.
- [ ] Fazer diplomacia considerar história, cultura, economia, ameaças, personalidade dos líderes e opinião pública.

### Prioridade 6 — ambiente e crises

- [ ] Implementar estações, clima, secas, enchentes, incêndios e desastres naturais.
- [ ] Implementar doenças, contágio, imunidade, tratamento e impacto demográfico.
- [ ] Implementar recursos renováveis e esgotáveis, degradação ambiental e recuperação.
- [ ] Permitir migração externa e repovoamento configuráveis após colapsos regionais.
- [ ] Fazer cidades planejarem reservas e reagirem antecipadamente a riscos conhecidos.

### Prioridade 7 — percepção pelo jogador e engines

- [ ] Criar consultas explicáveis: “por que este agente decidiu isso?” e “por que esta cidade entrou em guerra?”.
- [ ] Criar linha do tempo pesquisável para pessoas, famílias, organizações e cidades.
- [ ] Criar níveis de detalhe para simular de forma agregada o que está longe e individualmente o que está próximo.
- [ ] Validar os adaptadores dentro de projetos reais Unity e Godot, incluindo save/load e execução prolongada.
- [ ] Criar exemplos separados para cidade, família, economia, política, guerra, doença e desastre.

### Requisitos obrigatórios para cada atividade

Uma atividade só pode ser marcada como concluída quando:

1. possuir contrato público extensível sem exigir alteração do núcleo pelo jogo consumidor;
2. preservar determinismo para a mesma semente, configuração e sequência de comandos;
3. possuir persistência versionada e migração dos saves anteriores quando necessário;
4. incluir eventos tipados e explicação das decisões relevantes;
5. possuir testes unitários, integração, replay e restauração de snapshots;
6. entrar no backtest com cenários normais, extremos e de longa duração;
7. manter população, dinheiro, bens e demais recursos auditáveis;
8. ter documentação e exemplo de uso independente de engine;
9. respeitar o orçamento de desempenho para agentes individuais e populações agregadas;
10. atualizar `docs/PublicApi.txt`, compatibilidade e changelog.

### Ordem recomendada

1. Ciclo de vida, famílias e profissões.
2. Organizações e governos.
3. Economia dinâmica e propriedade.
4. Cultura, conhecimento e memória histórica.
5. Territórios, diplomacia avançada e guerra.
6. Ambiente, doenças e crises.
7. Integração visual e validação em Unity/Godot.

## Compilar e validar

```powershell
dotnet restore ArtificialLifeFramework.slnx
dotnet build ArtificialLifeFramework.slnx -c Release --no-restore
dotnet test ArtificialLifeFramework.slnx -c Release --no-build
dotnet run --project tests/ArtificialLife.SettlementBacktest -c Release --no-build -- --days=3650
dotnet run --project tests/ArtificialLife.Benchmarks -c Release --no-build
```

O backtest combina seis sementes, cinco condições e três conjuntos de regras: **90 cenários**. O modo completo audita **328.500 dias**, compara **90 replays** e verifica **180 restaurações**. Relatório em `artifacts/settlement-backtest/report.json`. Para verificações curtas, use `--days=90`; benchmarks aceitam `--smoke`. Detalhes em [SETTLEMENT_BACKTEST.md](SETTLEMENT_BACKTEST.md).

## Pacote e distribuição

```powershell
dotnet pack src/ArtificialLife.Core -c Release --no-build -o artifacts/packages
dotnet restore samples/ArtificialLife.PackageSmoke --source artifacts/packages
dotnet run --project samples/ArtificialLife.PackageSmoke -c Release --no-restore
```

O pacote e símbolos ficam em `artifacts/packages`. Não foi declarada licença. Os workflows fornecem compilação/testes nas duas plataformas, comparação dos estados finais, benchmarks e validação do pacote. A publicação exige execução manual do workflow de release, opção `publish`, ambiente `nuget` e segredo `NUGET_API_KEY` configurados pelo proprietário. Nenhuma publicação ocorre por um push comum.

Os exemplos de Unity/Godot incluem código de integração e uma sessão independente testada. Validação no editor e implementação visual pertencem ao projeto consumidor; não são declaradas como executadas neste repositório.
