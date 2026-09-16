# OMSI NavBR Multiplayer

[![Downloads](https://img.shields.io/github/downloads/MichaelPriest/OMSI-NavBR-Multiplayer/total?label=downloads&color=22c77a)](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases)

Aplicativo de navegação e multiplayer para **OMSI 2**, independente da Steam, com HUD/GPS, salas peer-host, chat, voz, integração experimental com o OMSI e suporte a cockpit físico via Arduino/ESP32.

> Versão em desenvolvimento: **0.3.0-alpha.11**  
> Teste público atual: **[v0.3.0-alpha.11-test.4](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.11-test.4)**  
> Release oficial anterior: **[v0.3.0-alpha.10](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.10)**

**Site oficial:** https://michaelpriest.github.io/OMSI-NavBR-Multiplayer/  
**Todas as releases:** https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases  
**Manual:** [docs/MANUAL_DE_USO.md](docs/MANUAL_DE_USO.md)  
**Checklist da Test 4:** [docs/ALPHA11_TEST4_COMMUNITY.md](docs/ALPHA11_TEST4_COMMUNITY.md)  
**Hardware Cockpit:** [docs/HARDWARE_COCKPIT.md](docs/HARDWARE_COCKPIT.md)

## Alpha.11 Test 4

A `v0.3.0-alpha.11-test.4` é a pré-release pública atual para validação da comunidade. Ela reúne a modernização da interface, correções de telemetria e a primeira versão funcional do **Hardware Cockpit Bridge**.

Principais mudanças:

- nova navegação da janela principal com foco em **Visão geral**, **Navegação**, **Multiplayer**, **Hardware cockpit** e **Ferramentas avançadas**;
- menu lateral com rolagem vertical e menos poluição visual;
- velocidade corrigida usando o `Tacho` do OMSI em km/h como fonte principal, com `Groundspeed` e vetor físico como fallback;
- Hardware Cockpit por USB/Serial, com seleção de porta COM, baud rate e conexão/desconexão;
- protocolo `NAVBR_HW_V1` em JSON Lines, aproximadamente a 5 Hz;
- leitura de `haltewunsch` para `stopRequested`, permitindo LED físico de **PARADA SOLICITADA**;
- exemplo Arduino/ESP32 incluído em `examples/NavBR.Hardware.Serial/NavBR_Hardware_Serial.ino`;
- suporte a `currentStreet` através de perfis `NavBR.streets.json`, sem inventar nomes de ruas quando o mapa não fornece dados confiáveis;
- plugin Native AOT x86 atualizado para observar `Velocity` e `haltewunsch`;
- CI atualizado para cancelar builds antigos substituídos por commits mais novos da mesma branch.

### Teste prioritário: velocidade

Compare durante aceleração, velocidade constante e frenagem:

1. velocímetro do ônibus no OMSI;
2. velocidade exibida pelo NavBR/HUD;
3. `speedKph` no preview do Hardware Cockpit.

Se houver diferença relevante, informe o modelo exato do ônibus para ajustarmos o perfil sem quebrar os demais veículos.

## Hardware Cockpit Bridge

O NavBR pode enviar sua telemetria normalizada para Arduino/ESP32 sem o microcontrolador acessar a memória do OMSI.

Configuração inicial recomendada:

```text
Protocolo: NAVBR_HW_V1
Transporte: USB / Serial
Baud rate: 115200
Formato: JSON Lines
Frequência: ~5 Hz
Fluxo: OMSI -> NavBR -> Arduino/ESP32
```

Entre os campos disponíveis estão:

- linha e rota;
- destino;
- próxima parada;
- rua atual quando houver perfil confiável;
- velocidade;
- atraso;
- portas;
- luzes e setas;
- buzina e limpadores;
- freio de estacionamento e marcha à ré;
- `stopRequested` para a luz de parada solicitada.

A fase atual é **somente saída para o hardware**. Botões físicos enviando comandos de volta ao OMSI ficam para uma etapa posterior e separada.

Consulte [docs/HARDWARE_COCKPIT.md](docs/HARDWARE_COCKPIT.md) para protocolo, configuração e perfil de ruas.

## HUD, GPS e rota

A Alpha.11 trabalha com:

- posição local e absoluta do veículo;
- `GridX/GridY` + coordenadas locais de tile;
- heading/quaternion;
- velocidade;
- linha/track, destino e próxima parada quando o timetable fornece os dados;
- rota ativa a partir de `.ttp/.ttr`, tiles `.map`, splines `.sli` e paths de objetos/crossings `.sco`;
- roadmaps como `whole.roadmap.bmp` quando disponíveis;
- marcadores de jogadores remotos suavizados;
- ocultação do HUD em menus e janelas auxiliares do OMSI.

### Regra das paradas

- **Rota/viagem ativa:** somente as paradas daquela rota devem aparecer.
- **Sem rota ativa:** todas as paradas válidas do mapa podem aparecer.
- O NavBR tenta resolver a sequência real de `[station]` no `.ttp`; quando isso não é possível, usa a geometria da rota como fallback.

## Multiplayer

O fluxo principal é **peer-host**:

1. quem cria a sala hospeda o servidor no próprio PC;
2. porta inicial: TCP `27730`;
3. convidados entram pelo endereço do host e sala;
4. SignalR transporta presença, telemetria, chat, voz e estados compartilhados;
5. servidor dedicado x64 continua disponível como alternativa.

Meta inicial: **até 32 jogadores por sala**. O limite de até 48 veículos de tráfego IA por snapshot é separado do número de jogadores.

Atalhos padrão:

- `F9` — chat de texto;
- `F10` — segurar para falar.

## Plugin e integração OMSI

A camada experimental inclui:

- plugin Native AOT x86;
- `.opl` carregado pelo OMSI;
- Named Pipe local cliente ↔ plugin;
- protocolo bridge versionado;
- telemetria rápida de `Velocity` e `haltewunsch`;
- fila de comandos executada no callback/thread do OMSI;
- shim C++ x86 para o ABI Borland/Delphi do OMSI 2.3.004;
- registro protegido `VehicleInstanceId → ponteiro OMSI` para veículos criados pelo NavBR;
- validação de ponteiros e lista `RoadVehicles` antes de writes físicos;
- opt-in explícito para operações experimentais.

A telemetria normal continua prioritariamente de leitura. Escritas físicas permanecem isoladas no caminho experimental do plugin.

## Pacotes da Test 4

A pré-release publica:

```text
OMSI-NavBR-Multiplayer-v0.3.0-alpha.11-test.4-win-x86.exe
OMSI-NavBR-Multiplayer-v0.3.0-alpha.11-test.4-win-x86.zip
OMSI-NavBR-Server-v0.3.0-alpha.11-test.4-win-x64.zip
OMSI-NavBR-Plugin-v0.3.0-alpha.11-test.4-win-x86.zip
ALPHA11_TEST4_COMMUNITY.md
HARDWARE_COCKPIT.md
NavBR_Hardware_Serial.ino
SHA256SUMS.txt
LICENSE
THIRD_PARTY_NOTICES.md
```

Para a maioria dos usuários, o **EXE standalone x86** é o arquivo recomendado.

## Compatibilidade

- Windows;
- OMSI 2.3.004 como alvo principal;
- cliente WPF x86;
- servidor dedicado x64 opcional;
- .NET 10 self-contained nos pacotes publicados;
- independente de Steam API/Steamworks.

## Stack

- .NET 10 / C# / WPF x86
- ASP.NET Core / Kestrel / SignalR
- NAudio + Concentus/Opus
- Windows Named Pipes
- System.IO.Ports / Serial
- Native AOT
- C++/MSVC x86 no interop experimental
- `.resx` / `ResourceManager`

## Documentação

- [Manual de uso](docs/MANUAL_DE_USO.md)
- [Alpha.11 Test 4 — comunidade](docs/ALPHA11_TEST4_COMMUNITY.md)
- [Hardware Cockpit Bridge](docs/HARDWARE_COCKPIT.md)
- [Desenvolvimento Alpha.11](docs/ALPHA11_DEVELOPMENT.md)
- [Plugin OMSI experimental](docs/OMSI_PLUGIN_EXPERIMENTAL.md)
- [HUD e voz](docs/HUD_AND_VOICE.md)
- [Telemetria](docs/TELEMETRY.md)
- [Arquitetura](docs/ARCHITECTURE.md)
- [Releases](docs/RELEASES.md)
- [Roadmap](docs/ROADMAP.md)

## Apoie o desenvolvimento

O **OMSI NavBR Multiplayer** é um projeto independente e público. Contribuições são voluntárias e ajudam com desenvolvimento, infraestrutura e testes da comunidade.

**Pix — chave aleatória:** `b07a9cc9-b10d-48a8-b201-d28bddc4399a`

O projeto continua público no GitHub e o código próprio do NavBR permanece sob licença MIT.

## Créditos

**Desenvolvedor:** MichaelPriest  
**Apoio ao desenvolvimento:** IA ChatGPT

## Licença

O código próprio do OMSI NavBR Multiplayer é disponibilizado sob licença **MIT**. Dependências e avisos de terceiros estão em `THIRD_PARTY_NOTICES.md` e `licenses/`.
