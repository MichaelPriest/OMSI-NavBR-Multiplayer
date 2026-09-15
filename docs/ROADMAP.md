# Roadmap

Este roadmap foi atualizado para refletir o estado real da **v0.3.0-alpha.9**.

Legenda:

- ✅ implementado no código;
- 🧪 implementado, mas ainda depende de validação real no OMSI/rede;
- ⬜ ainda pendente de desenvolvimento.

## Fase 0 — Bootstrap

- ✅ Repositório e estrutura inicial
- ✅ Cliente WPF
- ✅ Servidor SignalR
- ✅ Contrato de telemetria compartilhado
- ✅ Detecção de `Omsi.exe` sem Steam
- ✅ Base multilíngue com `.resx` / `ResourceManager`
- ✅ Português (Brasil), English, Español, Deutsch e Français
- ✅ Detecção automática do idioma do Windows
- ✅ Troca de idioma em tempo real e preferência persistida
- ✅ Build CI
- ✅ Ícone oficial embutido no executável
- ✅ Versionamento SemVer
- ✅ Workflow de GitHub Release para cliente e servidor
- ✅ EXE standalone x86
- ✅ ZIP do cliente x86
- ✅ ZIP do servidor dedicado x64
- ✅ GitHub Pages com catálogo de releases e contador de downloads
- ✅ Seção de feedback da comunidade
- ✅ Seção Contribua via Pix no site

## Fase 1 — Telemetria local

- ✅ Detectar OMSI 2.3.004 por versão/perfil
- ✅ Abrir processo com acesso somente leitura
- ✅ Implementar leitura de posição X/Y/Z
- ✅ Implementar leitura de heading
- ✅ Implementar leitura de velocidade
- ✅ Implementar identificação do mapa carregado
- ✅ Fallback do mapa/versão pelo `logfile.txt`
- ✅ Resolver veículo ativo do jogador
- ✅ Profile de compatibilidade 2.3.004
- ✅ Suporte técnico ao perfil 2.2.032
- ✅ Dashboard/HUD de telemetria
- ✅ Ler GridX/GridY e posição local TileX/TileY
- 🧪 Validar posição/escala em runtime no OMSI 2.3.004
- 🧪 Validar velocidade contra o velocímetro do OMSI
- 🧪 Validar sinal/zero do heading
- 🧪 Validar amplamente o perfil 2.2.032
- ⬜ Criar allowlist de hashes conhecidos após os testes
- ⬜ Fallback por signature scanning para builds futuras

### Critério de aceite

Com o OMSI 2.3.004 aberto e um ônibus ativo, o NavBR deve mostrar mapa, X/Y/Z, direção e velocidade mudando em tempo real sem Steam API e sem permissão de escrita no processo. O código já existe; falta consolidar a validação real.

## Fase 2 — GPS e HUD local

- ✅ Detectar pasta `maps`
- ✅ Catalogar mapas com `global.cfg`
- ✅ Detectar `whole.roadmap.bmp` / `roadmap.bmp`
- ✅ Mostrar mapas e roadmaps disponíveis no cliente
- ✅ Localizar textos do GPS nos cinco idiomas atuais
- ✅ Ler grade de tiles do `global.cfg`
- ✅ Transformar GridX/GridY + posição local em pixels do roadmap
- ✅ Suporte técnico a `[worldcoordinates]`
- ✅ Renderizar ônibus sobre o roadmap
- ✅ Zoom por botões/roda do mouse
- ✅ Zoom do HUD até 10×
- ✅ Pan por arraste
- ✅ Follow vehicle
- ✅ Indicador do ônibus rotacionado pelo heading
- ✅ Ajustar roadmap ao viewport
- ✅ HUD móvel com posição persistente
- ✅ Chat visual, jogadores e indicador de voz no HUD
- ✅ Proteção dos atalhos usando `Inputs/keyboard.cfg`
- ✅ Aprendizado da janela real de gameplay do OMSI para controlar a visibilidade do HUD
- ✅ Guia de geração de roadmap em `docs/GERAR_ROADMAP_MAPAS.md`
- 🧪 Validar visualmente marcador/heading em mapas reais
- 🧪 Validar `[worldcoordinates]` em mapas reais
- 🧪 Validar ocultação/reexibição do HUD em menus, opções e timetable
- 🧪 Validar fullscreen exclusivo
- ⬜ Modo heading-up com rotação do mapa inteiro
- ⬜ Tratamento visual aprimorado para mapas sem roadmap

## Fase 3 — TTData e navegação

### Já implementado

- ✅ Resolver `TTData` do mapa base
- ✅ Resolver `Chrono/*/TTData`
- ✅ Resolver `.ttp -> .ttr`
- ✅ Parser das entradas de `.ttr`
- ✅ Interpretar o terceiro campo de `[track_entry]` como índice de tile
- ✅ Resolver índice de tile pela ordem dos blocos `[map]` do `global.cfg`
- ✅ Linha/track ativa
- ✅ Destino quando disponível
- ✅ Próxima parada quando disponível pela telemetria/timetable
- ✅ Diagnóstico em `navbr-route.log`
- ✅ Fallback por tiles quando a geometria detalhada não pode ser resolvida
- ✅ Resolução de splines `.sli`
- ✅ Uso de `ObjectId` e `PathId`
- ✅ Paths `[path]` / `[path_2]`
- ✅ Resolução de objetos/crossings `.sco` quando possível

### Aguardando validação real

- 🧪 Conferir o traçado detalhado em mapas reais
- 🧪 Confirmar orientação/curvas das splines
- 🧪 Confirmar deslocamento lateral de faixa por `PathId`
- 🧪 Confirmar crossings/paths de objetos
- 🧪 Confirmar destino e próxima parada em diferentes mapas/ônibus
- 🧪 Testar mapas com TTData não convencional e Chronos ativos

### Ainda pendente

- ⬜ Parser dedicado de `Busstops.cfg`
- ⬜ Parser dedicado de `.ttl`, se necessário para recursos futuros
- ⬜ Distância restante
- ⬜ ETA
- ⬜ Atraso/adiantamento
- ⬜ Formatação de distância/tempo conforme cultura selecionada
- ⬜ Instruções de navegação mais avançadas por trecho/manobra

## Fase 4 — Multiplayer peer-host

### Já implementado

- ✅ Cliente SignalR
- ✅ Host ASP.NET Core/SignalR iniciado pelo próprio cliente
- ✅ Criar sala no PC do jogador
- ✅ Entrar em sala
- ✅ Nickname/presença
- ✅ Porta padrão TCP `27730`
- ✅ Convite versionado `NAVBR_INVITE_V1`
- ✅ Telemetria compartilhada em tempo real
- ✅ Jogadores remotos no mapa/minimapa
- ✅ Suavização/interpolação visual de jogadores remotos
- ✅ Identificador/fingerprint de compatibilidade do mapa
- ✅ Chat de texto
- ✅ Chat visual no HUD
- ✅ Voz push-to-talk
- ✅ NAudio + Concentus/Opus
- ✅ Indicador de quem está falando
- ✅ Reconexão automática SignalR com reentrada na sala
- ✅ Servidor dedicado opcional
- ✅ Detecção de endereços IPv4 locais do host
- ✅ Fluxo de configuração de firewall previsto pelo cliente

### Aguardando validação real

- 🧪 Teste completo entre dois computadores em LAN
- 🧪 Teste pela Internet com NAT/port forwarding
- 🧪 Validação de telemetria de vários jogadores
- 🧪 Validação de chat sob uso real
- 🧪 Validação de voz, microfone, reprodução e latência
- 🧪 Validação dos atalhos F9/F10 e combinações modificadas
- 🧪 Validar reconexão e reentrada automática após queda temporária
- 🧪 Teste do servidor dedicado separado do PC do jogador

### Ainda pendente

- ⬜ Diagnóstico de conectividade/porta mais completo
- ⬜ Rate limiting
- ⬜ Códigos de erro de rede estruturados e independentes de idioma
- ⬜ UPnP/NAT traversal
- ⬜ Melhorias de qualidade de voz sob perda/latência de rede

## Fase 5 — Infraestrutura online opcional

O peer-host é o modo principal da série 0.3. Esta fase é complementar, não requisito para uma sala comum.

- ⬜ Autenticação opcional
- ⬜ Descoberta de salas públicas
- ⬜ Salas privadas gerenciadas por serviço online
- ⬜ Senha/convite gerenciado por serviço
- ⬜ Presence service global
- ⬜ Persistência mínima
- ⬜ Deployment de produção

## Fase 6 — Veículos remotos dentro do OMSI (experimental)

Esta fase começou em uma branch isolada e **não faz parte da release alpha.9 normal**.

### Já iniciado

- ✅ Confirmar a interface oficial de plugins do OMSI (`.dll` + `.opl` em `OMSI\plugins`)
- ✅ Definir plugin experimental como módulo opcional, separado do cliente estável
- ✅ Criar projeto x86 `NavBR.OmsiPluginExperimental`
- ✅ Exportar callbacks básicos esperados pelo OMSI
- ✅ Criar `.opl` mínimo usando uma system variable para heartbeat
- ✅ Implementar log/heartbeat em `%LOCALAPPDATA%\OMSI NavBR Multiplayer\navbr-plugin.log`
- ✅ Manter o protótipo sem escrita de variáveis/triggers nesta primeira etapa
- ✅ Adicionar compilação/artefato do plugin experimental ao CI da branch/PR
- ✅ CI compilar e validar o pacote x86 experimental

### Aguardando validação real

- 🧪 Confirmar que o OMSI 2.3.004 carrega a DLL x86
- 🧪 Confirmar `PluginStart`, callbacks periódicos e `PluginFinalize`
- 🧪 Confirmar que o plugin não causa instabilidade ou queda de FPS perceptível

### Próximas etapas experimentais

- ⬜ Criar bridge local seguro entre o cliente NavBR e o plugin
- ⬜ Definir protocolo/versionamento para estado de veículos remotos
- ⬜ Enviar ao plugin um único jogador remoto de teste
- ⬜ Investigar criação/controle seguro de entidade remota no OMSI
- ⬜ Sincronizar posição/orientação de um único veículo remoto
- ⬜ Interpolação/extrapolação dentro do simulador
- ⬜ Compatibilidade/fallback de modelo de ônibus
- ⬜ Articulação
- ⬜ Portas
- ⬜ Luzes/setas/pisca-alerta/buzina
- ⬜ Linha/destino/matriz quando tecnicamente seguro
- ⬜ Limites de distância, quantidade de jogadores e performance
- ⬜ Procedimento automático de instalação/remoção do plugin

Esta fase só será promovida a funcionalidade oficial se funcionar sem corromper estado do simulador e sem tornar o plugin obrigatório para quem quiser apenas GPS/HUD/multiplayer externo.

## Prioridade imediata após a alpha.9

O plugin pode avançar em paralelo apenas como experimento isolado. Para o produto principal, a prioridade continua:

1. 🧪 validar HUD e telemetria no OMSI real;
2. 🧪 validar o novo parser/traçado `.ttr` em mapas reais;
3. 🧪 validar destino e próxima parada;
4. 🧪 testar multiplayer entre dois computadores;
5. 🧪 testar chat/voz/PTT e reconexão;
6. 🧪 validar o carregamento seguro do plugin experimental;
7. corrigir os problemas encontrados;
8. só então avançar para ETA/distância, diagnóstico de rede, UPnP/NAT traversal e criação real de veículo remoto.

## Localização contínua

Novas telas e funcionalidades devem nascer com chaves de recurso. Traduções não devem ser codificadas dentro de telemetria, TTData, mapas ou protocolo multiplayer. Isso permite adicionar novos idiomas progressivamente sem alterar a lógica do simulador.
