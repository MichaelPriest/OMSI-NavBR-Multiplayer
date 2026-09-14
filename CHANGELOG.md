# Changelog

Todas as mudanças relevantes do OMSI NavBR Multiplayer serão registradas aqui.

## [0.2.0-alpha.2] — em preparação

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
