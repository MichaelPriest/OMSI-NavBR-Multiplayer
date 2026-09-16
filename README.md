# OMSI NavBR Multiplayer

[![Downloads](https://img.shields.io/github/downloads/MichaelPriest/OMSI-NavBR-Multiplayer/total?label=downloads&color=22c77a)](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases)

Aplicativo de navegação e multiplayer para **OMSI 2**, independente da Steam.

> Versão em desenvolvimento: **0.3.0-alpha.11**  
> Teste público atual: **[v0.3.0-alpha.11-test.1](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.11-test.1)**  
> Última release oficial da série: **[v0.3.0-alpha.10](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.10)**

**Site oficial:** https://michaelpriest.github.io/OMSI-NavBR-Multiplayer/  
**Todas as releases:** https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases  
**Alpha.11 Test 1:** https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.11-test.1

Manual de uso: **[docs/MANUAL_DE_USO.md](docs/MANUAL_DE_USO.md)**  
Como gerar o roadmap dos mapas: **[docs/GERAR_ROADMAP_MAPAS.md](docs/GERAR_ROADMAP_MAPAS.md)**  
Roadmap: **[docs/ROADMAP.md](docs/ROADMAP.md)**  
Desenvolvimento da Alpha.11: **[docs/ALPHA11_DEVELOPMENT.md](docs/ALPHA11_DEVELOPMENT.md)**

## Objetivo

O OMSI NavBR Multiplayer é um aplicativo Windows externo ao jogo, projetado para:

- detectar automaticamente o `Omsi.exe` em execução;
- descobrir a pasta real do OMSI sem depender da Steam/Steamworks;
- ler telemetria do ônibus local em tempo real;
- carregar mapas, roadmaps e `TTData` diretamente da instalação do OMSI;
- oferecer GPS, Route Advisor e HUD sobre o jogo;
- mostrar paradas, próxima parada e visão geral da rota;
- conectar jogadores a salas multiplayer hospedadas pelo próprio criador da sala;
- mostrar outros jogadores no mapa e minimapa;
- oferecer chat de texto e voz com push-to-talk;
- sincronizar telemetria e preparar a infraestrutura para veículos remotos e tráfego compartilhado;
- oferecer interface multilíngue em português, inglês, espanhol, alemão e francês.

## Estado atual

### Teste público — Alpha.11 Test 1

A **v0.3.0-alpha.11-test.1** é a primeira build pública de teste da série Alpha.11 e está disponível com:

- cliente **EXE standalone x86**;
- cliente em ZIP;
- servidor dedicado x64 opcional;
- pacote do plugin OMSI experimental x86;
- `LICENSE` e `THIRD_PARTY_NOTICES.md`.

**Baixar a Alpha.11 Test 1:**  
https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.11-test.1

Principais avanços da série Alpha.11:

- HUD/minimapa com acompanhamento da janela real de gameplay do OMSI;
- ocultação do HUD em menus e janelas auxiliares sem voltar à lógica rígida de `MainWindowHandle`;
- leitura e desenho de pontos de parada do mapa;
- destaque da próxima parada;
- ícone de parada padrão OMSI, minimalista ou personalizado;
- botão **Rota completa** para enquadrar todo o percurso;
- melhorias no Roadmap Studio e limites de grade vindos do `global.cfg`;
- telemetria 3D com posição local e quaternion nativos do OMSI;
- infraestrutura de sincronização de tráfego com autoridade do host;
- bridge local cliente ↔ plugin por Named Pipes;
- evolução do plugin experimental e da camada nativa x86 para integração profunda com o OMSI 2.3.004.

> **Importante:** a representação física de ônibus remotos e do tráfego sincronizado dentro do mundo 3D do OMSI ainda está em desenvolvimento. A Test 1 não deve ser apresentada como tendo spawn físico funcional.

A branch principal de desenvolvimento desta fase é:

```text
feature/alpha11-deep-omsi-integration
```

### Release oficial anterior — Alpha.10

A **v0.3.0-alpha.10** continua disponível como a release oficial anterior da série 0.3:

https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.10

Ela permanece como referência anterior enquanto os recursos da Alpha.11 passam por testes reais no OMSI.

## Telemetria local

Já implementado para os perfis suportados do OMSI:

- detecção do processo `Omsi.exe`;
- caminho real da instalação;
- fingerprint SHA-256 do executável;
- acesso externo somente leitura com `OpenProcess` / `ReadProcessMemory`;
- posição X/Y/Z;
- posição local nativa do veículo;
- quaternion/rotação nativa;
- direção/heading;
- velocidade;
- nome do mapa carregado;
- linha/track ativa, destino e próxima parada quando disponibilizados pelo timetable;
- atualização contínua do dashboard/HUD.

O perfil principal é **OMSI 2.3.004**. O projeto também mantém suporte técnico ao perfil 2.2.032, ainda sujeito a validação real mais ampla.

## GPS, HUD e rota

O cliente já possui:

- catálogo de mapas instalados com `global.cfg`;
- identificação de compatibilidade do mapa;
- detecção de roadmaps globais;
- transformação GridX/GridY + posição local do tile para o roadmap;
- zoom e pan;
- modo **Seguir ônibus**;
- minimapa com orientação de condução;
- jogadores remotos compatíveis no mapa/minimapa;
- suavização dos marcadores remotos;
- chat visual, voz e atalhos dentro do HUD;
- paradas de ônibus desenhadas diretamente a partir do mapa;
- destaque da próxima parada;
- visão geral da rota completa;
- posição persistente do HUD;
- ocultação da sobreposição quando o OMSI abre menus, opções, timetable e outras janelas auxiliares.

O traçado da viagem ativa lê `.ttp/.ttr`, tiles `.map`, splines `.sli` e paths de objetos/crossings `.sco`, mantendo fallback seguro quando uma geometria não pode ser resolvida.

Atalhos padrão:

- `F9` — abrir chat de texto;
- `F10` — segurar para falar no chat por voz.

Os atalhos são configuráveis e o NavBR verifica conflitos com `Inputs/keyboard.cfg`.

## Multiplayer peer-host

Na série **0.3 alpha**, o servidor da sala é o **PC de quem cria a sala**. O próprio cliente inicia um host ASP.NET Core/SignalR local e entra nele automaticamente.

Fluxo básico:

1. O criador clica em **Criar sala neste PC**.
2. O NavBR inicia o host na porta TCP `27730`.
3. O criador compartilha o convite `NAVBR_INVITE_V1`.
4. O convidado cola o convite no NavBR.
5. Telemetria, presença, chat e voz passam pelo PC do host.

Já existem no código:

- criação e entrada em sala;
- nickname/presença;
- telemetria compartilhada;
- jogadores no mapa/minimapa;
- compatibilidade/fingerprint de mapa;
- chat de texto;
- voz push-to-talk;
- host peer-to-host na porta TCP `27730`;
- reconexão SignalR;
- servidor dedicado opcional;
- eleição de autoridade do tráfego;
- snapshots compactos de tráfego do host para os demais jogadores.

O servidor dedicado continua sendo **opcional**. Ele não é necessário para criar uma sala comum pelo cliente NavBR.

## Tráfego sincronizado

A Alpha.11 já possui a infraestrutura de rede para que o host seja a autoridade do tráfego relevante da sala:

- captura de veículos AI do OMSI 2.3.004;
- limite inicial de até 48 veículos por snapshot;
- raio de captura configurado pela implementação;
- posição absoluta e local;
- quaternion;
- velocidade;
- luzes e setas;
- sequência e autoridade do snapshot;
- filtro por mapa/compatibilidade;
- envio para o plugin dos clientes.

A aplicação física desses veículos dentro do OMSI dos outros jogadores ainda está em desenvolvimento. Também será necessário tratar a duplicação entre a IA local de cada OMSI e o tráfego sincronizado pelo host.

## Plugin OMSI experimental

A Alpha.11 aprofunda a investigação do plugin x86 opcional.

Estado atual:

- plugin Native AOT x86;
- `.opl` para carregamento pelo OMSI;
- Windows Named Pipe local restrito ao usuário atual;
- protocolo versionado com handshake e reconexão;
- recebimento de estados de jogadores remotos;
- recebimento e limpeza de snapshots de tráfego;
- filas para executar futuras escritas físicas no callback/thread do próprio OMSI;
- camada auxiliar nativa x86 `NavBR.OmsiInterop.dll` em desenvolvimento para traduzir chamadas para o ABI Borland/Delphi do OMSI 2.3.004;
- validações de arquitetura e exports no CI.

**O backend de criação física continua desativado até que spawn, identificação da instância, atualização e remoção estejam seguros e testados no OMSI real.**

A documentação técnica está em **[docs/OMSI_PLUGIN_EXPERIMENTAL.md](docs/OMSI_PLUGIN_EXPERIMENTAL.md)**.

## Chat e voz

O multiplayer inclui:

- chat de texto por sala;
- mensagens na janela multiplayer e no HUD;
- chat visual rolável;
- voz push-to-talk;
- captura/reprodução pelo NAudio;
- Opus via Concentus em 48 kHz mono;
- indicador visual de quem está falando;
- atalhos configuráveis.

## Próximos testes prioritários

Para a série Alpha.11, os testes prioritários são:

1. HUD visível durante a condução;
2. HUD oculto em menus/opções/timetable e reaparecendo no gameplay;
3. posição, velocidade e heading do ônibus local;
4. paradas e próxima parada em mapas diferentes;
5. rota completa e enquadramento correto;
6. multiplayer entre dois computadores;
7. chat e PTT;
8. troca de mapa durante uma sessão;
9. autoridade e snapshots de tráfego;
10. carregamento do plugin experimental no OMSI 2.3.004;
11. estabilidade do bridge cliente ↔ plugin;
12. ciclo físico seguro de criação, atualização e remoção de veículos remotos antes de habilitá-lo publicamente.

## Idiomas

A base inclui:

- Português (Brasil) — `pt-BR`;
- English — `en-US`;
- Español — `es-ES`;
- Deutsch — `de-DE`;
- Français — `fr-FR`.

## Compatibilidade

O perfil principal suportado tecnicamente é **OMSI 2.3.004 no Windows**.

O cliente não usa Steam API. Ele localiza `Omsi.exe` em execução e deriva o diretório da instalação diretamente do processo.

## Estrutura

```text
src/
  NavBR.Client/                   WPF, telemetria, GPS, HUD, multiplayer, voz e localização
  NavBR.Server/                   host multiplayer ASP.NET Core + SignalR
  NavBR.Shared/                   DTOs e protocolos compartilhados
  NavBR.OmsiPluginExperimental/   plugin x86 opcional

native/
  NavBR.OmsiInterop/              shim nativo x86 experimental para OMSI 2.3.004

docs/
  ALPHA11_DEVELOPMENT.md
  MANUAL_DE_USO.md
  GERAR_ROADMAP_MAPAS.md
  OMSI_PLUGIN_EXPERIMENTAL.md
  ARCHITECTURE.md
  ROADMAP.md
  TELEMETRY.md
  RELEASES.md
```

## Stack

- .NET 10
- C# / WPF x86
- ASP.NET Core / Kestrel
- SignalR / WebSocket
- NAudio
- Concentus / Opus
- Windows `OpenProcess` / `ReadProcessMemory`
- Windows Named Pipes
- Native AOT / DNNE no plugin experimental
- C++/MSVC x86 no interop nativo experimental
- `.resx` + `ResourceManager` para localização

## Builds e Releases

O GitHub Actions gera os pacotes de cliente e servidor. Nas builds públicas da Alpha.11 são usados:

- `OMSI-NavBR-Multiplayer-vX.X.X-win-x86.exe` — **cliente recomendado**, standalone/self-contained;
- `OMSI-NavBR-Multiplayer-vX.X.X-win-x86.zip` — cliente em ZIP;
- `OMSI-NavBR-Server-vX.X.X-win-x64.zip` — servidor dedicado opcional;
- `OMSI-NavBR-Plugin-Experimental-vX.X.X-win-x86.zip` — pacote técnico do plugin experimental quando aplicável;
- `LICENSE` e `THIRD_PARTY_NOTICES.md`.

### Qual arquivo baixar?

Para jogar, entrar em uma sala ou criar uma sala no próprio PC, prefira o **EXE standalone do cliente**.

O ZIP do cliente é apenas uma alternativa ao EXE. O ZIP do servidor é somente para quem deseja executar um servidor dedicado separado.

### Links atuais

- **Alpha.11 Test 1:** https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.11-test.1
- **Alpha.10:** https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.10
- **Todas as releases:** https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases

## Segurança e escopo

A integração normal do cliente continua externa e de leitura. O plugin e o interop nativo são experimentais e opcionais.

O projeto não implementa bypass de DRM, ativação ou patches específicos para executáveis crackeados. OMSI, Steam e demais marcas citadas pertencem aos respectivos titulares; o NavBR é um projeto independente.

## Licença

O código próprio do **OMSI NavBR Multiplayer** é disponibilizado sob licença **MIT**. Veja `LICENSE`.

Dependências de terceiros e seus respectivos textos de licença estão documentados em `THIRD_PARTY_NOTICES.md` e na pasta `licenses/`.