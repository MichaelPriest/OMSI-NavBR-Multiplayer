# Reference host integration for openOMSI

This directory documents the minimal host-side change required for an openOMSI build to
consume NavBR's optional `OpenOmsiGetOverlayFrame` export.

It is intentionally kept out of the stock plugin ABI. The standard OMSI callbacks remain
unchanged. The host-side extension should only be active for in-process plugins because
the overlay is rendered by the same openOMSI process.

## Loader extension

In `crates/omsi-plugin/src/lib.rs`:

```rust
type OverlayFrameFn = unsafe extern "system" fn(*mut u8, i32) -> i32;

pub struct Library {
    _lib: libloading::Library,
    start: StartFn,
    finalize: FinalizeFn,
    variable: Option<AccessFloatFn>,
    trigger: Option<AccessTriggerFn>,
    system: Option<AccessFloatFn>,
    string: Option<AccessStringFn>,
    overlay_frame: Option<OverlayFrameFn>,
}
```

During `Library::load`:

```rust
let overlay_frame = lib
    .get::<OverlayFrameFn>(b"OpenOmsiGetOverlayFrame\0")
    .ok()
    .map(|s| *s);
```

and include it in the returned `Library`.

A safe accessor can then probe and read the cached payload:

```rust
impl Library {
    pub fn overlay_frame(&self) -> Option<Vec<u8>> {
        let f = self.overlay_frame?;
        let required = unsafe { f(std::ptr::null_mut(), 0) };
        if !(2..=262_144).contains(&required) {
            return None;
        }

        let mut buf = vec![0u8; required as usize];
        let written = unsafe { f(buf.as_mut_ptr(), required) };
        if written <= 0 || written >= required {
            return None;
        }
        buf.truncate(written as usize);
        Some(buf)
    }
}
```

The generic `Plugin` / `Plugins` layer can expose frames from local backends only:

```rust
impl Plugin {
    pub fn overlay_frame(&self) -> Option<Vec<u8>> {
        match &self.backend {
            Backend::Local(lib) => lib.overlay_frame(),
            Backend::Remote(_) => None,
        }
    }
}

impl Plugins {
    pub fn overlay_frames(&self) -> Vec<Vec<u8>> {
        self.loaded
            .iter()
            .filter_map(Plugin::overlay_frame)
            .collect()
    }
}
```

## Why local only

A classic 32-bit OMSI DLL may run in `omsi-plugin-host32.exe` through Wine. That host is
for the legacy variable/trigger ABI and should not be extended with rendering data.
NavBR.OpenOmsiPlugin is NativeAOT x64 and is intended to load in-process in openOMSI.

## App integration

The app should parse the JSON snapshot after the normal plugin frame, then pass the latest
valid frame to the UI/render path. Invalid JSON, unsupported schema or stale frames must be
ignored without disabling the plugin.

The natural 2D integration point is `crates/omsi-app/src/ui.rs::Ui::draw`, whose overlays
are appended after the normal HUD. 3D ground arrows should be a separate scene primitive
built from the frame's world-space arrow points; they should not be faked as screen-space
2D overlays.

## Safety/performance

- hard-cap payloads at 262144 bytes;
- reuse the host buffer where practical;
- parse at most when the payload timestamp changes;
- never call route parsing or TTData work from the renderer;
- a malformed optional frame must not mark the OMSI plugin failed;
- no fixed memory addresses or process injection are required.
