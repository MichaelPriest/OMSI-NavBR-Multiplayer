# Roadmap

Este roadmap reflete o estado da série **v0.3.0-alpha.11** e separa claramente o que é prioridade antes da primeira versão estável do que fica planejado para depois do **v1.0**.

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
- ✅ Seção Contribua via Pix no site e no README

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

Com o OMSI 2.3.004 aberto e um ônibus ativo, o NavBR deve mostrar mapa, X/Y/Z, direção e velocidade mudando em tempo real sem Steam API. Escritas físicas permanecem restritas ao caminho experimental do plugin.

## Fase 2 — GPS e HUD local

- ✅ Detectar pasta `maps`
- ✅ Catalogar mapas com `global.cfg`
- ✅ Detectar `whole.roadmap.bmp` / `roadmap.bmp`
- ✅ Mostrar mapas e roadmaps disponíveis no cliente
- ✅ Separar mapas em **roadmap pronto** e **roadmap ausente**
- ✅ Mostrar nome, pasta, quantidade de tiles e BMP global encontrado
- ✅ Não considerar roadmap individual de tile como roadmap global pronto
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
- ✅ HUD Alpha.11 mais compacto e transparente
- ✅ Guia de geração de roadmap em `docs/GERAR_ROADMAP_MAPAS.md`
- 🧪 Validar visualmente marcador/heading em mapas reais
- 🧪 Validar a nova lista de roadmaps em uma instalação grande de OMSI
- 🧪 Validar `[worldcoordinates]` em mapas reais
- 🧪 Validar ocultação/reexibição do HUD em menus, opções e timetable
- 🧪 Validar fullscreen exclusivo
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
- ✅ Filtrar paradas pela viagem/rota ativa
- ✅ Mostrar todas as paradas somente quando não houver rota ativa

### Aguardando validação real

- 🧪 Conferir o traçado detalhado em mapas reais
- 🧪 Confirmar orientação/curvas das splines
- 🧪 Confirmar deslocamento lateral de faixa por `PathId`
- 🧪 Confirmar crossings/paths de objetos
- 🧪 Confirmar destino e próxima parada em diferentes mapas/ônibus
- 🧪 Testar mapas com TTData não convencional e Chronos ativos
- 🧪 Confirmar filtro de paradas em linhas que compartilham terminais e corredores

### Ainda pendente antes da estabilidade

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
- ✅ Atualização dinâmica de mapa + fingerprint quando o jogador troca de mapa durante a sessão
- ✅ Chat de texto
- ✅ Chat visual no HUD
- ✅ Voz push-to-talk
- ✅ NAudio + Concentus/Opus
- ✅ Indicador de quem está falando
- ✅ Reconexão automática SignalR com reentrada na sala
- ✅ Servidor dedicado opcional
- ✅ Detecção de endereços IPv4 locais do host
- ✅ Fluxo de configuração de firewall previsto pelo cliente
- ✅ Meta inicial documentada de até 32 jogadores por sala

### Aguardando validação real

- 🧪 Teste completo entre dois computadores em LAN
- 🧪 Teste pela Internet com NAT/port forwarding
- 🧪 Validação de telemetria de vários jogadores
- 🧪 Validar troca de mapa/fingerprint durante uma sessão
- 🧪 Validação de chat sob uso real
- 🧪 Validação de voz, microfone, reprodução e latência
- 🧪 Validação dos atalhos F9/F10 e combinações modificadas
- 🧪 Validar reconexão e reentrada automática após queda temporária
- 🧪 Teste do servidor dedicado separado do PC do jogador
- 🧪 Testes progressivos de carga até o alvo de 32 jogadores

### Ainda pendente antes da estabilidade

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

A Alpha.11 abre o primeiro teste público da camada física, mas o plugin continua opcional.

### Implementado na base experimental

- ✅ Interface de plugins OMSI (`.dll` + `.opl` em `OMSI\plugins`)
- ✅ Projeto x86 `NavBR.OmsiPluginExperimental`
- ✅ Plugin Native AOT x86
- ✅ Windows Named Pipe local restrito ao usuário atual
- ✅ Protocolo bridge versionado
- ✅ Reconexão local do bridge
- ✅ Encaminhamento de contexto do veículo local
- ✅ Encaminhamento de frames remotos do SignalR ao plugin
- ✅ Propagação de remoção de jogador e limpeza da sala
- ✅ Registro protegido de instâncias físicas do NavBR
- ✅ Shim C++ x86 para chamadas nativas do OMSI 2.3.004
- ✅ Spawn/update/despawn experimental disponível por opt-in
- ✅ Atualização experimental de posição, rotação, velocidade e estados visuais básicos
- ✅ Diagnósticos automáticos opcionais para o teste comunitário

### Aguardando validação real

- 🧪 Confirmar spawn físico em dois computadores reais
- 🧪 Confirmar posição correta em tiles diferentes
- 🧪 Confirmar rotação/quaternion e suavização
- 🧪 Confirmar faróis, freio e setas
- 🧪 Confirmar remoção segura quando jogador sai/desconecta
- 🧪 Confirmar compatibilidade com diferentes ônibus/add-ons
- 🧪 Confirmar estabilidade e impacto em FPS
- 🧪 Confirmar comportamento com vários ônibus físicos simultâneos

### Próximas etapas antes da estabilidade

- ⬜ Fechar sincronização confiável entre tiles
- ⬜ Extrapolação curta e limitada para perda de pacotes
- ⬜ Política de snap por erro máximo seguro
- ⬜ Compatibilidade/fallback de modelo de ônibus
- ⬜ Articulação
- ⬜ Portas
- ⬜ Linha/destino/matriz quando tecnicamente seguro
- ⬜ Limites de distância/LOD e performance para entidades físicas
- ⬜ Limpeza stale totalmente validada em runtime

Esta fase só será promovida a funcionalidade oficial se funcionar sem corromper o estado do simulador e sem tornar o plugin obrigatório para quem quiser apenas GPS/HUD/multiplayer externo.

## Prioridade até a primeira versão estável

Antes de abrir recursos de gestão/comunidade mais amplos, a prioridade é:

1. 🧪 validar HUD, velocidade e telemetria no OMSI real;
2. 🧪 validar rota, TTData e paradas em vários mapas;
3. 🧪 testar multiplayer entre dois ou mais computadores;
4. 🧪 testar chat/voz/PTT, reconexão e troca de mapa;
5. 🧪 validar o ônibus remoto 3D experimental;
6. 🧪 validar estabilidade, tiles, add-ons e desempenho;
7. ⬜ fechar diagnóstico de rede, rate limiting e NAT traversal;
8. ⬜ corrigir problemas apontados pela comunidade;
9. ⬜ promover somente recursos validados para a primeira versão estável.

## Pós-v1.0 — Plataforma comunitária OMSI

Os recursos abaixo ficam deliberadamente **depois da primeira versão estável**, para não desviar o foco da estabilidade do núcleo atual:

- ⬜ navegador público de servidores/salas com ping, mapa, versão, região e favoritos;
- ⬜ perfil do motorista com horas, quilômetros, linhas e histórico;
- ⬜ empresas virtuais, cargos, frota e garagem;
- ⬜ CCO/dispatcher com mapa operacional, atrasos, intervalos e mensagens;
- ⬜ pontualidade por parada, atraso e adiantamento;
- ⬜ sincronização opcional de hora/data/clima da sessão;
- ⬜ identificação de frota, prefixo, garagem e matrícula;
- ⬜ voz por proximidade e canais separados;
- ⬜ mapa web ao vivo opcional da sessão;
- ⬜ replay de viagens;
- ⬜ eventos e operações especiais comunitárias;
- ⬜ verificação automática de compatibilidade antes de entrar em uma sala;
- ⬜ manifesto de mods/dependências com hashes, sem redistribuir conteúdo sem permissão;
- ⬜ SDK/API NavBR para dashboards, bots e ferramentas de terceiros;
- ⬜ workshop comunitário para perfis, mapas, HUD e traduções permitidas;
- ⬜ painel de saúde da sessão com ping, perda, FPS e estado do bridge;
- ⬜ LOD/culling e frequência adaptativa por distância;
- ⬜ sincronização ampliada de portas, matriz, animações e estados do veículo;
- ⬜ autoridade de tráfego físico compartilhado mais avançada;
- ⬜ permissões de servidor: admin, moderador, CCO, motorista e visitante.

## Apoio ao projeto

### Brasil

- ✅ Pix continua como canal direto principal de apoio voluntário.
- ✅ A chave Pix aparece no portal e no README.

### Internacional / futuro

- ⬜ Estudar uma presença oficial do NavBR no Google Play após a consolidação da versão estável e de um eventual app/companion Android.
- ⬜ Se houver vendas, compras no app ou assinaturas pelo Google Play, usar o perfil de pagamentos oficial e direcionar os recebimentos para a **mesma conta bancária brasileira vinculada à chave Pix**, centralizando a tesouraria em uma única conta.
- ⬜ Não tratar Google Play como transferência direta para uma chave Pix: os repasses de comerciante seguem o sistema de pagamentos bancários do Google.
- ⬜ Manter qualquer apoio opcional separado dos recursos gratuitos essenciais do NavBR, salvo decisão futura explícita sobre produtos pagos adicionais.

## Localização contínua

Novas telas e funcionalidades devem nascer com chaves de recurso. Traduções não devem ser codificadas dentro de telemetria, TTData, mapas ou protocolo multiplayer. Isso permite adicionar novos idiomas progressivamente sem alterar a lógica do simulador.
