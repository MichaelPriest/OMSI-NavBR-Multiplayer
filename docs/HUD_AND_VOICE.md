# HUD, GPS, chat e voz

## Interface em jogo

O HUD do NavBR é uma sobreposição WPF transparente sobre a janela do OMSI. A interface da alpha.11 usa uma barra superior translúcida moderna e módulos separados para navegação, dados da linha, painel do ônibus, chat e voz.

Estrutura atual:

```text
┌──────────────────────────── tela do OMSI ─────────────────────────────┐
│ [N] ● sala/mapa   👥 jogadores   💬 CHAT   🎙 PTT   atalhos          │
│                                                                       │
│ LINHA 287   Destino: ...   Próxima parada: ...     ↗ Em 60 m ...    │
│                                                                       │
│ ┌──────────── GPS heading-up ────────────┐                            │
│ │ mapa + rota + pontos de parada giram   │                            │
│ │       H     H       H                  │                            │
│ │               ▲                       │                            │
│ │        ônibus sempre para cima         │                            │
│ └────────────────────────────────────────┘                            │
│ painel do ônibus: velocidade / pedais / portas / luzes               │
│ chat recebido                                                        │
│ Jogador: mensagem                                                     │
│ [CHAT] digitando...                                                   │
└───────────────────────────────────────────────────────────────────────┘
```

### Barra superior

A barra superior mostra, sem ocupar o mapa:

- estado da sala/conexão;
- mapa ativo;
- quantidade de jogadores;
- indicadores de chat e PTT;
- atalhos configurados;
- indicador animado de conexão;
- indicador de quem está falando.

O HUD continua click-through durante a condução normal.

## GPS heading-up

O módulo de navegação segue o comportamento típico de GPS automotivo:

- o marcador do ônibus local permanece centralizado e sempre apontando para cima;
- o roadmap, a rota, os pontos de parada e os jogadores remotos giram em sentido contrário ao heading do ônibus;
- o zoom dinâmico continua disponível, inclusive ajuste manual até 10x no modo de edição;
- linha, destino e próxima parada ficam fora da área do mapa para não esconder a navegação;
- o bloco legado `NAVBR DRIVE` não faz parte da interface final da alpha.11;
- o marcador local foi compactado para não cobrir a rota nem as paradas próximas.

Quando existe geometria de rota detalhada e contínua, o HUD pode mostrar uma indicação de manobra com seta e distância aproximada. O NavBR não inventa instruções quando só existe geometria grosseira por centro de tile: se os segmentos forem grandes demais, a seta é ocultada.

## Visão geral da rota

O mapa principal do NavBR possui o modo **Rota completa**. Quando a viagem ativa pode ser resolvida pelo timetable, o NavBR desenha a rota sobre o roadmap e calcula automaticamente um enquadramento que mostra o percurso inteiro.

Esse modo:

- usa a mesma geometria `.ttp` / `.ttr` / splines do GPS;
- calcula os limites reais dos pontos da rota, em vez de simplesmente mostrar o mapa inteiro;
- centraliza a rota no viewport com margem visual;
- desativa temporariamente o modo **Seguir ônibus**;
- permite zoom e pan manual mesmo depois do enquadramento inicial;
- volta ao modo de seguir o veículo ao desligar **Rota completa**;
- fica indisponível quando não existe geometria suficiente para representar a linha com segurança.

O comando aparece traduzido em Português (Brasil), English, Español, Deutsch e Français.

## Pontos de parada no HUD

A alpha.11 lê as paradas funcionais diretamente das tiles `.map` do mapa instalado. Como referência de formato foi estudado o comportamento público do OMSI RouteAdvisor, mas o parser do NavBR é uma implementação própria.

Comportamento previsto/implementado:

- localizar objetos funcionais de parada, incluindo o padrão `Sceneryobjects\Generic\bus_stop.sco` e variantes compatíveis;
- converter a posição local da parada para o mesmo sistema do roadmap;
- mostrar apenas marcadores próximos/visíveis no GPS para evitar poluição visual;
- manter o símbolo da parada legível enquanto o mapa gira;
- destacar a próxima parada usando o nome recebido pela telemetria e a parada compatível mais próxima;
- manter o texto de próxima parada também no painel superior de viagem.

### Ícone de parada

O modo padrão é **OMSI**, representado no HUD pelo símbolo clássico de parada `H`, mantendo a aparência familiar do simulador sem redistribuir assets proprietários do jogo.

O usuário pode trocar o visual em tempo de execução:

- **Padrão OMSI** — símbolo clássico `H`;
- **Minimalista** — marcador simples para quem prefere menos informação visual;
- **Personalizado** — arquivo PNG, JPG/JPEG ou BMP escolhido pelo usuário.

A escolha é persistida em `%LOCALAPPDATA%\OMSI NavBR Multiplayer\multiplayer.json`. Se o arquivo personalizado deixar de existir ou não puder ser lido, o HUD volta de forma segura ao visual padrão OMSI em vez de quebrar o GPS.

A próxima parada recebe destaque visual maior e glow laranja, independentemente do estilo selecionado.

## Painel do ônibus

O painel do ônibus é independente do GPS e pode ficar abaixo dele ou ser movido pelo usuário. Ele suporta:

- arrastar e salvar posição;
- resetar posição;
- aumentar/diminuir escala;
- ajustar transparência;
- ativar/desativar o painel;
- ativar/desativar combustível, pedais e indicadores;
- velocidade, aceleração, combustível, acelerador, freio, portas, setas, luzes, freio de estacionamento, ré e limpador quando a telemetria correspondente for válida.

Valores inválidos ou não disponíveis não devem ser inventados: o módulo mostra estado indisponível e mantém a leitura defensiva.

## Chat em jogo

Atalho padrão: `F9`.

O chat visual fica acoplado abaixo do GPS. Ao abrir o campo de digitação:

- o HUD passa temporariamente para modo interativo;
- uma camada transparente cobre a janela do OMSI para impedir cliques no jogo enquanto o usuário digita;
- o campo de chat recebe foco;
- PTT é interrompido para evitar conflito;
- `Enter` envia a mensagem;
- `Esc` fecha o campo sem enviar;
- ao fechar, o foco retorna ao OMSI e o overlay volta a ser click-through.

## Push-to-talk

Atalho padrão: `F10`.

- tecla pressionada: o microfone é capturado e frames Opus são enviados;
- tecla solta: a transmissão para imediatamente;
- o jogador pode desabilitar voz na janela multiplayer;
- o HUD mostra quem está falando.

## Atalhos personalizáveis

Chat e PTT não estão limitados a F9/F10. A interface oferece combinações com:

- F1-F4;
- F9-F12;
- Shift + tecla;
- Ctrl + tecla;
- Ctrl + Shift + tecla.

F5-F8 permanecem intencionalmente fora da lista por serem teclas frequentemente usadas pelo OMSI.

Antes de habilitar um atalho, o NavBR lê `Inputs/keyboard.cfg`. Se a combinação já estiver em uso no OMSI, o atalho é bloqueado e o HUD mostra o conflito. Chat e PTT também não podem usar a mesma combinação.

## Logs automáticos

A partir desta fase, o cliente cria automaticamente:

```text
%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr.log
```

em toda execução. O log registra início/fim da sessão e eventos de diagnóstico do cliente. Logs específicos continuam separados quando aplicável:

- `navbr-route.log` — resolução/qualidade da geometria da rota;
- `navbr-plugin.log` — plugin OMSI experimental;
- `navbr-error.log` — exceções não tratadas do cliente.

## Codec e áudio

- NAudio: captura e reprodução no Windows;
- Concentus: Opus gerenciado;
- 48 kHz, mono;
- frame de 20 ms;
- bitrate alvo 24 kbit/s;
- VBR + FEC habilitados na configuração inicial.

## Transporte alpha

Os frames Opus usam SignalR/WebSocket nesta fase. A implementação prioriza simplicidade e integração com o peer-host. Se testes reais mostrarem latência/jitter excessivos, o módulo poderá migrar para UDP ou WebRTC sem alterar o fluxo do HUD.

## Limitações atuais

- fullscreen exclusivo ainda precisa ser validado;
- seleção de dispositivo de entrada/saída ainda será adicionada;
- cancelamento de eco, redução de ruído e controle automático de ganho são recursos posteriores;
- qualidade e consumo de banda precisam de testes com jogadores em redes reais;
- as setas de manobra dependem de geometria suficientemente detalhada; o NavBR prefere não mostrar seta a apresentar uma orientação falsa;
- mapas que implementem parada funcional com uma estrutura totalmente diferente do objeto OMSI conhecido podem exigir um perfil/parser adicional;
- o HUD e o GPS mostram jogadores remotos no NavBR, mas ainda não criam ônibus físicos remotos dentro do mundo 3D do OMSI.
