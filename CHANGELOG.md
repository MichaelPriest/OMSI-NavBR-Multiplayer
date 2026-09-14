# Changelog

Todas as mudanças relevantes do OMSI NavBR Multiplayer serão registradas aqui.

## [0.2.0-alpha.1] — em preparação

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
- workflow de GitHub Releases para cliente win-x86 e servidor win-x64 self-contained.

### Melhorado

- descoberta de mapas tolera diretórios bloqueados, inválidos ou parcialmente instalados;
- CI compila cliente, servidor e biblioteca compartilhada.

### Validação pendente

- confirmar X/Y/Z em runtime em mais de um mapa;
- comparar velocidade do NavBR com o velocímetro do OMSI;
- confirmar orientação e zero do heading;
- implementar transformação OMSI world X/Y → pixels do roadmap;
- desenhar o ônibus no mapa.

## [0.1.0-alpha] — bootstrap

- estrutura inicial do cliente WPF;
- servidor ASP.NET Core + SignalR;
- contratos compartilhados;
- detecção inicial do processo OMSI;
- estrutura multilíngue baseada em `.resx`.
