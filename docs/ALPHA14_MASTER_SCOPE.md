# Alpha.14 — escopo mestre

Versão pública e atual: **v0.3.0-alpha.14-test.5**.

A Alpha.14 consolida a interface React/WebView2, multiplayer físico experimental, Personagem/RP e ferramentas operacionais.

## 1. Interface

- React + TypeScript + Vite em WebView2 como shell principal;
- host .NET/WPF x86 preservado apenas como camada técnica/fallback;
- `MainWindow` antigo não é exibido;
- controlador multiplayer nativo inicializa sem `Show()/Hide()`;
- Home e Executar OMSI;
- Navegação/GPS com rota real e visão 3D;
- Central Multiplayer;
- CCO, Empresa/Frota, Perfil e histórico;
- Hardware Cockpit;
- Instalações OMSI;
- Diagnóstico e Rede;
- Ghost / Replay;
- HUD configurável no React, com overlay OMSI nativo;
- onboarding/primeiro acesso React;
- interface pt-BR, English, Español, Deutsch e Français;
- site público também deve usar React, compartilhando o mesmo princípio de componentes e estado real de releases.

## 2. Multiplayer

- três modos explícitos: **Servidor NavBR oficial**, **LAN** e **Online através do Host**;
- TCP 27730 nos modos hospedados pelo PC;
- servidor NavBR dedicado no Render para o modo oficial;
- salas públicas/privadas;
- chat e voz;
- dispositivos de áudio e mixer por jogador;
- UPnP opcional e não bloqueante no Online através do Host;
- timeout de 8 segundos para descoberta/mapeamento UPnP;
- Firewall verificável em todos os perfis;
- diagnóstico separado de listener, NAT/CGNAT, UPnP e probe externo;
- presença, telemetria, mapa e estado operacional via SignalR;
- sem mapa/marcadores paralelos no layout WPF retirado.

## 3. Simulador

Somente desenvolvimento/teste:
- reutiliza host ou inicia servidor empacotado;
- senha privada;
- herda mapa/compatibilidade;
- aguarda telemetria real;
- bots próximos ao host;
- usa mapa/posição/Kachel reais da autoridade da sala;
- bots de veículo podem usar rotas independentes do HOF;
- prefere o MAN EN92 rígido padrão do OMSI para teste físico quando instalado;
- `--verify-physical` exige confirmação dos IDs exatos materializados por `MakeVehicle` no OMSI;
- `--verify` exige movimento e mapa consistente.

## 4. Personagem / RP

- personagens reais;
- funciona sem multiplayer;
- bridge/plugin v3;
- entrada/saída e restauração de vínculo/IA;
- W/S, A/D, Shift e Esc;
- estado RP separado da telemetria do ônibus.

### Funções RP já em validação

- câmera dedicada seguindo o personagem na visão 3D React;
- projeção do personagem no mesmo espaço mundial do roadmap;
- ajuste experimental de altura em terreno inclinado usando Z, gradiente e `delta_h` reais das splines OMSI; quando não há geometria confiável próxima, a altura atual é preservada;
- telemetria de animação com proveniência explícita: `AIMode`, `AIModeEx`, `AISubMode`, Soll/Act speed, `LastMovedDist` e `State` são relidos após o controle RP, mas são valores dirigidos pelo próprio NavBR e **não contam como validação independente**;
- `Activity_Leg`, `Activity_Arm_Umbrella`, `Activity_Arm_KI` e `Activity_Head_KI` são observados sem escrita do NavBR e permanecem **raw**;
- o cliente conta amostras e transições de `Activity_*`, inclusive quantas ocorreram enquanto havia movimento comandado, sem atribuir significado de gesto antes da validação prática.

### Próximas funções

- animações/gestos adicionais somente após os estados necessários serem documentados e validados em runtime;
- interação com objetos externos ao ônibus;
- personagem remoto físico completo.

## 5. Multiplayer físico

- spawn/update/despawn experimental;
- pose/quaternion nativos;
- interpolação adaptativa de posição/quaternion/velocidade no thread do OMSI, com alvo de até ~60 Hz;
- proteção contra frames fora de ordem e snap seguro para teleportes/grandes gaps;
- tolerância curta a falhas transitórias de update para evitar despawn/respawn desnecessário;
- culling físico por distância com histerese (spawn até 750 m, despawn acima de 1 km), mantendo jogadores distantes na sessão sem criar objetos físicos desnecessários;
- remoção automática de ônibus físico órfão após 5 s sem novos alvos, preservando ownership quando o OMSI rejeitar a limpeza;
- throughput seguro da fila física: 1 comando arbitrário/pesado + até 4 updates leves adicionais por frame do OMSI;
- prioridade dinâmica de capacidade pelos ônibus mais próximos, com margem de 75 m, uma troca por vez e cooldown de 2 s;
- telemetria read-only adicional para acelerador, freio, combustível e estados visuais já validados no perfil;
- smoothing ajustado pela cadência dos timestamps remotos para reduzir pausa entre alvos;
- velocidade/luzes/setas quando suportadas;
- compatibilidade antes da escrita;
- resolução de asset remoto por fingerprint SHA-256;
- lifecycle/erro físico exposto por jogador no React;
- topologia declarada do consist é inspecionada no processo desktop a partir de `[couple_front]` / `[couple_back]` reais do `.bus/.ovh`, seguindo apenas arquivos locais contidos em `Vehicles` e com limites de tamanho/profundidade;
- quando o addon declara mais de uma parte, o spawn físico é bloqueado **antes** de chamar o plugin e o React mostra a quantidade esperada;
- como segunda proteção, consist/multi-veículo também é detectado pelo conjunto real de `RoadVehicle` criado pelo OMSI; se a inspeção do arquivo não antecipar o caso, todas as partes criadas são removidas fail-safe e a quantidade observada é exposta no diagnóstico;
- **articulados ainda não são considerados suportados**: ownership, ordem e transforms das partes precisam ser verificáveis antes de qualquer controle físico;
- opt-in obrigatório.

## 6. Hardware Cockpit

- protocolo `NAVBR_HW_V1`;
- uma conexão serial compartilhada;
- streaming nativo a 5 Hz;
- COM/baud persistidos;
- reconexão somente à mesma COM.

## HUD in-game — estilos adicionais

- o HUD atual foi preservado como **Normal (HUD atual)** / tema **Atual / NavBR Clássico**;
- os presets antigos continuam disponíveis e retornam à aparência legada do HUD;
- novos estilos são opt-in e usam somente telemetria real já disponível no cliente:
  - **RP Urbano** — cards compactos e vidro escuro, inspirado em padrões de HUD de jogos RP/open-world;
  - **Route Advisor** — foco em linha, destino, próxima parada e navegação, inspirado em simuladores rodoviários;
  - **Corrida Minimal** — velocidade em destaque e baixa obstrução, inspirado em padrões de telemetria de jogos de corrida;
  - **Transit Pro** — foco operacional de ônibus, atraso, parada solicitada e estados do veículo;
- nomes, paletas, componentes e composição são originais do NavBR; não são copiados logos, assets ou layouts proprietários de outros jogos;
- o React atua como configurador; a exibição in-game continua no HUD nativo/overlay;
- presets podem definir escala, largura, opacidade e módulos, mas continuam consumindo estado real do OMSI/NavBR.

## 7. Release e validação

- **v0.3.0-alpha.14-test.5** é a release pública e versão atual da Alpha.14;
- hotfix público `29f30b2` corrige o travamento ao iniciar **Online através do Host**, movendo UPnP para segundo plano;
- a publicação foi validada por `build`, `alpha14 validation` e compatibilidade do workflow legado antes da promoção para `main`;
- toda publicação recompila/valida React, servidor, plugin, cliente e simulador;
- recursos físicos/RP continuam experimentais, opt-in e fail-safe.

## 8. Critério da Alpha pública

1. React, cliente, servidor, plugin e bridge compilando;
2. shell React abre com fallback seguro;
3. funções principais acessíveis sem ressurgimento de layout WPF;
4. peer-host/servidor funcionando;
5. simulador no mesmo mapa/operação;
6. regressões zero em HUD/telemetria;
7. RP e ônibus físico fail-safe;
8. Firewall/NAT/UPnP apresentados sem falsa equivalência com alcance externo;
9. site público servido pelo build React.
