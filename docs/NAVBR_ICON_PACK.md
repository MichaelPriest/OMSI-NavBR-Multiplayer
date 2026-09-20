# NavBR Icon Pack

Pacote vetorial autoral usado pela interface do OMSI NavBR Multiplayer.

## Direção visual

- linguagem de painel de transporte e computador de bordo;
- traço simples, técnico e legível em tamanhos pequenos;
- inspirado no contexto visual de simuladores de ônibus, sem copiar ícones, bitmaps ou assets do OMSI;
- renderização SVG por `currentColor`, permitindo estados normal, hover, warning e active pela própria UI;
- viewBox padrão `0 0 24 24` e espessura de traço padrão `1.8`.

## Grupos de ícones

### Navegação principal
`home`, `navigation`, `multiplayer`, `roleplay`, `ghost`, `operations`, `company`, `hardware`, `settings`, `help`.

### Multiplayer e rede
`roomAdd`, `roomJoin`, `server`, `users`, `chat`, `microphone`, `volume`, `network`, `firewall`, `clipboardCopy`, `clipboardPaste`, `star`.

### OMSI / veículo
`bus`, `route`, `destination`, `door`, `light`, `turnLeft`, `turnRight`, `hazard`, `brake`, `reverse`, `engine`.

### Navegação de rota
`straight`, `slightLeft`, `slightRight`, `sharpLeft`, `sharpRight`, `rejoin`.

### RP e Ghost
`character`, `action`, `record`, `play`, `stop`, `ghost`.

### Sistema e diagnóstico
`refresh`, `plugin`, `download`, `logs`, `test`, `info`.

## Implementação

A fonte canônica é `ui/navbr-web/src/NavBrIcon.tsx`. Os glyphs são desenhados diretamente em SVG e não dependem de bibliotecas de terceiros ou assets extraídos do jogo.

Não utilizar assets extraídos do OMSI ou de outros jogos para ampliar este pacote. Novos glyphs devem seguir a mesma grade, stroke e linguagem visual.
