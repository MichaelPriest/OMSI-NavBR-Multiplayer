# Alpha.14 Test 3 — release notes

A **Alpha.14 Test 3** é focada na remodelação completa da Central Multiplayer e na correção do primeiro ciclo funcional de Personagem/RP.

## Central Multiplayer

A Central deixou de usar a experiência genérica anterior e passa a seguir o shell Figma do NavBR.

- **Visão geral** com mapa da sessão como elemento dominante;
- métricas de sala, mapa, jogadores, latência e host;
- painel de jogadores próximos;
- atalhos operacionais;
- **Sala** separada em Criar sala e Entrar em sala;
- **Jogadores** em lista visual com estado, ônibus, linha, velocidade, ping, voz e estado físico;
- **Chat & Voz** separado de configurações técnicas;
- **Personagem / RP** como aba própria;
- **Avançado** reservado para ônibus físico, bridge, firewall, UPnP/NAT e diagnóstico.

A antiga página Multiplayer paralela do shell foi removida. O menu abre diretamente a Central real e não exibe salas fictícias.

## Personagem / RP

- HUD, Central e fluxo automático usam o mesmo `RoleplayCharacterController`;
- removida a antiga janela RP duplicada;
- seleção continua usando os personagens reais de `Map.Drivers`;
- plugin NavBR atualizado para **bridge protocol v3**;
- interop nativo atualizado para **state ABI v3**;
- capacidades RP do plugin são atualizadas após o OMSI terminar de inicializar;
- identificação do motorista usa personagem selecionado + vínculo com o ônibus real do jogador;
- ao ativar RP, o motorista é destacado e colocado ao lado do ônibus;
- ao voltar, NavBR restaura pose, vínculo com o ônibus e estado de IA capturados antes do RP;
- W/S, A/D, Shift e Esc continuam usando o mesmo controlador global.

## Multiplayer

- latência medida pelo próprio hub e publicada como presença real;
- estado de voz habilitado passa a integrar a presença real;
- estados RP remotos continuam separados da telemetria do ônibus;
- mapa da Central continua sem inventar jogadores/posições quando não há dados.

## Simulador de jogadores

Foi adicionado `NavBR.MultiplayerSimulator`, ferramenta exclusiva de desenvolvimento/teste.

Ela pode:

- conectar múltiplos clientes SignalR reais;
- simular ônibus em movimento;
- simular personagens RP com estados Parado / A pé / Correndo;
- testar spawn/update/despawn lógico da sessão;
- verificar automaticamente se o movimento atravessou o servidor;
- fornecer Grid/Tile opcionais para validar marcadores no HUD/minimapa.

O simulador **não é usado pelo app de produção**.

## CI

A validação Alpha.14 agora inclui:

- XAML/site;
- Shared;
- servidor;
- simulador multiplayer;
- teste de jogadores em movimento pelo SignalR;
- interop nativo x86;
- plugin Native AOT x86;
- recriação do bundle de plugin embutido;
- verificação bridge v3 / interop v3;
- cliente WPF x86;
- smoke test do plugin bridge.

## Ainda em desenvolvimento

- câmera dedicada seguindo o personagem;
- ajuste automático de altura em terreno inclinado;
- animações e gestos;
- interação com objetos/ônibus;
- personagem remoto físico completo;
- validação prática do ciclo RP em diferentes ônibus/mapas do OMSI.
