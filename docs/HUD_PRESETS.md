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


## Próximas paradas

**Transit Control** e **Navigation Pro** usam
`OmsiOrderedRouteStopReader` para ler as paradas ordenadas do `.ttp`.
A lista só aparece quando a viagem é resolvida sem ambiguidade e a próxima
parada informada pela telemetria é encontrada nessa sequência. Caso contrário,
o HUD não inventa uma ordem e mantém apenas a próxima parada conhecida.


## Refinamentos Classic e Minimal

- **Classic OMSI+** usa um display âmbar dedicado de linha, destino e próxima
  parada, com fonte monoespaçada e sem alterar a telemetria original.
- **Minimal Driver** remove combustível e atraso do topo e esconde a faixa de
  estado quando a operação está normal; ela reaparece automaticamente quando
  existir um estado relevante do veículo.


## Navegação avançada

**Navigation Pro** reutiliza `NavBRNavigationEngine` para exibir somente quando
resolvido com dados reais:

- próxima manobra e distância;
- estado fora da rota e distância para retorno;
- progresso e distância restante da rota;
- distância até a próxima parada;
- ETA para próxima parada e fim da rota.

O ETA usa `NavBRNavigationEtaEstimator`: ele só aparece depois de acumular
amostras confiáveis de progresso real e some quando a rota/ritmo deixam de ser
confiáveis. **Driver Assistance** também reaproveita a próxima manobra ou aviso
de retorno à rota como informação secundária.


## Escala e resolução

Os presets compostos respeitam a mesma `DashboardScale` do HUD modular.
Quando `DashboardAutoScale` está ativo, o fator de resolução já existente no
desktop também é aplicado. As escalas específicas de minimapa e multiplayer são
multiplicadas pela escala geral, preservando os controles independentes.


## Largura

`DashboardWidth` também é respeitado pelos presets compostos. O valor é
interpretado como um fator relativo à largura padrão do preset e ajusta os
painéis horizontais entre 70% e 135% do desenho original. A altura do mapa e a
composição vertical permanecem preservadas para evitar distorção.


## Layout estreito

Quando a janela do OMSI tem menos de 1120 px de largura, **Transit Control** e
**City Operations** movem o painel de foco para baixo da barra superior. Isso
evita sobreposição entre minimapa, painel central e multiplayer no rodapé.


## Alertas e indicadores laterais

Os presets compostos usam os controles globais de alertas e indicadores:

- `DashboardShowAlerts` exibe somente alertas derivados de estado real:
  portas abertas em movimento, freio de estacionamento durante movimento, ré
  ativa e veículo fora da rota.
- `DashboardShowSideIndicators` exibe indicadores ativos de portas, setas,
  pisca-alerta, faróis, freio P, ré, limpador e solicitação de parada.
- `DashboardAlertsScale` e `DashboardSideIndicatorsScale` controlam as
  escalas desses dois módulos.

Quando não existe estado ativo, os módulos ficam ocultos em vez de mostrar
informação artificial.
