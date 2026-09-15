# Estado real do multiplayer

> **Importante:** no estado atual do OMSI NavBR Multiplayer, entrar na mesma sala **não coloca o ônibus do outro jogador dentro do mapa 3D do OMSI**.

Este documento existe para separar claramente o que o multiplayer já faz do que ainda está em desenvolvimento.

## Resumo rápido

Hoje, dois ou mais jogadores podem entrar na mesma sala e o NavBR pode trocar dados entre eles.

O outro jogador pode aparecer:

- no **mapa/GPS do NavBR**;
- no **minimapa/HUD do NavBR**;
- na lista/presença da sala;
- no chat de texto;
- no chat de voz.

O outro jogador **ainda não aparece como um ônibus físico dentro do mundo 3D do OMSI**.

Em outras palavras:

```text
FUNCIONA HOJE
PC A / OMSI -> NavBR -> rede -> NavBR -> marcador remoto no GPS/HUD do PC B

AINDA NÃO FUNCIONA
PC A / ônibus -> rede -> criação de outro ônibus visível dentro do OMSI do PC B
```

## O que já existe no multiplayer

A base atual possui:

- criação de sala no próprio PC do host;
- host local ASP.NET Core + SignalR;
- porta padrão `TCP 27730`;
- entrada em sala por convite/endereço;
- presença e nickname dos jogadores;
- compartilhamento de telemetria;
- posição, heading, velocidade e contexto do mapa transportados pela sessão;
- atualização de mapa e `MapCompatibilityId` durante a sessão;
- marcadores de jogadores remotos no mapa/minimapa do NavBR;
- suavização visual dos marcadores remotos;
- chat de texto;
- voz push-to-talk;
- reconexão automática SignalR e tentativa de reentrada na sala;
- servidor dedicado opcional.

Esses recursos ainda precisam de validação real ampla entre computadores e diferentes redes antes de serem considerados estáveis.

## O que ainda NÃO existe

No estado atual, o NavBR **não**:

- cria o ônibus de outro jogador dentro do mapa 3D do OMSI;
- adiciona outro jogador como veículo físico/AI no simulador;
- move fisicamente um ônibus remoto dentro do OMSI;
- sincroniza fisicamente colisões entre jogadores;
- transforma os demais jogadores em tráfego compartilhado;
- sincroniza passageiros ou tráfego AI de um PC para outro;
- sincroniza semáforos do OMSI entre os computadores;
- aplica portas, luzes, setas, pisca-alerta, buzina ou matriz a um ônibus remoto dentro do OMSI;
- garante que todos os participantes tenham o mesmo ônibus/add-on instalado.

Portanto, **“multiplayer conectado” não significa ainda “outros ônibus visíveis dentro do OMSI”**.

## Mapa do NavBR x mapa do OMSI

Há duas coisas diferentes que podem ser chamadas de “mapa”:

### Mapa/GPS do NavBR

É a interface 2D usada pelo aplicativo para navegação. Nela o NavBR já pode desenhar marcadores de outros jogadores compatíveis da sala.

### Mundo 3D do OMSI

É o cenário real do simulador onde ruas, ônibus, tráfego, passageiros e objetos são renderizados pelo OMSI.

**O jogador remoto ainda não é criado neste mundo 3D.**

Essa diferença deve ser considerada em qualquer teste ou divulgação do projeto.

## O roadmap do mapa não cria jogadores remotos

Arquivos como:

```text
texture\map\whole.roadmap.bmp
texture\map\roadmap.bmp
```

servem como imagem de fundo para o GPS/mapa 2D do NavBR.

Eles **não são responsáveis por criar ônibus remotos dentro do OMSI**.

Um mapa pode ter o roadmap correto e mostrar perfeitamente os jogadores no GPS do NavBR, mas continuar sem qualquer ônibus remoto físico no simulador.

## Compatibilidade de mapa

O multiplayer transporta o nome do mapa e um `MapCompatibilityId`/fingerprint quando disponível.

Isso serve para:

- identificar se os jogadores provavelmente estão usando a mesma versão do mapa;
- evitar tratar um remoto de mapa incompatível como candidato a uma futura representação física;
- melhorar os diagnósticos da sessão.

Ter o mesmo fingerprint **não cria automaticamente o jogador no OMSI**. É apenas uma condição de compatibilidade.

## Plugin OMSI experimental da alpha.10

A alpha.10 em desenvolvimento possui um plugin x86 opcional e experimental.

O fluxo atual é:

```text
jogador remoto
   ↓ SignalR
NavBR.Client
   ↓ Named Pipe local
NavBR.OmsiPlugin
   ↓
registro / validação / diagnóstico
```

O plugin já pode receber estado remoto para diagnóstico, aplicar filtro de mapa, timeout e interpolação em memória própria.

**Ele ainda não aplica esse estado ao mundo do OMSI.**

Nesta etapa o plugin:

- não cria veículos;
- não move veículos;
- não escreve variáveis do veículo para representar outro jogador;
- não dispara triggers para representar outro jogador;
- não injeta patches no executável do OMSI.

O objetivo atual do plugin é provar, primeiro, que o carregamento e o bridge são estáveis no OMSI real.

## O que precisa acontecer antes de aparecer outro ônibus no OMSI

A primeira representação física remota só deve ser tentada depois de validar:

1. carregamento real do plugin no OMSI 2.3.004;
2. `PluginStart` e `PluginFinalize` corretos;
3. `status=CONNECTED`;
4. `process-match=YES`;
5. `heartbeat=LIVE` contínuo;
6. bridge estável entre NavBR e plugin;
7. fluxo completo entre dois computadores;
8. filtro correto por mapa/fingerprint;
9. remoção/timeout sem estados fantasmas;
10. ausência de crash ou regressão relevante de FPS.

Depois disso ainda será necessário investigar e provar um mecanismo seguro de representação por **veículo AI/instância equivalente**.

A interface pública `.opl` do OMSI não deve ser tratada como se fornecesse uma API direta de spawn de outro ônibus.

## Etapas planejadas para o multiplayer visual dentro do OMSI

Depois dos testes acima, a evolução prevista é:

1. criar uma única representação remota experimental;
2. associá-la a um jogador remoto compatível;
3. aplicar posição e orientação de forma segura;
4. usar interpolação para evitar saltos;
5. remover a entidade ao sair, trocar de mapa ou perder atualização;
6. criar política de distância/LOD e limite de veículos;
7. definir compatibilidade e fallback de modelo de ônibus;
8. somente depois sincronizar estados como portas, luzes, setas, buzina, articulação e matriz.

Se não houver um mecanismo suficientemente seguro e estável, o NavBR deve manter os jogadores somente no GPS/HUD em vez de forçar uma implementação invasiva.

## Estado para testes atuais

Ao testar as versões atuais, o resultado esperado é:

- os dois jogadores entram na mesma sala;
- presença e telemetria são trocadas;
- o remoto pode aparecer no GPS/HUD do NavBR;
- chat e voz podem ser usados;
- na alpha.10 experimental, o plugin pode receber e contabilizar o remoto no diagnóstico;
- **nenhum ônibus remoto físico deve aparecer dentro do OMSI ainda**.

Se um teste for descrito como “multiplayer funcionando”, informe qual camada foi validada: conexão, telemetria, GPS/HUD, chat/voz, bridge do plugin ou futura representação física.