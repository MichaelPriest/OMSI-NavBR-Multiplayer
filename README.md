# OMSI NavBR Multiplayer

Aplicativo de navegação e multiplayer para **OMSI 2**, independente da Steam.

## Objetivo

O OMSI NavBR Multiplayer será um aplicativo Windows externo ao jogo, capaz de:

- detectar automaticamente qualquer instalação em execução via `Omsi.exe`;
- descobrir a pasta real do OMSI a partir do processo, sem depender de Steam/Steamworks;
- ler telemetria do ônibus local (posição, direção, velocidade e estado da viagem);
- carregar mapas, roadmaps, paradas e `TTData` diretamente da instalação do OMSI;
- exibir GPS/Route Advisor em tempo real;
- conectar jogadores a salas multiplayer;
- mostrar outros jogadores no mapa;
- evoluir posteriormente para sincronização de veículos remotos dentro do OMSI;
- oferecer interface multilíngue com troca de idioma em tempo real.

## Idiomas

A arquitetura de localização já está ativa no cliente. A primeira base inclui:

- Português (Brasil) — `pt-BR`;
- English — `en-US`;
- Español — `es-ES`;
- Deutsch — `de-DE`;
- Français — `fr-FR`.

Na primeira execução, o NavBR tenta acompanhar o idioma do Windows. Se o idioma do sistema ainda não for suportado, usa inglês. O usuário pode trocar o idioma na interface e a preferência fica salva em `%LOCALAPPDATA%\OMSI NavBR Multiplayer\language.txt`.

Os textos ficam em arquivos `.resx`, permitindo adicionar novos idiomas sem alterar o motor de telemetria, navegação ou multiplayer.

## Compatibilidade planejada

O alvo inicial é **OMSI 2.3.004** no Windows. A arquitetura separa detecção do processo, leitura de memória e protocolo multiplayer para permitir perfis adicionais de versões legítimas do OMSI no futuro.

Não há dependência da Steam API. O programa localiza o `Omsi.exe` em execução e deriva o diretório de instalação a partir dele.

## Estrutura

```text
src/
  NavBR.Client/   Aplicativo Windows/WPF, GPS, overlay e localização
  NavBR.Server/   Backend multiplayer em ASP.NET Core + SignalR
  NavBR.Shared/   DTOs e protocolo compartilhado

docs/
  ARCHITECTURE.md
  ROADMAP.md
```

## Stack inicial

- .NET 10 LTS
- C# / WPF
- ASP.NET Core
- SignalR/WebSocket
- Windows `OpenProcess` / `ReadProcessMemory` para telemetria
- leitura direta de `global.cfg`, tiles e `TTData`
- recursos `.resx` + `ResourceManager` para localização

## Estado

**Fase 0 — bootstrap do projeto.**

Primeiro marco técnico:

1. detectar `Omsi.exe`;
2. mostrar PID, versão e diretório da instalação;
3. estabelecer a camada de telemetria;
4. transmitir um snapshot de telemetria para o servidor multiplayer.

A interface base já suporta troca de idioma em tempo real nos cinco idiomas iniciais.

## Segurança e escopo

O projeto não implementará bypass de DRM, ativação ou patches para executáveis crackeados. A integração é feita com uma instalação do OMSI já existente no computador do usuário.

## Licença

Ainda não definida.
