# openOMSI world-guidance renderer reference

The NavBR v2 overlay export already contains renderer-ready world primitives in
`WorldGuidance.GroundArrowPrimitives`. The host renderer should not derive route logic,
maneuvers, fade or culling again.

## Primitive fields

Each primitive provides:

- `X`, `Y`, `Z`: map-space center point;
- `HeadingDegrees`: clockwise heading, 0 = north/+Y;
- `WidthMeters`, `LengthMeters`: requested world size;
- `HeightOffsetMeters`: informational offset already included in `Z`;
- `Opacity`: 0..1;
- `DistanceAheadMeters`: route distance ahead of the player;
- `Kind`: `route`, `left`, `right`, `slight-left`, `slight-right`, `uturn`, or `rejoin`.

The producer currently applies a 0.06 m road-surface lift, begins distance fade at 150 m,
and removes arrows beyond 220 m. Maneuver/rejoin arrows are slightly enlarged.

## Base mesh

Use one shared unit arrow mesh and instance it.

Recommended local-space 2D footprint in meters before scaling:

```text
tip             ( 0.00, +0.50)
right shoulder  (+0.50, +0.05)
shaft right     (+0.18, +0.05)
shaft bottom    (+0.18, -0.50)
shaft left      (-0.18, -0.50)
left shoulder   (-0.50, +0.05)
```

Triangulate as a convex arrow or use two quads plus a triangle. Keep it planar; the
producer already supplies a safe Z offset.

Scale local X by `WidthMeters` and local Y by `LengthMeters`.

## Orientation

NavBR heading follows the OMSI/openOMSI convention used by `omsi.position()`:

- 0° points along +Y;
- 90° points along +X;
- 180° points along -Y;
- 270° points along -X.

For a local arrow authored along +Y, rotate around world Z by
`-HeadingDegrees` when the renderer's positive mathematical angle is counter-clockwise.

## Rendering

Recommended behavior:

- alpha blend with `Opacity`;
- depth test ON;
- depth write OFF;
- no backface culling, or duplicate/reverse the planar winding;
- render after opaque road/terrain geometry and before screen-space UI;
- clamp opacity rather than discarding near the fade boundary;
- reuse one GPU mesh and instance all primitives in one draw when possible.

The renderer may choose its own material. A neutral emissive/translucent material is
preferable so arrows remain visible at night without acting as scene lights.

## Semantic appearance

The host may map `Kind` to visuals without changing geometry:

- `route`: normal forward route arrow;
- turn kinds: emphasized maneuver arrow;
- `uturn`: U-turn glyph/material;
- `rejoin`: distinct rejoin material.

Color is intentionally not part of ABI v2 yet; this lets the openOMSI theme/accessibility
layer decide appearance.

## Culling

NavBR already distance-culls at 220 m. The renderer should still apply normal camera
frustum culling. It may also skip arrows behind the camera; this is a visual optimization
only and must not feed back into route state.

## Integration order

1. call/read `OpenOmsiGetOverlayFrameV2`;
2. parse only when `TimestampUnixMilliseconds` changes;
3. update/reuse the arrow instance buffer from `WorldGuidance.GroundArrowPrimitives`;
4. render world guidance with the 3D scene;
5. render `Overlay2D` via the app UI path.

No route parsing, TTData access, nearest-path calculation or maneuver classification
belongs in the renderer.
