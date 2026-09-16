# OMSI NavBR Multiplayer

[![Downloads](https://img.shields.io/github/downloads/MichaelPriest/OMSI-NavBR-Multiplayer/total?label=downloads&color=22c77a)](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases)

Aplicativo de navegação e multiplayer para **OMSI 2**, independente da Steam, com HUD/GPS, peer-host, salas públicas/privadas, chat, voz, CCO, integração experimental com o OMSI e Hardware Cockpit.

> Versão em desenvolvimento: **0.3.0-alpha.12**  
> Teste público atual: **[v0.3.0-alpha.12-test.1](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.12-test.1)**  
> Release anterior: **[v0.3.0-alpha.11-test.4](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.11-test.4)**

**Site oficial:** https://michaelpriest.github.io/OMSI-NavBR-Multiplayer/  
**Releases:** https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases  
**Checklist Alpha.12 Test 1:** [docs/ALPHA12_TEST1_COMMUNITY.md](docs/ALPHA12_TEST1_COMMUNITY.md)  
**Escopo mestre Alpha.12:** [docs/ALPHA12_MASTER_SCOPE.md](docs/ALPHA12_MASTER_SCOPE.md)  
**Hardware Cockpit:** [docs/HARDWARE_COCKPIT.md](docs/HARDWARE_COCKPIT.md)

## Alpha.12 Test 1

A `v0.3.0-alpha.12-test.1` é a primeira pré-release pública da Alpha.12. Ela foi criada para validar a nova base enquanto os módulos restantes continuam sendo incorporados e podem aparecer como **Em desenvolvimento** ou **Experimental**.

### Incluído nesta Alpha

- novo shell Alpha.12 e HUD redesenhado;
- perfil local do motorista e estatísticas;
- empresa virtual, frota e CCO básico;
- multiplayer peer-host TCP `27730`;
- diagnóstico de conectividade, UPnP opt-in e saúde da sessão;
- salas privadas com senha efêmera e navegador de salas públicas do servidor;
- chat e PTT;
- voz Geral, Empresa/Equipe, CCO e Proximidade;
- mute, deafen, ganho por jogador e seleção de dispositivos de áudio;
- plugin Native AOT x86 e bridge v2 experimentais;
- Hardware Cockpit Serial preservado.

### Em desenvolvimento na Alpha.12

- presença global e descoberta opcional de salas pela Internet;
- NAT traversal/fallback avançado;
- ônibus remoto físico 3D completo e tráfego IA compartilhado;
- Hardware Cockpit Wi-Fi/ESP32, displays e entradas físicas;
- navegação avançada com ETA/distâncias/manobras;
- CCO avançado, permissões e moderação;
- replay/Ghost, mapa web ao vivo e eventos;
- SDK/API, workshop e companion/mobile.

## Multiplayer

O fluxo principal continua **peer-host**:

1. quem cria a sala hospeda no próprio PC;
2. porta inicial TCP `27730`;
3. convidados entram pelo endereço do host;
4. SignalR transporta presença, telemetria, chat, voz e estados compartilhados;
5. servidor dedicado x64 continua opcional.

A Alpha.12 adiciona salas privadas e um navegador das salas públicas anunciadas pelo servidor configurado. Salas privadas não aparecem nesse diretório.

## Voz

Atalhos padrão:

- `F9` — chat;
- `F10` — segurar para falar.

A Alpha.12 Test 1 inclui canais Geral, Empresa/Equipe, CCO e Proximidade. Também há mute, deafen, ganho individual e seleção de microfone/saída.

## HUD, GPS e OMSI

O HUD continua aprendendo e preservando a janela real de gameplay do OMSI e deve ficar oculto em menus, opções e janelas auxiliares. A telemetria normal permanece prioritariamente de leitura; operações físicas continuam isoladas atrás de opt-in experimental.

## Hardware Cockpit

Base atual:

```text
Protocolo: NAVBR_HW_V1
Transporte: USB / Serial
Baud rate: 115200
Formato: JSON Lines
Frequência: ~5 Hz
Fluxo: OMSI -> NavBR -> Arduino/ESP32
```

Consulte [docs/HARDWARE_COCKPIT.md](docs/HARDWARE_COCKPIT.md).

## Pacotes da Alpha.12 Test 1

```text
OMSI-NavBR-Multiplayer-v0.3.0-alpha.12-test.1-win-x86.exe
OMSI-NavBR-Multiplayer-v0.3.0-alpha.12-test.1-win-x86.zip
OMSI-NavBR-Server-v0.3.0-alpha.12-test.1-win-x64.zip
OMSI-NavBR-Plugin-v0.3.0-alpha.12-test.1-win-x86.zip
ALPHA12_TEST1_COMMUNITY.md
HARDWARE_COCKPIT.md
SHA256SUMS.txt
LICENSE
THIRD_PARTY_NOTICES.md
```

Para a maioria dos usuários, o **EXE standalone x86** é o pacote recomendado.

## Compatibilidade

- Windows;
- OMSI 2.3.004 como alvo principal;
- cliente WPF x86;
- servidor dedicado x64 opcional;
- .NET 10 self-contained;
- independente da Steam API/Steamworks.

## Stack

- .NET 10 / C# / WPF x86
- ASP.NET Core / Kestrel / SignalR
- NAudio + Concentus/Opus
- Windows Named Pipes
- System.IO.Ports / Serial
- Native AOT
- C++/MSVC x86 para o interop experimental

## Documentação

- [Alpha.12 Test 1 — comunidade](docs/ALPHA12_TEST1_COMMUNITY.md)
- [Escopo mestre Alpha.12](docs/ALPHA12_MASTER_SCOPE.md)
- [Manual de uso](docs/MANUAL_DE_USO.md)
- [Hardware Cockpit Bridge](docs/HARDWARE_COCKPIT.md)
- [Plugin OMSI experimental](docs/OMSI_PLUGIN_EXPERIMENTAL.md)
- [HUD e voz](docs/HUD_AND_VOICE.md)
- [Telemetria](docs/TELEMETRY.md)
- [Arquitetura](docs/ARCHITECTURE.md)
- [Releases](docs/RELEASES.md)
- [Roadmap](docs/ROADMAP.md)

## Apoie o desenvolvimento

O **OMSI NavBR Multiplayer** é um projeto independente e público. Contribuições são voluntárias e ajudam com desenvolvimento, infraestrutura e testes da comunidade.

**Pix — chave aleatória:** `b07a9cc9-b10d-48a8-b201-d28bddc4399a`

## Créditos

**Desenvolvedor:** MichaelPriest  
**Apoio ao desenvolvimento:** IA ChatGPT

## Licença

O código próprio do OMSI NavBR Multiplayer é disponibilizado sob licença **MIT**. Dependências e avisos de terceiros estão em `THIRD_PARTY_NOTICES.md` e `licenses/`.
