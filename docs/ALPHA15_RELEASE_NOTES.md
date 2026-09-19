# Alpha.15 — notas da versão / Alpha.15 — release notes

Versão / Version: **v0.3.0-alpha.15**

## Português (pt-BR)

A Alpha.15 promove para uma nova série pública o conjunto de correções e consolidações validado após a Alpha.14 Test 6.

### Principais mudanças

- ônibus físico remoto resolve a **Kachel local** a partir de `GridX/GridY`; o índice de Kachel de outro OMSI não é mais reutilizado;
- state interop sobe para **ABI v7** com `NavBR_ResolveMapTileIndex`;
- `Tacho` e `Groundspeed` usam as unidades nativas corretas do OMSI;
- mensagens reais do backend físico chegam à interface React;
- falhas de spawn registram resultados nativos de `MakeVehicle`;
- RP só libera a posse após confirmar a restauração do motorista ao ônibus;
- updater do plugin identifica bundle atual por SHA-256;
- seleção de pasta, `Omsi.exe`, `.lnk` e `.url` converge para a mesma instalação preferida;
- Navegação 2D/3D reforça roadmap real, fallback PNG e diagnóstico de renderização;
- Portal V2 reorganiza downloads, todas as versões, roadmap, documentação, Pix e publicidade.

### Ainda experimental

- criação e movimento de ônibus físicos dentro do OMSI;
- Personagem/RP físico;
- Ghost 3D;
- escrita de memória/interop nativo.

### Validação automática

Passaram: build Windows x86, interop C++ x86, Native AOT x86, exports, instalação/remoção do plugin, bundle embutido, cliente, standalone e handshake do Plugin Bridge.

A validação automática não substitui dois PCs/duas sessões OMSI reais.

## English (en)

Alpha.15 promotes the post-Alpha.14 integration fixes into a new public alpha series.

### Main changes

- remote physical buses resolve the **local Kachel** from `GridX/GridY`; another OMSI process' Kachel index is no longer reused;
- state interop moves to **ABI v7** with `NavBR_ResolveMapTileIndex`;
- `Tacho` and `Groundspeed` are written using OMSI's native units;
- physical-backend error details reach the React UI;
- spawn failures include native `MakeVehicle` diagnostics;
- Character/RP only releases ownership after driver restoration is confirmed;
- plugin update detection uses the embedded bundle SHA-256;
- folder, `Omsi.exe`, `.lnk` and `.url` selection all converge on the preferred OMSI installation;
- 2D/3D Navigation strengthens real-roadmap loading, PNG fallback and rendering diagnostics;
- Portal V2 reorganizes downloads, all versions, roadmap, documentation, Pix support and advertising.

### Still experimental

Physical bus injection, physical Character/RP, Ghost 3D and native OMSI writes remain experimental and opt-in.
