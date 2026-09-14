# Changelog

Todas as mudanças relevantes do OMSI NavBR Multiplayer serão registradas aqui.

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
