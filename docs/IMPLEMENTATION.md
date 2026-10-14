# Validação da implementação — 13/10/2026

Implementação local preparada como versão 0.3.0, preservando alterações anteriores do projeto.

## Entregue

- Correção da migração que deixava assentamentos ativos sem habitantes.
- Separação do ciclo regional por responsabilidade; contratos de extensão para território, conflitos, progressão e efeitos diários.
- Regras configuráveis e preset opcional para extinção por fome, armazenamento, perdas, manutenção e diplomacia negociada.
- Eventos tipados, leitura por cursor e integração de comida com caravanas existentes.
- Persistência de mundo, assentamentos, host portátil e transporte; codecs de extensões e migração de saves regionais antigos.
- Contrato público versionado e testado; política de compatibilidade e documentação atualizadas.
- Testes xUnit, backtest ampliado, medições de escala, exemplos de integração e consumidor do pacote real.
- Workflows de validação Windows/Linux e preparação/publicação manual de release.

## Resultado verificado localmente

| Verificação | Resultado |
| --- | --- |
| Compilação Release | Zero erros e avisos |
| xUnit | 25 testes aprovados, incluindo grupos que preservam as 125 verificações anteriores |
| Backtest | 90 cenários, 328.500 auditorias diárias, 90 replays e 180 restaurações; zero falhas |
| API pública | 1.000 entradas registradas, comparadas pela suíte |
| 100 assentamentos, 365 dias | Aproximadamente 0,154 s de avanço |
| 100 mil habitantes agregados, 365 dias | Aproximadamente 0,136 s |
| Região padrão, mil anos | Aproximadamente 2,003 s |
| 10 mil agentes individuais, 100 passos | Aproximadamente 1,074 s |
| Pacote e símbolos | Arquivos criados; DLL, XML, README, documentos e PDB conferidos |
| Consumo do pacote | Instalação em cache independente e execução de 1.000 dias com salvar/restaurar aprovadas |
| Diff | Sem erros de whitespace |

Os tempos são desta máquina Windows, com .NET 10.0.12 e 12 processadores lógicos; não são garantias para outras máquinas. Dados brutos: `artifacts/benchmarks/full.json` e `artifacts/settlement-backtest/report.json`.

Pacote: `artifacts/packages/Wjunior30.ArtificialLife.Core.0.3.0.nupkg`.
SHA-256: `27a50536e4a231b2447a15957f9b7e339a830a48dea92d09b67c47e5be1d0fd3`.

## Etapas externas não executadas

Os workflows foram escritos, mas não executados no GitHub nesta sessão; a comparação entre Linux e Windows depende dessa execução. As sessões comuns dos exemplos de engines foram compiladas/testadas, mas os pequenos wrappers condicionais não foram validados dentro dos editores Unity/Godot. Não houve commit, push, criação de tag ou publicação pública de pacote.

O proprietário escolheu manter o projeto sem licença. Nenhuma licença foi adicionada ou inferida. O workflow de publicação depende de execução manual, ambiente e credencial configurados pelo proprietário.
