# OMSI NavBR Multiplayer v0.3.0-alpha.21

## Português (Brasil)

A Alpha.21 é uma **alpha pública de teste** focada no multiplayer físico dentro do OMSI, no novo **NavBR TP/TS**, no Mobile Companion e na robustez do HUD.

### Multiplayer físico

- materialização de ônibus remotos via `MakeVehicle` com validação real do modelo renderizável;
- correção da camada `ComplObjInst` para manter o ônibus físico visível;
- comboio do simulador segue a trilha real do ônibus local com espaçamento por distância percorrida;
- removido o fallback de órbita no modo físico;
- simulados não são materializados antes de existir telemetria real do host;
- falhas transitórias de readback/tile não causam mais despawn/respawn imediato;
- ônibus simulados aguardam uma âncora viária confiável quando o path não está disponível;
- resolução da pista prioriza **vehicle paths tipo 0 das splines .sli**, incluindo offset lateral da faixa;
- cruzamentos usam paths dirigíveis dos objetos de cenário quando disponíveis;
- seleção de path considera direção da trajetória para reduzir troca de rua em interseções;
- logs `physical-road-target` mostram a origem da âncora usada: `vehicle-path`, `scenery-vehicle-path` ou fallback.

### NavBR TP/TS

- novo painel operacional próprio do NavBR;
- substitui a nomenclatura de desenvolvimento usada anteriormente;
- linha, TP/TS automático/manual, hora/data da simulação, temperatura interna, passageiros, pontualidade e velocidade;
- leitura real das variáveis expostas pelo ônibus/OMSI, sem preencher dados inexistentes;
- tecla **K** para configurar operação;
- `Ctrl+Alt+F6` mostra/oculta o NavBR TP/TS;
- `Ctrl+Alt+F7` alterna tema;
- `Ctrl+Alt+F8` alterna tamanho;
- lista real de rotas do **HOF carregado pelo ônibus**, baseada nos blocos `[infosystem_trip]`;
- seleção de rota no painel/mobile preenche a linha operacional correspondente;
- quando o HOF não fornece rotas compatíveis, o painel permanece vazio em vez de inventar dados.

### HUD

- inicialização do HUD desvinculada da antiga janela WPF;
- HUD inicia junto com o runtime React;
- `Ctrl+Alt+H` liga/desliga toda a sobreposição;
- atalho funciona em fullscreen validando o PID real do OMSI em primeiro plano;
- lifecycle do overlay consolidado para evitar hooks/timers duplicados;
- novo marcador de jogador;
- nome do jogador acima do ônibus remoto físico.

### Mobile Companion Alpha 2

- contrato mobile v3;
- nova aba **Operação**;
- temperatura interna, passageiros, horário/data OMSI, pontualidade, linha/rota/terminal;
- mostrar/ocultar HUD do PC;
- mostrar/ocultar NavBR TP/TS;
- tema e tamanho do painel;
- TP/TS automático/manual;
- linha manual;
- lista das rotas reais do HOF;
- cache PWA versionado e limpeza segura somente dos caches NavBR.

### Validação

A Alpha.21 passa pelos gates de React desktop, PWA, APK Android, .NET, Native AOT x86, exports OMSI, Plugin Bridge, simulador SignalR, installer/uninstaller e pacotes de integração.

### Limitações conhecidas

O multiplayer físico continua experimental. O NavBR usa a geometria dos vehicle paths reais do mapa para manter os remotos na pista, mas não escreve estruturas internas `PathInfo` não documentadas do OMSI. Testes reais em mapas, cruzamentos e ônibus diferentes continuam necessários. Ônibus articulados ainda não são considerados suportados pelo caminho físico experimental.

## English

Alpha.21 is a public test prerelease focused on physical multiplayer, the new **NavBR TP/TS** operational panel, Mobile Companion v3, and HUD robustness.

Physical simulator buses now follow the host's travelled path by distance and are anchored to real OMSI vehicle paths where available, including lane offsets and intersection paths. The old orbit fallback is disabled in physical verification mode.

NavBR TP/TS provides operational line/direction data, simulation time/date, cabin temperature, passenger count, punctuality and speed, plus the actual HOF route list exposed by the current bus.

The Android/PWA companion gains the Operation page and remote HUD/TP-TS controls. Physical multiplayer remains experimental and still requires broad real-world validation.
