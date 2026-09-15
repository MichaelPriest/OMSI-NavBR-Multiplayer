# HUD, GPS, chat e voz

## Interface em jogo

O HUD do NavBR é uma sobreposição WPF transparente sobre a janela do OMSI. A interface atual usa uma barra superior translúcida moderna e módulos separados para navegação, dados da linha, chat e voz.

Estrutura atual:

```text
┌──────────────────────────── tela do OMSI ─────────────────────────────┐
│ [N] ● sala/mapa   👥 jogadores   💬 CHAT   🎙 PTT   atalhos          │
│                                                                       │
│ LINHA 287   Destino: ...   Próxima parada: ...     ↗ Em 60 m ...    │
│                                                                       │
│ ┌──────────── GPS heading-up ────────────┐                            │
│ │ mapa + rota giram                      │                            │
│ │               ▲                       │                            │
│ │        ônibus sempre para cima         │                            │
│ └────────────────────────────────────────┘                            │
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

O módulo de navegação agora segue o comportamento típico de GPS automotivo:

- o marcador do ônibus local permanece centralizado e sempre apontando para cima;
- o roadmap, a rota e os jogadores remotos giram em sentido contrário ao heading do ônibus;
- o zoom dinâmico continua disponível, inclusive ajuste manual até 10x no modo de edição;
- linha, destino e próxima parada ficam fora da área do mapa para não esconder a navegação.

Quando existe geometria de rota detalhada e contínua, o HUD pode mostrar uma indicação de manobra com seta e distância aproximada. O NavBR não inventa instruções quando só existe geometria grosseira por centro de tile: se os segmentos forem grandes demais, a seta é ocultada.

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
- o HUD e o GPS mostram jogadores remotos no NavBR, mas ainda não criam ônibus físicos remotos dentro do mundo 3D do OMSI.
