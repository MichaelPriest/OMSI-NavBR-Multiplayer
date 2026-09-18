# Alpha.14 — escopo mestre

Versão pública: **v0.3.0-alpha.14**.

A Alpha.14 consolida a interface React/WebView2, multiplayer físico experimental, Personagem/RP e ferramentas operacionais.

## 1. Interface

- React + TypeScript + Vite em WebView2 como shell principal;
- host .NET/WPF x86 preservado;
- fallback WPF seguro;
- Home e Executar OMSI;
- Navegação/GPS com rota real;
- Central Multiplayer;
- CCO, Empresa/Frota, Perfil;
- Hardware Cockpit;
- Instalações OMSI;
- Diagnóstico e Rede;
- Mapa 3D, HUD e RP continuam nativos;
- interface pt-BR, English, Español, Deutsch e Français.

## 2. Multiplayer

- peer-host TCP 27730;
- servidor dedicado opcional;
- salas públicas/privadas;
- chat e voz;
- UPnP opcional;
- relay experimental;
- Firewall verificável em todos os perfis;
- diagnóstico separado de listener, NAT/CGNAT, UPnP e probe externo;
- presença, telemetria, mapa e estado operacional via SignalR.

## 3. Simulador

Somente desenvolvimento/teste:
- reutiliza host ou inicia servidor empacotado;
- senha privada;
- herda mapa/compatibilidade;
- aguarda telemetria real;
- bots próximos ao host;
- herda linha, rota, destino e próxima parada;
- \`--verify\` exige movimento e mapa consistente.

## 4. Personagem / RP

- personagens reais;
- funciona sem multiplayer;
- bridge/plugin v3;
- entrada/saída e restauração de vínculo/IA;
- W/S, A/D, Shift e Esc;
- estado RP separado da telemetria do ônibus.

## 5. Multiplayer físico

- spawn/update/despawn experimental;
- pose/quaternion nativos;
- velocidade/luzes/setas quando suportadas;
- compatibilidade antes da escrita;
- opt-in obrigatório.

## 6. Hardware Cockpit

- protocolo NAVBR_HW_V1;
- uma conexão serial compartilhada;
- streaming nativo a 5 Hz;
- COM/baud persistidos;
- reconexão somente à mesma COM.

## 7. Critério da Alpha pública

1. React, cliente, servidor, plugin e bridge compilando;
2. shell React abre com fallback seguro;
3. funções principais acessíveis;
4. peer-host/servidor funcionando;
5. simulador no mesmo mapa/operação;
6. regressões zero em HUD/telemetria;
7. RP e ônibus físico fail-safe;
8. Firewall/NAT/UPnP apresentados sem falsa equivalência com alcance externo.
