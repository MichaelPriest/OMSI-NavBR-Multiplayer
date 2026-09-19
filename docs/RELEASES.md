# Releases

O OMSI NavBR Multiplayer usa SemVer e GitHub Actions para publicar prereleases e releases.

## Estado atual / Current state

```text
v0.3.0-alpha.17
```

Release:

https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.17

### Português (pt-BR)

A Alpha.17 concentra a correção do runtime físico/RP observado em OMSI real:

- o plugin Native AOT agora reconhece o runtime 2.3.004 pelo `logfile.txt`, igual ao cliente, quando o FileVersion do `Omsi.exe` está desatualizado;
- Plugin Bridge passa a informar a versão completa/informacional da build;
- atualização do plugin pode ficar agendada enquanto o OMSI está aberto e é aplicada quando `Omsi.exe` fecha;
- a Visão geral do Multiplayer usa o roadmap real e desenha ônibus/personagens remotos recebidos pela sessão;
- o RP oferece `Motorista atual (OMSI)` quando o catálogo Map.Drivers está vazio, resolvendo o humano real do ônibus sem criar NPC fake;
- rodapé React usa a versão real da build, sem texto Alpha hardcoded;
- permanecem as correções de PhysicalGrid/NavGrid, retorno à rota e simulador da Alpha.16.

### English (en)

Alpha.17 focuses on the physical/RP runtime issue observed in real OMSI:

- the Native AOT plugin now recognizes OMSI 2.3.004 from `logfile.txt`, matching the desktop client when `Omsi.exe` FileVersion metadata is stale;
- Plugin Bridge reports the full/informational build version;
- a required plugin update can be deferred while OMSI is running and applied after `Omsi.exe` exits;
- Multiplayer Overview uses the real roadmap and renders remote buses/roleplay characters received from the session;
- RP exposes `Current driver (OMSI)` when Map.Drivers is empty, resolving the real seated human without creating a fake NPC;
- the React footer uses the actual build version instead of a hardcoded Alpha label;
- Alpha.16 PhysicalGrid/NavGrid, route-rejoin and simulator fixes remain included.

Physical vehicle injection and native Character/RP are still prerelease features and require real-OMSI validation.

## Pacotes / Packages

O workflow publica:

- `OMSI-NavBR-Multiplayer-v0.3.0-alpha.17-win-x86.exe`;
- `OMSI-NavBR-Multiplayer-v0.3.0-alpha.17-win-x86.zip`;
- `OMSI-NavBR-Server-v0.3.0-alpha.17-win-x64.zip`;
- `OMSI-NavBR-Plugin-v0.3.0-alpha.17-win-x86.zip`;
- `OMSI-NavBR-Multiplayer-Simulator-v0.3.0-alpha.17-win-x64-dev.zip`.

O EXE standalone x86 é a opção recomendada para teste do cliente.

## Histórico recente / Recent history

- `v0.3.0-alpha.17` — runtime OMSI 2.3.004 unificado, reparo do plugin, mapa multiplayer real e fallback do motorista atual;
- `v0.3.0-alpha.16` — mapa 2D multiplayer, retorno à rota, probes separados de RP/ônibus físico e simulator PhysicalGrid;
- `v0.3.0-alpha.15` — consolidação de runtime físico, RP, plugin updater e Portal V2;
- `v0.3.0-alpha.14` — Alpha.14 pública.

## Portal

https://michaelpriest.github.io/OMSI-NavBR-Multiplayer/

## Créditos / Credits

**Desenvolvedor / Developer:** MichaelPriest  
**Apoio ao desenvolvimento / Development assistance:** IA ChatGPT
