# HUD e chat por voz

## HUD

O HUD do NavBR é uma sobreposição WPF transparente sobre a janela do OMSI. Ele usa identidade visual própria e somente se inspira em padrões comuns de HUD de jogos de mundo aberto.

Layout inicial:

```text
┌──────────────────────────── tela do OMSI ─────────────────────────────┐
│                                                                       │
│                                                                       │
│                                                                       │
│  chat temporário                                                      │
│  Jogador: mensagem                                                    │
│  ┌──────────────────────────┐                                         │
│  │ minimapa + jogadores     │                                         │
│  │ ônibus local centralizado│                                         │
│  └──────────────────────────┘                                         │
│  ● sala online • T: chat • N: voz                                    │
└───────────────────────────────────────────────────────────────────────┘
```

O overlay fica click-through normalmente para não bloquear o mouse/teclado do OMSI. Ao pressionar `T`, ele entra temporariamente em modo interativo para receber a mensagem.

## Push-to-talk

Atalho inicial: `N`.

- tecla pressionada: o microfone é capturado e frames Opus são enviados;
- tecla solta: transmissão do microfone para imediatamente;
- o jogador pode desabilitar voz na janela multiplayer.

## Codec e áudio

- NAudio: captura e reprodução no Windows;
- Concentus: Opus gerenciado;
- 48 kHz, mono;
- frame de 20 ms;
- bitrate alvo 24 kbit/s;
- VBR + FEC habilitados na configuração inicial.

## Transporte alpha

Os frames Opus usam SignalR/WebSocket nesta fase. A implementação prioriza simplicidade e integração com o peer-host. Se os testes mostrarem latência/jitter excessivos, o módulo poderá migrar para UDP ou WebRTC sem alterar o fluxo do HUD.

## Limitações iniciais

- fullscreen exclusivo ainda precisa ser validado;
- seleção de dispositivo de entrada/saída ainda será adicionada;
- cancelamento de eco, redução de ruído e controle automático de ganho são recursos posteriores;
- qualidade e consumo de banda precisam de testes com jogadores em redes reais.
