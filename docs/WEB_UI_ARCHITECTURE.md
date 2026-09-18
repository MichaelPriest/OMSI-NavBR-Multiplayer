# NavBR Web UI architecture

## Decision

The NavBR desktop client uses **React + TypeScript + Vite hosted by Microsoft WebView2 inside the existing .NET/WPF x86 process**.

Next.js is intentionally not used for the desktop shell. NavBR does not need server-side rendering for local screens, and static Vite assets avoid shipping a Node server.

```text
OMSI 2 / Native plugin x86
        ↓
NavBR .NET/C# services
        ↓
typed desktop bridge
        ↓
WebView2
        ↓
React + TypeScript UI
```

## Native authority

C# remains authoritative for OMSI detection/launch, telemetry, native interop, Plugin Bridge, SignalR multiplayer, peer-host TCP 27730, Firewall/NAT/UPnP, voice, OMSI files/installations, Hardware Cockpit transport, HUD rendering/interaction, roadmap generation, Ghost recording/file I/O/physical replay and the physical RP runtime.

React owns visual composition and sends only explicit feature commands through the bridge. Production screens do not synthesize telemetry: missing native state is rendered as empty/waiting.

## Localization bridge

React uses the same native `LocalizationService` owned by the .NET/WPF host. The WebView state exposes the current culture and the five supported cultures (pt-BR, en-US, es-ES, de-DE and fr-FR). Changing language in the React sidebar calls the native `setLanguage` command, so the existing `language.txt` preference remains the single persisted source of truth.

New React surfaces use the shared translation provider and fall back to English when a key is unavailable; raw OMSI/runtime values are never translated or replaced with synthetic data.

## Primary shell and native host

React/WebView2 is the primary visible desktop shell in Alpha.14.

1. `MainWindow` starts first only to initialize native services that have not yet been detached from the historical WPF shell.
2. The host window is created off-screen, without taskbar presence and with zero opacity; it is never a user-facing fallback.
3. `OpenPrimaryWebShell()` opens `WebShellWindow`, which is the only desktop shell exposed to the user.
4. If WebView2 navigation fails, `WebShellWindow` shows its own native error panel instead of revealing the retired WPF layout.
5. Closing the React shell leaves the hidden native host running in the tray; the tray icon always reopens React.
6. The bridge no longer exposes `showLegacyShell` or `openOmsiProfiles`.
7. `ui/navbr-web/dist` is copied into build and publish output; the packaged static bootstrap remains a WebView content fallback when the React dist is unavailable.

Normal user flows for OMSI installations, HUD configuration, Roadmap Studio and launch recovery stay inside React. When no valid OMSI profile exists, the shell navigates to **Settings → Installations** instead of opening the old WPF profile window. **Move HUD** remains native because it requires direct mouse interaction with the OMSI overlay.

## Migrated desktop surfaces

The React shell now provides real-data surfaces for:

- Home and native OMSI launcher;
- Navigation/GPS using `NavBRNavigationEngine`, route traces, ordered stops and ETA, with an embedded React 3D scene backed by the real OMSI roadmap;
- Multiplayer Central backed by the existing `MultiplayerWindow` controller;
- CCO, remote drivers and operational reports;
- Company/Fleet and Driver Profile stores;
- OMSI installation profiles, launch arguments, native folder selection and Explorer handoff;
- HUD customization backed by `MultiplayerSettingsStore` and `HudProfileCatalog`;
- Roadmap Studio backed by `OmsiRoadmapGeneratorService` and `OmsiRoadmapVectorGeneratorService`;
- Ghost / Replay backed by `GhostRecorder`, `GhostReplayPlayer` and `GhostReplayAnalyticsCalculator`;
- diagnostics consent and log status;
- Personagem/RP selection and controls backed by the single native `RoleplayCharacterController` and real `Map.Drivers` catalog;
- Hardware Cockpit serial configuration and native packet preview;
- verified networking diagnostics for Firewall TCP 27730, listener state, NAT/CGNAT, UPnP and optional external probe.

HUD rendering, focus, click-through and drag/move interaction remain native because they depend on OMSI window behavior. HUD configuration is React and writes through the existing native settings store, whose `SettingsSaved` event reapplies changes live. The RP control surface is React, while physical character possession, keyboard capture, transforms and Plugin Bridge interaction remain native C# responsibilities.

## Multiplayer bridge

React does not create a second SignalR client. The existing C# `MultiplayerWindow` can run hidden as the live session controller. The bridge exposes room lifecycle, public/private rooms, compatibility, players, chat, voice, audio devices, per-player mute/gain, RP state and real session positions. React never creates a second audio pipeline: device changes and mixer controls call the existing `VoiceChatService`.

Passwords remain ephemeral and are not persisted.

## Navigation bridge

`MainWindow.WebNavigation.cs` caches map and route resources and reuses the native navigation engine. It exposes real route geometry, ordered stops, vehicle position, progress, remaining distance, next stop, maneuvers, off-route state and ETA. If route/stop files cannot be resolved safely, the web map fails closed instead of drawing generic map data as an active route.

## CCO / company bridge

The CCO React surface reuses:

- `DispatcherSessionFeed`;
- `DispatcherOperationalFeed`;
- `VirtualCompanyStore`;
- `DriverProfileStore`.

Recognize/resolve actions go back through the native operational feed. Fleet registration uses the current real OMSI vehicle.

## Hardware Cockpit bridge

The React shell and native host share one `HardwareCockpitBridgeController`. It owns the only serial connection, persisted COM/baud settings and exact-port auto-reconnect behavior.

The authoritative 200 ms MainWindow telemetry tick publishes `NAVBR_HW_V1` frames at approximately 5 Hz, so serial streaming does not depend on a UI page being open. React only configures the controller and renders the exact native frame preview.

## Network diagnostics bridge

Firewall/NAT/UPnP remain native:

- `WindowsFirewallService` applies the TCP 27730 inbound rule for all Windows profiles, requests UAC and verifies the rule afterwards;
- `NatDiagnosticsService` reports local listener, IPv4 addresses, UPnP gateway and WAN classification;
- automatic UPnP cannot be changed while a local host is running;
- the external TCP probe is independent and only runs when its callback service is configured.

The React Network page presents these checks separately so a valid firewall rule is never treated as proof of Internet reachability.

## Ghost / Replay bridge

`MainWindow.WebGhost.cs` owns the React-facing Ghost controller while reusing the existing native services.

- recording uses the same `GhostRecorder` at the legacy 100 ms cadence and only consumes real local telemetry;
- saved/imported `.navbrghost` files are parsed by `GhostReplayPlayer.LoadAsync` before being accepted;
- library analytics reuse `GhostReplayAnalyticsCalculator`;
- the React route preview receives only decimated real X/Z frame coordinates and is strictly read-only;
- library selection accepts only file names inside the NavBR Ghost directory instead of arbitrary paths from JavaScript;
- physical Ghost 3D playback continues through `GhostReplayPlayer`, which delegates spawn/update/despawn to the existing Plugin Bridge and fails safely when the plugin rejects writes.

React does not implement a second replay engine and does not synthesize route frames.

## React 3D navigation bridge

The old separate WPF 3D map is no longer required by the normal React flow. `MainWindow.WebNavigation3D.cs` reuses the active native map/layout/route and `Navigation3DSessionFeed` to expose only real scene data: map bounds, local bus, compatible remote buses and route state. The active map directory is mapped into WebView2 as `navbr-map.local`, so the browser loads `whole.roadmap.bmp` directly instead of serializing the bitmap into every state update. Only the currently active map directory is exposed by that mapping.

React handles camera mode, zoom and perspective only; route resolution, map compatibility and vehicle positions remain native C# responsibilities.

## Bridge safety

Commands are enumerated feature actions rather than arbitrary native execution. Malformed messages are ignored or returned as command errors. The native process remains the source of truth.


## OMSI installations bridge

The React installation page uses the existing `OmsiInstallationProfileStore`, `OmsiInstallationLocator` and `OmsiLauncherService`. Folder selection is still a native Windows dialog invoked from C#, and opening an installation delegates to Explorer. React does not invent or cache a second installation registry.

## HUD customization bridge

HUD customization is exposed by `BuildWebSystemState()` from the real `MultiplayerSettingsStore` and `HudProfileCatalog`. Presets, themes, anchors, dimensions, opacity, module visibility and per-widget scale are validated with the same native limits used by the WPF editor. Saving raises `SettingsSaved`, so the native HUD applies changes immediately. Only drag/move interaction remains native.

## Roadmap Studio bridge

Roadmap Studio uses the real `_installedMaps` catalog and delegates all work to the existing native services:

- `OmsiRoadmapGeneratorService` for tile-image analysis and composition;
- `OmsiRoadmapVectorGeneratorService` for spline-based generation.

The WebView receives analysis, progress and build results while the C# services own file I/O, backup creation and safety limits. No roadmap geometry or output file is synthesized in JavaScript.
