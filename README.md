# OMSI NavBR Multiplayer

[![Downloads](https://img.shields.io/github/downloads/MichaelPriest/OMSI-NavBR-Multiplayer/total?label=downloads&color=22c77a)](https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases)

Aplicativo de navegação e multiplayer para **OMSI 2**, independente da Steam.

> Versão em desenvolvimento: **0.3.0-alpha.9**  
> Última release publicada: **v0.3.0-alpha.9**

Site oficial: **https://michaelpriest.github.io/OMSI-NavBR-Multiplayer/**

Manual de uso: **[docs/MANUAL_DE_USO.md](docs/MANUAL_DE_USO.md)**  
Roadmap atualizado: **[docs/ROADMAP.md](docs/ROADMAP.md)**

## Objetivo

O OMSI NavBR Multiplayer é um aplicativo Windows externo ao jogo, projetado para:

- detectar automaticamente uma instalação em execução via `Omsi.exe`;
- descobrir a pasta real do OMSI a partir do processo, sem depender de Steam/Steamworks;
- ler telemetria do ônibus local em tempo real;
- carregar mapas, roadmaps e `TTData` diretamente da instalação do OMSI;
- oferecer GPS/Route Advisor e HUD sobre o jogo;
- conectar jogadores a salas multiplayer hospedadas pelo próprio criador da sala;
- mostrar outros jogadores no mapa e minimapa;
- oferecer chat de texto e chat por voz;
- futuramente experimentar sincronização de veículos remotos dentro do OMSI;
- oferecer interface multilíngue com troca de idioma em tempo real.

## Estado atual — alpha.9

A **v0.3.0-alpha.9** está publicada como prerelease com EXE standalone, ZIP do cliente e ZIP do servidor dedicado opcional.

O CI valida compilação e publicação dos artefatos. Recursos que dependem do comportamento do OMSI, rede entre computadores, microfone ou geometria específica dos mapas continuam marcados como **implementados, aguardando validação real** até serem testados no simulador.

### Telemetria local

Já implementado para os perfis suportados do OMSI:

- detecção do processo `Omsi.exe`;
- caminho real da instalação;
- fingerprint SHA-256 do executável;
- acesso externo somente leitura com `OpenProcess` / `ReadProcessMemory`;
- posição X/Y/Z;
- direção/heading;
- velocidade;
- nome do mapa carregado, com fallback pelo `logfile.txt`;
- linha/track ativa, destino e próxima parada quando disponibilizados pelo timetable;
- atualização contínua do dashboard/HUD.

A integração continua somente leitura: o NavBR não injeta código nem grava na memória do OMSI.

Ainda precisam de validação real mais ampla:

- escala/sinal da posição em mapas diferentes;
- velocidade contra o velocímetro do OMSI;
- zero/sentido do heading;
- perfil 2.2.032;
- allowlist de hashes conhecidos para builds testadas.

### GPS, HUD e rota

O cliente já:

- encontra automaticamente a pasta `maps` da instalação detectada;
- cataloga mapas que possuem `global.cfg`;
- calcula um identificador de compatibilidade do mapa;
- detecta `whole.roadmap.bmp`, `roadmap.bmp` e variantes;
- transforma GridX/GridY + posição local do tile em pixels do roadmap;
- possui suporte técnico a mapas com `[worldcoordinates]`;
- desenha o marcador do ônibus no roadmap;
- oferece zoom, pan, modo **Seguir ônibus** e comando **Ajustar**;
- gira o marcador conforme o heading recebido;
- possui HUD móvel, com posição persistente;
- oferece zoom do minimapa de até **10×**;
- mostra outros jogadores compatíveis no mapa/minimapa;
- suaviza marcadores remotos;
- mostra chat visual, contador de jogadores, indicador de voz e atalhos dentro do HUD;
- acompanha a janela de gameplay do OMSI e tenta ocultar a sobreposição em menus e diálogos auxiliares;
- devolve o foco ao OMSI depois que o jogador fecha o campo de chat.

Na **alpha.9**, o traçado da viagem ativa lê o `.ttp/.ttr`, resolve o índice de tile pela ordem dos blocos `[map]` do `global.cfg` e tenta reconstruir a geometria usando `ObjectId`, `PathId`, tiles `.map`, splines `.sli` e paths de objetos/crossings `.sco`. Quando um trecho não pode ser decodificado com segurança, o NavBR mantém fallback por tiles.

O traçado detalhado, destino, próxima parada e comportamento completo do HUD ainda precisam de validação visual no OMSI real antes de serem considerados estáveis.

O HUD segue uma organização inspirada em jogos de mundo aberto, com identidade visual própria do NavBR. Ele não copia assets ou interface proprietária de GTA/Rockstar.

Atalhos padrão atuais no HUD:

- `F9` — abrir chat de texto;
- `F10` — segurar para falar no chat por voz.

Os atalhos são configuráveis na janela multiplayer. Para evitar comandos conhecidos do OMSI, o NavBR não oferece F5, F6, F7 ou F8. As opções são combinações baseadas em `F9` e `F10`, com ou sem `Shift` e/ou `Ctrl`. Chat e push-to-talk precisam usar combinações diferentes.

Como o OMSI permite ao usuário e a add-ons alterar comandos, o NavBR lê `Inputs/keyboard.cfg` da instalação detectada e compara **scan code + modificadores** da combinação escolhida. Se a combinação já estiver atribuída no OMSI, o NavBR não ativa aquele atalho e mostra um aviso no HUD. Se o `keyboard.cfg` não puder ser verificado, os atalhos ficam desativados por segurança.

A sobreposição é voltada inicialmente a OMSI em modo janela ou janela sem bordas. Overlay em fullscreen exclusivo ainda precisa de validação real.

### Multiplayer peer-host

Na série **0.3 alpha**, o servidor da sala é o **PC de quem cria a sala**. O próprio cliente NavBR inicia um host ASP.NET Core/SignalR local e entra nele automaticamente.

Fluxo básico:

1. O criador clica em **Criar sala neste PC**.
2. O NavBR inicia o host na porta TCP `27730`.
3. O criador copia o convite versionado `NAVBR_INVITE_V1`.
4. O convidado cola o convite no NavBR.
5. Telemetria, presença, chat e voz passam pelo PC do host.

Já existem no código:

- criação e entrada em sala;
- nickname/presença;
- telemetria compartilhada;
- jogadores no mapa/minimapa;
- suavização/interpolação visual dos jogadores remotos;
- compatibilidade/fingerprint de mapa;
- chat de texto;
- voz push-to-talk;
- convite versionado;
- host peer-to-peer na porta TCP `27730`;
- reconexão automática SignalR com reentrada na sala;
- servidor dedicado opcional.

Em rede local, o NavBR mostra automaticamente os endereços IPv4 disponíveis. Para jogadores fora da mesma rede, o host pode precisar liberar o NavBR no Windows Firewall e encaminhar a porta TCP `27730` no roteador.

Ainda precisam ser desenvolvidos/refinados depois dos testes reais:

- diagnóstico de conectividade/porta mais completo;
- rate limiting;
- códigos de erro de rede mais estruturados e independentes de idioma;
- UPnP/NAT traversal para reduzir configuração manual de porta;
- melhorias de voz em redes com perda/latência elevada.

O pacote `OMSI-NavBR-Server` continua disponível somente para quem quiser executar um **host dedicado separado**. Ele não é necessário para criar uma sala comum pelo cliente NavBR.

### Chat e voz

O multiplayer inclui:

- chat de texto por sala, limitado a 280 caracteres por mensagem;
- mensagens visíveis na janela multiplayer e no HUD;
- chat visual rolável no HUD;
- voz push-to-talk por sala;
- captura e reprodução de áudio pelo NAudio;
- codificação Opus via Concentus em 48 kHz mono, quadros de 20 ms;
- indicador visual de quem está falando;
- atalhos configuráveis e protegidos contra conflitos com o `keyboard.cfg` real do OMSI.

Nesta alpha a voz é transportada pelo mesmo canal SignalR/WebSocket da sessão. Isso simplifica o peer-host inicial, mas pode ter mais latência sob perda de rede do que um transporte UDP/WebRTC.

Voz, latência, atalhos, reconexão e peer-host ainda exigem validação real entre computadores antes de serem considerados estáveis.

## Próximos testes prioritários

Antes de continuar o desenvolvimento de novas funções, a alpha.9 deve ser validada em OMSI real, priorizando:

1. HUD visível durante a condução;
2. HUD oculto em menus/opções/timetable e reaparecendo ao retornar ao gameplay;
3. marcador, posição, velocidade e heading;
4. traçado detalhado da linha em mapas reais;
5. destino e próxima parada;
6. multiplayer entre dois computadores em LAN;
7. multiplayer pela Internet com firewall/NAT configurados;
8. chat, PTT e áudio entre dois computadores;
9. reconexão/reentrada na sala após queda temporária;
10. conflitos dos atalhos com `keyboard.cfg`;
11. perfil OMSI 2.2.032 quando houver ambiente para teste.

Depois desses testes, o desenvolvimento segue com as correções encontradas e com os itens pendentes descritos em `docs/ROADMAP.md`.

## Idiomas

A base inclui:

- Português (Brasil) — `pt-BR`;
- English — `en-US`;
- Español — `es-ES`;
- Deutsch — `de-DE`;
- Français — `fr-FR`.

Na primeira execução, o NavBR tenta acompanhar o idioma do Windows. Se o idioma do sistema ainda não for suportado, usa inglês. A preferência fica salva em `%LOCALAPPDATA%\OMSI NavBR Multiplayer\language.txt`.

Configurações do multiplayer/HUD, incluindo atalhos escolhidos, posição do HUD e zoom, ficam na configuração local do NavBR.

## Compatibilidade

O perfil principal **suportado tecnicamente** é **OMSI 2.3.004 no Windows**. A alpha.7 também introduziu suporte técnico ao perfil **2.2.032 (tram patch)**, porém esse perfil ainda precisa de validação real mais ampla.

O cliente não usa Steam API: ele localiza `Omsi.exe` em execução e deriva o diretório da instalação diretamente do processo. Quando o metadado de versão do executável diverge da versão carregada, o NavBR pode usar o `logfile.txt` do OMSI como referência de runtime.

## Estrutura

```text
src/
  NavBR.Client/   WPF, telemetria, GPS, HUD, multiplayer, voz e localização
  NavBR.Server/   host multiplayer em ASP.NET Core + SignalR
  NavBR.Shared/   DTOs e protocolo compartilhado

docs/
  MANUAL_DE_USO.md
  ARCHITECTURE.md
  ROADMAP.md
  TELEMETRY.md
  RELEASES.md
licenses/
  licenças das dependências redistribuídas
```

## Stack

- .NET 10
- C# / WPF
- ASP.NET Core / Kestrel
- SignalR / WebSocket
- NAudio
- Concentus / Opus
- Windows `OpenProcess` / `ReadProcessMemory`
- leitura direta de `global.cfg`, tiles `.map`, splines `.sli`, objetos `.sco`, roadmaps, `TTData` e `Inputs/keyboard.cfg`
- `.resx` + `ResourceManager` para localização

## Builds e Releases

O GitHub Actions compila cliente e servidor automaticamente. Cada nova versão gera um **GitHub Prerelease** com:

- `OMSI-NavBR-Multiplayer-vX.X.X-win-x86.exe` — **cliente recomendado**, standalone/self-contained;
- `OMSI-NavBR-Multiplayer-vX.X.X-win-x86.zip` — **cliente em ZIP**, alternativa ao EXE standalone;
- `OMSI-NavBR-Server-vX.X.X-win-x64.zip` — **servidor dedicado opcional**;
- `LICENSE` e `THIRD_PARTY_NOTICES.md` para os avisos legais do projeto e dependências.

### Qual arquivo baixar?

Para **jogar, entrar em uma sala ou criar uma sala no próprio PC**, baixe somente o **EXE standalone do cliente**. O próprio NavBR inicia o host da sala; **não é necessário baixar o servidor dedicado**.

O **ZIP do cliente** é apenas uma forma alternativa de distribuir o mesmo cliente. Não é necessário baixar EXE e ZIP juntos.

O **ZIP do servidor** é somente para quem quiser rodar um **servidor dedicado separado**, em outro computador ou processo. Nesse modo, extraia o ZIP e mantenha todos os arquivos do pacote juntos; o servidor dedicado atual não é publicado como EXE único.

O `.exe` standalone inclui o runtime necessário e recebe o ícone oficial do NavBR como recurso Win32.

O site oficial usa o `download_count` público dos assets de release do GitHub para mostrar o total de downloads dos pacotes `.exe` e `.zip`, por versão e por arquivo. O catálogo do GitHub Pages é atualizado após releases e periodicamente.

## Segurança e escopo

O projeto não implementa bypass de DRM, ativação ou patches específicos para executáveis crackeados. A integração trabalha com um processo `Omsi.exe` já existente e compatível no computador do usuário.

No multiplayer são transmitidos dados do jogo/sessão, mensagens de chat e, quando ativado, áudio do microfone durante o push-to-talk. O NavBR não usa a localização física do usuário para posicionar jogadores; os endereços de rede são necessários somente para conectar ao PC que hospeda a sala.

## Licença

O código próprio do **OMSI NavBR Multiplayer** é disponibilizado sob licença **MIT**. Veja `LICENSE`.

Dependências de terceiros e seus respectivos textos de licença estão documentados em `THIRD_PARTY_NOTICES.md` e na pasta `licenses/`. OMSI, Steam, GTA/Rockstar e demais marcas citadas pertencem aos respectivos titulares; o NavBR é um projeto independente.
