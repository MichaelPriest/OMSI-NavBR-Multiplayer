# Telemetria local do OMSI 2.3.004

## Objetivo

A Fase 1 usa um leitor externo e **somente leitura**. O NavBR não injeta DLL, não chama Steamworks e não precisa conhecer onde o OMSI foi instalado antes de o processo iniciar.

Fluxo:

```text
Omsi.exe
  ↓ OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION)
ReadProcessMemory
  ↓
perfil OMSI 2.3.004
  ↓
VehicleTelemetry
  ↓
WPF / GPS / multiplayer
```

## Compatibilidade

O perfil inicial é exclusivo para OMSI `2.3.004`. O cliente calcula também o SHA-256 do `Omsi.exe`, que servirá para diferenciar builds que eventualmente informem a mesma versão de arquivo mas tenham layout interno diferente.

Versões desconhecidas são detectadas normalmente, porém a leitura de telemetria não é iniciada até que exista um perfil correspondente.

## Endereços e relocação

O perfil guarda globais como **RVA**, não como endereço absoluto. A base preferencial do executável histórico é `0x00400000`; no runtime o NavBR soma o RVA à base real retornada pelo Windows.

Globais utilizados no perfil 2.3.004:

| Dado | Endereço histórico | RVA |
| --- | ---: | ---: |
| Lista de veículos rodoviários | `0x00861508` | `0x00461508` |
| Índice do veículo do jogador | `0x00861740` | `0x00461740` |
| Ponteiro do mapa atual | `0x00861588` | `0x00461588` |

Estruturas utilizadas:

- `OmsiMapObjInst.Position`: `+0x004`;
- `OmsiMapObjInst.Rotation`: `+0x050`;
- `OmsiMapObjInst.AbsPosition`: `+0x078`;
- tradução da `D3DMatrix`: `_30/_31/_32`, a partir de `+0x030` dentro da matriz;
- `OmsiPhysObjInst.Velocity`: `+0x1C0`;
- `OmsiMovingMapObjInst.Groundspeed`: `+0x428`;
- `OmsiMap.Loaded`: `+0x120`;
- `OmsiMap.Name`: `+0x150`;
- `OmsiMap.FriendlyName`: `+0x158`.

A resolução do veículo segue a lista interna `TMyOMSIList`/`TList`: lista global → `FList +0x28` → itens `+0x4` → item pelo índice do jogador.

## Valores publicados pelo provider

O primeiro provider entrega:

- posição absoluta X/Y/Z;
- direção em graus derivada do quaternion do veículo;
- velocidade em km/h derivada do vetor de velocidade (com `Groundspeed` como fallback);
- nome amigável/nome do mapa;
- timestamp UTC;
- indicador de mapa carregado.

Linha, rota e nome amigável do veículo ficam para as próximas etapas, porque exigem leitura do timetable/estrutura do veículo e devem ser implementados sem misturar responsabilidades.

## Validação de runtime ainda necessária

O layout foi baseado em estruturas publicamente documentadas/reverse-engineered para OMSI 2.3.004, mas três pontos precisam de teste em jogo antes de serem considerados estáveis:

1. confirmar que a posição absoluta acompanha o ônibus corretamente em mapas diferentes;
2. comparar velocidade calculada com o velocímetro do OMSI;
3. confirmar sinal e zero do heading (N/E/S/W) para o renderer do GPS.

Até essa validação, o provider é marcado como **implementado, aguardando validação de runtime**.

## Referências técnicas

- `beispielsweise/OMSI-RouteAdvisor` — implementação externa de `ReadProcessMemory` e offsets de 2.3.004;
- `space928/Omsi-Extensions` / OmsiHook — nomes/offsets de estruturas, globals e exemplos de posição/velocidade/mapa.

O NavBR não incorpora OmsiHook como dependência nesta fase; ele implementa uma camada própria limitada a leitura.
