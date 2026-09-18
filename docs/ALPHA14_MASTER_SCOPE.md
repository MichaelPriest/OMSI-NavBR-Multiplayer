# Alpha.14 — escopo mestre

Versão de validação pública: **v0.3.0-alpha.14-test.3**.

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

- peer-host TCP 27730;
- servidor dedicado opcional;
- salas públicas/privadas;
- chat e voz;
- dispositivos de áudio e mixer por jogador;
- UPnP opcional;
- relay experimental;
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
- herda linha, rota, destino e próxima parada;
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
- ajuste experimental de altura em terreno inclinado usando Z, gradiente e `delta_h` reais das splines OMSI; quando não há geometria confiável próxima, a altura atual é preservada.

### Próximas funções

- animações e gestos;
- interação com ônibus/objetos;
- personagem remoto físico completo.

## 5. Multiplayer físico

- spawn/update/despawn experimental;
- pose/quaternion nativos;
- velocidade/luzes/setas quando suportadas;
- compatibilidade antes da escrita;
- resolução de asset remoto por fingerprint SHA-256;
- lifecycle/erro físico exposto por jogador no React;
- consist/multi-veículo detectado pelo conjunto real de `RoadVehicle` criado pelo OMSI; enquanto ownership/ordem/transforms das partes não forem verificáveis, todas as partes criadas são removidas fail-safe e a quantidade detectada é exposta no diagnóstico;
- **articulados ainda não são considerados suportados**;
- opt-in obrigatório.

## 6. Hardware Cockpit

- protocolo `NAVBR_HW_V1`;
- uma conexão serial compartilhada;
- streaming nativo a 5 Hz;
- COM/baud persistidos;
- reconexão somente à mesma COM.

## 7. Release e validação

- `test/alpha14-test3`: build privada em GitHub Actions, sem alterar release;
- `publish/alpha14-test3`: publicação explicitamente aprovada da prerelease;
- toda publicação recompila/valida React, servidor, plugin, cliente e simulador;
- publicação final da Alpha.14 depende da validação prática OMSI.

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
