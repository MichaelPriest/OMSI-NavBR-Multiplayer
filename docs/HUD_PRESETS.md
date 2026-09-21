# Presets de HUD do NavBR

Esta família de presets reutiliza o mesmo estado real de telemetria, navegação,
multiplayer, chat e voz. Nenhum preset cria dados sintéticos.

## Presets compostos

- **Imersivo / Operação** (`immersive-operation`) — referência base com barra
  superior, minimapa e multiplayer.
- **Transit Control** (`transit-control`) — operação profissional com mapa e
  status mais amplos.
- **Cockpit Digital** (`cockpit-digital`) — cluster central moderno, velocidade
  em destaque e minimapa compacto.
- **Navigation Pro** (`navigation-pro`) — mapa maior e foco em rota/próxima
  parada.
- **Multiplayer Focus** (`multiplayer-focus`) — jogadores, ping, chat e voz em
  primeiro plano.
- **Classic OMSI+** (`classic-omsi-plus`) — visual âmbar compacto inspirado em
  displays clássicos do OMSI.
- **Minimal Driver** (`minimal-driver`) — mínimo de elementos para manter a
  cabine livre.
- **Streamer / Broadcast** (`streamer-broadcast`) — composição nas bordas para
  gravação e transmissão.
- **Glass / Night HUD** (`glass-night`) — transparência e contraste reduzidos
  para condução noturna.
- **City Operations** (`city-operations`) — visão operacional mais densa com
  mapa e multiplayer simultâneos.
- **Driver Assistance** (`driver-assistance`) — próxima parada e alertas reais
  do veículo em destaque.

## Compatibilidade

Configurações antigas continuam sendo aceitas:

- `transit-pro` -> `transit-control`
- `route-advisor` -> `navigation-pro`
- `digital-cluster` -> `cockpit-digital`

O HUD clássico permanece disponível. Trocar para um preset composto não altera
a fonte dos dados e não remove as configurações globais de escala, opacidade ou
módulos.


## Painel de foco

Alguns presets usam um quarto módulo de foco, além de barra superior, mapa e
multiplayer:

- **Cockpit Digital:** cluster central com velocidade grande e serviço atual.
- **Navigation Pro:** próxima parada, rua atual e serviço em destaque ao lado
  do mapa.
- **Driver Assistance:** estado real do veículo e próxima parada em destaque.
- **City Operations:** acelerador, freio, estado operacional e serviço atual.

Esse painel usa somente telemetria real já disponível no runtime.
