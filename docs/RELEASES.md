# Releases

## Estado atual / Current state

`v0.3.0-alpha.19`

> **Alpha pública de teste.** O Mobile Companion Alpha 2 passa a ser distribuído publicamente junto do cliente Windows. O multiplayer LAN/local e online e os controles físicos/IBIS ainda exigem validação em uma variedade maior de PCs, ônibus e redes reais.

### Alpha.19

- instalador e EXE Windows x86;
- **APK Android NavBR Mobile Companion Alpha 2**;
- descoberta automática do NavBR na mesma rede LAN/Wi-Fi;
- GPS e telemetria real no celular;
- multiplayer, lista/mapa de jogadores, voz, mixer e PTT pelo celular;
- IBIS com visor estilo cockpit e teclas associadas somente a `[mouseevent]` reais detectados no ônibus;
- controles do ônibus pelo celular continuam experimentais, desligados por padrão e exigem `local-vehicle-trigger`;
- verificador/reparo automático do plugin e Plugin Bridge Native AOT x86;
- simulador multiplayer incluído.

### Ainda em validação

- LAN/local entre dois PCs reais em diferentes ambientes;
- Servidor NavBR online/Host pela Internet em redes reais distintas;
- ônibus remoto físico e RP físico remoto;
- compatibilidade do painel IBIS com a variedade de ônibus/add-ons do OMSI;
- comandos de IBIS que dependam de variáveis/stringvars específicas do veículo.

Release: https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.19

## English

Alpha.19 is a public test prerelease and adds the **NavBR Mobile Companion Alpha 2 Android APK** to the public release. The phone app uses real desktop/OMSI state, automatic same-LAN discovery, navigation, vehicle telemetry, multiplayer, voice/PTT and a cockpit-style IBIS panel. Vehicle/IBIS writes remain experimental and are restricted to detected and revalidated real vehicle `[mouseevent]` triggers.

LAN/online multiplayer, remote physical buses/RP and broad bus/add-on IBIS compatibility still require real-world testing.

## Pacotes / Packages

- Windows x86 installer — recommended;
- standalone Windows x86 EXE;
- Windows client ZIP;
- **Android APK — NavBR Mobile Companion Alpha 2**;
- dedicated Windows x64 server;
- OMSI x86 plugin;
- multiplayer simulator;
- documentation and SHA256SUMS.
