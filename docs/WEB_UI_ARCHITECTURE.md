# NavBR Web UI architecture

## Decision

The NavBR desktop client uses **React + TypeScript + Vite hosted by Microsoft WebView2 inside the existing .NET/WPF x86 process**.

Next.js is intentionally not used for the desktop shell. NavBR does not need server-side rendering for local screens, and static Vite assets avoid shipping a Node server.

\`\`\`text
OMSI 2 / Native plugin x86
        ↓
NavBR .NET/C# services
        ↓
typed desktop bridge
        ↓
WebView2
        ↓
React + TypeScript UI
\`\`\`

## Native authority

C# remains authoritative for OMSI detection/launch, telemetry, native interop, Plugin Bridge, SignalR multiplayer, peer-host TCP 27730, Firewall/NAT/UPnP, voice, OMSI files/installations, Hardware Cockpit transport and native HUD/RP overlays.

React owns visual composition and sends only explicit feature commands through the bridge. Production screens do not synthesize telemetry: missing native state is rendered as empty/waiting.

## Primary shell and fallback

React/WebView2 is the primary visible desktop shell in Alpha.14.

1. \`MainWindow\` starts first and initializes native services.
2. \`OpenPrimaryWebShell()\` opens WebView2.
3. WPF is hidden only after \`WebShellWindow.ShellReady\` confirms successful navigation.
4. If WebView2 cannot load, WPF remains visible automatically.
5. The tray icon opens/hides the React primary shell.
6. Settings contains an explicit **Abrir interface WPF** fallback.
7. \`ui/navbr-web/dist\` is copied into build and publish output; the packaged static bootstrap remains a fallback when the React dist is unavailable.

## Migrated desktop surfaces

The React shell now provides real-data surfaces for:

- Home and native OMSI launcher;
- Navigation/GPS using \`NavBRNavigationEngine\`, route traces, ordered stops and ETA;
- Multiplayer Central backed by the existing \`MultiplayerWindow\` controller;
- CCO, remote drivers and operational reports;
- Company/Fleet and Driver Profile stores;
- OMSI installation profiles and launch arguments;
- diagnostics consent and log status;
- Personagem/RP selection and controls backed by the single native `RoleplayCharacterController` and real `Map.Drivers` catalog;
- Hardware Cockpit serial configuration and native packet preview;
- verified networking diagnostics for Firewall TCP 27730, listener state, NAT/CGNAT, UPnP and optional external probe.

The HUD remains native because focus, click-through and OMSI window behavior are native responsibilities. The RP control surface is React, while physical character possession, keyboard capture, transforms and Plugin Bridge interaction remain native C# responsibilities.

## Multiplayer bridge

React does not create a second SignalR client. The existing C# \`MultiplayerWindow\` can run hidden as the live session controller. The bridge exposes room lifecycle, public/private rooms, compatibility, players, chat, voice, audio devices, per-player mute/gain, RP state and real session positions. React never creates a second audio pipeline: device changes and mixer controls call the existing `VoiceChatService`.

Passwords remain ephemeral and are not persisted.

## Navigation bridge

\`MainWindow.WebNavigation.cs\` caches map and route resources and reuses the native navigation engine. It exposes real route geometry, ordered stops, vehicle position, progress, remaining distance, next stop, maneuvers, off-route state and ETA. If route/stop files cannot be resolved safely, the web map fails closed instead of drawing generic map data as an active route.

## CCO / company bridge

The CCO React surface reuses:

- \`DispatcherSessionFeed\`;
- \`DispatcherOperationalFeed\`;
- \`VirtualCompanyStore\`;
- \`DriverProfileStore\`.

Recognize/resolve actions go back through the native operational feed. Fleet registration uses the current real OMSI vehicle.

## Hardware Cockpit bridge

React and WPF share one \`HardwareCockpitBridgeController\`. It owns the only serial connection, persisted COM/baud settings and exact-port auto-reconnect behavior.

The authoritative 200 ms MainWindow telemetry tick publishes \`NAVBR_HW_V1\` frames at approximately 5 Hz, so serial streaming does not depend on a UI page being open. React only configures the controller and renders the exact native frame preview.

## Network diagnostics bridge

Firewall/NAT/UPnP remain native:

- \`WindowsFirewallService\` applies the TCP 27730 inbound rule for all Windows profiles, requests UAC and verifies the rule afterwards;
- \`NatDiagnosticsService\` reports local listener, IPv4 addresses, UPnP gateway and WAN classification;
- automatic UPnP cannot be changed while a local host is running;
- the external TCP probe is independent and only runs when its callback service is configured.

The React Network page presents these checks separately so a valid firewall rule is never treated as proof of Internet reachability.

## Bridge safety

Commands are enumerated feature actions rather than arbitrary native execution. Malformed messages are ignored or returned as command errors. The native process remains the source of truth.
