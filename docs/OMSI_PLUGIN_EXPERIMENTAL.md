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
- a fila do callback processa no máximo 1 comando arbitrário/pesado por frame e até 4 updates leves adicionais, elevando a fluidez com vários ônibus sem liberar bursts de spawn no mesmo frame.

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
