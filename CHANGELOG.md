# Changelog

Todas as mudanças relevantes do OMSI NavBR Multiplayer serão registradas aqui.

## [0.3.0-alpha.20] — reorganização da interface, HUD por workflow e minimapa HD/Ultra

### Adicionado

- seleção de HUD em categorias por finalidade;
- workspace do HUD com seções **Escolher HUD**, **Aparência**, **Módulos** e **Posição & ações**;
- prévia não persistente do HUD no app quando o OMSI está fechado e no overlay real quando está aberto;
- Roadmap Studio com comparação visual do roadmap OMSI e da textura NavBR HD;
- geração de minimapa NavBR em **HD 2× (440 px/tile)** e **Ultra 3× (660 px/tile)**, limitada a 8192 px;
- hot-reload do roadmap gerado para HUD e navegação 2D/3D;
- atalhos da Home agrupados por Viagem, Online & RP e Ferramentas.

### Melhorado

- barra lateral reorganizada em grupos funcionais;
- idioma movido para **Configurações → Geral**;
- Configurações divididas por Interface, OMSI & Mapas, Conectividade e Sistema;
- Multiplayer dividido em Sessão, Comunicação & RP e Sistema;
- preferências reais da antiga aba Avançado foram consolidadas em Geral e a aba duplicada foi removida;
- HUDs compostos preservam dados reais do C#/OMSI e continuam sem mocks de produção.

### Mantido como alpha pública

- multiplayer LAN/online ainda requer validação real mais ampla entre dois PCs/duas sessões OMSI;
- ônibus remoto físico, RP físico, controles locais e integrações de IBIS continuam experimentais e opt-in quando aplicável.

## [0.3.0-alpha.15] — Kachel local, ABI v7, RP seguro e consolidação do Portal V2

### Corrigido

- ônibus remoto físico deixa de reutilizar o `MapTileIndex` de outro processo OMSI; cada cliente resolve a Kachel local pelo par estável `GridX/GridY`;
- `Tacho` do veículo físico remoto passa a ser escrito em km/h e `Groundspeed` permanece em m/s;
- retorno do Personagem/RP ao ônibus só é considerado concluído após confirmação do estado nativo do motorista;
- cliente repete de forma limitada a restauração do motorista quando o OMSI rejeita uma escrita transitória;
- updater do plugin diferencia bundle antigo/atual por SHA-256, evitando considerar uma DLL antiga como atual;
- seleção de `Omsi.exe`, `.lnk` e `.url` passa a vincular corretamente a instalação preferida;
- roadmap da navegação prefere cache PNG derivado do roadmap real e apresenta diagnóstico explícito quando a imagem não renderiza.

### Adicionado

- state interop **ABI v7** e export `NavBR_ResolveMapTileIndex`;
- validação do par `GridX/GridY` no servidor;
- mensagens detalhadas do backend físico expostas no React;
- diagnóstico dos retornos nativos de `MakeVehicle` e da cópia da lista temporária;
- Portal V2 profissional com download atual, arquivo completo de versões, busca/filtro, roadmap, documentação, Pix e publicidade preservados;
- branch estática `gh-pages` para publicação do portal sem depender do workflow antigo do GitHub Pages.

### Mantido como experimental

- ônibus remoto físico dentro do OMSI;
- Personagem/RP físico;
- replay Ghost 3D;
- escrita nativa no OMSI via Plugin Bridge.

### Validação

- build Windows x86, Native AOT x86, exports, instalação/remoção do plugin, bundle embutido e handshake do Plugin Bridge passaram nos gates automáticos;
- ainda é necessário teste real em dois PCs/duas sessões OMSI para confirmar spawn, troca de Kachel, suavização, RP e navegação em mapas reais.

## [0.3.0-alpha.11-test.2] — teste comunitário, HUD corrigido e 3D experimental

### Corrigido

- velocidade do HUD passa a usar o `Groundspeed` do OMSI com fallback para a velocidade linear real;
- offset da velocidade linear do OMSI 2.3.004 corrigido para `0x174`; `0x1C0` permanece identificado como velocidade de rotação/turn;
- conflito de hooks `OnContentRendered` eliminado na tela principal;
- servidor dedicado volta a publicar corretamente como `win-x64` no pipeline da Test 2;
- smoke test bidirecional do bridge v2 ajustado para leitura/escrita concorrente no Named Pipe.

### Adicionado

- primeira rodada pública do **ônibus remoto físico 3D experimental**, com opt-in explícito e desligado por padrão;
- fluxo de `spawn → atualização → despawn` protegido pelo plugin/interop x86;
- registro `VehicleInstanceId → ponteiro OMSI` para impedir writes em veículos que não foram criados pelo NavBR;
- validação do ponteiro contra a lista `RoadVehicles` antes de alterações físicas;
- limite inicial de até 32 veículos remotos físicos registrados pelo caminho experimental;
- diagnóstico remoto opcional com consentimento, fila offline limitada, envio em lotes e sanitização de dados;
- coletor de diagnóstico desacoplado por `diagnostics.json` do portal;
- manual para iniciantes **dentro do aplicativo**, disponível offline em Português, English, Español, Deutsch e Français;
- créditos visíveis no app: **Desenvolvedor: MichaelPriest • Com apoio da IA ChatGPT**;
- checklist público `docs/ALPHA11_TEST2_COMMUNITY.md`;
- pacote separado do plugin experimental com `NavBR.OmsiPlugin.dll`, `.opl` e `NavBR.OmsiInterop.dll`.

### Melhorado

- HUD principal mais compacto e menos invasivo;
- painel do ônibus reduzido e fundos mais transparentes sem reduzir a legibilidade dos textos principais;
- quando existe uma viagem/rota ativa, o HUD mostra somente as paradas daquela rota; todas as paradas do mapa ficam reservadas ao estado sem rota ativa;
- leitura das paradas usa a sequência `[station]` do `.ttp` quando possível e a geometria ativa como fallback seguro;
- visibilidade do HUD continua baseada na janela real de gameplay aprendida a partir do `GetForegroundWindow()` do próprio `Omsi.exe`, evitando depender rigidamente de `Process.MainWindowHandle`;
- documentação, README e portal atualizados para a série Alpha.11;
- portal passa a destacar explicitamente a Test 2 e seus pacotes mesmo durante atrasos de atualização do catálogo de releases.

### Multiplayer

- meta inicial de até **32 jogadores por sala**;
- peer-host continua usando TCP `27730`;
- servidor dedicado continua opcional;
- snapshots de tráfego permanecem separados da contagem de jogadores, com até 48 veículos relevantes por snapshot;
- chat de texto e voz push-to-talk permanecem disponíveis.

### Diagnóstico e privacidade

- envio automático fica **desligado por padrão** e exige consentimento;
- não envia conteúdo do chat, áudio/voz, senhas, tokens ou arquivos pessoais;
- caminhos locais e identificadores sensíveis são sanitizados antes de entrar na fila;
- erros do bridge, comandos físicos e exceções do aplicativo podem entrar na telemetria técnica quando o usuário autoriza.

### Experimental / validação necessária

- ônibus remoto 3D ainda pode apresentar posicionamento incorreto entre tiles;
- compatibilidade com ônibus/add-ons precisa de teste comunitário amplo;
- movimento, quaternion, luzes, setas, freio, remoção e impacto em FPS ainda precisam de validação no OMSI real;
- cada computador precisa possuir legalmente e localmente o mesmo modelo de ônibus do jogador remoto no caminho relativo compatível em `Vehicles\`;
- o NavBR não transfere nem redistribui ônibus pagos/proprietários;
- tráfego IA físico compartilhado completo ainda não deve ser considerado finalizado.

## [0.3.0-alpha.10-test.4] — HUD moderno, GPS heading-up e novo ícone

### Adicionado

- barra superior translúcida dentro do jogo com status de conexão, mapa, jogadores, chat, PTT e atalhos;
- animação discreta do indicador de conexão;
- GPS em modo **heading-up**, mantendo o ônibus local apontado para cima enquanto mapa/rota giram;
- card separado para linha, destino e próxima parada;
- setas de manobra com distância aproximada quando existe geometria detalhada suficiente;
- chat visual acoplado abaixo do GPS;
- camada que bloqueia cliques no OMSI enquanto o chat está em modo de digitação;
- mais opções de atalhos para chat/PTT: F1-F4 e F9-F12 com combinações Shift/Ctrl/Ctrl+Shift;
- log de sessão automático em `%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr.log`.

### Melhorado

- novo ícone oficial incorporado ao app/EXE e validado pelo CI;
- F5-F8 continuam fora das opções de atalho para evitar conflitos comuns do OMSI;
- orientação de manobra é ocultada quando a rota disponível é apenas coarse/tile-fallback, evitando instruções inventadas;
- documentação do HUD/chat/voz e checklist de teste atualizados para a test.4.

### Mantido

- plugin OMSI continua experimental e opcional;
- preflight de .NET 10 Runtime x86 antes da instalação do plugin;
- multiplayer peer-host TCP 27730, SignalR, chat, PTT e reconexão/rejoin;
- ainda não existe criação física de ônibus remoto dentro do mundo 3D do OMSI.

### Validação necessária

- confirmar no OMSI 2.3.004 que o mapa gira no sentido correto durante curvas;
- confirmar que o clique não chega ao jogo durante a digitação do chat;
- validar setas de manobra em rota com geometria detalhada;
- confirmar novo ícone no Explorer/taskbar em máquina sem cache anterior;
- validar HUD moderno em janela e janela sem bordas;
- executar teste de dois PCs com GPS/HUD/chat/PTT e plugin bridge.

## [0.3.0-alpha.4] — convite colável e HUD suavizado

### Adicionado

- formato de convite versionado `NAVBR_INVITE_V1` com servidor, sala e porta;
- botão para colar convite recebido de outro jogador e preencher automaticamente servidor + sala;
- compatibilidade de leitura com o convite simples gerado pela alpha.3;
- suavização dos jogadores remotos também no minimapa compacto do HUD.

### Melhorado

- o minimapa do HUD atualiza os jogadores remotos em aproximadamente 30 FPS entre os frames de telemetria de rede;
- marcadores do HUD são ocultados quando o frame remoto fica antigo, sai da área visível ou o mapa é incompatível;
- convite peer-host fica pronto para ser enviado por Discord, WhatsApp ou outro chat sem o convidado precisar copiar campos manualmente.

### Validação pendente

- calibrar suavização em teste real com dois jogadores;
- validar copiar/colar convite entre dois PCs na mesma LAN;
- validar conexão externa após configuração de firewall/port forwarding;
- medir latência de voz e comportamento do HUD no OMSI 2.3.004.

## [0.3.0-alpha.3] — suavização e convite rápido

### Adicionado

- suavização/interpolação dos jogadores remotos no GPS principal para reduzir saltos entre frames de telemetria;
- interpolação do heading pelo caminho angular mais curto, evitando giros de quase 360° ao cruzar 0°/360°;
- timeout visual para ocultar marcadores remotos que deixaram de receber frames;
- botão de copiar convite da sala com endereço LAN, nome da sala e porta TCP `27730`.

### Melhorado

- o marcador remoto passa a trabalhar com alvo + movimento progressivo em vez de saltar diretamente a cada atualização de 4 Hz;
- mudanças grandes de posição continuam usando snap imediato para não animar teleporte/troca brusca de posição;
- fluxo peer-host ficou mais simples para compartilhar os dados básicos da sala em rede local.

### Validação pendente

- calibrar o fator de suavização em teste real com dois jogadores;
- validar comportamento com latência e perda de pacotes;
- aplicar a mesma estratégia de interpolação ao minimapa compacto do HUD se necessário;
- evoluir conexão pela Internet para reduzir a necessidade de port forwarding manual.

## [0.3.0-alpha.2] — peer-host, HUD, chat e voz

### Adicionado

- criação de sala diretamente pelo cliente: o PC de quem cria a sala inicia o servidor multiplayer embutido;
- porta padrão TCP `27730` e exibição automática dos endereços IPv4 da rede local;
- pacote de servidor dedicado continua disponível para hosts separados;
- HUD transparente sobre o OMSI com identidade própria do NavBR e disposição inspirada em HUDs de jogos de mundo aberto;
- minimapa compacto no canto inferior esquerdo, centralizado no ônibus local;
- jogadores remotos no minimapa;
- chat de texto por sala, também exibido no HUD;
- atalho global `T` para abrir o chat durante o jogo;
- chat de voz push-to-talk com atalho `N`;
- captura/reprodução de áudio com NAudio e codificação Opus com Concentus;
- indicador de quem está falando;
- identificador de compatibilidade do mapa para evitar misturar versões diferentes do mesmo mapa;
- licença MIT do projeto e avisos/licenças das dependências redistribuídas;
- traduções dos novos recursos em Português (Brasil), English, Español, Deutsch e Français.

### Multiplayer e rede

- presença, telemetria, texto e voz trafegam pelo PC que hospeda a sala;
- em LAN os jogadores podem usar o IPv4 exibido pelo NavBR;
- para conexões pela Internet, esta alpha pode exigir liberação no Windows Firewall e encaminhamento da porta TCP `27730` no roteador;
- UPnP/NAT traversal fica planejado para reduzir configuração manual em versões seguintes.

### Voz

- áudio inicial em 48 kHz mono;
- frames Opus de 20 ms;
- bitrate alvo de 24 kbit/s com VBR e FEC;
- transporte inicial pelo SignalR/WebSocket da própria sala;
- transporte UDP/WebRTC de menor latência poderá substituir essa camada futuramente.

### Legal

- código próprio do NavBR sob MIT em `LICENSE`;
- `THIRD_PARTY_NOTICES.md` criado;
- textos das licenças de ASP.NET Core/SignalR, NAudio e Concentus/Opus incluídos em `licenses/` e nos pacotes publicados.

### Validação pendente

- validar overlay em OMSI 2.3.004 em modo janela e janela sem bordas;
- medir latência e estabilidade do áudio em duas redes reais;
- confirmar permissões do Windows Firewall e experiência de port forwarding;
- testar vários microfones/dispositivos de áudio;
- fullscreen exclusivo ainda não é considerado validado.

## [0.3.0-alpha.1] — multiplayer online inicial

### Adicionado

- janela multiplayer com endereço do servidor, sala e apelido persistidos localmente;
- conexão cliente-servidor por SignalR/WebSocket;
- identidade local persistente independente de Steam;
- entrada e saída de salas com lista de jogadores conectados;
- reconexão automática após perda temporária da conexão;
- envio da telemetria do OMSI quatro vezes por segundo enquanto conectado;
- presença com mapa atual e atualização quando o jogador troca de mapa;
- cálculo de distância entre jogadores no mesmo mapa;
- marcadores azuis para jogadores remotos no GPS do NavBR, com heading e velocidade;
- contratos compartilhados para presença, snapshot da sala e frames de telemetria;
- registro de salas no servidor e limpeza automática ao desconectar;
- validação básica de identidade, tamanho de campos e valores numéricos da telemetria;
- traduções do multiplayer em Português (Brasil), English, Español, Deutsch e Français.

### Segurança e privacidade

- o servidor sobrescreve o `PlayerId` recebido na telemetria com a identidade da sessão, reduzindo spoofing básico;
- somente dados do jogo/sessão são transmitidos pelo cliente;
- o multiplayer continua independente de Steamworks.

## [0.2.0-alpha.3] — EXE standalone e ícone corrigido

### Corrigido

- corrigida a estrutura interna do arquivo `NavBR.ico` antes da compilação, evitando o ícone genérico do Windows no executável;
- o build agora valida que o `.exe` standalone contém recurso Win32 de ícone (`RT_GROUP_ICON`).

### Adicionado

- publicação de um **`.exe` standalone/self-contained** do cliente diretamente em cada GitHub Release;
- build de CI separado para validar o executável único Windows x86;
- reparo automático do contêiner ICO também em builds locais no Windows.

## [0.2.0-alpha.2] — GPS interativo

### Adicionado

- controles de zoom `+` e `-` no roadmap;
- zoom pelo scroll do mouse com âncora no ponto do cursor;
- pan do mapa arrastando com o botão esquerdo;
- modo **Seguir ônibus** para manter o veículo centralizado;
- comando **Ajustar** para enquadrar todo o roadmap;
- marcador direcional do ônibus rotacionado pelo heading da telemetria;
- opção **Sempre visível** para manter o NavBR acima de outras janelas;
- traduções dos novos controles em Português (Brasil), English, Español, Deutsch e Français.

### Melhorado

- visualizador do roadmap passou a usar viewport com zoom e rolagem em vez de apenas uma imagem ajustada;
- arrastar o mapa desativa automaticamente o modo de seguir o ônibus;
- troca de mapa reinicializa de forma segura zoom, posição e marcador.

## [0.2.0-alpha.1] — primeira versão de teste

### Adicionado

- telemetria externa e somente leitura para OMSI 2.3.004;
- detecção de `Omsi.exe` independente da Steam;
- fingerprint SHA-256 do executável;
- posição X/Y/Z, heading, velocidade e mapa carregado;
- dashboard de telemetria com polling de 200 ms;
- suporte inicial a Português (Brasil), English, Español, Deutsch e Français;
- identidade visual e ícone oficial do NavBR;
- catálogo automático dos mapas instalados;
- detecção de `global.cfg` e roadmaps do OMSI;
- transformação inicial para pixels do roadmap em mapas padrão de 300 m/tile;
- primeiro marcador do ônibus sobre `whole.roadmap.bmp`;
- workflow de GitHub Releases para cliente win-x86 e servidor win-x64 self-contained.

## [0.1.0-alpha] — bootstrap

- estrutura inicial do cliente WPF;
- servidor ASP.NET Core + SignalR;
- contratos compartilhados;
- detecção inicial do processo OMSI;
- estrutura multilíngue baseada em `.resx`.
