# NavBR Mobile Companion — roadmap futuro

## Estado

**Planejado para uma etapa futura. Não faz parte do escopo atual de correção/validação da Alpha.14.**

O foco imediato do projeto continua sendo estabilizar e validar as funções já existentes no desktop/OMSI antes de adicionar novas superfícies.

## Prioridade atual

Antes de iniciar qualquer desenvolvimento para smartphone, devem ser corrigidos e validados no OMSI real:

- materialização do ônibus remoto físico no mapa;
- atualização/movimentação correta do ônibus remoto físico;
- carregamento automático do roadmap real do mapa ativo;
- navegação e retorno à rota usando a malha/splines reais quando o veículo sair da rota;
- modo Personagem/RP físico, incluindo sair e retornar ao ônibus;
- criação, entrada e gerenciamento de salas com interface simples e clara;
- estabilidade do Plugin Bridge, telemetria, multiplayer e interface React/WebView2.

Nenhuma dessas correções deve ser substituída por mock, simulação de interface ou dado inventado em produção.

## Visão futura

Depois que o núcleo estiver estável, o projeto poderá receber um **NavBR Mobile Companion**, inicialmente como PWA responsiva e, se fizer sentido, posteriormente empacotada para Android/iOS.

Possibilidades documentadas para essa etapa futura:

- segundo monitor de navegação/GPS;
- mapa, rota, próxima parada, distância, atraso e caminho de retorno à rota;
- visualização de jogadores próximos e estado multiplayer;
- criar/entrar em salas e pareamento por QR Code;
- chat, rádio/PTT e canais de comunicação;
- painel CCO/Dispatcher;
- painel do motorista;
- comandos auxiliares validados pelo cliente C# e Plugin Bridge;
- controles relacionados ao modo RP;
- diagnóstico do OMSI, Plugin Bridge, servidor, ping e ônibus físico;
- modo painel de bordo para usar o smartphone como tela auxiliar enquanto o OMSI permanece em tela cheia.

## IBIS Mobile / terminal operacional

O modo **IBIS Mobile** será diferente do GPS. Ele deverá funcionar como um terminal de operação do ônibus e permitir configurar a viagem usando somente dados reais disponíveis no OMSI/NavBR.

Funções previstas:

- selecionar a **linha**;
- selecionar a **rota/curso**;
- escolher o **sentido**;
- selecionar ou confirmar o **destino**;
- carregar/usar o **HOF** compatível;
- selecionar a **viagem/serviço** quando essa informação estiver disponível;
- iniciar, alterar e encerrar a operação;
- acompanhar a próxima parada e o estado operacional;
- futuramente comandar anúncios e informações de destino quando houver integração real suportada.

A interface poderá oferecer dois modos: uma visualização inspirada em IBIS físico, com teclado/campos operacionais, e uma visualização moderna simplificada.

Fluxo pretendido:

`IBIS no smartphone -> NavBR Client/Server -> C# valida mapa/HOF/ônibus -> Plugin Bridge/OMSI`

O smartphone não deverá escrever diretamente na memória do OMSI. Linha, rota, destino, curso e demais opções deverão vir do estado real do mapa/HOF/ônibus carregado; quando uma informação real não estiver disponível, a interface deverá indicar indisponibilidade em vez de inventar valores.

O **GPS/segundo monitor** permanece uma função separada: ele é responsável por navegação e acompanhamento da rota, enquanto o **IBIS Mobile** é responsável pela configuração operacional da viagem.

## Arquitetura pretendida

O smartphone não deve acessar diretamente a memória do OMSI.

A arquitetura prevista é:

`Smartphone/PWA -> NavBR Client/Server -> C# authority -> Plugin Bridge/OMSI`

O C# continua sendo a autoridade sobre telemetria, estado do OMSI, multiplayer, escrita física, RP, hardware e comandos. A interface mobile deve consumir apenas estado real e apresentar indisponibilidade quando a fonte real não estiver disponível.

## Fora do escopo atual

Durante a correção da Alpha.14:

- não criar PWA mobile;
- não criar APK/IPA;
- não adicionar novas APIs apenas para o Companion;
- não alterar o protocolo multiplayer por causa do mobile;
- não atrasar correções atuais para implementar recursos de smartphone.

Este documento existe somente para preservar a ideia e o direcionamento futuro sem ampliar o escopo da validação atual.
