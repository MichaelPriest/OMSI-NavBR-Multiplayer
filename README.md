# OMSI NavBR Multiplayer

Aplicativo de navegação e multiplayer para **OMSI 2**, independente da Steam.

> Versão em desenvolvimento: **0.2.0-alpha.3**

## Objetivo

O OMSI NavBR Multiplayer é um aplicativo Windows externo ao jogo, projetado para:

- detectar automaticamente qualquer instalação em execução via `Omsi.exe`;
- descobrir a pasta real do OMSI a partir do processo, sem depender de Steam/Steamworks;
- ler telemetria do ônibus local em tempo real;
- carregar mapas, roadmaps, paradas e `TTData` diretamente da instalação do OMSI;
- oferecer GPS/Route Advisor;
- conectar jogadores a salas multiplayer;
- mostrar outros jogadores no mapa;
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

### GPS — Fase 2 em andamento

O cliente já:

- encontra automaticamente a pasta `maps` da instalação detectada;
- cataloga mapas que possuem `global.cfg`;
- lê o nome do mapa quando disponível;
- conta tiles;
- detecta `whole.roadmap.bmp`, `roadmap.bmp` e variantes de roadmap;
- transforma GridX/GridY + posição local do tile em pixels do roadmap para mapas padrão de 300 m/tile;
- desenha o marcador do ônibus no roadmap;
- oferece zoom por botões e roda do mouse;
- permite pan por arraste;
- possui modo **Seguir ônibus**;
- possui comando **Ajustar**;
- gira o marcador conforme o heading recebido;
- pode permanecer **Sempre visível** sobre o OMSI.

A calibração final da posição e orientação do marcador ainda depende de teste real no OMSI 2.3.004. Mapas com `[worldcoordinates]` continuam separados até a georreferência correta ser implementada.

### Identidade visual

O cliente usa a identidade oficial **OMSI NavBR Multiplayer**, incluindo ícone próprio incorporado ao executável e à janela principal. O pipeline valida a presença do recurso Win32 de ícone no `.exe` standalone antes da publicação.

## Idiomas

A primeira base inclui:

- Português (Brasil) — `pt-BR`;
- English — `en-US`;
- Español — `es-ES`;
- Deutsch — `de-DE`;
- Français — `fr-FR`.

Na primeira execução, o NavBR tenta acompanhar o idioma do Windows. Se o idioma do sistema ainda não for suportado, usa inglês. A preferência fica salva em `%LOCALAPPDATA%\OMSI NavBR Multiplayer\language.txt`.

Os textos ficam em `.resx`, permitindo adicionar novos idiomas sem modificar o motor de telemetria, GPS ou multiplayer.

## Compatibilidade

O alvo inicial é **OMSI 2.3.004 no Windows**. O cliente não usa Steam API: ele localiza `Omsi.exe` em execução e deriva o diretório da instalação diretamente do processo.

A arquitetura separa detecção, perfis de memória, GPS e protocolo multiplayer para permitir suporte a outras builds compatíveis no futuro.

## Estrutura

```text
src/
  NavBR.Client/   aplicativo Windows/WPF, telemetria, GPS e localização
  NavBR.Server/   backend multiplayer em ASP.NET Core + SignalR
  NavBR.Shared/   DTOs e protocolo compartilhado

docs/
  ARCHITECTURE.md
  ROADMAP.md
  TELEMETRY.md
  RELEASES.md
```

## Stack

- .NET 10 LTS
- C# / WPF
- ASP.NET Core
- SignalR / WebSocket
- Windows `OpenProcess` / `ReadProcessMemory`
- leitura direta de `global.cfg`, roadmaps, tiles e `TTData`
- `.resx` + `ResourceManager` para localização

## Builds e Releases

O GitHub Actions compila cliente e servidor automaticamente. Cada nova versão gera um **GitHub Prerelease** com:

- `OMSI-NavBR-Multiplayer-vX.X.X-win-x86.exe` — cliente Windows x86 standalone/self-contained, pronto para abrir diretamente;
- `OMSI-NavBR-Multiplayer-vX.X.X-win-x86.zip` — pacote completo do cliente;
- `OMSI-NavBR-Server-vX.X.X-win-x64.zip` — servidor Windows x64 self-contained;
- release notes geradas automaticamente.

O `.exe` standalone inclui as dependências do .NET necessárias para execução e recebe o ícone oficial do NavBR como recurso Win32.

Durante a fase alpha serão publicados marcos incrementais para teste conforme as funcionalidades forem ficando utilizáveis.

## Segurança e escopo

O projeto não implementa bypass de DRM, ativação ou patches específicos para executáveis crackeados. A integração trabalha com um processo `Omsi.exe` já existente e compatível no computador do usuário.

## Licença

Ainda não definida.
