# OMSI NavBR Multiplayer

Companion app independente para **OMSI 2**, com navegação/HUD, telemetria, multiplayer peer-host, chat/voz, CCO, perfil do motorista, Hardware Cockpit, Ghost/Replay, integração experimental com veículos remotos físicos e **modo Personagem/RP**.

## Versão pública atual

A versão pública e atual é **v0.3.0-alpha.19**.

- cliente principal: **instalador/EXE Windows x86**;
- **APK Android NavBR Mobile Companion Alpha 2**;
- ZIP do cliente;
- servidor dedicado Windows x64;
- plugin OMSI Native AOT x86;
- simulador multiplayer de desenvolvimento/teste;
- documentação e SHA256SUMS.

> **AVISO DE TESTE DA ALPHA.19:** esta versão foi liberada publicamente para ampliar a validação. O multiplayer **LAN/local** e **online/Servidor NavBR/Host pela Internet ainda não foram validados ponta a ponta entre dois PCs/duas sessões reais do OMSI**. Sala conectada, telemetria ou confirmação do simulador não devem ser interpretadas como validação completa do multiplayer físico. Use como alpha pública de teste e reporte logs/resultados.

## Destaques da Alpha.19

- **NavBR Mobile Companion Alpha 2 para Android** incluído na release pública;
- descoberta automática do PC NavBR na mesma LAN, com fallback manual;
- GPS, telemetria do ônibus, multiplayer, voz/PTT e estado IBIS reais no celular;
- painel IBIS estilo cockpit com visor/teclado e mapeamento somente para `[mouseevent]` reais do veículo;
- controles locais do ônibus pelo celular permanecem **EXPERIMENTAIS**, desligados por padrão e revalidados no desktop;

- verificador de plugin compara individualmente `NavBR.OmsiPlugin.dll`, `NavBR.OmsiInterop.dll` e `NavBR.OmsiPlugin.opl` por SHA-256 contra o bundle embutido;
- atualização automática do plugin quando ausente/desatualizado e atualização adiada automaticamente enquanto o OMSI estiver aberto;
- diagnóstico de spawn físico reforçado para impedir que bots/simulador reutilizem o ponteiro do ônibus local;
- confirmação de materialização física exige ponteiro novo, RoadVehicle válido, presença contínua e diagnóstico de render do OMSI;
- pacote de ícones autoral NavBR integrado à interface;
- instalador Windows com desinstalador e simulador incluído;

- **React + TypeScript + Vite em WebView2 como única interface desktop acessível ao usuário**, com .NET/WPF x86 preservado apenas como host técnico invisível dos serviços nativos;
- Home com **Executar OMSI**, Navegação/GPS 2D/3D, Multiplayer, CCO, Empresa/Frota, Perfil, Ghost/Replay, Hardware Cockpit, Instalações OMSI, HUD, Roadmap Studio, Diagnóstico e ferramentas;
- **HUD configurável no React** com preset, tema, ancoragem, escala, opacidade e módulos; **Mover HUD** continua sobre o overlay nativo;
- selects/ComboBox com tema escuro consistente;
- Central Multiplayer sem o wizard legado sobreposto;
- abas: Visão geral, Sala, Jogadores, Chat & Voz, Personagem/RP e Avançado, com dispositivos de áudio e mixer por jogador no próprio React;
- salas públicas/privadas, senha e convite com três modos: Servidor NavBR oficial, LAN e Online através do Host;
- tela **Rede** com verificação real do Firewall TCP 27730, listener local, NAT/CGNAT, UPnP e teste externo quando configurado;
- Plugin Bridge **v3** + **state interop ABI v7**, com resolução local de Kachel por `GridX/GridY`;
- modo Personagem/RP disponível também sem multiplayer, com catálogo real de `Map.Drivers`, ativação, seleção e retorno ao ônibus pelo React;
- **Ghost / Replay no React** com gravação real a 10 Hz, biblioteca/importação, analytics, prévia read-only da rota e replay 3D experimental pelo Plugin Bridge;
- ônibus remoto físico experimental com interpolação, culling por proximidade, diagnóstico detalhado e **resolução da Kachel local por GridX/GridY**, sem reutilizar índices de tile de outro processo OMSI;
- simulador multiplayer com bots no **mesmo mapa**, **próximos do host**, ônibus rígido padrão EN92 quando disponível e rotas independentes do HOF para teste físico;
- modo `--verify-physical` só aprova quando o host confirma que os IDs exatos dos bots foram materializados por `MakeVehicle` no OMSI;
- Navegação 2D/3D usa o roadmap real catalogado e calcula caminho de retorno à rota pelas splines quando o veículo está fora da rota;
- RP prioriza o motorista humano ativo real do ônibus e só encerra a posse após confirmar a restauração do motorista ao veículo;
- interface pt-BR, English, Español, Deutsch e Français.

## Executar OMSI pelo NavBR

Na Home há um atalho **Executar OMSI**. O NavBR usa a instalação real detectada/cadastrada em **Instalações OMSI** e prioriza o perfil preferido. A própria tela React também permite selecionar uma pasta real pelo Windows, abrir a instalação no Explorer, editar o perfil, definir o preferido e iniciar o OMSI.

Se nenhuma instalação válida for encontrada, o app abre a seleção de instalações em vez de usar um caminho fixo ou depender da Steam.

## Multiplayer

O NavBR suporta **três modos de multiplayer**:

### 1. Servidor NavBR oficial

O `NavBR.Server` roda no servidor oficial hospedado no Render e recebe as salas via HTTPS/SignalR.

- jogadores não precisam abrir TCP 27730;
- não depende de UPnP nem do IP público do jogador;
- CGNAT do jogador não impede a conexão;
- quem cria a sala é o proprietário administrativo da sala, mas o servidor continua sendo o NavBR no Render;
- a infraestrutura atual é **gratuita e limitada**, adequada principalmente para Alpha/testes.

O Render Free atual fornece 0,1 CPU, 512 MB de RAM, 750 horas gratuitas por mês e uma única instância, com possibilidade de spin-down após 15 minutos sem tráfego. No futuro o projeto poderá oferecer uma **assinatura oficial** com maior capacidade e estabilidade; ainda não há plano, preço ou data definidos.

### 2. LAN

O PC de quem cria a sala executa o `NavBR.Server` apenas para a rede local.

- porta padrão: TCP 27730;
- não usa o Servidor NavBR oficial;
- indicado para jogadores na mesma LAN.

### 3. Online através do Host

O PC de quem cria a sala executa o `NavBR.Server` e recebe os demais jogadores pela Internet.

- não usa o Servidor NavBR oficial;
- pode exigir Firewall, UPnP ou redirecionamento da TCP 27730 dependendo da rede;
- o servidor local sobe primeiro e a tentativa de UPnP acontece em segundo plano;
- a descoberta/mapeamento UPnP tem timeout de 8 segundos e não bloqueia mais a interface;
- se o UPnP falhar ou expirar, a sala continua ativa em LAN e o app mostra o estado real;
- a capacidade depende do PC e da conexão de Internet do host.

### Render gratuito

[![Deploy to Render](https://render.com/images/deploy-to-render-button.svg)](https://render.com/deploy?repo=https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer)

O repositório inclui `render.yaml` e `Dockerfile.render` para criar um Web Service gratuito. Depois do deploy, copie a URL `https://<servico>.onrender.com` para **Multiplayer → Sala → Usar servidor online**.

Veja [docs/RENDER_HOSTING.md](docs/RENDER_HOSTING.md).

## Simulador Multiplayer

O simulador é somente para desenvolvimento/teste e não injeta dados fake na interface de produção.

Quando executado contra uma sala real:

1. detecta o mapa da autoridade/jogador real;
2. herda MapName e compatibilidade;
3. espera telemetria real para usar a posição do host como centro;
4. posiciona os bots em um raio curto, por padrão **18 m**;
5. mantém os bots próximos do host e pode usar rotas diferentes do HOF;
6. usa o MAN EN92 padrão do OMSI como primeira opção de ônibus rígido para teste físico, quando instalado;
7. `--verify` valida rede/movimento e `--verify-physical` exige confirmação real de `MakeVehicle` no OMSI.

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

A Alpha.19 mantém o fluxo principal de Ghost na interface React.

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
- hospedagem multiplayer: **Servidor NavBR oficial**, **LAN** ou **Online através do Host**;
- peer-host local: **TCP 27730**;
- servidor online: **HTTPS/WSS** pelo provedor de hospedagem;
- projeto público.

## Documentação

- [docs/ALPHA19_RELEASE_NOTES.md](docs/ALPHA19_RELEASE_NOTES.md) — notas e limitações públicas da Alpha.19;
- [docs/ALPHA18_RELEASE_NOTES.md](docs/ALPHA18_RELEASE_NOTES.md) — histórico da Alpha.18;
- [docs/ALPHA18_COMMUNITY.md](docs/ALPHA18_COMMUNITY.md) — roteiro de validação pública da Alpha.18;
- [docs/ALPHA15_RELEASE_NOTES.md](docs/ALPHA15_RELEASE_NOTES.md) — histórico da Alpha.15;
- [docs/ALPHA15_MASTER_SCOPE.md](docs/ALPHA15_MASTER_SCOPE.md) — escopo consolidado da Alpha.15;
- [docs/ALPHA14_RELEASE_NOTES.md](docs/ALPHA14_RELEASE_NOTES.md) — histórico da Alpha.14;
- [docs/MULTIPLAYER_SIMULATOR.md](docs/MULTIPLAYER_SIMULATOR.md) — simulador;
- [docs/NETWORKING.md](docs/NETWORKING.md) — rede/Firewall/UPnP/servidor online;
- [docs/PEER_HOST.md](docs/PEER_HOST.md) — host local;
- [docs/RENDER_HOSTING.md](docs/RENDER_HOSTING.md) — servidor online gratuito no Render;
- [docs/OMSI_PLUGIN_EXPERIMENTAL.md](docs/OMSI_PLUGIN_EXPERIMENTAL.md) — plugin v3;
- [docs/HARDWARE_COCKPIT.md](docs/HARDWARE_COCKPIT.md) — Hardware Cockpit;
- [docs/MOBILE_COMPANION.md](docs/MOBILE_COMPANION.md) — roadmap futuro do Companion para smartphone (fora do escopo atual);
- [docs/MANUAL_DE_USO.md](docs/MANUAL_DE_USO.md) — manual.

## Segurança

A telemetria externa do OMSI permanece **read-only**. Escritas experimentais ficam isoladas no plugin/bridge, exigem ativação explícita e falham de forma segura quando a capacidade não está disponível.

O projeto não redistribui mapas, ônibus, HOFs ou outros conteúdos proprietários/pagos do OMSI.

## Portal

O GitHub Pages concentra downloads, releases, documentação e estado dos testes públicos.

## Licença

Consulte [LICENSE](LICENSE) e [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Interface principal React/WebView2

A Alpha.19 usa **React + TypeScript + Vite em WebView2 como shell desktop principal**. O processo continua sendo o cliente .NET/WPF x86: C# permanece responsável por telemetria, OMSI/plugin, SignalR, host TCP 27730, pipeline de voz/Opus, Firewall/NAT/UPnP, Hardware Cockpit, arquivos do OMSI, renderização/interação do HUD, arquivos do mapa/roadmap, geração de roadmaps, gravação/arquivos e playback físico de Ghosts e runtime físico do RP.

O shell WPF anterior não é mais uma superfície acessível ao usuário. O `MainWindow` continua compilado temporariamente apenas como **host técnico em memória** enquanto serviços nativos ainda são desacoplados de sua classe. O app não usa mais `StartupUri="MainWindow.xaml"` e não chama mais `Show()` no host; telemetria, estatísticas, RP e tray são inicializados explicitamente e os antigos installers/renderizadores visuais da Alpha.11/12 não são executados. Fechar o React mantém o NavBR na bandeja em vez de reabrir o layout antigo. Falhas de carregamento do WebView2 são apresentadas no painel de erro da própria janela React/WebView2. O ícone da bandeja sempre reabre a interface React. **Mover HUD** continua nativo por depender da interação direta com o overlay do OMSI.

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
