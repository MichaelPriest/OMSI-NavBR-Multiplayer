# Alpha.14 Test 3 — release notes

A Alpha.14 Test 3 concentra a consolidação da Central Multiplayer, do Personagem/RP e da nova interface desktop React/WebView2.

## React/WebView2

- React + TypeScript + Vite hospedado no cliente .NET/WPF x86;
- React passa a ser o shell principal após carregamento confirmado;
- fallback WPF seguro quando WebView2 falha ou quando o usuário solicita;
- Home e Executar OMSI;
- Navegação/GPS com rota, paradas, manobras e ETA reais;
- Central Multiplayer completa;
- CCO, motoristas remotos, ocorrências, Empresa/Frota e Perfil;
- Hardware Cockpit com conexão serial compartilhada;
- Instalações OMSI e perfis de lançamento;
- Diagnóstico/privacidade;
- Rede com Firewall TCP 27730 verificado, listener, NAT/CGNAT, UPnP e teste externo;
- tray abre/oculta o shell React;
- Mapa 3D, HUD e RP continuam nativos.

## Multiplayer

- controlador C# existente compartilhado com o React, sem segundo SignalR;
- criação/entrada/saída de salas;
- sala local TCP 27730;
- salas privadas com senha efêmera;
- diretório público, busca e favoritos;
- avaliação nativa de compatibilidade;
- jogadores, latência, voz, chat e RP reais;
- mapa da sessão somente com posições recentes e compatíveis.

## Personagem / RP

- único \`RoleplayCharacterController\`;
- personagens reais de \`Map.Drivers\`;
- bridge protocol v3 / interop v3;
- vínculo real com ônibus;
- restauração de pose/vínculo/IA ao retornar;
- W/S, A/D, Shift e Esc no controlador global.

## Hardware e rede

- serial centralizada em \`HardwareCockpitBridgeController\`;
- streaming \`NAVBR_HW_V1\` no tick de telemetria a 5 Hz;
- auto-reconnect somente à COM escolhida;
- Firewall aplicado com UAC e verificação;
- NAT/UPnP separados de alcance externo.

## CI

A validação Alpha.14 compila o frontend React antes do cliente e valida Shared/Server, simulador, interop x86, Native AOT, bundle/protocolo v3, cliente Windows x86 e smoke test do Plugin Bridge.

## Ainda experimental

- câmera dedicada de RP;
- terreno inclinado;
- animações/gestos;
- interação física adicional;
- personagem remoto físico completo;
- ônibus remoto físico entre diferentes mapas/modelos.
