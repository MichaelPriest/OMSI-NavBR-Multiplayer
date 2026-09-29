# OMSI NavBR Multiplayer v0.3.0-alpha.23

## Português (Brasil)

A Alpha.23 inaugura a linha pública de teste da nova arquitetura **WinUI 3 x64 + RuntimeHost x86 + plugin OMSI Native AOT x86**.

### O que mudou

- app principal WinUI 3 x64;
- RuntimeHost x86 oculto para OMSI 2;
- plugin Native AOT x86 preservando compatibilidade real com OMSI;
- IPC local por Named Pipe;
- Performance Bridge com governador adaptativo, backpressure e métricas;
- snapshots WinUI escopados à página ativa;
- menos locks, P/Invokes e alocações no plugin físico e no RP;
- telemetria x86 com cache de identidade, Kachel, mapa e trip;
- leitura de processo reutiliza o handle existente;
- tráfego AI filtra distância antes de identidade/arquivo;
- Hardware Cockpit evita trabalho quando inativo;
- tempo da leitura de telemetria exposto na WinUI.

### Pacote

O instalador recomendado é:

`OMSI-NavBR-Multiplayer-v0.3.0-alpha.23-Setup-win-x64.exe`

Ele contém o shell WinUI x64, o RuntimeHost x86, o plugin OMSI x86 e o simulador.

### Validação automática obrigatória

A release só é publicada se o mesmo commit tiver `build.yml` verde, incluindo:

- Native AOT x86 e exports do interop;
- smoke de instalação/remoção do plugin;
- build do RuntimeHost x86;
- build WinUI x64;
- Plugin Bridge;
- smoke XAML;
- WinUI x64 -> RuntimeHost x86 IPC;
- simulador multiplayer local;
- instalador x64;
- instalação/desinstalação silenciosas.

### Ainda experimental

Esta é uma alpha de teste. Ainda precisamos validar principalmente:

1. dois PCs / dois OMSI reais em A <-> B;
2. movimento físico contínuo durante condução real;
3. troca de Kachel, curvas e cruzamentos;
4. reconexão e despawn;
5. luzes/setas;
6. FPS e microtravadas;
7. RP real;
8. Empresa/CCO/crachá;
9. HUD em fullscreen/pause;
10. Hardware Cockpit serial real.

Ônibus articulados continuam fora do conjunto físico considerado validado.

## English

Alpha.23 is the public test line for the new WinUI 3 x64 desktop architecture while preserving the x86 OMSI RuntimeHost and Native AOT x86 plugin. It focuses on lower callback/polling overhead, physical-vehicle stability, scoped WinUI state, RP hot-path reductions, telemetry caching and a complete x64 installer.

Real two-PC/two-OMSI physical-player validation is still required.
