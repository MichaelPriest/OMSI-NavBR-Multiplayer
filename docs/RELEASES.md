# Releases

## Estado atual / Current state

`v0.3.0-alpha.21`

> **Alpha pública de teste.** A Alpha.21 concentra a validação do multiplayer físico, o NavBR TP/TS, rotas reais do HOF, HUD e Mobile Companion v3. O teste **online com bots/AI do simulador seguindo o host foi concluído com sucesso**; o próximo gate é a validação ponta a ponta com **players reais em dois PCs/duas sessões OMSI**.

### Alpha.21

- ônibus simulados físicos seguem a trilha real do host por distância percorrida;
- ancoragem prioriza vehicle paths das splines e paths dirigíveis de cruzamentos;
- removido o fallback circular do simulador físico;
- NavBR TP/TS com linha, TP/TS, pontualidade, passageiros, temperatura e hora/data OMSI;
- lista real de rotas do HOF do ônibus atual;
- HUD com autostart no runtime React e `Ctrl+Alt+H`;
- nome do jogador acima do ônibus físico e marcadores atualizados;
- Mobile Companion v3 com aba Operação e controles do HUD/NavBR TP/TS;
- APK Android, plugin Native AOT x86, simulador, installer e demais pacotes continuam incluídos.

### Validação concluída nesta etapa

- teste online do simulador/AI validado com ônibus físicos seguindo o host em OMSI real;
- pipeline simulador → rede → cliente → plugin → RoadVehicle confirmado operacional;
- correções de ownership/matrizes e escopo do HUD incorporadas ao código promovido para `main`.

### Ainda em validação na Alpha.21

- comportamento físico em mapas/add-ons variados;
- permanência perfeita na faixa em cruzamentos e assets com paths incompletos;
- teste ponta a ponta com **players reais** em duas sessões OMSI/PCs independentes;
- ônibus articulados no backend físico.

Release: https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.21

### Alpha.20

- instalador e EXE Windows x86;
- APK Android NavBR Mobile Companion Alpha 2;
- interface desktop reorganizada por grupos funcionais;
- idioma e preferências gerais em **Configurações → Geral**;
- HUD com categorias e workspace: Escolher HUD, Aparência, Módulos e Posição & ações;
- prévia de HUD no app/overlay sem persistir antes de Aplicar;
- Home e Multiplayer reorganizados por fluxo;
- Roadmap Studio com comparação OMSI × NavBR HD;
- minimapa HD 2× e Ultra 3× com limite seguro de 8192 px;
- hot-reload de roadmap no HUD e Navegação 2D/3D;
- verificador/reparo automático do plugin e Plugin Bridge Native AOT x86;
- simulador multiplayer incluído.

### Ainda em validação

- LAN/local entre dois PCs reais em diferentes ambientes;
- Servidor NavBR online/Host pela Internet em redes reais distintas;
- ônibus remoto físico e RP físico remoto;
- compatibilidade do painel IBIS com a variedade de ônibus/add-ons do OMSI;
- comandos de IBIS que dependam de variáveis/stringvars específicas do veículo.

Release: https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/v0.3.0-alpha.20

## English

Alpha.20 is a public test prerelease focused on a clearer desktop information architecture, a dedicated HUD workspace, in-app/live HUD preview, and HD/Ultra minimap generation with Roadmap Studio comparison. The Android Mobile Companion Alpha 2 remains included.

LAN/online multiplayer, remote physical buses/RP and broad bus/add-on IBIS compatibility still require real-world testing.

## Pacotes / Packages

- Windows x86 installer — recommended;
- standalone Windows x86 EXE;
- Windows client ZIP;
- Android APK — NavBR Mobile Companion Alpha 2;
- dedicated Windows x64 server;
- OMSI x86 plugin;
- multiplayer simulator;
- documentation and SHA256SUMS.

## Histórico

- [Alpha.19](ALPHA19_RELEASE_NOTES.md) — Mobile Companion Alpha 2 e validações anteriores;
- [Alpha.18](ALPHA18_RELEASE_NOTES.md) — histórico público anterior.
