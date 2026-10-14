# Settlement backtest

A matriz oficial combina seis sementes (1, 7, 42, 100, 2026, 65535), cinco condições (normal, escassez, hostilidade, produção de comida zero, conflito zero) e três regras (Legacy, Negotiated, Sustainable): 90 cenários de 3.650 dias.

Cada dia audita conservação populacional, estado finito e não negativo, exclusividade territorial, relações diplomáticas, participantes de conflitos e desgaste. Ao final, verifica o histórico, eventos tipados, replay independente e duas restaurações seguidas de continuação. O relatório inclui séries anuais, mortalidade por mil habitantes-ano, duração média de guerras encerradas e perdas de recursos. Os denominadores populacionais acumulam a população existente no início de cada dia.

O cenário sem comida permite extinção total quando a regra explicitamente a habilita. O cenário sem conflito exige zero batalhas. Cada execução normal de dez anos precisa atingir uma cidade. Não se exige a mesma quantidade de cidades entre regras diferentes.

```powershell
dotnet run --project tests/ArtificialLife.SettlementBacktest -c Release -- --days=3650
```

O relatório `artifacts/settlement-backtest/report.json` usa esquema 3 e fingerprint versão 3. A execução completa compreende 328.500 auditorias diárias, 90 replays e 180 restaurações. `--days=90` executa a matriz curta e substitui o relatório; regenere a execução completa antes de atualizar a referência.

A implementação de 0.3.0 preserva o modo antigo, corrige migração que esvaziava assentamentos e acrescenta um preset opcional com extinção por fome, recursos limitados e tréguas maiores. Os relatórios da análise anterior em `artifacts/backtest-review-2026-10-13` são evidências históricas anteriores às correções, não a referência atual.

## Escala

`dotnet run --project tests/ArtificialLife.Benchmarks -c Release` mede 100 assentamentos, 100 mil habitantes agregados, mil anos simulados e 10 mil agentes individuais com decisões. Salva tempo, passos/segundo, alocações, memória retida, tamanho de snapshot e tempo de restauração mais recaptura. Cada caso também compara o estado restaurado e o original.

Os limites iniciais são 120 segundos de avanço por caso e 128 MiB por snapshot. São guardas contra regressões graves, não garantias de desempenho. As medições incluem runtime e sistema operacional; compare máquinas e condições equivalentes. Os mil anos usam uma região padrão, não a combinação simultânea de todas as escalas máximas. O histórico completo continua crescendo conforme eventos ocorrem.
