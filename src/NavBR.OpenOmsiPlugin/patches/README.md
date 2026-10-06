# openOMSI host patches

These patches are reference patches against the current openOMSI architecture. Apply them
in this order when building an enhanced openOMSI host for NavBR:

1. `openomsi-navbr-overlay-loader-v2.patch`
   - detects `OpenOmsiGetOverlayFrameV2` with `libloading`;
   - exposes raw UTF-8 frames from local/in-process plugins only.

2. `openomsi-navbr-overlay-app-state-v2.patch`
   - parses the v2 JSON envelope with `serde_json`;
   - caches only newer timestamps;
   - separates `Overlay2D` from `WorldGuidance`.

3. `openomsi-navbr-overlay-ui-v2.patch`
   - adds the parsed 2D frame to `ui::Frame`;
   - draws the first compact navigation card through the native `Scene::overlays` path.

4. `openomsi-navbr-world-guidance-stock-fallback-v2.patch`
   - shows world guidance with the same helper-object system already used by openOMSI route
     arrows;
   - safe compatibility renderer using stock route-arrow objects.

5. `openomsi-navbr-world-guidance-mesh-v2.patch`
   - replaces the stock-object fallback with one shared custom arrow mesh/material;
   - reuses up to 24 scene instances, updates transform/alpha only, uses
     `RenderPhase::AfterVehicles`, no shadows and no separate render pass;
   - this is the preferred Forza-style world-guidance renderer once the first four patches
     have been validated on the target openOMSI revision.

The first two patches are infrastructure. The third is native 2D UI integration. The
fourth provides immediate in-world guidance using stock assets without memory injection.
The fifth upgrades that fallback to the custom translucent mesh renderer.

These patches are intentionally shipped with the NavBR plugin rather than applied to the
upstream repository automatically. They do not modify the user's openOMSI installation.
