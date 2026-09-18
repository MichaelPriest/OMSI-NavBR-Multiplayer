# OMSI NavBR Multiplayer

Companion app independente para **OMSI 2**, com navegação/HUD, telemetria, multiplayer peer-host, chat/voz, CCO, perfil do motorista, Hardware Cockpit, Ghost/Replay, integração experimental com veículos remotos físicos e **modo Personagem/RP**.

## Versão pública atual

A próxima publicação pública é **v0.3.0-alpha.14**.

- cliente principal: **EXE standalone Windows x86**;
- ZIP do cliente;
- servidor dedicado Windows x64;
- plugin OMSI Native AOT x86;
- simulador multiplayer de desenvolvimento/teste;
- documentação e SHA256SUMS.

> A Alpha.14 continua sendo uma **prerelease pública**. Recursos de escrita física no OMSI permanecem experimentais e opt-in.

## Destaques da Alpha.14

- **React + TypeScript + Vite em WebView2 como interface principal**, com host .NET/WPF x86 e fallback WPF seguro;
- Home com **Executar OMSI**, Navegação/GPS 2D/3D, Multiplayer, CCO, Empresa/Frota, Perfil, Ghost/Replay, Hardware Cockpit, Instalações OMSI, HUD, Roadmap Studio, Diagnóstico e ferramentas;
- **HUD configurável no React** com preset, tema, ancoragem, escala, opacidade e módulos; **Mover HUD** continua sobre o overlay nativo;
- selects/ComboBox com tema escuro consistente;
- Central Multiplayer sem o wizard legado sobreposto;
- abas: Visão geral, Sala, Jogadores, Chat & Voz, Personagem/RP e Avançado, com dispositivos de áudio e mixer por jogador no próprio React;
- salas públicas/privadas, senha, convite, peer-host TCP 27730, UPnP e relay experimental;
- tela **Rede** com verificação real do Firewall TCP 27730, listener local, NAT/CGNAT, UPnP e teste externo quando configurado;
- Plugin Bridge **v3** + interop RP v3;
- modo Personagem/RP disponível também sem multiplayer, com catálogo real de `Map.Drivers`, ativação, seleção e retorno ao ônibus pelo React;
- **Ghost / Replay no React** com gravação real a 10 Hz, biblioteca/importação, analytics, prévia read-only da rota e replay 3D experimental pelo Plugin Bridge;
- ônibus remoto físico experimental;
- simulador multiplayer com bots no **mesmo mapa**, **próximos do host** e herdando **linha/rota/destino/próxima parada** da operação ativa da sala;
- interface pt-BR, English, Español, Deutsch e Français.

## Executar OMSI pelo NavBR

Na Home há um atalho **Executar OMSI**. O NavBR usa a instalação real detectada/cadastrada em **Instalações OMSI** e prioriza o perfil preferido. A própria tela React também permite selecionar uma pasta real pelo Windows, abrir a instalação no Explorer, editar o perfil, definir o preferido e iniciar o OMSI.

Se nenhuma instalação válida for encontrada, o app abre a seleção de instalações em vez de usar um caminho fixo ou depender da Steam.

## Multiplayer

O computador de quem cria a sala pode funcionar como servidor da própria sessão.

- porta padrão: TCP 27730;
- telemetria, presença, chat e voz passam pelo SignalR;
- salas privadas não aparecem no navegador público;
- UPnP é opcional;
- servidor dedicado continua disponível;
- relay/fallback permanece experimental;
- o host direto pode exigir Firewall/port forwarding dependendo da rede.

## Simulador Multiplayer

O simulador é somente para desenvolvimento/teste e não injeta dados fake na interface de produção.

Quando executado contra uma sala real:

1. detecta o mapa da autoridade/jogador real;
2. herda MapName e compatibilidade;
3. espera telemetria real para usar a posição do host como centro;
4. posiciona os bots em um raio curto, por padrão **18 m**;
5. herda a operação ativa da sala: **linha, rota, destino e próxima parada**;
6. rejeita no modo --verify bots que publiquem em mapa diferente.

Se 127.0.0.1:27730 estiver vazio, o pacote do simulador pode iniciar automaticamente o NavBR.Server incluído.

Veja [docs/MULTIPLAYER_SIMULATOR.md](docs/MULTIPLAYER_SIMULATOR.md).

## Modo Personagem / RP

O modo RP é experimental.

- usa personagens reais da lista Drivers do mapa;
- controle inicial: W/S, A/D, Shift e Esc;
- bridge/plugin v3;
- restauração de vínculo/IA ao voltar ao ônibus;
- sincronização RP separada no multiplayer.

Ainda exigem validação física mais ampla: câmera dedicada, terreno inclinado, animações/gestos, interação com objetos/veículos e personagem remoto físico completo.

## Ghost / Replay

A Alpha.14 também leva o fluxo principal de Ghost para a interface React.

- grava telemetria local real a cada 100 ms;
- salva arquivos `.navbrghost` usando o `GhostRecorder` existente;
- permite abrir e importar replays validados;
- mostra biblioteca local e analytics calculados pelo C#;
- desenha uma prévia read-only do trajeto usando coordenadas X/Z reais dos frames;
- reprodução Ghost 3D continua experimental e usa o `GhostReplayPlayer` + Plugin Bridge para spawn/update/despawn;
- quando o bridge recusa escrita física, o NavBR falha de forma segura.

## Requisitos principais

- OMSI alvo inicial: **2.3.004**;
- cliente: **.NET 10 / C# / WPF x86 host + WebView2 + React/TypeScript/Vite**;
- servidor: **ASP.NET Core + SignalR**;
- host da sala: o próprio PC de quem cria a sala;
- porta padrão: **TCP 27730**;
- projeto público.

## Documentação

- [docs/ALPHA14_RELEASE_NOTES.md](docs/ALPHA14_RELEASE_NOTES.md) — notas da Alpha.14 pública;
- [docs/ALPHA14_COMMUNITY.md](docs/ALPHA14_COMMUNITY.md) — roteiro de teste;
- [docs/ALPHA14_MASTER_SCOPE.md](docs/ALPHA14_MASTER_SCOPE.md) — escopo consolidado;
- [docs/MULTIPLAYER_SIMULATOR.md](docs/MULTIPLAYER_SIMULATOR.md) — simulador;
- [docs/NETWORKING.md](docs/NETWORKING.md) — rede/Firewall/UPnP/relay;
- [docs/PEER_HOST.md](docs/PEER_HOST.md) — host local;
- [docs/OMSI_PLUGIN_EXPERIMENTAL.md](docs/OMSI_PLUGIN_EXPERIMENTAL.md) — plugin v3;
- [docs/HARDWARE_COCKPIT.md](docs/HARDWARE_COCKPIT.md) — Hardware Cockpit;
- [docs/MANUAL_DE_USO.md](docs/MANUAL_DE_USO.md) — manual.

## Segurança

A telemetria externa do OMSI permanece **read-only**. Escritas experimentais ficam isoladas no plugin/bridge, exigem ativação explícita e falham de forma segura quando a capacidade não está disponível.

O projeto não redistribui mapas, ônibus, HOFs ou outros conteúdos proprietários/pagos do OMSI.

## Portal

O GitHub Pages concentra downloads, releases, documentação e estado dos testes públicos.

## Licença

Consulte [LICENSE](LICENSE) e [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Interface principal React/WebView2

A Alpha.14 usa **React + TypeScript + Vite em WebView2 como shell desktop principal**. O processo continua sendo o cliente .NET/WPF x86: C# permanece responsável por telemetria, OMSI/plugin, SignalR, host TCP 27730, pipeline de voz/Opus, Firewall/NAT/UPnP, Hardware Cockpit, arquivos do OMSI, renderização/interação do HUD, arquivos do mapa/roadmap, geração de roadmaps, gravação/arquivos e playback físico de Ghosts e runtime físico do RP.

O shell WPF anterior permanece apenas como fallback técnico para comparação, diagnóstico e áreas ainda não migradas. Ele só é ocultado depois que o WebView2 confirma o carregamento da interface; se o WebView2 falhar, o WPF continua disponível. O ícone da bandeja também reabre a interface React principal. Nos fluxos normais, Instalações OMSI, configuração do HUD e Roadmap Studio permanecem no React; **Mover HUD** continua nativo por depender da interação direta com o overlay do OMSI.

Superfícies já migradas para React:

- Home operacional e Executar OMSI;
- Navegação/GPS com geometria real da rota, paradas, manobras e ETA, incluindo visão 3D React com roadmap real e ônibus remotos compatíveis;
- Central Multiplayer, salas públicas/privadas, jogadores, chat, voz, seleção de microfone/saída, mixer por jogador e RP;
- CCO, ocorrências, Empresa/Frota e Perfil;
- Personagem/RP com personagens reais de `Map.Drivers`, estado do Plugin Bridge e comandos Sair/Retornar ao ônibus;
- Ghost/Replay com gravação de telemetria real, biblioteca local, importação validada, analytics, prévia de rota e reprodução 3D experimental;
- Hardware Cockpit com uma única conexão serial compartilhada;
- Instalações OMSI e perfis de lançamento, incluindo seletor nativo de pasta e abertura no Explorer;
- HUD com presets/tema/ancoragem, escala, dimensões, opacidade, módulos e escalas individuais, aplicados ao vivo pelo store nativo;
- Roadmap Studio com análise de tiles, geração por imagens e geração vetorial pelas splines usando os serviços C# existentes;
- Diagnóstico e privacidade;
- Rede com Firewall TCP 27730 verificado, NAT/CGNAT, UPnP e teste externo quando configurado.

Nenhuma tela de produção inventa telemetria quando o estado real não está disponível.

Detalhes técnicos: [docs/WEB_UI_ARCHITECTURE.md](docs/WEB_UI_ARCHITECTURE.md).
