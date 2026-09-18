# NavBR Web UI architecture

## Decision

The desktop migration uses **React + TypeScript + Vite hosted by Microsoft WebView2 inside the existing .NET/WPF x86 client**.

Next.js is intentionally not used for the desktop shell. NavBR does not need server-side rendering for its local desktop screens, and keeping the frontend as static assets avoids introducing a Node server into the installed application.

Target flow:

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

## Responsibilities

C# remains authoritative for:

- OMSI process detection and launch;
- OMSI telemetry and native interop;
- plugin bridge;
- SignalR multiplayer;
- room hosting and TCP 27730;
- firewall, NAT and UPnP;
- voice;
- OMSI files and installations;
- Hardware Cockpit;
- native HUD/overlay and RP integration where required.

The web UI is responsible for:

- visual shell and navigation;
- operational dashboards;
- rendering state supplied by C#;
- sending explicit user commands back to the native host.

Production screens must not create synthetic telemetry. When native state is absent, the UI renders an empty/waiting state.

## Alpha.14 migration strategy

The migration is incremental.

1. The WPF client remains the stable fallback.
2. `WebShellWindow` hosts WebView2.
3. `MainWindow` supplies the same real `_currentOmsi` and `_lastTelemetry` state already used by WPF.
4. The initial WebView2 Home is shipped as a zero-runtime-dependency bootstrap.
5. The React/Vite project lives under `ui/navbr-web`.
6. When `ui/navbr-web/dist/index.html` is present, WebView2 prefers it automatically; otherwise it loads the bootstrap.
7. Screens migrate one module at a time without moving OMSI/native responsibilities to JavaScript.

## Native bridge — initial contract

Native state is pushed as:

```json
{
  "type": "navbr-state",
  "payload": {
    "omsi": {},
    "telemetry": {}
  }
}
```

Initial commands from the UI:

- `launchOmsi`
- `refreshState`

The contract will be expanded by feature area instead of exposing arbitrary native execution to JavaScript.

## First migrated screen

The first screen is the operational Home. It already renders real:

- OMSI running state;
- OMSI version;
- active map;
- X/Y/Z position;
- heading;
- speed.

It can also launch OMSI through the existing native `OmsiLauncherService` path.

## Next migration blocks

Recommended order:

1. Central Multiplayer and room/session status;
2. Navigation/GPS and roadmap rendering;
3. Profile/company/fleet/CCO;
4. settings, installations and diagnostics;
5. Hardware Cockpit;
6. remaining utility screens.

The native HUD and RP overlay stay native until a web implementation is demonstrably equivalent for focus, click-through and OMSI window behavior.


## Multiplayer migration status

The React shell now contains a real Multiplayer Central backed by the existing C# `MultiplayerWindow` controller instead of a second SignalR client.

Migrated web surfaces:

- Overview with live session status and current operation context;
- Room controls for join, local host creation, disconnect and host shutdown;
- public and private rooms, with private passwords kept ephemeral in the native settings object;
- public-room directory, local favorites and search;
- native room compatibility evaluation before direct join;
- player list with latency, voice and RP state;
- room chat with native send path;
- RP/HUD shortcuts;
- advanced handoff to the native firewall/NAT/UPnP and voice controls.

When the React UI needs multiplayer services but the legacy window is not open, NavBR can initialize the existing WPF controller hidden. This preserves all established lifecycle handlers, HUD integration, SignalR state, host behavior and cleanup while the visual surfaces are migrated incrementally.

The native WPF Multiplayer window remains available as a fallback and for controls that have not yet been migrated.
