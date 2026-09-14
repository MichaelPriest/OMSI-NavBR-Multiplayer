# OMSI NavBR Multiplayer

Aplicativo de navegação e multiplayer para **OMSI 2**, independente da Steam.

> Versão em desenvolvimento: **0.3.0-alpha.3**

Site oficial: **https://michaelpriest.github.io/OMSI-NavBR-Multiplayer/**

## Objetivo

O OMSI NavBR Multiplayer é um aplicativo Windows externo ao jogo, projetado para:

- detectar automaticamente qualquer instalação em execução via `Omsi.exe`;
- descobrir a pasta real do OMSI a partir do processo, sem depender de Steam/Steamworks;
- ler telemetria do ônibus local em tempo real;
- carregar mapas, roadmaps, paradas e `TTData` diretamente da instalação do OMSI;
- oferecer GPS/Route Advisor e HUD sobre o jogo;
- conectar jogadores a salas multiplayer hospedadas pelo próprio criador da sala;
- mostrar outros jogadores no mapa e minimapa;
- oferecer chat de texto e chat por voz;
- futuramente experimentar sincronização de veículos remotos dentro do OMSI;
- oferecer interface multilíngue com troca de idioma em tempo real.

## Estado atual

### Telemetria local

Já implementado para o perfil inicial do **OMSI 2.3.004**:

- detecção do processo `Omsi.exe`;
- caminho real da instalação;
- fingerprint SHA-256 do executável;
- acesso externo somente leitura com `OpenProcess` / `ReadProcessMemory`;
- posição X/Y/Z;
- direção/heading;
- velocidade;
- nome do mapa carregado;
- atualização do dashboard a cada 200 ms.

A telemetria ainda precisa de validação em runtime no jogo para confirmar escala/sinal da posição, velocidade e orientação em mapas diferentes.

### GPS e HUD

O cliente já:

- encontra automaticamente a pasta `maps` da instalação detectada;
- cataloga mapas que possuem `global.cfg`;
- calcula um identificador de compatibilidade para ajudar a detectar versões diferentes do mesmo mapa;
- detecta `whole.roadmap.bmp`, `roadmap.bmp` e variantes de roadmap;
- transforma GridX/GridY + posição local do tile em pixels do roadmap para mapas padrão de 300 m/tile;
- desenha o marcador do ônibus no roadmap;
- oferece zoom, pan, modo **Seguir ônibus** e comando **Ajustar**;
- gira o marcador conforme o heading recebido;
- pode permanecer **Sempre visível** sobre o OMSI;
- possui um HUD compacto sobre o jogo com minimapa centralizado no ônibus;
- mostra outros jogadores compatíveis no minimapa;
- suaviza os marcadores remotos no GPS principal para reduzir saltos entre atualizações;
- mostra chat sobre o minimapa e indicador de voz.

O HUD segue uma organização inspirada em jogos de mundo aberto, com identidade visual própria do NavBR. Ele não copia assets ou interface proprietária de GTA/Rockstar.

Atalhos iniciais no HUD:

- `T` — abrir chat de texto;
- `N` — segurar para falar no chat por voz.

A sobreposição é voltada inicialmente a OMSI em modo janela ou janela sem bordas. Overlay em fullscreen exclusivo ainda precisa de validação.

### Multiplayer peer-host — Fase 3

Na série **0.3 alpha**, o servidor da sala é o **PC de quem cria a sala**. O próprio cliente NavBR inicia um host ASP.NET Core/SignalR local e entra nele automaticamente.

Fluxo básico:

1. O criador clica em **Criar sala neste PC**.
2. O NavBR inicia o host na porta TCP `27730`.
3. O criador compartilha o endereço acessível e o código/nome da sala; na alpha.3 há um botão rápido de copiar os dados do convite.
4. Os demais jogadores informam esse endereço e entram na sala.
5. Telemetria, presença, chat e voz passam pelo PC do host.

Em rede local, o NavBR mostra automaticamente os endereços IPv4 disponíveis. Para jogadores fora da mesma rede, nesta alpha o host pode precisar liberar o NavBR no Windows Firewall e encaminhar a porta TCP `27730` no roteador. UPnP/NAT traversal é uma evolução planejada para reduzir essa configuração manual.

O pacote `OMSI-NavBR-Server` continua disponível para quem quiser executar um host dedicado em outro PC ou servidor.

### Chat e voz

O multiplayer inclui:

- chat de texto por sala, limitado a 280 caracteres por mensagem;
- mensagens visíveis na janela multiplayer e no HUD;
- voz push-to-talk por sala;
- captura e reprodução de áudio pelo NAudio;
- codificação Opus via Concentus em 48 kHz mono, quadros de 20 ms;
- indicador visual de quem está falando.

Nesta alpha a voz é transportada pelo mesmo canal SignalR/WebSocket da sessão. Isso simplifica o peer-host inicial, mas pode ter mais latência sob perda de rede do que um transporte UDP/WebRTC; uma camada de voz de baixa latência pode substituir esse transporte futuramente sem alterar o HUD.

## Idiomas

A base inclui:

- Português (Brasil) — `pt-BR`;
- English — `en-US`;
- Español — `es-ES`;
- Deutsch — `de-DE`;
- Français — `fr-FR`.

Na primeira execução, o NavBR tenta acompanhar o idioma do Windows. Se o idioma do sistema ainda não for suportado, usa inglês. A preferência fica salva em `%LOCALAPPDATA%\OMSI NavBR Multiplayer\language.txt`.

Configurações do multiplayer ficam em `%LOCALAPPDATA%\OMSI NavBR Multiplayer\multiplayer.json`.

## Compatibilidade

O alvo inicial é **OMSI 2.3.004 no Windows**. O cliente não usa Steam API: ele localiza `Omsi.exe` em execução e deriva o diretório da instalação diretamente do processo.

A arquitetura separa detecção, perfis de memória, GPS e protocolo multiplayer para permitir suporte a outras builds compatíveis no futuro.

## Estrutura

```text
src/
  NavBR.Client/   WPF, telemetria, GPS, HUD, multiplayer, voz e localização
  NavBR.Server/   host multiplayer em ASP.NET Core + SignalR
  NavBR.Shared/   DTOs e protocolo compartilhado

docs/
  ARCHITECTURE.md
  ROADMAP.md
  TELEMETRY.md
  RELEASES.md
licenses/
  licenças das dependências redistribuídas
```

## Stack

- .NET 10 LTS
- C# / WPF
- ASP.NET Core / Kestrel
- SignalR / WebSocket
- NAudio
- Concentus / Opus
- Windows `OpenProcess` / `ReadProcessMemory`
- leitura direta de `global.cfg`, roadmaps, tiles e `TTData`
- `.resx` + `ResourceManager` para localização

## Builds e Releases

O GitHub Actions compila cliente e servidor automaticamente. Cada nova versão gera um **GitHub Prerelease** com:

- `OMSI-NavBR-Multiplayer-vX.X.X-win-x86.exe` — cliente Windows x86 standalone/self-contained;
- `OMSI-NavBR-Multiplayer-vX.X.X-win-x86.zip` — pacote completo do cliente;
- `OMSI-NavBR-Server-vX.X.X-win-x64.zip` — servidor/host dedicado Windows x64;
- `LICENSE` e `THIRD_PARTY_NOTICES.md` para os avisos legais do projeto e dependências.

O `.exe` standalone inclui o runtime necessário e recebe o ícone oficial do NavBR como recurso Win32.

## Segurança e escopo

O projeto não implementa bypass de DRM, ativação ou patches específicos para executáveis crackeados. A integração trabalha com um processo `Omsi.exe` já existente e compatível no computador do usuário.

No multiplayer são transmitidos dados do jogo/sessão, mensagens de chat e, quando ativado, áudio do microfone durante o push-to-talk. O NavBR não usa a localização física do usuário para posicionar jogadores; os endereços de rede são necessários somente para conectar ao PC que hospeda a sala.

## Licença

O código próprio do **OMSI NavBR Multiplayer** é disponibilizado sob licença **MIT**. Veja `LICENSE`.

Dependências de terceiros e seus respectivos textos de licença estão documentados em `THIRD_PARTY_NOTICES.md` e na pasta `licenses/`. OMSI, Steam, GTA/Rockstar e demais marcas citadas pertencem aos respectivos titulares; o NavBR é um projeto independente.
