# Alpha.13 Test 1 — release notes

## Marco da versão

A Alpha.13 Test 1 inaugura a fase de **multiplayer físico** do NavBR.

O objetivo desta pré-release é validar em 2+ PCs que os jogadores da mesma sala conseguem aparecer como ônibus físicos dentro do OMSI 2.

## Incluído

- fluxo online real SignalR → cliente → plugin → interop OMSI;
- coordenador físico único no serviço multiplayer;
- spawn/update/despawn experimental;
- pose nativa `LocalX/Y/Z` e quaternion do OMSI 2.3.004;
- velocidade;
- luzes e setas básicas suportadas pelo backend atual;
- limpeza em saída, reconexão e encerramento da sessão;
- Central Multiplayer pode ser fechada sem encerrar a sessão ativa;
- GPS/HUD, perfil, histórico, empresa, Company Network, CCO, voz e Hardware Cockpit preservados;
- base de entitlement preparada para distribuição comercial futura, sem bloqueio durante Alpha/Beta.

## Não incluído ainda

- portas;
- matriz/linha/destino física;
- articulação;
- animações específicas por veículo;
- sincronização física completa de tráfego IA.

## Downloads do portal

A partir desta fase o portal passa a exibir:

- downloads de cada release individual;
- downloads acumulados da Alpha atual, somando todas as Test builds;
- downloads totais do projeto, incluindo pré-releases públicas.

Isso corrige a regra anterior que excluía tags `-test` do total agregado.

## Próximo passo

O resultado do teste real de spawn/movimento/estabilidade define a Test 2. Portas, matriz e articulação só avançam depois que o ciclo físico básico estiver validado.
