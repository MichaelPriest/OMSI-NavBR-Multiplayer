# Arquitetura

## Princípios

1. **Sem dependência da Steam**: o cliente encontra `Omsi.exe` em execução e deriva a instalação pelo caminho do processo.
2. **Integração desacoplada**: telemetria, mapa, navegação, HUD, voz e multiplayer são módulos separados.
3. **Compatibilidade por perfil**: cada build suportada do OMSI terá um perfil de leitura/assinaturas próprio.
4. **Sem DRM bypass**: o projeto não modifica ativação/licenciamento do OMSI.
5. **Peer-host por padrão**: o PC que cria a sala executa o host multiplayer embutido.
6. **Servidor dedicado opcional**: `NavBR.Server` continua podendo ser executado separadamente.
7. **Localização desde a base**: módulos de domínio não dependem de texto de interface; idiomas são tratados no cliente por recursos.

## Fluxo

```text
Omsi.exe
  │
  ├─ Process detector
  ├─ Version detector
  └─ Telemetry provider
         │
         ▼
   NavBR.Client
     ├─ Localization
     ├─ Map reader / fingerprint
     ├─ TTData reader
     ├─ Navigation engine
     ├─ GPS / HUD overlay
     ├─ Text + voice chat
     └─ Multiplayer client
              │
       ┌──────┴──────────────────┐
       │ Criador da sala         │
       │ RoomHostService         │
       │ NavBR.Server embutido   │
       │ TCP 27730 / SignalR     │
       └──────┬──────────────────┘
              │
        ┌─────┴─────┐
        ▼           ▼
     Player A    Player B
```

## Cliente

O cliente é WPF em `net10.0-windows`, inicialmente compilado em x86 para acompanhar o OMSI 2, que é um processo de 32 bits.

### Camadas principais

- `OmsiProcessDetector`: encontra PID, executável, versão e pasta da instalação.
- `ICompatibilityProfile`: identifica uma build conhecida.
- `ITelemetryProvider`: abstrai leitura de memória/plugin.
- `LocalizationService`: detecta idioma, carrega recursos e persiste preferência.
- `OmsiMapCatalog`: `global.cfg`, tiles, roadmap e fingerprint de compatibilidade.
- `TtDataReader`: `Busstops.cfg`, `.ttr`, `.ttp`, `.ttl`.
- `NavigationEngine`: próxima parada, progresso da viagem, distância e ETA.
- `MultiplayerClientService`: sala, presença, telemetria, chat e voz.
- `RoomHostService`: inicia/encerra o servidor da sala no PC do criador.
- `VoiceChatService`: captura/reprodução e codec Opus.
- `HudOverlayWindow`: minimapa, chat e presença de voz sobre o OMSI.

## Internacionalização

A interface usa `ResourceManager` e arquivos `.resx` no namespace `NavBR.Client.Resources`.

Idiomas iniciais:

```text
pt-BR  Português (Brasil)
en-US  English
es-ES  Español
de-DE  Deutsch
fr-FR  Français
```

Regras:

- detectar o idioma do Windows na primeira execução;
- usar inglês como fallback;
- permitir troca em tempo real;
- salvar preferência em `%LOCALAPPDATA%\OMSI NavBR Multiplayer\language.txt`;
- não colocar textos de UI em providers, parsers ou contratos de rede;
- novas telas devem usar chaves de recurso.

## Telemetria

O primeiro backend é um leitor externo e somente leitura do processo OMSI. A implementação usa detecção de versão e mantém offsets/perfis isolados do restante do aplicativo.

GPS, HUD e multiplayer consomem `VehicleTelemetry`, sem conhecer como a telemetria foi obtida.

## Multiplayer peer-host

O modo padrão deixa de depender de um servidor público central. Ao criar uma sala:

1. `RoomHostService` inicia `NavBRServerApplication` dentro do processo do cliente;
2. Kestrel escuta em `0.0.0.0:27730`;
3. o jogador host conecta localmente por `127.0.0.1`;
4. os convidados conectam ao IPv4/endereço alcançável do host;
5. o host distribui presença, telemetria, chat e frames de voz somente aos membros da sala.

O servidor dedicado continua usando a mesma aplicação ASP.NET Core, evitando duas implementações diferentes do protocolo.

### Rede

A porta padrão inicial é **TCP 27730**.

- em LAN, o aplicativo lista endereços IPv4 utilizáveis;
- pela Internet, esta alpha pode exigir regra no Windows Firewall e port forwarding/NAT no roteador do host;
- UPnP/PCP/NAT-PMP ou outra estratégia de NAT traversal poderá ser adicionada depois;
- nenhuma identidade Steam é necessária.

## Chat de texto

Mensagens têm limite inicial de 280 caracteres. O servidor aplica identidade/nickname a partir da presença da conexão e distribui a mensagem pela sala.

O HUD expõe `T` como atalho inicial para abrir a entrada de texto. Enquanto fechado, o overlay fica click-through para não capturar comandos do OMSI.

## Chat por voz

Pipeline inicial:

```text
Microfone
  ↓ NAudio / PCM 48 kHz mono
Opus / Concentus
  ↓ frames de 20 ms ~24 kbit/s
SignalR / WebSocket
  ↓
PC host da sala
  ↓
Clientes remotos
  ↓ Opus decode + mixer NAudio
Áudio do jogador
```

`N` é o push-to-talk inicial. Apenas enquanto a tecla está pressionada o cliente codifica e transmite áudio.

SignalR/WebSocket foi escolhido para reduzir complexidade nesta alpha. Para reduzir latência e head-of-line blocking, a voz poderá migrar para UDP/WebRTC mantendo os mesmos conceitos de sala e identidade.

## HUD sobre o OMSI

O HUD é uma janela WPF transparente, sem moldura, sempre acima do OMSI, que acompanha posição/tamanho da janela do simulador.

Modo normal:

- click-through;
- minimapa no canto inferior esquerdo;
- chat temporário acima do minimapa;
- indicador de conexão e voz;
- outros jogadores no minimapa.

Modo de entrada de chat:

- o overlay fica interativo temporariamente;
- recebe texto;
- Enter envia;
- Esc fecha e restaura click-through.

A composição é inspirada em convenções de HUD de jogos de mundo aberto, mas usa identidade, formas e assets próprios do NavBR. Fullscreen exclusivo ainda precisa de validação; janela/borderless é o alvo inicial.

## Compatibilidade de mapas

Cada mapa recebe um identificador SHA-256 baseado em arquivos estruturais disponíveis localmente. A presença do jogador carrega esse identificador junto com o nome do mapa.

Jogadores com identificadores diferentes não são sobrepostos como se estivessem no mesmo mapa, mesmo que o nome textual seja igual. Isso reduz erros quando há versões/modificações diferentes do mesmo mapa.

## Licenças

O projeto próprio usa MIT. Dependências e avisos de redistribuição ficam em:

```text
LICENSE
THIRD_PARTY_NOTICES.md
licenses/
```

Os pacotes de release devem preservar esses arquivos quando as licenças das dependências exigirem aviso em redistribuição binária.
