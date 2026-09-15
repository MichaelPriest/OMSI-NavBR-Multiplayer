# Telemetria local do OMSI

## Objetivo

A integração usa um leitor externo e **somente leitura**. O NavBR não injeta DLL, não grava na memória do OMSI, não chama Steamworks e não precisa conhecer previamente o caminho de instalação antes de o processo iniciar.

Fluxo principal:

```text
Omsi.exe
  ↓ OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION)
ReadProcessMemory
  ↓
perfil de compatibilidade OMSI
  ↓
VehicleTelemetry
  ↓
WPF / GPS / HUD / multiplayer
```

## Compatibilidade atual

O perfil principal suportado tecnicamente é o **OMSI 2.3.004**.

A série 0.3 também possui suporte técnico ao perfil **2.2.032 (tram patch)**, porém esse perfil ainda precisa de validação real mais ampla.

O cliente calcula o SHA-256 do `Omsi.exe` para ajudar a diferenciar builds. A allowlist de hashes conhecidos ainda será consolidada depois dos testes reais.

Quando metadados do executável e runtime divergem, o NavBR também pode usar informações do `logfile.txt` como apoio de detecção.

Versões sem perfil correspondente podem ser detectadas como processo, mas a leitura estruturada de telemetria depende de um perfil compatível.

## Endereços e relocação

Os perfis guardam globais como **RVA**, não como endereço absoluto. A base preferencial histórica do executável 2.3.004 é `0x00400000`; no runtime o NavBR soma o RVA à base real retornada pelo Windows.

Globais utilizados pelo perfil 2.3.004 incluem:

| Dado | Endereço histórico | RVA |
| --- | ---: | ---: |
| Lista de veículos rodoviários | `0x00861508` | `0x00461508` |
| Índice do veículo do jogador | `0x00861740` | `0x00461740` |
| Ponteiro do mapa atual | `0x00861588` | `0x00461588` |

Estruturas utilizadas incluem:

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

## Valores publicados

A telemetria atual pode fornecer, conforme o perfil/runtime disponível:

- posição absoluta X/Y/Z;
- GridX/GridY e posição local do tile;
- direção/heading;
- velocidade em km/h;
- nome/nome amigável do mapa;
- timestamp UTC;
- estado de mapa carregado;
- linha/track ativa;
- destino;
- próxima parada quando a estrutura/timetable permite.

Linha, destino e próxima parada são consumidos também pelas camadas de timetable/navegação. O objetivo é manter offsets e estruturas de memória isolados da UI e do protocolo multiplayer.

## Relação com TTData

A telemetria identifica o estado atual da sessão. Para desenhar a rota ativa, o NavBR combina esse estado com arquivos do mapa:

```text
TTData / Chrono/*/TTData
  ↓
.ttp -> .ttr
  ↓
índice de tile -> global.cfg [map]
  ↓
tiles .map / splines .sli / crossings .sco
  ↓
traçado no GPS/HUD
```

O parser de rota possui diagnóstico próprio em:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-route.log
```

## Validação de runtime ainda necessária

Apesar de a leitura estar implementada, ainda não consideramos todos os pontos estáveis até completar testes reais:

1. confirmar posição/escala em mapas diferentes;
2. comparar velocidade calculada com o velocímetro do OMSI;
3. confirmar sinal e zero do heading;
4. validar destino e próxima parada em diferentes ônibus/mapas;
5. ampliar os testes do perfil 2.2.032;
6. formar uma allowlist de hashes conhecidos a partir das builds realmente testadas.

Até essa validação, os recursos correspondentes são classificados como **implementados, aguardando validação de runtime**.

## Evolução futura

Depois dos testes da alpha.9:

- consolidar hashes conhecidos;
- considerar signature scanning como fallback para builds futuras;
- ampliar perfis apenas quando houver evidência real de compatibilidade;
- manter qualquer integração com o OMSI em modo seguro e desacoplado.

## Referências técnicas

- `beispielsweise/OMSI-RouteAdvisor` — implementação externa de `ReadProcessMemory` e offsets de 2.3.004;
- `space928/Omsi-Extensions` / OmsiHook — nomes/offsets de estruturas, globals e exemplos de posição/velocidade/mapa.

O NavBR não incorpora OmsiHook como dependência. Ele implementa uma camada própria limitada a leitura.
