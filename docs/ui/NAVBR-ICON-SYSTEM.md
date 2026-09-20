# NavBR Icon System

O NavBR usa um conjunto vetorial autoral inspirado na linguagem de painéis, sinalização e equipamentos de operação de ônibus. Os ícones não copiam assets do OMSI; apenas preservam uma leitura visual compatível com o contexto do simulador.

## Regras

- Grid base: 24 x 24.
- Stroke padrão: 1.8.
- Cantos e terminações arredondados.
- Cor por `currentColor`, controlada pelo estado do componente.
- Estados visuais: normal, hover, ativo e desabilitado.
- Não usar emoji ou caracteres Unicode como ícone de interface.
- Novos ícones devem entrar em `ui/navbr-web/src/NavBrIcon.tsx`.
- Nomes devem ser semânticos, não baseados na aparência.

## Pacote inicial

Navegação: home, navigation, multiplayer, roleplay, ghost, operations, company, hardware, settings, help.

Multiplayer e comunicação: roomAdd, roomJoin, server, users, chat, microphone, volume, network, firewall.

Operação: bus, map, route, destination, door, light, turnLeft, turnRight, hazard, brake, reverse, engine.

Ferramentas: refresh, play, plugin, download, logs, test, info.

## Versão

A versão do aplicativo deve aparecer em uma área própria do layout. Informações de implementação como framework, WebView, protocolo interno ou backend não devem aparecer na navegação normal do usuário. Diagnóstico técnico fica restrito às telas de suporte/diagnóstico quando necessário.
