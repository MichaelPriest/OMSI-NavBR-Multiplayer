# Arquitetura

## Princípios

1. **Sem dependência da Steam**: o cliente encontra `Omsi.exe` em execução e deriva a instalação pelo caminho do processo.
2. **Integração desacoplada**: telemetria, mapa, navegação, HUD, voz e multiplayer são módulos separados.
3. **Compatibilidade por perfil**: cada build suportada do OMSI possui um perfil de leitura próprio.
4. **Somente leitura no OMSI**: o NavBR não injeta código e não grava na memória do simulador.
5. **Sem DRM bypass**: o projeto não modifica ativação/licenciamento do OMSI.
6. **Peer-host por padrão**: o PC que cria a sala executa o host multiplayer embutido.
7. **Servidor dedicado opcional**: `NavBR.Server` continua podendo ser executado separadamente.
8. **Localização desde a base**: módulos de domínio não dependem de texto de interface; idiomas são tratados no cliente por recursos.
9. **Fallback seguro**: quando uma geometria/estrutura não pode ser resolvida com segurança, o NavBR prefere reduzir precisão em vez de inventar dados.

## Fluxo

```text
Omsi.exe
  │
  ├─ Process detector
  ├─ Version/profile detector
  └─ Telemetry provider (read-only)
         │
         ▼
   NavBR.Client
     ├─ Localization
     ├─ Map catalog / fingerprint
     ├─ TTData / route trace reader
     ├─ Navigation state
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

O cliente é WPF em .NET 10 para Windows, compilado em **x86** para acompanhar a arquitetura do OMSI 2.

### Camadas principais

- `OmsiProcessDetector`: encontra PID, executável, versão e pasta da instalação.
- perfis de compatibilidade: isolam offsets/estruturas por build do OMSI.
- `ITelemetryProvider`: abstrai leitura externa do processo.
- `LocalizationService`: detecta idioma, carrega recursos e persiste preferência.
- catálogo/leitor de mapas: `global.cfg`, tiles, roadmaps e fingerprint de compatibilidade.
- leitor de rota/TTData: resolve `TTData`, `Chrono/*/TTData`, `.ttp -> .ttr` e a sequência de entradas do trajeto.
- leitor de geometria: associa índice de tile, `ObjectId`, `PathId`, tiles `.map`, splines `.sli` e crossings `.sco`.
- navegação/HUD: linha, destino, próxima parada, traçado da viagem e renderização do minimapa.
- `MultiplayerClientService`: sala, presença, telemetria, chat e voz.
- `RoomHostService`: inicia/encerra o servidor da sala no PC do criador.
- `VoiceChatService`: captura/reprodução e codec Opus.
- `HudOverlayWindow`: minimapa, chat e presença de voz sobre o OMSI.

### Itens ainda não completos na navegação

O código atual não deve ser descrito como tendo uma engine completa de operação. Ainda faltam, entre outros:

- parser dedicado de `Busstops.cfg`;
- parser dedicado de `.ttl`, se necessário;
- distância restante;
- ETA;
- atraso/adiantamento;
- instruções avançadas de navegação por trecho/manobra.

## Internacionalização

A interface usa `ResourceManager` e arquivos `.resx` no namespace de recursos do cliente.

Idiomas atuais:

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

O backend principal é um leitor externo e somente leitura do processo OMSI. A implementação usa detecção de versão/perfil e mantém offsets isolados do restante do aplicativo.

GPS, HUD e multiplayer consomem dados de telemetria sem conhecer como eles foram obtidos.

Perfis atuais:

- **2.3.004** — alvo principal;
- **2.2.032** — suporte técnico existente, ainda aguardando validação real mais ampla.

## Mapas e rota

O NavBR lê diretamente os arquivos instalados do mapa.

Fluxo simplificado:

```text
global.cfg
  ├─ tiles .map
  └─ ordem dos blocos [map]

TTData / Chrono/*/TTData
  └─ .ttp -> .ttr
          │
          ├─ ObjectId
          ├─ PathId
          └─ TileIndex
                 │
                 ▼
         global.cfg [map]
                 │
                 ▼
        GridX/GridY + tile
                 │
        ┌────────┴─────────┐
        ▼                  ▼
    splines .sli      crossings .sco
        │                  │
        └────────┬─────────┘
                 ▼
           rota no GPS/HUD
```

Na alpha.9, o terceiro campo relevante de `[track_entry]` é tratado como **índice de tile**, resolvido pela ordem dos blocos `[map]` do `global.cfg`.

Se a geometria detalhada não puder ser resolvida de forma confiável, o renderer mantém fallback por tiles.

## Multiplayer peer-host

O modo padrão não depende de um servidor público central. Ao criar uma sala:

1. `RoomHostService` inicia a aplicação de servidor dentro do processo do cliente;
2. Kestrel escuta inicialmente em `0.0.0.0:27730`;
3. o jogador host conecta localmente;
4. os convidados conectam ao IPv4/endereço alcançável do host;
5. o host distribui presença, telemetria, chat e frames de voz aos membros da sala.

O servidor dedicado continua usando a mesma base ASP.NET Core/SignalR para evitar protocolos paralelos.

### Convites

O cliente usa um formato de convite versionado:

```text
NAVBR_INVITE_V1
```

O objetivo é permitir evolução futura sem quebrar convites antigos de forma silenciosa.

### Rede

A porta padrão inicial é **TCP 27730**.

- em LAN, o aplicativo lista endereços IPv4 utilizáveis;
- pela Internet, a alpha pode exigir regra no Windows Firewall e port forwarding/NAT no roteador do host;
- UPnP/NAT traversal continua pendente;
- reconnect automático continua pendente;
- rate limiting e códigos de erro de rede mais estruturados continuam pendentes;
- nenhuma identidade Steam é necessária.

## Chat de texto

Mensagens têm limite inicial de **280 caracteres**. O servidor aplica identidade/nickname a partir da presença da conexão e distribui a mensagem pela sala.

O HUD usa atualmente **F9** como atalho padrão para abrir o chat. As combinações podem ser configuradas e são verificadas contra `Inputs/keyboard.cfg` do OMSI.

Enquanto o campo de chat está fechado, o overlay permanece voltado a não capturar comandos desnecessários do simulador.

## Chat por voz

Pipeline atual:

```text
Microfone
  ↓ NAudio / PCM 48 kHz mono
Opus / Concentus
  ↓ frames de 20 ms
SignalR / WebSocket
  ↓
PC host da sala
  ↓
Clientes remotos
  ↓ Opus decode + NAudio
Áudio do jogador
```

O push-to-talk usa **F10** como atalho padrão. Chat e PTT precisam usar combinações diferentes.

SignalR/WebSocket foi escolhido para reduzir complexidade nesta alpha. Para reduzir latência e head-of-line blocking, a voz poderá futuramente migrar para UDP/WebRTC sem alterar os conceitos de sala e identidade.

Voz e latência ainda precisam de validação real entre computadores.

## HUD sobre o OMSI

O HUD é uma janela WPF transparente e sem moldura que acompanha a janela de gameplay do simulador.

Na alpha.9, o lifecycle do HUD aprende o HWND real de gameplay quando o OMSI está em primeiro plano e tenta diferenciar essa superfície de janelas auxiliares do mesmo processo.

Objetivos atuais:

- mostrar o HUD durante a condução;
- ocultar sobre menus/opções/timetable/diálogos auxiliares;
- reaparecer quando o usuário retorna ao gameplay;
- manter minimapa, chat, jogadores e indicador de voz disponíveis sem interferir na condução.

Fullscreen exclusivo ainda precisa de validação; janela/borderless é o alvo inicial.

## Compatibilidade de mapas

Cada mapa recebe um identificador SHA-256 baseado em arquivos estruturais disponíveis localmente. A presença do jogador carrega esse identificador junto com o nome do mapa.

Jogadores com identificadores diferentes não devem ser sobrepostos como se estivessem usando exatamente a mesma versão do mapa.

## Estado de validação da alpha.9

O CI confirma build/publicação, mas não substitui teste no OMSI real.

Ainda precisam de validação end-to-end:

- HUD e menus;
- posição/velocidade/heading;
- traçado detalhado;
- destino/próxima parada;
- multiplayer entre dois computadores;
- voz/PTT;
- atalhos;
- Internet/NAT;
- servidor dedicado em outro computador.

## Licenças

O projeto próprio usa MIT. Dependências e avisos de redistribuição ficam em:

```text
LICENSE
THIRD_PARTY_NOTICES.md
licenses/
```

Os pacotes de release preservam esses avisos legais.
