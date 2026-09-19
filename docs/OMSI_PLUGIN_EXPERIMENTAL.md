# Plugin OMSI experimental — Alpha.14

O plugin de escrita física continua **experimental, opt-in e focado no OMSI 2.3.004**.

## Bridge v3

- named pipe: OMSI.NavBR.Multiplayer.Plugin.v3;
- ABI/state version 3;
- plugin Native AOT x86;
- capacidades atualizadas em runtime;
- capability `vehicle-interpolation` para ônibus remoto físico suavizado;
- cliente e plugin de gerações antigas são deliberadamente incompatíveis.

## Ônibus remoto físico

O cliente possui um coordenador único para spawn/update/despawn.

Antes de escrever no OMSI, valida:

- opt-in;
- bridge conectado;
- capabilities necessárias;
- jogador remoto em jogo;
- identidade real do veículo;
- mapa/protocolo compatíveis;
- limites de segurança.

### Movimento remoto suavizado

A PR #30 passa a tratar os frames recebidos como alvos de movimento em vez de teletransportar o ônibus a cada pacote:

- interpolação adaptativa executada no callback do próprio OMSI, limitada a aproximadamente 60 Hz;
- posição, quaternion e velocidade são interpolados;
- quaternion usa nlerp normalizado com correção de hemisfério;
- frames antigos/fora de ordem são ignorados;
- saltos de 30 m ou intervalos superiores a 1,5 s são tratados como teleporte e aplicados por snap seguro;
- luzes e setas continuam sendo aplicadas imediatamente;
- luz de freio também pode ser inferida de `BrakePercent`;
- até duas falhas transitórias de update são toleradas antes de respawn; erros fatais de ownership/ponteiro continuam fail-safe;
- ônibus remotos só são materializados fisicamente quando estão próximos: spawn até 750 m e despawn acima de 1 km, com histerese para evitar churn na borda; jogadores fora desse raio continuam presentes normalmente no multiplayer;
- se app/pipe/rede desaparecer sem um despawn limpo, um alvo físico sem atualização por 5 s entra em limpeza automática; se o OMSI rejeitar a remoção, o ownership é preservado para nova tentativa;
- a fila do callback processa no máximo 1 comando arbitrário/pesado por frame e até 4 updates leves adicionais, elevando a fluidez com vários ônibus sem liberar bursts de spawn no mesmo frame;
- quando os 32 slots físicos estão ocupados, um jogador pelo menos 75 m mais próximo pode liberar assíncronamente o slot do ônibus físico mais distante; somente uma substituição ocorre por vez e o ônibus removido recebe cooldown de 2 s para não tomar a vaga imediatamente;
- acelerador, freio, combustível, luz externa, luz interna, luz de freio e setas agora são preenchidos pela telemetria read-only quando os offsets do perfil retornam valores válidos;
- a duração da interpolação acompanha a cadência real dos timestamps remotos, reduzindo pequenas pausas entre frames.

## Personagem / RP

A Alpha.14 também usa o plugin v3 para o modo Personagem/RP:

- seleciona personagem real da lista Drivers;
- salva vínculo/IA/pose;
- destaca o motorista do ônibus;
- aplica transform durante o controle;
- restaura estado ao retornar.

Controles iniciais: W/S, A/D, Shift e Esc.

## Segurança

Nenhum ponteiro vindo da rede é usado diretamente. Ponteiros físicos são descobertos e validados localmente.

Caminhos de veículos devem resolver para arquivos válidos dentro da instalação OMSI.

## Ainda experimental

- portas e matriz por modelo;
- articulação;
- câmera RP dedicada;
- terreno inclinado;
- animações/gestos;
- personagem remoto físico completo;
- compatibilidade ampla com outras versões do OMSI.


## Alpha.15 — state interop ABI v7 / Alpha.15 — state interop ABI v7

### Português (pt-BR)

A Alpha.15 exige state interop **v7**. O novo export `NavBR_ResolveMapTileIndex(gridX, gridY)` resolve uma identidade de tile estável para o índice/pointer de Kachel carregado no processo OMSI local.

`MapTileIndex` não deve ser tratado como identidade portátil entre dois clientes multiplayer: ele é índice da lista de Kacheln carregadas naquele processo.

O updater também compara o SHA-256 do bundle embarcado. Manifestos antigos sem fingerprint são considerados desatualizados.

### English (en)

Alpha.15 requires state interop **v7**. The new `NavBR_ResolveMapTileIndex(gridX, gridY)` export resolves a stable tile identity into the Kachel index/pointer loaded by the local OMSI process.

`MapTileIndex` must not be treated as a portable identity across multiplayer clients because it indexes the Kachel list of a specific OMSI process.

The updater also compares the embedded bundle SHA-256. Older manifests without a fingerprint are treated as outdated.


## Performance Guard / Proteção de desempenho

O Plugin Bridge mantém todo acesso nativo na thread de callback do OMSI, mas o
trabalho nessa thread agora é explicitamente limitado para não transformar o
multiplayer em uma fonte adicional de stutter:

- no máximo 12 ônibus remotos são materializados fisicamente; os demais
  continuam visíveis no HUD/mapa/rede sem criar outro RoadVehicle no OMSI;
- os 12 slots físicos priorizam os jogadores mais próximos;
- spawn ocorre até 500 m e o despawn usa histerese até 700 m;
- updates do bridge usam LOD por distância (mais frequentes perto do jogador);
- um spawn confirmado não recebe um update duplicado no mesmo frame;
- ônibus cuja interpolação já terminou deixam de receber writes de transform
  até chegar um novo alvo;
- a interpolação nativa reduz automaticamente de 50 Hz para 30/20 Hz conforme
  aumenta o número de ônibus físicos;
- a fila de comandos é drenada em fatias limitadas, no máximo uma vez por
  janela de trabalho do OMSI, e não em todo callback de variável;
- heartbeat/log de arquivo é gravado por uma fila de background, nunca por I/O
  síncrono dentro do callback do simulador;
- readback de validação durante movimento foi reduzido para 1 Hz, mantendo
  validação imediata em spawn, teleport e troca de Kachel.

Essas medidas não alteram arquivos, configurações gráficas, IA ou parâmetros
internos do OMSI do usuário. Elas reduzem apenas o custo incremental do NavBR.

### English

The Plugin Bridge keeps native access on OMSI's callback thread, but work on
that thread is now explicitly budgeted. Physical rendering is limited to the
12 nearest remote buses (500 m spawn / 700 m despawn hysteresis), bridge updates
use distance LOD, settled buses stop receiving redundant native transform
writes, interpolation scales from 50 Hz down to 30/20 Hz as physical-bus count
grows, command draining is rate-limited, readback runs at 1 Hz during smooth
motion, and file logging is handed to a background writer. Players beyond the
physical budget remain present in the session, HUD and maps.
