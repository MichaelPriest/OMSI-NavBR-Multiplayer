# openOMSI overlay export ABI v1

NavBR keeps the standard OMSI plugin ABI unchanged and adds one optional openOMSI-only
export. An openOMSI build may detect this symbol dynamically; original OMSI 2 and hosts
that do not know it simply ignore it.

## Export

```c
int __stdcall OpenOmsiGetOverlayFrame(
    unsigned char* buffer,
    int capacity);
```

The payload is UTF-8 JSON encoded as `OpenOmsiOverlayFrameState`.

### Probe

Call with `buffer = NULL` and `capacity = 0`.

The return value is the required buffer capacity in bytes, including the trailing NUL.
A return value of `1` means the current JSON payload is empty. A return value of `0`
means the plugin could not serve a frame.

### Read

Allocate at least the probed capacity and call again.

If `capacity` is large enough, the plugin copies the latest cached JSON payload,
writes a trailing NUL and returns the number of UTF-8 payload bytes excluding that NUL.

If `capacity` is too small, nothing is copied and the return value is the required
capacity, including the NUL.

The current producer caps a frame at 262144 UTF-8 bytes. Frames larger than that are
not published; the previously valid cached frame remains available.

## Threading

The export is read-only. It never parses TTData, resolves geometry or rebuilds navigation
state. Those operations happen in the normal plugin status pipeline and publish an
immutable cached byte array. Therefore an openOMSI renderer may query the export every
render frame without forcing route work onto the render thread.

The consumer must still treat the returned JSON as a snapshot. Probe and read can race
with a newer frame. The safe consumer sequence is:

1. probe required capacity;
2. allocate/reuse a buffer of at least that size;
3. read;
4. if the returned value is greater than the supplied capacity, resize and retry once;
5. parse only the returned byte count, not the trailing NUL.

## Frame contents

`OpenOmsiOverlayFrameState` contains:

- timestamp;
- minimap/full-map/HUD/TeleMatrix/navigation visibility flags;
- map center, rotation, radius and orientation mode;
- compact navigation primary/secondary text and maneuver icon;
- distance to maneuver and route remaining;
- off-route state;
- traveled, forward and rejoin route polylines;
- AI/player/stop markers;
- ground-arrow payload with world X/Y/Z, heading, distance ahead and semantic kind.

The ground-arrow entries are navigation data, not a claim that a stock openOMSI build
already renders custom 3D arrows. Rendering remains the host's responsibility.

## Compatibility

The bridge capability is `openomsi-overlay-export-v1`.

The symbol is additive and must never be made a required part of the legacy OMSI plugin
ABI. The existing exports remain:

- `PluginStart`
- `PluginFinalize`
- `AccessVariable`
- `AccessTrigger`
- `AccessStringVariable`
- `AccessSystemVariable`

This lets the same NavBR DLL continue to behave as a standard plugin while enhanced
openOMSI builds opt into the overlay frame.


## ABI v2

A second optional export keeps v1 intact and exposes a versioned envelope with the 2D and
world-space render paths already separated:

```c
int __stdcall OpenOmsiGetOverlayFrameV2(
    unsigned char* buffer,
    int capacity);
```

Probe/read semantics, UTF-8 encoding, NUL termination and the 262144-byte cap are identical
to v1.

The v2 JSON root is `OpenOmsiOverlayExportEnvelopeV2`:

- `Version = 2`;
- `TimestampUnixMilliseconds`;
- `Overlay2D`: minimap/full-map/HUD/TeleMatrix, routes and screen-overlay markers;
- `WorldGuidance`: world-space ground-arrow payload.

The bridge capability is `openomsi-overlay-export-v2`. Consumers should prefer v2 when
available and fall back to v1.
