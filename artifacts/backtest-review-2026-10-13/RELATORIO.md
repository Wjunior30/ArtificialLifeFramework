# Backtest e melhorias — 13/10/2026

## Conclusão

O comportamento padrão passou nos testes de integridade e determinismo. A persistência regional também preservou o estado e a evolução nos pontos testados. Entretanto, uma configuração aceita pela API produziu um assentamento ativo sem habitantes no dia 28. Há ainda limitações de equilíbrio: sobrevivência indefinida sem comida, concentração de assentamentos sob hostilidade e grandes estoques acumulados.

Esta execução avalia o código local, incluindo alterações anteriores ainda sem commit. Não foram alteradas as regras de produção para corrigir os achados.

## Execução e escopo

- Compilação Release da solução: zero erros e zero avisos, com as dependências já restauradas (`--no-restore`). A tentativa inicial de restaurar encontrou acesso negado à configuração pessoal do NuGet.
- ArtificialLife.Tests: 96 verificações, zero falhas.
- SimulationLife.Core.Tests: 29 verificações, zero falhas.
- Backtest oficial: 18 cenários, seis sementes, 3.650 dias por cenário; 65.700 dias auditados e replay independente, zero falhas.
- Diagnóstico ampliado: 60 cenários, 219.000 auditorias diárias, 60 replays comparando o hash completo e 120 restaurações nos dias 500 e 1.825 seguidas de avanço até o dia 3.650; zero falhas.
- O diagnóstico ampliado combina duas diplomacias (Legacy/Negotiated), cinco condições (normal, escassez, hostilidade, produção de comida zero e conflito zero) e as mesmas seis sementes. Inclui os 18 casos oficiais; não representa 78 casos únicos.
- Caso adicional de regras personalizadas: uma falha reproduzida no dia 28.

O teste de persistência cobre SettlementSimulation. Não valida toda a persistência de LifeWorld, extensões de ações/comandos, arquivos adulterados, compatibilidade entre versões ou integração com engines.

## Resultado do backtest oficial

| Métrica | Total nas 18 execuções |
| --- | ---: |
| Assentamentos fundados | 237 |
| Evoluções de estágio | 357 |
| Alianças | 76 |
| Guerras | 246 |
| Batalhas | 387 |
| Assentamentos destruídos | 142 |
| Reocupações de ruínas | 76 |
| Cidades ao final | 23 |

## Comparação entre diplomacias

Cada linha agrega seis sementes de dez anos. Assentamentos e população são médias finais; mortes, destruições e acordos de paz são totais acumulados.

| Condição | Diplomacia | Assentamentos | População | Mortes | Destruições | Acordos de paz |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Normal | Legacy | 8,17 | 193,17 | 934 | 47 | 0 |
| Normal | Negotiated | 11,33 | 256,33 | 1.071 | 33 | 77 |
| Escassez | Legacy | 5,83 | 121,33 | 955 | 42 | 0 |
| Escassez | Negotiated | 7,67 | 153,83 | 1.086 | 38 | 45 |
| Hostilidade | Legacy | 1,83 | 51,33 | 502 | 53 | 0 |
| Hostilidade | Negotiated | 2,67 | 61,17 | 622 | 53 | 20 |

A diplomacia negociada preservou mais assentamentos e população ao final nesta amostra. Não reduziu as mortes absolutas ou o número de batalhas: no cenário normal, as batalhas passaram de 178 para 208; no hostil, de 97 para 120. Como as trajetórias populacionais diferem, esses totais não estabelecem uma taxa de mortalidade maior. É necessário medir mortes por habitante-ano antes de concluir sobre segurança ou equilíbrio.

## Melhorias prioritárias

### 1. Alta — impedir migração que esvazia o assentamento

Configuração válida: `FounderCount = 80`, `InitialPopulation = 240`, `BirthProbability = 0`, `ProsperityBirthBonus = 0`, semente 42, multiplicadores de comida 1 e conflito 0. No dia 28, a auditoria retorna `Settlement 2 has no population.`

Em `RedistributePopulation`, a condição exige população >= 42, mas a retirada usa o FounderCount configurável. Um assentamento com 80 habitantes pode perder os 80 e continuar ativo. Tornar o mínimo de população remanescente explícito e exigir população suficiente para o grupo migrante; ou implementar abandono completo como transição válida. Acrescentar esse caso à suíte permanente.

### 2. Alta para coerência da simulação — definir extinção e abandono por fome

Com produção de comida zero e conflito zero, todas as 12 execuções (seis sementes, dois modos) terminam com seis assentamentos e seis habitantes após dez anos. A mortalidade por fome é limitada a `population - 1`, protegendo o último habitante indefinidamente.

Se essa proteção for desejada, documentá-la como regra de jogo configurável. Caso contrário, permitir morte do último habitante e atualizar ocupação, histórico, relações e eventual abandono. Testar os ciclos de extinção e repovoamento sem exigir artificialmente um assentamento sobrevivente.

### 3. Média — calibrar diplomacia usando medidas populacionais

No modo hostil, Legacy termina com 1–3 assentamentos por semente; Negotiated, com 2–4. Ambos acumulam 53 destruições. Tréguas já ajudam na sobrevivência final, mas precisam de calibração de reincidência, custo da guerra e recuperação das relações.

Adicionar mortalidade por habitante-ano, dias em guerra, tempo até nova guerra e sobrevivência por assentamento. Comparar distribuições em mais sementes antes de mudar o padrão para Negotiated.

### 4. Média — dar finalidade e limites aos estoques

No cenário sem conflito, o maior estoque observado em um assentamento ultrapassou 61 mil unidades de comida; houve 170 mortes por fome somadas nas seis sementes, em cada modo. São observações agregadas ao longo do tempo: não demonstram que os estoques e as mortes coexistiram no mesmo dia.

Adicionar medições por local e por dia para investigar distribuição; considerar transporte e comércio regional de alimentos, armazenamento, perdas e manutenção. Materiais também acumularam dezenas de milhares de unidades, sugerindo necessidade de usos adicionais após a construção.

### 5. Média — incorporar a cobertura ampliada ao teste oficial

O backtest oficial usa apenas o modo Legacy e não testa restauração. Incorporar Negotiated, continuidade após save, condições extremas e variação de regras. Incluir casos de saves inválidos, persistência de LifeWorld e extensões, além de verificar conflitos ativos e desgaste de guerra na auditoria principal.

Os requisitos agregados de evolução podem esconder sementes problemáticas. Definir critérios por cenário e registrar séries temporais para explicar colapsos.

### 6. Média — versionar o novo fingerprint e atualizar a documentação

O fingerprint regional atual deriva do hash SHA-256 do estado serializado, incluindo regras e conflitos. O relatório continua declarando `fingerprintVersion = 2`, embora a implementação anterior em desenvolvimento utilizasse outro cálculo. Versionar explicitamente o contrato antes de comparar novas referências com relatórios anteriores.

README e CHANGELOG também precisam refletir que já existe persistência regional em desenvolvimento, distinguindo o que foi testado do que ainda falta. Medir desempenho e crescimento do histórico em mapas maiores; esta execução usa os 18 locais padrão e não certifica escala máxima.

## Artefatos e reprodução

- `../settlement-backtest/report.json`: relatório oficial regenerado.
- `diagnostics.json`: 60 cenários e verificações de persistência.
- `custom-rules.json`: configuração e estado da falha do dia 28.
- `Program.cs` e `Diagnostics.csproj`: diagnóstico reproduzível, referenciando a biblioteca compilada em Release.

Executar da raiz do repositório:

```powershell
dotnet build ArtificialLifeFramework.slnx -c Release --no-restore
dotnet run --project tests/ArtificialLife.Tests -c Release --no-build
dotnet run --project tests/SimulationLife.Core.Tests -c Release --no-build
dotnet run --project tests/ArtificialLife.SettlementBacktest -c Release --no-build -- --days=3650
dotnet restore artifacts/backtest-review-2026-10-13/Diagnostics.csproj --configfile artifacts/backtest-review-2026-10-13/NuGet.Config
dotnet run --project artifacts/backtest-review-2026-10-13/Diagnostics.csproj -c Release --no-restore
dotnet run --project artifacts/backtest-review-2026-10-13/Diagnostics.csproj -c Release --no-restore -- --custom-only
```

O último comando deve retornar código 1 enquanto a falha de migração existir. Nesta sessão, o restore do diagnóstico usou APPDATA apontando temporariamente para sua pasta `obj/appdata` para evitar ler a configuração pessoal inacessível. Nenhuma dependência externa é necessária para esse diagnóstico.

Os resultados são de regras sintéticas de jogo e de seis sementes. Integridade numérica e reprodução determinística não demonstram realismo, equilíbrio de jogo ou ausência de outros defeitos.
