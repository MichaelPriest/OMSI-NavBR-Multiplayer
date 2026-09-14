# Changelog

Todas as mudanças relevantes do OMSI NavBR Multiplayer serão registradas aqui.

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
- traduções do multiplayer em Português (Brasil), English, Español, Deutsch e Français;
- GitHub Pages passa a atualizar o catálogo também quando uma Release é publicada.

### Segurança e privacidade

- o servidor sobrescreve o `PlayerId` recebido na telemetria com a identidade da sessão, reduzindo spoofing básico;
- somente dados do jogo/sessão são transmitidos pelo cliente; nenhuma localização do mundo real é enviada;
- o multiplayer continua independente de Steamworks.

### Próximos passos

- hospedar uma instância pública oficial do servidor NavBR;
- salas privadas com senha e criação/listagem de salas;
- suavização/interpolação dos jogadores remotos;
- compatibilidade de mapa por identificador/hash;
- sincronização opcional de linha, rota e estados adicionais do ônibus.

## [0.2.0-alpha.3] — EXE standalone e ícone corrigido

### Corrigido

- corrigida a estrutura interna do arquivo `NavBR.ico` antes da compilação, evitando o ícone genérico do Windows no executável;
- o build agora valida que o `.exe` standalone contém recurso Win32 de ícone (`RT_GROUP_ICON`).

### Adicionado

- publicação de um **`.exe` standalone/self-contained** do cliente diretamente em cada GitHub Release;
- build de CI separado para validar o executável único Windows x86;
- reparo automático do contêiner ICO também em builds locais no Windows.

### Distribuição

A partir desta versão, cada release publica:

- `OMSI-NavBR-Multiplayer-vX.X.X-win-x86.exe` — cliente standalone;
- `OMSI-NavBR-Multiplayer-vX.X.X-win-x86.zip` — cliente completo em ZIP;
- `OMSI-NavBR-Server-vX.X.X-win-x64.zip` — servidor multiplayer.

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

### Validação pendente

- confirmar no OMSI 2.3.004 a posição real do marcador em diferentes mapas;
- confirmar o sentido/zero do heading antes de considerar a seta calibrada;
- implementar mapas com `[worldcoordinates]` sem assumir tile de 300 m.

## [0.2.0-alpha.1] — primeira versão de teste

### Adicionado

- telemetria externa e somente leitura para OMSI 2.3.004;
- detecção de `Omsi.exe` independente da Steam;
- fingerprint SHA-256 do executável;
- posição X/Y/Z, heading, velocidade e mapa carregado;
- dashboard de telemetria com polling de 200 ms;
- suporte inicial a Português (Brasil), English, Español, Deutsch e Français;
- troca de idioma em tempo real e preferência persistida;
- identidade visual e ícone oficial do NavBR;
- catálogo automático dos mapas instalados;
- detecção de `global.cfg` e roadmaps do OMSI;
- leitura de GridX/GridY e posição local TileX/TileY do OMSI 2.3.004;
- transformação inicial para pixels do roadmap em mapas padrão de 300 m/tile;
- primeiro marcador do ônibus sobre `whole.roadmap.bmp`;
- workflow de GitHub Releases para cliente win-x86 e servidor win-x64 self-contained.

### Melhorado

- descoberta de mapas tolera diretórios bloqueados, inválidos ou parcialmente instalados;
- CI compila cliente, servidor e biblioteca compartilhada.

### Validação pendente

- confirmar X/Y/Z em runtime em mais de um mapa;
- comparar velocidade do NavBR com o velocímetro do OMSI;
- confirmar orientação e zero do heading;
- validar o marcador no roadmap em runtime.

## [0.1.0-alpha] — bootstrap

- estrutura inicial do cliente WPF;
- servidor ASP.NET Core + SignalR;
- contratos compartilhados;
- detecção inicial do processo OMSI;
- estrutura multilíngue baseada em `.resx`.
