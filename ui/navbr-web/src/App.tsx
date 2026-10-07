import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import { I18nProvider, useI18n } from "./i18n";
import { NavBrIcon, type NavBrIconName } from "./NavBrIcon";
import {
  type NavBrCompanyMember,
  type NavBrGhostState,
  type NavBrHudPreset,
  type NavBrHudState,
  type NavBrMultiplayerState,
  type NavBrNavigationState,
  type NavBrOmsiInstallation,
  type NavBrRoadmapStudioState,
  type NavBrSessionPoint,
  type NavBrState,
  sendCommand,
  subscribeToNavBrState
} from "./navbrBridge";

type Screen = "home" | "navigation" | "roleplay" | "ghost" | "operations" | "companyNetwork" | "hardware" | "settings" | "multiplayer" | "help";
type MultiplayerTab = "overview" | "room" | "players" | "chat" | "roleplay" | "advanced";

const format = (value: number | undefined | null, digits = 1) =>
  typeof value === "number" && Number.isFinite(value) ? value.toFixed(digits) : "—";

const fallbackMultiplayer: NavBrMultiplayerState = {
  available: false,
  connected: false,
  connectionState: "Disconnected",
  serverUrl: "—",
  roomId: "—",
  displayName: "—",
  hostRunning: false,
  hostPort: null,
  hostReachability: "inactive",
  internetInviteAddress: null,
  upnpMapped: false,
  upnpMessage: null,
  externalProbeConfigured: false,
  roomIsPrivate: false,
  inviteAddresses: [],
  latencyMs: null,
  voiceEnabled: false,
  voiceChannel: "general",
  voiceProximityMeters: 120,
  voiceDeafened: false,
  voiceInputDeviceNumber: 0,
  voiceOutputDeviceNumber: -1,
  voiceInputDevices: [],
  voiceOutputDevices: [],
  voiceMixers: [],
  voicePushToTalkActive: false,
  voiceQuality: {
    activeStreams: 0,
    receivedPackets: 0,
    playedPackets: 0,
    fecRecoveredPackets: 0,
    estimatedLostPackets: 0,
    latePackets: 0,
    duplicatePackets: 0,
    averageJitterMilliseconds: 0,
    targetBufferMilliseconds: 40,
    estimatedLossPercent: 0
  },
  chatHotkey: "F9",
  voiceHotkey: "F10",
  hotkeyOptions: [],
  relayEnabled: false,
  relayServerUrl: "https://omsi-navbr-multiplayer-server.onrender.com",
  physicalVehiclesEnabled: false,
  physicalVehiclesAvailable: false,
  openOmsiV6: {
    active: false,
    isHost: false,
    port: null,
    sessionCode: null,
    webSocketUrl: null,
    role: "offline"
  },
  networkQuality: {
    level: "Unknown",
    roundTripMs: null,
    jitterMs: null,
    lossPercent: 0,
    samples: 0,
    updatedAtUtc: new Date(0).toISOString()
  },
  sessionAuthority: {
    roomOwnerPlayerId: null,
    roomOwnerDisplayName: null,
    trafficAuthorityPlayerId: null,
    trafficAuthorityDisplayName: null,
    isRoomOwner: false,
    isTrafficAuthority: false
  },
  transportMode: "none",
  roomCompatibility: {
    level: "none",
    remoteCount: 0,
    blocking: 0,
    warnings: 0,
    partial: 0,
    affectedAreas: []
  },
  sessionOperationalState: null,
  roleplayEnabled: false,
  localRoleplayActive: false,
  selectedRoleplayCharacter: null,
  playerCount: 0,
  players: [],
  sessionPoints: [],
  chat: []
};

function buildVersionLabel(version?: string | null) {
  if (!version) return "Alpha";
  const clean = version.split("+")[0];
  const alpha = clean.match(/alpha\.([0-9]+(?:\.[0-9]+)*)/i);
  return alpha ? `Alpha.${alpha[1]}` : clean;
}

function Sidebar({
  screen,
  setScreen,
  appVersion
}: {
  screen: Screen;
  setScreen: (screen: Screen) => void;
  appVersion?: string | null;
}) {
  const { t, pick } = useI18n();
  const navGroups: Array<{
    id: string;
    label: string;
    items: Array<{ screen: Screen; icon: NavBrIconName; label: string }>;
  }> = [
    {
      id: "main",
      label: pick("PRINCIPAL", "MAIN", "PRINCIPAL", "HAUPTBEREICH", "PRINCIPAL"),
      items: [
        { screen: "home", icon: "home", label: t("nav.home") },
        { screen: "navigation", icon: "navigation", label: t("nav.navigation") }
      ]
    },
    {
      id: "social",
      label: pick("MULTIPLAYER & RP", "MULTIPLAYER & RP", "MULTIJUGADOR & RP", "MULTIPLAYER & RP", "MULTIJOUEUR & RP"),
      items: [
        { screen: "multiplayer", icon: "multiplayer", label: t("nav.multiplayer") },
        { screen: "roleplay", icon: "roleplay", label: t("nav.roleplay") },
        { screen: "companyNetwork", icon: "company", label: t("nav.company") }
      ]
    },
    {
      id: "tools",
      label: pick("OPERAÇÃO & FERRAMENTAS", "OPERATIONS & TOOLS", "OPERACIÓN & HERRAMIENTAS", "BETRIEB & WERKZEUGE", "EXPLOITATION & OUTILS"),
      items: [
        { screen: "operations", icon: "operations", label: t("nav.operations") },
        { screen: "ghost", icon: "ghost", label: t("nav.ghost") },
        { screen: "hardware", icon: "hardware", label: t("nav.hardware") }
      ]
    },
    {
      id: "system",
      label: pick("SISTEMA", "SYSTEM", "SISTEMA", "SYSTEM", "SYSTÈME"),
      items: [
        { screen: "settings", icon: "settings", label: t("nav.settings") },
        { screen: "help", icon: "help", label: pick("Ajuda", "Help", "Ayuda", "Hilfe", "Aide") }
      ]
    },
  ];

  return (
    <aside className="sidebar">
      <div className="brand">
        <div className="brand-mark">N</div>
        <div><strong>NavBR</strong><span>OMSI Multiplayer</span></div>
      </div>

      <nav className="nav nav-grouped">
        {navGroups.map(group => (
          <section className="nav-group" key={group.id}>
            <span className="nav-group-label">{group.label}</span>
            {group.items.map(item => (
              <button
                key={item.screen}
                className={`nav-item ${screen === item.screen ? "active" : ""}`}
                onClick={() => setScreen(item.screen)}
              >
                <span className="nav-icon-frame"><NavBrIcon name={item.icon} size={19} /></span>
                <span>{item.label}</span>
              </button>
            ))}
          </section>
        ))}
      </nav>

      <div className="sidebar-footer">
        <div className="version-panel" aria-label={pick("Versão do NavBR", "NavBR version", "Versión de NavBR", "NavBR-Version", "Version de NavBR")}>
          <span>{pick("VERSÃO", "VERSION", "VERSIÓN", "VERSION", "VERSION")}</span>
          <strong>{buildVersionLabel(appVersion)}</strong>
        </div>
      </div>
    </aside>
  );
}

function Home({
  state,
  onNavigate,
  onOpenHud,
  onOpenRoadmap
}: {
  state: NavBrState | null;
  onNavigate: (screen: Screen) => void;
  onOpenHud: () => void;
  onOpenRoadmap: () => void;
}) {
  const { t, pick } = useI18n();
  const omsi = state?.omsi;
  const telemetry = state?.telemetry;
  const active = Boolean(omsi?.running && telemetry?.inGame);
  const companyBadge =
    state?.companyNetwork.membership?.badge ||
    state?.companyNetwork.company?.selfBadge ||
    null;
  const plugin = state?.system?.pluginInstallation;
  const mobile = state?.system?.mobileCompanion;
  const applicationUpdate = state?.system?.applicationUpdate;

  return (
    <>
      <header className="topbar">
        <div>
          <span className="eyebrow">{t("home.eyebrow")}</span>
          <h1>{t("home.title")}</h1>
          <p>{t("home.subtitle")}</p>
        </div>
        <div className="top-actions">
          <button className="button ghost icon-button" onClick={() => sendCommand("refreshOmsiDetection")}><NavBrIcon name="refresh" size={16} />{t("common.refresh")}</button>
          <button className="button primary icon-button" disabled={Boolean(omsi?.running)} onClick={() => sendCommand("launchOmsi")}>
            <NavBrIcon name="play" size={16} />{omsi?.running ? t("home.open") : t("home.launch")}
          </button>
        </div>
      </header>

      <section className="hero card">
        <div>
          <span className={`badge ${omsi?.running ? "online" : ""}`}>
            {active ? t("home.operationActive") : omsi?.running ? t("home.omsiDetected") : t("home.waitingOmsi")}
          </span>
          <h2>{active ? telemetry?.mapName || t("home.trip") : omsi?.running ? t("home.open") : t("home.noOperation")}</h2>
          <p>
            {active
              ? t("home.telemetryLive")
              : omsi?.running
                ? t("home.telemetryWaiting")
                : t("home.telemetryClosed")}
          </p>
          {active && (
            <div className="operation-strip">
              <span><small>{t("home.line")}</small><strong>{telemetry?.line || "—"}</strong></span>
              <span><small>{t("home.route")}</small><strong>{telemetry?.route || "—"}</strong></span>
              <span><small>{t("home.destination")}</small><strong>{telemetry?.destinationName || "—"}</strong></span>
              <span><small>{t("home.nextStop")}</small><strong>{telemetry?.nextStopName || "—"}</strong></span>
            </div>
          )}
        </div>
        <div className="speed-panel">
          <span>{t("home.speed")}</span>
          <strong>{telemetry ? format(telemetry.speedKph, 0) : "--"}</strong>
          <small>km/h</small>
        </div>
      </section>

      <section className="status-grid">
        <article className="card status-card">
          <span className="card-label">OMSI</span>
          <strong>{omsi?.running ? t("home.running") : t("home.notDetected")}</strong>
          <small>{omsi?.version ? `${t("home.version")} ${omsi.version}` : `${t("home.version")} —`}</small>
        </article>
        <article className="card status-card">
          <span className="card-label">{t("home.map")}</span>
          <strong>{telemetry?.mapName || "—"}</strong>
          <small>{telemetry ? `X ${format(telemetry.x, 2)} · Y ${format(telemetry.y, 2)}` : t("home.positionUnavailable")}</small>
        </article>
        <article className="card status-card">
          <span className="card-label">MULTIPLAYER</span>
          <strong>{state?.multiplayer.connected ? state.multiplayer.roomId : t("home.disconnected")}</strong>
          <small>{state?.multiplayer.connected ? `${state.multiplayer.playerCount} jogador(es)` : t("home.noRoom")}</small>
        </article>
      </section>

      <section className="status-grid home-system-grid">
        <article className="card status-card">
          <span className="card-label">{pick("PLUGIN OMSI 2", "OMSI 2 PLUGIN", "PLUGIN OMSI 2", "OMSI-2-PLUGIN", "PLUGIN OMSI 2")}</span>
          <strong>{plugin?.state === "installed" || plugin?.state === "ready" ? pick("Em dia", "Current", "Al día", "Aktuell", "À jour") : plugin?.state === "outdated" ? pick("Atualização necessária", "Update required", "Actualización necesaria", "Update erforderlich", "Mise à jour requise") : pick("Verificar", "Check", "Verificar", "Prüfen", "Vérifier")}</strong>
          <small>{plugin?.installedVersion || plugin?.expectedVersion || "—"}</small>
        </article>
        <article className="card status-card">
          <span className="card-label">{pick("EMPRESA / CRACHÁ", "COMPANY / BADGE", "EMPRESA / CREDENCIAL", "UNTERNEHMEN / AUSWEIS", "ENTREPRISE / BADGE")}</span>
          <strong>{companyBadge ? companyBadge.companyShortName || companyBadge.companyName : pick("Sem vínculo", "Not linked", "Sin vínculo", "Nicht verknüpft", "Non lié")}</strong>
          <small>{companyBadge ? `${companyBadge.employeeNumber} · ${companyBadge.role}` : pick("Configure em Empresa", "Configure under Company", "Configura en Empresa", "Unter Unternehmen konfigurieren", "Configurer dans Entreprise")}</small>
        </article>
        <article className="card status-card">
          <span className="card-label">MOBILE COMPANION</span>
          <strong>{mobile?.running ? "ONLINE" : "OFFLINE"}</strong>
          <small>{mobile?.running ? `${mobile.urls.length} endpoint(s) · ${mobile.mode}` : pick("Aguardando serviço", "Waiting for service", "Esperando servicio", "Warte auf Dienst", "En attente du service")}</small>
        </article>
        <article className="card status-card">
          <span className="card-label">{pick("ATUALIZAÇÕES", "UPDATES", "ACTUALIZACIONES", "UPDATES", "MISES À JOUR")}</span>
          <strong>{applicationUpdate?.readyToInstall ? pick("Pronta para instalar", "Ready to install", "Lista para instalar", "Installationsbereit", "Prête à installer") : applicationUpdate?.updateAvailable ? pick("Baixando / validando", "Downloading / verifying", "Descargando / validando", "Download / Prüfung", "Téléchargement / validation") : pick("Em dia", "Current", "Al día", "Aktuell", "À jour")}</strong>
          <small>{applicationUpdate?.availableVersion || applicationUpdate?.currentVersion || state?.appVersion || "—"}</small>
        </article>
      </section>

      <section className="home-shortcuts card">
        <div className="section-heading">
          <div>
            <span className="eyebrow">{pick("ATALHOS RÁPIDOS", "QUICK ACCESS", "ACCESOS RÁPIDOS", "SCHNELLZUGRIFF", "ACCÈS RAPIDE")}</span>
            <h3>{pick("Ir direto ao que você usa", "Go straight to what you use", "Ir directo a lo que usas", "Direkt zu den wichtigsten Bereichen", "Accéder directement à l’essentiel")}</h3>
          </div>
        </div>
        <div className="home-shortcut-groups">
          <section>
            <span className="home-shortcut-group-label">{pick("VIAGEM", "DRIVING", "VIAJE", "FAHRT", "CONDUITE")}</span>
            <div className="home-shortcut-grid">
              <button onClick={() => onNavigate("navigation")}><NavBrIcon name="navigation" size={21} /><span><strong>{t("nav.navigation")}</strong><small>{pick("Mapa, rota e GPS", "Map, route and GPS", "Mapa, ruta y GPS", "Karte, Route und GPS", "Carte, itinéraire et GPS")}</small></span></button>
              <button onClick={() => onNavigate("operations")}><NavBrIcon name="operations" size={21} /><span><strong>{t("nav.operations")}</strong><small>{pick("Operação e frota", "Operations and fleet", "Operación y flota", "Betrieb und Flotte", "Exploitation et flotte")}</small></span></button>
            </div>
          </section>

          <section>
            <span className="home-shortcut-group-label">{pick("ONLINE & RP", "ONLINE & RP", "ONLINE & RP", "ONLINE & RP", "EN LIGNE & RP")}</span>
            <div className="home-shortcut-grid">
              <button onClick={() => onNavigate("multiplayer")}><NavBrIcon name="multiplayer" size={21} /><span><strong>{t("nav.multiplayer")}</strong><small>{pick("Salas, jogadores e voz", "Rooms, players and voice", "Salas, jugadores y voz", "Räume, Spieler und Sprache", "Salons, joueurs et voix")}</small></span></button>
              <button onClick={() => onNavigate("roleplay")}><NavBrIcon name="roleplay" size={21} /><span><strong>{t("nav.roleplay")}</strong><small>{pick("Personagem e interação RP", "Character and RP interaction", "Personaje e interacción RP", "Charakter und RP-Interaktion", "Personnage et interaction RP")}</small></span></button>
            </div>
          </section>

          <section>
            <span className="home-shortcut-group-label">{pick("FERRAMENTAS", "TOOLS", "HERRAMIENTAS", "WERKZEUGE", "OUTILS")}</span>
            <div className="home-shortcut-grid tools">
              <button onClick={onOpenHud}><NavBrIcon name="settings" size={21} /><span><strong>HUD</strong><small>{pick("Presets, prévia e Move HUD", "Presets, preview and Move HUD", "Presets, vista previa y Move HUD", "Presets, Vorschau und Move HUD", "Presets, aperçu et Move HUD")}</small></span></button>
              <button onClick={onOpenRoadmap}><NavBrIcon name="map" size={21} /><span><strong>Roadmap Studio</strong><small>{pick("Roadmap e textura HD/Ultra", "Roadmap and HD/Ultra texture", "Roadmap y textura HD/Ultra", "Roadmap und HD/Ultra-Textur", "Roadmap et texture HD/Ultra")}</small></span></button>
              <button onClick={() => onNavigate("hardware")}><NavBrIcon name="hardware" size={21} /><span><strong>{t("nav.hardware")}</strong><small>{pick("Cockpit e dispositivos", "Cockpit and devices", "Cabina y dispositivos", "Cockpit und Geräte", "Cockpit et périphériques")}</small></span></button>
            </div>
          </section>
        </div>
      </section>
    </>
  );
}


const formatDistance = (meters: number | undefined | null) => {
  if (meters == null || !Number.isFinite(meters)) return "—";
  const safe = Math.max(0, meters);
  return safe >= 1000 ? `${(safe / 1000).toFixed(safe >= 10000 ? 0 : 1)} km` : `${Math.round(safe / 10) * 10} m`;
};

const formatEta = (seconds: number | undefined | null) => {
  if (seconds == null || !Number.isFinite(seconds) || seconds < 0) return "—";
  const totalMinutes = Math.max(1, Math.round(seconds / 60));
  if (totalMinutes < 60) return `${totalMinutes} min`;
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return minutes ? `${hours} h ${minutes} min` : `${hours} h`;
};

const maneuverLabel = (
  maneuver: string,
  pick: (pt: string, en: string, es: string, de: string, fr: string) => string
): { icon: NavBrIconName; title: string } => {
  switch (maneuver) {
    case "SlightLeft": return { icon: "slightLeft", title: pick("Mantenha à esquerda", "Keep left", "Mantente a la izquierda", "Links halten", "Restez à gauche") };
    case "Left": return { icon: "turnLeft", title: pick("Vire à esquerda", "Turn left", "Gira a la izquierda", "Links abbiegen", "Tournez à gauche") };
    case "SharpLeft": return { icon: "sharpLeft", title: pick("Curva forte à esquerda", "Sharp left", "Giro cerrado a la izquierda", "Scharf links", "Virage serré à gauche") };
    case "SlightRight": return { icon: "slightRight", title: pick("Mantenha à direita", "Keep right", "Mantente a la derecha", "Rechts halten", "Restez à droite") };
    case "Right": return { icon: "turnRight", title: pick("Vire à direita", "Turn right", "Gira a la derecha", "Rechts abbiegen", "Tournez à droite") };
    case "SharpRight": return { icon: "sharpRight", title: pick("Curva forte à direita", "Sharp right", "Giro cerrado a la derecha", "Scharf rechts", "Virage serré à droite") };
    case "RejoinRoute": return { icon: "rejoin", title: pick("Retorne para a rota", "Rejoin the route", "Vuelve a la ruta", "Zur Route zurückkehren", "Rejoignez l’itinéraire") };
    default: return { icon: "straight", title: pick("Siga em frente", "Continue straight", "Sigue recto", "Geradeaus weiter", "Continuez tout droit") };
  }
};

function NavigationMap({
  navigation,
  remoteVehicles = [],
  remoteRoleplayCharacters = []
}: {
  navigation: NavBrNavigationState;
  remoteVehicles?: NavBrState["navigation3D"]["remoteVehicles"];
  remoteRoleplayCharacters?: NavBrState["navigation3D"]["remoteRoleplayCharacters"];
}) {
  const { t, pick } = useI18n();
  const [mode, setMode] = useState<"follow" | "full">("follow");
  const [zoom, setZoom] = useState(1);
  const [roadmapSrc, setRoadmapSrc] = useState<string | null>(navigation.roadmapUrl || navigation.roadmapFallbackUrl || null);
  const [roadmapLoadFailed, setRoadmapLoadFailed] = useState(false);

  useEffect(() => {
    setRoadmapSrc(navigation.roadmapUrl || navigation.roadmapFallbackUrl || null);
    setRoadmapLoadFailed(false);
  }, [navigation.roadmapUrl, navigation.roadmapFallbackUrl]);

  const geometry = useMemo(() => {
    const route = navigation.routePoints;
    const rejoin = navigation.rejoinPoints || [];
    const vehicle = navigation.vehicle;
    const remotePoints = [
      ...remoteVehicles.map(item => ({ x: item.x, y: item.y })),
      ...remoteRoleplayCharacters.map(item => ({ x: item.x, y: item.y }))
    ];

    if (route.length < 2 &&
        !navigation.roadmapAvailable &&
        !vehicle &&
        remotePoints.length === 0) {
      return null;
    }

    let minX: number;
    let maxX: number;
    let minY: number;
    let maxY: number;

    if (mode === "follow" && vehicle) {
      const radius = 650;
      minX = vehicle.x - radius;
      maxX = vehicle.x + radius;
      minY = -vehicle.y - radius;
      maxY = -vehicle.y + radius;
    } else if (route.length > 0 || rejoin.length > 0 || remotePoints.length > 0) {
      const allPoints = [...route, ...rejoin, ...remotePoints];
      const xs = allPoints.map(point => point.x);
      const ys = allPoints.map(point => -point.y);
      minX = Math.min(...xs);
      maxX = Math.max(...xs);
      minY = Math.min(...ys);
      maxY = Math.max(...ys);
      const pad = Math.max(80, Math.max(maxX - minX, maxY - minY) * 0.08);
      minX -= pad;
      maxX += pad;
      minY -= pad;
      maxY += pad;
    } else if (navigation.bounds) {
      minX = navigation.bounds.minX;
      maxX = navigation.bounds.maxX;
      minY = -navigation.bounds.maxY;
      maxY = -navigation.bounds.minY;
    } else if (vehicle) {
      const radius = 650;
      minX = vehicle.x - radius;
      maxX = vehicle.x + radius;
      minY = -vehicle.y - radius;
      maxY = -vehicle.y + radius;
    } else {
      return null;
    }

    const baseWidth = Math.max(120, maxX - minX);
    const baseHeight = Math.max(120, maxY - minY);
    const width = baseWidth / zoom;
    const height = baseHeight / zoom;
    const centerX = (minX + maxX) / 2;
    const centerY = (minY + maxY) / 2;
    const routePoints = route.map(point => `${point.x},${-point.y}`).join(" ");
    const rejoinPoints = rejoin.map(point => `${point.x},${-point.y}`).join(" ");

    return {
      viewBox: `${centerX - width / 2} ${centerY - height / 2} ${width} ${height}`,
      routePoints,
      rejoinPoints
    };
  }, [
    navigation.routePoints,
    navigation.rejoinPoints,
    navigation.roadmapAvailable,
    navigation.bounds,
    navigation.vehicle,
    remoteVehicles,
    remoteRoleplayCharacters,
    mode,
    zoom
  ]);

  return (
    <div className="navigation-map">
      <div className="navigation-map-toolbar">
        <button className={mode === "follow" ? "active" : ""} onClick={() => setMode("follow")}>{t("nav.followBus")}</button>
        <button className={mode === "full" ? "active" : ""} onClick={() => setMode("full")}>{t("nav.fullRoute")}</button>
        <button onClick={() => setZoom(value => Math.max(0.5, value * 0.8))} title={pick("Diminuir zoom", "Zoom out", "Alejar", "Herauszoomen", "Dézoomer")}>−</button>
        <button onClick={() => setZoom(value => Math.min(4, value * 1.25))} title={pick("Aumentar zoom", "Zoom in", "Acercar", "Hineinzoomen", "Zoomer")}>+</button>
        <button onClick={() => { setMode("full"); setZoom(1); }}>{pick("Ajustar", "Fit", "Ajustar", "Einpassen", "Ajuster")}</button>
        {roadmapLoadFailed && (
          <span className="navigation-map-diagnostic">
            {pick("Roadmap: falha ao renderizar", "Roadmap: render failed", "Roadmap: error al renderizar", "Roadmap: Renderfehler", "Roadmap : échec du rendu")}
          </span>
        )}
        {navigation.routePoints.length < 2 && navigation.routeDiagnostic && (
          <span
            className="navigation-map-diagnostic"
            title={[
              navigation.routeDiagnostic.trackName ? `track=${navigation.routeDiagnostic.trackName}` : null,
              navigation.routeDiagnostic.line ? `line=${navigation.routeDiagnostic.line}` : null,
              navigation.routeDiagnostic.lookupValue ? `lookup=${navigation.routeDiagnostic.lookupValue}` : null,
              `entries=${navigation.routeDiagnostic.entryCount}`,
              `points=${navigation.routeDiagnostic.pointCount}`
            ].filter(Boolean).join(" • ")}
          >
            TTData: {navigation.routeDiagnostic.mode}
          </span>
        )}
      </div>

      {!geometry ? (
        <div className="map-center-message navigation-empty">
          <strong>{t("nav.routeUnavailable")}</strong>
          <span>{t("nav.routeUnavailableDetail")}</span>
        </div>
      ) : (
        <svg viewBox={geometry.viewBox} preserveAspectRatio="xMidYMid meet" aria-label={pick("Roadmap da rota ativa", "Active route roadmap", "Roadmap de la ruta activa", "Roadmap der aktiven Route", "Roadmap de l’itinéraire actif")}>
          {navigation.roadmapAvailable && roadmapSrc && navigation.bounds && (
            <image
              className="nav-roadmap-image"
              href={roadmapSrc}
              onError={() => {
                if (navigation.roadmapFallbackUrl && roadmapSrc !== navigation.roadmapFallbackUrl) {
                  setRoadmapSrc(navigation.roadmapFallbackUrl);
                } else {
                  setRoadmapSrc(null);
                  setRoadmapLoadFailed(true);
                }
              }}
              x={navigation.bounds.minX}
              y={-navigation.bounds.maxY}
              width={navigation.bounds.maxX - navigation.bounds.minX}
              height={navigation.bounds.maxY - navigation.bounds.minY}
              preserveAspectRatio="none"
            />
          )}

          {geometry.routePoints && (
            <>
              <polyline className="nav-route-shadow" points={geometry.routePoints} />
              <polyline className="nav-route-line" points={geometry.routePoints} />
            </>
          )}

          {navigation.rejoinAvailable && geometry.rejoinPoints && (
            <>
              <polyline className="nav-rejoin-shadow" points={geometry.rejoinPoints} />
              <polyline className="nav-rejoin-line" points={geometry.rejoinPoints} />
              {navigation.rejoinPoint && (
                <g transform={`translate(${navigation.rejoinPoint.x} ${-navigation.rejoinPoint.y})`}>
                  <circle className="nav-rejoin-target" r="13" />
                </g>
              )}
            </>
          )}

          {navigation.stopPoints.map((stop, index) => (
            <g key={`${stop.name}-${index}`} transform={`translate(${stop.x} ${-stop.y})`}>
              <circle className={`nav-stop ${stop.isNext ? "next" : ""}`} r={stop.isNext ? 15 : 9} />
              {stop.isNext && (
                <text className="nav-stop-label" x="20" y="-16">{stop.name}</text>
              )}
            </g>
          ))}

          {remoteVehicles.map(remote => (
            <g
              className="nav-remote-bus"
              key={`bus-${remote.playerId}`}
              transform={`translate(${remote.x} ${-remote.y}) rotate(${remote.headingDegrees})`}
            >
              <circle r="18" className="nav-remote-bus-outer" />
              <circle r="13" className="nav-remote-bus-inner" />
              <polygon className="nav-remote-bus-arrow" points="0,-14 7,9 0,4 -7,9" />
              <text
                className="nav-remote-label"
                x="22"
                y="-18"
                transform={`rotate(${-remote.headingDegrees} 22 -18)`}
              >
                {remote.displayName}
              </text>
            </g>
          ))}

          {remoteRoleplayCharacters.map(remote => (
            <g
              className="nav-remote-roleplay"
              key={`rp-${remote.playerId}`}
              transform={`translate(${remote.x} ${-remote.y}) rotate(${remote.headingDegrees})`}
            >
              <circle r="16" className="nav-remote-roleplay-outer" />
              <circle cy="-3" r="5" className="nav-remote-roleplay-head" />
              <path className="nav-remote-roleplay-body" d="M 0 3 L 0 15 M -6 8 L 6 8 M 0 15 L -5 24 M 0 15 L 5 24" />
              <text
                className="nav-remote-label roleplay"
                x="20"
                y="-17"
                transform={`rotate(${-remote.headingDegrees} 20 -17)`}
              >
                {remote.displayName}
              </text>
            </g>
          ))}

          {navigation.vehicle && (
            <g
              className="nav-vehicle"
              transform={`translate(${navigation.vehicle.x} ${-navigation.vehicle.y}) rotate(${navigation.vehicle.headingDegrees})`}
            >
              <circle r="19" className="nav-vehicle-hud-outer" />
              <circle r="14" className="nav-vehicle-hud-inner" />
              <polygon className="nav-vehicle-hud-arrow" points="0,-15 8,10 0,4 -8,10" />
            </g>
          )}
        </svg>
      )}

      <div className="navigation-map-legend">
        <span><i className="route" /> {pick("Rota OMSI", "OMSI route", "Ruta OMSI", "OMSI-Route", "Itinéraire OMSI")}</span>
        {navigation.rejoinAvailable && <span><i className="rejoin" /> {pick("Retorno à rota", "Route rejoin", "Retorno a la ruta", "Routenrückkehr", "Retour à l’itinéraire")}</span>}
        <span><i className="stop" /> {pick("Paradas", "Stops", "Paradas", "Haltestellen", "Arrêts")}</span>
        <span><i className="bus" /> {pick("Seu ônibus", "Your bus", "Tu autobús", "Dein Bus", "Votre bus")}</span>
        {remoteVehicles.length > 0 && (
          <span><i className="remote-bus" /> {remoteVehicles.length} {pick("ônibus online", "online bus(es)", "autobús(es) online", "Online-Bus(se)", "bus en ligne")}</span>
        )}
        {remoteRoleplayCharacters.length > 0 && (
          <span><i className="remote-rp" /> {remoteRoleplayCharacters.length} RP</span>
        )}
      </div>
    </div>
  );
}


function Navigation3DMap({ state }: { state: NavBrState["navigation3D"] }) {
  const { t, pick } = useI18n();
  const [camera, setCamera] = useState<"follow" | "roleplay" | "aerial">("follow");
  const [zoom, setZoom] = useState(1);
  const [roadmapSrc, setRoadmapSrc] = useState<string | null>(state.roadmapUrl || state.roadmapFallbackUrl || null);
  const [roadmapLoadFailed, setRoadmapLoadFailed] = useState(false);

  useEffect(() => {
    setRoadmapSrc(state.roadmapUrl || state.roadmapFallbackUrl || null);
    setRoadmapLoadFailed(false);
  }, [state.roadmapUrl, state.roadmapFallbackUrl]);

  useEffect(() => {
    if (state.localRoleplayCharacter) {
      setCamera("roleplay");
    } else {
      setCamera(current => current === "roleplay" ? "follow" : current);
    }
  }, [Boolean(state.localRoleplayCharacter)]);

  const scene = useMemo(() => {
    if (!state.bounds || !state.roadmapAvailable || !roadmapSrc) {
      return null;
    }

    const width = Math.max(1, state.bounds.maxX - state.bounds.minX);
    const height = Math.max(1, state.bounds.maxY - state.bounds.minY);
    const sceneWidth = 1000;
    const sceneHeight = Math.max(280, 1000 * height / width);

    const project = (x: number, y: number) => ({
      x: (x - state.bounds!.minX) / width * sceneWidth,
      y: sceneHeight - ((y - state.bounds!.minY) / height * sceneHeight)
    });

    const route = state.routePoints.map(point => project(point.x, point.y));
    const rejoin = (state.rejoinPoints || []).map(point => project(point.x, point.y));
    const rejoinTarget = state.rejoinPoint
      ? project(state.rejoinPoint.x, state.rejoinPoint.y)
      : null;
    const local = state.localVehicle
      ? { ...state.localVehicle, ...project(state.localVehicle.x, state.localVehicle.y) }
      : null;
    const remotes = state.remoteVehicles.map(vehicle => ({
      ...vehicle,
      ...project(vehicle.x, vehicle.y)
    }));
    const remoteRoleplay = state.remoteRoleplayCharacters.map(character => ({
      ...character,
      ...project(character.x, character.y)
    }));
    const roleplay = state.localRoleplayCharacter
      ? {
          ...state.localRoleplayCharacter,
          ...project(state.localRoleplayCharacter.x, state.localRoleplayCharacter.y)
        }
      : null;

    let viewBox = `0 0 ${sceneWidth} ${sceneHeight}`;
    const followTarget = camera === "roleplay" ? roleplay : camera === "follow" ? local : null;
    if (followTarget) {
      const spanX = Math.max(150, 430 / zoom);
      const spanY = Math.max(110, 290 / zoom);
      const minX = Math.max(0, Math.min(sceneWidth - spanX, followTarget.x - spanX / 2));
      const minY = Math.max(0, Math.min(sceneHeight - spanY, followTarget.y - spanY * 0.58));
      viewBox = `${minX} ${minY} ${spanX} ${spanY}`;
    }

    return { sceneWidth, sceneHeight, route, rejoin, rejoinTarget, local, roleplay, remotes, remoteRoleplay, viewBox };
  }, [state, camera, zoom, roadmapSrc]);

  if (!state.roadmapAvailable) {
    return (
      <div className="map-center-message navigation-empty nav3d-empty">
        <strong>{pick("Roadmap 3D indisponível", "3D roadmap unavailable", "Roadmap 3D no disponible", "3D-Roadmap nicht verfügbar", "Roadmap 3D indisponible")}</strong>
        <span>{pick("Gere o whole.roadmap.bmp em Configurações → Roadmap Studio para usar a visão 3D real.", "Generate whole.roadmap.bmp in Settings → Roadmap Studio to use the real 3D view.", "Genera whole.roadmap.bmp en Configuración → Roadmap Studio para usar la vista 3D real.", "Erzeuge whole.roadmap.bmp unter Einstellungen → Roadmap Studio für die echte 3D-Ansicht.", "Générez whole.roadmap.bmp dans Paramètres → Roadmap Studio pour utiliser la vue 3D réelle.")}</span>
      </div>
    );
  }

  if (roadmapLoadFailed) {
    return (
      <div className="map-center-message navigation-empty nav3d-empty">
        <strong>{pick("Roadmap encontrado, mas não pôde ser renderizado", "Roadmap found but could not be rendered", "Roadmap encontrado, pero no se pudo renderizar", "Roadmap gefunden, konnte aber nicht gerendert werden", "Roadmap trouvé mais impossible à afficher")}</strong>
        <span>{pick("O NavBR tentou o PNG em cache e o arquivo real do mapa. Verifique o Roadmap Studio e os arquivos do mapa ativo.", "NavBR tried the cached PNG and the real map file. Check Roadmap Studio and the active map files.", "NavBR intentó el PNG en caché y el archivo real del mapa. Revisa Roadmap Studio y los archivos del mapa activo.", "NavBR hat das PNG im Cache und die echte Kartendatei versucht. Prüfe Roadmap Studio und die Dateien der aktiven Karte.", "NavBR a essayé le PNG en cache et le fichier réel de la carte. Vérifiez Roadmap Studio et les fichiers de la carte active.")}</span>
      </div>
    );
  }

  if (!scene || !state.available) {
    return (
      <div className="map-center-message navigation-empty nav3d-empty">
        <strong>{pick("Aguardando posição do ônibus", "Waiting for bus position", "Esperando posición del autobús", "Warte auf Busposition", "En attente de la position du bus")}</strong>
        <span>{pick("O mapa real está disponível, mas a telemetria de posição do OMSI ainda não está pronta.", "The real map is available, but OMSI position telemetry is not ready yet.", "El mapa real está disponible, pero la telemetría de posición de OMSI aún no está lista.", "Die echte Karte ist verfügbar, aber die OMSI-Positionstelemetrie ist noch nicht bereit.", "La carte réelle est disponible, mais la télémétrie de position OMSI n’est pas encore prête.")}</span>
      </div>
    );
  }

  const routePoints = scene.route.map(point => `${point.x},${point.y}`).join(" ");
  const rejoinPoints = scene.rejoin.map(point => `${point.x},${point.y}`).join(" ");

  return (
    <div className="navigation-3d">
      <div className="navigation-map-toolbar nav3d-toolbar">
        <button className={camera === "follow" ? "active" : ""} onClick={() => setCamera("follow")}>{t("nav.followBus")}</button>
        <button
          className={camera === "roleplay" ? "active" : ""}
          disabled={!scene?.roleplay}
          onClick={() => setCamera("roleplay")}
        >
          {pick("Seguir personagem", "Follow character", "Seguir personaje", "Charakter folgen", "Suivre le personnage")}
        </button>
        <button className={camera === "aerial" ? "active" : ""} onClick={() => setCamera("aerial")}>{pick("Visão aérea", "Aerial view", "Vista aérea", "Luftansicht", "Vue aérienne")}</button>
        <label>
          <span>Zoom</span>
          <input
            type="range"
            min="0.65"
            max="2.3"
            step="0.05"
            value={zoom}
            disabled={camera === "aerial"}
            onChange={event => setZoom(Number(event.target.value))}
          />
        </label>
      </div>

      <div className={`nav3d-stage ${camera}`}>
        <div className="nav3d-perspective">
          <svg
            viewBox={scene.viewBox}
            preserveAspectRatio="xMidYMid meet"
            aria-label={pick("Mapa 3D do OMSI", "OMSI 3D map", "Mapa 3D de OMSI", "OMSI-3D-Karte", "Carte 3D OMSI")}
          >
            <image
              href={roadmapSrc || undefined}
              onError={() => {
                if (state.roadmapFallbackUrl && roadmapSrc !== state.roadmapFallbackUrl) {
                  setRoadmapSrc(state.roadmapFallbackUrl);
                } else {
                  setRoadmapSrc(null);
                  setRoadmapLoadFailed(true);
                }
              }}
              x="0"
              y="0"
              width={scene.sceneWidth}
              height={scene.sceneHeight}
              preserveAspectRatio="none"
              className="nav3d-roadmap"
            />

            {scene.route.length >= 2 && (
              <>
                <polyline className="nav3d-route-shadow" points={routePoints} />
                <polyline className="nav3d-route-line" points={routePoints} />
              </>
            )}

            {state.rejoinAvailable && scene.rejoin.length >= 2 && (
              <>
                <polyline className="nav3d-rejoin-shadow" points={rejoinPoints} />
                <polyline className="nav3d-rejoin-line" points={rejoinPoints} />
                {scene.rejoinTarget && (
                  <circle
                    className="nav3d-rejoin-target"
                    cx={scene.rejoinTarget.x}
                    cy={scene.rejoinTarget.y}
                    r="9"
                  />
                )}
              </>
            )}

            {scene.remotes.map(remote => (
              <g
                className="nav3d-remote-bus"
                key={remote.playerId}
                transform={`translate(${remote.x} ${remote.y}) rotate(${remote.headingDegrees})`}
              >
                <circle r="17" className="nav3d-remote-halo" />
                <rect x="-8" y="-15" width="16" height="30" rx="5" />
                <path d="M 0 -24 L -6 -14 L 6 -14 Z" />
                <text
                  x="20"
                  y="-18"
                  transform={`rotate(${-remote.headingDegrees} 20 -18)`}
                >
                  {remote.displayName}
                </text>
              </g>
            ))}

            {scene.remoteRoleplay.map(remote => (
              <g
                className="nav3d-roleplay-character nav3d-remote-roleplay-character"
                key={remote.playerId}
                transform={`translate(${remote.x} ${remote.y}) rotate(${remote.headingDegrees})`}
              >
                <circle r="16" className="nav3d-roleplay-halo" />
                <circle cy="-3" r="5" className="nav3d-roleplay-head" />
                <path className="nav3d-roleplay-body" d="M 0 3 L 0 16 M -7 8 L 7 8 M 0 16 L -6 26 M 0 16 L 6 26" />
                <path className="nav3d-roleplay-heading" d="M 0 -26 L -5 -17 L 5 -17 Z" />
                <text
                  x="19"
                  y="-18"
                  transform={`rotate(${-remote.headingDegrees} 19 -18)`}
                >
                  {remote.displayName}
                </text>
              </g>
            ))}

            {scene.local && (
              <g
                className="nav3d-local-bus"
                transform={`translate(${scene.local.x} ${scene.local.y}) rotate(${scene.local.headingDegrees})`}
              >
                <circle r="22" className="nav3d-local-halo" />
                <rect x="-10" y="-18" width="20" height="36" rx="6" />
                <path d="M 0 -29 L -8 -17 L 8 -17 Z" />
              </g>
            )}

            {scene.roleplay && (
              <g
                className="nav3d-roleplay-character"
                transform={`translate(${scene.roleplay.x} ${scene.roleplay.y}) rotate(${scene.roleplay.headingDegrees})`}
              >
                <circle r="18" className="nav3d-roleplay-halo" />
                <circle cy="-3" r="6" className="nav3d-roleplay-head" />
                <path className="nav3d-roleplay-body" d="M 0 4 L 0 18 M -8 9 L 8 9 M 0 18 L -7 29 M 0 18 L 7 29" />
                <path className="nav3d-roleplay-heading" d="M 0 -29 L -6 -19 L 6 -19 Z" />
                <text
                  x="22"
                  y="-20"
                  transform={`rotate(${-scene.roleplay.headingDegrees} 22 -20)`}
                >
                  {scene.roleplay.characterName || pick("Personagem", "Character", "Personaje", "Charakter", "Personnage")}
                </text>
              </g>
            )}
          </svg>
        </div>
      </div>

      <div className="nav3d-footer">
        <span><strong>{state.mapName || pick("Mapa OMSI", "OMSI map", "Mapa OMSI", "OMSI-Karte", "Carte OMSI")}</strong></span>
        <span>{state.routeAvailable ? pick("Rota real carregada", "Real route loaded", "Ruta real cargada", "Echte Route geladen", "Itinéraire réel chargé") : pick("Rota não resolvida", "Route not resolved", "Ruta no resuelta", "Route nicht aufgelöst", "Itinéraire non résolu")}</span>
        <span>{state.remoteCount} {pick("ônibus remoto(s) compatível(is)", "compatible remote bus(es)", "autobús(es) remoto(s) compatible(s)", "kompatible Remote-Busse", "bus distant(s) compatible(s)")}</span>
        <span>{state.remoteRoleplayCount} {pick("personagem(ns) RP remoto(s)", "remote RP character(s)", "personaje(s) RP remoto(s)", "Remote-RP-Charakter(e)", "personnage(s) RP distant(s)")}</span>
        <span>
          {scene.roleplay
            ? pick(
                `RP ativo: ${scene.roleplay.characterName || "Personagem"} • ${scene.roleplay.activity}`,
                `RP active: ${scene.roleplay.characterName || "Character"} • ${scene.roleplay.activity}`,
                `RP activo: ${scene.roleplay.characterName || "Personaje"} • ${scene.roleplay.activity}`,
                `RP aktiv: ${scene.roleplay.characterName || "Charakter"} • ${scene.roleplay.activity}`,
                `RP actif : ${scene.roleplay.characterName || "Personnage"} • ${scene.roleplay.activity}`
              )
            : pick("RP inativo", "RP inactive", "RP inactivo", "RP inaktiv", "RP inactif")}
        </span>
      </div>
    </div>
  );
}

function Navigation({
  state,
  requestedView
}: {
  state: NavBrState | null;
  requestedView?: { id: number; view: "2d" | "3d" } | null;
}) {
  const { t, pick } = useI18n();
  const navigation = state?.navigation;
  const navigation3D = state?.navigation3D;
  const [mapView, setMapView] = useState<"2d" | "3d">("2d");

  useEffect(() => {
    if (requestedView) {
      setMapView(requestedView.view);
    }
  }, [requestedView?.id]);
  const telemetry = state?.telemetry;
  const maneuver = maneuverLabel(navigation?.maneuver || "None", pick);
  const routeActive = Boolean(navigation?.available);

  if (!navigation) {
    return <div className="card empty-state">{pick("Aguardando estado de navegação…", "Waiting for navigation state…", "Esperando el estado de navegación…", "Warte auf Navigationsstatus…", "En attente de l’état de navigation…")}</div>;
  }

  return (
    <>
      <header className="topbar navigation-header">
        <div>
          <span className="eyebrow">GPS / ROADMAP</span>
          <h1>{t("nav.navigation")}</h1>
          <p>{pick("Rota, paradas e orientação calculadas a partir do mapa e da viagem reais do OMSI.", "Route, stops and guidance calculated from the real OMSI map and trip.", "Ruta, paradas y orientación calculadas desde el mapa y el viaje reales de OMSI.", "Route, Haltestellen und Führung aus echter OMSI-Karte und Fahrt berechnet.", "Itinéraire, arrêts et guidage calculés à partir de la carte et du trajet réels d’OMSI.")}</p>
        </div>
        <div className="top-actions">
          <span className={`connection-pill ${routeActive ? "connected" : ""}`}>
            <i /> {routeActive ? navigation.isOnRoute ? pick("Na rota", "On route", "En ruta", "Auf Route", "Sur l’itinéraire") : pick("Fora da rota", "Off route", "Fuera de ruta", "Abseits der Route", "Hors itinéraire") : pick("Sem rota", "No route", "Sin ruta", "Keine Route", "Aucun itinéraire")}
          </span>
          <button className="button ghost" onClick={() => setMapView(current => current === "2d" ? "3d" : "2d")}>
            {mapView === "2d" ? pick("Mapa 3D", "3D Map", "Mapa 3D", "3D-Karte", "Carte 3D") : pick("Mapa 2D", "2D Map", "Mapa 2D", "2D-Karte", "Carte 2D")}
          </button>
          <button
            className={`button ghost ${state?.shell.topmost ? "active" : ""}`}
            onClick={() => sendCommand("setShellTopmost", { enabled: !state?.shell.topmost })}
          >
            {state?.shell.topmost
              ? pick("Sempre no topo ✓", "Always on top ✓", "Siempre arriba ✓", "Immer im Vordergrund ✓", "Toujours au premier plan ✓")
              : pick("Sempre no topo", "Always on top", "Siempre arriba", "Immer im Vordergrund", "Toujours au premier plan")}
          </button>
        </div>
      </header>

      <section className="navigation-metrics">
        <div className="metric"><small>{t("home.line")}</small><strong>{navigation.line || telemetry?.line || "—"}</strong></div>
        <div className="metric"><small>{t("home.route")}</small><strong>{navigation.route || telemetry?.route || "—"}</strong></div>
        <div className="metric"><small>{t("home.destination")}</small><strong>{navigation.destinationName || "—"}</strong></div>
        <div className="metric"><small>{pick("PROGRESSO", "PROGRESS", "PROGRESO", "FORTSCHRITT", "PROGRESSION")}</small><strong>{routeActive ? `${format(navigation.routeProgressPercent, 0)}%` : "—"}</strong></div>
        <div className="metric"><small>{pick("RESTANTE", "REMAINING", "RESTANTE", "VERBLEIBEND", "RESTANT")}</small><strong>{routeActive ? formatDistance(navigation.distanceRemainingMeters) : "—"}</strong></div>
      </section>

      <section className="navigation-layout">
        <article className="card navigation-map-card">
          <div className="section-heading">
            <div>
              <span className="eyebrow">{pick("MAPA DA ROTA", "ROUTE MAP", "MAPA DE RUTA", "ROUTENKARTE", "CARTE DE L’ITINÉRAIRE")}</span>
              <h3>{navigation.mapName || telemetry?.mapName || pick("Mapa OMSI", "OMSI map", "Mapa OMSI", "OMSI-Karte", "Carte OMSI")}</h3>
            </div>
            <span className="route-source-pill">GEOMETRIA OMSI</span>
          </div>
          {mapView === "3d" && navigation3D
            ? <Navigation3DMap state={navigation3D} />
            : <NavigationMap
                navigation={navigation}
                remoteVehicles={navigation3D?.remoteVehicles}
                remoteRoleplayCharacters={navigation3D?.remoteRoleplayCharacters}
              />}
        </article>

        <aside className="navigation-side">
          <article className={`card maneuver-card ${navigation.maneuver === "RejoinRoute" ? "warning" : ""}`}>
            <span className="eyebrow">{navigation.maneuver === "RejoinRoute" ? pick("CORREÇÃO DE ROTA", "ROUTE CORRECTION", "CORRECCIÓN DE RUTA", "ROUTENKORREKTUR", "CORRECTION D’ITINÉRAIRE") : pick("PRÓXIMA MANOBRA", "NEXT MANEUVER", "PRÓXIMA MANIOBRA", "NÄCHSTES MANÖVER", "PROCHAINE MANŒUVRE")}</span>
            <div className="maneuver-main">
              <strong><NavBrIcon name={maneuver.icon} size={34} /></strong>
              <div>
                <h3>{maneuver.title}</h3>
                <p>
                  {navigation.maneuver === "RejoinRoute"
                    ? navigation.rejoinAvailable
                      ? `${formatDistance(navigation.rejoinDistanceMeters)} ${pick("pela via de retorno", "via rejoin path", "por la vía de retorno", "über den Rückkehrweg", "par le chemin de retour")}`
                      : formatDistance(navigation.offRouteDistanceMeters)
                    : navigation.distanceToManeuverMeters != null
                      ? `em ${formatDistance(navigation.distanceToManeuverMeters)}`
                      : navigation.currentStreetName || pick("Continue pela rota", "Continue on the route", "Continúa por la ruta", "Der Route weiter folgen", "Continuez sur l’itinéraire")}
                </p>
              </div>
            </div>
          </article>

          <article className="card next-stop-card">
            <span className="eyebrow">{t("home.nextStop")}</span>
            <h3>{navigation.nextStopName || "—"}</h3>
            <div className="next-stop-stats">
              <span><small>{pick("DISTÂNCIA", "DISTANCE", "DISTANCIA", "DISTANZ", "DISTANCE")}</small><strong>{formatDistance(navigation.distanceToNextStopMeters)}</strong></span>
              <span><small>ETA</small><strong>{formatEta(navigation.etaToNextStopSeconds)}</strong></span>
            </div>
          </article>

          <article className="card route-progress-card">
            <div className="section-heading compact">
              <div><span className="eyebrow">{pick("VIAGEM", "TRIP", "VIAJE", "FAHRT", "TRAJET")}</span><h3>{navigation.destinationName || pick("Destino não informado", "Destination not provided", "Destino no informado", "Ziel nicht angegeben", "Destination non renseignée")}</h3></div>
            </div>
            <div className="route-progress-track"><i style={{ width: `${Math.max(0, Math.min(100, navigation.routeProgressPercent))}%` }} /></div>
            <div className="route-progress-meta">
              <span>{formatDistance(navigation.distanceRemainingMeters)} {pick("restantes", "remaining", "restantes", "verbleibend", "restants")}</span>
              <span>{formatEta(navigation.etaToRouteEndSeconds)}</span>
            </div>
            {navigation.currentStreetName && <p className="current-street">{pick("Agora", "Now", "Ahora", "Jetzt", "Maintenant")}: {navigation.currentStreetName}</p>}
          </article>
        </aside>
      </section>

      <section className="card upcoming-stops-card">
        <div className="section-heading">
          <div><span className="eyebrow">{pick("ITINERÁRIO", "ITINERARY", "ITINERARIO", "FAHRPLAN", "ITINÉRAIRE")}</span><h3>{pick("Próximas paradas", "Upcoming stops", "Próximas paradas", "Nächste Haltestellen", "Prochains arrêts")}</h3></div>
          <span className="stop-count">{navigation.stopSequence.totalStops || 0} {pick("paradas na rota", "stops on route", "paradas en la ruta", "Haltestellen auf der Route", "arrêts sur l’itinéraire")}</span>
        </div>
        {navigation.stopSequence.upcomingStops.length === 0 ? (
          <div className="empty-state compact-empty">{pick("Sequência de paradas ainda não resolvida para esta viagem.", "Stop sequence has not been resolved for this trip yet.", "La secuencia de paradas aún no está resuelta para este viaje.", "Die Haltestellenfolge ist für diese Fahrt noch nicht aufgelöst.", "La séquence des arrêts n’est pas encore résolue pour ce trajet.")}</div>
        ) : (
          <div className="upcoming-stops">
            {navigation.stopSequence.upcomingStops.map((stop, index) => (
              <div className={index === 0 ? "next" : ""} key={`${stop}-${index}`}>
                <span>{navigation.stopSequence.nextStopIndex != null ? navigation.stopSequence.nextStopIndex + index + 1 : index + 1}</span>
                <strong>{stop}</strong>
                {index === 0 && <small>{pick("PRÓXIMA", "NEXT", "PRÓXIMA", "NÄCHSTE", "PROCHAIN")}</small>}
              </div>
            ))}
          </div>
        )}
      </section>
    </>
  );
}

function physicalVehicleStatusLabel(
  state: string | null | undefined,
  errorCode: string | null | undefined,
  partCount: number | null | undefined,
  expectedPartCount: number | null | undefined,
  pick: (pt: string, en: string, es: string, de: string, fr: string) => string
) {
  switch (state) {
    case "active": return pick("OMSI 2 3D ativo · movimento suavizado", "OMSI 2 3D active · smoothed motion", "OMSI 2 3D activo · movimiento suavizado", "OMSI 2 3D aktiv · geglättete Bewegung", "OMSI 2 3D actif · mouvement lissé");
    case "active-openomsi":
    case "active-openomsi-drawn":
      return pick(
        "openOMSI 3D confirmado · drawn=true",
        "openOMSI 3D confirmed · drawn=true",
        "openOMSI 3D confirmado · drawn=true",
        "openOMSI 3D bestätigt · drawn=true",
        "openOMSI 3D confirmé · drawn=true"
      );
    case "openomsi-identity-pending":
      return pick(
        "Identidade do ônibus pendente · INFO/STATE bloqueados",
        "Bus identity pending · INFO/STATE blocked",
        "Identidad del autobús pendiente · INFO/STATE bloqueados",
        "Bus-Identität ausstehend · INFO/STATE blockiert",
        "Identité du bus en attente · INFO/STATE bloqués"
      );
    case "openomsi-asset-missing":
      return pick(
        "Addon do ônibus não instalado neste PC",
        "Bus addon is not installed on this PC",
        "El addon del autobús no está instalado en este PC",
        "Bus-Add-on ist auf diesem PC nicht installiert",
        "L’addon du bus n’est pas installé sur ce PC"
      );
    case "openomsi-sent-unconfirmed":
      return pick(
        "openOMSI recebeu envio · aguardando status LAN",
        "openOMSI sent · waiting for LAN status",
        "openOMSI enviado · esperando estado LAN",
        "openOMSI gesendet · wartet auf LAN-Status",
        "openOMSI envoyé · attente du statut LAN"
      );
    case "openomsi-peer-missing":
      return pick(
        "openOMSI ainda não listou o peer",
        "openOMSI has not listed the peer yet",
        "openOMSI aún no lista el peer",
        "openOMSI listet den Peer noch nicht",
        "openOMSI ne liste pas encore le pair"
      );
    case "openomsi-bus-path-mismatch":
      return pick(
        "openOMSI recebeu outro caminho de ônibus",
        "openOMSI reported a different bus path",
        "openOMSI informó otra ruta de autobús",
        "openOMSI meldet einen anderen Buspfad",
        "openOMSI signale un autre chemin de bus"
      );
    case "openomsi-peer-not-drawn":
    case "openomsi-sent-not-drawn":
      return pick(
        "openOMSI recebeu o peer · drawn=false",
        "openOMSI received peer · drawn=false",
        "openOMSI recibió el peer · drawn=false",
        "openOMSI hat den Peer · drawn=false",
        "openOMSI a reçu le pair · drawn=false"
      );
    case "resolving-asset": return pick("Localizando ônibus local", "Resolving local bus", "Buscando autobús local", "Lokaler Bus wird gesucht", "Recherche du bus local");
    case "spawning": return pick("Criando ônibus no OMSI", "Spawning bus in OMSI", "Creando autobús en OMSI", "Bus wird in OMSI erstellt", "Création du bus dans OMSI");
    case "consist-unsupported":
      if (expectedPartCount && expectedPartCount > 1 && partCount && partCount > 1) {
        if (expectedPartCount !== partCount) {
          return pick(
            `Consist bloqueado — addon declara ${expectedPartCount} partes, OMSI criou ${partCount}`,
            `Consist blocked — addon declares ${expectedPartCount} parts, OMSI created ${partCount}`,
            `Consist bloqueado — el addon declara ${expectedPartCount} partes, OMSI creó ${partCount}`,
            `Verband blockiert — Add-on deklariert ${expectedPartCount} Teile, OMSI erzeugte ${partCount}`,
            `Convoi bloqué — l’addon déclare ${expectedPartCount} parties, OMSI en a créé ${partCount}`
          );
        }

        return pick(
          `Articulado/consist confirmado (${partCount} partes) — bloqueado por segurança`,
          `Articulated/consist confirmed (${partCount} parts) — safely blocked`,
          `Articulado/consist confirmado (${partCount} partes) — bloqueado de forma segura`,
          `Gelenk-/Mehrfachverband bestätigt (${partCount} Teile) — sicher blockiert`,
          `Articulé/convoi confirmé (${partCount} parties) — bloqué en sécurité`
        );
      }

      if (expectedPartCount && expectedPartCount > 1) {
        return pick(
          `Addon declara articulado/consist com ${expectedPartCount} partes — spawn bloqueado por segurança`,
          `Addon declares an articulated/consist with ${expectedPartCount} parts — spawn safely blocked`,
          `El addon declara un articulado/consist de ${expectedPartCount} partes — spawn bloqueado de forma segura`,
          `Add-on deklariert einen Gelenk-/Mehrfachverband mit ${expectedPartCount} Teilen — Spawn sicher blockiert`,
          `L’addon déclare un articulé/convoi de ${expectedPartCount} parties — spawn bloqué en sécurité`
        );
      }

      return partCount && partCount > 1
        ? pick(
            `OMSI criou um articulado/consist com ${partCount} partes — removido por segurança`,
            `OMSI created an articulated/consist with ${partCount} parts — safely removed`,
            `OMSI creó un articulado/consist de ${partCount} partes — eliminado de forma segura`,
            `OMSI erzeugte einen Gelenk-/Mehrfachverband mit ${partCount} Teilen — sicher entfernt`,
            `OMSI a créé un articulé/convoi de ${partCount} parties — supprimé en sécurité`
          )
        : pick(
            "Articulado/consist detectado — suporte físico ainda bloqueado",
            "Articulated/consist detected — physical support still blocked",
            "Articulado/consist detectado — soporte físico todavía bloqueado",
            "Gelenk-/Mehrfachverband erkannt — physische Unterstützung noch blockiert",
            "Articulé/convoi détecté — prise en charge physique encore bloquée"
          );
    case "asset-unresolved": return pick("Modelo local não encontrado", "Local model not found", "Modelo local no encontrado", "Lokales Modell nicht gefunden", "Modèle local introuvable");
    case "tile-unavailable":
      if (errorCode === "remote-grid-missing" || errorCode === "tile-grid-missing") {
        return pick(
          "Aguardando GridX/GridY real do jogador remoto",
          "Waiting for the remote player's real OMSI GridX/GridY",
          "Esperando GridX/GridY real del jugador remoto",
          "Warte auf echte OMSI-GridX/GridY des Remote-Spielers",
          "En attente des vrais GridX/GridY OMSI du joueur distant"
        );
      }

      return pick(
        "Grid recebido, aguardando a Kachel carregar no OMSI local",
        "Grid received; waiting for the Kachel to load in the local OMSI",
        "Grid recibido; esperando que cargue la Kachel en el OMSI local",
        "Grid empfangen; warte auf das Laden der Kachel im lokalen OMSI",
        "Grid reçu ; en attente du chargement de la Kachel dans l’OMSI local"
      );
    case "identity-missing": return pick("Aguardando identidade do ônibus", "Waiting for bus identity", "Esperando identidad del autobús", "Warte auf Bus-Identität", "En attente de l’identité du bus");
    case "incompatible": return errorCode
      ? pick(`Incompatível: ${errorCode}`, `Incompatible: ${errorCode}`, `Incompatible: ${errorCode}`, `Inkompatibel: ${errorCode}`, `Incompatible : ${errorCode}`)
      : pick("Sessão incompatível", "Incompatible session", "Sesión incompatible", "Inkompatible Sitzung", "Session incompatible");
    case "plugin-unavailable": return pick("Plugin físico indisponível", "Physical plugin unavailable", "Plugin físico no disponible", "Physisches Plugin nicht verfügbar", "Plugin physique indisponible");
    case "local-state-unavailable": return pick("Aguardando estado local", "Waiting for local state", "Esperando estado local", "Warte auf lokalen Status", "En attente de l’état local");
    case "remote-not-in-game": return pick("Jogador fora do gameplay", "Player not in gameplay", "Jugador fuera del juego", "Spieler nicht im Gameplay", "Joueur hors gameplay");
    case "limit-reached": return pick("Limite físico atingido", "Physical limit reached", "Límite físico alcanzado", "Physisches Limit erreicht", "Limite physique atteinte");
    case "waiting-nearer-slot": return pick("Aguardando vaga física por proximidade", "Waiting for a nearer physical slot", "Esperando una plaza física por proximidad", "Warte auf näheren physischen Slot", "En attente d’une place physique de proximité");
    case "capacity-evicted": return pick("Mantido online · vaga 3D priorizada para ônibus mais próximo", "Still online · 3D slot prioritized for a nearer bus", "Sigue online · plaza 3D priorizada para un bus más cercano", "Weiter online · 3D-Slot für näheren Bus priorisiert", "Toujours en ligne · place 3D priorisée pour un bus plus proche");
    case "out-of-range": return pick("Fora do raio físico 3D", "Outside physical 3D range", "Fuera del radio físico 3D", "Außerhalb des physischen 3D-Radius", "Hors de la portée physique 3D");
    case "switching-vehicle": return pick("Trocando modelo físico", "Switching physical model", "Cambiando modelo físico", "Physisches Modell wird gewechselt", "Changement de modèle physique");
    case "session-changed": return pick("Sessão mudou durante a resolução", "Session changed during resolution", "La sesión cambió durante la resolución", "Sitzung änderte sich während der Auflösung", "La session a changé pendant la résolution");
    case "path-state-missing": return pick("Estado do asset local foi perdido", "Local asset state was lost", "Se perdió el estado del asset local", "Lokaler Asset-Status ging verloren", "L’état de l’asset local a été perdu");
    case "spawn-failed": return errorCode
      ? pick(`Falha no spawn: ${errorCode}`, `Spawn failed: ${errorCode}`, `Falló el spawn: ${errorCode}`, `Spawn fehlgeschlagen: ${errorCode}`, `Échec du spawn : ${errorCode}`)
      : pick("Falha ao criar ônibus físico", "Physical bus spawn failed", "Falló la creación del autobús físico", "Physischer Bus konnte nicht erstellt werden", "Échec de création du bus physique");
    case "update-retrying": return errorCode
      ? pick(`Recuperando 3D: ${errorCode}`, `Recovering 3D: ${errorCode}`, `Recuperando 3D: ${errorCode}`, `3D-Wiederherstellung: ${errorCode}`, `Récupération 3D : ${errorCode}`)
      : pick("Recuperando atualização 3D", "Recovering 3D update", "Recuperando actualización 3D", "3D-Aktualisierung wird wiederhergestellt", "Récupération de la mise à jour 3D");
    case "update-failed": return errorCode
      ? pick(`Falha ao atualizar: ${errorCode}`, `Update failed: ${errorCode}`, `Falló la actualización: ${errorCode}`, `Update fehlgeschlagen: ${errorCode}`, `Échec de mise à jour : ${errorCode}`)
      : pick("Falha ao atualizar ônibus físico", "Physical bus update failed", "Falló la actualización del autobús físico", "Physischer Bus konnte nicht aktualisiert werden", "Échec de mise à jour du bus physique");
    case "despawn-failed": return errorCode
      ? pick(`Falha ao remover: ${errorCode}`, `Removal failed: ${errorCode}`, `Falló la eliminación: ${errorCode}`, `Entfernen fehlgeschlagen: ${errorCode}`, `Échec de suppression : ${errorCode}`)
      : pick("Falha ao remover ônibus físico", "Physical bus removal failed", "Falló la eliminación del autobús físico", "Physischer Bus konnte nicht entfernt werden", "Échec de suppression du bus physique");
    case "disabled": return pick("Ônibus físico desativado", "Physical bus disabled", "Autobús físico desactivado", "Physischer Bus deaktiviert", "Bus physique désactivé");
    default: return pick("Aguardando telemetria física", "Waiting for physical telemetry", "Esperando telemetría física", "Warte auf physische Telemetrie", "En attente de télémétrie physique");
  }
}

function SessionMap({ points }: { points: NavBrSessionPoint[] }) {
  const { pick } = useI18n();
  const plotted = useMemo(() => {
    if (points.length === 0) return [];

    const minX = Math.min(...points.map(point => point.x));
    const maxX = Math.max(...points.map(point => point.x));
    const minY = Math.min(...points.map(point => point.y));
    const maxY = Math.max(...points.map(point => point.y));
    const centerX = (minX + maxX) / 2;
    const centerY = (minY + maxY) / 2;
    const spanX = Math.max(60, maxX - minX);
    const spanY = Math.max(60, maxY - minY);
    const scale = Math.min(72 / spanX, 72 / spanY, 0.9);

    return points.map(point => ({
      ...point,
      px: 50 + (point.x - centerX) * scale,
      py: 50 - (point.y - centerY) * scale
    }));
  }, [points]);

  if (plotted.length === 0) {
    return (
      <div className="session-map-placeholder">
        <div className="map-grid-lines" />
        <div className="map-center-message">
          <strong>{pick("Sem posições válidas", "No valid positions", "Sin posiciones válidas", "Keine gültigen Positionen", "Aucune position valide")}</strong>
          <span>{pick("Ônibus e personagens só aparecem quando há telemetria real, recente e compatível.", "Buses and characters only appear with real, recent and compatible telemetry.", "Autobuses y personajes solo aparecen con telemetría real, reciente y compatible.", "Busse und Charaktere erscheinen nur mit echter, aktueller und kompatibler Telemetrie.", "Les bus et personnages n’apparaissent qu’avec une télémétrie réelle, récente et compatible.")}</span>
        </div>
      </div>
    );
  }

  return (
    <div className="session-map-real">
      <div className="map-grid-lines" />
      <svg viewBox="0 0 100 100" role="img" aria-label={pick("Mapa relativo da sessão", "Relative session map", "Mapa relativo de la sesión", "Relative Sitzungskarte", "Carte relative de la session")}>
        {plotted.map(point => (
          <g
            key={point.playerId}
            className={`session-point ${point.kind} ${point.isLocal ? "local" : ""}`}
            transform={`translate(${point.px} ${point.py}) rotate(${point.headingDegrees})`}
          >
            {point.kind === "bus"
              ? <path d="M -3.5 -5.5 L 3.5 -5.5 L 4 4.5 L 0 7 L -4 4.5 Z" />
              : <circle r="4.2" />}
            <path className="heading-arrow" d="M 0 -8 L -1.8 -4.8 L 1.8 -4.8 Z" />
          </g>
        ))}
        {plotted.map(point => (
          <g key={`label-${point.playerId}`} transform={`translate(${Math.min(92, point.px + 5)} ${Math.max(5, point.py - 4)})`}>
            <text className="session-label">{point.isLocal ? pick("Você", "You", "Tú", "Du", "Vous") : point.displayName}</text>
            <text y="3.3" className="session-label-detail">
              {point.kind === "roleplay"
                ? `${point.activity || "RP"} · ${format(point.speedKph, 0)} km/h`
                : `${point.line ? `${pick("Linha", "Line", "Línea", "Linie", "Ligne")} ${point.line} · ` : ""}${format(point.speedKph, 0)} km/h`}
            </text>
          </g>
        ))}
      </svg>
      <div className="map-legend">
        <span><i className="legend-local" /> {pick("Você", "You", "Tú", "Du", "Vous")}</span>
        <span><i className="legend-bus" /> {pick("Ônibus", "Bus", "Autobús", "Bus", "Bus")}</span>
        <span><i className="legend-rp" /> {pick("Personagem", "Character", "Personaje", "Charakter", "Personnage")}</span>
      </div>
    </div>
  );
}


type OperationsTab = "overview" | "drivers" | "reports" | "company";

function formatDelay(
  seconds: number | undefined | null,
  pick: (pt: string, en: string, es: string, de: string, fr: string) => string
) {
  if (seconds == null || !Number.isFinite(seconds)) return "—";
  const abs = Math.abs(Math.round(seconds));
  const minutes = Math.floor(abs / 60);
  const remainder = abs % 60;
  const value = minutes > 0 ? `${minutes}m ${remainder.toString().padStart(2, "0")}s` : `${remainder}s`;
  return seconds > 0 ? `+${value}` : seconds < 0 ? `-${value}` : pick("No horário", "On time", "A tiempo", "Pünktlich", "À l’heure");
}

function reportSeverityLabel(
  severity: string,
  pick: (pt: string, en: string, es: string, de: string, fr: string) => string
) {
  if (severity === "Critical") return pick("Crítica", "Critical", "Crítica", "Kritisch", "Critique");
  if (severity === "Attention") return pick("Atenção", "Attention", "Atención", "Achtung", "Attention");
  return pick("Informativa", "Informational", "Informativa", "Informativ", "Informative");
}

function reportStatusLabel(
  status: string,
  pick: (pt: string, en: string, es: string, de: string, fr: string) => string
) {
  if (status === "Acknowledged") return pick("Reconhecida", "Acknowledged", "Reconocida", "Bestätigt", "Reconnue");
  if (status === "Resolved") return pick("Resolvida", "Resolved", "Resuelta", "Gelöst", "Résolue");
  return pick("Aberta", "Open", "Abierta", "Offen", "Ouverte");
}

function Operations({
  state,
  error,
  onNavigate,
  requestedTab
}: {
  state: NavBrState | null;
  error: string | null;
  onNavigate: (screen: Screen) => void;
  requestedTab?: { id: number; tab: OperationsTab } | null;
}) {
  const { pick } = useI18n();
  const operations = state?.operations;
  const multiplayer = state?.multiplayer ?? fallbackMultiplayer;
  const [tab, setTab] = useState<OperationsTab>("overview");

  useEffect(() => {
    if (requestedTab) {
      setTab(requestedTab.tab);
    }
  }, [requestedTab?.id]);

  const companyHydrated = useRef(false);
  const profileHydrated = useRef(false);
  const [companyName, setCompanyName] = useState("");
  const [companyShortName, setCompanyShortName] = useState("");
  const [companyBaseMap, setCompanyBaseMap] = useState("");
  const [profileName, setProfileName] = useState("");
  const [profileCompany, setProfileCompany] = useState("");
  const [fleetNumber, setFleetNumber] = useState("");
  const [fleetLivery, setFleetLivery] = useState("");

  useEffect(() => {
    if (!operations) return;

    if (operations.operatorBadgeVerified && operations.operatorBadge) {
      setCompanyName(operations.operatorBadge.companyName || "");
      setCompanyShortName(operations.operatorBadge.companyShortName || "");
      if (!companyHydrated.current) {
        setCompanyBaseMap(operations.company.baseMap || "");
      }
      companyHydrated.current = true;
      return;
    }

    if (companyHydrated.current) return;
    setCompanyName(operations.company.name || "");
    setCompanyShortName(operations.company.shortName || "");
    setCompanyBaseMap(operations.company.baseMap || "");
    companyHydrated.current = true;
  }, [
    operations?.operatorBadgeVerified,
    operations?.operatorBadge?.companyName,
    operations?.operatorBadge?.companyShortName,
    operations?.company.name,
    operations?.company.shortName,
    operations?.company.baseMap
  ]);

  useEffect(() => {
    if (!operations) return;

    if (operations.operatorBadgeVerified && operations.operatorBadge) {
      setProfileName(operations.operatorBadge.displayName || "");
      setProfileCompany(operations.operatorBadge.companyName || "");
      profileHydrated.current = true;
      return;
    }

    if (profileHydrated.current) return;
    setProfileName(operations.profile.displayName || "");
    setProfileCompany(operations.profile.companyName || "");
    profileHydrated.current = true;
  }, [
    operations?.operatorBadgeVerified,
    operations?.operatorBadge?.displayName,
    operations?.operatorBadge?.companyName,
    operations?.profile.displayName,
    operations?.profile.companyName
  ]);

  if (!operations) {
    return <div className="card empty-state">{pick("Aguardando dados do CCO…", "Waiting for operations data…", "Esperando datos del CCO…", "Warte auf Leitstellendaten…", "En attente des données CCO…")}</div>;
  }

  const local = operations.localOperation;
  const activeReports = operations.reports.filter(report => report.status !== "Resolved");
  const criticalReports = activeReports.filter(report => report.severity === "Critical");
  const staleDrivers = operations.drivers.filter(driver => driver.stale);
  const delayedDrivers = operations.drivers.filter(driver => (driver.delaySeconds ?? 0) > 120);
  const localFleet = local?.vehicleName
    ? operations.company.fleet.find(vehicle =>
        vehicle.vehicleModel.localeCompare(local.vehicleName || "", undefined, { sensitivity: "accent" }) === 0)
    : undefined;
  const tripHistory = operations.tripHistory || [];
  const historyDistanceKm = tripHistory.reduce((sum, trip) => sum + trip.distanceKm, 0);
  const historyDrivingSeconds = tripHistory.reduce((sum, trip) => sum + trip.drivingSeconds, 0);
  const historyLines = new Set(tripHistory.map(trip => trip.line).filter(Boolean)).size;
  const historyMaps = new Set(tripHistory.map(trip => trip.mapName).filter(Boolean)).size;

  return (
    <>
      <header className="topbar operations-header">
        <div>
          <span className="eyebrow">{pick("CENTRO DE CONTROLE OPERACIONAL", "OPERATIONS CONTROL CENTER", "CENTRO DE CONTROL OPERACIONAL", "BETRIEBSLEITSTELLE", "CENTRE DE CONTRÔLE OPÉRATIONNEL")}</span>
          <h1>CCO</h1>
          {operations.operatorBadge && (
            <p className="company-badge-inline">
              {operations.operatorBadge.companyShortName} · {pick("Crachá", "Badge", "Credencial", "Ausweis", "Badge")} {operations.operatorBadge.employeeNumber} · {companyRoleLabel(operations.operatorBadge.role, pick)} · {operations.operatorBadgeVerified ? pick("VERIFICADO", "VERIFIED", "VERIFICADO", "VERIFIZIERT", "VÉRIFIÉ") : pick("NÃO VERIFICADO", "UNVERIFIED", "NO VERIFICADO", "NICHT VERIFIZIERT", "NON VÉRIFIÉ")}
            </p>
          )}
          <p>{pick("Operação local, motoristas da sessão e ocorrências recebidas pelo backend NavBR.", "Local operation, session drivers and reports received by the NavBR backend.", "Operación local, conductores de la sesión e incidencias recibidas por el backend NavBR.", "Lokaler Betrieb, Sitzungsfahrer und Meldungen aus dem NavBR-Backend.", "Opération locale, conducteurs de session et incidents reçus par le backend NavBR.")}</p>
        </div>
        <div className="top-actions">
          <span className={`connection-pill ${operations.connected ? "connected" : ""}`}>
            <i /> {operations.connected ? operations.roomId || pick("Sessão ativa", "Active session", "Sesión activa", "Aktive Sitzung", "Session active") : pick("Sem sessão", "No session", "Sin sesión", "Keine Sitzung", "Aucune session")}
          </span>
          <button className="button ghost" onClick={() => onNavigate("multiplayer")}>Multiplayer</button>
        </div>
      </header>

      {error && <div className="command-error">{error}</div>}

      <section className="cco-metrics">
        <div className="metric"><small>{pick("MOTORISTAS REMOTOS", "REMOTE DRIVERS", "CONDUCTORES REMOTOS", "REMOTE-FAHRER", "CONDUCTEURS DISTANTS")}</small><strong>{operations.drivers.length}</strong></div>
        <div className="metric"><small>{pick("OCORRÊNCIAS ABERTAS", "OPEN REPORTS", "INCIDENCIAS ABIERTAS", "OFFENE MELDUNGEN", "INCIDENTS OUVERTS")}</small><strong>{activeReports.length}</strong></div>
        <div className="metric"><small>{pick("CRÍTICAS", "CRITICAL", "CRÍTICAS", "KRITISCH", "CRITIQUES")}</small><strong>{criticalReports.length}</strong></div>
        <div className="metric"><small>{pick("ATRASO > 2 MIN", "DELAY > 2 MIN", "RETRASO > 2 MIN", "VERSPÄTUNG > 2 MIN", "RETARD > 2 MIN")}</small><strong>{delayedDrivers.length}</strong></div>
        <div className="metric"><small>{pick("SEM TELEMETRIA", "NO TELEMETRY", "SIN TELEMETRÍA", "KEINE TELEMETRIE", "SANS TÉLÉMÉTRIE")}</small><strong>{staleDrivers.length}</strong></div>
      </section>

      <div className="mp-tabs cco-tabs" role="tablist">
        {([
          ["overview", pick("Visão geral", "Overview", "Resumen", "Übersicht", "Vue d’ensemble")],
          ["drivers", pick("Motoristas", "Drivers", "Conductores", "Fahrer", "Conducteurs")],
          ["reports", pick("Ocorrências", "Reports", "Incidencias", "Meldungen", "Incidents")],
          ["company", pick("Empresa / Frota", "Company / Fleet", "Empresa / Flota", "Unternehmen / Flotte", "Entreprise / Flotte")]
        ] as [OperationsTab, string][]).map(([key, label]) => (
          <button key={key} className={tab === key ? "active" : ""} onClick={() => setTab(key)}>{label}</button>
        ))}
      </div>

      {tab === "overview" && (
        <section className="cco-overview-grid">
          <article className="card cco-map-card">
            <div className="section-heading">
              <div>
                <span className="eyebrow">{pick("MAPA E RETORNO À ROTA", "MAP AND ROUTE REJOIN", "MAPA Y RETORNO A LA RUTA", "KARTE UND ROUTENRÜCKKEHR", "CARTE ET RETOUR À L’ITINÉRAIRE")}</span>
                <h3>{local?.mapName || state?.telemetry?.mapName || pick("Sem mapa ativo", "No active map", "Sin mapa activo", "Keine aktive Karte", "Aucune carte active")}</h3>
              </div>
              <span className={`live-pill ${operations.connected ? "" : "muted"}`}><span /> {operations.connected ? "LIVE" : "LOCAL"}</span>
            </div>
            {state?.navigation
              ? <NavigationMap
                  navigation={state.navigation}
                  remoteVehicles={state.navigation3D?.remoteVehicles}
                  remoteRoleplayCharacters={state.navigation3D?.remoteRoleplayCharacters}
                />
              : <SessionMap points={multiplayer.sessionPoints} />}
          </article>

          <aside className="cco-side-stack">
            <article className="card compact-card">
              <span className="eyebrow">{pick("OPERAÇÃO LOCAL", "LOCAL OPERATION", "OPERACIÓN LOCAL", "LOKALER BETRIEB", "OPÉRATION LOCALE")}</span>
              <h3>{local?.vehicleName || pick("Nenhum ônibus detectado", "No bus detected", "Ningún autobús detectado", "Kein Bus erkannt", "Aucun bus détecté")}</h3>
              <p>{local?.line ? `${pick("Linha", "Line", "Línea", "Linie", "Ligne")} ${local.line}` : pick("Sem linha", "No line", "Sin línea", "Keine Linie", "Aucune ligne")} · {local?.route || pick("Sem rota", "No route", "Sin ruta", "Keine Route", "Aucun itinéraire")}</p>
              <p>{local?.destination || pick("Destino não informado", "Destination not provided", "Destino no informado", "Ziel nicht angegeben", "Destination non renseignée")}</p>
            </article>

            <article className="card compact-card cco-speed-card">
              <span className="eyebrow">{pick("AGORA", "NOW", "AHORA", "JETZT", "MAINTENANT")}</span>
              <div className="cco-live-values">
                <span><strong>{local ? format(local.speedKph, 0) : "—"}</strong><small>km/h</small></span>
                <span><strong>{formatDelay(local?.delaySeconds, pick)}</strong><small>{pick("atraso", "delay", "retraso", "Verspätung", "retard")}</small></span>
              </div>
              <p>{local?.currentStreet || local?.nextStop || pick("Aguardando telemetria operacional", "Waiting for operational telemetry", "Esperando telemetría operacional", "Warte auf Betriebstelemetrie", "En attente de la télémétrie opérationnelle")}</p>
            </article>

            <article className="card compact-card">
              <span className="eyebrow">{pick("EMPRESA", "COMPANY", "EMPRESA", "UNTERNEHMEN", "ENTREPRISE")}</span>
              <h3>{operations.company.name || pick("Empresa não configurada", "Company not configured", "Empresa no configurada", "Unternehmen nicht konfiguriert", "Entreprise non configurée")}</h3>
              <p>{operations.company.fleet.length} {pick("veículo(s) cadastrados", "registered vehicle(s)", "vehículo(s) registrados", "registrierte Fahrzeuge", "véhicule(s) enregistrés")}</p>
              <button className="text-action" onClick={() => setTab("company")}>{pick("Abrir Empresa / Frota", "Open Company / Fleet", "Abrir Empresa / Flota", "Unternehmen / Flotte öffnen", "Ouvrir Entreprise / Flotte")} →</button>
            </article>
            <article className="card compact-card">
              <span className="eyebrow">{pick("IDENTIDADE", "IDENTITY", "IDENTIDAD", "IDENTITÄT", "IDENTITÉ")}</span>
              <div className="details-grid">
                <div><small>{pick("MOTORISTA", "DRIVER", "CONDUCTOR", "FAHRER", "CONDUCTEUR")}</small><strong>{operations.profile.displayName || "—"}</strong></div>
                <div><small>{pick("EMPRESA", "COMPANY", "EMPRESA", "UNTERNEHMEN", "ENTREPRISE")}</small><strong>{operations.company.name || operations.profile.companyName || "—"}</strong></div>
                <div><small>{pick("VEÍCULO", "VEHICLE", "VEHÍCULO", "FAHRZEUG", "VÉHICULE")}</small><strong>{local?.vehicleName || "—"}</strong></div>
                <div><small>{pick("PREFIXO", "FLEET NO.", "PREFIJO", "WAGENTR.", "N° PARC")}</small><strong>{localFleet?.fleetNumber || "—"}</strong></div>
              </div>
            </article>

            <article className="card compact-card">
              <span className="eyebrow">{pick("TELEMETRIA", "TELEMETRY", "TELEMETRÍA", "TELEMETRIE", "TÉLÉMÉTRIE")}</span>
              <div className="details-grid">
                <div><small>{pick("RUA", "STREET", "CALLE", "STRASSE", "RUE")}</small><strong>{local?.currentStreet || "—"}</strong></div>
                <div><small>{pick("PRÓXIMA PARADA", "NEXT STOP", "PRÓXIMA PARADA", "NÄCHSTER HALT", "PROCHAIN ARRÊT")}</small><strong>{local?.nextStop || "—"}</strong></div>
                <div><small>{pick("PORTAS", "DOORS", "PUERTAS", "TÜREN", "PORTES")}</small><strong>{local?.doors || "—"}</strong></div>
                <div><small>{pick("PARADA SOLICITADA", "STOP REQUEST", "PARADA SOLICITADA", "HALTEWUNSCH", "ARRÊT DEMANDÉ")}</small><strong>{local ? (local.stopRequested ? pick("Sim", "Yes", "Sí", "Ja", "Oui") : pick("Não", "No", "No", "Nein", "Non")) : "—"}</strong></div>
              </div>
            </article>
          </aside>
        </section>
      )}

      {tab === "drivers" && (
        <section className="card cco-panel">
          <div className="section-heading">
            <div><span className="eyebrow">{pick("MOTORISTAS", "DRIVERS", "CONDUCTORES", "FAHRER", "CONDUCTEURS")}</span><h3>{pick("Operação remota da sala", "Remote room operation", "Operación remota de la sala", "Remote-Raumbetrieb", "Opération distante de la salle")}</h3></div>
            <span className="stop-count">{operations.drivers.length} {pick("conectado(s)", "connected", "conectado(s)", "verbunden", "connecté(s)")}</span>
          </div>

          {operations.drivers.length === 0 ? (
            <div className="empty-state">{pick("Nenhum motorista remoto com telemetria real disponível.", "No remote driver with real telemetry available.", "Ningún conductor remoto con telemetría real disponible.", "Kein Remote-Fahrer mit echter Telemetrie verfügbar.", "Aucun conducteur distant avec télémétrie réelle disponible.")}</div>
          ) : (
            <div className="drivers-table">
              {operations.drivers.map(driver => (
                <div className={`driver-row ${driver.stale ? "stale" : ""}`} key={driver.playerId}>
                  <span className="driver-avatar">{driver.displayName.slice(0, 1).toUpperCase()}</span>
                  <div className="driver-primary">
                    <strong>{driver.displayName}</strong>
                    {validCompanyBadge(driver.companyBadge) && (
                      <small className="company-badge-inline">
                        {driver.companyBadge?.companyShortName || driver.companyBadge?.companyName} · {pick("Crachá", "Badge", "Credencial", "Ausweis", "Badge")} {driver.companyBadge?.employeeNumber} · {companyRoleLabel(driver.companyBadge?.role || "", pick)} · {driver.companyBadgeVerified ? pick("VERIFICADO", "VERIFIED", "VERIFICADO", "VERIFIZIERT", "VÉRIFIÉ") : pick("NÃO VERIFICADO", "UNVERIFIED", "NO VERIFICADO", "NICHT VERIFIZIERT", "NON VÉRIFIÉ")}
                      </small>
                    )}
                    <small>{driver.vehicleName || pick("Ônibus não informado", "Bus not provided", "Autobús no informado", "Bus nicht angegeben", "Bus non renseigné")} · {driver.mapName || pick("Mapa —", "Map —", "Mapa —", "Karte —", "Carte —")}</small>
                  </div>
                  <div className="driver-service">
                    <strong>{driver.line || "—"}</strong>
                    <small>{driver.route || driver.destination || pick("Sem rota", "No route", "Sin ruta", "Keine Route", "Aucun itinéraire")}</small>
                  </div>
                  <div className="driver-live">
                    <strong>{format(driver.speedKph, 0)} km/h</strong>
                    <small className={(driver.delaySeconds ?? 0) > 120 ? "late" : ""}>{formatDelay(driver.delaySeconds, pick)}</small>
                  </div>
                  <div className="driver-status">
                    {driver.latestReport ? (
                      <span className={`report-chip ${driver.latestReport.severity.toLowerCase()}`}>
                        {reportSeverityLabel(driver.latestReport.severity, pick)}
                      </span>
                    ) : driver.stale ? (
                      <span className="report-chip stale">{pick("Sem atualização", "No update", "Sin actualización", "Keine Aktualisierung", "Aucune mise à jour")}</span>
                    ) : (
                      <span className="report-chip ok">{pick("Normal", "Normal", "Normal", "Normal", "Normal")}</span>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      )}

      {tab === "reports" && (
        <section className="card cco-panel">
          <div className="section-heading">
            <div>
              <span className="eyebrow">{pick("OCORRÊNCIAS", "REPORTS", "INCIDENCIAS", "MELDUNGEN", "INCIDENTS")}</span>
              <h3>{pick("Assistência e incidentes da sessão", "Session assistance and incidents", "Asistencia e incidentes de la sesión", "Hilfe und Vorfälle der Sitzung", "Assistance et incidents de la session")}</h3>
            </div>
            <span className={`authority-pill ${operations.canManageReports ? "enabled" : ""}`}>
              {operations.canManageReports ? pick("Autoridade CCO", "Operations authority", "Autoridad CCO", "Leitstellenberechtigung", "Autorité CCO") : pick("Somente leitura", "Read only", "Solo lectura", "Nur lesen", "Lecture seule")}
            </span>
          </div>

          {operations.reports.length === 0 ? (
            <div className="empty-state">{pick("Nenhuma ocorrência recebida nesta sessão.", "No report received in this session.", "Ninguna incidencia recibida en esta sesión.", "Keine Meldung in dieser Sitzung empfangen.", "Aucun incident reçu dans cette session.")}</div>
          ) : (
            <div className="reports-list">
              {operations.reports.map(report => (
                <article className={`report-card ${report.severity.toLowerCase()} ${report.status.toLowerCase()}`} key={report.reportId}>
                  <div className="report-card-top">
                    <div>
                      <span className={`report-chip ${report.severity.toLowerCase()}`}>{reportSeverityLabel(report.severity, pick)}</span>
                      <strong>{report.displayName}</strong>
                    </div>
                    <span className="report-status">{reportStatusLabel(report.status, pick)}</span>
                  </div>
                  <h4>{report.kind === "Incident" ? pick("Incidente", "Incident", "Incidente", "Vorfall", "Incident") : pick("Pedido de assistência", "Assistance request", "Solicitud de asistencia", "Hilfeanfrage", "Demande d’assistance")}</h4>
                  <p>{report.message || pick("Sem mensagem adicional.", "No additional message.", "Sin mensaje adicional.", "Keine zusätzliche Nachricht.", "Aucun message supplémentaire.")}</p>
                  <small>{new Date(report.updatedAtUtc).toLocaleString()}</small>

                  {operations.canManageReports && report.status !== "Resolved" && (
                    <div className="report-actions">
                      {report.status === "Open" && (
                        <button className="button ghost compact" onClick={() => sendCommand("acknowledgeOperationalReport", { reportId: report.reportId })}>
                          {pick("Reconhecer", "Acknowledge", "Reconocer", "Bestätigen", "Reconnaître")}
                        </button>
                      )}
                      <button className="button primary compact" onClick={() => sendCommand("resolveOperationalReport", { reportId: report.reportId })}>
                        {pick("Resolver", "Resolve", "Resolver", "Lösen", "Résoudre")}
                      </button>
                    </div>
                  )}
                </article>
              ))}
            </div>
          )}
        </section>
      )}

      {tab === "company" && (
        <section className="company-layout">
          <article className="card company-card">
            <div className="section-heading">
              <div><span className="eyebrow">{pick("EMPRESA VIRTUAL", "VIRTUAL COMPANY", "EMPRESA VIRTUAL", "VIRTUELLES UNTERNEHMEN", "ENTREPRISE VIRTUELLE")}</span><h3>{pick("Identidade operacional", "Operational identity", "Identidad operacional", "Betriebsidentität", "Identité opérationnelle")}</h3></div>
            </div>
            <div className="company-form">
              <label><span>{pick("Nome", "Name", "Nombre", "Name", "Nom")}</span><input value={companyName} disabled={operations.operatorBadgeVerified} onChange={event => setCompanyName(event.target.value)} /></label>
              <label><span>{pick("Sigla", "Short name", "Sigla", "Kürzel", "Sigle")}</span><input value={companyShortName} disabled={operations.operatorBadgeVerified} onChange={event => setCompanyShortName(event.target.value)} /></label>
              <label className="wide"><span>{pick("Mapa base", "Base map", "Mapa base", "Basiskarte", "Carte de base")}</span><input value={companyBaseMap} onChange={event => setCompanyBaseMap(event.target.value)} placeholder={pick("Opcional", "Optional", "Opcional", "Optional", "Optionnel")} /></label>
            </div>
            <button className="button primary" onClick={() => sendCommand("saveCompany", {
              name: companyName,
              shortName: companyShortName,
              baseMap: companyBaseMap
            })}>{pick("Salvar empresa", "Save company", "Guardar empresa", "Unternehmen speichern", "Enregistrer l’entreprise")}</button>
          </article>

          <article className="card company-card">
            <div className="section-heading">
              <div><span className="eyebrow">{pick("PERFIL", "PROFILE", "PERFIL", "PROFIL", "PROFIL")}</span><h3>{pick("Motorista", "Driver", "Conductor", "Fahrer", "Conducteur")}</h3></div>
            </div>
            <div className="company-form">
              <label className="wide"><span>{pick("Nome no NavBR", "NavBR name", "Nombre en NavBR", "Name in NavBR", "Nom dans NavBR")}</span><input value={profileName} disabled={operations.operatorBadgeVerified} onChange={event => setProfileName(event.target.value)} /></label>
              <label className="wide"><span>{pick("Empresa do perfil", "Profile company", "Empresa del perfil", "Profilunternehmen", "Entreprise du profil")}</span><input value={profileCompany} disabled={operations.operatorBadgeVerified} onChange={event => setProfileCompany(event.target.value)} /></label>
            </div>
            {operations.operatorBadgeVerified && operations.operatorBadge && (
              <div className="network-message">
                {pick(
                  "Identidade e empresa sincronizadas pelo crachá verificado da Rede da Empresa. Para alterar esses dados, atualize o cadastro/cargo na Empresa.",
                  "Identity and company are synchronized from the verified Company Network badge. Change them through Company membership/role management.",
                  "La identidad y la empresa se sincronizan con la credencial verificada de la Red de Empresa. Modifícalas desde la gestión de Empresa.",
                  "Identität und Unternehmen werden über den verifizierten Unternehmensausweis synchronisiert. Änderungen erfolgen in der Unternehmensverwaltung.",
                  "L’identité et l’entreprise sont synchronisées depuis le badge vérifié du réseau Entreprise. Modifiez-les via la gestion de l’entreprise."
                )}
              </div>
            )}
            <div className="profile-stats">
              <span><small>{pick("VIAGENS", "TRIPS", "VIAJES", "FAHRTEN", "TRAJETS")}</small><strong>{operations.profile.trips}</strong></span>
              <span><small>{pick("DISTÂNCIA", "DISTANCE", "DISTANCIA", "DISTANZ", "DISTANCE")}</small><strong>{operations.profile.totalDistanceKm.toFixed(1)} km</strong></span>
              <span><small>{pick("MÉDIA", "AVERAGE", "MEDIA", "DURCHSCHNITT", "MOYENNE")}</small><strong>{operations.profile.averageMovingSpeedKph.toFixed(1)} km/h</strong></span>
              <span><small>{pick("MÁXIMA", "MAXIMUM", "MÁXIMA", "MAXIMUM", "MAXIMUM")}</small><strong>{operations.profile.highestSpeedKph.toFixed(0)} km/h</strong></span>
            </div>
            <div className="room-actions">
              <button className="button ghost" onClick={() => sendCommand("saveDriverProfile", { displayName: profileName, companyName: profileCompany })}>{pick("Salvar perfil", "Save profile", "Guardar perfil", "Profil speichern", "Enregistrer le profil")}</button>
              <button className="button ghost" onClick={() => sendCommand("selectDriverProfileImport")}>{pick("Importar", "Import", "Importar", "Importieren", "Importer")}</button>
              <button className="button ghost" onClick={() => sendCommand("exportDriverProfile")}>{pick("Exportar", "Export", "Exportar", "Exportieren", "Exporter")}</button>
            </div>
            {operations.profileTransfer.notice && <div className="network-message">{operations.profileTransfer.notice}</div>}
            {operations.profileTransfer.pending && (
              <div className="card compact-card">
                <span className="eyebrow">{pick("CONFIRMAR IMPORTAÇÃO", "CONFIRM IMPORT", "CONFIRMAR IMPORTACIÓN", "IMPORT BESTÄTIGEN", "CONFIRMER L’IMPORT")}</span>
                <h3>{operations.profileTransfer.pending.displayName}</h3>
                <p>
                  {operations.profileTransfer.pending.includesTripHistory
                    ? pick(
                        `Substituir o perfil local e o histórico atual por este arquivo (${operations.profileTransfer.pending.tripCount} viagens)?`,
                        `Replace the local profile and current history with this file (${operations.profileTransfer.pending.tripCount} trips)?`,
                        `¿Sustituir el perfil local y el historial actual por este archivo (${operations.profileTransfer.pending.tripCount} viajes)?`,
                        `Lokales Profil und Verlauf durch diese Datei ersetzen (${operations.profileTransfer.pending.tripCount} Fahrten)?`,
                        `Remplacer le profil local et l’historique par ce fichier (${operations.profileTransfer.pending.tripCount} trajets) ?`
                      )
                    : pick(
                        "Este arquivo antigo não contém histórico. O perfil será substituído e o histórico local será preservado.",
                        "This older file has no trip history. The profile will be replaced and local history preserved.",
                        "Este archivo antiguo no contiene historial. El perfil se sustituirá y el historial local se conservará.",
                        "Diese ältere Datei enthält keinen Verlauf. Das Profil wird ersetzt, der lokale Verlauf bleibt erhalten.",
                        "Cet ancien fichier ne contient pas d’historique. Le profil sera remplacé et l’historique local conservé."
                      )}
                </p>
                {operations.operatorBadgeVerified && (
                  <div className="network-message">
                    {pick(
                      "O histórico e as estatísticas serão importados, mas nome e empresa continuarão seguindo o crachá verificado.",
                      "History and statistics will be imported, but name and company will continue to follow the verified badge.",
                      "Se importarán historial y estadísticas, pero nombre y empresa seguirán la credencial verificada.",
                      "Verlauf und Statistiken werden importiert; Name und Unternehmen folgen weiterhin dem verifizierten Ausweis.",
                      "L’historique et les statistiques seront importés, mais le nom et l’entreprise resteront liés au badge vérifié."
                    )}
                  </div>
                )}
                <div className="room-actions">
                  <button className="button primary" onClick={() => sendCommand("applyDriverProfileImport")}>{pick("Importar agora", "Import now", "Importar ahora", "Jetzt importieren", "Importer maintenant")}</button>
                  <button className="button ghost" onClick={() => sendCommand("cancelDriverProfileImport")}>{pick("Cancelar", "Cancel", "Cancelar", "Abbrechen", "Annuler")}</button>
                </div>
              </div>
            )}
          </article>

          <article className="card fleet-card">
            <div className="section-heading">
              <div><span className="eyebrow">{pick("HISTÓRICO", "HISTORY", "HISTORIAL", "VERLAUF", "HISTORIQUE")}</span><h3>{pick("Viagens reais", "Real trips", "Viajes reales", "Echte Fahrten", "Trajets réels")}</h3></div>
              <span className="stop-count">{tripHistory.length} {pick("viagem(ns)", "trip(s)", "viaje(s)", "Fahrt(en)", "trajet(s)")}</span>
            </div>
            <div className="profile-stats">
              <span><small>{pick("DISTÂNCIA", "DISTANCE", "DISTANCIA", "DISTANZ", "DISTANCE")}</small><strong>{historyDistanceKm.toFixed(1)} km</strong></span>
              <span><small>{pick("TEMPO", "TIME", "TIEMPO", "ZEIT", "TEMPS")}</small><strong>{formatReplayDuration(historyDrivingSeconds)}</strong></span>
              <span><small>{pick("LINHAS", "LINES", "LÍNEAS", "LINIEN", "LIGNES")}</small><strong>{historyLines}</strong></span>
              <span><small>{pick("MAPAS", "MAPS", "MAPAS", "KARTEN", "CARTES")}</small><strong>{historyMaps}</strong></span>
            </div>
            {tripHistory.length === 0 ? (
              <div className="empty-state compact-empty">{pick("Nenhuma viagem concluída foi registrada ainda. O histórico é preenchido ao encerrar sessões reais do OMSI.", "No completed trip has been recorded yet. History is populated when real OMSI sessions end.", "Aún no se registró ningún viaje completado. El historial se llena al terminar sesiones reales de OMSI.", "Noch keine abgeschlossene Fahrt aufgezeichnet. Der Verlauf wird nach echten OMSI-Sitzungen gefüllt.", "Aucun trajet terminé n’a encore été enregistré. L’historique se remplit à la fin des sessions OMSI réelles.")}</div>
            ) : (
              <div className="fleet-list">
                {tripHistory.map((trip, index) => (
                  <div className="fleet-row" key={`${trip.startedAtUtc}-${index}`}>
                    <div><strong>{new Date(trip.startedAtUtc).toLocaleString()}</strong><small>{[trip.line, trip.route].filter(Boolean).join(" / ") || trip.mapName || "—"}</small></div>
                    <div><strong>{trip.distanceKm.toFixed(1)} km · {formatReplayDuration(trip.drivingSeconds)}</strong><small>{trip.mapName || "—"} · {trip.vehicleName || "—"}</small></div>
                    <small>{pick("Máxima", "Top", "Máxima", "Max.", "Max.")}: {trip.highestSpeedKph.toFixed(1)} km/h · {new Date(trip.endedAtUtc).toLocaleString()}</small>
                  </div>
                ))}
              </div>
            )}
          </article>

          <article className="card fleet-card">
            <div className="section-heading">
              <div><span className="eyebrow">{pick("FROTA", "FLEET", "FLOTA", "FLOTTE", "FLOTTE")}</span><h3>{operations.company.fleet.length} {pick("veículo(s)", "vehicle(s)", "vehículo(s)", "Fahrzeuge", "véhicule(s)")}</h3></div>
            </div>

            <div className="register-vehicle">
              <div>
                <small>{pick("ÔNIBUS ATUAL DO OMSI", "CURRENT OMSI BUS", "AUTOBÚS ACTUAL DE OMSI", "AKTUELLER OMSI-BUS", "BUS OMSI ACTUEL")}</small>
                <strong>{local?.vehicleName || pick("Nenhum ônibus detectado", "No bus detected", "Ningún autobús detectado", "Kein Bus erkannt", "Aucun bus détecté")}</strong>
              </div>
              <input value={fleetNumber} onChange={event => setFleetNumber(event.target.value)} placeholder={pick("Prefixo / número", "Fleet number", "Prefijo / número", "Flottennummer", "Numéro de flotte")} />
              <input value={fleetLivery} onChange={event => setFleetLivery(event.target.value)} placeholder={pick("Pintura (opcional)", "Livery (optional)", "Pintura (opcional)", "Lackierung (optional)", "Livrée (optionnel)")} />
              <button className="button primary" disabled={!local?.vehicleName} onClick={() => {
                sendCommand("registerCurrentVehicle", { fleetNumber, livery: fleetLivery });
                setFleetNumber("");
                setFleetLivery("");
              }}>{pick("Cadastrar atual", "Register current", "Registrar actual", "Aktuellen registrieren", "Enregistrer l’actuel")}</button>
            </div>

            {operations.company.fleet.length === 0 ? (
              <div className="empty-state compact-empty">{pick("Nenhum veículo cadastrado na frota.", "No vehicle registered in the fleet.", "Ningún vehículo registrado en la flota.", "Kein Fahrzeug in der Flotte registriert.", "Aucun véhicule enregistré dans la flotte.")}</div>
            ) : (
              <div className="fleet-list">
                {operations.company.fleet.map(vehicle => (
                  <div className="fleet-row" key={vehicle.id}>
                    <span className="fleet-number">{vehicle.fleetNumber}</span>
                    <div><strong>{vehicle.vehicleModel}</strong><small>{vehicle.livery || pick("Pintura não informada", "Livery not provided", "Pintura no informada", "Lackierung nicht angegeben", "Livrée non renseignée")}</small></div>
                    <small>{vehicle.lastUsedAt ? `${pick("Último uso", "Last used", "Último uso", "Zuletzt verwendet", "Dernière utilisation")}: ${new Date(vehicle.lastUsedAt).toLocaleDateString()}` : pick("Sem uso registrado", "No recorded use", "Sin uso registrado", "Keine Nutzung registriert", "Aucune utilisation enregistrée")}</small>
                    <button className="button ghost compact danger" onClick={() => sendCommand("removeFleetVehicle", { vehicleId: vehicle.id })}>{pick("Remover", "Remove", "Eliminar", "Entfernen", "Supprimer")}</button>
                  </div>
                ))}
              </div>
            )}
          </article>
        </section>
      )}
    </>
  );
}




type CompanyNetworkTab = "network" | "team";

function validCompanyBadge<T extends {
  companyShortName?: string | null;
  companyName?: string | null;
  displayName?: string | null;
  employeeNumber?: string | null;
  role?: string | null;
}>(badge: T | null | undefined): T | null {
  if (!badge) return null;
  if (!badge.displayName?.trim() || !badge.employeeNumber?.trim()) return null;
  if (!badge.companyShortName?.trim() && !badge.companyName?.trim()) return null;
  return badge;
}

function companyRoleLabel(
  role: string,
  pick: (pt: string, en: string, es: string, de: string, fr: string) => string
) {
  const labels: Record<string, string> = {
    President: pick("Presidente", "President", "Presidente", "Präsident", "Président"),
    VicePresident: pick("Vice-Presidente", "Vice President", "Vicepresidente", "Vizepräsident", "Vice-président"),
    Director: pick("Diretoria", "Director", "Dirección", "Direktor", "Direction"),
    OperationsManager: pick("Gerente Operacional", "Operations Manager", "Gerente Operacional", "Betriebsleiter", "Responsable des opérations"),
    Dispatcher: pick("CCO / Dispatcher", "Dispatch", "CCO / Dispatcher", "Leitstelle", "CCO / Dispatch"),
    Supervisor: pick("Fiscal / Supervisor", "Supervisor", "Supervisor", "Supervisor", "Superviseur"),
    SeniorDriver: pick("Motorista Sênior", "Senior Driver", "Conductor Senior", "Senior-Fahrer", "Conducteur senior"),
    Driver: pick("Motorista", "Driver", "Conductor", "Fahrer", "Conducteur"),
    Trainee: pick("Aprendiz", "Trainee", "Aprendiz", "Auszubildender", "Stagiaire")
  };
  return labels[role] || role;
}

function CompanyMemberRow({ member, assignableRoles }: { member: NavBrCompanyMember; assignableRoles: string[] }) {
  const { pick } = useI18n();
  const [role, setRole] = useState(member.role);
  useEffect(() => setRole(member.role), [member.role]);

  return (
    <div className={`company-member-row ${member.isSelf ? "self" : ""}`}>
      <span className="company-member-avatar">{member.displayName.slice(0, 1).toUpperCase()}</span>
      <div className="company-member-main">
        <strong>{member.displayName}{member.isSelf ? ` · ${pick("Você", "You", "Tú", "Du", "Vous")}` : ""}</strong>
        <small>{member.playerId}{member.isOwner ? " · OWNER" : ""}</small>
        {member.employeeNumber && <small className="company-badge-inline">{pick("Crachá", "Badge", "Credencial", "Ausweis", "Badge")} {member.employeeNumber}</small>}
      </div>
      <div className="company-member-role">
        <span>{companyRoleLabel(member.role, pick)}</span>
        <small>{member.permissions || pick("Sem permissões administrativas", "No administrative permissions", "Sin permisos administrativos", "Keine administrativen Rechte", "Aucune permission administrative")}</small>
      </div>
      {member.canChangeRole ? (
        <div className="company-member-actions">
          <select value={role} onChange={event => setRole(event.target.value)}>
            {assignableRoles.map(item => <option key={item} value={item}>{companyRoleLabel(item, pick)}</option>)}
          </select>
          <button className="button ghost compact" disabled={role === member.role} onClick={() => sendCommand("changeCompanyMemberRole", { playerId: member.playerId, role })}>{pick("Aplicar cargo", "Apply role", "Aplicar cargo", "Rolle anwenden", "Appliquer le rôle")}</button>
          {member.canRemove && <button className="button ghost compact danger" onClick={() => { if (window.confirm(pick("Remover " + member.displayName + " da empresa?", "Remove " + member.displayName + " from the company?", "¿Eliminar a " + member.displayName + " de la empresa?", member.displayName + " aus dem Unternehmen entfernen?", "Retirer " + member.displayName + " de l’entreprise ?"))) sendCommand("removeCompanyMember", { playerId: member.playerId }); }}>{pick("Remover", "Remove", "Eliminar", "Entfernen", "Supprimer")}</button>}
        </div>
      ) : <span className="company-member-locked">{member.isOwner ? pick("Protegido", "Protected", "Protegido", "Geschützt", "Protégé") : pick("Sem permissão", "No permission", "Sin permiso", "Keine Berechtigung", "Sans permission")}</span>}
    </div>
  );
}

function CompanyNetwork({
  state,
  error,
  requestedTab
}: {
  state: NavBrState | null;
  error: string | null;
  requestedTab?: { id: number; tab: CompanyNetworkTab } | null;
}) {
  const { pick } = useI18n();
  const companyNetwork = state?.companyNetwork;
  const localCompany = state?.operations.company;
  const [tab, setTab] = useState<CompanyNetworkTab>("network");

  useEffect(() => {
    if (requestedTab) {
      setTab(requestedTab.tab);
    }
  }, [requestedTab?.id]);
  const [nodeUrl, setNodeUrl] = useState("");
  const [nodeUrlDirty, setNodeUrlDirty] = useState(false);
  const [inviteCode, setInviteCode] = useState("");
  const [inviteRole, setInviteRole] = useState("Driver");
  const refreshRequested = useRef(false);

  useEffect(() => {
    if (!companyNetwork || refreshRequested.current) return;
    refreshRequested.current = true;
    sendCommand("refreshCompanyNetwork");
  }, [companyNetwork?.available]);

  useEffect(() => {
    if (!companyNetwork) return;

    const backendNodeUrl = companyNetwork.membership?.nodeUrl || "";
    if (nodeUrlDirty) {
      if (backendNodeUrl && backendNodeUrl === nodeUrl) {
        setNodeUrlDirty(false);
      }
    } else {
      setNodeUrl(backendNodeUrl);
    }

    if (!companyNetwork.assignableRoles.includes(inviteRole)) {
      setInviteRole(
        companyNetwork.assignableRoles.includes("Driver")
          ? "Driver"
          : companyNetwork.assignableRoles[0] || "Driver"
      );
    }
  }, [
    companyNetwork?.membership?.nodeUrl,
    companyNetwork?.assignableRoles,
    inviteRole,
    nodeUrl,
    nodeUrlDirty
  ]);

  if (!companyNetwork?.available) return <div className="card empty-state">{pick("Carregando Rede da Empresa…", "Loading Company Network…", "Cargando Red de Empresa…", "Unternehmensnetz wird geladen…", "Chargement du Réseau Entreprise…")}</div>;

  const company = companyNetwork.company;
  const node = companyNetwork.node;
  const selfBadge = validCompanyBadge(
    company?.selfBadge || companyNetwork.membership?.badge || null
  );
  const canCreateInvite = Boolean(node?.running && company?.canInvite);

  return (
    <>
      <header className="topbar company-network-header">
        <div><span className="eyebrow">NAVBR COMPANY NETWORK</span><h1>{pick("Rede da empresa", "Company network", "Red de empresa", "Unternehmensnetz", "Réseau entreprise")}</h1><p>{pick("Gerencie sua empresa, equipe e convites online em um só lugar.", "Manage your company, team and online invites in one place.", "Gestiona tu empresa, equipo e invitaciones online en un solo lugar.", "Verwalte Unternehmen, Team und Online-Einladungen an einem Ort.", "Gérez votre entreprise, votre équipe et vos invitations en ligne au même endroit.")}</p></div>
        <div className="top-actions"><span className={`connection-pill ${node?.running ? "connected" : ""}`}><i /> {node?.running ? pick("Empresa online", "Company online", "Empresa online", "Unternehmen online", "Entreprise en ligne") : companyNetwork.membership ? pick("Vinculado", "Linked", "Vinculado", "Verknüpft", "Lié") : "Offline"}</span><button className="button ghost" onClick={() => sendCommand("refreshCompanyNetwork")}>{pick("Atualizar", "Refresh", "Actualizar", "Aktualisieren", "Actualiser")}</button></div>
      </header>
      {error && <div className="command-error">{error}</div>}
      <section className="company-network-metrics">
        <div className="metric"><small>{pick("EMPRESA", "COMPANY", "EMPRESA", "UNTERNEHMEN", "ENTREPRISE")}</small><strong>{company?.name || localCompany?.name || "—"}</strong></div>
        <div className="metric"><small>{pick("CARGO", "ROLE", "CARGO", "ROLLE", "RÔLE")}</small><strong>{company?.selfRole ? companyRoleLabel(company.selfRole, pick) : companyNetwork.membership?.role ? companyRoleLabel(companyNetwork.membership.role, pick) : "—"}</strong></div>
        <div className="metric"><small>{pick("CRACHÁ", "BADGE", "CREDENCIAL", "AUSWEIS", "BADGE")}</small><strong>{selfBadge ? `${selfBadge.companyShortName} #${selfBadge.employeeNumber}` : "—"}</strong></div>
        <div className="metric"><small>{pick("MEMBROS", "MEMBERS", "MIEMBROS", "MITGLIEDER", "MEMBRES")}</small><strong>{company?.memberCount ?? 0}</strong></div>
        <div className="metric"><small>NODE</small><strong>{node?.running ? "Online" : "Offline"}</strong></div>
      </section>
      <div className="mp-tabs" role="tablist"><button className={tab === "network" ? "active" : ""} onClick={() => setTab("network")}>{pick("Rede", "Network", "Red", "Netzwerk", "Réseau")}</button><button className={tab === "team" ? "active" : ""} onClick={() => setTab("team")}>{pick("Equipe", "Team", "Equipo", "Team", "Équipe")}</button></div>
      {tab === "network" && (
        <section className="network-layout">
          <article className="card network-overview-card">
            <div className="section-heading">
              <div>
                <span className="eyebrow">openOMSI v6</span>
                <h3>{pick("Transporte físico", "Physical transport", "Transporte físico", "Physischer Transport", "Transport physique")}</h3>
              </div>
            </div>
            <div className="network-status-grid">
              <div>
                <small>{pick("ESTADO", "STATUS", "ESTADO", "STATUS", "ÉTAT")}</small>
                <strong className={multiplayer.openOmsiV6.active ? "ok" : "warn"}>
                  {multiplayer.openOmsiV6.active
                    ? multiplayer.openOmsiV6.isHost
                      ? "HOST"
                      : pick("CLIENTE", "CLIENT", "CLIENTE", "CLIENT", "CLIENT")
                    : "OFFLINE"}
                </strong>
                <span>{pick("openOMSI v6 é a única autoridade de movimento físico.", "openOMSI v6 is the only physical-motion authority.", "openOMSI v6 es la única autoridad de movimiento físico.", "openOMSI v6 ist die einzige Autorität für physische Bewegung.", "openOMSI v6 est la seule autorité de mouvement physique.")}</span>
              </div>
              <div>
                <small>UDP</small>
                <strong>{multiplayer.openOmsiV6.port ?? "—"}</strong>
                <span>{pick("Usado diretamente na LAN quando disponível.", "Used directly on LAN when available.", "Usado directamente en LAN cuando está disponible.", "Wird im LAN direkt verwendet, wenn verfügbar.", "Utilisé directement sur le LAN lorsque disponible.")}</span>
              </div>
              <div>
                <small>{pick("CÓDIGO DA SESSÃO", "SESSION CODE", "CÓDIGO DE SESIÓN", "SITZUNGSCODE", "CODE DE SESSION")}</small>
                <strong>{multiplayer.openOmsiV6.sessionCode || "—"}</strong>
                <span>{pick("Mesmo código no host e em todos os clientes.", "The same code is shared by host and all clients.", "El mismo código se comparte entre host y clientes.", "Derselbe Code gilt für Host und alle Clients.", "Le même code est partagé par l’hôte et tous les clients.")}</span>
              </div>
              <div>
                <small>WEBSOCKET / TUNNEL</small>
                <strong>{multiplayer.openOmsiV6.webSocketUrl ? pick("Disponível", "Available", "Disponible", "Verfügbar", "Disponible") : "—"}</strong>
                <span>{multiplayer.openOmsiV6.webSocketUrl || pick("UDP/LAN ativo; túnel não publicado.", "UDP/LAN active; no tunnel published.", "UDP/LAN activo; túnel no publicado.", "UDP/LAN aktiv; kein Tunnel veröffentlicht.", "UDP/LAN actif ; aucun tunnel publié.")}</span>
              </div>
            </div>
            <p className="network-note">
              {pick(
                "O SignalR continua apenas como sidecar para CCO, empresa, frota, perfil, permissões e relatórios. Ele não move ônibus nem personagens.",
                "SignalR remains only as a sidecar for CCO, company, fleet, profile, permissions and reports. It does not move buses or characters.",
                "SignalR queda solo como sidecar para CCO, empresa, flota, perfil, permisos e informes. No mueve autobuses ni personajes.",
                "SignalR bleibt nur Sidecar für CCO, Unternehmen, Flotte, Profil, Berechtigungen und Berichte. Es bewegt keine Busse oder Charaktere.",
                "SignalR reste uniquement un sidecar pour le CCO, l’entreprise, la flotte, le profil, les autorisations et les rapports. Il ne déplace ni bus ni personnages."
              )}
            </p>
          </article>
        </section>
      )}
    </>
  );
}


function roleplayStatusLabel(
  status: string | null | undefined,
  pick: (pt: string, en: string, es: string, de: string, fr: string) => string
) {
  switch (status) {
    case "roleplay-active": return pick("Personagem ativo", "Character active", "Personaje activo", "Charakter aktiv", "Personnage actif");
    case "roleplay-paused-focus-loss": return pick("RP pausado · OMSI fora de foco", "RP paused · OMSI is out of focus", "RP pausado · OMSI fuera de foco", "RP pausiert · OMSI nicht im Fokus", "RP en pause · OMSI n’est pas au premier plan");
    case "roleplay-focus-stop-failed": return pick("Falha ao confirmar parada segura do personagem", "Could not confirm the character safe stop", "No se pudo confirmar la parada segura del personaje", "Sicherer Stopp der Figur konnte nicht bestätigt werden", "Impossible de confirmer l’arrêt sécurisé du personnage");
    case "roleplay-returned-to-bus": return pick("Motorista retornou ao ônibus", "Driver returned to the bus", "El conductor volvió al autobús", "Fahrer ist zum Bus zurückgekehrt", "Le conducteur est retourné au bus");
    case "roleplay-character-selected": return pick("Personagem selecionado", "Character selected", "Personaje seleccionado", "Charakter ausgewählt", "Personnage sélectionné");
    case "roleplay-active-driver-auto-selected": return pick("Motorista ativo detectado automaticamente", "Active driver detected automatically", "Conductor activo detectado automáticamente", "Aktiver Fahrer automatisch erkannt", "Conducteur actif détecté automatiquement");
    case "roleplay-active-driver-not-detected": return pick("Aguardando o motorista ativo do ônibus", "Waiting for the active bus driver", "Esperando al conductor activo del autobús", "Warte auf den aktiven Busfahrer", "En attente du conducteur actif du bus");
    case "roleplay-plugin-unavailable": return pick("Plugin Bridge sem suporte RP", "Plugin Bridge has no RP support", "Plugin Bridge sin soporte RP", "Plugin Bridge ohne RP-Unterstützung", "Plugin Bridge sans prise en charge RP");
    case "roleplay-plugin-disconnected": return pick("Plugin Bridge desconectado", "Plugin Bridge disconnected", "Plugin Bridge desconectado", "Plugin Bridge getrennt", "Plugin Bridge déconnecté");
    case "roleplay-plugin-capability-unavailable": return pick("Plugin conectado, mas capacidades RP ausentes", "Plugin connected, but RP capabilities are missing", "Plugin conectado, pero faltan capacidades RP", "Plugin verbunden, aber RP-Funktionen fehlen", "Plugin connecté, mais capacités RP absentes");
    case "roleplay-character-required": return pick("Selecione um personagem", "Select a character", "Selecciona un personaje", "Charakter auswählen", "Sélectionnez un personnage");
    case "roleplay-waiting-telemetry": return pick("Aguardando telemetria do OMSI", "Waiting for OMSI telemetry", "Esperando telemetría de OMSI", "Warte auf OMSI-Telemetrie", "En attente de la télémétrie OMSI");
    case "roleplay-map-or-character-changed": return pick("Mapa/personagem alterado", "Map/character changed", "Mapa/personaje cambiado", "Karte/Charakter geändert", "Carte/personnage modifié");
    case "roleplay-control-lost": return pick("Controle do personagem perdido", "Character control lost", "Control del personaje perdido", "Charaktersteuerung verloren", "Contrôle du personnage perdu");
    case "roleplay-bus-too-far": return pick("Aproxime-se do ônibus para entrar", "Move closer to the bus to enter", "Acércate al autobús para entrar", "Gehe näher zum Bus, um einzusteigen", "Rapprochez-vous du bus pour entrer");
    case "roleplay-bus-position-unavailable": return pick("Posição do ônibus indisponível", "Bus position unavailable", "Posición del autobús no disponible", "Busposition nicht verfügbar", "Position du bus indisponible");
    case "roleplay-interaction-triggered": return pick("Interação enviada ao ônibus", "Bus interaction sent", "Interacción enviada al autobús", "Bus-Interaktion ausgelöst", "Interaction envoyée au bus");
    case "roleplay-interaction-too-far": return pick("Aproxime-se do ônibus para interagir", "Move closer to the bus to interact", "Acércate al autobús para interactuar", "Gehe näher zum Bus, um zu interagieren", "Rapprochez-vous du bus pour interagir");
    case "roleplay-interaction-plugin-unavailable": return pick("Plugin Bridge sem suporte a interações RP", "Plugin Bridge has no RP interaction support", "Plugin Bridge sin soporte de interacción RP", "Plugin Bridge ohne RP-Interaktionsunterstützung", "Plugin Bridge sans prise en charge des interactions RP");
    case "roleplay-interaction-busy": return pick("Outra interação ainda está em andamento", "Another interaction is still in progress", "Otra interacción sigue en curso", "Eine andere Interaktion läuft noch", "Une autre interaction est encore en cours");
    case "roleplay-interaction-invalid": return pick("Interação inválida", "Invalid interaction", "Interacción inválida", "Ungültige Interaktion", "Interaction invalide");
    case "roleplay-interaction-not-in-catalog": return pick("Esse evento não existe mais no addon atual", "That event no longer exists in the current addon", "Ese evento ya no existe en el addon actual", "Dieses Ereignis existiert im aktuellen Add-on nicht mehr", "Cet événement n’existe plus dans l’addon actuel");
    case "roleplay-interaction-unavailable": return pick("Interação RP indisponível", "RP interaction unavailable", "Interacción RP no disponible", "RP-Interaktion nicht verfügbar", "Interaction RP indisponible");
    case "roleplay-bus-changed": return pick("O ônibus original do RP não é mais o veículo do jogador", "The original RP bus is no longer the player vehicle", "El autobús RP original ya no es el vehículo del jugador", "Der ursprüngliche RP-Bus ist nicht mehr das Spielerfahrzeug", "Le bus RP d’origine n’est plus le véhicule du joueur");
    case "roleplay-interaction-position-unavailable": return pick("Não foi possível validar a posição para interagir", "Could not validate position for interaction", "No se pudo validar la posición para interactuar", "Position für die Interaktion konnte nicht geprüft werden", "Impossible de valider la position pour l’interaction");
    case "roleplay-trigger-failed": return pick("O OMSI rejeitou a interação", "OMSI rejected the interaction", "OMSI rechazó la interacción", "OMSI hat die Interaktion abgelehnt", "OMSI a rejeté l’interaction");
    case "roleplay-trigger-release-failed": return pick("O acionamento ocorreu, mas a liberação do evento não foi confirmada", "The trigger fired, but release was not confirmed", "El evento se activó, pero no se confirmó su liberación", "Der Trigger wurde ausgelöst, aber das Loslassen wurde nicht bestätigt", "Le déclencheur a été activé, mais son relâchement n’a pas été confirmé");
    case "roleplay-entered-bus": return pick("Retornou ao ônibus", "Returned to the bus", "Volvió al autobús", "Zum Bus zurückgekehrt", "Retour au bus");
    case "roleplay-emergency-return": return pick("RP encerrado pelo retorno de emergência", "RP ended by emergency return", "RP finalizado por retorno de emergencia", "RP durch Notfall-Rückkehr beendet", "RP terminé par retour d’urgence");
    case "roleplay-release-failed": return pick("O RP foi encerrado, mas o plugin não confirmou a restauração do motorista", "RP ended, but the plugin did not confirm driver restoration", "El RP terminó, pero el plugin no confirmó la restauración del conductor", "RP wurde beendet, aber das Plugin bestätigte die Fahrerwiederherstellung nicht", "Le RP est terminé, mais le plugin n’a pas confirmé la restauration du conducteur");
    case "driver-restore-failed": return pick("O OMSI recusou a restauração do motorista; o NavBR tentou novamente e manteve o erro visível", "OMSI rejected driver restoration; NavBR retried and kept the error visible", "OMSI rechazó la restauración del conductor; NavBR volvió a intentarlo y mantuvo visible el error", "OMSI hat die Fahrerwiederherstellung abgelehnt; NavBR hat erneut versucht und den Fehler sichtbar gehalten", "OMSI a refusé la restauration du conducteur ; NavBR a réessayé et a conservé l’erreur visible");
    case "driver-restore-unconfirmed": return pick("O motorista foi restaurado, mas o OMSI não confirmou o estado final", "The driver was restored, but OMSI did not confirm the final state", "El conductor fue restaurado, pero OMSI no confirmó el estado final", "Der Fahrer wurde wiederhergestellt, aber OMSI bestätigte den Endzustand nicht", "Le conducteur a été restauré, mais OMSI n’a pas confirmé l’état final");
    case "driver-pointer-stale": return pick("O personagem do motorista não existe mais na lista ativa do OMSI", "The driver character no longer exists in OMSI's active list", "El personaje del conductor ya no existe en la lista activa de OMSI", "Die Fahrerfigur existiert nicht mehr in der aktiven OMSI-Liste", "Le personnage conducteur n’existe plus dans la liste active d’OMSI");
    case "driver-detach-failed": return pick("O OMSI recusou retirar o motorista do ônibus", "OMSI rejected detaching the driver from the bus", "OMSI rechazó separar al conductor del autobús", "OMSI hat das Lösen des Fahrers vom Bus abgelehnt", "OMSI a refusé de détacher le conducteur du bus");
    case "driver-detach-unconfirmed": return pick("O motorista saiu do ônibus, mas o estado não foi confirmado", "The driver left the bus, but the state was not confirmed", "El conductor salió del autobús, pero no se confirmó el estado", "Der Fahrer hat den Bus verlassen, aber der Zustand wurde nicht bestätigt", "Le conducteur a quitté le bus, mais l’état n’a pas été confirmé");
    case "driver-transform-unconfirmed": return pick("O OMSI não confirmou a posição física do personagem", "OMSI did not confirm the character's physical position", "OMSI no confirmó la posición física del personaje", "OMSI bestätigte die physische Position der Figur nicht", "OMSI n’a pas confirmé la position physique du personnage");
    case "selected-driver-too-far": return pick("O motorista selecionado está longe demais do ônibus ativo", "The selected driver is too far from the active bus", "El conductor seleccionado está demasiado lejos del autobús activo", "Der ausgewählte Fahrer ist zu weit vom aktiven Bus entfernt", "Le conducteur sélectionné est trop éloigné du bus actif");
    case "roleplay-writes-disabled": return pick("As escritas experimentais de RP estão desativadas", "Experimental RP writes are disabled", "Las escrituras experimentales de RP están desactivadas", "Experimentelle RP-Schreibzugriffe sind deaktiviert", "Les écritures RP expérimentales sont désactivées");
    case "roleplay-session-ended": return pick("Sessão/mapa do OMSI encerrado · personagem restaurado", "OMSI session/map ended · character restored", "Sesión/mapa de OMSI finalizado · personaje restaurado", "OMSI-Sitzung/Karte beendet · Charakter wiederhergestellt", "Session/carte OMSI terminée · personnage restauré");
    case "roleplay-plugin-disconnected": return pick("Plugin OMSI desconectado · RP encerrado com segurança", "OMSI plugin disconnected · RP stopped safely", "Plugin OMSI desconectado · RP detenido con seguridad", "OMSI-Plugin getrennt · RP sicher beendet", "Plugin OMSI déconnecté · RP arrêté en sécurité");
    case "roleplay-disabled": return pick("Recurso RP desativado", "RP feature disabled", "Función RP desactivada", "RP-Funktion deaktiviert", "Fonction RP désactivée");
    case "roleplay-enabled": return pick("Recurso RP ativado", "RP feature enabled", "Función RP activada", "RP-Funktion aktiviert", "Fonction RP activée");
    default: return status || pick("Pronto", "Ready", "Listo", "Bereit", "Prêt");
  }
}

function humanAiModeLabel(value: number) {
  const names = [
    "THAM_Stop",
    "THAM_WalkToTarget",
    "THAM_WaitBeforeTarget",
    "THAM_AtTarget",
    "THAM_TooFar",
    "THAM_WalkToPathTarget",
    "THAM_WaitOnPath",
    "THAM_AtPathTarget",
    "THAM_WalkStreet",
    "THAM_Stand"
  ];
  return names[value] ? `${names[value]} (${value})` : `Unknown (${value})`;
}

function humanAiModeExLabel(value: number) {
  const names = [
    "THAME_DoNothing",
    "THAME_WaitingForBus",
    "THAME_WalkingToBusPre",
    "THAME_WalkingToBus",
    "THAME_WalkingInBusToPlace",
    "THAME_WalkingInBusToExit",
    "THAME_WalkingToBusstop",
    "THAME_SittingInBus",
    "THAME_WalkStreet",
    "THAME_DrivingBus"
  ];
  return names[value] ? `${names[value]} (${value})` : `Unknown (${value})`;
}

function humanAiSubModeLabel(value: number) {
  const names = [
    "None",
    "WaitForStamper",
    "Stamp",
    "WaitForTicketBuy",
    "WaitForGeldabwurf",
    "WaitForTicketAndChange",
    "WaitForTakingTicket",
    "WaitForChange",
    "FinishedBuyingTicket"
  ];
  return names[value] ? `${names[value]} (${value})` : `Unknown (${value})`;
}

function RoleplayPanel({
  state,
  error,
  embedded = false
}: {
  state: NavBrState | null;
  error: string | null;
  embedded?: boolean;
}) {
  const { pick } = useI18n();
  const roleplay = state?.roleplay;
  const multiplayer = state?.multiplayer ?? fallbackMultiplayer;
  const [interactionFilter, setInteractionFilter] = useState("");

  if (!roleplay) {
    return <div className="card empty-state">{pick("Aguardando estado do Personagem / RP…", "Waiting for Character / RP state…", "Esperando el estado del Personaje / RP…", "Warte auf Charakter-/RP-Status…", "En attente de l’état Personnage / RP…")}</div>;
  }

  // "Sair do ônibus" is itself the explicit RP opt-in. Do not require the
  // write flag/runtimeAvailable before the first click, otherwise the UI can
  // deadlock with the button disabled before C# has a chance to enable RP.
  const canStart = roleplay.mapReady && !roleplay.active;
  const current = roleplay.current;
  const normalizedInteractionFilter = interactionFilter.trim().toLowerCase();
  const filteredInteractions = normalizedInteractionFilter
    ? roleplay.interactions.filter(interaction =>
        interaction.name.toLowerCase().includes(normalizedInteractionFilter)
      )
    : roleplay.interactions;

  return (
    <>
      {!embedded && (
        <header className="topbar roleplay-header">
          <div>
            <span className="eyebrow">{pick("PERSONAGEM / RP", "CHARACTER / RP", "PERSONAJE / RP", "CHARAKTER / RP", "PERSONNAGE / RP")}</span>
            <h1>{pick("Motorista fora do ônibus", "Driver outside the bus", "Conductor fuera del autobús", "Fahrer außerhalb des Busses", "Conducteur hors du bus")}</h1>
            <p>{pick("Seleção e controle do personagem usando o estado real do OMSI e do Plugin Bridge v3.", "Character selection and control using real OMSI and Plugin Bridge v3 state.", "Selección y control del personaje usando el estado real de OMSI y Plugin Bridge v3.", "Charakterauswahl und -steuerung mit echtem OMSI- und Plugin-Bridge-v3-Status.", "Sélection et contrôle du personnage à partir de l’état réel d’OMSI et de Plugin Bridge v3.")}</p>
          </div>
          <div className="top-actions">
            <span className={`connection-pill ${roleplay.active ? "connected" : ""}`}>
              <i /> {roleplay.active ? pick("Fora do ônibus", "Outside the bus", "Fuera del autobús", "Außerhalb des Busses", "Hors du bus") : pick("No ônibus", "In the bus", "En el autobús", "Im Bus", "Dans le bus")}
            </span>
          </div>
        </header>
      )}

      {error && <div className="command-error">{error}</div>}

      <section className={embedded ? "rp-web-grid embedded" : "rp-web-grid"}>
        <article className="card rp-status-card">
          <div className="section-heading">
            <div>
              <span className="eyebrow">{pick("ESTADO", "STATE", "ESTADO", "STATUS", "ÉTAT")}</span>
              <h3>{roleplay.active ? current?.characterName || roleplay.selected?.displayName || pick("Personagem ativo", "Character active", "Personaje activo", "Charakter aktiv", "Personnage actif") : roleplay.selected?.displayName || pick("Nenhum personagem selecionado", "No character selected", "Ningún personaje seleccionado", "Kein Charakter ausgewählt", "Aucun personnage sélectionné")}</h3>
            </div>
            <span className={`hardware-state-pill ${roleplay.runtimeAvailable ? "connected" : ""}`}>
              {roleplay.runtimeAvailable ? pick("Integração disponível", "Integration available", "Integración disponible", "Integration verfügbar", "Intégration disponible") : pick("Integração indisponível", "Integration unavailable", "Integración no disponible", "Integration nicht verfügbar", "Intégration indisponible")}
            </span>
          </div>

          <label className="rp-enable-toggle">
            <input
              type="checkbox"
              checked={roleplay.enabled}
              onChange={event => sendCommand("setRoleplayEnabled", { enabled: event.target.checked })}
            />
            <span>
              <strong>{pick("Ativar Personagem / RP", "Enable Character / RP", "Activar Personaje / RP", "Charakter / RP aktivieren", "Activer Personnage / RP")}</strong>
              <small>{pick("Habilita o modo experimental sem depender da janela Multiplayer WPF.", "Enables experimental mode without depending on the WPF Multiplayer window.", "Activa el modo experimental sin depender de la ventana Multiplayer WPF.", "Aktiviert den experimentellen Modus ohne Abhängigkeit vom WPF-Multiplayerfenster.", "Active le mode expérimental sans dépendre de la fenêtre Multiplayer WPF.")}</small>
            </span>
          </label>

          {roleplay.errorMessage && !roleplay.active && (
            <div className="command-error rp-backend-error">
              <strong>{roleplay.errorCode || pick("Falha no RP", "RP failure", "Fallo de RP", "RP-Fehler", "Échec RP")}</strong>
              <span>{roleplay.errorMessage}</span>
            </div>
          )}

          <div className="details-grid rp-status-grid">
            <div><small>{pick("MAPA", "MAP", "MAPA", "KARTE", "CARTE")}</small><strong>{state?.telemetry?.mapName || "—"}</strong></div>
            <div><small>STATUS</small><strong>{roleplayStatusLabel(roleplay.status, pick)}</strong></div>
            <div><small>{pick("MAPA PRONTO", "MAP READY", "MAPA LISTO", "KARTE BEREIT", "CARTE PRÊTE")}</small><strong>{roleplay.mapReady ? pick("Sim", "Yes", "Sí", "Ja", "Oui") : pick("Não", "No", "No", "Nein", "Non")}</strong></div>
            <div><small>MULTIPLAYER</small><strong>{multiplayer.connected ? multiplayer.roomId : pick("Não conectado", "Not connected", "No conectado", "Nicht verbunden", "Non connecté")}</strong></div>
            <div><small>{pick("TERRENO", "TERRAIN", "TERRENO", "GELÄNDE", "TERRAIN")}</small><strong>{roleplay.active ? roleplay.terrainFollowing ? pick("Seguindo spline", "Following spline", "Siguiendo spline", "Spline-Folge aktiv", "Suivi de spline") : pick("Altura preservada", "Height preserved", "Altura conservada", "Höhe beibehalten", "Hauteur conservée") : "—"}</strong></div>
            <div><small>{pick("MOVIMENTO", "MOVEMENT", "MOVIMIENTO", "BEWEGUNG", "MOUVEMENT")}</small><strong>{roleplay.active ? roleplay.cameraRelativeMovement ? pick("Câmera openOMSI", "openOMSI camera", "Cámara openOMSI", "openOMSI-Kamera", "Caméra openOMSI") : pick("Fallback heading", "Heading fallback", "Fallback de dirección", "Heading-Fallback", "Fallback direction") : "—"}</strong></div>
            <div><small>{pick("DISTÂNCIA DO ÔNIBUS", "BUS DISTANCE", "DISTANCIA DEL AUTOBÚS", "BUS-ENTFERNUNG", "DISTANCE DU BUS")}</small><strong>{roleplay.active ? `${format(roleplay.busDistanceMeters, 1)} m` : "—"}</strong></div>
          </div>

          {current && (
            <div className="rp-current-state">
              <span><small>{pick("ATIVIDADE", "ACTIVITY", "ACTIVIDAD", "AKTIVITÄT", "ACTIVITÉ")}</small><strong>{current.activity}</strong></span>
              <span><small>{pick("VELOCIDADE", "SPEED", "VELOCIDAD", "GESCHWINDIGKEIT", "VITESSE")}</small><strong>{format(current.speedMps * 3.6, 1)} km/h</strong></span>
              <span><small>{pick("DIREÇÃO", "HEADING", "DIRECCIÓN", "RICHTUNG", "DIRECTION")}</small><strong>{format(current.headingDegrees, 0)}°</strong></span>
              <span><small>HUMAN INDEX</small><strong>{current.humanIndex ?? "—"}</strong></span>
            </div>
          )}

          {roleplay.active && roleplay.nativeAnimation && (
            <>
              <div className="section-heading">
                <div>
                  <span className="eyebrow">{pick("TELEMETRIA DE ANIMAÇÃO", "ANIMATION TELEMETRY", "TELEMETRÍA DE ANIMACIÓN", "ANIMATIONS-TELEMETRIE", "TÉLÉMÉTRIE D’ANIMATION")}</span>
                  <h3>{pick("Proveniência dos estados do humano OMSI", "OMSI human state provenance", "Procedencia de los estados del humano OMSI", "Herkunft der OMSI-Human-Zustände", "Provenance des états du personnage OMSI")}</h3>
                </div>
                <span className="hardware-state-pill connected">{pick("Proveniência explícita", "Explicit provenance", "Procedencia explícita", "Explizite Herkunft", "Provenance explicite")}</span>
              </div>
              <div className="details-grid rp-status-grid">
                <div><small>AI MODE · NAVBR → OMSI</small><strong>{humanAiModeLabel(roleplay.nativeAnimation.aiMode)}</strong></div>
                <div><small>AI MODE EX · NAVBR → OMSI</small><strong>{humanAiModeExLabel(roleplay.nativeAnimation.aiModeEx)}</strong></div>
                <div><small>AI SUBMODE · NAVBR → OMSI</small><strong>{humanAiSubModeLabel(roleplay.nativeAnimation.aiSubMode)}</strong></div>
                <div><small>LAST MOVED · NAVBR → OMSI</small><strong>{format(roleplay.nativeAnimation.lastMovedDistanceMeters, 3)} m</strong></div>
                <div><small>STATE RAW · NAVBR → OMSI</small><strong>{format(roleplay.nativeAnimation.animationState, 3)}</strong></div>
                <div><small>SOLL / ACT · NAVBR → OMSI</small><strong>{format(roleplay.nativeAnimation.sollSpeedMps, 2)} / {format(roleplay.nativeAnimation.actSpeedMps, 2)} m/s</strong></div>
                <div><small>ACTIVITY LEG · OMSI OBSERVED</small><strong>{roleplay.nativeAnimation.activityLegRaw ?? "—"}</strong></div>
                <div><small>ARM UMBRELLA · OMSI OBSERVED</small><strong>{roleplay.nativeAnimation.activityArmUmbrellaRaw ?? "—"}</strong></div>
                <div><small>ARM KI · OMSI OBSERVED</small><strong>{roleplay.nativeAnimation.activityArmKiRaw ?? "—"}</strong></div>
                <div><small>HEAD KI · OMSI OBSERVED</small><strong>{roleplay.nativeAnimation.activityHeadKiRaw ?? "—"}</strong></div>
              </div>

              {roleplay.nativeActivityObservation && (
                <>
                  <div className="section-heading">
                    <div>
                      <span className="eyebrow">{pick("OBSERVAÇÃO INDEPENDENTE", "INDEPENDENT OBSERVATION", "OBSERVACIÓN INDEPENDIENTE", "UNABHÄNGIGE BEOBACHTUNG", "OBSERVATION INDÉPENDANTE")}</span>
                      <h3>{pick("Transições reais de Activity_*", "Real Activity_* transitions", "Transiciones reales de Activity_*", "Echte Activity_*-Übergänge", "Transitions réelles Activity_*")}</h3>
                    </div>
                  </div>
                  <div className="details-grid rp-status-grid">
                    <div><small>{pick("AMOSTRAS", "SAMPLES", "MUESTRAS", "SAMPLES", "ÉCHANTILLONS")}</small><strong>{roleplay.nativeActivityObservation.samples}</strong></div>
                    <div><small>{pick("AMOSTRAS EM MOVIMENTO", "MOVING SAMPLES", "MUESTRAS EN MOVIMIENTO", "SAMPLES IN BEWEGUNG", "ÉCHANTILLONS EN MOUVEMENT")}</small><strong>{roleplay.nativeActivityObservation.movingSamples}</strong></div>
                    <div><small>{pick("TRANSIÇÕES RAW", "RAW TRANSITIONS", "TRANSICIONES RAW", "RAW-ÜBERGÄNGE", "TRANSITIONS BRUTES")}</small><strong>{roleplay.nativeActivityObservation.transitionCount}</strong></div>
                    <div><small>{pick("TRANSIÇÕES DURANTE MOVIMENTO", "TRANSITIONS WHILE MOVING", "TRANSICIONES DURANTE MOVIMIENTO", "ÜBERGÄNGE BEI BEWEGUNG", "TRANSITIONS EN MOUVEMENT")}</small><strong>{roleplay.nativeActivityObservation.movingTransitionCount}</strong></div>
                    <div><small>{pick("MUDOU NESTA AMOSTRA", "CHANGED THIS SAMPLE", "CAMBIÓ EN ESTA MUESTRA", "IN DIESEM SAMPLE GEÄNDERT", "MODIFIÉ CET ÉCHANTILLON")}</small><strong>{roleplay.nativeActivityObservation.changedThisFrame ? pick("Sim", "Yes", "Sí", "Ja", "Oui") : pick("Não", "No", "No", "Nein", "Non")}</strong></div>
                    <div><small>{pick("ÚLTIMA TRANSIÇÃO", "LAST TRANSITION", "ÚLTIMA TRANSICIÓN", "LETZTER ÜBERGANG", "DERNIÈRE TRANSITION")}</small><strong>{roleplay.nativeActivityObservation.lastTransitionAtUtc ? new Date(roleplay.nativeActivityObservation.lastTransitionAtUtc).toLocaleTimeString() : "—"}</strong></div>
                  </div>
                </>
              )}
              <p className="migration-note">
                {pick(
                  "AI modes, velocidades, LastMovedDist e State refletem valores dirigidos pelo controle RP do NavBR e não contam como validação independente. Apenas Activity_* é observado sem escrita do NavBR; as transições são contadas sem atribuir significado de gesto.",
                  "AI modes, speeds, LastMovedDist and State reflect values driven by NavBR RP control and do not count as independent validation. Only Activity_* is observed without NavBR writes; transitions are counted without assigning gesture meaning.",
                  "Los modos AI, velocidades, LastMovedDist y State reflejan valores dirigidos por el control RP de NavBR y no cuentan como validación independiente. Solo Activity_* se observa sin escrituras de NavBR; las transiciones se cuentan sin asignar significado de gesto.",
                  "AI-Modi, Geschwindigkeiten, LastMovedDist und State spiegeln vom NavBR-RP gesteuerte Werte wider und gelten nicht als unabhängige Validierung. Nur Activity_* wird ohne NavBR-Schreibzugriff beobachtet; Übergänge werden ohne Gesteninterpretation gezählt.",
                  "Les modes AI, vitesses, LastMovedDist et State reflètent des valeurs pilotées par le contrôle RP de NavBR et ne constituent pas une validation indépendante. Seuls Activity_* sont observés sans écriture NavBR ; les transitions sont comptées sans interprétation gestuelle."
                )}
              </p>
            </>
          )}

          <div className="action-row rp-actions">
            {roleplay.active ? (
              <>
                <button className="button primary" disabled={!roleplay.canEnterBus} onClick={() => sendCommand("enterRoleplayBus")}>
                  {pick("Entrar no ônibus", "Enter bus", "Entrar al autobús", "In den Bus einsteigen", "Entrer dans le bus")}
                </button>
                <button className="button ghost" onClick={() => sendCommand("stopRoleplay")}>
                  {pick("Retorno de emergência", "Emergency return", "Retorno de emergencia", "Notfall-Rückkehr", "Retour d’urgence")}
                </button>
              </>
            ) : (
              <button className="button primary" disabled={!canStart} onClick={() => sendCommand("startRoleplay")}>{pick("Sair do ônibus", "Leave bus", "Salir del autobús", "Bus verlassen", "Sortir du bus")}</button>
            )}
            <button className="button ghost" onClick={() => sendCommand("refreshState")}>{pick("Atualizar catálogo", "Refresh catalog", "Actualizar catálogo", "Katalog aktualisieren", "Actualiser le catalogue")}</button>
          </div>

          {!roleplay.enabled && <p className="migration-note">{pick("O modo Personagem / RP está desativado nas configurações experimentais.", "Character / RP mode is disabled in experimental settings.", "El modo Personaje / RP está desactivado en la configuración experimental.", "Charakter-/RP-Modus ist in den experimentellen Einstellungen deaktiviert.", "Le mode Personnage / RP est désactivé dans les paramètres expérimentaux.")}</p>}
          {roleplay.enabled && !roleplay.mapReady && <p className="migration-note">{pick("Entre em um mapa do OMSI para carregar os personagens reais de Map.Drivers.", "Enter an OMSI map to load real Map.Drivers characters.", "Entra en un mapa de OMSI para cargar los personajes reales de Map.Drivers.", "Öffne eine OMSI-Karte, um echte Map.Drivers-Charaktere zu laden.", "Entrez dans une carte OMSI pour charger les personnages réels de Map.Drivers.")}</p>}
          {roleplay.mapReady && !roleplay.runtimeAvailable && <p className="migration-note">{pick("Atualize a integração do OMSI para usar o modo Personagem / RP neste mapa.", "Update the OMSI integration to use Character / RP mode on this map.", "Actualiza la integración de OMSI para usar el modo Personaje / RP en este mapa.", "Aktualisiere die OMSI-Integration, um den Charakter-/RP-Modus auf dieser Karte zu verwenden.", "Mettez à jour l’intégration OMSI pour utiliser le mode Personnage / RP sur cette carte.")}</p>}
        </article>

        <article className="card rp-character-card rp-character-selection-card">
          <div className="section-heading">
            <div><span className="eyebrow">MAP.DRIVERS</span><h3>{pick("Personagens disponíveis", "Available characters", "Personajes disponibles", "Verfügbare Charaktere", "Personnages disponibles")}</h3></div>
            <span className="stop-count">{roleplay.characters.length}</span>
          </div>

          {roleplay.characters.length === 0 ? (
            <div className="empty-state">{pick("Nenhum personagem disponível para o mapa atual.", "No character available for the current map.", "Ningún personaje disponible para el mapa actual.", "Kein Charakter für die aktuelle Karte verfügbar.", "Aucun personnage disponible pour la carte actuelle.")}</div>
          ) : (
            <div className="rp-character-list">
              {roleplay.characters.map(character => (
                <button
                  key={character.id}
                  className={`rp-character-row ${character.selected ? "selected" : ""}`}
                  disabled={roleplay.active || !character.isActiveDriver}
                  onClick={() => sendCommand("selectRoleplayCharacter", { characterId: character.id })}
                >
                  <span className="rp-character-avatar"><NavBrIcon name="character" size={18} /></span>
                  <span>
                    <strong>{character.displayName}</strong>
                    <small>{character.isActiveDriver ? pick("Motorista ativo do mapa", "Active map driver", "Conductor activo del mapa", "Aktiver Kartenfahrer", "Conducteur actif de la carte") : character.sourceValue}</small>
                  </span>
                  <em>{character.selected
                    ? pick("Selecionado", "Selected", "Seleccionado", "Ausgewählt", "Sélectionné")
                    : character.isActiveDriver
                      ? pick("Usar", "Use", "Usar", "Verwenden", "Utiliser")
                      : pick("Catálogo", "Catalog", "Catálogo", "Katalog", "Catalogue")}</em>
                </button>
              ))}
            </div>
          )}
          <p className="hardware-note">{pick("A seleção é válida somente para a sessão/mapa atual. O DefinitionPointer nativo não é persistido.", "Selection is valid only for the current session/map. The native DefinitionPointer is not persisted.", "La selección solo es válida para la sesión/mapa actual. El DefinitionPointer nativo no se conserva.", "Die Auswahl gilt nur für die aktuelle Sitzung/Karte. Der native DefinitionPointer wird nicht gespeichert.", "La sélection n’est valable que pour la session/carte actuelle. Le DefinitionPointer natif n’est pas persisté.")}</p>
        </article>

        {roleplay.active && (
          <article className="card rp-character-card">
            <div className="section-heading">
              <div>
                <span className="eyebrow">[MOUSEEVENT]</span>
                <h3>{pick("Interações do ônibus", "Bus interactions", "Interacciones del autobús", "Bus-Interaktionen", "Interactions du bus")}</h3>
              </div>
              <span className="stop-count">{roleplay.interactions.length}</span>
            </div>

            <div className="rp-interaction-toolbar">
              <input
                type="search"
                value={interactionFilter}
                onChange={event => setInteractionFilter(event.target.value)}
                placeholder={pick("Filtrar pelo nome real do evento…", "Filter by the real event name…", "Filtrar por el nombre real del evento…", "Nach echtem Ereignisnamen filtern…", "Filtrer par le nom réel de l’événement…")}
                aria-label={pick("Filtrar interações do ônibus", "Filter bus interactions", "Filtrar interacciones del autobús", "Bus-Interaktionen filtern", "Filtrer les interactions du bus")}
              />
              <span className={`rp-interaction-proximity ${roleplay.canInteractWithBus ? "ready" : ""}`}>
                {roleplay.busDistanceMeters == null
                  ? pick("Distância indisponível", "Distance unavailable", "Distancia no disponible", "Entfernung nicht verfügbar", "Distance indisponible")
                  : `${format(roleplay.busDistanceMeters, 1)} / ${format(roleplay.interactionRangeMeters, 0)} m`}
              </span>
            </div>

            {roleplay.lastInteraction && (
              <div className={`rp-interaction-feedback ${roleplay.lastInteraction.succeeded ? "success" : "failed"}`}>
                <span>
                  <small>{pick("ÚLTIMA INTERAÇÃO", "LAST INTERACTION", "ÚLTIMA INTERACCIÓN", "LETZTE INTERAKTION", "DERNIÈRE INTERACTION")}</small>
                  <strong>{roleplay.lastInteraction.name}</strong>
                </span>
                <em>
                  {roleplay.lastInteraction.succeeded
                    ? pick("Confirmada pelo bridge", "Confirmed by the bridge", "Confirmada por el bridge", "Vom Bridge bestätigt", "Confirmée par le bridge")
                    : roleplayStatusLabel(roleplay.lastInteraction.status, pick)}
                </em>
              </div>
            )}

            {!roleplay.interactionRuntimeAvailable ? (
              <div className="empty-state">{pick("O Plugin Bridge atual ainda não anuncia suporte a interações RP.", "The current Plugin Bridge does not yet advertise RP interaction support.", "El Plugin Bridge actual todavía no anuncia soporte para interacciones RP.", "Der aktuelle Plugin Bridge meldet noch keine RP-Interaktionsunterstützung.", "Le Plugin Bridge actuel n’annonce pas encore la prise en charge des interactions RP.")}</div>
            ) : roleplay.interactions.length === 0 ? (
              <div className="empty-state">{pick("Nenhum [mouseevent] real foi encontrado nos model.cfg deste veículo.", "No real [mouseevent] was found in this vehicle's model.cfg files.", "No se encontró ningún [mouseevent] real en los model.cfg de este vehículo.", "In den model.cfg-Dateien dieses Fahrzeugs wurde kein echtes [mouseevent] gefunden.", "Aucun [mouseevent] réel n’a été trouvé dans les model.cfg de ce véhicule.")}</div>
            ) : filteredInteractions.length === 0 ? (
              <div className="empty-state">{pick("Nenhum evento real corresponde ao filtro.", "No real event matches the filter.", "Ningún evento real coincide con el filtro.", "Kein echtes Ereignis entspricht dem Filter.", "Aucun événement réel ne correspond au filtre.")}</div>
            ) : (
              <div className="rp-character-list">
                {filteredInteractions.map(interaction => (
                  <button
                    key={interaction.name}
                    className="rp-character-row"
                    disabled={!roleplay.canInteractWithBus}
                    onClick={() => sendCommand("triggerRoleplayVehicle", { triggerName: interaction.name })}
                  >
                    <span className="rp-character-avatar"><NavBrIcon name="action" size={18} /></span>
                    <span>
                      <strong>{interaction.name}</strong>
                      <small>{pick("Evento real declarado pelo addon", "Real event declared by the addon", "Evento real declarado por el addon", "Vom Add-on deklariertes echtes Ereignis", "Événement réel déclaré par l’addon")}</small>
                    </span>
                    <em>{pick("Acionar", "Trigger", "Activar", "Auslösen", "Déclencher")}</em>
                  </button>
                ))}
              </div>
            )}

            {!roleplay.canInteractWithBus && roleplay.interactionRuntimeAvailable && roleplay.interactions.length > 0 && (
              <p className="rp-interaction-blocked">
                {roleplay.busDistanceMeters == null
                  ? pick("A posição real do ônibus ainda não pôde ser validada.", "The real bus position could not be validated yet.", "La posición real del autobús aún no se pudo validar.", "Die echte Busposition konnte noch nicht geprüft werden.", "La position réelle du bus n’a pas encore pu être validée.")
                  : pick(
                      `Aproxime-se até ${roleplay.interactionRangeMeters.toFixed(0)} m do ônibus original para liberar os eventos.`,
                      `Move within ${roleplay.interactionRangeMeters.toFixed(0)} m of the original bus to enable events.`,
                      `Acércate a menos de ${roleplay.interactionRangeMeters.toFixed(0)} m del autobús original para habilitar los eventos.`,
                      `Gehe bis auf ${roleplay.interactionRangeMeters.toFixed(0)} m an den ursprünglichen Bus heran, um Ereignisse freizugeben.`,
                      `Approchez-vous à moins de ${roleplay.interactionRangeMeters.toFixed(0)} m du bus d’origine pour activer les événements.`
                    )}
              </p>
            )}

            <p className="hardware-note">
              {pick(
                `Os eventos vêm diretamente dos [mouseevent] do ônibus ativo. O acionamento só é liberado a até ${roleplay.interactionRangeMeters.toFixed(0)} m do ônibus original do RP.`,
                `Events come directly from the active bus [mouseevent] entries. Triggering is enabled only within ${roleplay.interactionRangeMeters.toFixed(0)} m of the original RP bus.`,
                `Los eventos provienen directamente de los [mouseevent] del autobús activo. Solo se pueden activar a menos de ${roleplay.interactionRangeMeters.toFixed(0)} m del autobús RP original.`,
                `Die Ereignisse stammen direkt aus den [mouseevent]-Einträgen des aktiven Busses. Auslösen ist nur innerhalb von ${roleplay.interactionRangeMeters.toFixed(0)} m vom ursprünglichen RP-Bus möglich.`,
                `Les événements proviennent directement des entrées [mouseevent] du bus actif. Le déclenchement n’est autorisé qu’à moins de ${roleplay.interactionRangeMeters.toFixed(0)} m du bus RP d’origine.`
              )}
            </p>
          </article>
        )}
      </section>

      {!embedded && (
        <section className="card rp-controls-card">
          <div className="section-heading">
            <div><span className="eyebrow">{pick("CONTROLES", "CONTROLS", "CONTROLES", "STEUERUNG", "COMMANDES")}</span><h3>{pick("Durante o RP", "During RP", "Durante RP", "Während RP", "Pendant le RP")}</h3></div>
          </div>
          <div className="rp-key-grid">
            <span><kbd>W</kbd><strong>{pick("Andar para frente", "Walk forward", "Caminar hacia delante", "Vorwärts gehen", "Marcher en avant")}</strong></span>
            <span><kbd>S</kbd><strong>{pick("Andar para trás", "Walk backward", "Caminar hacia atrás", "Rückwärts gehen", "Marcher en arrière")}</strong></span>
            <span><kbd>A / D</kbd><strong>{pick("Virar", "Turn", "Girar", "Drehen", "Tourner")}</strong></span>
            <span><kbd>Shift</kbd><strong>{pick("Correr", "Run", "Correr", "Laufen", "Courir")}</strong></span>
            <span><kbd>E</kbd><strong>{pick("Entrar no ônibus quando estiver próximo", "Enter the bus when nearby", "Entrar al autobús cuando esté cerca", "In den Bus einsteigen, wenn er nahe ist", "Entrer dans le bus à proximité")}</strong></span>
            <span><kbd>Esc</kbd><strong>{pick("Retorno de emergência", "Emergency return", "Retorno de emergencia", "Notfall-Rückkehr", "Retour d’urgence")}</strong></span>
          </div>
          <p>{pick("Os atalhos só são capturados quando o OMSI está em primeiro plano. Ao trocar de janela, o NavBR zera o movimento, solta as teclas RP e mantém o personagem parado até um novo comando. E exige proximidade real do ônibus; Esc permanece disponível como retorno de emergência.", "Shortcuts are captured only while OMSI is in the foreground. When focus changes, NavBR zeroes movement, releases RP keys, and keeps the character stopped until a new command. E requires real bus proximity; Esc remains available as an emergency return.", "Los atajos solo se capturan cuando OMSI está en primer plano. Al cambiar de ventana, NavBR detiene el movimiento, libera las teclas RP y mantiene al personaje parado hasta un nuevo comando. E requiere proximidad real al autobús; Esc sigue disponible como retorno de emergencia.", "Tastenkürzel werden nur erfasst, wenn OMSI im Vordergrund ist. Beim Fensterwechsel stoppt NavBR die Bewegung, löst die RP-Tasten und hält die Figur bis zu einer neuen Eingabe an. E erfordert echte Busnähe; Esc bleibt als Notfall-Rückkehr verfügbar.", "Les raccourcis ne sont capturés que lorsque OMSI est au premier plan. Lors d’un changement de fenêtre, NavBR annule le mouvement, libère les touches RP et maintient le personnage à l’arrêt jusqu’à une nouvelle commande. E exige une proximité réelle du bus ; Esc reste disponible comme retour d’urgence.")}</p>
        </section>
      )}
    </>
  );
}

function Roleplay({ state, error }: { state: NavBrState | null; error: string | null }) {
  return <RoleplayPanel state={state} error={error} />;
}

function Multiplayer({
  state,
  error,
  onOpenNetwork,
  onOpenHud
}: {
  state: NavBrState | null;
  error: string | null;
  onOpenNetwork: () => void;
  onOpenHud: () => void;
}) {
  const { pick } = useI18n();
  const multiplayer = state?.multiplayer ?? fallbackMultiplayer;
  const telemetry = state?.telemetry;
  const mobileControls = state?.system?.mobileCompanion;
  const [tab, setTab] = useState<MultiplayerTab>("overview");
  const [chatText, setChatText] = useState("");
  const chatLogRef = useRef<HTMLDivElement>(null);
  const [serverUrl, setServerUrl] = useState("");
  const [roomId, setRoomId] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [privateRoom, setPrivateRoom] = useState(false);
  const [roomPassword, setRoomPassword] = useState("");
  const [roomSearch, setRoomSearch] = useState("");
  const [voiceChannel, setVoiceChannel] = useState("general");
  const [voiceRadius, setVoiceRadius] = useState(120);
  const [voiceDeafened, setVoiceDeafened] = useState(false);
  const [voiceDirty, setVoiceDirty] = useState(false);
  const [voiceMixerDrafts, setVoiceMixerDrafts] = useState<Record<string, number>>({});
  const [relayEnabled, setRelayEnabled] = useState(false);
  const [relayServerUrl, setRelayServerUrl] = useState("");
  const [relayServerDirty, setRelayServerDirty] = useState(false);
  const [chatHotkey, setChatHotkey] = useState("F9");
  const [voiceHotkey, setVoiceHotkey] = useState("F10");
  const [hotkeysDirty, setHotkeysDirty] = useState(false);
  const [inviteNotice, setInviteNotice] = useState<string | null>(null);
  const [roomIntent, setRoomIntent] = useState<"create" | "join">("create");
  const [createRoomMode, setCreateRoomMode] = useState<"navbr" | "lan" | "host">("navbr");
  const defaultOnlineServer = "https://omsi-navbr-multiplayer-server.onrender.com";

  useEffect(() => {
    setServerUrl(current => current || multiplayer.serverUrl || defaultOnlineServer);
    setRoomId(current => current || multiplayer.roomId || "");
    setDisplayName(current => current || multiplayer.displayName || "");
  }, [multiplayer.serverUrl, multiplayer.roomId, multiplayer.displayName]);

  useEffect(() => {
    if (tab !== "chat") return;
    const log = chatLogRef.current;
    if (!log) return;

    const frame = window.requestAnimationFrame(() => {
      log.scrollTop = log.scrollHeight;
    });
    return () => window.cancelAnimationFrame(frame);
  }, [tab]);

  useEffect(() => {
    if (tab !== "chat") return;
    const log = chatLogRef.current;
    if (!log) return;

    const distanceFromBottom =
      log.scrollHeight - log.scrollTop - log.clientHeight;
    if (distanceFromBottom <= 120) {
      const frame = window.requestAnimationFrame(() => {
        log.scrollTop = log.scrollHeight;
      });
      return () => window.cancelAnimationFrame(frame);
    }
  }, [tab, multiplayer.chat.length]);

  useEffect(() => {
    const backendChannel = multiplayer.voiceChannel || "general";
    const backendRadius = multiplayer.voiceProximityMeters || 120;
    const backendDeafened = multiplayer.voiceDeafened;

    if (voiceDirty) {
      const settled =
        backendChannel === voiceChannel &&
        Math.abs(backendRadius - voiceRadius) < 0.5 &&
        backendDeafened === voiceDeafened;
      if (settled) {
        setVoiceDirty(false);
      }
      return;
    }

    setVoiceChannel(backendChannel);
    setVoiceRadius(backendRadius);
    setVoiceDeafened(backendDeafened);
  }, [
    multiplayer.voiceChannel,
    multiplayer.voiceProximityMeters,
    multiplayer.voiceDeafened,
    voiceDirty,
    voiceChannel,
    voiceRadius,
    voiceDeafened
  ]);

  useEffect(() => {
    setVoiceMixerDrafts(current => {
      let changed = false;
      const next = { ...current };
      const activeIds = new Set(multiplayer.voiceMixers.map(player => player.playerId));

      for (const playerId of Object.keys(next)) {
        const player = multiplayer.voiceMixers.find(item => item.playerId === playerId);
        if (!activeIds.has(playerId) || (player && Math.abs(player.gain - next[playerId]) < 0.01)) {
          delete next[playerId];
          changed = true;
        }
      }

      return changed ? next : current;
    });
  }, [multiplayer.voiceMixers]);

  useEffect(() => {
    setRelayEnabled(multiplayer.relayEnabled);
  }, [multiplayer.relayEnabled]);

  useEffect(() => {
    const backendRelayUrl = multiplayer.relayServerUrl || "";
    if (relayServerDirty) {
      if (backendRelayUrl === relayServerUrl) {
        setRelayServerDirty(false);
      }
      return;
    }

    setRelayServerUrl(backendRelayUrl);
  }, [
    multiplayer.relayServerUrl,
    relayServerDirty,
    relayServerUrl
  ]);

  useEffect(() => {
    const backendChatHotkey = multiplayer.chatHotkey || "F9";
    const backendVoiceHotkey = multiplayer.voiceHotkey || "F10";
    if (hotkeysDirty) {
      if (
        backendChatHotkey === chatHotkey &&
        backendVoiceHotkey === voiceHotkey
      ) {
        setHotkeysDirty(false);
      }
      return;
    }

    setChatHotkey(backendChatHotkey);
    setVoiceHotkey(backendVoiceHotkey);
  }, [
    multiplayer.chatHotkey,
    multiplayer.voiceHotkey,
    hotkeysDirty,
    chatHotkey,
    voiceHotkey
  ]);

  const canSubmitRoomIdentity =
    Boolean(roomId.trim()) &&
    Boolean(displayName.trim()) &&
    (!privateRoom || roomPassword.length >= 4);
  const canJoinRoom =
    Boolean(serverUrl.trim()) &&
    Boolean(roomId.trim()) &&
    Boolean(displayName.trim());

  const statusLabel = multiplayer.connected
    ? pick("Conectado", "Connected", "Conectado", "Verbunden", "Connecté")
    : multiplayer.available
      ? multiplayer.connectionState
      : pick("Controlador inativo", "Controller inactive", "Controlador inactivo", "Controller inaktiv", "Contrôleur inactif");

  const hostReachabilityLabel =
    multiplayer.hostReachability === "internet-address-available"
      ? pick("Internet via UPnP", "Internet via UPnP", "Internet vía UPnP", "Internet über UPnP", "Internet via UPnP")
      : multiplayer.hostReachability === "upnp-mapped-unverified"
        ? pick("UPnP ativo · não verificado externamente", "UPnP active · not externally verified", "UPnP activo · no verificado externamente", "UPnP aktiv · extern nicht geprüft", "UPnP actif · non vérifié depuis l’extérieur")
        : multiplayer.hostReachability === "checking"
          ? pick("Verificando UPnP", "Checking UPnP", "Verificando UPnP", "UPnP wird geprüft", "Vérification UPnP")
          : multiplayer.hostReachability === "lan-only"
            ? pick("Somente LAN", "LAN only", "Solo LAN", "Nur LAN", "LAN uniquement")
            : pick("Host inativo", "Host inactive", "Host inactivo", "Host inaktiv", "Hôte inactif");

  const hostReachabilityDetail =
    multiplayer.hostReachability === "internet-address-available"
      ? (multiplayer.externalProbeConfigured
          ? pick("Endereço externo disponível. Use o teste externo para confirmar alcance.", "External address available. Use the external test to confirm reachability.", "Dirección externa disponible. Usa la prueba externa para confirmar alcance.", "Externe Adresse verfügbar. Externen Test zur Bestätigung verwenden.", "Adresse externe disponible. Utilisez le test externe pour confirmer l’accessibilité.")
          : pick("Endereço externo obtido por UPnP. O teste externo não está configurado nesta instalação.", "External address obtained via UPnP. External testing is not configured in this installation.", "Dirección externa obtenida por UPnP. La prueba externa no está configurada en esta instalación.", "Externe Adresse über UPnP erhalten. Externer Test ist in dieser Installation nicht konfiguriert.", "Adresse externe obtenue via UPnP. Le test externe n’est pas configuré dans cette installation."))
      : multiplayer.hostReachability === "upnp-mapped-unverified"
        ? pick("A porta foi mapeada por UPnP, mas não há confirmação externa.", "The port was mapped by UPnP, but there is no external confirmation.", "El puerto fue mapeado por UPnP, pero no hay confirmación externa.", "Der Port wurde per UPnP gemappt, aber extern nicht bestätigt.", "Le port a été mappé via UPnP, sans confirmation externe.")
        : multiplayer.hostReachability === "lan-only"
          ? pick("A sala funciona na rede local. Para Internet, ative UPnP ou configure redirecionamento da TCP 27730.", "The room works on the local network. For Internet access, enable UPnP or forward TCP 27730.", "La sala funciona en la red local. Para Internet, activa UPnP o redirige TCP 27730.", "Der Raum funktioniert im lokalen Netz. Für Internet UPnP aktivieren oder TCP 27730 weiterleiten.", "La salle fonctionne sur le réseau local. Pour Internet, activez UPnP ou redirigez TCP 27730.")
          : null;

  const visiblePlayers = useMemo(
    () => multiplayer.players.slice().sort((a, b) => a.displayName.localeCompare(b.displayName)),
    [multiplayer.players]
  );

  const publicRooms = useMemo(() => {
    const query = roomSearch.trim().toLocaleLowerCase();
    const rooms = state?.roomDirectory.rooms ?? [];
    if (!query) return rooms;

    return rooms.filter(room =>
      [room.roomId, room.mapName, room.navbrVersion, room.vehiclePath, room.hofName]
        .filter(Boolean)
        .some(value => String(value).toLocaleLowerCase().includes(query))
    );
  }, [state?.roomDirectory.rooms, roomSearch]);

  const submitChat = (event: FormEvent) => {
    event.preventDefault();
    const text = chatText.trim();
    if (!text || !multiplayer.connected) return;
    sendCommand("sendChat", { text });
    setChatText("");
  };

  const buildInviteText = () => {
    if (!multiplayer.connected || !multiplayer.roomId) return null;

    const useOnlineServer = multiplayer.relayEnabled && !multiplayer.hostRunning;
    const activeServer = useOnlineServer
      ? (multiplayer.serverUrl || multiplayer.relayServerUrl || serverUrl)
      : (multiplayer.internetInviteAddress || multiplayer.inviteAddresses[0] || multiplayer.serverUrl || serverUrl);

    if (!activeServer) return null;

    const lines = [
      "NAVBR_INVITE_V1",
      `server=${activeServer}`,
      `room=${multiplayer.roomId}`
    ];
    if (!useOnlineServer) {
      lines.push(`port=${multiplayer.hostPort ?? 27730}`);
    }
    lines.push(`mode=${multiplayer.transportMode === "dedicated-server" ? "dedicated-server" : useOnlineServer ? "relay" : "peer-host"}`);
    if (multiplayer.openOmsiV6.sessionCode) {
      lines.push(`openomsi=${multiplayer.openOmsiV6.sessionCode}`);
    }
    if (multiplayer.openOmsiV6.webSocketUrl) {
      lines.push(`openomsi_ws=${multiplayer.openOmsiV6.webSocketUrl}`);
    }
    return lines.join("\n");
  };

  const copyInvite = async () => {
    const invite = buildInviteText();
    if (!invite) {
      setInviteNotice(pick("Crie ou entre em uma sala antes de copiar o convite.", "Create or join a room before copying the invite.", "Crea o entra en una sala antes de copiar la invitación.", "Erstelle oder betrete einen Raum, bevor du die Einladung kopierst.", "Créez ou rejoignez une salle avant de copier l’invitation."));
      return;
    }

    try {
      await navigator.clipboard.writeText(invite);
      setInviteNotice(pick("Convite copiado.", "Invite copied.", "Invitación copiada.", "Einladung kopiert.", "Invitation copiée."));
    } catch {
      setInviteNotice(pick("Não foi possível acessar a área de transferência.", "Clipboard access was not available.", "No se pudo acceder al portapapeles.", "Kein Zugriff auf die Zwischenablage.", "Impossible d’accéder au presse-papiers."));
    }
  };

  const pasteInvite = async () => {
    try {
      const raw = (await navigator.clipboard.readText()).trim();
      const lines = raw.split(/\r?\n/).map(line => line.trim()).filter(Boolean);
      if (lines[0]?.toUpperCase() !== "NAVBR_INVITE_V1") {
        throw new Error("invalid-invite");
      }

      const values = new Map<string, string>();
      for (const line of lines.slice(1)) {
        const separator = line.indexOf("=");
        if (separator <= 0) continue;
        values.set(line.slice(0, separator).trim().toLowerCase(), line.slice(separator + 1).trim());
      }

      const importedServer = values.get("server") || "";
      const importedRoom = values.get("room") || "";
      const mode = (values.get("mode") || "peer-host").toLowerCase();
      if (!importedServer || !importedRoom) {
        throw new Error("invalid-invite");
      }

      setServerUrl(importedServer);
      setRoomId(importedRoom);
      setRoomPassword("");
      if (mode === "relay" || mode === "dedicated-server") {
        setRelayEnabled(true);
        setRelayServerUrl(importedServer);
        setRelayServerDirty(true);
        sendCommand("configureRelay", { enabled: true, relayServerUrl: importedServer });
      }
      setInviteNotice(pick("Convite importado para os campos da sala.", "Invite imported into the room fields.", "Invitación importada en los campos de la sala.", "Einladung in die Raumfelder übernommen.", "Invitation importée dans les champs de la salle."));
    } catch {
      setInviteNotice(pick("Convite inválido ou área de transferência indisponível.", "Invalid invite or clipboard unavailable.", "Invitación no válida o portapapeles no disponible.", "Ungültige Einladung oder Zwischenablage nicht verfügbar.", "Invitation invalide ou presse-papiers indisponible."));
    }
  };

  return (
    <>
      <header className="topbar multiplayer-header">
        <div>
          <span className="eyebrow">{pick("CENTRAL MULTIPLAYER", "MULTIPLAYER CENTER", "CENTRAL MULTIJUGADOR", "MULTIPLAYER-ZENTRALE", "CENTRALE MULTIJOUEUR")}</span>
          <h1>{pick("Sessão NavBR", "NavBR session", "Sesión NavBR", "NavBR-Sitzung", "Session NavBR")}</h1>
          <p>{pick("Gerencie sala, jogadores, chat, voz e personagem em tempo real.", "Manage room, players, chat, voice and character in real time.", "Gestiona sala, jugadores, chat, voz y personaje en tiempo real.", "Verwalte Raum, Spieler, Chat, Sprache und Charakter in Echtzeit.", "Gérez la salle, les joueurs, le chat, la voix et le personnage en temps réel.")}</p>
        </div>
        <div className="top-actions">
          <span className={`connection-pill ${multiplayer.connected ? "connected" : ""}`}>
            <i /> {statusLabel}
          </span>
          <button
            className="button primary"
            onClick={() => {
              if (!multiplayer.available) {
                sendCommand("ensureMultiplayerController");
              }
              setTab("room");
            }}
          >
            {multiplayer.available ? pick("Controles da sala", "Room controls", "Controles de sala", "Raumsteuerung", "Contrôles de salle") : pick("Ativar multiplayer", "Enable multiplayer", "Activar multijugador", "Multiplayer aktivieren", "Activer le multijoueur")}
          </button>
        </div>
      </header>

      {error && <div className="command-error">{error}</div>}

      <section className="session-metrics">
        <div className="metric"><small>{pick("SALA ATUAL", "CURRENT ROOM", "SALA ACTUAL", "AKTUELLER RAUM", "SALLE ACTUELLE")}</small><strong>{multiplayer.connected ? multiplayer.roomId : "—"}</strong></div>
        <div className="metric"><small>{pick("MAPA LOCAL", "LOCAL MAP", "MAPA LOCAL", "LOKALE KARTE", "CARTE LOCALE")}</small><strong>{telemetry?.mapName || "—"}</strong></div>
        <div className="metric"><small>{pick("JOGADORES", "PLAYERS", "JUGADORES", "SPIELER", "JOUEURS")}</small><strong>{multiplayer.playerCount}</strong></div>
        <div className="metric"><small>{pick("LATÊNCIA", "LATENCY", "LATENCIA", "LATENZ", "LATENCE")}</small><strong>{multiplayer.latencyMs == null ? "—" : `${format(multiplayer.latencyMs, 0)} ms`}</strong></div>
        <div className="metric"><small>{pick("SESSÃO V6", "V6 SESSION", "SESIÓN V6", "V6-SITZUNG", "SESSION V6")}</small><strong>{
          multiplayer.openOmsiV6.sessionCode ||
          (multiplayer.openOmsiV6.active
            ? multiplayer.openOmsiV6.isHost ? "HOST" : "CLIENT"
            : "—")
        }</strong></div>
        <div className="metric"><small>{pick("TRANSPORTE", "TRANSPORT", "TRANSPORTE", "TRANSPORT", "TRANSPORT")}</small><strong>{
          multiplayer.openOmsiV6.active
            ? multiplayer.openOmsiV6.isHost
              ? pick("openOMSI v6 · Host", "openOMSI v6 · Host", "openOMSI v6 · Host", "openOMSI v6 · Host", "openOMSI v6 · Hôte")
              : pick("openOMSI v6 · Cliente", "openOMSI v6 · Client", "openOMSI v6 · Cliente", "openOMSI v6 · Client", "openOMSI v6 · Client")
            : pick("Aguardando openOMSI v6", "Waiting for openOMSI v6", "Esperando openOMSI v6", "Warte auf openOMSI v6", "En attente d’openOMSI v6")
        }</strong></div>
      </section>

      <div className="multiplayer-group-nav" role="tablist">
        {([
          {
            id: "session",
            label: pick("SESSÃO", "SESSION", "SESIÓN", "SITZUNG", "SESSION"),
            items: [
              ["overview", pick("Visão geral", "Overview", "Resumen", "Übersicht", "Vue d’ensemble")],
              ["room", pick("Sala", "Room", "Sala", "Raum", "Salle")],
              ["players", pick("Jogadores", "Players", "Jugadores", "Spieler", "Joueurs")]
            ]
          },
          {
            id: "social",
            label: pick("COMUNICAÇÃO & RP", "COMMUNICATION & RP", "COMUNICACIÓN & RP", "KOMMUNIKATION & RP", "COMMUNICATION & RP"),
            items: [
              ["chat", pick("Chat & Voz", "Chat & Voice", "Chat y Voz", "Chat & Sprache", "Chat & Voix")],
              ["roleplay", pick("Personagem / RP", "Character / RP", "Personaje / RP", "Charakter / RP", "Personnage / RP")]
            ]
          },
          {
            id: "system",
            label: pick("SISTEMA", "SYSTEM", "SISTEMA", "SYSTEM", "SYSTÈME"),
            items: [
              ["advanced", pick("Avançado", "Advanced", "Avanzado", "Erweitert", "Avancé")]
            ]
          }
        ] as Array<{ id: string; label: string; items: [MultiplayerTab, string][] }>).map(group => (
          <section className="multiplayer-nav-group" key={group.id}>
            <span>{group.label}</span>
            <div>
              {group.items.map(([key, label]) => (
                <button
                  key={key}
                  className={tab === key ? "active" : ""}
                  onClick={() => setTab(key)}
                >
                  {label}
                </button>
              ))}
            </div>
          </section>
        ))}
      </div>

      {tab === "overview" && (
        <section className="mp-grid">
          <article className="card mp-main-card">
            <div className="section-heading">
              <div><span className="eyebrow">{pick("SESSÃO AO VIVO", "LIVE SESSION", "SESIÓN EN VIVO", "LIVE-SITZUNG", "SESSION EN DIRECT")}</span><h3>{pick("Operação compartilhada", "Shared operation", "Operación compartida", "Gemeinsamer Betrieb", "Opération partagée")}</h3></div>
              <span className={`live-pill ${multiplayer.connected ? "" : "muted"}`}><span /> {multiplayer.connected ? "LIVE" : "OFFLINE"}</span>
            </div>
            {state?.navigation?.available || state?.navigation?.roadmapAvailable
              ? <NavigationMap
                  navigation={state.navigation}
                  remoteVehicles={state.navigation3D?.remoteVehicles}
                  remoteRoleplayCharacters={state.navigation3D?.remoteRoleplayCharacters}
                />
              : <SessionMap points={multiplayer.sessionPoints} />}
          </article>

          <aside className="mp-side-stack">
            <article className="card compact-card">
              <span className="eyebrow">{pick("VOCÊ", "YOU", "TÚ", "DU", "VOUS")}</span>
              <h3>{multiplayer.displayName || pick("Motorista", "Driver", "Conductor", "Fahrer", "Conducteur")}</h3>
              <p>{telemetry?.line ? `${pick("Linha", "Line", "Línea", "Linie", "Ligne")} ${telemetry.line}` : pick("Sem linha ativa", "No active line", "Sin línea activa", "Keine aktive Linie", "Aucune ligne active")}</p>
              <p>{telemetry?.route || telemetry?.destinationName || pick("Aguardando rota", "Waiting for route", "Esperando ruta", "Warte auf Route", "En attente de l’itinéraire")}</p>
            </article>
            <article className="card compact-card experimental-quick-card">
              <span className="eyebrow">{pick("RECURSOS EXPERIMENTAIS", "EXPERIMENTAL FEATURES", "FUNCIONES EXPERIMENTALES", "EXPERIMENTELLE FUNKTIONEN", "FONCTIONS EXPÉRIMENTALES")}</span>
              <h3>{pick("Ativação rápida", "Quick enable", "Activación rápida", "Schnell aktivieren", "Activation rapide")}</h3>
              <p>{pick("Os mesmos recursos experimentais ficam visíveis aqui no app principal, sem precisar procurar em Avançado ou nas configurações do Mobile Companion.", "The same experimental features are visible here in the main app, without hunting through Advanced or Mobile Companion settings.", "Las mismas funciones experimentales aparecen aquí en la app principal, sin buscarlas en Avanzado o Mobile Companion.", "Dieselben experimentellen Funktionen sind hier direkt in der Haupt-App sichtbar.", "Les mêmes fonctions expérimentales sont visibles ici dans l’application principale.")}</p>
              <div className="network-message">
                <strong>openOMSI v6</strong> · {multiplayer.physicalVehiclesAvailable
                  ? pick("ônibus remotos físicos ativos", "physical remote buses active", "autobuses remotos físicos activos", "physische Remote-Busse aktiv", "bus distants physiques actifs")
                  : pick("aguardando capacidade do Plugin Bridge", "waiting for Plugin Bridge capability", "esperando capacidad de Plugin Bridge", "wartet auf Plugin-Bridge-Fähigkeit", "en attente de la capacité Plugin Bridge")}
              </div>
              <label className="privacy-toggle">
                <input
                  type="checkbox"
                  checked={Boolean(mobileControls?.vehicleControlsEnabled)}
                  onChange={event => sendCommand("setMobileVehicleControlsEnabled", { enabled: event.target.checked })}
                />
                <span>{pick("Controles do ônibus pelo APK/celular (EXPERIMENTAL)", "Bus controls from APK/phone (EXPERIMENTAL)", "Controles del autobús desde APK/móvil (EXPERIMENTAL)", "Bussteuerung über APK/Smartphone (EXPERIMENTELL)", "Commandes du bus via APK/téléphone (EXPÉRIMENTAL)")}</span>
              </label>
              <small className="plugin-update-hint">
                {mobileControls?.vehicleControlsEnabled
                  ? mobileControls.vehicleControlsAvailable
                    ? pick("Autorizado e disponível para o APK.", "Authorized and available to the APK.", "Autorizado y disponible para el APK.", "Freigegeben und für die APK verfügbar.", "Autorisé et disponible pour l’APK.")
                    : pick("Autorizado; aguardando a capacidade local-vehicle-trigger.", "Authorized; waiting for local-vehicle-trigger capability.", "Autorizado; esperando local-vehicle-trigger.", "Freigegeben; wartet auf local-vehicle-trigger.", "Autorisé ; en attente de local-vehicle-trigger.")
                  : pick("Desativado. O APK permanece somente leitura para os controles locais.", "Disabled. The APK remains read-only for local controls.", "Desactivado. El APK permanece en solo lectura para controles locales.", "Deaktiviert. Die APK bleibt für lokale Steuerungen schreibgeschützt.", "Désactivé. L’APK reste en lecture seule pour les commandes locales.")}
              </small>
            </article>

            <article className="card compact-card">
              <span className="eyebrow">{pick("VOZ", "VOICE", "VOZ", "SPRACHE", "VOIX")}</span>
              <h3>{multiplayer.voiceEnabled ? pick("Ativa", "Active", "Activa", "Aktiv", "Active") : pick("Desativada", "Disabled", "Desactivada", "Deaktiviert", "Désactivée")}</h3>
              <p>{pick("Canal", "Channel", "Canal", "Kanal", "Canal")} {multiplayer.voiceChannel || "general"}</p>
            </article>
            <article className="card compact-card">
              <span className="eyebrow">{pick("PERSONAGEM", "CHARACTER", "PERSONAJE", "CHARAKTER", "PERSONNAGE")}</span>
              <h3>{multiplayer.localRoleplayActive ? pick("Fora do ônibus", "Outside the bus", "Fuera del autobús", "Außerhalb des Busses", "Hors du bus") : pick("No ônibus", "In the bus", "En el autobús", "Im Bus", "Dans le bus")}</h3>
              <button className="text-action" onClick={() => setTab("roleplay")}>{pick("Abrir Personagem / RP", "Open Character / RP", "Abrir Personaje / RP", "Charakter / RP öffnen", "Ouvrir Personnage / RP")} →</button>
            </article>
            <article className="card compact-card">
              <span className="eyebrow">{pick("QUALIDADE DA SESSÃO", "SESSION QUALITY", "CALIDAD DE SESIÓN", "SITZUNGSQUALITÄT", "QUALITÉ DE SESSION")}</span>
              <h3>{multiplayer.networkQuality.level}</h3>
              <div className="details-grid">
                <div><small>RTT</small><strong>{multiplayer.networkQuality.roundTripMs == null ? "—" : `${format(multiplayer.networkQuality.roundTripMs, 0)} ms`}</strong></div>
                <div><small>{pick("JITTER", "JITTER", "JITTER", "JITTER", "JITTER")}</small><strong>{multiplayer.networkQuality.jitterMs == null ? "—" : `${format(multiplayer.networkQuality.jitterMs, 0)} ms`}</strong></div>
                <div><small>{pick("PERDA", "LOSS", "PÉRDIDA", "VERLUST", "PERTE")}</small><strong>{format(multiplayer.networkQuality.lossPercent, 1)}%</strong></div>
                <div><small>{pick("AMOSTRAS", "SAMPLES", "MUESTRAS", "MESSUNGEN", "ÉCHANTILLONS")}</small><strong>{multiplayer.networkQuality.samples}</strong></div>
              </div>
            </article>

            <article className="card compact-card">
              <span className="eyebrow">{pick("AUTORIDADE", "AUTHORITY", "AUTORIDAD", "AUTORITÄT", "AUTORITÉ")}</span>
              <h3>{multiplayer.roomIsPrivate ? pick("Sala privada", "Private room", "Sala privada", "Privater Raum", "Salle privée") : pick("Sala pública", "Public room", "Sala pública", "Öffentlicher Raum", "Salle publique")}</h3>
              <p>{pick("Dono", "Owner", "Propietario", "Besitzer", "Propriétaire")}: <strong>{multiplayer.sessionAuthority.roomOwnerDisplayName || "—"}</strong></p>
              <p>{pick("Tráfego", "Traffic", "Tráfico", "Verkehr", "Trafic")}: <strong>{multiplayer.sessionAuthority.trafficAuthorityDisplayName || "—"}</strong></p>
              {(multiplayer.sessionAuthority.isRoomOwner || multiplayer.sessionAuthority.isTrafficAuthority) && (
                <span className="authority-pill enabled">
                  {multiplayer.sessionAuthority.isRoomOwner && multiplayer.sessionAuthority.isTrafficAuthority
                    ? pick("Você é dono e autoridade", "You are owner and authority", "Eres propietario y autoridad", "Du bist Besitzer und Autorität", "Vous êtes propriétaire et autorité")
                    : multiplayer.sessionAuthority.isRoomOwner
                      ? pick("Você é o dono", "You are the owner", "Eres el propietario", "Du bist der Besitzer", "Vous êtes le propriétaire")
                      : pick("Você é a autoridade de tráfego", "You are the traffic authority", "Eres la autoridad de tráfico", "Du bist die Verkehrsautorität", "Vous êtes l’autorité trafic")}
                </span>
              )}
            </article>

            <article className="card compact-card">
              <span className="eyebrow">{pick("SINCRONIZAÇÃO DA SESSÃO", "SESSION SYNC", "SINCRONIZACIÓN DE SESIÓN", "SITZUNGSSYNC", "SYNCHRONISATION DE SESSION")}</span>
              <h3>
                {!multiplayer.sessionOperationalState
                  ? pick("Aguardando", "Waiting", "Esperando", "Wartet", "En attente")
                  : multiplayer.sessionAuthority.isTrafficAuthority
                    ? pick("Publicando", "Publishing", "Publicando", "Sendet", "Publication")
                    : pick("Sincronizado", "Synced", "Sincronizado", "Synchron", "Synchronisé")}
              </h3>
              {multiplayer.sessionOperationalState ? (
                <>
                  <p>{multiplayer.sessionOperationalState.mapName || "—"} · {multiplayer.sessionOperationalState.line || "—"} / {multiplayer.sessionOperationalState.route || "—"}</p>
                  <p>{multiplayer.sessionOperationalState.destinationName || "—"} → {multiplayer.sessionOperationalState.nextStopName || "—"}</p>
                  <small>seq {multiplayer.sessionOperationalState.sequence} · {new Date(multiplayer.sessionOperationalState.serverTimestampUtc).toLocaleTimeString()}</small>
                </>
              ) : (
                <p>{pick("O estado operacional aparecerá quando a sala publicar um snapshot real.", "Operational state appears when the room publishes a real snapshot.", "El estado operacional aparecerá cuando la sala publique un snapshot real.", "Der Betriebsstatus erscheint, sobald der Raum einen echten Snapshot veröffentlicht.", "L’état opérationnel apparaît lorsque la salle publie un snapshot réel.")}</p>
              )}
            </article>

            <article className="card compact-card">
              <span className="eyebrow">{pick("APOIO CCO", "DISPATCH SUPPORT", "APOYO CCO", "LEITSTELLENHILFE", "ASSISTANCE CCO")}</span>
              <h3>{pick("Ocorrência do motorista", "Driver report", "Incidencia del conductor", "Fahrermeldung", "Signalement conducteur")}</h3>
              <p>{pick("Envia apoio/incidente real para o CCO da sessão ou marca seus chamados como normalizados.", "Sends a real support/incident report to session dispatch or marks your reports resolved.", "Envía apoyo/incidente real al CCO de la sesión o marca tus avisos como normalizados.", "Sendet eine echte Hilfe-/Vorfallmeldung an die Leitstelle oder markiert eigene Meldungen als erledigt.", "Envoie une demande/incidence réelle au CCO ou clôture vos propres signalements.")}</p>
              <div className="room-actions">
                <button className="button ghost" disabled={!multiplayer.connected} onClick={() => sendCommand("submitOperationalReport", { kind: "assistance" })}>{pick("Pedir apoio", "Request support", "Pedir apoyo", "Hilfe anfordern", "Demander de l’aide")}</button>
                <button className="button ghost danger" disabled={!multiplayer.connected} onClick={() => sendCommand("submitOperationalReport", { kind: "incident" })}>{pick("Reportar incidente", "Report incident", "Reportar incidente", "Vorfall melden", "Signaler un incident")}</button>
                <button className="button ghost" disabled={!multiplayer.connected} onClick={() => sendCommand("resolveMyOperationalReports")}>{pick("Normalizado", "Resolved", "Normalizado", "Normalisiert", "Normalisé")}</button>
              </div>
            </article>
          </aside>
        </section>
      )}

      {tab === "room" && (
        <section className="room-screen">
          <div className="room-screen-header">
            <div>
              <span className="eyebrow">{pick("SALA MULTIPLAYER", "MULTIPLAYER ROOM", "SALA MULTIJUGADOR", "MULTIPLAYER-RAUM", "SALLE MULTIJOUEUR")}</span>
              <h3>{multiplayer.connected
                ? (multiplayer.roomId || pick("Sala conectada", "Connected room", "Sala conectada", "Verbundener Raum", "Salle connectée"))
                : pick("Entrar ou criar uma sala", "Join or create a room", "Entrar o crear una sala", "Raum beitreten oder erstellen", "Rejoindre ou créer une salle")}</h3>
              <p>{multiplayer.connected
                ? pick("A operação da sala está ativa. Configurações de entrada ficam bloqueadas até desconectar.", "The room session is active. Join settings stay locked until you disconnect.", "La sesión está activa. La configuración de entrada permanece bloqueada hasta desconectar.", "Die Raumsitzung ist aktiv. Beitrittseinstellungen bleiben bis zur Trennung gesperrt.", "La session est active. Les paramètres d’entrée restent verrouillés jusqu’à la déconnexion.")
                : pick("Escolha um modo abaixo. As opções avançadas ficam separadas para não poluir a tela.", "Choose a mode below. Advanced options are separated to keep the screen clean.", "Elige un modo abajo. Las opciones avanzadas están separadas para mantener la pantalla limpia.", "Wähle unten einen Modus. Erweiterte Optionen sind getrennt, damit die Ansicht übersichtlich bleibt.", "Choisissez un mode ci-dessous. Les options avancées sont séparées pour garder l’écran lisible.")}</p>
            </div>
            <div className="room-header-actions">
              <span className={"connection-pill " + (multiplayer.connected ? "connected" : "")}><i /> {statusLabel}</span>
              <button className="button ghost icon-button" onClick={onOpenNetwork}><NavBrIcon name="network" size={16} />{pick("Rede / Firewall", "Network / Firewall", "Red / Firewall", "Netzwerk / Firewall", "Réseau / Pare-feu")}</button>
            </div>
          </div>

          <div className="room-dashboard">
            <article className="card room-setup-card">
              <div className="room-intent-switch" role="tablist" aria-label={pick("Ação da sala", "Room action", "Acción de la sala", "Raumaktion", "Action de salle")}>
                <button
                  className={roomIntent === "create" ? "active" : ""}
                  onClick={() => setRoomIntent("create")}
                  disabled={multiplayer.connected}
                >
                  <NavBrIcon name="roomAdd" size={16} />{pick("Criar sala", "Create room", "Crear sala", "Raum erstellen", "Créer une salle")}
                </button>
                <button
                  className={roomIntent === "join" ? "active" : ""}
                  onClick={() => setRoomIntent("join")}
                  disabled={multiplayer.connected}
                >
                  <NavBrIcon name="roomJoin" size={16} />{pick("Entrar em sala", "Join room", "Entrar en sala", "Raum beitreten", "Rejoindre une salle")}
                </button>
              </div>

              {!multiplayer.connected ? (
                roomIntent === "create" ? (
                  <>
                    <div className="section-heading compact room-create-heading">
                      <div>
                        <span className="eyebrow">{pick("CRIAR", "CREATE", "CREAR", "ERSTELLEN", "CRÉER")}</span>
                        <h3>{pick("Nova sala", "New room", "Nueva sala", "Neuer Raum", "Nouvelle salle")}</h3>
                      </div>
                    </div>

                    <div className="room-form-grid room-create-fields">
                      <label>
                        <span>{pick("Nome da sala", "Room name", "Nombre de sala", "Raumname", "Nom de la salle")}</span>
                        <input value={roomId} onChange={event => setRoomId(event.target.value)} placeholder="navbr-1234" />
                      </label>
                      <label>
                        <span>{pick("Seu apelido", "Your display name", "Tu apodo", "Dein Anzeigename", "Votre pseudo")}</span>
                        <input value={displayName} onChange={event => setDisplayName(event.target.value)} placeholder="Driver" />
                      </label>
                    </div>

                    <div className="room-privacy-row">
                      <label className="privacy-toggle">
                        <input type="checkbox" checked={privateRoom} onChange={event => setPrivateRoom(event.target.checked)} />
                        <span>{pick("Privada", "Private", "Privada", "Privat", "Privée")}</span>
                      </label>
                      {privateRoom && (
                        <label className="password-field">
                          <span>{pick("Senha", "Password", "Contraseña", "Passwort", "Mot de passe")}</span>
                          <input
                            type="password"
                            value={roomPassword}
                            onChange={event => setRoomPassword(event.target.value)}
                            placeholder={pick("Mínimo 4 caracteres", "Minimum 4 characters", "Mínimo 4 caracteres", "Mindestens 4 Zeichen", "Minimum 4 caractères")}
                          />
                        </label>
                      )}
                    </div>

                    <div className="room-mode-compact">
                      <button
                        className={createRoomMode === "navbr" ? "active" : ""}
                        onClick={() => setCreateRoomMode("navbr")}
                      >
                        <strong>{pick("Servidor NavBR", "NavBR Server", "Servidor NavBR", "NavBR-Server", "Serveur NavBR")}</strong>
                        <small>{pick("Online", "Online", "Online", "Online", "En ligne")}</small>
                      </button>
                      <button
                        className={createRoomMode === "lan" ? "active" : ""}
                        onClick={() => setCreateRoomMode("lan")}
                      >
                        <strong>LAN</strong>
                        <small>{pick("Rede local", "Local network", "Red local", "Lokales Netz", "Réseau local")}</small>
                      </button>
                      <button
                        className={createRoomMode === "host" ? "active" : ""}
                        onClick={() => setCreateRoomMode("host")}
                      >
                        <strong>{pick("Meu PC", "My PC", "Mi PC", "Mein PC", "Mon PC")}</strong>
                        <small>{pick("Host Internet", "Internet host", "Host Internet", "Internet-Host", "Hôte Internet")}</small>
                      </button>
                    </div>

                    <button
                      className="button primary room-create-submit"
                      disabled={!canSubmitRoomIdentity}
                      onClick={() => {
                        if (createRoomMode === "navbr") {
                          const onlineUrl = relayServerUrl || defaultOnlineServer;
                          setRelayEnabled(true);
                          setRelayServerUrl(onlineUrl);
                          setRelayServerDirty(true);
                          setServerUrl(onlineUrl);
                          sendCommand("createOnlineRoom", {
                            roomId,
                            displayName,
                            isPrivate: privateRoom,
                            roomPassword,
                            serverUrl: onlineUrl
                          });
                          return;
                        }

                        setRelayEnabled(false);
                        sendCommand("createLocalRoom", {
                          roomId,
                          displayName,
                          isPrivate: privateRoom,
                          roomPassword,
                          useRelay: false,
                          exposeInternet: createRoomMode === "host"
                        });
                      }}
                    >
                      {pick("Criar sala", "Create room", "Crear sala", "Raum erstellen", "Créer la salle")}
                    </button>

                    <details className="room-help-details">
                      <summary>{pick("Qual modo devo usar?", "Which mode should I use?", "¿Qué modo debo usar?", "Welchen Modus soll ich verwenden?", "Quel mode utiliser ?")}</summary>
                      <div className="room-help-grid">
                        <span><strong>{pick("Servidor NavBR", "NavBR Server", "Servidor NavBR", "NavBR-Server", "Serveur NavBR")}</strong>{pick("Mais simples para jogar pela Internet.", "Simplest option for Internet play.", "La opción más simple para jugar por Internet.", "Einfachste Option für Internet-Spiel.", "Option la plus simple pour jouer en ligne.")}</span>
                        <span><strong>LAN</strong>{pick("Somente computadores na mesma rede.", "Only computers on the same network.", "Solo equipos en la misma red.", "Nur Computer im selben Netzwerk.", "Uniquement les ordinateurs du même réseau.")}</span>
                        <span><strong>{pick("Meu PC", "My PC", "Mi PC", "Mein PC", "Mon PC")}</strong>{pick("Hospeda o openOMSI v6; usa UDP direto e WebSocket/túnel como fallback para Internet.", "Hosts openOMSI v6; uses direct UDP and WebSocket/tunnel fallback for Internet.", "Aloja openOMSI v6; usa UDP directo y WebSocket/túnel como fallback para Internet.", "Hostet openOMSI v6; nutzt direktes UDP und WebSocket/Tunnel als Internet-Fallback.", "Héberge openOMSI v6 ; utilise UDP direct et WebSocket/tunnel comme solution de repli Internet.")}</span>
                      </div>
                    </details>

                    {createRoomMode === "navbr" && (
                      <details className="room-advanced-options">
                        <summary>{pick("Servidor personalizado", "Custom server", "Servidor personalizado", "Eigener Server", "Serveur personnalisé")}</summary>
                        <label className="password-field">
                          <span>{pick("URL do servidor", "Server URL", "URL del servidor", "Server-URL", "URL du serveur")}</span>
                          <input
                            value={relayServerUrl}
                            onChange={event => {
                              setRelayServerUrl(event.target.value);
                              setRelayServerDirty(true);
                            }}
                            onBlur={() => sendCommand("configureRelay", { enabled: true, relayServerUrl })}
                            placeholder={defaultOnlineServer}
                          />
                        </label>
                      </details>
                    )}
                  </>
                ) : (
                  <>
                    <div className="section-heading compact room-create-heading">
                      <div>
                        <span className="eyebrow">{pick("ENTRAR", "JOIN", "ENTRAR", "BEITRETEN", "REJOINDRE")}</span>
                        <h3>{pick("Conectar a uma sala", "Connect to a room", "Conectar a una sala", "Mit einem Raum verbinden", "Se connecter à une salle")}</h3>
                      </div>
                    </div>

                    <div className="room-form-grid room-join-fields">
                      <label>
                        <span>{pick("Sala", "Room", "Sala", "Raum", "Salle")}</span>
                        <input value={roomId} onChange={event => setRoomId(event.target.value)} placeholder="navbr-1234" />
                      </label>
                      <label>
                        <span>{pick("Seu apelido", "Your display name", "Tu apodo", "Dein Anzeigename", "Votre pseudo")}</span>
                        <input value={displayName} onChange={event => setDisplayName(event.target.value)} placeholder="Driver" />
                      </label>
                      <label className="room-server-field">
                        <span>{pick("Servidor", "Server", "Servidor", "Server", "Serveur")}</span>
                        <input value={serverUrl} onChange={event => setServerUrl(event.target.value)} placeholder={defaultOnlineServer} />
                      </label>
                      <label>
                        <span>{pick("Senha, se houver", "Password, if required", "Contraseña, si existe", "Passwort, falls nötig", "Mot de passe, si nécessaire")}</span>
                        <input type="password" value={roomPassword} onChange={event => setRoomPassword(event.target.value)} />
                      </label>
                    </div>

                    <div className="room-join-actions">
                      <button className="button primary" disabled={!canJoinRoom} onClick={() => sendCommand("connectRoom", { serverUrl, roomId, displayName, roomPassword })}>
                        {pick("Entrar", "Join", "Entrar", "Beitreten", "Rejoindre")}
                      </button>
                      <button className="button ghost icon-button" onClick={pasteInvite}><NavBrIcon name="clipboardPaste" size={16} />{pick("Colar convite", "Paste invite", "Pegar invitación", "Einladung einfügen", "Coller l’invitation")}</button>
                    </div>
                  </>
                )
              ) : (
                <>
                  <div className="section-heading compact">
                    <div>
                      <span className="eyebrow">{pick("SALA ATIVA", "ACTIVE ROOM", "SALA ACTIVA", "AKTIVER RAUM", "SALLE ACTIVE")}</span>
                      <h3>{multiplayer.roomId || roomId}</h3>
                    </div>
                  </div>
                  <div className="room-connected-actions">
                    <button className="button ghost icon-button" onClick={copyInvite}><NavBrIcon name="clipboardCopy" size={16} />{pick("Copiar convite", "Copy invite", "Copiar invitación", "Einladung kopieren", "Copier l’invitation")}</button>
                    <button className="button ghost danger" onClick={() => sendCommand(multiplayer.hostRunning ? "stopLocalHost" : "disconnectRoom")}>
                      {multiplayer.hostRunning ? pick("Encerrar servidor", "Stop server", "Detener servidor", "Server stoppen", "Arrêter le serveur") : pick("Desconectar", "Disconnect", "Desconectar", "Trennen", "Déconnecter")}
                    </button>
                  </div>
                </>
              )}

              {inviteNotice && <div className="network-message">{inviteNotice}</div>}
            </article>

            <aside className="card room-status-card">
              <div className="section-heading compact">
                <div>
                  <span className="eyebrow">{pick("STATUS", "STATUS", "ESTADO", "STATUS", "ÉTAT")}</span>
                  <h3>{multiplayer.connected ? pick("Sala ativa", "Active room", "Sala activa", "Aktiver Raum", "Salle active") : pick("Aguardando conexão", "Waiting for connection", "Esperando conexión", "Warte auf Verbindung", "En attente de connexion")}</h3>
                </div>
              </div>

              <div className="room-status-list">
                <div><small>{pick("ESTADO", "STATE", "ESTADO", "STATUS", "ÉTAT")}</small><strong>{statusLabel}</strong></div>
                <div><small>{pick("SALA", "ROOM", "SALA", "RAUM", "SALLE")}</small><strong>{multiplayer.roomId || roomId || "—"}</strong></div>
                <div><small>{pick("MODO", "MODE", "MODO", "MODUS", "MODE")}</small><strong>{
                  multiplayer.transportMode === "dedicated-server"
                    ? pick("Servidor NavBR", "NavBR Server", "Servidor NavBR", "NavBR-Server", "Serveur NavBR")
                    : multiplayer.hostRunning
                      ? hostReachabilityLabel
                      : multiplayer.connected
                        ? pick("Cliente remoto", "Remote client", "Cliente remoto", "Remote-Client", "Client distant")
                        : "—"
                }</strong></div>
                <div><small>{pick("JOGADORES", "PLAYERS", "JUGADORES", "SPIELER", "JOUEURS")}</small><strong>{multiplayer.connected ? multiplayer.playerCount : "—"}</strong></div>
                <div><small>{pick("DONO", "OWNER", "PROPIETARIO", "BESITZER", "PROPRIÉTAIRE")}</small><strong>{multiplayer.sessionAuthority.roomOwnerDisplayName || "—"}</strong></div>
                <div><small>{pick("SERVIDOR", "SERVER", "SERVIDOR", "SERVER", "SERVEUR")}</small><strong>{multiplayer.serverUrl || serverUrl || "—"}</strong></div>
                <div><small>{pick("FÍSICO", "PHYSICAL", "FÍSICO", "PHYSISCH", "PHYSIQUE")}</small><strong>{
                  multiplayer.openOmsiV6.active
                    ? multiplayer.openOmsiV6.isHost
                      ? `openOMSI v6 · HOST · UDP ${multiplayer.openOmsiV6.port ?? "—"}`
                      : pick("openOMSI v6 · CLIENTE", "openOMSI v6 · CLIENT", "openOMSI v6 · CLIENTE", "openOMSI v6 · CLIENT", "openOMSI v6 · CLIENT")
                    : pick("OFFLINE", "OFFLINE", "OFFLINE", "OFFLINE", "HORS LIGNE")
                }</strong></div>
              </div>

              {multiplayer.openOmsiV6.sessionCode && (
                <div className="invite-box compact-invite">
                  <small>{pick("CÓDIGO OPENOMSI", "OPENOMSI CODE", "CÓDIGO OPENOMSI", "OPENOMSI-CODE", "CODE OPENOMSI")}</small>
                  <code>{multiplayer.openOmsiV6.sessionCode}</code>
                </div>
              )}

              {multiplayer.openOmsiV6.webSocketUrl && (
                <div className="invite-box compact-invite">
                  <small>{pick("TÚNEL OPENOMSI", "OPENOMSI TUNNEL", "TÚNEL OPENOMSI", "OPENOMSI-TUNNEL", "TUNNEL OPENOMSI")}</small>
                  <code>{multiplayer.openOmsiV6.webSocketUrl}</code>
                </div>
              )}

              {multiplayer.transportMode === "dedicated-server" && (
                <div className="room-status-note">
                  <strong>{pick("Servidor dedicado", "Dedicated server", "Servidor dedicado", "Dedizierter Server", "Serveur dédié")}</strong>
                  <span>{pick("Seu PC funciona somente como cliente; a sala é executada no servidor NavBR.", "Your PC acts only as a client; the room runs on the NavBR server.", "Tu PC funciona solo como cliente; la sala se ejecuta en el servidor NavBR.", "Dein PC ist nur Client; der Raum läuft auf dem NavBR-Server.", "Votre PC agit uniquement comme client ; la salle s’exécute sur le serveur NavBR.")}</span>
                </div>
              )}

              {multiplayer.hostRunning && hostReachabilityDetail && multiplayer.transportMode !== "dedicated-server" && (
                <div className="room-status-note">
                  <strong>{hostReachabilityLabel}</strong>
                  <span>{hostReachabilityDetail}</span>
                </div>
              )}

              {multiplayer.inviteAddresses.length > 0 && (
                <div className="invite-box compact-invite">
                  <small>{pick("ENDEREÇOS DE CONVITE", "INVITE ADDRESSES", "DIRECCIONES DE INVITACIÓN", "EINLADUNGSADRESSEN", "ADRESSES D’INVITATION")}</small>
                  {multiplayer.inviteAddresses.map(address => <code key={address}>{address}</code>)}
                </div>
              )}

              <button className="button ghost room-status-network" onClick={onOpenNetwork}>
                {pick("Abrir diagnóstico de rede", "Open network diagnostics", "Abrir diagnóstico de red", "Netzwerkdiagnose öffnen", "Ouvrir le diagnostic réseau")}
              </button>
            </aside>
          </div>

          <article className="card public-room-browser room-directory-card">
            <div className="section-heading">
              <div>
                <span className="eyebrow">{pick("SALAS PÚBLICAS", "PUBLIC ROOMS", "SALAS PÚBLICAS", "ÖFFENTLICHE RÄUME", "SALLES PUBLIQUES")}</span>
                <h3>{pick("Encontrar uma sala ativa", "Find an active room", "Encontrar una sala activa", "Aktiven Raum finden", "Trouver une salle active")}</h3>
              </div>
              <button
                className="button ghost"
                disabled={Boolean(state?.roomDirectory.refreshing)}
                onClick={() => sendCommand("refreshPublicRooms", { serverUrl })}
              >
                {state?.roomDirectory.refreshing
                  ? pick("Atualizando…", "Refreshing…", "Actualizando…", "Wird aktualisiert…", "Actualisation…")
                  : pick("Atualizar", "Refresh", "Actualizar", "Aktualisieren", "Actualiser")}
              </button>
            </div>

            <input
              className="room-search"
              value={roomSearch}
              onChange={event => setRoomSearch(event.target.value)}
              placeholder={pick("Buscar sala, mapa, versão, ônibus ou HOF", "Search room, map, version, bus or HOF", "Buscar sala, mapa, versión, autobús o HOF", "Raum, Karte, Version, Bus oder HOF suchen", "Rechercher salle, carte, version, bus ou HOF")}
            />
            {state?.roomDirectory.error && <div className="directory-error">{state.roomDirectory.error}</div>}

            <div className="public-room-list">
              {state?.roomDirectory.refreshing ? (
                <div className="empty-state compact-empty">
                  {pick("Buscando salas públicas…", "Loading public rooms…", "Buscando salas públicas…", "Öffentliche Räume werden geladen…", "Chargement des salles publiques…")}
                </div>
              ) : publicRooms.length === 0 ? (
                <div className="empty-state compact-empty">{pick("Nenhuma sala pública carregada.", "No public room loaded.", "No hay salas públicas cargadas.", "Keine öffentlichen Räume geladen.", "Aucune salle publique chargée.")}</div>
              ) : publicRooms.map(room => (
                <div className="public-room-row" key={room.roomId}>
                  <button
                    className={"favorite-button " + (room.favorite ? "active" : "")}
                    onClick={() => sendCommand("toggleRoomFavorite", { roomId: room.roomId })}
                    title={room.favorite ? pick("Remover dos favoritos", "Remove from favorites", "Quitar de favoritos", "Aus Favoriten entfernen", "Retirer des favoris") : pick("Adicionar aos favoritos", "Add to favorites", "Añadir a favoritos", "Zu Favoriten hinzufügen", "Ajouter aux favoris")}
                  >
                    <NavBrIcon name="star" size={16} fill={room.favorite ? "currentColor" : "none"} />
                  </button>
                  <button
                    className="public-room-main"
                    onClick={() => {
                      setRoomId(room.roomId);
                      setPrivateRoom(false);
                      setRoomPassword("");
                    }}
                  >
                    <strong>{room.roomId}</strong>
                    <span>{room.mapName || pick("Mapa não informado", "Map not provided", "Mapa no informado", "Karte nicht angegeben", "Carte non renseignée")} · {room.playerCount} {pick("jogador(es)", "player(s)", "jugador(es)", "Spieler", "joueur(s)")}</span>
                    <small>NavBR {room.navbrVersion || "—"} · OMSI {room.omsiVersion || "—"}</small>
                    <em className={"compatibility-badge " + room.compatibility}>
                      {room.compatibility === "compatible" ? pick("Compatível", "Compatible", "Compatible", "Kompatibel", "Compatible") : room.compatibility === "warning" ? pick("Compatibilidade parcial", "Partial compatibility", "Compatibilidad parcial", "Teilweise kompatibel", "Compatibilité partielle") : pick("Requer ajuste local", "Requires local adjustment", "Requiere ajuste local", "Lokale Anpassung erforderlich", "Nécessite un ajustement local")}
                    </em>
                    {room.compatibilityIssues.length > 0 && <small className="compatibility-detail">{room.compatibilityIssues[0]}</small>}
                  </button>
                  <button
                    className="button compact"
                    disabled={!room.directJoinAllowed || !serverUrl.trim() || !displayName.trim()}
                    title={!room.directJoinAllowed
                      ? pick("Carregue a configuração compatível antes de entrar diretamente.", "Load a compatible configuration before joining directly.", "Carga una configuración compatible antes de entrar directamente.", "Vor direktem Beitritt eine kompatible Konfiguration laden.", "Chargez une configuration compatible avant de rejoindre directement.")
                      : !displayName.trim()
                        ? pick("Informe seu apelido antes de entrar.", "Enter your display name before joining.", "Indica tu apodo antes de entrar.", "Gib vor dem Beitritt deinen Anzeigenamen ein.", "Indiquez votre pseudo avant de rejoindre.")
                        : undefined}
                    onClick={() => {
                      setRoomId(room.roomId);
                      setPrivateRoom(false);
                      setRoomPassword("");
                      sendCommand("connectRoom", { serverUrl, roomId: room.roomId, displayName, roomPassword: "" });
                    }}
                  >
                    {pick("Entrar", "Join", "Entrar", "Beitreten", "Rejoindre")}
                  </button>
                </div>
              ))}
            </div>
          </article>
        </section>
      )}

      {tab === "players" && (
        <section className="card mp-panel">
          <div className="section-heading">
            <div><span className="eyebrow">{pick("JOGADORES", "PLAYERS", "JUGADORES", "SPIELER", "JOUEURS")}</span><h3>{visiblePlayers.length} {pick("na sessão", "in session", "en sesión", "in Sitzung", "dans la session")}</h3></div>
          </div>

          <article className="compatibility-summary">
            <div>
              <small>{pick("COMPATIBILIDADE DA SALA", "ROOM COMPATIBILITY", "COMPATIBILIDAD DE SALA", "RAUMKOMPATIBILITÄT", "COMPATIBILITÉ DE LA SALLE")}</small>
              <strong className={`compatibility-badge ${multiplayer.roomCompatibility.level === "blocked" ? "blocked" : multiplayer.roomCompatibility.level === "warning" || multiplayer.roomCompatibility.level === "partial" ? "warning" : "compatible"}`}>
                {multiplayer.roomCompatibility.level === "blocked"
                  ? pick("Bloqueio", "Blocked", "Bloqueo", "Blockiert", "Bloqué")
                  : multiplayer.roomCompatibility.level === "warning"
                    ? pick("Atenção", "Warning", "Atención", "Achtung", "Attention")
                    : multiplayer.roomCompatibility.level === "partial"
                      ? pick("Parcial", "Partial", "Parcial", "Teilweise", "Partiel")
                      : multiplayer.roomCompatibility.level === "compatible"
                        ? pick("Compatível", "Compatible", "Compatible", "Kompatibel", "Compatible")
                        : multiplayer.roomCompatibility.level === "waiting"
                          ? pick("Aguardando outro jogador", "Waiting for another player", "Esperando otro jugador", "Warte auf weiteren Spieler", "En attente d’un autre joueur")
                          : pick("Sem sessão", "No session", "Sin sesión", "Keine Sitzung", "Aucune session")}
              </strong>
            </div>
            <p>
              {multiplayer.roomCompatibility.remoteCount > 0
                ? `${multiplayer.roomCompatibility.remoteCount} ${pick("remoto(s)", "remote player(s)", "jugador(es) remoto(s)", "Remote-Spieler", "joueur(s) distant(s)")} · ${multiplayer.roomCompatibility.blocking} ${pick("bloqueio(s)", "block(s)", "bloqueo(s)", "Blockierungen", "blocage(s)")} · ${multiplayer.roomCompatibility.warnings} ${pick("aviso(s)", "warning(s)", "aviso(s)", "Warnungen", "avertissement(s)")}`
                : pick("A comparação usa mapa, ônibus, HOF, protocolo e a exigência de ônibus físico quando ativada.", "Comparison uses map, bus, HOF, protocol, and the physical-bus requirement when enabled.", "La comparación usa mapa, autobús, HOF, protocolo y la exigencia de autobús físico cuando está activada.", "Der Vergleich nutzt Karte, Bus, HOF, Protokoll und bei Aktivierung die physische Bus-Anforderung.", "La comparaison utilise carte, bus, HOF, protocole et l’exigence de bus physique lorsqu’elle est activée.")}
            </p>
            {multiplayer.roomCompatibility.affectedAreas.length > 0 && (
              <small>{pick("Áreas", "Areas", "Áreas", "Bereiche", "Zones")}: {multiplayer.roomCompatibility.affectedAreas.join(", ")}</small>
            )}
          </article>
          {visiblePlayers.length === 0 ? (
            <div className="empty-state">{pick("Nenhum jogador remoto disponível.", "No remote player available.", "Ningún jugador remoto disponible.", "Kein Remote-Spieler verfügbar.", "Aucun joueur distant disponible.")}</div>
          ) : (
            <div className="players-table">
              {visiblePlayers.map(player => (
                <div className="player-row" key={player.playerId}>
                  <span className={`avatar-dot ${player.roleplayActive ? "rp" : ""}`}>{player.displayName.slice(0, 1).toUpperCase()}</span>
                  <div className="player-main">
                    <strong>{player.displayName}{player.isLocal ? ` · ${pick("Você", "You", "Tú", "Du", "Vous")}` : ""}</strong>
                    {validCompanyBadge(player.companyBadge) && (
                      <small className="company-badge-inline">
                        {player.companyBadge?.companyShortName || player.companyBadge?.companyName} · {pick("Crachá", "Badge", "Credencial", "Ausweis", "Badge")} {player.companyBadge?.employeeNumber} · {companyRoleLabel(player.companyBadge?.role || "", pick)}
                      </small>
                    )}
                    <small>
                      {[player.line, player.route, player.mapName]
                        .filter(Boolean)
                        .join(" · ") || pick("Sem serviço informado", "No service reported", "Sin servicio informado", "Kein Dienst gemeldet", "Aucun service indiqué")}
                    </small>
                    <small
                      title={!player.isLocal && multiplayer.physicalVehiclesEnabled
                        ? player.physicalVehicleErrorMessage || undefined
                        : undefined}
                    >
                      {player.vehicleName || pick("Ônibus não informado", "Bus not provided", "Autobús no informado", "Bus nicht angegeben", "Bus non renseigné")}
                      {(player.destinationName || player.nextStopName) ? ` → ${player.destinationName || player.nextStopName}` : ""}
                      {!player.isLocal && multiplayer.physicalVehiclesEnabled
                        ? ` · ${physicalVehicleStatusLabel(player.physicalVehicleState, player.physicalVehicleErrorCode, player.physicalVehiclePartCount, player.physicalVehicleExpectedPartCount, pick)}`
                        : ""}
                    </small>
                    {!player.isLocal &&
                      multiplayer.physicalVehiclesEnabled &&
                      player.openOmsiVisualSyncStatus &&
                      player.openOmsiVisualSyncStatus !== "none" && (
                        <small className="physical-runtime-detail">
                          {pick("Sync visual", "Visual sync", "Sync visual", "Visual-Sync", "Sync visuelle")}: {
                            player.openOmsiVisualSyncStatus === "compatible"
                              ? pick("compatível", "compatible", "compatible", "kompatibel", "compatible")
                              : player.openOmsiVisualSyncStatus === "mismatch"
                                ? pick("incompatível", "mismatch", "incompatible", "inkompatibel", "incompatible")
                                : player.openOmsiVisualSyncStatus === "basic"
                                  ? pick("fallback básico", "basic fallback", "fallback básico", "Basis-Fallback", "fallback de base")
                                  : pick("aguardando", "pending", "pendiente", "ausstehend", "en attente")
                          }
                          {player.openOmsiVisualSyncHash ? ` · ${player.openOmsiVisualSyncHash}` : ""}
                        </small>
                      )}
                    {!player.isLocal &&
                      multiplayer.physicalVehiclesEnabled &&
                      player.physicalVehicleErrorMessage &&
                      player.physicalVehicleState !== "active" &&
                      player.physicalVehicleState !== "active-openomsi" &&
                       player.physicalVehicleState !== "active-openomsi-drawn" && (
                        <small className="physical-error-detail">
                          {player.physicalVehicleErrorMessage}
                        </small>
                      )}
                    {!player.isLocal &&
                      multiplayer.physicalVehiclesEnabled &&
                      player.physicalVehicleState !== "active" &&
                      player.physicalVehicleState !== "active-openomsi" &&
                       player.physicalVehicleState !== "active-openomsi-drawn" &&
                      (player.physicalTelemetryGridX != null ||
                       player.physicalTelemetryGridY != null ||
                       player.physicalTelemetryNavigationGridX != null ||
                       player.physicalTelemetryNavigationGridY != null ||
                       player.physicalTelemetryLocalX != null ||
                       player.physicalTelemetryLocalY != null) && (
                        <small className="physical-runtime-detail">
                          {[
                            player.physicalTelemetryGridX != null && player.physicalTelemetryGridY != null
                              ? `PhysGrid ${player.physicalTelemetryGridX}/${player.physicalTelemetryGridY}`
                              : pick("PhysGrid indisponível", "PhysGrid unavailable", "PhysGrid no disponible", "PhysGrid nicht verfügbar", "PhysGrid indisponible"),
                            player.physicalTelemetryNavigationGridX != null && player.physicalTelemetryNavigationGridY != null
                              ? `NavGrid ${player.physicalTelemetryNavigationGridX}/${player.physicalTelemetryNavigationGridY}`
                              : null,
                            player.physicalTelemetryLocalX != null && player.physicalTelemetryLocalY != null
                              ? `Local ${format(player.physicalTelemetryLocalX, 1)} / ${format(player.physicalTelemetryLocalY, 1)}${player.physicalTelemetryLocalZ != null ? ` / ${format(player.physicalTelemetryLocalZ, 1)}` : ""}`
                              : null,
                            player.physicalTelemetryTileX != null && player.physicalTelemetryTileY != null
                              ? `TileXY ${format(player.physicalTelemetryTileX, 1)} / ${format(player.physicalTelemetryTileY, 1)}`
                              : null,
                            player.physicalTelemetryRemoteTileIndex != null
                              ? `Kachel(remote) #${player.physicalTelemetryRemoteTileIndex}`
                              : null
                          ].filter(Boolean).join(" · ")}
                        </small>
                      )}
                  </div>
                  <span className={`voice-state ${player.speaking ? "speaking" : ""} ${player.telemetryStale ? "stale" : ""}`}>
                    {player.speaking
                      ? pick("Falando", "Speaking", "Hablando", "Spricht", "Parle")
                      : player.telemetryStale
                        ? pick("Telemetria atrasada", "Stale telemetry", "Telemetría atrasada", "Verzögerte Telemetrie", "Télémétrie en retard")
                        : player.voiceEnabled
                          ? pick("Voz ativa", "Voice active", "Voz activa", "Sprache aktiv", "Voix active")
                          : pick("Ao vivo", "Live", "En vivo", "Live", "En direct")}
                  </span>
                  <span className="latency">
                    {player.speedKph == null ? "—" : `${format(player.speedKph, 0)} km/h`}
                    {" · "}{player.distanceText || "—"}
                    {player.delaySeconds == null ? "" : ` · ${formatDelay(player.delaySeconds, pick)}`}
                    {" · "}{player.isLocal
                      ? pick("agora", "now", "ahora", "jetzt", "maintenant")
                      : player.telemetryAgeSeconds == null
                        ? pick("aguardando", "waiting", "esperando", "wartet", "en attente")
                        : player.telemetryAgeSeconds < 1
                          ? pick("agora", "now", "ahora", "jetzt", "maintenant")
                          : `${format(player.telemetryAgeSeconds, 0)}s`}
                    {" · "}{player.latencyMs == null ? "—" : `${player.latencyMs} ms`}
                  </span>
                </div>
              ))}
            </div>
          )}
        </section>
      )}

      {tab === "chat" && (
        <section className="mp-chat-layout">
          <article className="card chat-card">
            <div className="section-heading">
              <div><span className="eyebrow">CHAT</span><h3>{pick("Mensagens da sala", "Room messages", "Mensajes de sala", "Raumnachrichten", "Messages de la salle")}</h3></div>
            </div>
            <div className="chat-status-row">
              <span className={multiplayer.connected ? "online" : "offline"}>
                {multiplayer.connected
                  ? pick("Conectado", "Connected", "Conectado", "Verbunden", "Connecté")
                  : pick("Desconectado", "Disconnected", "Desconectado", "Getrennt", "Déconnecté")}
              </span>
              <span>{multiplayer.chat.length} {pick("mensagens", "messages", "mensajes", "Nachrichten", "messages")}</span>
              <span>{pick("Canal", "Channel", "Canal", "Kanal", "Canal")}: {voiceChannel}</span>
            </div>
            <div className="chat-log" ref={chatLogRef} aria-live="polite">
              {multiplayer.chat.length === 0 ? (
                <div className="empty-state">{pick("Nenhuma mensagem recebida.", "No message received.", "Ningún mensaje recibido.", "Keine Nachricht empfangen.", "Aucun message reçu.")}</div>
              ) : multiplayer.chat.map((message, index) => (
                <div className={`chat-message ${message.isSystem ? "system" : ""}`} key={`${message.timestampUtc}-${index}`}>
                  <div><strong>{message.isSystem ? "NavBR" : message.displayName}</strong><time>{new Date(message.timestampUtc).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</time></div>
                  <p>{message.text}</p>
                </div>
              ))}
            </div>
            <form className="chat-compose" onSubmit={submitChat}>
              <input value={chatText} onChange={event => setChatText(event.target.value)} disabled={!multiplayer.connected} placeholder={multiplayer.connected ? pick("Escreva uma mensagem…", "Write a message…", "Escribe un mensaje…", "Nachricht schreiben…", "Écrivez un message…") : pick("Conecte-se para conversar", "Connect to chat", "Conéctate para conversar", "Zum Chatten verbinden", "Connectez-vous pour discuter")} />
              <button className="button primary" disabled={!multiplayer.connected || !chatText.trim()}>{pick("Enviar", "Send", "Enviar", "Senden", "Envoyer")}</button>
            </form>
          </article>
          <aside className="card voice-card">
            <span className="eyebrow">VOZ</span>
            <h3>{multiplayer.voicePushToTalkActive ? pick("Transmitindo", "Transmitting", "Transmitiendo", "Sendet", "Transmission") : multiplayer.voiceEnabled ? pick("Voz habilitada", "Voice enabled", "Voz habilitada", "Sprache aktiviert", "Voix activée") : pick("Voz desativada", "Voice disabled", "Voz desactivada", "Sprache deaktiviert", "Voix désactivée")}</h3>

            <div className="details-grid">
              <div><small>PTT</small><strong>{multiplayer.voicePushToTalkActive ? pick("Ativo", "Active", "Activo", "Aktiv", "Actif") : pick("Inativo", "Inactive", "Inactivo", "Inaktiv", "Inactif")}</strong></div>
              <div><small>{pick("STREAMS", "STREAMS", "STREAMS", "STREAMS", "FLUX")}</small><strong>{multiplayer.voiceQuality.activeStreams}</strong></div>
              <div><small>{pick("JITTER", "JITTER", "JITTER", "JITTER", "JITTER")}</small><strong>{format(multiplayer.voiceQuality.averageJitterMilliseconds, 0)} ms</strong></div>
              <div><small>{pick("PERDA EST.", "EST. LOSS", "PÉRDIDA EST.", "GESCH. VERLUST", "PERTE EST.")}</small><strong>{format(multiplayer.voiceQuality.estimatedLossPercent, 1)}%</strong></div>
              <div><small>FEC</small><strong>{multiplayer.voiceQuality.fecRecoveredPackets}</strong></div>
              <div><small>{pick("BUFFER", "BUFFER", "BÚFER", "PUFFER", "TAMPON")}</small><strong>{multiplayer.voiceQuality.targetBufferMilliseconds} ms</strong></div>
            </div>

            <label className="voice-toggle">
              <input
                type="checkbox"
                checked={multiplayer.voiceEnabled}
                onChange={event => sendCommand("setVoiceEnabled", { enabled: event.target.checked })}
              />
              <span>{pick("Ativar voz na sala", "Enable room voice", "Activar voz en la sala", "Raum-Sprache aktivieren", "Activer la voix dans la salle")}</span>
            </label>

            <label className="voice-field">
              <span>{pick("Canal", "Channel", "Canal", "Kanal", "Canal")}</span>
              <select
                value={voiceChannel}
                onChange={event => {
                  const channel = event.target.value;
                  setVoiceChannel(channel);
                  setVoiceDirty(true);
                  sendCommand("configureVoice", {
                    channel,
                    proximityMeters: voiceRadius,
                    deafened: voiceDeafened
                  });
                }}
              >
                <option value="general">{pick("Geral", "General", "General", "Allgemein", "Général")}</option>
                <option value="company">{pick("Empresa/equipe", "Company/team", "Empresa/equipo", "Unternehmen/Team", "Entreprise/équipe")}</option>
                <option value="dispatch">CCO</option>
                <option value="proximity">{pick("Proximidade", "Proximity", "Proximidad", "Nähe", "Proximité")}</option>
              </select>
            </label>

            {voiceChannel === "proximity" && (
              <label className="voice-field">
                <span>{pick("Raio de proximidade", "Proximity radius", "Radio de proximidad", "Nähe-Radius", "Rayon de proximité")}: {voiceRadius.toFixed(0)} m</span>
                <input
                  type="range"
                  min="20"
                  max="1000"
                  step="10"
                  value={voiceRadius}
                  onChange={event => {
                    setVoiceRadius(Number(event.target.value));
                    setVoiceDirty(true);
                  }}
                  onMouseUp={() => sendCommand("configureVoice", {
                    channel: voiceChannel,
                    proximityMeters: voiceRadius,
                    deafened: voiceDeafened
                  })}
                  onTouchEnd={() => sendCommand("configureVoice", {
                    channel: voiceChannel,
                    proximityMeters: voiceRadius,
                    deafened: voiceDeafened
                  })}
                  onKeyUp={event => {
                    if (
                      event.key === "ArrowLeft" ||
                      event.key === "ArrowRight" ||
                      event.key === "Home" ||
                      event.key === "End"
                    ) {
                      sendCommand("configureVoice", {
                        channel: voiceChannel,
                        proximityMeters: voiceRadius,
                        deafened: voiceDeafened
                      });
                    }
                  }}
                />
              </label>
            )}

            <label className="voice-toggle">
              <input
                type="checkbox"
                checked={voiceDeafened}
                onChange={event => {
                  const deafened = event.target.checked;
                  setVoiceDeafened(deafened);
                  setVoiceDirty(true);
                  sendCommand("configureVoice", {
                    channel: voiceChannel,
                    proximityMeters: voiceRadius,
                    deafened
                  });
                }}
              />
              <span>{pick("Silenciar áudio remoto", "Mute remote audio", "Silenciar audio remoto", "Remote-Audio stummschalten", "Couper l’audio distant")}</span>
            </label>

            <div className="voice-device-grid">
              <label className="voice-field">
                <span>{pick("Microfone", "Microphone", "Micrófono", "Mikrofon", "Microphone")}</span>
                <select
                  value={multiplayer.voiceInputDeviceNumber}
                  onChange={event => sendCommand("configureVoiceDevices", {
                    inputDeviceNumber: Number(event.target.value),
                    outputDeviceNumber: multiplayer.voiceOutputDeviceNumber
                  })}
                >
                  {multiplayer.voiceInputDevices.length === 0
                    ? <option value={0}>{pick("Nenhum microfone detectado", "No microphone detected", "Ningún micrófono detectado", "Kein Mikrofon erkannt", "Aucun microphone détecté")}</option>
                    : multiplayer.voiceInputDevices.map(device => (
                      <option key={device.deviceNumber} value={device.deviceNumber}>{device.displayName}</option>
                    ))}
                </select>
              </label>

              <label className="voice-field">
                <span>{pick("Saída de áudio", "Audio output", "Salida de audio", "Audioausgabe", "Sortie audio")}</span>
                <select
                  value={multiplayer.voiceOutputDeviceNumber}
                  onChange={event => sendCommand("configureVoiceDevices", {
                    inputDeviceNumber: multiplayer.voiceInputDeviceNumber,
                    outputDeviceNumber: Number(event.target.value)
                  })}
                >
                  {multiplayer.voiceOutputDevices.map(device => (
                    <option key={device.deviceNumber} value={device.deviceNumber}>{device.displayName}</option>
                  ))}
                </select>
              </label>
            </div>

            <div className="voice-mixer">
              <div className="section-heading compact">
                <div><span className="eyebrow">MIXER</span><h3>{pick("Jogadores", "Players", "Jugadores", "Spieler", "Joueurs")}</h3></div>
              </div>
              {multiplayer.voiceMixers.length === 0 ? (
                <div className="empty-state compact-empty">{pick("Nenhum jogador remoto para ajustar.", "No remote player to adjust.", "Ningún jugador remoto para ajustar.", "Kein Remote-Spieler zum Anpassen.", "Aucun joueur distant à régler.")}</div>
              ) : multiplayer.voiceMixers.map(player => (
                <div className="voice-mixer-row" key={player.playerId}>
                  <div>
                    <strong>{player.displayName}</strong>
                    <small>{player.speaking ? pick("Falando agora", "Speaking now", "Hablando ahora", "Spricht gerade", "Parle maintenant") : player.muted ? pick("Mutado", "Muted", "Silenciado", "Stumm", "Muet") : pick("Áudio ativo", "Audio active", "Audio activo", "Audio aktiv", "Audio actif")}</small>
                  </div>
                  <label className="voice-mute-toggle">
                    <input
                      type="checkbox"
                      checked={player.muted}
                      onChange={event => sendCommand("configureRemoteVoice", {
                        playerId: player.playerId,
                        muted: event.target.checked,
                        gain: player.gain
                      })}
                    />
                    <span>Mute</span>
                  </label>
                  <label className="voice-gain">
                    <span>{Math.round((voiceMixerDrafts[player.playerId] ?? player.gain) * 100)}%</span>
                    <input
                      type="range"
                      min="0"
                      max="2"
                      step="0.05"
                      value={voiceMixerDrafts[player.playerId] ?? player.gain}
                      onChange={event => {
                        const gain = Number(event.target.value);
                        setVoiceMixerDrafts(current => ({ ...current, [player.playerId]: gain }));
                      }}
                      onMouseUp={() => sendCommand("configureRemoteVoice", {
                        playerId: player.playerId,
                        muted: player.muted,
                        gain: voiceMixerDrafts[player.playerId] ?? player.gain
                      })}
                      onTouchEnd={() => sendCommand("configureRemoteVoice", {
                        playerId: player.playerId,
                        muted: player.muted,
                        gain: voiceMixerDrafts[player.playerId] ?? player.gain
                      })}
                      onKeyUp={event => {
                        if (event.key === "ArrowLeft" || event.key === "ArrowRight" || event.key === "Home" || event.key === "End") {
                          sendCommand("configureRemoteVoice", {
                            playerId: player.playerId,
                            muted: player.muted,
                            gain: voiceMixerDrafts[player.playerId] ?? player.gain
                          });
                        }
                      }}
                    />
                  </label>
                </div>
              ))}
            </div>
          </aside>
        </section>
      )}

      {tab === "roleplay" && <RoleplayPanel state={state} error={error} embedded />}

      {tab === "advanced" && (
        <section className="advanced-grid">
          <article className="card compact-card">
            <span className="eyebrow">HUD</span>
            <h3>{pick("Mover HUD", "Move HUD", "Mover HUD", "HUD verschieben", "Déplacer le HUD")}</h3>
            <p>{pick("Ativa o modo de reposicionamento do overlay nativo.", "Enables native overlay repositioning mode.", "Activa el modo de reposicionamiento del overlay nativo.", "Aktiviert den Verschiebemodus des nativen Overlays.", "Active le mode de repositionnement de l’overlay natif.")}</p>
            <button className="button ghost" onClick={() => sendCommand("toggleHudLayout")}>{pick("Mover HUD", "Move HUD", "Mover HUD", "HUD verschieben", "Déplacer le HUD")}</button>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">HUD</span>
            <h3>{pick("Configurar HUD", "Configure HUD", "Configurar HUD", "HUD konfigurieren", "Configurer le HUD")}</h3>
            <p>{pick("Ajuste presets, tema, escala, opacidade e módulos do HUD.", "Adjust HUD presets, theme, scale, opacity and modules.", "Ajusta presets, tema, escala, opacidad y módulos del HUD.", "Passe HUD-Presets, Thema, Skalierung, Deckkraft und Module an.", "Réglez les préréglages, le thème, l’échelle, l’opacité et les modules du HUD.")}</p>
            <button className="button ghost" onClick={onOpenHud}>{pick("Configurar HUD", "Configure HUD", "Configurar HUD", "HUD konfigurieren", "Configurer le HUD")}</button>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">{pick("REDE", "NETWORK", "RED", "NETZWERK", "RÉSEAU")}</span>
            <h3>{pick("Host local", "Local host", "Host local", "Lokaler Host", "Hôte local")}</h3>
            <p>{multiplayer.hostRunning ? `${pick("Escutando na porta TCP", "Listening on TCP port", "Escuchando en el puerto TCP", "Lauscht auf TCP-Port", "Écoute sur le port TCP")} ${multiplayer.hostPort ?? 27730}.` : pick("Host local não está ativo.", "Local host is not active.", "El host local no está activo.", "Lokaler Host ist nicht aktiv.", "L’hôte local n’est pas actif.")}</p>
            <button className="button ghost" onClick={onOpenNetwork}>{pick("Abrir Configurações", "Open Settings", "Abrir Configuración", "Einstellungen öffnen", "Ouvrir les paramètres")} &gt; {pick("Rede", "Network", "Red", "Netzwerk", "Réseau")}</button>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">{pick("ÔNIBUS FÍSICOS", "PHYSICAL BUSES", "AUTOBUSES FÍSICOS", "PHYSISCHE BUSSE", "BUS PHYSIQUES")}</span>
            <h3>{pick("Jogadores dentro do OMSI", "Players inside OMSI", "Jugadores dentro de OMSI", "Spieler in OMSI", "Joueurs dans OMSI")}</h3>
            <p>{multiplayer.physicalVehiclesAvailable
              ? pick("Ativo. openOMSI v6 controla spawn e movimento dos jogadores dentro do OMSI.", "Active. openOMSI v6 controls player spawn and motion inside OMSI.", "Activo. openOMSI v6 controla spawn y movimiento dentro de OMSI.", "Aktiv. openOMSI v6 steuert Spawn und Bewegung der Spieler in OMSI.", "Actif. openOMSI v6 contrôle l’apparition et le mouvement des joueurs dans OMSI.")
              : pick("Aguardando o Plugin Bridge anunciar as capacidades físicas necessárias.", "Waiting for Plugin Bridge to advertise the required physical capabilities.", "Esperando que Plugin Bridge anuncie las capacidades físicas necesarias.", "Wartet auf die erforderlichen physischen Plugin-Bridge-Fähigkeiten.", "En attente des capacités physiques requises du Plugin Bridge.")}</p>
          </article>

          <article className="card compact-card">
            <span className="eyebrow">{pick("ATALHOS", "HOTKEYS", "ATAJOS", "HOTKEYS", "RACCOURCIS")}</span>
            <h3>{pick("Chat e Push-to-Talk", "Chat and Push-to-Talk", "Chat y Push-to-Talk", "Chat und Push-to-Talk", "Chat et Push-to-Talk")}</h3>
            <div className="voice-device-grid">
              <label className="voice-field">
                <span>{pick("Abrir chat", "Open chat", "Abrir chat", "Chat öffnen", "Ouvrir le chat")}</span>
                <select
                  value={chatHotkey}
                  onChange={event => {
                    const value = event.target.value;
                    setChatHotkey(value);
                    setHotkeysDirty(true);
                    sendCommand("configureMultiplayerHotkeys", { chatHotkey: value, voiceHotkey });
                  }}
                >
                  {multiplayer.hotkeyOptions.map(option => <option key={option} value={option}>{option}</option>)}
                </select>
              </label>
              <label className="voice-field">
                <span>PTT</span>
                <select
                  value={voiceHotkey}
                  onChange={event => {
                    const value = event.target.value;
                    setVoiceHotkey(value);
                    setHotkeysDirty(true);
                    sendCommand("configureMultiplayerHotkeys", { chatHotkey, voiceHotkey: value });
                  }}
                >
                  {multiplayer.hotkeyOptions.map(option => <option key={option} value={option}>{option}</option>)}
                </select>
              </label>
            </div>
          </article>
        </section>
      )}
    </>
  );
}


function ghostStatusLabel(status: string | null | undefined, t: (key: string) => string) {
  switch (status) {
    case "recording": return t("ghost.recording");
    case "recording-saved": return t("ghost.saved");
    case "recording-cancelled": return t("ghost.cancelled");
    case "ghost-loaded": return t("ghost.loaded");
    case "ghost-imported": return t("ghost.imported");
    case "playback-starting": return t("ghost.playbackStarting");
    case "playback-stopped": return t("ghost.playbackStopped");
    case "playback-completed": return t("ghost.playbackCompleted");
    case "playback-failed": return t("ghost.playbackUnavailable");
    default: return status || t("common.ready");
  }
}


function formatReplayDuration(seconds: number | undefined | null) {
  if (seconds == null || !Number.isFinite(seconds) || seconds < 0) return "—";
  const rounded = Math.max(0, Math.round(seconds));
  const hours = Math.floor(rounded / 3600);
  const minutes = Math.floor((rounded % 3600) / 60);
  const secs = rounded % 60;
  return hours > 0
    ? `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(secs).padStart(2, "0")}`
    : `${String(minutes).padStart(2, "0")}:${String(secs).padStart(2, "0")}`;
}

function GhostRoutePreview({
  points
}: {
  points: { x: number; z: number; offsetMilliseconds: number }[];
}) {
  const plot = useMemo(() => {
    if (points.length < 2) return null;
    const minX = Math.min(...points.map(point => point.x));
    const maxX = Math.max(...points.map(point => point.x));
    const minZ = Math.min(...points.map(point => point.z));
    const maxZ = Math.max(...points.map(point => point.z));
    const spanX = Math.max(1, maxX - minX);
    const spanZ = Math.max(1, maxZ - minZ);
    const padding = 7;
    const width = 100 - padding * 2;
    const height = 100 - padding * 2;
    const scale = Math.min(width / spanX, height / spanZ);
    const renderedWidth = spanX * scale;
    const renderedHeight = spanZ * scale;
    const offsetX = (100 - renderedWidth) / 2;
    const offsetY = (100 - renderedHeight) / 2;
    return points.map(point => ({
      ...point,
      px: offsetX + (point.x - minX) * scale,
      py: 100 - (offsetY + (point.z - minZ) * scale)
    }));
  }, [points]);

  if (!plot || plot.length < 2) {
    return <div className="empty-state">Replay sem coordenadas suficientes para pré-visualização.</div>;
  }

  return (
    <div className="ghost-route-preview">
      <svg viewBox="0 0 100 100" role="img" aria-label="Trajeto real gravado no Ghost">
        <polyline
          className="ghost-route-shadow"
          points={plot.map(point => `${point.px},${point.py}`).join(" ")}
        />
        <polyline
          className="ghost-route-line"
          points={plot.map(point => `${point.px},${point.py}`).join(" ")}
        />
        <circle className="ghost-route-start" cx={plot[0].px} cy={plot[0].py} r="1.8" />
        <circle className="ghost-route-end" cx={plot[plot.length - 1].px} cy={plot[plot.length - 1].py} r="2.2" />
      </svg>
    </div>
  );
}

function GhostReplay({
  state,
  error
}: {
  state: NavBrState | null;
  error: string | null;
}) {
  const { t, pick } = useI18n();
  const ghost: NavBrGhostState | undefined = state?.ghost;
  const [recordName, setRecordName] = useState("");
  const [playbackSpeed, setPlaybackSpeed] = useState(1);
  const [loop, setLoop] = useState(false);
  const [compareFiles, setCompareFiles] = useState<string[]>([]);

  useEffect(() => {
    sendCommand("refreshGhostLibrary");
  }, []);

  if (!ghost) {
    return <div className="card empty-state">{t("common.waiting")} Ghost / Replay…</div>;
  }

  const selected = ghost.selected;
  const analytics = selected?.analytics;
  const canRecord = Boolean(state?.telemetry?.inGame) && !ghost.recording && !ghost.playing;
  const canPlay = Boolean(ghost.selectedPath) && !ghost.recording && !ghost.playing;
  const compared = compareFiles
    .map(fileName => ghost.library.find(item => item.fileName === fileName))
    .filter((item): item is NavBrGhostState["library"][number] => Boolean(item));
  const toggleCompare = (fileName: string) => {
    setCompareFiles(current => current.includes(fileName)
      ? current.filter(item => item !== fileName)
      : current.length < 2
        ? [...current, fileName]
        : [current[1], fileName]);
  };

  return (
    <>
      <header className="topbar ghost-header">
        <div>
          <span className="eyebrow">GHOST / REPLAY</span>
          <h1>{t("ghost.title")}</h1>
          <p>{t("ghost.subtitle")}</p>
        </div>
        <div className="top-actions">
          <span className={`connection-pill ${ghost.recording || ghost.playing ? "connected" : ""}`}>
            <i /> {ghost.recording ? `Gravando · ${ghost.frameCount} frames` : ghost.playing ? t("ghost.playing") : ghostStatusLabel(ghost.status, t)}
          </span>
        </div>
      </header>

      {(error || ghost.error) && <div className="command-error">{error || ghost.error}</div>}

      <section className="ghost-layout">
        <article className="card ghost-record-card">
          <div className="section-heading">
            <div><span className="eyebrow">{t("ghost.recordingSection")}</span><h3>{t("ghost.realTelemetry")}</h3></div>
            <span className={`hardware-state-pill ${state?.telemetry?.inGame ? "connected" : ""}`}>
              {state?.telemetry?.inGame ? t("ghost.omsiReady") : t("ghost.waitingMapBus")}
            </span>
          </div>

          <label className="voice-field">
            <span>{t("ghost.recordName")}</span>
            <input
              value={recordName}
              disabled={ghost.recording || ghost.playing}
              onChange={event => setRecordName(event.target.value)}
              placeholder="Opcional — ex.: Linha 675N manhã"
            />
          </label>

          <div className="ghost-actions">
            <button
              className="button primary"
              disabled={!canRecord}
              onClick={() => sendCommand("startGhostRecording", { name: recordName })}
            >
              <NavBrIcon name="record" size={14} />{t("ghost.recordTrip")}
            </button>
            <button
              className="button ghost"
              disabled={!ghost.recording}
              onClick={() => sendCommand("stopGhostRecording")}
            >
              <NavBrIcon name="stop" size={14} />{t("ghost.stopSave")}
            </button>
            <button
              className="button ghost danger"
              disabled={!ghost.recording}
              onClick={() => sendCommand("cancelGhostRecording")}
            >
              {t("ghost.cancelRecording")}
            </button>
          </div>

          <div className="ghost-record-stats">
            <span><small>FRAMES</small><strong>{ghost.frameCount.toLocaleString()}</strong></span>
            <span><small>CADÊNCIA</small><strong>100 ms</strong></span>
            <span><small>MODO</small><strong>{t("ghost.readOnly")}</strong></span>
          </div>
        </article>

        <article className="card ghost-file-card">
          <div className="section-heading">
            <div><span className="eyebrow">{t("ghost.file")}</span><h3>{selected?.name || t("ghost.noneLoaded")}</h3></div>
          </div>

          <div className="ghost-actions">
            <button
              className="button ghost"
              disabled={ghost.recording || ghost.playing}
              onClick={() => sendCommand("selectGhostFile")}
            >
              {t("ghost.open")}
            </button>
            <button className="button ghost" onClick={() => sendCommand("openGhostFolder")}>
              {t("ghost.openFolder")}
            </button>
          </div>

          <code className="ghost-path">{ghost.selectedPath || ghost.ghostDirectory}</code>

          {selected && (
            <div className="ghost-metadata-grid">
              <span><small>MAPA</small><strong>{selected.mapName || "—"}</strong></span>
              <span><small>VEÍCULO</small><strong>{selected.vehicleName || "—"}</strong></span>
              <span><small>HOF</small><strong>{selected.hofName || "—"}</strong></span>
              <span><small>DURAÇÃO</small><strong>{formatReplayDuration(selected.durationSeconds)}</strong></span>
              <span><small>FRAMES</small><strong>{selected.frameCount.toLocaleString()}</strong></span>
              <span><small>LINHA</small><strong>{selected.line || "—"}</strong></span>
              <span><small>GRAVADO</small><strong>{new Date(selected.recordedAtUtc).toLocaleString()}</strong></span>
            </div>
          )}
        </article>
      </section>

      <section className="ghost-layout ghost-secondary">
        <article className="card ghost-playback-card">
          <div className="section-heading">
            <div><span className="eyebrow">GHOST 3D</span><h3>{t("ghost.playback")}</h3></div>
            <span className={`hardware-state-pill ${ghost.playing ? "connected" : ""}`}>
              {ghost.playing ? t("ghost.playing") : t("ghost.stopped")}
            </span>
          </div>

          <label className="voice-field">
            <span>{t("ghost.speed")}: {playbackSpeed.toFixed(1)}×</span>
            <input
              type="range"
              min="0.1"
              max="4"
              step="0.1"
              disabled={ghost.playing}
              value={playbackSpeed}
              onChange={event => setPlaybackSpeed(Number(event.target.value))}
            />
          </label>

          <label className="diagnostics-toggle compact-toggle">
            <input
              type="checkbox"
              checked={loop}
              disabled={ghost.playing}
              onChange={event => setLoop(event.target.checked)}
            />
            <span>{t("ghost.loop")}</span>
          </label>

          <div className="ghost-actions">
            <button
              className="button primary"
              disabled={!canPlay}
              onClick={() => sendCommand("playGhost", { playbackSpeed, loop })}
            >
              <NavBrIcon name="play" size={14} />{t("ghost.play")}
            </button>
            <button
              className="button ghost"
              disabled={!ghost.playing}
              onClick={() => sendCommand("stopGhostPlayback")}
            >
              <NavBrIcon name="stop" size={14} />{t("ghost.stopPlayback")}
            </button>
          </div>

          <p className="migration-note">
            {t("ghost.safeFailure")}
          </p>
        </article>

        <article className="card ghost-analytics-card">
          <div className="section-heading">
            <div><span className="eyebrow">ANALYTICS</span><h3>{t("ghost.metrics")}</h3></div>
          </div>

          {!analytics ? (
            <div className="empty-state">{t("ghost.noAnalytics")}</div>
          ) : (
            <div className="ghost-analytics-grid">
              <span><small>{t("ghost.estimatedDistance")}</small><strong>{analytics.estimatedDistanceKm.toFixed(2)} km</strong></span>
              <span><small>{t("ghost.averageSpeed")}</small><strong>{analytics.averageSpeedKph.toFixed(1)} km/h</strong></span>
              <span><small>{t("ghost.maximumSpeed")}</small><strong>{analytics.maximumSpeedKph.toFixed(1)} km/h</strong></span>
              <span><small>{t("ghost.validSamples")}</small><strong>{analytics.validSpeedSamples.toLocaleString()}</strong></span>
            </div>
          )}
        </article>
      </section>

      <section className="ghost-layout ghost-secondary">
        <article className="card ghost-library-card">
          <div className="section-heading">
            <div><span className="eyebrow">{t("ghost.library")}</span><h3>{t("ghost.localReplays")}</h3></div>
            <div className="ghost-library-actions">
              <button
                className="button ghost compact"
                disabled={ghost.recording || ghost.playing}
                onClick={() => sendCommand("importGhostReplay")}
              >
                {t("common.import")}
              </button>
              <button
                className="button ghost compact"
                onClick={() => sendCommand("refreshGhostLibrary")}
              >
                {t("common.refresh")}
              </button>
            </div>
          </div>

          {ghost.library.length === 0 ? (
            <div className="empty-state">
              {ghost.libraryInvalidCount > 0
                ? t("ghost.noValid", { count: ghost.libraryInvalidCount })
                : t("ghost.noReplays")}
            </div>
          ) : (
            <div className="ghost-library-list">
              {ghost.library.map(item => (
                <div key={item.fileName} className={`ghost-library-row ${item.selected ? "selected" : ""}`}>
                  <button
                    className="ghost-library-main"
                    disabled={ghost.recording || ghost.playing}
                    onClick={() => sendCommand("selectGhostLibraryItem", { fileName: item.fileName })}
                  >
                    <span>
                      <strong>{item.name}</strong>
                      <small>{item.mapName || "Mapa —"} · {item.vehicleName || "Veículo —"}</small>
                    </span>
                    <span>
                      <strong>{formatReplayDuration(item.durationSeconds)}</strong>
                      <small>{item.estimatedDistanceKm.toFixed(2)} km · {item.frameCount.toLocaleString()} frames</small>
                    </span>
                  </button>
                  <button
                    className={`button ghost compact ${compareFiles.includes(item.fileName) ? "active" : ""}`}
                    disabled={ghost.recording || ghost.playing}
                    onClick={() => toggleCompare(item.fileName)}
                  >
                    {compareFiles.includes(item.fileName) ? pick("Selecionado", "Selected", "Seleccionado", "Ausgewählt", "Sélectionné") : pick("Comparar", "Compare", "Comparar", "Vergleichen", "Comparer")}
                  </button>
                </div>
              ))}
            </div>
          )}

          {ghost.libraryInvalidCount > 0 && ghost.library.length > 0 && (
            <p className="migration-note">{t("ghost.invalidIgnored", { count: ghost.libraryInvalidCount })}</p>
          )}
        </article>

        <article className="card ghost-route-card">
          <div className="section-heading">
            <div><span className="eyebrow">{pick("COMPARAÇÃO", "COMPARISON", "COMPARACIÓN", "VERGLEICH", "COMPARAISON")}</span><h3>{pick("Comparar replays", "Compare replays", "Comparar replays", "Replays vergleichen", "Comparer les replays")}</h3></div>
            <span className="route-source-pill">{compared.length}/2</span>
          </div>
          {compared.length !== 2 ? (
            <div className="empty-state compact-empty">{pick("Marque dois replays na biblioteca para comparar métricas reais.", "Select two replays in the library to compare real metrics.", "Selecciona dos replays en la biblioteca para comparar métricas reales.", "Wähle zwei Replays in der Bibliothek, um echte Messwerte zu vergleichen.", "Sélectionnez deux replays dans la bibliothèque pour comparer les mesures réelles.")}</div>
          ) : (() => {
            const left = compared[0];
            const right = compared[1];
            const sameMap = (left.mapName || "").trim().toLowerCase() === (right.mapName || "").trim().toLowerCase();
            const sameVehicle = (left.vehicleName || "").trim().toLowerCase() === (right.vehicleName || "").trim().toLowerCase();
            return (
              <div className="advanced-grid">
                <article className="card compact-card">
                  <span className="eyebrow">REPLAY A</span>
                  <h3>{left.name}</h3>
                  <p>{left.mapName || "—"} · {left.vehicleName || "—"}</p>
                  <div className="details-grid">
                    <div><small>{pick("DURAÇÃO", "DURATION", "DURACIÓN", "DAUER", "DURÉE")}</small><strong>{formatReplayDuration(left.durationSeconds)}</strong></div>
                    <div><small>{pick("DISTÂNCIA", "DISTANCE", "DISTANCIA", "DISTANZ", "DISTANCE")}</small><strong>{left.estimatedDistanceKm.toFixed(2)} km</strong></div>
                    <div><small>{pick("MÉDIA", "AVERAGE", "MEDIA", "DURCHSCHNITT", "MOYENNE")}</small><strong>{left.averageSpeedKph.toFixed(1)} km/h</strong></div>
                    <div><small>{pick("MÁXIMA", "MAXIMUM", "MÁXIMA", "MAXIMUM", "MAXIMUM")}</small><strong>{left.maximumSpeedKph.toFixed(1)} km/h</strong></div>
                  </div>
                </article>
                <article className="card compact-card">
                  <span className="eyebrow">REPLAY B</span>
                  <h3>{right.name}</h3>
                  <p>{right.mapName || "—"} · {right.vehicleName || "—"}</p>
                  <div className="details-grid">
                    <div><small>{pick("DURAÇÃO", "DURATION", "DURACIÓN", "DAUER", "DURÉE")}</small><strong>{formatReplayDuration(right.durationSeconds)}</strong></div>
                    <div><small>{pick("DISTÂNCIA", "DISTANCE", "DISTANCIA", "DISTANZ", "DISTANCE")}</small><strong>{right.estimatedDistanceKm.toFixed(2)} km</strong></div>
                    <div><small>{pick("MÉDIA", "AVERAGE", "MEDIA", "DURCHSCHNITT", "MOYENNE")}</small><strong>{right.averageSpeedKph.toFixed(1)} km/h</strong></div>
                    <div><small>{pick("MÁXIMA", "MAXIMUM", "MÁXIMA", "MAXIMUM", "MAXIMUM")}</small><strong>{right.maximumSpeedKph.toFixed(1)} km/h</strong></div>
                  </div>
                </article>
                <article className="card compact-card">
                  <span className="eyebrow">B − A</span>
                  <h3>{sameMap && sameVehicle ? pick("Comparação direta", "Direct comparison", "Comparación directa", "Direkter Vergleich", "Comparaison directe") : sameMap ? pick("Mesmo mapa, veículo diferente", "Same map, different vehicle", "Mismo mapa, vehículo diferente", "Gleiche Karte, anderes Fahrzeug", "Même carte, véhicule différent") : pick("Mapas diferentes", "Different maps", "Mapas diferentes", "Unterschiedliche Karten", "Cartes différentes")}</h3>
                  <div className="details-grid">
                    <div><small>{pick("DURAÇÃO", "DURATION", "DURACIÓN", "DAUER", "DURÉE")}</small><strong>{formatReplayDuration(Math.abs(right.durationSeconds - left.durationSeconds))} {right.durationSeconds >= left.durationSeconds ? "+" : "−"}</strong></div>
                    <div><small>{pick("DISTÂNCIA", "DISTANCE", "DISTANCIA", "DISTANZ", "DISTANCE")}</small><strong>{(right.estimatedDistanceKm - left.estimatedDistanceKm).toFixed(2)} km</strong></div>
                    <div><small>{pick("MÉDIA", "AVERAGE", "MEDIA", "DURCHSCHNITT", "MOYENNE")}</small><strong>{(right.averageSpeedKph - left.averageSpeedKph).toFixed(1)} km/h</strong></div>
                    <div><small>{pick("MÁXIMA", "MAXIMUM", "MÁXIMA", "MAXIMUM", "MAXIMUM")}</small><strong>{(right.maximumSpeedKph - left.maximumSpeedKph).toFixed(1)} km/h</strong></div>
                  </div>
                  <p>{sameMap ? (sameVehicle ? pick("Mesmo mapa e mesmo veículo identificados.", "Same map and same vehicle identified.", "Mismo mapa y mismo vehículo identificados.", "Gleiche Karte und gleiches Fahrzeug erkannt.", "Même carte et même véhicule identifiés.") : pick("Mesmo mapa, mas veículos diferentes; interprete tempos e velocidades considerando o veículo.", "Same map, but different vehicles; interpret times and speeds with the vehicle difference in mind.", "Mismo mapa, pero vehículos diferentes; interpreta tiempos y velocidades considerando el vehículo.", "Gleiche Karte, aber unterschiedliche Fahrzeuge; Zeiten und Geschwindigkeiten entsprechend bewerten.", "Même carte, mais véhicules différents ; interprétez temps et vitesses en tenant compte du véhicule.")) : pick("Os replays foram gravados em mapas diferentes; as métricas são reais, mas não representam viagens equivalentes.", "The replays were recorded on different maps; metrics are real but do not represent equivalent trips.", "Los replays se grabaron en mapas diferentes; las métricas son reales, pero no representan viajes equivalentes.", "Die Replays wurden auf unterschiedlichen Karten aufgenommen; die Messwerte sind real, aber die Fahrten nicht gleichwertig.", "Les replays ont été enregistrés sur des cartes différentes ; les mesures sont réelles mais les trajets ne sont pas équivalents.")}</p>
                </article>
              </div>
            );
          })()}
        </article>

        <article className="card ghost-route-card">
          <div className="section-heading">            <div><span className="eyebrow">{t("ghost.preview")}</span><h3>{t("ghost.recordedRoute")}</h3></div>
            <span className="route-source-pill">READ-ONLY</span>
          </div>
          <GhostRoutePreview points={selected?.routePoints || []} />
          <p className="migration-note">{t("ghost.previewNote")}</p>
        </article>
      </section>
    </>
  );
}

function Help({ state }: { state: NavBrState | null }) {
  const { pick } = useI18n();
  const multiplayer = state?.multiplayer ?? fallbackMultiplayer;
  const sections = [
    {
      title: pick("1. Comece aqui", "1. Start here", "1. Primeros pasos", "1. Erste Schritte", "1. Bien démarrer"),
      body: pick(
        "Abra o NavBR e o OMSI 2, aguarde a detecção e a telemetria, carregue mapa e ônibus e inicie a viagem. A Home mostra a prontidão do Plugin OMSI 2, Empresa/Crachá, Mobile Companion e Atualizações antes de você entrar nas áreas avançadas.",
        "Open NavBR and OMSI 2, wait for detection and telemetry, load a map and bus, and start the trip. Home shows readiness for the OMSI 2 Plugin, Company/Badge, Mobile Companion and Updates before you enter advanced areas.",
        "Abre NavBR y OMSI 2, espera detección y telemetría, carga mapa y autobús e inicia el viaje. Inicio muestra el estado de Plugin OMSI 2, Empresa/Credencial, Mobile Companion y Actualizaciones.",
        "Starte NavBR und OMSI 2, warte auf Erkennung und Telemetrie, lade Karte und Bus und beginne die Fahrt. Home zeigt den Status von OMSI-2-Plugin, Unternehmen/Ausweis, Mobile Companion und Updates.",
        "Ouvrez NavBR et OMSI 2, attendez la détection et la télémétrie, chargez la carte et le bus puis démarrez. L’accueil affiche l’état du plugin OMSI 2, Entreprise/Badge, Mobile Companion et Mises à jour."
      )
    },
    {
      title: pick("2. Empresa e Crachá", "2. Company and Badge", "2. Empresa y Credencial", "2. Unternehmen und Ausweis", "2. Entreprise et Badge"),
      body: pick(
        "Quando o crachá está verificado, ele é a fonte de verdade da identidade operacional: nome do motorista, empresa e sigla. Campos conflitantes ficam bloqueados para evitar que a sessão use uma identidade diferente da validada. Faça a validação antes de usar recursos de Empresa/CCO.",
        "When the badge is verified, it becomes the source of truth for operational identity: driver name, company and acronym. Conflicting fields are locked so the session cannot use an identity different from the validated one. Validate it before using Company/CCO features.",
        "Cuando la credencial está verificada, pasa a ser la fuente de verdad de nombre del conductor, empresa y sigla. Los campos en conflicto quedan bloqueados antes de usar Empresa/CCO.",
        "Ist der Ausweis verifiziert, ist er die Quelle für Fahrername, Unternehmen und Kürzel. Abweichende Felder werden gesperrt, bevor Unternehmens-/CCO-Funktionen genutzt werden.",
        "Quand le badge est vérifié, il devient la source de référence pour le nom du conducteur, l’entreprise et le sigle. Les champs en conflit sont verrouillés avant l’utilisation des fonctions Entreprise/CCO."
      )
    },
    {
      title: pick("3. HUD, GPS e minimapa", "3. HUD, GPS and minimap", "3. HUD, GPS y minimapa", "3. HUD, GPS und Minikarte", "3. HUD, GPS et minicarte"),
      body: pick(
        "Velocidade, linha, rota, destino, próxima parada e navegação vêm do estado real do OMSI. Em Configurações > HUD ajuste preset, tema, escala, opacidade e módulos. O minimapa pode ser Retangular ou Circular · GTA; o formato também é aplicado ao overlay nativo e aos HUDs compostos.",
        "Speed, line, route, destination, next stop and navigation come from real OMSI state. Under Settings > HUD adjust preset, theme, scale, opacity and modules. The minimap can be Rectangular or Circular · GTA, and the shape also applies to the native overlay and composed HUDs.",
        "Velocidad, línea, ruta, destino, próxima parada y navegación vienen del estado real de OMSI. En Configuración > HUD puedes usar minimapa Rectangular o Circular · GTA.",
        "Geschwindigkeit, Linie, Route, Ziel, nächste Haltestelle und Navigation stammen aus dem echten OMSI-Status. Unter Einstellungen > HUD kann die Minikarte Rechteckig oder Rund · GTA sein.",
        "Vitesse, ligne, itinéraire, destination, prochain arrêt et navigation viennent de l’état réel d’OMSI. Dans Paramètres > HUD, la minicarte peut être Rectangulaire ou Circulaire · GTA."
      )
    },
    {
      title: pick("4. Multiplayer", "4. Multiplayer", "4. Multijugador", "4. Multiplayer", "4. Multijoueur"),
      body: pick(
        "Crie ou entre em uma sala NavBR normalmente. O movimento físico usa openOMSI v6 automaticamente; o SignalR fica apenas para CCO/empresa/serviços. Em LAN usa UDP direto e, para Internet/NAT, pode usar WebSocket/túnel. Ônibus remotos físicos são parte padrão da sessão.",
        "Create or join a NavBR room normally. Physical movement uses openOMSI v6 automatically; SignalR is only for CCO/company/services. LAN uses direct UDP and Internet/NAT can fall back to WebSocket/tunnel. Physical remote buses are a standard session feature.",
        "Crea o entra en una sala NavBR normalmente. El movimiento físico usa openOMSI v6 automáticamente; SignalR queda solo para CCO/empresa/servicios.",
        "Erstelle oder betrete einen NavBR-Raum normal. Physische Bewegung läuft automatisch über openOMSI v6; SignalR bleibt nur für CCO/Unternehmen/Dienste.",
        "Créez ou rejoignez normalement une salle NavBR. Le mouvement physique utilise automatiquement openOMSI v6 ; SignalR reste réservé au CCO/entreprise/services."
      )
    },
    {
      title: pick("5. Chat e voz", "5. Chat and voice", "5. Chat y voz", "5. Chat und Sprache", "5. Chat et voix"),
      body: pick(
        `O atalho atual do chat é ${multiplayer.chatHotkey || "F9"} e o PTT é ${multiplayer.voiceHotkey || "F10"}. Em Multiplayer > Chat & Voz configure canal, proximidade, microfone, saída, deafen e mute/volume por jogador. Sliders mantêm edição local e enviam a alteração ao soltar, toque ou tecla para evitar polling excessivo.`,
        `The current chat hotkey is ${multiplayer.chatHotkey || "F9"} and PTT is ${multiplayer.voiceHotkey || "F10"}. Under Multiplayer > Chat & Voice configure channel, proximity, microphone, output, deafen and per-player mute/volume. Sliders keep a local draft and commit on release, tap or keyboard input to avoid excessive polling.`,
        `El atajo del chat es ${multiplayer.chatHotkey || "F9"} y PTT es ${multiplayer.voiceHotkey || "F10"}. En Multiplayer > Chat y Voz configura canal, proximidad, dispositivos y mixer.`,
        `Der Chat-Hotkey ist ${multiplayer.chatHotkey || "F9"}, PTT ist ${multiplayer.voiceHotkey || "F10"}. Unter Multiplayer > Chat & Sprache werden Kanal, Nähe, Geräte und Mixer eingestellt.`,
        `Le raccourci chat est ${multiplayer.chatHotkey || "F9"} et le PTT ${multiplayer.voiceHotkey || "F10"}. Dans Multijoueur > Chat & Voix, configurez canal, proximité, périphériques et mixage.`
      )
    },
    {
      title: pick("6. Atualizações automáticas", "6. Automatic updates", "6. Actualizaciones automáticas", "6. Automatische Updates", "6. Mises à jour automatiques"),
      body: pick(
        "O NavBR verifica releases oficiais ao abrir. Quando encontra uma versão mais nova, baixa o instalador, valida SHA256SUMS.txt e confere o SHA-256 novamente antes da instalação. Em Configurações > Atualizações acompanhe versão, progresso, notas da release e use Atualizar e reiniciar quando o pacote estiver validado.",
        "NavBR checks official releases at startup. When a newer version is found, it downloads the installer, validates SHA256SUMS.txt and checks SHA-256 again before installation. Under Settings > Updates monitor version, progress and release notes, then use Update & restart after validation.",
        "NavBR comprueba releases oficiales al iniciar, descarga el instalador y valida SHA-256 antes de permitir Actualizar y reiniciar.",
        "NavBR prüft beim Start offizielle Releases, lädt den Installer und validiert SHA-256, bevor Aktualisieren & neu starten freigegeben wird.",
        "NavBR vérifie les releases officielles au démarrage, télécharge l’installeur et valide SHA-256 avant d’autoriser Mettre à jour et redémarrer."
      )
    },
    {
      title: pick("7. openOMSI é separado do App", "7. openOMSI is separate from the App", "7. openOMSI está separado de la App", "7. openOMSI ist von der App getrennt", "7. openOMSI est séparé de l’app"),
      body: pick(
        "O NavBR App não instala, remove, atualiza nem inicia openOMSI e não embute o plugin openOMSI no desktop. O plugin NavBR para openOMSI é distribuído separadamente pelo ambiente/launcher do openOMSI. O App mantém somente o gateway local e a detecção de ambiente/conteúdo em modo leitura.",
        "NavBR App does not install, remove, update or launch openOMSI and does not embed the openOMSI plugin in the desktop package. The NavBR openOMSI plugin is distributed separately by the openOMSI environment/launcher. The App only keeps the local gateway and read-only environment/content discovery.",
        "NavBR App no instala, elimina, actualiza ni inicia openOMSI. El plugin se distribuye por separado y la App mantiene solo el gateway local y detección de solo lectura.",
        "Die NavBR-App installiert, entfernt, aktualisiert oder startet openOMSI nicht. Das Plugin wird separat verteilt; die App stellt nur das lokale Gateway und eine schreibgeschützte Umgebungserkennung bereit.",
        "L’app NavBR n’installe, ne supprime, ne met à jour ni ne lance openOMSI. Le plugin est distribué séparément ; l’app conserve seulement la passerelle locale et la détection en lecture seule."
      )
    },
    {
      title: pick("8. Mobile Companion", "8. Mobile Companion", "8. Mobile Companion", "8. Mobile Companion", "8. Mobile Companion"),
      body: pick(
        "O Mobile Companion usa descoberta LAN e código de pareamento, recebe o roadmap real do OMSI atrás da rota/paradas/ônibus e usa bounds reais para alinhamento. Ao voltar de suspensão, bloqueio ou perda de rede ele reconecta e atualiza; o PTT é liberado ao suspender para não deixar o microfone preso.",
        "Mobile Companion uses LAN discovery and a pairing code, receives the real OMSI roadmap behind route/stops/bus layers and aligns using real bounds. After suspension, lock or network loss it reconnects and refreshes; PTT is released on suspend so the microphone cannot remain stuck.",
        "Mobile Companion usa descubrimiento LAN, código de emparejamiento y roadmap real de OMSI. Al volver de suspensión o red se reconecta y libera PTT al suspender.",
        "Mobile Companion nutzt LAN-Erkennung, Pairing-Code und die echte OMSI-Roadmap. Nach Suspend oder Netzverlust verbindet es sich neu; PTT wird beim Suspend freigegeben.",
        "Mobile Companion utilise la découverte LAN, un code d’appairage et la vraie roadmap OMSI. Après suspension ou perte réseau il se reconnecte ; le PTT est relâché à la suspension."
      )
    },
    {
      title: pick("9. Personagem / RP", "9. Character / RP", "9. Personaje / RP", "9. Charakter / RP", "9. Personnage / RP"),
      body: pick(
        "No RP, W/A/S/D movem, setas giram, Shift corre, Espaço pula, E entra no ônibus quando próximo e Esc faz retorno de emergência. A sessão RP termina automaticamente se o OMSI sair do mapa/sessão ou se o Plugin Bridge desconectar, evitando estado preso.",
        "In RP, W/A/S/D move, arrows rotate, Shift runs, Space jumps, E enters the bus when nearby and Esc performs an emergency return. RP ends automatically if OMSI leaves the map/session or Plugin Bridge disconnects, preventing a stuck state.",
        "En RP: W/A/S/D, flechas, Shift, Espacio, E y Esc controlan al personaje. La sesión termina si OMSI sale del mapa/sesión o si se desconecta Plugin Bridge.",
        "Im RP steuern W/A/S/D, Pfeile, Shift, Leertaste, E und Esc den Charakter. Die Sitzung endet automatisch, wenn OMSI Karte/Sitzung verlässt oder Plugin Bridge getrennt wird.",
        "En RP, W/A/S/D, flèches, Shift, Espace, E et Échap contrôlent le personnage. La session s’arrête si OMSI quitte la carte/session ou si Plugin Bridge se déconnecte."
      )
    },
    {
      title: pick("10. Hardware Cockpit e desempenho", "10. Hardware Cockpit and performance", "10. Hardware Cockpit y rendimiento", "10. Hardware Cockpit und Leistung", "10. Hardware Cockpit et performances"),
      body: pick(
        "O Hardware Cockpit nunca troca silenciosamente de porta COM. A tela mostra se a COM configurada está disponível, reconexão pendente, tentativa atual e próxima tentativa. O governor continua oferecendo Auto, Stability, Multiplayer, Quality e Diagnostics e ajusta telemetria/HUD conforme custo de leitura e pressão do plugin.",
        "Hardware Cockpit never silently switches COM ports. The screen shows whether the configured COM port is available, pending reconnect, current attempt and next retry. The governor keeps Auto, Stability, Multiplayer, Quality and Diagnostics profiles and adapts telemetry/HUD to read cost and plugin pressure.",
        "Hardware Cockpit no cambia silenciosamente de COM. Muestra disponibilidad, reconexión pendiente, intento actual y próximo reintento; el governor mantiene sus perfiles de rendimiento.",
        "Hardware Cockpit wechselt den COM-Port nie unbemerkt. Es zeigt Verfügbarkeit, ausstehende Wiederverbindung, aktuellen Versuch und nächsten Retry; der Governor behält seine Leistungsprofile.",
        "Hardware Cockpit ne change jamais silencieusement de port COM. Il affiche disponibilité, reconnexion en attente, tentative actuelle et prochain essai ; le governor conserve ses profils de performance."
      )
    },
    {
      title: pick("11. Diagnóstico e pacote de suporte", "11. Diagnostics and support bundle", "11. Diagnóstico y paquete de soporte", "11. Diagnose und Support-Paket", "11. Diagnostic et paquet de support"),
      body: pick(
        "Em Configurações > Diagnóstico use Gerar pacote de diagnóstico para criar um ZIP sanitizado com summary JSON e log sanitizado. Senhas, tokens, IDs de sala/jogador, IPs, e-mails e caminhos locais são mascarados/removidos; o log bruto não entra. A tela também mostra poll efetivo, última/média de leitura, refresh do HUD, pressão do plugin e perfil ativo.",
        "Under Settings > Diagnostics use Generate diagnostic bundle to create a sanitized ZIP with summary JSON and sanitized log. Passwords, tokens, room/player IDs, IPs, email addresses and local paths are masked/removed; raw logs are not included. The page also shows effective poll, last/average read time, HUD refresh, plugin pressure and active profile.",
        "En Configuración > Diagnóstico genera un ZIP sanitizado; elimina o enmascara contraseñas, tokens, IDs, IP, e-mail y rutas locales, y no incluye el log bruto.",
        "Unter Einstellungen > Diagnose wird ein bereinigtes ZIP erzeugt; Passwörter, Tokens, IDs, IPs, E-Mails und lokale Pfade werden entfernt/maskiert, Rohlogs werden nicht aufgenommen.",
        "Dans Paramètres > Diagnostic, créez un ZIP assaini ; mots de passe, jetons, identifiants, IP, e-mails et chemins locaux sont masqués/supprimés et les logs bruts ne sont pas inclus."
      )
    },
    {
      title: pick("12. Se algo não funcionar", "12. Troubleshooting", "12. Si algo no funciona", "12. Wenn etwas nicht funktioniert", "12. Si quelque chose ne fonctionne pas"),
      body: pick(
        "OMSI não detectado: confira a instalação e se Omsi.exe está aberto. HUD sem dados: entre no gameplay. Mapa/rota ausente: confira roadmap e viagem ativa. Mobile sem mapa: verifique o serviço e refaça o pareamento. Hardware sem COM: confirme a porta configurada e acompanhe o auto-reconnect. Multiplayer sem conexão: confira servidor, sala, senha e Rede.",
        "OMSI not detected: check the installation and that Omsi.exe is running. HUD without data: enter gameplay. Missing map/route: check roadmap and active trip. Mobile without a map: verify the service and pair again. Hardware without COM: confirm the configured port and watch auto-reconnect. Multiplayer connection: verify server, room, password and Network.",
        "OMSI no detectado: revisa instalación y Omsi.exe. Sin HUD: entra al juego. Sin mapa/ruta: revisa roadmap. Mobile: servicio y emparejamiento. Hardware: COM y auto-reconnect. Multiplayer: servidor, sala, contraseña y Red.",
        "OMSI nicht erkannt: Installation und Omsi.exe prüfen. HUD ohne Daten: Gameplay starten. Karte/Route: Roadmap prüfen. Mobile: Dienst und Pairing. Hardware: COM und Auto-Reconnect. Multiplayer: Server, Raum, Passwort und Netzwerk.",
        "OMSI non détecté : vérifiez l’installation et Omsi.exe. HUD sans données : entrez en jeu. Carte/route : vérifiez la roadmap. Mobile : service et appairage. Hardware : COM et reconnexion. Multijoueur : serveur, salle, mot de passe et Réseau."
      )
    },
    {
      title: pick("13. Teste da comunidade", "13. Community testing", "13. Prueba comunitaria", "13. Community-Test", "13. Test communautaire"),
      body: pick(
        "Ao relatar um erro, informe o que estava fazendo, mapa e ônibus e, quando possível, anexe o pacote sanitizado de Configurações > Diagnóstico. Prints ajudam a reproduzir problemas de UI. Para multiplayer físico, o teste real entre dois PCs continua reservado para a etapa final desta linha.",
        "When reporting a problem, include what you were doing, map and bus and, when possible, attach the sanitized bundle from Settings > Diagnostics. Screenshots help reproduce UI problems. Real two-PC physical multiplayer testing remains reserved for the final stage of this line.",
        "Al reportar un problema, indica qué hacías, mapa y autobús y adjunta el paquete sanitizado cuando sea posible. El test físico real entre dos PCs queda para la etapa final.",
        "Bei Fehlerberichten bitte Aktion, Karte und Bus angeben und möglichst das bereinigte Diagnosepaket anhängen. Der reale physische Multiplayer-Test mit zwei PCs bleibt für die letzte Phase.",
        "Pour signaler un problème, indiquez l’action, la carte et le bus et joignez si possible le paquet de diagnostic assaini. Le test physique réel entre deux PC reste réservé à la phase finale."
      )
    },
  ];

  return (
    <>
      <header className="topbar">
        <div>
          <span className="eyebrow">{pick("AJUDA", "HELP", "AYUDA", "HILFE", "AIDE")}</span>
          <h1>{pick("Manual do NavBR", "NavBR manual", "Manual de NavBR", "NavBR-Handbuch", "Manuel NavBR")}</h1>
          <p>{pick("Guia de uso e canais de feedback, agora dentro da interface React.", "Usage guide and feedback channels, now inside the React interface.", "Guía de uso y canales de feedback dentro de la interfaz React.", "Benutzerhilfe und Feedback jetzt direkt in der React-Oberfläche.", "Guide d’utilisation et retours directement dans l’interface React.")}</p>
        </div>
      </header>

      {state?.system.legacyPreferences.firstRunCompleted === false && (
        <section className="card cco-panel">
          <div className="section-heading">
            <div>
              <span className="eyebrow">{pick("PRIMEIRO ACESSO", "FIRST RUN", "PRIMER ACCESO", "ERSTER START", "PREMIER DÉMARRAGE")}</span>
              <h3>{pick("Bem-vindo ao OMSI NavBR Multiplayer", "Welcome to OMSI NavBR Multiplayer", "Bienvenido a OMSI NavBR Multiplayer", "Willkommen bei OMSI NavBR Multiplayer", "Bienvenue dans OMSI NavBR Multiplayer")}</h3>
            </div>
          </div>
          <p>{pick(
            "Escolha o idioma na barra lateral e confira a prontidão do ambiente. OMSI e Plugin Bridge são os itens essenciais; Mobile, Hardware e openOMSI são opcionais e podem ser configurados depois.",
            "Choose your language from the sidebar and review environment readiness. OMSI and Plugin Bridge are the essential items; Mobile, Hardware and openOMSI are optional and can be configured later.",
            "Elige el idioma y revisa la preparación del entorno. OMSI y Plugin Bridge son esenciales; Mobile, Hardware y openOMSI son opcionales.",
            "Wähle die Sprache und prüfe die Bereitschaft. OMSI und Plugin Bridge sind erforderlich; Mobile, Hardware und openOMSI sind optional.",
            "Choisissez la langue et vérifiez l’état de préparation. OMSI et Plugin Bridge sont essentiels ; Mobile, Hardware et openOMSI sont facultatifs."
          )}</p>

          <div className="first-run-readiness">
            <article className={`first-run-check ${state?.omsi.running ? "ready" : "pending"}`}>
              <NavBrIcon name={state?.omsi.running ? "info" : "bus"} size={16} />
              <div>
                <strong>OMSI 2</strong>
                <small>{state?.omsi.running
                  ? pick("Detectado e em execução", "Detected and running", "Detectado y en ejecución", "Erkannt und läuft", "Détecté et en cours")
                  : pick("Abra o OMSI 2 para validar telemetria", "Open OMSI 2 to validate telemetry", "Abre OMSI 2 para validar telemetría", "OMSI 2 öffnen, um Telemetrie zu prüfen", "Ouvrez OMSI 2 pour valider la télémétrie")}</small>
              </div>
            </article>

            <article className={`first-run-check ${state?.system.pluginInstallation?.state === "installed" || state?.system.pluginInstallation?.state === "ready" ? "ready" : "pending"}`}>
              <NavBrIcon name="plugin" size={16} />
              <div>
                <strong>Plugin Bridge OMSI 2</strong>
                <small>{state?.system.pluginInstallation?.state === "installed" || state?.system.pluginInstallation?.state === "ready"
                  ? pick("Instalado e compatível", "Installed and compatible", "Instalado y compatible", "Installiert und kompatibel", "Installé et compatible")
                  : pick("Configure em Configurações > Instalações", "Configure under Settings > Installations", "Configura en Ajustes > Instalaciones", "Unter Einstellungen > Installationen konfigurieren", "Configurez dans Paramètres > Installations")}</small>
              </div>
            </article>

            <article className={`first-run-check ${state?.system.applicationUpdate?.updateAvailable ? "attention" : "ready"}`}>
              <NavBrIcon name="refresh" size={16} />
              <div>
                <strong>{pick("Atualizações", "Updates", "Actualizaciones", "Updates", "Mises à jour")}</strong>
                <small>{state?.system.applicationUpdate?.readyToInstall
                  ? pick("Nova versão pronta para instalar", "New version ready to install", "Nueva versión lista", "Neue Version installationsbereit", "Nouvelle version prête")
                  : state?.system.applicationUpdate?.updateAvailable
                    ? pick("Baixando ou validando nova versão", "Downloading or verifying a new version", "Descargando o validando", "Neue Version wird geladen/geprüft", "Téléchargement ou validation en cours")
                    : pick("Nenhuma ação necessária", "No action required", "No se requiere acción", "Keine Aktion erforderlich", "Aucune action requise")}</small>
              </div>
            </article>

            <article className={`first-run-check optional ${state?.system.mobileCompanion?.running ? "ready" : "pending"}`}>
              <NavBrIcon name="network" size={16} />
              <div>
                <strong>Mobile Companion · {pick("Opcional", "Optional", "Opcional", "Optional", "Facultatif")}</strong>
                <small>{state?.system.mobileCompanion?.running
                  ? pick("Serviço disponível na rede local", "Service available on the local network", "Servicio disponible en la red local", "Dienst im lokalen Netzwerk verfügbar", "Service disponible sur le réseau local")
                  : pick("Pode ser ativado depois", "Can be enabled later", "Puede activarse después", "Kann später aktiviert werden", "Peut être activé plus tard")}</small>
              </div>
            </article>

            <article className="first-run-check optional ready">
              <NavBrIcon name="network" size={16} />
              <div>
                <strong>openOMSI · {pick("Externo", "External", "Externo", "Extern", "Externe")}</strong>
                <small>{pick(
                  "Plugin separado; instalação pertence ao launcher/ambiente openOMSI",
                  "Separate plugin; installation belongs to the openOMSI launcher/environment",
                  "Plugin separado; la instalación pertenece al launcher/entorno openOMSI",
                  "Separates Plugin; Installation erfolgt über openOMSI-Launcher/Umgebung",
                  "Plugin séparé ; installation gérée par le launcher/environnement openOMSI"
                )}</small>
              </div>
            </article>
          </div>

          <div className="room-actions">
            <button className="button primary" onClick={() => sendCommand("completeFirstRun")}>
              {pick("Começar a usar o NavBR", "Start using NavBR", "Empezar a usar NavBR", "NavBR verwenden", "Commencer à utiliser NavBR")}
            </button>
          </div>
        </section>
      )}

      <section className="company-layout">
        {sections.map(section => (
          <article className="card company-card" key={section.title}>
            <div className="section-heading"><div><h3>{section.title}</h3></div></div>
            <p>{section.body}</p>
          </article>
        ))}
      </section>

      <section className="card cco-panel">
        <div className="section-heading">
          <div>
            <span className="eyebrow">FEEDBACK</span>
            <h3>{pick("Ajude a melhorar o NavBR", "Help improve NavBR", "Ayuda a mejorar NavBR", "Hilf mit, NavBR zu verbessern", "Aidez à améliorer NavBR")}</h3>
          </div>
        </div>
        <p>{pick("Os formulários são Issues públicas do GitHub. Não envie senhas, tokens ou dados privados; prints e logs técnicos ajudam em bugs.", "Forms are public GitHub Issues. Do not send passwords, tokens or private data; screenshots and technical logs help with bugs.", "Los formularios son Issues públicas de GitHub. No envíes contraseñas, tokens ni datos privados.", "Die Formulare sind öffentliche GitHub-Issues. Keine Passwörter, Tokens oder privaten Daten senden.", "Les formulaires sont des Issues GitHub publiques. N’envoyez pas de mots de passe, jetons ou données privées.")}</p>
        <div className="advanced-grid">
          <article className="card compact-card">
            <span className="eyebrow">BUG</span>
            <h3>{pick("Relatar um bug", "Report a bug", "Reportar un error", "Fehler melden", "Signaler un bug")}</h3>
            <p>{pick("HUD, mapa, rota, chat, voz, multiplayer, instalação ou interface.", "HUD, map, route, chat, voice, multiplayer, installation or interface.", "HUD, mapa, ruta, chat, voz, multijugador, instalación o interfaz.", "HUD, Karte, Route, Chat, Sprache, Multiplayer, Installation oder Oberfläche.", "HUD, carte, itinéraire, chat, voix, multijoueur, installation ou interface.")}</p>
            <button className="button primary" onClick={() => sendCommand("openFeedback", { kind: "bug" })}>{pick("Relatar problema", "Report problem", "Reportar problema", "Problem melden", "Signaler le problème")}</button>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">{pick("SUGESTÃO", "SUGGESTION", "SUGERENCIA", "VORSCHLAG", "SUGGESTION")}</span>
            <h3>{pick("Sugerir uma melhoria", "Suggest an improvement", "Sugerir una mejora", "Verbesserung vorschlagen", "Suggérer une amélioration")}</h3>
            <p>{pick("Ideias para qualquer área do NavBR.", "Ideas for any part of NavBR.", "Ideas para cualquier área de NavBR.", "Ideen für jeden Bereich von NavBR.", "Idées pour toute partie de NavBR.")}</p>
            <button className="button ghost" onClick={() => sendCommand("openFeedback", { kind: "suggestion" })}>{pick("Enviar sugestão", "Send suggestion", "Enviar sugerencia", "Vorschlag senden", "Envoyer une suggestion")}</button>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">{pick("GERAL", "GENERAL", "GENERAL", "ALLGEMEIN", "GÉNÉRAL")}</span>
            <h3>{pick("Feedback geral", "General feedback", "Feedback general", "Allgemeines Feedback", "Retour général")}</h3>
            <p>{pick("Conte o que funciona bem e o que deveria receber prioridade.", "Tell us what works well and what should be prioritized.", "Cuéntanos qué funciona bien y qué debería tener prioridad.", "Sag uns, was gut funktioniert und was Priorität haben sollte.", "Dites-nous ce qui fonctionne bien et ce qui devrait être prioritaire.")}</p>
            <button className="button ghost" onClick={() => sendCommand("openFeedback", { kind: "general" })}>{pick("Avaliar o projeto", "Review the project", "Evaluar el proyecto", "Projekt bewerten", "Évaluer le projet")}</button>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">GITHUB</span>
            <h3>{pick("Feedback público", "Public feedback", "Feedback público", "Öffentliches Feedback", "Retour public")}</h3>
            <p>{pick("Veja relatos existentes e acompanhe o andamento.", "View existing reports and follow progress.", "Consulta reportes existentes y su progreso.", "Vorhandene Meldungen und Fortschritt ansehen.", "Consultez les signalements existants et leur suivi.")}</p>
            <button className="button ghost" onClick={() => sendCommand("openFeedback", { kind: "issues" })}>{pick("Ver feedback", "View feedback", "Ver feedback", "Feedback ansehen", "Voir les retours")}</button>
          </article>
        </div>
      </section>
    </>
  );
}


function ApplicationUpdatePrompt({
  state,
  dismissed,
  onDismiss
}: {
  state: NavBrState | null;
  dismissed: boolean;
  onDismiss: () => void;
}) {
  const { pick } = useI18n();
  const update = state?.system?.applicationUpdate;

  if (!update || dismissed || !update.updateAvailable || update.status === "failed") {
    return null;
  }

  const busy = update.status === "downloading" || update.status === "verifying";
  const ready = update.status === "ready" && update.readyToInstall;
  const available = update.status === "available" && update.updateAvailable;
  const installing = update.status === "installing";
  const progress = Math.max(0, Math.min(100, update.progressPercent ?? 0));

  return (
    <section className={`card cco-panel application-update-prompt ${ready ? "ready" : ""}`}>
      <div className="section-heading">
        <div>
          <span className="eyebrow">
            {pick("ATUALIZAÇÃO DO NAVBR", "NAVBR UPDATE", "ACTUALIZACIÓN NAVBR", "NAVBR-UPDATE", "MISE À JOUR NAVBR")}
          </span>
          <h3>
            {ready
              ? pick("Atualização pronta para instalar", "Update ready to install", "Actualización lista para instalar", "Update ist installationsbereit", "Mise à jour prête à installer")
              : installing
                ? pick("Preparando atualização", "Preparing update", "Preparando actualización", "Update wird vorbereitet", "Préparation de la mise à jour")
                : pick("Nova versão encontrada", "New version found", "Nueva versión encontrada", "Neue Version gefunden", "Nouvelle version trouvée")}
          </h3>
        </div>
        <span className="update-version-pill">
          {update.currentVersion} → {update.availableVersion || "—"}
        </span>
      </div>

      <p>
        {update.message || pick(
          "O NavBR encontrou uma versão mais nova e está preparando a atualização oficial.",
          "NavBR found a newer version and is preparing the official update.",
          "NavBR encontró una versión más nueva y está preparando la actualización oficial.",
          "NavBR hat eine neuere Version gefunden und bereitet das offizielle Update vor.",
          "NavBR a trouvé une version plus récente et prépare la mise à jour officielle."
        )}
      </p>

      {busy && (
        <div className="application-update-progress" aria-label={`${progress}%`}>
          <span style={{ width: `${progress}%` }} />
        </div>
      )}

      <div className="room-actions">
        {available && (
          <button className="button primary" onClick={() => sendCommand("downloadApplicationUpdate")}>
            {pick("Baixar atualização", "Download update", "Descargar actualización", "Update herunterladen", "Télécharger la mise à jour")}
          </button>
        )}
        {ready && (
          <button className="button primary" onClick={() => sendCommand("installApplicationUpdate")}>
            {pick("Atualizar e reiniciar", "Update & restart", "Actualizar y reiniciar", "Aktualisieren & neu starten", "Mettre à jour et redémarrer")}
          </button>
        )}
        {update.releaseUrl && (
          <button className="button ghost" onClick={() => window.open(update.releaseUrl || "", "_blank", "noopener,noreferrer")}>
            {pick("Ver release", "View release", "Ver release", "Release anzeigen", "Voir la release")}
          </button>
        )}
        {ready && (
          <button className="button ghost" onClick={onDismiss}>
            {pick("Depois", "Later", "Después", "Später", "Plus tard")}
          </button>
        )}
      </div>
    </section>
  );
}

function PluginStartupPrompt({
  state,
  dismissed,
  onDismiss,
  onOpenInstallations
}: {
  state: NavBrState | null;
  dismissed: boolean;
  onDismiss: () => void;
  onOpenInstallations: () => void;
}) {
  const { pick } = useI18n();
  const plugin = state?.system.pluginInstallation;
  if (!plugin || dismissed || plugin.state === "installed" || plugin.state === "untracked") {
    return null;
  }

  const needsInstall = plugin.state === "missing" || plugin.state === "partial" || plugin.state === "outdated";
  const canInstall = plugin.installAvailable && !plugin.omsiRunning;

  return (
    <section className="card cco-panel plugin-startup-prompt">
      <div className="section-heading">
        <div>
          <span className="eyebrow">{pick("PLUGIN OMSI", "OMSI PLUGIN", "PLUGIN OMSI", "OMSI-PLUGIN", "PLUGIN OMSI")}</span>
          <h3>{plugin.state === "outdated"
            ? pick("Plugin NavBR precisa ser atualizado", "NavBR plugin needs an update", "El plugin NavBR necesita actualizarse", "NavBR-Plugin muss aktualisiert werden", "Le plugin NavBR doit être mis à jour")
            : needsInstall
              ? pick("Plugin NavBR não está instalado corretamente", "NavBR plugin is not installed correctly", "El plugin NavBR no está instalado correctamente", "NavBR-Plugin ist nicht korrekt installiert", "Le plugin NavBR n’est pas correctement installé")
              : pick("Verifique o plugin NavBR", "Check the NavBR plugin", "Verifica el plugin NavBR", "NavBR-Plugin prüfen", "Vérifiez le plugin NavBR")}</h3>
        </div>
      </div>
      <p>
        {pick("Encontrados ", "Found ", "Encontrados ", "Gefunden: ", "Trouvés : ")}
        <strong>{plugin.requiredFilesFound}/{plugin.requiredFilesTotal}</strong>
        {pick(" arquivos necessários. O plugin é necessário para integração física/RP com o OMSI.", " required files. The plugin is required for physical/RP integration with OMSI.", " archivos necesarios. El plugin es necesario para la integración física/RP con OMSI.", " benötigte Dateien. Das Plugin wird für die physische/RP-Integration mit OMSI benötigt.", " fichiers requis. Le plugin est requis pour l’intégration physique/RP avec OMSI.")}
      </p>
      {plugin.omsiRunning && (
        <p className="migration-note">
          {plugin.state === "outdated"
            ? pick(
                "Feche o OMSI uma vez. O NavBR aplicará automaticamente o plugin novo assim que Omsi.exe encerrar; depois abra o OMSI novamente para liberar ônibus físicos e RP.",
                "Close OMSI once. NavBR will automatically apply the new plugin as soon as Omsi.exe exits; then launch OMSI again to enable physical buses and RP.",
                "Cierra OMSI una vez. NavBR aplicará automáticamente el plugin nuevo cuando Omsi.exe termine; después vuelve a abrir OMSI para habilitar autobuses físicos y RP.",
                "OMSI einmal schließen. NavBR installiert das neue Plugin automatisch, sobald Omsi.exe beendet ist; danach OMSI erneut starten, um physische Busse und RP zu aktivieren.",
                "Fermez OMSI une fois. NavBR appliquera automatiquement le nouveau plugin dès l’arrêt de Omsi.exe ; relancez ensuite OMSI pour activer les bus physiques et le RP."
              )
            : pick("Feche o OMSI antes de instalar ou atualizar o plugin.", "Close OMSI before installing or updating the plugin.", "Cierra OMSI antes de instalar o actualizar el plugin.", "OMSI vor Installation oder Aktualisierung schließen.", "Fermez OMSI avant d’installer ou mettre à jour le plugin.")}
        </p>
      )}
      {!plugin.installAvailable && !plugin.omsiRunning && (
        <p className="migration-note">
          {pick("Cadastre uma instalação válida do OMSI 2 em Configurações para habilitar a instalação automática.", "Register a valid OMSI 2 installation in Settings to enable automatic installation.", "Registra una instalación válida de OMSI 2 en Configuración para habilitar la instalación automática.", "Eine gültige OMSI-2-Installation in den Einstellungen registrieren.", "Enregistrez une installation OMSI 2 valide dans Paramètres pour activer l’installation automatique.")}
        </p>
      )}
      <div className="room-actions">
        <button className="button primary" disabled={!canInstall} onClick={() => sendCommand("installOmsiPlugin")}>
          {pick("Instalar / atualizar plugin", "Install / update plugin", "Instalar / actualizar plugin", "Plugin installieren / aktualisieren", "Installer / mettre à jour le plugin")}
        </button>
        <button className="button ghost" onClick={onOpenInstallations}>
          {pick("Ver instalações OMSI", "View OMSI installations", "Ver instalaciones OMSI", "OMSI-Installationen anzeigen", "Voir les installations OMSI")}
        </button>
        <button className="button ghost" onClick={onDismiss}>
          {pick("Agora não", "Not now", "Ahora no", "Jetzt nicht", "Pas maintenant")}
        </button>
      </div>
    </section>
  );
}

export default function App() {
  const [state, setState] = useState<NavBrState | null>(null);
  const [screen, setScreen] = useState<Screen>("home");
  const [commandError, setCommandError] = useState<string | null>(null);
  const commandErrorTimer = useRef<number | null>(null);
  const [settingsTabRequest, setSettingsTabRequest] = useState<SettingsTab | null>(null);
  const [navigationViewRequest, setNavigationViewRequest] = useState<{ id: number; view: "2d" | "3d" } | null>(null);
  const [operationsTabRequest, setOperationsTabRequest] = useState<{ id: number; tab: OperationsTab } | null>(null);
  const [companyNetworkTabRequest, setCompanyNetworkTabRequest] = useState<{ id: number; tab: CompanyNetworkTab } | null>(null);
  const [pluginPromptDismissed, setPluginPromptDismissed] = useState(false);
  const [updatePromptDismissed, setUpdatePromptDismissed] = useState(false);
  const lastNavigationRequestId = useRef<number | null>(null);
  const firstRunRouted = useRef(false);

  const openSettingsTab = (tab: SettingsTab) => {
    setSettingsTabRequest(tab);
    setScreen("settings");
  };

  useEffect(() => subscribeToNavBrState(
    next => {
      setState(next);

      const navigationRequest = next.navigationRequest;
      if (
        !firstRunRouted.current &&
        next.system?.legacyPreferences?.firstRunCompleted === false &&
        !navigationRequest
      ) {
        firstRunRouted.current = true;
        setScreen("help");
      } else if (
        !firstRunRouted.current &&
        next.system?.legacyPreferences?.firstRunCompleted !== false
      ) {
        firstRunRouted.current = true;
      }
      if (navigationRequest && navigationRequest.id !== lastNavigationRequestId.current) {
        lastNavigationRequestId.current = navigationRequest.id;
        const requested = navigationRequest.screen;
        const requestedSettingsTab = requested?.startsWith("settings-")
          ? requested.slice("settings-".length) as SettingsTab
          : null;
        if (requestedSettingsTab && ["general", "updates", "installations", "hud", "roadmap", "diagnostics", "network"].includes(requestedSettingsTab)) {
          setSettingsTabRequest(requestedSettingsTab);
          setScreen("settings");
        } else if (requested === "navigation-3d") {
          setNavigationViewRequest({ id: navigationRequest.id, view: "3d" });
          setScreen("navigation");
        } else if (requested === "operations-company") {
          setOperationsTabRequest({ id: navigationRequest.id, tab: "company" });
          setScreen("operations");
        } else if (requested === "companyNetwork-team") {
          setCompanyNetworkTabRequest({ id: navigationRequest.id, tab: "team" });
          setScreen("companyNetwork");
        } else if (requested && [
          "home",
          "navigation",
          "roleplay",
          "ghost",
          "operations",
          "companyNetwork",
          "hardware",
          "settings",
          "multiplayer",
          "help"
        ].includes(requested)) {
          setScreen(requested as Screen);
        }
      }
    },
    message => {
      setCommandError(message);
      if (commandErrorTimer.current != null) {
        window.clearTimeout(commandErrorTimer.current);
      }
      commandErrorTimer.current = window.setTimeout(() => {
        setCommandError(null);
        commandErrorTimer.current = null;
      }, 8000);
    }
  ), []);

  useEffect(() => () => {
    if (commandErrorTimer.current != null) {
      window.clearTimeout(commandErrorTimer.current);
    }
  }, []);

  return (
    <I18nProvider cultureName={state?.cultureName} languages={state?.supportedLanguages}>
    <div className="app-shell">
      <Sidebar screen={screen} setScreen={setScreen} appVersion={state?.appVersion} />
      <main>
        {!state && (
          <section className="card app-loading-state" aria-live="polite">
            <span className="app-loading-spinner" />
            <div>
              <strong>NavBR</strong>
              <small>Inicializando serviços e sincronizando o estado do OMSI…</small>
            </div>
          </section>
        )}
        <ApplicationUpdatePrompt
          state={state}
          dismissed={updatePromptDismissed}
          onDismiss={() => setUpdatePromptDismissed(true)}
        />
        <PluginStartupPrompt
          state={state}
          dismissed={pluginPromptDismissed}
          onDismiss={() => setPluginPromptDismissed(true)}
          onOpenInstallations={() => openSettingsTab("installations")}
        />
        {screen === "home"
          ? <Home
              state={state}
              onNavigate={setScreen}
              onOpenHud={() => openSettingsTab("hud")}
              onOpenRoadmap={() => openSettingsTab("roadmap")}
            />
          : screen === "navigation"
            ? <Navigation state={state} requestedView={navigationViewRequest} />
            : screen === "roleplay"
              ? <Roleplay state={state} error={commandError} />
              : screen === "ghost"
                ? <GhostReplay state={state} error={commandError} />
              : screen === "operations"
                ? <Operations state={state} error={commandError} onNavigate={setScreen} requestedTab={operationsTabRequest} />
              : screen === "companyNetwork"
                ? <CompanyNetwork state={state} error={commandError} requestedTab={companyNetworkTabRequest} />
                : screen === "hardware"
                ? <Hardware state={state} error={commandError} />
                : screen === "settings"
                  ? <Settings state={state} error={commandError} requestedTab={settingsTabRequest} />
                  : screen === "help"
                    ? <Help state={state} />
                    : <Multiplayer
                        state={state}
                        error={commandError}
                        onOpenNetwork={() => openSettingsTab("network")}
                        onOpenHud={() => openSettingsTab("hud")}
                      />}
      </main>
    </div>
    </I18nProvider>
  );
}
