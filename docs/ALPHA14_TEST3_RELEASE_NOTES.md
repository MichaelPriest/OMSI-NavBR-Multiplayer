# Alpha.14 Test 3 — release notes

A Alpha.14 Test 3 consolida a Central Multiplayer, o Personagem/RP e a interface desktop React/WebView2. Esta é a prerelease pública de validação antes da promoção da Alpha.14.

## React/WebView2

- React + TypeScript + Vite hospedado no cliente .NET/WPF x86;
- idioma do shell React sincronizado com o `LocalizationService` nativo, com pt-BR, en-US, es-ES, de-DE e fr-FR usando a mesma preferência persistida do app;
- React é o shell principal após carregamento confirmado;
- o layout WPF anterior deixou de ser uma superfície de usuário: `MainWindow` permanece apenas como host técnico em memória;
- o controlador multiplayer WPF é inicializado explicitamente, sem `Show()/Hide()`, e não desenha mais jogadores em `RoadmapCanvas` legado;
- falhas do WebView2 usam o painel de erro da própria janela nova e o tray sempre reabre React;
- Instalações OMSI, HUD e Roadmap Studio não abrem editores WPF antigos; **Executar OMSI** direciona para Instalações React quando nenhum perfil válido existe;
- Home e Executar OMSI;
- Navegação/GPS com rota, paradas, manobras e ETA reais, mais visão 3D integrada ao React usando o roadmap real do mapa;
- Central Multiplayer completa, incluindo salas públicas/privadas, seleção de microfone/saída e mixer por jogador;
- CCO, motoristas remotos, ocorrências, Empresa/Frota e Perfil;
- Hardware Cockpit com conexão serial compartilhada;
- Instalações OMSI e perfis de lançamento completos no React, com seletor nativo de pasta e Explorer;
- HUD configurável no React e aplicado ao vivo pelo store nativo;
- Roadmap Studio no React com análise por tiles e geração vetorial por splines, usando diretamente `OmsiRoadmapGeneratorService` e `OmsiRoadmapVectorGeneratorService`;
- Ghost / Replay no React com gravação, biblioteca/importação, analytics, prévia read-only e replay 3D;
- Diagnóstico/privacidade;
- Rede com Firewall TCP 27730 verificado, listener, NAT/CGNAT, UPnP e teste externo;
- tray abre/oculta o shell React;
- primeiro acesso/onboarding migrado para React e concluído somente após `completeFirstRun`;
- Personagem/RP possui tela React própria e também integra a aba Multiplayer;
- Mapa 3D está integrado à Navegação React; apenas o modo de mover o HUD continua como interação nativa necessária ao overlay OMSI.

## Multiplayer

- controlador C# existente compartilhado com o React, sem segundo SignalR;
- criação/entrada/saída de salas;
- sala local TCP 27730;
- salas privadas com senha efêmera;
- diretório público, busca e favoritos no React;
- avaliação nativa de compatibilidade;
- jogadores, latência, voz, chat e RP reais;
- dispositivos de entrada/saída de áudio e mute/ganho por jogador usam o `VoiceChatService` existente;
- mapa da sessão somente com posições recentes e compatíveis;
- sem render paralelo de jogadores no antigo roadmap WPF.

## Personagem / RP

- único `RoleplayCharacterController`;
- personagens reais de `Map.Drivers`;
- bridge protocol v3 / interop v3;
- vínculo real com ônibus;
- restauração de pose/vínculo/IA ao retornar;
- ativação experimental, catálogo real de `Map.Drivers`, seleção, Sair do ônibus e Retornar ao ônibus diretamente no React;
- HUD e auto-prompt abrem a tela RP React;
- W/S, A/D, Shift e Esc continuam no controlador global nativo.

## Hardware e rede

- serial centralizada em `HardwareCockpitBridgeController`;
- streaming `NAVBR_HW_V1` no tick de telemetria a 5 Hz;
- auto-reconnect somente à COM escolhida;
- Firewall aplicado com UAC e verificação;
- NAT/UPnP separados de alcance externo.

## Segurança de release

- pushes em `test/alpha14-test3` geram somente artefatos privados do GitHub Actions;
- publicação pública usa a branch dedicada `publish/alpha14-test3` ou execução manual explicitamente aprovada;
- o pipeline recompila React, cliente, servidor, plugin e simulador antes de substituir os assets públicos;
- GitHub Pages é atualizado somente depois de publicação aprovada.

## CI

A validação Alpha.14 compila o frontend React antes do cliente e valida Shared/Server, simulador, interop x86, Native AOT, bundle/protocolo v3, cliente Windows x86 e smoke test do Plugin Bridge.

## Ainda experimental

- câmera dedicada seguindo o personagem na visão 3D React já está em validação;
- ajuste experimental de altura por splines reais do OMSI já está em validação, com fallback para preservar Z quando não há geometria confiável;
- animações e gestos;
- interação física adicional com ônibus/objetos;
- personagem remoto físico completo;
- ônibus remoto físico entre diferentes mapas/modelos;
- validação prática do ciclo RP em diferentes ônibus e mapas do OMSI.

## HUD e Roadmap Studio

- presets, temas e ancoragem do HUD usam o catálogo nativo existente;
- escala, largura, altura, opacidade, módulos e escala individual dos widgets são salvos pelo `MultiplayerSettingsStore`;
- o overlay recebe `SettingsSaved` e aplica as mudanças em tempo real;
- o modo Mover HUD continua nativo por depender da interação direta com o overlay do OMSI;
- Roadmap Studio usa a lista real de mapas instalados;
- análise de tiles e montagem de `whole.roadmap.bmp` usam `OmsiRoadmapGeneratorService`;
- geração vetorial usa `OmsiRoadmapVectorGeneratorService` e as splines reais do mapa;
- progresso e resultado são exibidos no React, sem duplicar o algoritmo no frontend.

## Ghost / Replay

- gravação de telemetria local real em cadência de 100 ms usando o `GhostRecorder` existente;
- parada/salvamento produz `.navbrghost` e recarrega os metadados reais;
- biblioteca local mostra somente replays válidos e contabiliza incompatíveis ignorados;
- importação valida o arquivo antes de copiá-lo para a biblioteca;
- analytics reutilizam `GhostReplayAnalyticsCalculator`;
- prévia React usa coordenadas X/Z reais dos frames e não envia comandos ao OMSI;
- playback físico usa `GhostReplayPlayer` e o Plugin Bridge existente, com velocidade de 0,1× a 4× e loop opcional;
- gravação e playback possuem proteções nativas contra concorrência.
