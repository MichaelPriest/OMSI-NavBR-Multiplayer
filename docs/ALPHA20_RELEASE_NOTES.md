# OMSI NavBR Multiplayer v0.3.0-alpha.20

## Português (Brasil)

A Alpha.20 é uma **alpha pública de teste** focada em organização da interface, personalização do HUD e qualidade visual do minimapa, mantendo C#/OMSI como autoridade dos dados reais.

### Interface reorganizada

- barra lateral dividida em **Principal**, **Multiplayer & RP**, **Operação & Ferramentas** e **Sistema**;
- Home com atalhos agrupados em **Viagem**, **Online & RP** e **Ferramentas**;
- idioma movido para **Configurações → Geral**;
- Configurações divididas em **Interface**, **OMSI & Mapas**, **Conectividade** e **Sistema**;
- a antiga aba Avançado de Configurações foi removida porque duplicava atalhos; as preferências reais restantes foram movidas para Geral;
- Multiplayer agora organiza as telas em **Sessão**, **Comunicação & RP** e **Sistema**.

### HUD

- galeria de presets filtrada por finalidade: Operação, Direção, Multiplayer, Clássicos e Legados;
- workspace em quatro áreas: **Escolher HUD**, **Aparência**, **Módulos** e **Posição & ações**;
- barra persistente de **Prévia / Aplicar / Reset**;
- prévia ao vivo no overlay quando o OMSI está aberto;
- prévia dentro do próprio app quando o OMSI está fechado, usando estados neutros e sem inventar telemetria;
- Move HUD continua separado e persiste a posição personalizada.

### Roadmap / minimapa

- Roadmap Studio mostra comparação **Roadmap OMSI × NavBR HD**;
- textura NavBR HD é separada e **não substitui** o `whole.roadmap.bmp` original;
- **HD 2×:** 440 px/tile;
- **Ultra 3×:** 660 px/tile para mapas pequenos/médios;
- limite máximo de 8192 px para controlar memória;
- resolução estimada exibida antes da geração;
- roadmap gerado é recarregado imediatamente no HUD e na Navegação 2D/3D.

### Pacotes

A release pública inclui instalador/EXE/ZIP Windows x86, Mobile Companion Alpha 2 APK, servidor dedicado, plugin OMSI Native AOT x86, simulador multiplayer, documentação e SHA256SUMS.

### Limitações conhecidas

Multiplayer LAN/online ainda precisa de validação ponta a ponta mais ampla entre PCs/sessões OMSI reais. Ônibus remoto físico, RP físico, controles locais do ônibus e integrações IBIS permanecem experimentais quando aplicável.

## English

Alpha.20 is a public test prerelease focused on clearer desktop information architecture, HUD customization and minimap quality while keeping C#/OMSI authoritative for real runtime data.

The desktop UI is grouped by workflow, language moves to Settings → General, Multiplayer is grouped by purpose, and the HUD editor becomes a four-section workspace. HUD preview works inside the app when OMSI is closed and on the real overlay when OMSI is running, without fabricated telemetry.

Roadmap Studio now compares the original OMSI roadmap with a NavBR-only HD texture. HD uses 440 px/tile and Ultra uses 660 px/tile, capped at 8192 px, and generated roadmaps hot-reload into HUD and 2D/3D navigation.

LAN/online multiplayer and physical OMSI integrations still require broader real-world validation.
