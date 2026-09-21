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

Abaixo de 920 px, os presets compostos entram em uma faixa ultraestreita:
métricas secundárias de atraso e combustível cedem espaço para linha, rota,
próxima parada e velocidade; mapa, multiplayer e painel de foco recebem limites
mais conservadores. As colunas do topo também deixam de reservar espaço vazio
quando um módulo está oculto pelo preset ou pelo usuário.


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


## Minimal Driver contextual

O preset **Minimal Driver** mantém o módulo de minimapa habilitado como
capacidade, mas o esconde durante operação normal. Quando o motor de navegação
resolve a rota e detecta que o ônibus está fora dela, o minimapa aparece
automaticamente para ajudar no retorno. Se o usuário desligar o minimapa nas
configurações, essa abertura contextual também é desativada.


## Âncora do painel principal

Nos presets compostos, `DashboardAnchor` posiciona o painel principal de foco
quando o preset possui esse módulo. As posições superiores ficam abaixo da
barra de serviço; as posições inferiores evitam o minimapa à esquerda e o
painel multiplayer à direita quando esses módulos estão visíveis.

A opção `free` preserva o layout desenhado especificamente para cada preset.
Presets sem painel de foco mantêm suas barras e módulos nas posições próprias.
As âncoras superiores consideram a escala global do HUD; as âncoras inferiores
também reservam espaço conforme as escalas individuais de minimapa e multiplayer,
evitando que uma personalização ampliada volte a sobrepor o painel de foco.


## Arraste do painel principal

O modo global **Mover HUD** também controla o painel principal dos presets
compostos que possuem painel de foco.

- o handle aparece somente durante o modo de edição;
- arrastar grava `DashboardX` e `DashboardY`;
- o arraste muda a âncora para `custom`;
- duplo clique no handle retorna para o layout livre/original do preset;
- sair do modo de edição encerra e salva qualquer arraste em andamento.

A âncora **Personalizada** também fica disponível no editor para preservar uma
posição criada por arraste.


## Tema independente do preset

O seletor de tema passa a funcionar também nos HUDs compostos. O preset continua
definindo o tema inicial, mas o usuário pode trocar a paleta depois sem alterar a
composição ou os módulos.

Os temas modernos e legados são mapeados para paletas compatíveis, incluindo
NavBR Modern, Urban Glass, Route Night, Racing Clean, Bus Panel, LCD, Âmbar
Clássico e Claro. Temas de display também aplicam fonte monoespaçada ao HUD
composto.


## Altura do painel principal

Nos presets compostos, `DashboardHeight = 0` mantém a altura automática.
Quando o usuário define um valor maior que zero, ele passa a ser a altura mínima
do painel principal de foco. Nos presets sem painel de foco, a altura mínima é
aplicada à barra principal. Mapa e multiplayer preservam suas proporções.


## Posicionamento de alertas e indicadores

Os módulos auxiliares também seguem a intenção visual de cada preset:

- **Streamer / Broadcast:** alertas e indicadores ficam nas bordas, preservando
  o centro da tela para gravação/transmissão.
- **Glass / Night:** alertas e indicadores ficam próximos aos cantos e fora do
  campo visual central.
- **Navigation Pro:** alertas acompanham o lado da orientação.
- **Multiplayer Focus:** indicadores laterais são deslocados para não competir
  com o painel multiplayer.
