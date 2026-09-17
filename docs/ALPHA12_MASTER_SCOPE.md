# Alpha.12 — escopo mestre de expansão

A **Alpha.12** consolida em uma única versão de desenvolvimento todos os recursos que estavam espalhados entre roadmap, ideias futuras e módulos experimentais do OMSI NavBR Multiplayer.

Branch de desenvolvimento:

```text
feature/alpha12-full-expansion
```

Versão de desenvolvimento:

```text
0.3.0-alpha.12-dev
```

## Regra da Alpha.12

Todos os itens abaixo fazem parte do escopo da próxima versão. Recursos que ainda não tiverem validação suficiente podem permanecer atrás de **feature flags experimentais**, mas não devem desaparecer do backlog.

Legenda:

- ✅ já existe na base e será preservado;
- 🚧 entra em desenvolvimento na Alpha.12;
- 🧪 existe parcialmente/experimental e precisa ser concluído/validado;
- 🔒 deve ficar protegido por opt-in/feature flag enquanto não for estável.

---

## 1. Núcleo, interface e experiência do usuário

- ✅ cliente WPF x86 independente da Steam;
- ✅ servidor SignalR e modo peer-host;
- ✅ EXE standalone e pacotes ZIP;
- ✅ GitHub Releases e GitHub Pages;
- ✅ suporte a pt-BR, English, Español, Deutsch e Français;
- 🚧 remover textos hardcoded da interface Alpha.11/12 e mover tudo para recursos localizados;
- ✅ dashboard principal mais limpo e orientado ao motorista;
- ✅ Multiplayer como página integrada ao shell principal;
- ✅ Configurações centralizadas;
- 🚧 central de notificações/status;
- ✅ tela de saúde do sistema e da sessão;
- 🚧 modo espectador;
- ✅ perfis visuais/HUD configuráveis;
- 🚧 acessibilidade e melhor suporte a escalas do Windows;
- 🚧 atalhos totalmente configuráveis e verificados contra conflitos do OMSI.

## 2. Telemetria OMSI e compatibilidade

- ✅ leitura read-only de processo;
- ✅ mapa, veículo, posição, Grid/Tile, heading e velocidade;
- ✅ OMSI 2.3.004 como alvo principal;
- ✅ base técnica 2.2.032;
- 🧪 validação ampla de offsets em diferentes instalações;
- 🚧 allowlist de hashes conhecidos;
- 🚧 signature scanning para builds futuras;
- 🚧 perfis de ônibus/add-ons para variáveis específicas;
- 🚧 catálogo de capacidades por modelo de ônibus;
- 🚧 diagnóstico automático de incompatibilidade;
- 🚧 fallback seguro quando uma variável não existir.

## 3. GPS, HUD, TTData e navegação

- ✅ roadmap, zoom, pan e follow vehicle;
- ✅ rota ativa, linha, destino e próxima parada;
- ✅ leitura `.ttp`, `.ttr`, Chrono, splines e crossings;
- ✅ filtro de paradas da viagem ativa;
- ✅ visão geral da rota;
- ✅ distância restante total calculada sobre a geometria real da rota;
- ✅ distância até a próxima parada quando a parada real pode ser projetada com segurança na rota;
- ✅ ETA da próxima parada com estimativa adaptativa baseada somente no progresso real observado; sem amostras suficientes/recentes exibe `—`;
- ✅ ETA de chegada ao terminal/fim da rota com a mesma estimativa adaptativa e descarte de amostras obsoletas;
- 🚧 atraso/adiantamento por viagem;
- 🚧 atraso/adiantamento por parada;
- ✅ instruções de navegação por trecho/manobra calculadas da geometria real da rota;
- 🧪 aviso antecipado de curva/saída — curvas/manobras e distância já existem; sem classificação semântica específica de saída viária;
- 🚧 nomes de vias via perfis `NavBR.streets.json`;
- 🚧 gerador/editor/importador de perfis de ruas no Roadmap Studio;
- 🚧 melhor experiência para mapas sem `whole.roadmap.bmp`;
- 🚧 geração automática/vetorial de roadmap sem depender do OMSI Editor;
- 🚧 cache de geometria e otimização de mapas grandes.

## 4. Multiplayer peer-host

- ✅ host local no PC de quem cria a sala;
- ✅ porta inicial TCP 27730;
- ✅ criar/entrar em sala;
- ✅ presença, telemetria, chat e PTT;
- ✅ jogadores remotos no mapa;
- ✅ reconexão automática;
- ✅ servidor dedicado opcional;
- ✅ diagnóstico de conectividade/porta;
- ✅ teste automático de porta acessível externamente por probe opt-in;
- ✅ UPnP quando disponível;
- 🚧 NAT traversal/fallback seguro;
- ✅ códigos de erro de rede estruturados;
- ✅ rate limiting;
- 🚧 limites e proteção contra abuso;
- ✅ métricas de ping, jitter e perda;
- ✅ frequência adaptativa de telemetria;
- 🚧 LOD/culling por distância;
- 🚧 extrapolação curta para perda de pacotes;
- 🚧 snap seguro quando o erro ultrapassar tolerância;
- 🚧 testes progressivos até 32 jogadores.

## 5. Salas públicas, privadas e infraestrutura online opcional

- ✅ navegador público de salas/servidores usando o diretório real `/api/rooms`;
- 🧪 filtros — busca por sala/mapa/versão/ônibus/HOF, compatibilidade e quantidade mínima de jogadores já existem; região ainda não é publicada pelo protocolo atual;
- 🚧 ping visível antes de entrar — a listagem pública ainda não publica endpoint do host para medição direta por sala;
- ✅ favoritos persistentes localmente, com filtro e prioridade na ordenação;
- ✅ salas privadas;
- 🧪 senha/convite gerenciado — senha efêmera existe; convite avançado ainda não;
- 🚧 presence service global;
- 🚧 autenticação opcional;
- 🚧 persistência mínima de perfil e preferências online;
- 🚧 serviço de descoberta separado do peer-host;
- 🔒 toda infraestrutura online deve continuar opcional para quem só quiser host direto.

## 6. Voz e comunicação

- ✅ chat de texto;
- ✅ push-to-talk;
- ✅ Opus/NAudio;
- ✅ voz por proximidade;
- ✅ canais de voz separados;
- ✅ canal da sala;
- ✅ canal de empresa;
- ✅ canal CCO/dispatcher;
- ✅ mute/deafen;
- ✅ seleção de dispositivos de entrada/saída;
- ✅ ganho individual por jogador;
- ✅ indicadores de qualidade da voz;
- ✅ jitter buffer adaptativo e tratamento de perda.

## 7. Veículos remotos físicos dentro do OMSI

- ✅ plugin Native AOT x86;
- ✅ Named Pipe local;
- ✅ protocolo bridge versionado;
- ✅ shim C++ x86;
- ✅ spawn/update/despawn experimental;
- ✅ posição, rotação e estados básicos;
- 🧪 sincronização entre tiles;
- 🚧 articulação de ônibus;
- 🚧 portas;
- 🚧 matriz/linha/destino;
- 🚧 faróis, freio, setas e alerta com perfis por veículo;
- 🚧 limpadores e outros estados visuais;
- 🚧 rodas/animações quando tecnicamente seguro;
- 🚧 fallback de modelo de ônibus;
- 🚧 validação de modelo/HOF/dependências;
- 🚧 limite de distância para spawn físico;
- 🚧 LOD/culling;
- 🚧 limpeza stale validada;
- 🚧 sincronização de vários veículos simultâneos;
- 🔒 escrita no OMSI permanece isolada, opt-in e experimental até validação ampla.

## 8. Tráfego IA compartilhado

- ✅ leitura de tráfego e snapshots experimentais;
- 🚧 autoridade de tráfego por sessão;
- 🚧 seleção de veículos relevantes por distância;
- 🚧 sincronização de transform e estados;
- 🚧 spawn/despawn remoto de tráfego quando seguro;
- 🚧 limitação por desempenho;
- 🚧 política de ownership para evitar duplicação;
- 🚧 fallback para somente visualização quando a sincronização física não for segura.

## 9. Hardware Cockpit — Arduino/ESP32

- ✅ `NAVBR_HW_V1`;
- ✅ USB/Serial;
- ✅ COM/baud/conectar/desconectar;
- ✅ JSON Lines ~5 Hz;
- ✅ velocidade, linha, rota, destino, próxima parada e estados;
- ✅ `stopRequested` via `haltewunsch`;
- ✅ exemplo Arduino com LED de parada;
- ✅ persistência de COM/baud;
- ✅ auto-reconnect com backoff e reconexão restrita à mesma COM salva;
- 🚧 perfis/aliases de variáveis por ônibus;
- 🚧 Wi-Fi ESP32 via UDP;
- 🚧 WebSocket para ESP32;
- 🚧 OLED;
- 🚧 LCD;
- 🚧 matriz de LED;
- 🚧 letreiro dianteiro/lateral/traseiro;
- 🚧 velocímetro físico;
- 🚧 indicadores de porta, seta, farol e freio;
- 🚧 buzzer/avisos;
- 🚧 exemplos de projetos físicos documentados;
- 🔒 fase bidirecional opcional: botões físicos -> NavBR -> OMSI;
- 🚧 isolamento e autorização explícita para qualquer escrita vinda do hardware.

## 10. Perfil do motorista

- ✅ perfil local do motorista;
- 🚧 nickname/identidade de sessão;
- ✅ horas dirigidas calculadas a partir da telemetria real;
- ✅ quilômetros rodados calculados a partir da telemetria real;
- 🧪 linhas operadas — cada viagem concluída registra a linha/rota real observada quando disponível; ainda não há catálogo/agregação por linha;
- 🧪 mapas utilizados — cada viagem concluída registra o mapa real observado quando disponível; ainda não há catálogo/agregação por mapa;
- ✅ histórico detalhado de viagens local, versionado e limitado às 250 mais recentes, com início/fim, tempo dirigindo, distância, maior velocidade, mapa, linha, rota e veículo reais quando disponíveis;
- 🚧 pontualidade;
- 🧪 estatísticas de condução — tempo, distância, viagens, maior velocidade, média móvel e detalhes por viagem existem; conjunto avançado ainda não;
- 🚧 conquistas/medalhas opcionais;
- ✅ exportação/importação do perfil com formato versionado e validação antes de sobrescrever;
- 🚧 sincronização online opcional.

## 11. Empresas virtuais

- 🚧 criação de empresa virtual;
- 🚧 nome, sigla e identidade visual;
- 🚧 cargos;
- 🚧 motoristas;
- 🚧 frota;
- 🚧 prefixos;
- 🚧 garagem;
- 🚧 veículos autorizados;
- 🚧 linhas/serviços atribuídos;
- 🚧 escala/operação;
- 🚧 estatísticas da empresa;
- 🚧 permissões por cargo;
- 🚧 integração opcional com salas privadas/públicas.

## 12. CCO / Dispatcher

- 🧪 painel operacional da sessão;
- ✅ mapa ao vivo de motoristas quando roadmap/layout/telemetria são resolvíveis;
- ✅ linha/viagem atual quando disponível na telemetria;
- 🚧 atraso/adiantamento;
- 🚧 intervalo entre veículos;
- 🧪 mensagens operacionais;
- 🚧 despacho de motoristas;
- 🚧 atribuição de linha/veículo;
- 🚧 incidentes/avisos;
- ✅ canais de voz do CCO;
- 🧪 permissões específicas de dispatcher;
- 🚧 modo somente observação para supervisores.

## 13. Sincronização de sessão

- 🚧 hora da sessão;
- 🚧 data da sessão;
- 🚧 clima opcional;
- 🚧 política de autoridade do host;
- 🚧 possibilidade de seguir o host ou manter tempo/clima local;
- 🚧 sincronização de eventos operacionais;
- 🚧 mensagens globais da sessão.

## 14. Identificação de frota e veículo

- 🚧 prefixo/frota;
- 🚧 garagem;
- 🚧 matrícula;
- 🚧 número interno;
- 🧪 empresa virtual vinculada;
- ✅ HOF ativo quando disponível na telemetria/compatibilidade;
- ✅ modelo e variante do ônibus quando disponíveis;
- 🧪 exibição desses dados no HUD/CCO quando permitido.

## 15. Compatibilidade de mapas/mods/dependências

- ✅ fingerprint de mapa básico;
- 🚧 manifesto versionado de compatibilidade;
- 🚧 hashes de arquivos relevantes;
- ✅ detecção de HOF quando disponível;
- 🧪 detecção de ônibus/modelos necessários;
- 🚧 lista do que está ausente antes de entrar na sala;
- 🧪 comparação de versões;
- 🧪 política de compatibilidade estrita/opcional;
- 🚧 nunca redistribuir conteúdo pago/proprietário sem permissão.

## 16. Replay e Ghost Bus

- ✅ base versionada de Ghost Recorder/Replay;
- ✅ Ghost/Replay exposto no Alpha.12 em AVANÇADO com interface localizada em pt-BR/en/es/de/fr;
- ✅ gravação somente com telemetria real local, amostrada pela ferramenta em ~100 ms;
- ✅ parar e salvar em arquivo `.navbrghost` versionado;
- ✅ abrir Ghost existente e inspecionar nome, mapa, veículo, HOF, duração e frames reais antes de reproduzir;
- ✅ controle separado para iniciar e parar a reprodução;
- 🧪 reprodução física Ghost 3D via plugin bridge experimental;
- 🔒 spawn/update/despawn físico permanece opt-in/experimental e falha com diagnóstico quando o bridge não oferece suporte, sem escrita insegura;
- 🧪 reprodução/visualização de viagem no mapa possui base experimental e ainda precisa integração final no fluxo principal;
- 🧪 comparação de tempo/desempenho possui base experimental e ainda precisa integração final no fluxo principal;
- 🚧 compartilhamento de replay quando permitido;
- 🚧 integração futura com eventos e treinamento.

## 17. Mapa web ao vivo

- 🚧 servidor web opcional da sessão;
- 🚧 mapa ao vivo no navegador;
- 🚧 jogadores e veículos relevantes;
- 🚧 linha/destino;
- 🚧 status da sessão;
- 🚧 CCO web somente leitura inicialmente;
- 🚧 autenticação/permissões quando houver controle operacional;
- 🔒 desativado por padrão para preservar privacidade e superfície de rede.

## 18. Eventos e operações especiais

- 🚧 criação de eventos;
- 🚧 comboios/operações programadas;
- 🚧 slots de motorista;
- 🚧 regras de evento;
- 🚧 briefing;
- 🚧 CCO de evento;
- 🚧 resultados/estatísticas opcionais;
- 🚧 modo comunidade/RP.

## 19. Permissões e moderação

- 🚧 admin;
- 🚧 moderador;
- 🧪 CCO;
- 🚧 motorista;
- 🚧 visitante;
- 🚧 espectador;
- 🧪 permissões por ação;
- 🚧 kick/ban da sessão;
- ✅ mute de chat/voz;
- 🚧 logs operacionais mínimos;
- 🚧 controles de privacidade.

## 20. Painel de saúde da sessão

- ✅ ping/latência;
- ✅ jitter;
- ✅ perda estimada de pacotes;
- ✅ frequência/taxa de telemetria indicada;
- 🚧 FPS local quando disponível;
- ✅ estado do plugin;
- ✅ estado do bridge;
- 🚧 fila de comandos físicos;
- ✅ tráfego/veículos remotos sincronizados em métricas agregadas disponíveis;
- 🚧 alertas de incompatibilidade;
- ✅ exportação de diagnóstico sanitizado somente com métricas agregadas, sem senha, token, IP/endereço, PlayerId, sala, caminhos locais ou credenciais.

## 21. SDK / API NavBR

- 🚧 API local versionada;
- 🚧 eventos de telemetria;
- 🚧 eventos multiplayer;
- 🚧 API para dashboards;
- 🚧 API para bots;
- 🚧 API para ferramentas de CCO;
- 🚧 documentação pública;
- 🚧 versionamento/compatibilidade do SDK;
- 🔒 nenhuma API externa pode permitir escrita insegura no OMSI por padrão.

## 22. Workshop / conteúdo comunitário

- 🚧 perfis de ônibus;
- 🚧 perfis de ruas;
- 🚧 perfis de Hardware Cockpit;
- 🚧 layouts de HUD permitidos;
- 🚧 traduções;
- 🚧 configurações de mapa;
- 🚧 metadados de compatibilidade;
- 🚧 importação/exportação;
- 🚧 validação de schema;
- 🚧 não hospedar/republicar assets proprietários sem autorização.

## 23. Site/portal Alpha.12

- ✅ download e catálogo de releases;
- ✅ contador de downloads;
- ✅ documentação e feedback;
- 🚧 página específica da Alpha.12;
- 🚧 matriz de funcionalidades/estado;
- 🚧 compatibilidade conhecida;
- 🚧 status das features experimentais;
- 🚧 navegador de salas quando a infraestrutura online existir;
- 🚧 documentação do Hardware Cockpit;
- 🚧 documentação do SDK;
- 🚧 área comunitária futura.

## 24. Companion/mobile futuro dentro do escopo Alpha.12

- 🚧 definir API para companion;
- 🚧 painel móvel de sessão/CCO;
- 🚧 telemetria somente leitura no celular;
- 🚧 mapa ao vivo;
- 🚧 preparação técnica para eventual Android;
- 🚧 avaliar publicação futura no Google Play somente depois de a base desktop estar estável.

## Ordem interna de implementação

A Alpha.12 contém todo o escopo acima, mas será construída em ondas para evitar regressões:

1. fundação: localização, feature flags, modelos compartilhados e saúde da sessão;
2. navegação: ETA, distância, atraso, ruas e Roadmap Studio;
3. rede: diagnóstico, rate limit, NAT/UPnP, métricas e otimização;
4. multiplayer 3D: tiles, LOD, portas, articulação, matriz e estados;
5. Hardware Cockpit: aliases, persistência, Wi-Fi e displays;
6. perfil do motorista e compatibilidade/mod manifest;
7. salas públicas/privadas e presença global opcional;
8. empresas virtuais e CCO;
9. replay/Ghost, mapa web, eventos e permissões;
10. SDK, workshop e companion.

## Gate de release

A Alpha.12 pode publicar testes intermediários (`alpha.12-test.N`) durante o desenvolvimento. Um módulo pode entrar no pacote marcado como experimental antes de estar apto para uso padrão.

Nenhum recurso experimental deve tornar obrigatório o plugin físico para quem quiser apenas GPS/HUD/multiplayer externo.
