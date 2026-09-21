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
  const { t, pick, cultureName, languages, setLanguage } = useI18n();
  const navItems: Array<{ screen: Screen; icon: NavBrIconName; label: string }> = [
    { screen: "home", icon: "home", label: t("nav.home") },
    { screen: "navigation", icon: "navigation", label: t("nav.navigation") },
    { screen: "multiplayer", icon: "multiplayer", label: t("nav.multiplayer") },
    { screen: "roleplay", icon: "roleplay", label: t("nav.roleplay") },
    { screen: "ghost", icon: "ghost", label: t("nav.ghost") },
    { screen: "operations", icon: "operations", label: t("nav.operations") },
    { screen: "companyNetwork", icon: "company", label: t("nav.company") },
    { screen: "hardware", icon: "hardware", label: t("nav.hardware") },
    { screen: "settings", icon: "settings", label: t("nav.settings") },
    { screen: "help", icon: "help", label: pick("Ajuda", "Help", "Ayuda", "Hilfe", "Aide") }
  ];

  return (
    <aside className="sidebar">
      <div className="brand">
        <div className="brand-mark">N</div>
        <div><strong>NavBR</strong><span>OMSI Multiplayer</span></div>
      </div>

      <nav className="nav">
        {navItems.map(item => (
          <button
            key={item.screen}
            className={`nav-item ${screen === item.screen ? "active" : ""}`}
            onClick={() => setScreen(item.screen)}
          >
            <span className="nav-icon-frame"><NavBrIcon name={item.icon} size={19} /></span>
            <span>{item.label}</span>
          </button>
        ))}
      </nav>

      <div className="sidebar-footer">
        <div className="version-panel" aria-label={pick("Versão do NavBR", "NavBR version", "Versión de NavBR", "NavBR-Version", "Version de NavBR")}>
          <span>{pick("VERSÃO", "VERSION", "VERSIÓN", "VERSION", "VERSION")}</span>
          <strong>{buildVersionLabel(appVersion)}</strong>
        </div>
        <label className="sidebar-language">
          <span>{t("common.language")}</span>
          <select value={cultureName} onChange={event => setLanguage(event.target.value)}>
            {languages.map(language => (
              <option key={language.cultureName} value={language.cultureName}>{language.displayName}</option>
            ))}
          </select>
        </label>
      </div>
    </aside>
  );
}

function Home({ state }: { state: NavBrState | null }) {
  const { t } = useI18n();
  const omsi = state?.omsi;
  const telemetry = state?.telemetry;
  const active = Boolean(omsi?.running && telemetry?.inGame);

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
    case "active": return pick("OMSI 3D ativo · movimento suavizado", "OMSI 3D active · smoothed motion", "OMSI 3D activo · movimiento suavizado", "OMSI 3D aktiv · geglättete Bewegung", "OMSI 3D actif · mouvement lissé");
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
    if (!operations || companyHydrated.current) return;
    setCompanyName(operations.company.name || "");
    setCompanyShortName(operations.company.shortName || "");
    setCompanyBaseMap(operations.company.baseMap || "");
    companyHydrated.current = true;
  }, [operations]);

  useEffect(() => {
    if (!operations || profileHydrated.current) return;
    setProfileName(operations.profile.displayName || "");
    setProfileCompany(operations.profile.companyName || "");
    profileHydrated.current = true;
  }, [operations]);

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
              <label><span>{pick("Nome", "Name", "Nombre", "Name", "Nom")}</span><input value={companyName} onChange={event => setCompanyName(event.target.value)} /></label>
              <label><span>{pick("Sigla", "Short name", "Sigla", "Kürzel", "Sigle")}</span><input value={companyShortName} onChange={event => setCompanyShortName(event.target.value)} /></label>
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
              <label className="wide"><span>{pick("Nome no NavBR", "NavBR name", "Nombre en NavBR", "Name in NavBR", "Nom dans NavBR")}</span><input value={profileName} onChange={event => setProfileName(event.target.value)} /></label>
              <label className="wide"><span>{pick("Empresa do perfil", "Profile company", "Empresa del perfil", "Profilunternehmen", "Entreprise du profil")}</span><input value={profileCompany} onChange={event => setProfileCompany(event.target.value)} /></label>
            </div>
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
    setNodeUrl(current => current || companyNetwork.membership?.nodeUrl || "");
    if (!companyNetwork.assignableRoles.includes(inviteRole)) setInviteRole(companyNetwork.assignableRoles.includes("Driver") ? "Driver" : companyNetwork.assignableRoles[0] || "Driver");
  }, [companyNetwork?.membership?.nodeUrl, companyNetwork?.assignableRoles]);

  if (!companyNetwork?.available) return <div className="card empty-state">{pick("Carregando Rede da Empresa…", "Loading Company Network…", "Cargando Red de Empresa…", "Unternehmensnetz wird geladen…", "Chargement du Réseau Entreprise…")}</div>;

  const company = companyNetwork.company;
  const node = companyNetwork.node;
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
        <div className="metric"><small>{pick("MEMBROS", "MEMBERS", "MIEMBROS", "MITGLIEDER", "MEMBRES")}</small><strong>{company?.memberCount ?? 0}</strong></div>
        <div className="metric"><small>NODE</small><strong>{node?.running ? "Online" : "Offline"}</strong></div>
      </section>
      <div className="mp-tabs" role="tablist"><button className={tab === "network" ? "active" : ""} onClick={() => setTab("network")}>{pick("Rede", "Network", "Red", "Netzwerk", "Réseau")}</button><button className={tab === "team" ? "active" : ""} onClick={() => setTab("team")}>{pick("Equipe", "Team", "Equipo", "Team", "Équipe")}</button></div>
      {tab === "network" && (
        <section className="company-network-layout">
          <article className="card company-node-card"><div className="section-heading"><div><span className="eyebrow">IDENTIDADE NAVBR</span><h3>{companyNetwork.identity?.displayName || pick("Motorista", "Driver", "Conductor", "Fahrer", "Conducteur")}</h3></div></div><code>{companyNetwork.identity?.playerId || "—"}</code><p>{pick("Sua identidade fica protegida neste computador.", "Your identity stays protected on this computer.", "Tu identidad permanece protegida en este equipo.", "Deine Identität bleibt auf diesem Computer geschützt.", "Votre identité reste protégée sur cet ordinateur.")}</p></article>
          <article className="card company-node-card"><div className="section-heading"><div><span className="eyebrow">COMPANY NODE</span><h3>TCP 27740</h3></div><span className={`hardware-state-pill ${node?.running ? "connected" : ""}`}>{node?.running ? "ONLINE" : "OFFLINE"}</span></div><p>{pick("O nó da empresa é independente da sala multiplayer TCP 27730.", "The company node is independent from the TCP 27730 multiplayer room.", "El nodo de empresa es independiente de la sala multijugador TCP 27730.", "Der Unternehmens-Node ist unabhängig vom Multiplayer-Raum TCP 27730.", "Le nœud de l’entreprise est indépendant de la salle multijoueur TCP 27730.")}</p><div className="company-node-actions">{node?.running ? <button className="button ghost danger" onClick={() => sendCommand("stopCompanyNode")}>{pick("Parar Company Node", "Stop Company Node", "Detener Company Node", "Company Node stoppen", "Arrêter Company Node")}</button> : <button className="button primary" disabled={!localCompany?.name} onClick={() => sendCommand("startCompanyNode")}>{pick("Hospedar empresa neste PC", "Host company on this PC", "Alojar empresa en este PC", "Unternehmen auf diesem PC hosten", "Héberger l’entreprise sur ce PC")}</button>}</div>{!localCompany?.name && <p className="network-note">{pick("Configure primeiro a Empresa/Frota no CCO.", "Configure Company/Fleet in Operations first.", "Configura primero Empresa/Flota en CCO.", "Zuerst Unternehmen/Flotte in der Leitstelle konfigurieren.", "Configurez d’abord Entreprise/Flotte dans le CCO.")}</p>}{node?.running && <div className="company-node-addresses">{[node.localUrl, ...node.lanUrls].filter(Boolean).filter((value, index, all) => all.indexOf(value) === index).map(url => <code key={url}>{url}</code>)}</div>}</article>
          <article className="card company-join-card"><span className="eyebrow">{pick("ENTRAR EM EMPRESA ONLINE", "JOIN ONLINE COMPANY", "ENTRAR EN EMPRESA ONLINE", "ONLINE-UNTERNEHMEN BEITRETEN", "REJOINDRE UNE ENTREPRISE EN LIGNE")}</span><h3>{pick("Convite assinado", "Signed invite", "Invitación firmada", "Signierte Einladung", "Invitation signée")}</h3><label><span>{pick("Endereço do Company Node", "Company Node address", "Dirección del Company Node", "Company-Node-Adresse", "Adresse du Company Node")}</span><input value={nodeUrl} onChange={event => setNodeUrl(event.target.value)} placeholder="http://192.168.0.10:27740" /></label><label><span>{pick("Código do convite", "Invite code", "Código de invitación", "Einladungscode", "Code d’invitation")}</span><input value={inviteCode} onChange={event => setInviteCode(event.target.value)} placeholder="NBR-...." /></label><button className="button primary" disabled={!nodeUrl.trim() || !inviteCode.trim()} onClick={() => sendCommand("joinCompany", { nodeUrl, inviteCode })}>{pick("Entrar na empresa", "Join company", "Entrar en la empresa", "Unternehmen beitreten", "Rejoindre l’entreprise")}</button></article>
          <article className="card company-invite-card"><span className="eyebrow">{pick("CONVIDAR", "INVITE", "INVITAR", "EINLADEN", "INVITER")}</span><h3>{pick("Novo membro", "New member", "Nuevo miembro", "Neues Mitglied", "Nouveau membre")}</h3><p>{pick("Convites expiram em 7 dias e são criados para um único uso.", "Invites expire in 7 days and are created for one-time use.", "Las invitaciones caducan en 7 días y son de un solo uso.", "Einladungen laufen nach 7 Tagen ab und sind einmalig.", "Les invitations expirent après 7 jours et sont à usage unique.")}</p><label><span>{pick("Cargo inicial", "Initial role", "Cargo inicial", "Anfangsrolle", "Rôle initial")}</span><select value={inviteRole} disabled={!canCreateInvite} onChange={event => setInviteRole(event.target.value)}>{companyNetwork.assignableRoles.map(role => <option key={role} value={role}>{companyRoleLabel(role, pick)}</option>)}</select></label><button className="button ghost" disabled={!canCreateInvite} onClick={() => sendCommand("createCompanyInvite", { role: inviteRole })}>{pick("Criar convite", "Create invite", "Crear invitación", "Einladung erstellen", "Créer une invitation")}</button>{companyNetwork.invite && <div className="company-invite-result"><strong>{companyNetwork.invite.code}</strong><pre>{companyNetwork.invite.payload}</pre><button className="button ghost compact" onClick={() => { if (companyNetwork.invite?.payload) void navigator.clipboard?.writeText(companyNetwork.invite.payload); }}>{pick("Copiar convite", "Copy invite", "Copiar invitación", "Einladung kopieren", "Copier l’invitation")}</button></div>}</article>
        </section>
      )}
      {tab === "team" && <section className="card company-team-card"><div className="section-heading"><div><span className="eyebrow">{pick("EQUIPE", "TEAM", "EQUIPO", "TEAM", "ÉQUIPE")}</span><h3>{company?.name || pick("Empresa Online", "Online Company", "Empresa Online", "Online-Unternehmen", "Entreprise en ligne")}</h3></div><span className="stop-count">{company?.memberCount ?? 0} {pick("membro(s)", "member(s)", "miembro(s)", "Mitglieder", "membre(s)")}</span></div>{!company || company.members.length === 0 ? <div className="empty-state">{pick("Nenhum quadro de membros foi carregado. Atualize a Rede da Empresa.", "No member roster has been loaded. Refresh Company Network.", "No se ha cargado la lista de miembros. Actualiza la Red de Empresa.", "Keine Mitgliederliste geladen. Unternehmensnetz aktualisieren.", "Aucune liste de membres chargée. Actualisez le Réseau Entreprise.")}</div> : <div className="company-members-list">{company.members.map(member => <CompanyMemberRow key={member.playerId} member={member} assignableRoles={companyNetwork.assignableRoles} />)}</div>}</section>}
    </>
  );
}

const hardwareBaudRates = [9600, 19200, 38400, 57600, 115200, 230400];

function Hardware({ state, error }: { state: NavBrState | null; error: string | null }) {
  const { pick } = useI18n();
  const hardware = state?.hardware;
  const [portName, setPortName] = useState("");
  const [baudRate, setBaudRate] = useState(115200);
  const [autoReconnect, setAutoReconnect] = useState(false);

  useEffect(() => {
    if (!hardware || hardware.connected) return;
    setPortName(hardware.portName || hardware.availablePorts[0] || "");
    setBaudRate(hardware.baudRate || 115200);
    setAutoReconnect(hardware.autoReconnect);
  }, [hardware?.connected, hardware?.portName, hardware?.baudRate, hardware?.autoReconnect, hardware?.availablePorts]);

  if (!hardware) {
    return <div className="card empty-state">{pick("Aguardando estado do Hardware Cockpit…", "Waiting for Hardware Cockpit state…", "Esperando el estado del Hardware Cockpit…", "Warte auf Hardware-Cockpit-Status…", "En attente de l’état du Hardware Cockpit…")}</div>;
  }

  const telemetry = hardware.telemetry;

  const persistSelection = (nextPort: string, nextBaud: number, nextAutoReconnect: boolean) => {
    sendCommand("saveHardwareSelection", {
      portName: nextPort,
      baudRate: nextBaud,
      autoReconnect: nextAutoReconnect
    });
  };

  return (
    <>
      <header className="topbar hardware-header">
        <div>
          <span className="eyebrow">HARDWARE COCKPIT</span>
          <h1>{pick("Painel físico", "Physical dashboard", "Panel físico", "Physisches Dashboard", "Tableau physique")}</h1>
          <p>{pick("Bridge serial compartilhada para Arduino, ESP32, letreiros, LEDs e computador de bordo.", "Shared serial bridge for Arduino, ESP32, destination signs, LEDs and onboard computer.", "Bridge serial compartido para Arduino, ESP32, letreros, LEDs y computadora de a bordo.", "Gemeinsame serielle Bridge für Arduino, ESP32, Zielanzeigen, LEDs und Bordcomputer.", "Bridge série partagée pour Arduino, ESP32, girouettes, LED et ordinateur de bord.")}</p>
        </div>
        <div className="top-actions">
          <span className={`connection-pill ${hardware.connected ? "connected" : ""}`}>
            <i /> {hardware.connected ? `${hardware.portName} @ ${hardware.baudRate}` : pick("Serial desconectada", "Serial disconnected", "Serial desconectada", "Seriell getrennt", "Série déconnectée")}
          </span>
        </div>
      </header>

      {error && <div className="command-error">{error}</div>}
      {hardware.lastError && <div className="command-error">{hardware.lastError}</div>}

      <section className="hardware-layout">
        <article className="card hardware-connect-card">
          <div className="section-heading">
            <div>
              <span className="eyebrow">USB / SERIAL</span>
              <h3>{hardware.protocol}</h3>
            </div>
            <span className={`hardware-state-pill ${hardware.connected ? "connected" : ""}`}>
              {hardware.connected ? pick("5 Hz ativo", "5 Hz active", "5 Hz activo", "5 Hz aktiv", "5 Hz actif") : pick("Aguardando conexão", "Waiting for connection", "Esperando conexión", "Warte auf Verbindung", "En attente de connexion")}
            </span>
          </div>

          <div className="hardware-controls">
            <label>
              <span>{pick("Porta COM", "COM port", "Puerto COM", "COM-Port", "Port COM")}</span>
              <select
                value={portName}
                disabled={hardware.connected}
                onChange={event => {
                  const value = event.target.value;
                  setPortName(value);
                  persistSelection(value, baudRate, autoReconnect);
                }}
              >
                <option value="">{pick("Selecione…", "Select…", "Selecciona…", "Auswählen…", "Sélectionner…")}</option>
                {hardware.availablePorts.map(port => <option key={port} value={port}>{port}</option>)}
              </select>
            </label>

            <label>
              <span>Baud</span>
              <select
                value={baudRate}
                disabled={hardware.connected}
                onChange={event => {
                  const value = Number(event.target.value);
                  setBaudRate(value);
                  persistSelection(portName, value, autoReconnect);
                }}
              >
                {hardwareBaudRates.map(baud => <option key={baud} value={baud}>{baud}</option>)}
              </select>
            </label>

            <label className="hardware-auto">
              <input
                type="checkbox"
                checked={autoReconnect}
                onChange={event => {
                  const value = event.target.checked;
                  setAutoReconnect(value);
                  persistSelection(portName, baudRate, value);
                }}
              />
              <span>{pick("Reconectar automaticamente na mesma COM", "Automatically reconnect to the same COM", "Reconectar automáticamente al mismo COM", "Automatisch mit demselben COM-Port verbinden", "Reconnecter automatiquement au même port COM")}</span>
            </label>
          </div>

          <div className="hardware-actions">
            {hardware.connected ? (
              <button className="button ghost danger" onClick={() => sendCommand("disconnectHardware")}>{pick("Desconectar", "Disconnect", "Desconectar", "Trennen", "Déconnecter")}</button>
            ) : (
              <button
                className="button primary"
                disabled={!portName}
                onClick={() => sendCommand("connectHardware", { portName, baudRate, autoReconnect })}
              >
                {pick("Conectar hardware", "Connect hardware", "Conectar hardware", "Hardware verbinden", "Connecter le matériel")}
              </button>
            )}
            <button className="button ghost" onClick={() => sendCommand("refreshState")}>{pick("Atualizar portas", "Refresh ports", "Actualizar puertos", "Ports aktualisieren", "Actualiser les ports")}</button>
          </div>

          <p className="hardware-note">
            {pick("O NavBR nunca troca silenciosamente para outra porta COM. O auto-reconnect tenta apenas a porta explicitamente escolhida.", "NavBR never silently switches to another COM port. Auto-reconnect only retries the explicitly selected port.", "NavBR nunca cambia silenciosamente a otro puerto COM. La reconexión automática solo intenta el puerto elegido explícitamente.", "NavBR wechselt niemals unbemerkt auf einen anderen COM-Port. Auto-Reconnect versucht nur den ausdrücklich gewählten Port.", "NavBR ne bascule jamais silencieusement vers un autre port COM. La reconnexion automatique ne tente que le port explicitement choisi.")}
          </p>
        </article>

        <article className="card hardware-live-card">
          <div className="section-heading">
            <div><span className="eyebrow">{pick("TELEMETRIA AO VIVO", "LIVE TELEMETRY", "TELEMETRÍA EN VIVO", "LIVE-TELEMETRIE", "TÉLÉMÉTRIE EN DIRECT")}</span><h3>{telemetry ? pick("Quadro atual", "Current frame", "Cuadro actual", "Aktueller Frame", "Trame actuelle") : pick("Aguardando OMSI", "Waiting for OMSI", "Esperando OMSI", "Warte auf OMSI", "En attente d’OMSI")}</h3></div>
            {hardware.lastFrameSentAtUtc && <small>{pick("Último envio", "Last send", "Último envío", "Letzter Versand", "Dernier envoi")}: {new Date(hardware.lastFrameSentAtUtc).toLocaleTimeString()}</small>}
          </div>

          <div className="hardware-live-grid">
            <span><small>{pick("LINHA", "LINE", "LÍNEA", "LINIE", "LIGNE")}</small><strong>{telemetry?.line || "—"}</strong></span>
            <span><small>{pick("DESTINO", "DESTINATION", "DESTINO", "ZIEL", "DESTINATION")}</small><strong>{telemetry?.destination || "—"}</strong></span>
            <span><small>{pick("PRÓXIMA PARADA", "NEXT STOP", "PRÓXIMA PARADA", "NÄCHSTER HALT", "PROCHAIN ARRÊT")}</small><strong>{telemetry?.nextStop || "—"}</strong></span>
            <span><small>{pick("RUA ATUAL", "CURRENT STREET", "CALLE ACTUAL", "AKTUELLE STRASSE", "RUE ACTUELLE")}</small><strong>{telemetry?.currentStreet || "—"}</strong></span>
            <span><small>{pick("VELOCIDADE", "SPEED", "VELOCIDAD", "GESCHWINDIGKEIT", "VITESSE")}</small><strong>{telemetry ? `${format(telemetry.speedKph, 1)} km/h` : "—"}</strong></span>
            <span className={telemetry?.stopRequested ? "attention" : ""}><small>{pick("PARADA SOLICITADA", "STOP REQUESTED", "PARADA SOLICITADA", "HALTEWUNSCH", "ARRÊT DEMANDÉ")}</small><strong>{telemetry ? telemetry.stopRequested ? pick("SIM", "YES", "SÍ", "JA", "OUI") : pick("Não", "No", "No", "Nein", "Non") : "—"}</strong></span>
            <span><small>{pick("PORTAS", "DOORS", "PUERTAS", "TÜREN", "PORTES")}</small><strong>{telemetry?.doors || "—"}</strong></span>
            <span><small>{pick("SETA", "TURN SIGNAL", "INTERMITENTE", "BLINKER", "CLIGNOTANT")}</small><strong>{telemetry?.turnSignal || "—"}</strong></span>
          </div>
        </article>
      </section>

      <section className="card hardware-payload-card">
        <div className="section-heading">
          <div><span className="eyebrow">{pick("PREVIEW TÉCNICO", "TECHNICAL PREVIEW", "VISTA TÉCNICA", "TECHNISCHE VORSCHAU", "APERÇU TECHNIQUE")}</span><h3>{pick("Pacote enviado ao cockpit", "Packet sent to cockpit", "Paquete enviado al cockpit", "An Cockpit gesendetes Paket", "Paquet envoyé au cockpit")}</h3></div>
          <span className="route-source-pill">JSON LINES</span>
        </div>
        <pre>{hardware.payloadPreview || `{"protocol":"${hardware.protocol}","state":"waiting-for-telemetry"}`}</pre>
      </section>
    </>
  );
}

type SettingsTab = "installations" | "hud" | "roadmap" | "diagnostics" | "network" | "advanced";

function OmsiProfileCard({ profile }: { profile: NavBrOmsiInstallation }) {
  const { pick } = useI18n();
  const [name, setName] = useState(profile.name);
  const [launchArguments, setLaunchArguments] = useState(profile.launchArguments || "");

  useEffect(() => {
    setName(profile.name);
    setLaunchArguments(profile.launchArguments || "");
  }, [profile.id, profile.name, profile.launchArguments]);

  return (
    <article className={`installation-card ${profile.isPreferred ? "preferred" : ""} ${profile.isRunning ? "running" : ""}`}>
      <div className="installation-top">
        <div>
          <div className="installation-badges">
            {profile.isPreferred && <span className="install-badge preferred">{pick("Preferido", "Preferred", "Preferido", "Bevorzugt", "Préféré")}</span>}
            {profile.isRunning && <span className="install-badge running">{pick("Em execução", "Running", "En ejecución", "Läuft", "En cours")}</span>}
            {!profile.executableExists && <span className="install-badge invalid">{pick("Omsi.exe ausente", "Omsi.exe missing", "Falta Omsi.exe", "Omsi.exe fehlt", "Omsi.exe absent")}</span>}
          </div>
          <h3>{profile.name}</h3>
          <code>{profile.installDirectory}</code>
        </div>
        <button
          className="button primary compact"
          disabled={!profile.executableExists}
          onClick={() => sendCommand("launchOmsiProfile", { profileId: profile.id })}
        >
          {profile.isRunning ? pick("Ativar OMSI", "Activate OMSI", "Activar OMSI", "OMSI aktivieren", "Activer OMSI") : pick("Executar", "Run", "Ejecutar", "Starten", "Exécuter")}
        </button>
      </div>

      <div className="installation-edit-grid">
        <label>
          <span>{pick("Nome do perfil", "Profile name", "Nombre del perfil", "Profilname", "Nom du profil")}</span>
          <input value={name} onChange={event => setName(event.target.value)} />
        </label>
        <label>
          <span>{pick("Argumentos de inicialização", "Launch arguments", "Argumentos de inicio", "Startargumente", "Arguments de lancement")}</span>
          <input value={launchArguments} onChange={event => setLaunchArguments(event.target.value)} placeholder={pick("Opcional", "Optional", "Opcional", "Optional", "Optionnel")} />
        </label>
      </div>

      <div className="installation-actions">
        <button className="button ghost compact" onClick={() => sendCommand("updateOmsiProfile", {
          profileId: profile.id,
          name,
          launchArguments
        })}>{pick("Salvar perfil", "Save profile", "Guardar perfil", "Profil speichern", "Enregistrer le profil")}</button>
        {!profile.isPreferred && (
          <button className="button ghost compact" onClick={() => sendCommand("setPreferredOmsiProfile", { profileId: profile.id })}>
            {pick("Tornar preferido", "Make preferred", "Hacer preferido", "Als bevorzugt setzen", "Définir comme préféré")}
          </button>
        )}
        <button className="button ghost compact" onClick={() => sendCommand("openOmsiProfileFolder", { profileId: profile.id })}>
          {pick("Abrir pasta", "Open folder", "Abrir carpeta", "Ordner öffnen", "Ouvrir le dossier")}
        </button>
        <button className="button ghost compact danger" onClick={() => sendCommand("removeOmsiProfile", { profileId: profile.id })}>
          {pick("Remover", "Remove", "Eliminar", "Entfernen", "Supprimer")}
        </button>
      </div>

      {profile.lastUsedAtUtc && (
        <small className="install-last-used">{pick("Último uso", "Last used", "Último uso", "Zuletzt verwendet", "Dernière utilisation")}: {new Date(profile.lastUsedAtUtc).toLocaleString()}</small>
      )}
    </article>
  );
}


const COMPOSED_HUD_PRESETS = new Set([
  "immersive-operation",
  "transit-control",
  "cockpit-digital",
  "navigation-pro",
  "multiplayer-focus",
  "classic-omsi-plus",
  "minimal-driver",
  "streamer-broadcast",
  "glass-night",
  "city-operations",
  "driver-assistance"
]);

function isComposedHudPreset(id: string | null | undefined) {
  return COMPOSED_HUD_PRESETS.has((id || "").toLowerCase());
}

const HUD_PRESET_GROUPS = [
  {
    id: "operations",
    ids: ["immersive-operation", "transit-control", "city-operations", "driver-assistance"]
  },
  {
    id: "driving",
    ids: ["cockpit-digital", "navigation-pro", "minimal-driver"]
  },
  {
    id: "social",
    ids: ["multiplayer-focus", "streamer-broadcast"]
  },
  {
    id: "classic",
    ids: ["classic-omsi-plus", "glass-night"]
  },
  {
    id: "current",
    ids: ["normal", "compact", "full", "rp-urban", "racing-minimal", "lcd-amber", "transparent"]
  }
] as const;

function HudSettingsPanel({ hud }: { hud: NavBrHudState }) {
  const { t, pick } = useI18n();
  const [draft, setDraft] = useState<NavBrHudState>(hud);
  const [dirty, setDirty] = useState(false);
  const [previewing, setPreviewing] = useState(Boolean(hud.previewActive));
  const previousClassicPreset = useRef(
    isComposedHudPreset(hud.preset) ? "normal" : hud.preset || "normal"
  );
  const composedMode = isComposedHudPreset(draft.preset);
  const selectedPreset = hud.presets.find(item => item.id === draft.preset);
  const groupedPresets = HUD_PRESET_GROUPS
    .map(group => ({
      ...group,
      presets: group.ids
        .map(id => hud.presets.find(item => item.id === id))
        .filter((item): item is NavBrHudPreset => Boolean(item))
    }))
    .filter(group => group.presets.length > 0);

  useEffect(() => {
    if (!dirty) {
      setDraft(hud);
      if (!isComposedHudPreset(hud.preset)) {
        previousClassicPreset.current = hud.preset || "normal";
      }
    }
  }, [hud, dirty]);

  const patch = (next: Partial<NavBrHudState>) => {
    setDraft(current => ({ ...current, ...next }));
    setDirty(true);
  };

  const applyPreset = (presetId: string) => {
    const preset = hud.presets.find(item => item.id === presetId);
    if (!preset) {
      patch({ preset: presetId });
      return;
    }

    patch({
      preset: preset.id,
      theme: preset.themeId,
      width: preset.width,
      scale: preset.scale,
      opacity: preset.opacity,
      showFuel: preset.showFuel,
      showPedals: preset.showPedals,
      showStatus: preset.showStatus,
      showMinimap: preset.showMinimap,
      showMultiplayer: preset.showMultiplayer,
      showAlerts: preset.showAlerts,
      showSideIndicators: preset.showSideIndicators
    });
  };

  const hudPayload = useMemo(() => ({
    enabled: draft.enabled,
    preset: draft.preset,
    theme: draft.theme,
    anchor: draft.anchor,
    scale: draft.scale,
    width: draft.width,
    height: draft.height,
    opacity: draft.opacity,
    autoScale: draft.autoScale,
    showFuel: draft.showFuel,
    showPedals: draft.showPedals,
    showStatus: draft.showStatus,
    showMinimap: draft.showMinimap,
    showMultiplayer: draft.showMultiplayer,
    showAlerts: draft.showAlerts,
    showSideIndicators: draft.showSideIndicators,
    minimapScale: draft.minimapScale,
    multiplayerScale: draft.multiplayerScale,
    alertsScale: draft.alertsScale,
    sideIndicatorsScale: draft.sideIndicatorsScale
  }), [
    draft.enabled,
    draft.preset,
    draft.theme,
    draft.anchor,
    draft.scale,
    draft.width,
    draft.height,
    draft.opacity,
    draft.autoScale,
    draft.showFuel,
    draft.showPedals,
    draft.showStatus,
    draft.showMinimap,
    draft.showMultiplayer,
    draft.showAlerts,
    draft.showSideIndicators,
    draft.minimapScale,
    draft.multiplayerScale,
    draft.alertsScale,
    draft.sideIndicatorsScale
  ]);

  useEffect(() => {
    if (!previewing) return;
    const timer = window.setTimeout(() => {
      sendCommand("previewHudSettings", hudPayload);
    }, 90);
    return () => window.clearTimeout(timer);
  }, [previewing, hudPayload]);

  useEffect(() => () => {
    sendCommand("clearHudPreview");
  }, []);

  const togglePreview = () => {
    if (previewing) {
      sendCommand("clearHudPreview");
      setPreviewing(false);
      return;
    }
    setPreviewing(true);
  };

  const save = () => {
    sendCommand("saveHudSettings", hudPayload);
    setPreviewing(false);
    setDirty(false);
  };

  const moduleChecks = [
    ["showFuel", t("hud.fuel")],
    ["showPedals", t("hud.pedals")],
    ["showStatus", t("hud.indicators")],
    ["showMinimap", t("hud.minimap")],
    ["showMultiplayer", t("hud.multiplayer")],
    ["showAlerts", t("hud.alerts")],
    ["showSideIndicators", t("hud.sideIndicators")]
  ] as const;

  return (
    <section className="hud-settings-layout">
      <article className="card hud-settings-card">
        <div className="section-heading">
          <div><span className="eyebrow">HUD</span><h3>{t("hud.identity")}</h3></div>
          <span className={`hardware-state-pill ${draft.enabled ? "connected" : ""}`}>
            {draft.enabled ? t("common.active") : t("common.disabled")}
          </span>
        </div>

        <label className="diagnostics-toggle hud-enabled-toggle">
          <input
            type="checkbox"
            checked={draft.enabled}
            onChange={event => patch({ enabled: event.target.checked })}
          />
          <span>{t("hud.showPanel")}</span>
        </label>

        <div className="hud-mode-switch">
          <div>
            <span className="eyebrow">{pick("MODO DE HUD", "HUD MODE", "MODO DE HUD", "HUD-MODUS", "MODE HUD")}</span>
            <strong>{composedMode
              ? selectedPreset?.displayName || pick("HUD composto", "Composed HUD", "HUD compuesto", "Komponiertes HUD", "HUD composé")
              : pick("HUD atual", "Current HUD", "HUD actual", "Aktuelles HUD", "HUD actuel")}</strong>
            <small>{composedMode
              ? pick(
                  "Preset em tela com composição própria. O HUD clássico continua disponível e pode ser restaurado a qualquer momento.",
                  "Screen-composed preset with its own layout. The classic HUD remains available and can be restored at any time.",
                  "Preset compuesto en pantalla con diseño propio. El HUD clásico sigue disponible y se puede restaurar en cualquier momento.",
                  "Bildschirm-Preset mit eigenem Layout. Das klassische HUD bleibt verfügbar und kann jederzeit wiederhergestellt werden.",
                  "Preset composé à l’écran avec sa propre disposition. Le HUD classique reste disponible et peut être restauré à tout moment."
                )
              : pick(
                  "Mantém o layout atual. Você pode testar qualquer preset novo sem substituir definitivamente este HUD.",
                  "Keeps the current layout. You can test any new preset without permanently replacing this HUD.",
                  "Mantiene el diseño actual. Puedes probar cualquier preset nuevo sin reemplazar definitivamente este HUD.",
                  "Behält das aktuelle Layout. Neue Presets können getestet werden, ohne dieses HUD dauerhaft zu ersetzen.",
                  "Conserve la disposition actuelle. Vous pouvez tester les nouveaux presets sans remplacer définitivement ce HUD."
                )}</small>
          </div>
          <button
            type="button"
            className={`button ${composedMode ? "ghost" : "primary"}`}
            onClick={() => {
              if (composedMode) {
                applyPreset(previousClassicPreset.current || "normal");
              } else {
                previousClassicPreset.current = draft.preset || "normal";
                applyPreset("immersive-operation");
              }
            }}
          >
            {composedMode
              ? pick("Voltar ao HUD atual", "Back to current HUD", "Volver al HUD actual", "Zum aktuellen HUD", "Revenir au HUD actuel")
              : pick("Explorar novos HUDs", "Explore new HUDs", "Explorar nuevos HUD", "Neue HUDs erkunden", "Explorer les nouveaux HUD")}
          </button>
        </div>

        <div className="hud-preset-groups">
          {groupedPresets.map(group => {
            const label = group.id === "operations"
              ? pick("Operação", "Operations", "Operación", "Betrieb", "Exploitation")
              : group.id === "driving"
                ? pick("Direção e navegação", "Driving & navigation", "Conducción y navegación", "Fahren & Navigation", "Conduite et navigation")
                : group.id === "social"
                  ? pick("Multiplayer e streaming", "Multiplayer & streaming", "Multijugador y streaming", "Multiplayer & Streaming", "Multijoueur et streaming")
                  : group.id === "classic"
                    ? pick("Clássicos e discretos", "Classic & subtle", "Clásicos y discretos", "Klassisch & dezent", "Classiques et discrets")
                    : pick("HUD atual", "Current HUD", "HUD actual", "Aktuelles HUD", "HUD actuel");
            return (
              <section className="hud-preset-group" key={group.id}>
                <div className="hud-preset-group-heading">
                  <strong>{label}</strong>
                  <span>{group.presets.length}</span>
                </div>
                <div className="hud-style-gallery">
                  {group.presets.map(preset => (
                    <button
                      type="button"
                      key={preset.id}
                      className={`hud-style-card ${draft.preset === preset.id ? "selected" : ""}`}
                      data-hud-theme={preset.themeId}
                      data-hud-badge={
                        preset.id === "immersive-operation"
                          ? pick("NOVO", "NEW", "NUEVO", "NEU", "NOUVEAU")
                          : preset.id === "cockpit-digital"
                            ? "CLUSTER"
                            : preset.id === "navigation-pro"
                              ? "GPS"
                              : preset.id === "driver-assistance"
                                ? pick("ASSIST.", "ASSIST", "ASIST.", "ASSIST.", "ASSIST.")
                                : undefined
                      }
                      onClick={() => applyPreset(preset.id)}
                    >
                      <span className="hud-style-preview" aria-hidden="true">
                        <i className="hud-style-route" />
                        <i className="hud-style-speed" />
                        <i className="hud-style-chip first" />
                        <i className="hud-style-chip second" />
                      </span>
                      <span className="hud-style-copy">
                        <strong>{preset.displayName}</strong>
                        <small>{preset.inspiration}</small>
                        <em>{preset.description}</em>
                        <span className="hud-style-tags" aria-hidden="true">
                          {preset.showMinimap && <i>{pick("Mapa", "Map", "Mapa", "Karte", "Carte")}</i>}
                          {preset.showMultiplayer && <i>MP</i>}
                          {preset.showStatus && <i>{pick("Status", "Status", "Estado", "Status", "État")}</i>}
                          {preset.showAlerts && <i>{pick("Alertas", "Alerts", "Alertas", "Alarme", "Alertes")}</i>}
                          {preset.showPedals && <i>{pick("Pedais", "Pedals", "Pedales", "Pedale", "Pédales")}</i>}
                        </span>
                      </span>
                    </button>
                  ))}
                </div>
              </section>
            );
          })}
        </div>

        <div className="hud-select-grid">
          <label className="voice-field">
            <span>{t("hud.style")}</span>
            <select value={draft.preset} onChange={event => applyPreset(event.target.value)}>
              {hud.presets.map(item => <option key={item.id} value={item.id}>{item.displayName}</option>)}
            </select>
          </label>
          <label className="voice-field">
            <span>{t("hud.theme")}</span>
            <select value={draft.theme} onChange={event => patch({ theme: event.target.value })}>
              {hud.themes.map(item => <option key={item.id} value={item.id}>{item.displayName}</option>)}
            </select>
          </label>
          <label className="voice-field">
            <span>{composedMode
              ? pick("Âncora do painel principal", "Primary panel anchor", "Ancla del panel principal", "Anker des Hauptpanels", "Ancrage du panneau principal")
              : t("hud.anchor")}</span>
            <select value={draft.anchor} onChange={event => patch({ anchor: event.target.value })}>
              {hud.anchors.map(item => <option key={item.id} value={item.id}>{item.displayName}</option>)}
            </select>
          </label>
          <label className="diagnostics-toggle compact-toggle">
            <input
              type="checkbox"
              checked={draft.autoScale}
              onChange={event => patch({ autoScale: event.target.checked })}
            />
            <span>{t("hud.autoScale")}</span>
          </label>
        </div>

        <div className="hud-preview-line">
          <strong>{hud.presets.find(item => item.id === draft.preset)?.displayName || draft.preset}</strong>
          <span>{Math.round(draft.width)} px · {Math.round(draft.scale * 100)}% · {Math.round(draft.opacity * 100)}%</span>
        </div>

        {previewing && (
          <div className="hud-contextual-note">
            <strong>{pick("PRÉVIA AO VIVO", "LIVE PREVIEW", "VISTA PREVIA EN VIVO", "LIVE-VORSCHAU", "APERÇU EN DIRECT")}</strong>
            <span>{pick(
              "O preset selecionado está sendo mostrado temporariamente no overlay. Tema, tamanho e módulos atualizam ao vivo e ainda não foram salvos.",
              "The selected preset is temporarily visible on the overlay. Theme, size and widget changes update live and are not saved yet.",
              "El preset seleccionado se muestra temporalmente en el overlay. Tema, tamaño y módulos se actualizan en vivo y aún no se guardan.",
              "Das ausgewählte Preset wird vorübergehend im Overlay angezeigt. Thema, Größe und Module aktualisieren sich live und sind noch nicht gespeichert.",
              "Le preset sélectionné est affiché temporairement dans l’overlay. Le thème, la taille et les modules se mettent à jour en direct sans être enregistrés."
            )}</span>
          </div>
        )}
      </article>

      <article className="card hud-settings-card">
        <span className="eyebrow">{t("hud.size")}</span>
        <div className="hud-slider-list">
          <label>
            <span><strong>{t("hud.scale")}</strong><em>{Math.round(draft.scale * 100)}%</em></span>
            <input type="range" min="0.6" max="1.8" step="0.05" value={draft.scale}
              onChange={event => patch({ scale: Number(event.target.value) })} />
          </label>
          <label>
            <span><strong>{t("hud.width")}</strong><em>{Math.round(draft.width)} px</em></span>
            <input type="range" min="280" max="960" step="10" value={draft.width}
              onChange={event => patch({ width: Number(event.target.value) })} />
          </label>
          <label>
            <span><strong>{composedMode
              ? pick("Altura mínima do painel principal", "Primary panel minimum height", "Altura mínima del panel principal", "Mindesthöhe des Hauptpanels", "Hauteur minimale du panneau principal")
              : t("hud.height")}</strong><em>{draft.height < 1 ? t("hud.auto") : `${Math.round(draft.height)} px`}</em></span>
            <input type="range" min="0" max="720" step="10" value={draft.height}
              onChange={event => patch({ height: Number(event.target.value) })} />
          </label>
          <label>
            <span><strong>{t("hud.opacity")}</strong><em>{Math.round(draft.opacity * 100)}%</em></span>
            <input type="range" min="0.35" max="1" step="0.05" value={draft.opacity}
              onChange={event => patch({ opacity: Number(event.target.value) })} />
          </label>
        </div>
      </article>

      <article className="card hud-settings-card">
        <span className="eyebrow">{t("hud.visibleModules")}</span>
        <div className="hud-module-grid">
          {moduleChecks.map(([key, label]) => (
            <label className="diagnostics-toggle compact-toggle" key={key}>
              <input
                type="checkbox"
                checked={draft[key]}
                onChange={event => patch({ [key]: event.target.checked } as Partial<NavBrHudState>)}
              />
              <span>{label}</span>
            </label>
          ))}
        </div>
        {draft.preset === "minimal-driver" && draft.showMinimap && (
          <div className="hud-contextual-note">
            <strong>{pick("Módulo contextual", "Contextual widget", "Módulo contextual", "Kontextmodul", "Module contextuel")}</strong>
            <span>{pick(
              "No Minimal Driver, o minimapa fica oculto durante a condução normal e aparece automaticamente apenas quando a rota foi resolvida e o ônibus sai dela.",
              "In Minimal Driver, the minimap stays hidden during normal driving and appears automatically only when the route is resolved and the bus goes off route.",
              "En Minimal Driver, el minimapa permanece oculto durante la conducción normal y aparece automáticamente solo cuando la ruta está resuelta y el autobús sale de ella.",
              "Im Minimal Driver bleibt die Minikarte bei normaler Fahrt verborgen und erscheint automatisch nur bei aufgelöster Route und Verlassen der Route.",
              "Dans Minimal Driver, la mini-carte reste masquée en conduite normale et apparaît automatiquement uniquement lorsque l’itinéraire est résolu et que le bus le quitte."
            )}</span>
          </div>
        )}
      </article>

      <article className="card hud-settings-card">
        <span className="eyebrow">{t("hud.widgetScale")}</span>
        <div className="hud-slider-list">
          {([
            ["minimapScale", t("hud.minimap")],
            ["multiplayerScale", t("nav.multiplayer")],
            ["alertsScale", t("hud.alerts")],
            ["sideIndicatorsScale", t("hud.sideIndicators")]
          ] as const).map(([key, label]) => (
            <label key={key}>
              <span><strong>{label}</strong><em>{draft[key].toFixed(2)}×</em></span>
              <input type="range" min="0.55" max="2" step="0.05" value={draft[key]}
                onChange={event => patch({ [key]: Number(event.target.value) } as Partial<NavBrHudState>)} />
            </label>
          ))}
        </div>
      </article>

      <div className="hud-settings-actions">
        <button
          className={`button ${previewing ? "ghost" : "primary"}`}
          onClick={togglePreview}
        >
          {previewing
            ? pick("Ocultar prévia", "Hide preview", "Ocultar vista previa", "Vorschau ausblenden", "Masquer l’aperçu")
            : pick("Visualizar prévia", "Preview on screen", "Ver vista previa", "Vorschau anzeigen", "Visualiser l’aperçu")}
        </button>
        <button className="button primary" disabled={!dirty} onClick={save}>
          {dirty ? t("hud.apply") : t("hud.applied")}
        </button>
        <button className="button ghost" onClick={() => {
          sendCommand("resetHudSettings");
          setPreviewing(false);
          setDirty(false);
        }}>{t("common.reset")}</button>
        <button
          className="button ghost"
          disabled={previewing}
          title={previewing
            ? pick(
                "Aplique ou oculte a prévia antes de mover o HUD.",
                "Apply or hide the preview before moving the HUD.",
                "Aplica u oculta la vista previa antes de mover el HUD.",
                "Übernimm oder schließe die Vorschau, bevor du das HUD verschiebst.",
                "Appliquez ou masquez l’aperçu avant de déplacer le HUD."
              )
            : undefined}
          onClick={() => sendCommand("toggleHudLayout")}
        >
          {t("hud.move")}
        </button>
      </div>
    </section>
  );
}


function formatFileSize(bytes: number | null | undefined) {
  if (bytes == null || !Number.isFinite(bytes)) return "—";
  if (bytes < 1024) return `${Math.round(bytes)} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

function roadmapStatusLabel(
  status: string | null | undefined,
  pick: (pt: string, en: string, es: string, de: string, fr: string) => string
) {
  switch (status) {
    case "analysis-ready": return pick("Análise pronta", "Analysis ready", "Análisis listo", "Analyse bereit", "Analyse prête");
    case "analysis-no-tile-images": return pick("Sem imagens de tile", "No tile images", "Sin imágenes de tile", "Keine Tile-Bilder", "Aucune image de tile");
    case "analysis-failed": return pick("Falha na análise", "Analysis failed", "Falló el análisis", "Analyse fehlgeschlagen", "Échec de l’analyse");
    case "building-tiles": return pick("Montando roadmap por tiles", "Building roadmap from tiles", "Montando roadmap por tiles", "Roadmap aus Tiles wird erstellt", "Construction de la roadmap depuis les tiles");
    case "building-vector": return pick("Gerando roadmap vetorial", "Generating vector roadmap", "Generando roadmap vectorial", "Vektor-Roadmap wird erzeugt", "Génération de la roadmap vectorielle");
    case "tiles-built": return pick("Roadmap por tiles concluído", "Tile roadmap completed", "Roadmap por tiles completado", "Tile-Roadmap abgeschlossen", "Roadmap par tiles terminée");
    case "vector-built": return pick("Roadmap vetorial concluído", "Vector roadmap completed", "Roadmap vectorial completado", "Vektor-Roadmap abgeschlossen", "Roadmap vectorielle terminée");
    case "tiles-build-failed": return pick("Falha na geração por tiles", "Tile build failed", "Falló la generación por tiles", "Tile-Erzeugung fehlgeschlagen", "Échec de la génération par tiles");
    case "vector-build-failed": return pick("Falha na geração vetorial", "Vector build failed", "Falló la generación vectorial", "Vektor-Erzeugung fehlgeschlagen", "Échec de la génération vectorielle");
    default: return status || pick("Pronto", "Ready", "Listo", "Bereit", "Prêt");
  }
}

function RoadmapStudioPanel({ roadmap }: { roadmap: NavBrRoadmapStudioState }) {
  const { t, pick } = useI18n();
  const [selectedFolder, setSelectedFolder] = useState("");

  useEffect(() => {
    setSelectedFolder(current => {
      if (current && roadmap.maps.some(map => map.folderName === current)) {
        return current;
      }
      if (roadmap.selectedFolder && roadmap.maps.some(map => map.folderName === roadmap.selectedFolder)) {
        return roadmap.selectedFolder;
      }
      return roadmap.maps[0]?.folderName || "";
    });
  }, [roadmap.maps, roadmap.selectedFolder]);

  const selectedMap = roadmap.maps.find(map => map.folderName === selectedFolder) || null;
  const selectedMatchesNative = roadmap.selectedFolder === selectedFolder;
  const analysis = selectedMatchesNative ? roadmap.analysis : null;
  const result = selectedMatchesNative ? roadmap.result : null;
  const progress = Math.max(0, Math.min(100, (roadmap.progress ?? 0) * 100));

  return (
    <section className="roadmap-studio-layout">
      <article className="card roadmap-control-card">
        <div className="section-heading">
          <div>
            <span className="eyebrow">ROADMAP STUDIO</span>
            <h3>{t("roadmap.title")}</h3>
          </div>
          <span className={`hardware-state-pill ${roadmap.busy ? "connected" : ""}`}>
            {roadmap.busy ? pick("Processando", "Processing", "Procesando", "Verarbeitung", "Traitement") : roadmapStatusLabel(roadmap.status, pick)}
          </span>
        </div>

        {roadmap.maps.length === 0 ? (
          <div className="empty-state">{t("roadmap.noMaps")}</div>
        ) : (
          <>
            <label className="voice-field roadmap-map-select">
              <span>{t("roadmap.map")}</span>
              <select
                value={selectedFolder}
                disabled={roadmap.busy}
                onChange={event => setSelectedFolder(event.target.value)}
              >
                {roadmap.maps.map(map => (
                  <option key={map.folderName} value={map.folderName}>{map.displayName}</option>
                ))}
              </select>
            </label>

            {selectedMap && (
              <div className="roadmap-map-facts">
                <span><small>{pick("PASTA", "FOLDER", "CARPETA", "ORDNER", "DOSSIER")}</small><strong>{selectedMap.folderName}</strong></span>
                <span><small>{pick("TILES DO MAPA", "MAP TILES", "TILES DEL MAPA", "KARTEN-TILES", "TILES DE LA CARTE")}</small><strong>{selectedMap.tileCount}</strong></span>
                <span><small>WHOLE ROADMAP</small><strong>{selectedMap.roadmapExists ? pick("Existe", "Exists", "Existe", "Vorhanden", "Présente") : pick("Ausente", "Missing", "Ausente", "Fehlt", "Absente")}</strong></span>
              </div>
            )}

            <div className="roadmap-actions">
              <button
                className="button ghost"
                disabled={!selectedFolder || roadmap.busy}
                onClick={() => sendCommand("analyzeRoadmap", { folderName: selectedFolder })}
              >
                {t("roadmap.analyze")}
              </button>
              <button
                className="button primary"
                disabled={!selectedFolder || roadmap.busy || !analysis?.canBuildFromTiles}
                onClick={() => sendCommand("buildRoadmapTiles", { folderName: selectedFolder })}
              >
                {t("roadmap.buildTiles")}
              </button>
              <button
                className="button ghost"
                disabled={!selectedFolder || roadmap.busy}
                onClick={() => sendCommand("buildRoadmapVector", { folderName: selectedFolder })}
              >
                {t("roadmap.buildVector")}
              </button>
              <button
                className="button ghost"
                disabled={!selectedFolder || roadmap.busy}
                onClick={() => sendCommand("openRoadmapFolder", { folderName: selectedFolder })}
              >
                {t("common.openFolder")}
              </button>
            </div>

            {roadmap.busy && (
              <div className="roadmap-progress">
                <div><span>{roadmapStatusLabel(roadmap.status, pick)}</span><strong>{Math.round(progress)}%</strong></div>
                <div className="roadmap-progress-track"><i style={{ width: `${progress}%` }} /></div>
              </div>
            )}
          </>
        )}

        {roadmap.error && <div className="directory-error">{roadmap.error}</div>}
      </article>

      <article className="card roadmap-analysis-card">
        <div className="section-heading">
          <div><span className="eyebrow">{t("roadmap.analysis")}</span><h3>{t("roadmap.tilesOutput")}</h3></div>
        </div>

        {!analysis ? (
          <div className="empty-state">{t("roadmap.emptyAnalysis")}</div>
        ) : (
          <>
            <div className="roadmap-analysis-grid">
              <span><small>{pick("IMAGENS DE TILE", "TILE IMAGES", "IMÁGENES DE TILE", "TILE-BILDER", "IMAGES DE TILE")}</small><strong>{analysis.tileImageCount}</strong></span>
              <span><small>{pick("POSIÇÕES SEM IMAGEM", "POSITIONS WITHOUT IMAGE", "POSICIONES SIN IMAGEN", "POSITIONEN OHNE BILD", "POSITIONS SANS IMAGE")}</small><strong>{analysis.missingTileImages}</strong></span>
              <span><small>{pick("GRADE X", "GRID X", "CUADRÍCULA X", "RASTER X", "GRILLE X")}</small><strong>{analysis.minGridX} … {analysis.maxGridX}</strong></span>
              <span><small>{pick("GRADE Y", "GRID Y", "CUADRÍCULA Y", "RASTER Y", "GRILLE Y")}</small><strong>{analysis.minGridY} … {analysis.maxGridY}</strong></span>
              <span><small>TILE</small><strong>{analysis.tilePixelWidth > 0 ? `${analysis.tilePixelWidth}×${analysis.tilePixelHeight}` : "—"}</strong></span>
              <span><small>{pick("SAÍDA", "OUTPUT", "SALIDA", "AUSGABE", "SORTIE")}</small><strong>{analysis.outputPixelWidth > 0 ? `${analysis.outputPixelWidth}×${analysis.outputPixelHeight}` : "—"}</strong></span>
              <span><small>{pick("ESTIMATIVA", "ESTIMATE", "ESTIMACIÓN", "SCHÄTZUNG", "ESTIMATION")}</small><strong>{formatFileSize(analysis.estimatedBytes)}</strong></span>
              <span><small>{pick("BACKUP NECESSÁRIO", "BACKUP REQUIRED", "BACKUP NECESARIO", "BACKUP ERFORDERLICH", "SAUVEGARDE REQUISE")}</small><strong>{analysis.existingWholeRoadmap ? pick("Sim", "Yes", "Sí", "Ja", "Oui") : pick("Não", "No", "No", "Nein", "Non")}</strong></span>
            </div>
            <code className="roadmap-output-path">{analysis.outputPath}</code>
          </>
        )}
      </article>

      <article className="card roadmap-result-card">
        <div className="section-heading">
          <div><span className="eyebrow">{t("roadmap.lastBuild")}</span><h3>{t("roadmap.realResult")}</h3></div>
        </div>
        {!result ? (
          <div className="empty-state">{t("roadmap.noBuild")}</div>
        ) : (
          <>
            <div className="roadmap-analysis-grid">
              <span><small>{pick("MODO", "MODE", "MODO", "MODUS", "MODE")}</small><strong>{result.mode === "tiles" ? pick("Imagens de tile", "Tile images", "Imágenes de tile", "Tile-Bilder", "Images de tile") : pick("Vetorial / splines", "Vector / splines", "Vectorial / splines", "Vektor / Splines", "Vectoriel / splines")}</strong></span>
              <span><small>{pick("DIMENSÃO", "DIMENSIONS", "DIMENSIÓN", "ABMESSUNGEN", "DIMENSIONS")}</small><strong>{result.pixelWidth}×{result.pixelHeight}</strong></span>
              <span><small>{pick("TAMANHO", "SIZE", "TAMAÑO", "GRÖSSE", "TAILLE")}</small><strong>{formatFileSize(result.fileSizeBytes)}</strong></span>
              <span><small>{pick("TEMPO", "TIME", "TIEMPO", "ZEIT", "TEMPS")}</small><strong>{result.elapsedSeconds.toFixed(1)} s</strong></span>
              {result.tileImagesUsed != null && <span><small>{pick("TILES USADOS", "TILES USED", "TILES USADOS", "VERWENDETE TILES", "TILES UTILISÉS")}</small><strong>{result.tileImagesUsed}</strong></span>}
              {result.missingTileImages != null && <span><small>{pick("VAZIOS", "MISSING", "VACÍOS", "FEHLEND", "MANQUANTS")}</small><strong>{result.missingTileImages}</strong></span>}
              {result.tileFilesRead != null && <span><small>{pick("TILES LIDOS", "TILES READ", "TILES LEÍDOS", "GELESENE TILES", "TILES LUS")}</small><strong>{result.tileFilesRead}</strong></span>}
              {result.splinesDrawn != null && <span><small>SPLINES</small><strong>{result.splinesDrawn}</strong></span>}
            </div>
            <code className="roadmap-output-path">{result.outputPath}</code>
            {result.backupPath && <p className="roadmap-backup">{pick("Backup", "Backup", "Copia de seguridad", "Sicherung", "Sauvegarde")}: <code>{result.backupPath}</code></p>}
          </>
        )}
      </article>
    </section>
  );
}

function Settings({
  state,
  error,
  requestedTab
}: {
  state: NavBrState | null;
  error: string | null;
  requestedTab?: SettingsTab | null;
}) {
  const { t, pick } = useI18n();
  const system = state?.system;
  const network = state?.network;
  const [tab, setTab] = useState<SettingsTab>("installations");
  const [manualPath, setManualPath] = useState("");
  const networkRequested = useRef(false);

  useEffect(() => {
    if (requestedTab) {
      setTab(requestedTab);
    }
  }, [requestedTab]);

  useEffect(() => {
    if (tab === "network" && !network?.diagnostics && !networkRequested.current) {
      networkRequested.current = true;
      sendCommand("refreshNetworkDiagnostics");
    }
  }, [tab, network?.diagnostics]);

  if (!system) {
    return <div className="card empty-state">{t("common.waiting")}…</div>;
  }

  const logSize = system.diagnostics.logSizeBytes >= 1024 * 1024
    ? `${(system.diagnostics.logSizeBytes / (1024 * 1024)).toFixed(1)} MB`
    : `${Math.max(0, system.diagnostics.logSizeBytes / 1024).toFixed(1)} KB`;

  return (
    <>
      <header className="topbar settings-header">
        <div>
          <span className="eyebrow">{pick("SISTEMA NAVBR", "NAVBR SYSTEM", "SISTEMA NAVBR", "NAVBR-SYSTEM", "SYSTÈME NAVBR")}</span>
          <h1>{t("settings.title")}</h1>
          <p>{t("settings.subtitle")}</p>
        </div>
        <div className="top-actions">
          <span className={`connection-pill ${state?.omsi.running ? "connected" : ""}`}>
            <i /> {state?.omsi.running ? t("home.omsiDetected") : t("home.notDetected")}
          </span>
        </div>
      </header>

      {error && <div className="command-error">{error}</div>}

      <div className="mp-tabs settings-tabs" role="tablist">
        {([
          ["installations", t("settings.installations")],
          ["hud", t("settings.hud")],
          ["roadmap", t("settings.roadmap")],
          ["diagnostics", t("settings.diagnostics")],
          ["network", t("settings.network")],
          ["advanced", t("settings.advanced")]
        ] as [SettingsTab, string][]).map(([key, label]) => (
          <button key={key} className={tab === key ? "active" : ""} onClick={() => setTab(key)}>{label}</button>
        ))}
      </div>

      {tab === "installations" && (
        <section className="settings-installations">
          {system.installationsNotice && (
            <div className="network-message installations-notice">{system.installationsNotice}</div>
          )}
          <article className="card discovery-card">
            <div className="section-heading">
              <div><span className="eyebrow">{pick("PLUGIN NAVBR", "NAVBR PLUGIN", "PLUGIN NAVBR", "NAVBR-PLUGIN", "PLUGIN NAVBR")}</span><h3>{pick("Verificação automática na abertura", "Automatic startup check", "Verificación automática al iniciar", "Automatische Prüfung beim Start", "Vérification automatique au démarrage")}</h3></div>
              <span className={`compatibility-badge ${system.pluginInstallation.state === "installed" ? "compatible" : system.pluginInstallation.state === "untracked" ? "warning" : "blocked"}`}>
                {system.pluginInstallation.state.toUpperCase()}
              </span>
            </div>
            <p>{pick("Ao abrir o NavBR, o app localiza o OMSI e verifica os 3 arquivos necessários. Se estiverem ausentes ou desatualizados e for seguro substituir, o pacote oficial embutido é instalado automaticamente.", "When NavBR starts, it locates OMSI and checks the 3 required files. If they are missing or outdated and replacement is safe, the embedded official package is installed automatically.", "Al iniciar NavBR, la app localiza OMSI y verifica los 3 archivos necesarios. Si faltan o están desactualizados y es seguro reemplazarlos, instala automáticamente el paquete oficial integrado.", "Beim Start sucht NavBR OMSI und prüft die 3 erforderlichen Dateien. Fehlen sie oder sind sie veraltet und ein Austausch ist sicher, wird das eingebettete offizielle Paket automatisch installiert.", "Au démarrage, NavBR localise OMSI et vérifie les 3 fichiers requis. S'ils manquent ou sont obsolètes et que le remplacement est sûr, le paquet officiel intégré est installé automatiquement.")}</p>
            <div className="details-grid">
              <div><small>{pick("ARQUIVOS", "FILES", "ARCHIVOS", "DATEIEN", "FICHIERS")}</small><strong>{system.pluginInstallation.requiredFilesFound}/{system.pluginInstallation.requiredFilesTotal}</strong></div>
              <div><small>{pick("SHA-256 OK", "SHA-256 OK", "SHA-256 OK", "SHA-256 OK", "SHA-256 OK")}</small><strong>{system.pluginInstallation.verifiedFiles}/{system.pluginInstallation.requiredFilesTotal}</strong></div>
              <div><small>{pick("VERSÃO INSTALADA", "INSTALLED VERSION", "VERSIÓN INSTALADA", "INSTALLIERTE VERSION", "VERSION INSTALLÉE")}</small><strong>{system.pluginInstallation.installedVersion || "—"}</strong></div>
              <div><small>{pick("VERSÃO ESPERADA", "EXPECTED VERSION", "VERSIÓN ESPERADA", "ERWARTETE VERSION", "VERSION ATTENDUE")}</small><strong>{system.pluginInstallation.expectedVersion || "—"}</strong></div>
              <div><small>{pick("MANIFESTO", "MANIFEST", "MANIFIESTO", "MANIFEST", "MANIFESTE")}</small><strong>{system.pluginInstallation.manifestPresent ? "OK" : "—"}</strong></div>
              <div><small>{pick("PACOTE EMBUTIDO", "EMBEDDED PACKAGE", "PAQUETE INTEGRADO", "EINGEBETTETES PAKET", "PAQUET INTÉGRÉ")}</small><strong>{system.pluginInstallation.embeddedPackageAvailable ? "OK" : "—"}</strong></div>
              <div><small>{pick("ATUALIZAÇÃO", "UPDATE", "ACTUALIZACIÓN", "UPDATE", "MISE À JOUR")}</small><strong>{system.pluginInstallation.updateRequired ? pick("Necessária", "Required", "Necesaria", "Erforderlich", "Requise") : pick("Em dia", "Current", "Al día", "Aktuell", "À jour")}</strong></div>
              <div><small>OMSI</small><strong>{system.pluginInstallation.omsiRunning ? pick("Em execução", "Running", "En ejecución", "Läuft", "En cours") : pick("Fechado", "Closed", "Cerrado", "Geschlossen", "Fermé")}</strong></div>
            </div>
            <div className="plugin-file-verification">
              {system.pluginInstallation.files.map(file => (
                <span key={file.name} className={file.exists && file.hashMatches ? "verified" : "mismatch"}>
                  <NavBrIcon name={file.exists && file.hashMatches ? "info" : "hazard"} size={13} />
                  {file.name}
                </span>
              ))}
            </div>
            <div className="plugin-update-actions">
              <div className="discovery-actions">
                <button className="button ghost icon-button" disabled={!system.pluginInstallation.verificationAvailable} onClick={() => sendCommand("verifyOmsiPlugin")}>
                  <NavBrIcon name="plugin" size={16} />{pick("Verificar e atualizar agora", "Verify and update now", "Verificar y actualizar ahora", "Jetzt prüfen und aktualisieren", "Vérifier et mettre à jour")}
                </button>
              </div>
              {system.pluginInstallation.autoUpdatePending && (
                <small className="plugin-update-hint">{pick("Atualização pendente: será aplicada automaticamente quando o OMSI fechar.", "Update pending: it will be applied automatically when OMSI closes.", "Actualización pendiente: se aplicará automáticamente al cerrar OMSI.", "Update ausstehend: es wird automatisch nach dem Schließen von OMSI angewendet.", "Mise à jour en attente : elle sera appliquée automatiquement à la fermeture d’OMSI.")}</small>
              )}
              {!system.pluginInstallation.installAvailable && system.pluginInstallation.installBlockReason && (
                  <small className="plugin-update-hint">
                    {system.pluginInstallation.installBlockReason === "omsi-running"
                      ? pick("Feche o OMSI para liberar a atualização do plugin.", "Close OMSI to enable the plugin update.", "Cierra OMSI para habilitar la actualización del plugin.", "OMSI schließen, um das Plugin-Update freizugeben.", "Fermez OMSI pour autoriser la mise à jour du plugin.")
                      : system.pluginInstallation.installBlockReason === "omsi-not-found"
                        ? pick("Cadastre a pasta do OMSI, o Omsi.exe ou um atalho .lnk/.url válido.", "Register the OMSI folder, Omsi.exe, or a valid .lnk/.url shortcut.", "Registra la carpeta de OMSI, Omsi.exe o un acceso directo .lnk/.url válido.", "OMSI-Ordner, Omsi.exe oder eine gültige .lnk/.url-Verknüpfung hinterlegen.", "Enregistrez le dossier OMSI, Omsi.exe ou un raccourci .lnk/.url valide.")
                        : pick("Esta build não contém o pacote embutido do plugin.", "This build does not contain the embedded plugin package.", "Esta build no contiene el paquete integrado del plugin.", "Dieser Build enthält das eingebettete Plugin-Paket nicht.", "Cette build ne contient pas le paquet intégré du plugin.")}
                  </small>
                )}
              </div>
          </article>

          <article className="card discovery-card">
            <div className="section-heading">
              <div>
                <span className="eyebrow">MOBILE COMPANION</span>
                <h3>{pick("Celular como GPS / IBIS", "Phone as GPS / IBIS", "Móvil como GPS / IBIS", "Smartphone als GPS / IBIS", "Téléphone comme GPS / IBIS")}</h3>
              </div>
              <span className={`compatibility-badge ${system.mobileCompanion.running ? "compatible" : "blocked"}`}>
                {system.mobileCompanion.running ? "ONLINE" : "OFFLINE"}
              </span>
            </div>
            <p>{pick("Abra um dos endereços abaixo no celular conectado à mesma rede do PC. O código muda a cada abertura do NavBR.", "Open one of the addresses below on a phone connected to the same network as the PC. The code changes each time NavBR starts.", "Abre una de las direcciones en un móvil conectado a la misma red del PC. El código cambia cada vez que inicia NavBR.", "Öffne eine der Adressen auf einem Smartphone im selben Netzwerk wie der PC. Der Code ändert sich bei jedem NavBR-Start.", "Ouvrez l'une des adresses ci-dessous sur un téléphone connecté au même réseau que le PC. Le code change à chaque démarrage de NavBR.")}</p>
            <div className="details-grid">
              <div><small>{pick("PORTA", "PORT", "PUERTO", "PORT", "PORT")}</small><strong>{system.mobileCompanion.port}</strong></div>
              <div><small>{pick("CÓDIGO DE PAREAMENTO", "PAIRING CODE", "CÓDIGO DE EMPAREJAMIENTO", "KOPPLUNGSCODE", "CODE D’APPAIRAGE")}</small><strong>{system.mobileCompanion.pairingCode || "—"}</strong></div>
            </div>
            <div className="plugin-file-verification">
              {system.mobileCompanion.urls.length === 0
                ? <span className="mismatch">{pick("Nenhum endereço LAN disponível", "No LAN address available", "Sin dirección LAN disponible", "Keine LAN-Adresse verfügbar", "Aucune adresse LAN disponible")}</span>
                : system.mobileCompanion.urls.map(url => (
                    <span key={url} className="verified" onClick={() => void navigator.clipboard?.writeText(url)} title={pick("Clique para copiar", "Click to copy", "Haz clic para copiar", "Zum Kopieren klicken", "Cliquer pour copier")}>
                      <NavBrIcon name="network" size={13} />{url}
                    </span>
                  ))}
            </div>
            <label className="privacy-toggle">
              <input
                type="checkbox"
                checked={system.mobileCompanion.vehicleControlsEnabled}
                onChange={event => sendCommand("setMobileVehicleControlsEnabled", { enabled: event.target.checked })}
              />
              <span>{pick("Ativar controles do ônibus pelo celular (EXPERIMENTAL)", "Enable bus controls from phone (EXPERIMENTAL)", "Activar controles del autobús desde el móvil (EXPERIMENTAL)", "Bussteuerung über Smartphone aktivieren (EXPERIMENTELL)", "Activer les commandes du bus depuis le téléphone (EXPÉRIMENTAL)")}</span>
            </label>
            <small className="plugin-update-hint">
              {system.mobileCompanion.vehicleControlsEnabled
                ? system.mobileCompanion.vehicleControlsAvailable
                  ? pick("Controles locais autorizados. O APK ainda revalida cada evento contra o catálogo real do ônibus.", "Local controls authorized. The APK still revalidates every event against the real bus catalog.", "Controles locales autorizados. El APK aún revalida cada evento contra el catálogo real del autobús.", "Lokale Steuerung freigegeben. Die APK validiert jedes Ereignis weiterhin gegen den echten Bus-Katalog.", "Commandes locales autorisées. L’APK revalide chaque événement avec le catalogue réel du bus.")
                  : pick("Autorizado, mas aguardando a capacidade local-vehicle-trigger do Plugin Bridge.", "Authorized, but waiting for the Plugin Bridge local-vehicle-trigger capability.", "Autorizado, pero esperando la capacidad local-vehicle-trigger del Plugin Bridge.", "Freigegeben, wartet aber auf die local-vehicle-trigger-Fähigkeit des Plugin Bridge.", "Autorisé, mais en attente de la capacité local-vehicle-trigger du Plugin Bridge.")
                : pick("Desligado por padrão. GPS, painel, multiplayer e voz continuam somente leitura/controle seguro.", "Off by default. GPS, dashboard, multiplayer and voice remain available safely.", "Desactivado por defecto. GPS, panel, multijugador y voz siguen disponibles de forma segura.", "Standardmäßig deaktiviert. GPS, Dashboard, Multiplayer und Sprache bleiben sicher verfügbar.", "Désactivé par défaut. GPS, tableau de bord, multijoueur et voix restent disponibles en toute sécurité.")}
            </small>
          </article>

          <article className="card discovery-card">
            <div className="section-heading">
              <div><span className="eyebrow">{pick("DESCOBERTA", "DISCOVERY", "DESCUBRIMIENTO", "ERKENNUNG", "DÉTECTION")}</span><h3>{pick("Encontrar OMSI 2", "Find OMSI 2", "Encontrar OMSI 2", "OMSI 2 finden", "Trouver OMSI 2")}</h3></div>
              <div className="settings-action-row">
                <button className="button ghost" onClick={() => sendCommand("selectOmsiExecutable")}>{pick("Selecionar Omsi.exe / atalho", "Select Omsi.exe / shortcut", "Seleccionar Omsi.exe / acceso directo", "Omsi.exe / Verknüpfung wählen", "Sélectionner Omsi.exe / raccourci")}</button>
                <button className="button ghost" onClick={() => sendCommand("selectOmsiFolder")}>{pick("Selecionar pasta", "Select folder", "Seleccionar carpeta", "Ordner auswählen", "Sélectionner le dossier")}</button>
              </div>
            </div>
            <p>{pick("O NavBR pode localizar instalações registradas, bibliotecas Steam e também aceitar uma pasta, o próprio Omsi.exe ou atalhos .lnk/.url.", "NavBR can locate registered installations and Steam libraries, and can also accept a folder, Omsi.exe itself, or .lnk/.url shortcuts.", "NavBR puede localizar instalaciones registradas y bibliotecas Steam, y también aceptar una carpeta, el propio Omsi.exe o accesos directos .lnk/.url.", "NavBR kann registrierte Installationen und Steam-Bibliotheken finden und auch einen Ordner, Omsi.exe selbst oder .lnk/.url-Verknüpfungen akzeptieren.", "NavBR peut localiser les installations enregistrées et les bibliothèques Steam, et accepter aussi un dossier, Omsi.exe lui-même ou des raccourcis .lnk/.url.")}</p>
            <div className="discovery-actions">
              <input
                value={manualPath}
                onChange={event => setManualPath(event.target.value)}
                placeholder="Ex.: G:\Games\OMSI 2 ou Desktop\OMSI 2.lnk/.url"
              />
              <button className="button primary" onClick={() => sendCommand("discoverOmsiProfiles", { path: manualPath })}>
                {manualPath.trim() ? pick("Adicionar / descobrir", "Add / discover", "Añadir / descubrir", "Hinzufügen / erkennen", "Ajouter / détecter") : pick("Descobrir automaticamente", "Discover automatically", "Descubrir automáticamente", "Automatisch erkennen", "Détecter automatiquement")}
              </button>
            </div>
          </article>

          <div className="installation-list">
            {system.installations.length === 0 ? (
              <div className="card empty-state">{pick("Nenhum perfil OMSI cadastrado. Use a descoberta acima para localizar uma instalação real.", "No OMSI profile registered. Use discovery above to locate a real installation.", "Ningún perfil OMSI registrado. Usa el descubrimiento para localizar una instalación real.", "Kein OMSI-Profil registriert. Nutze die Erkennung oben, um eine echte Installation zu finden.", "Aucun profil OMSI enregistré. Utilisez la détection ci-dessus pour trouver une installation réelle.")}</div>
            ) : system.installations.map(profile => (
              <OmsiProfileCard key={profile.id} profile={profile} />
            ))}
          </div>
        </section>
      )}

      {tab === "hud" && <HudSettingsPanel hud={system.hud} />}

      {tab === "roadmap" && <RoadmapStudioPanel roadmap={state!.roadmapStudio} />}

      {tab === "diagnostics" && (
        <section className="diagnostics-layout">
          <article className="card diagnostics-consent-card">
            <span className="eyebrow">{pick("PRIVACIDADE", "PRIVACY", "PRIVACIDAD", "DATENSCHUTZ", "CONFIDENTIALITÉ")}</span>
            <h3>{pick("Diagnóstico remoto", "Remote diagnostics", "Diagnóstico remoto", "Remote-Diagnose", "Diagnostic distant")}</h3>
            <p>
              {pick("O envio é opt-in. Quando desativado, o NavBR não registra eventos para envio remoto e limpa a fila local de diagnóstico.", "Sending is opt-in. When disabled, NavBR does not queue remote diagnostic events and clears the local diagnostic queue.", "El envío es opcional. Cuando está desactivado, NavBR no registra eventos para envío remoto y limpia la cola local.", "Das Senden ist Opt-in. Wenn deaktiviert, sammelt NavBR keine Remote-Diagnoseereignisse und leert die lokale Warteschlange.", "L’envoi est volontaire. Lorsqu’il est désactivé, NavBR n’enregistre pas d’événements de diagnostic distant et vide la file locale.")}
            </p>
            <label className="diagnostics-toggle">
              <input
                type="checkbox"
                checked={system.diagnostics.enabled}
                onChange={event => sendCommand("setDiagnosticsEnabled", { enabled: event.target.checked })}
              />
              <span>{system.diagnostics.enabled ? pick("Diagnóstico remoto ativado", "Remote diagnostics enabled", "Diagnóstico remoto activado", "Remote-Diagnose aktiviert", "Diagnostic distant activé") : pick("Diagnóstico remoto desativado", "Remote diagnostics disabled", "Diagnóstico remoto desactivado", "Remote-Diagnose deaktiviert", "Diagnostic distant désactivé")}</span>
            </label>
            <div className="diagnostics-actions">
              <button className="button ghost" disabled={!system.diagnostics.enabled} onClick={() => sendCommand("flushDiagnostics")}>
                {pick("Tentar enviar fila agora", "Try sending queue now", "Intentar enviar la cola ahora", "Warteschlange jetzt senden", "Tenter d’envoyer la file maintenant")}
              </button>
              <button className="button ghost danger" onClick={() => sendCommand("purgeDiagnostics")}>
                {pick("Limpar fila de diagnóstico", "Clear diagnostic queue", "Limpiar cola de diagnóstico", "Diagnosewarteschlange leeren", "Vider la file de diagnostic")}
              </button>
            </div>
          </article>

          <article className="card diagnostics-log-card">
            <div className="section-heading">
              <div>
                <span className="eyebrow">{pick("SAÚDE DA SESSÃO", "SESSION HEALTH", "SALUD DE LA SESIÓN", "SITZUNGSSTATUS", "SANTÉ DE SESSION")}</span>
                <h3>{pick("OMSI, multiplayer, rede e plugin", "OMSI, multiplayer, network and plugin", "OMSI, multijugador, red y plugin", "OMSI, Multiplayer, Netzwerk und Plugin", "OMSI, multijoueur, réseau et plugin")}</h3>
              </div>
              <button className="button ghost" onClick={() => sendCommand("exportSessionHealth")}>
                {pick("Exportar relatório sanitizado", "Export sanitized report", "Exportar informe sanitizado", "Bereinigten Bericht exportieren", "Exporter le rapport assaini")}
              </button>
            </div>
            {system.sessionHealthNotice && <div className="network-message">{system.sessionHealthNotice}</div>}
            <div className="diagnostic-facts">
              <span><small>OMSI</small><strong>{system.sessionHealth.omsiActive ? pick("Ativo", "Active", "Activo", "Aktiv", "Actif") : pick("Aguardando", "Waiting", "Esperando", "Wartet", "En attente")}</strong></span>
              <span><small>MULTIPLAYER</small><strong>{system.sessionHealth.multiplayerConnected ? pick("Conectado", "Connected", "Conectado", "Verbunden", "Connecté") : pick("Desconectado", "Disconnected", "Desconectado", "Getrennt", "Déconnecté")}</strong></span>
              <span><small>BRIDGE / PLUGIN</small><strong>{system.sessionHealth.pluginConnected ? `${pick("Conectado", "Connected", "Conectado", "Verbunden", "Connecté")}${system.sessionHealth.pluginVersion ? ` · ${system.sessionHealth.pluginVersion}` : ""}` : pick("Offline opcional", "Optional offline", "Offline opcional", "Optional offline", "Hors ligne optionnel")}</strong></span>
              <span><small>{pick("MOTORISTAS REMOTOS", "REMOTE DRIVERS", "CONDUCTORES REMOTOS", "REMOTE-FAHRER", "CONDUCTEURS DISTANTS")}</small><strong>{system.sessionHealth.multiplayerConnected ? system.sessionHealth.remoteDrivers : "—"}</strong></span>
              <span><small>{pick("TELEMETRIA REMOTA", "REMOTE TELEMETRY", "TELEMETRÍA REMOTA", "REMOTE-TELEMETRIE", "TÉLÉMÉTRIE DISTANTE")}</small><strong>{system.sessionHealth.remoteTelemetryAgeSeconds == null ? "—" : system.sessionHealth.remoteTelemetryAgeSeconds < 1 ? pick("Agora", "Now", "Ahora", "Jetzt", "Maintenant") : `${format(system.sessionHealth.remoteTelemetryAgeSeconds, 0)} s`}</strong></span>
              <span><small>{pick("LATÊNCIA", "LATENCY", "LATENCIA", "LATENZ", "LATENCE")}</small><strong>{system.sessionHealth.latencyMs == null ? "—" : `${format(system.sessionHealth.latencyMs, 0)} ms`}</strong></span>
              <span><small>JITTER</small><strong>{system.sessionHealth.jitterMs == null ? "—" : `${format(system.sessionHealth.jitterMs, 0)} ms`}</strong></span>
              <span><small>{pick("PERDA EST.", "EST. LOSS", "PÉRDIDA EST.", "GESCH. VERLUST", "PERTE EST.")}</small><strong>{system.sessionHealth.lossPercent == null ? "—" : `${format(system.sessionHealth.lossPercent, 1)}%`}</strong></span>
              <span><small>{pick("TAXA DE TELEMETRIA", "TELEMETRY RATE", "TASA DE TELEMETRÍA", "TELEMETRIE-RATE", "TAUX TÉLÉMÉTRIE")}</small><strong>{system.sessionHealth.telemetryRateHz == null ? "—" : `~${format(system.sessionHealth.telemetryRateHz, 1)} Hz`}</strong></span>
            </div>
            <p>{pick("Os valores de rede vêm de sondas reais ao mesmo peer-host NavBR. A frequência de telemetria se adapta automaticamente à qualidade da conexão.", "Network values come from real probes to the same NavBR peer-host. Telemetry frequency adapts automatically to connection quality.", "Los valores de red vienen de sondas reales al mismo peer-host NavBR. La frecuencia de telemetría se adapta automáticamente.", "Netzwerkwerte stammen aus echten Messungen zum selben NavBR-Peer-Host. Die Telemetrierate passt sich automatisch an.", "Les valeurs réseau viennent de sondes réelles vers le même peer-host NavBR. La fréquence de télémétrie s’adapte automatiquement.")}</p>
          </article>
          <article className="card diagnostics-log-card">
            <span className="eyebrow">{pick("LOG LOCAL", "LOCAL LOG", "LOG LOCAL", "LOKALES LOG", "JOURNAL LOCAL")}</span>
            <h3>navbr.log</h3>
            <div className="diagnostic-facts">
              <span><small>{pick("ARQUIVO", "FILE", "ARCHIVO", "DATEI", "FICHIER")}</small><strong>{system.diagnostics.logExists ? pick("Disponível", "Available", "Disponible", "Verfügbar", "Disponible") : pick("Ainda não criado", "Not created yet", "Aún no creado", "Noch nicht erstellt", "Pas encore créé")}</strong></span>
              <span><small>{pick("TAMANHO", "SIZE", "TAMAÑO", "GRÖSSE", "TAILLE")}</small><strong>{system.diagnostics.logExists ? logSize : "—"}</strong></span>
              <span><small>{pick("ATUALIZAÇÃO", "UPDATED", "ACTUALIZACIÓN", "AKTUALISIERT", "MISE À JOUR")}</small><strong>{system.diagnostics.logUpdatedAtUtc ? new Date(system.diagnostics.logUpdatedAtUtc).toLocaleString() : "—"}</strong></span>
            </div>
            <code>{system.diagnostics.logPath}</code>
            <p>{pick("O log local continua existindo independentemente do consentimento de diagnóstico remoto e é usado para suporte técnico local.", "The local log remains available regardless of remote diagnostics consent and is used for local technical support.", "El log local sigue existiendo independientemente del consentimiento de diagnóstico remoto y se usa para soporte técnico local.", "Das lokale Log bleibt unabhängig von der Remote-Diagnoseeinwilligung bestehen und dient dem lokalen Support.", "Le journal local reste disponible indépendamment du consentement au diagnostic distant et sert au support technique local.")}</p>
          </article>
        </section>
      )}

      {tab === "network" && (
        <section className="network-layout">
          <article className="card network-overview-card">
            <div className="section-heading">
              <div><span className="eyebrow">{pick("CONECTIVIDADE", "CONNECTIVITY", "CONECTIVIDAD", "KONNEKTIVITÄT", "CONNECTIVITÉ")}</span><h3>TCP {network?.hostPort ?? 27730}</h3></div>
              <button className="button ghost" onClick={() => sendCommand("refreshNetworkDiagnostics")}>{pick("Atualizar diagnóstico", "Refresh diagnostics", "Actualizar diagnóstico", "Diagnose aktualisieren", "Actualiser le diagnostic")}</button>
            </div>

            {network?.message && <div className="network-message">{network.message}</div>}
            {network?.error && <div className="directory-error">{network.error}</div>}

            <div className="network-status-grid">
              <div>
                <small>{pick("FIREWALL WINDOWS", "WINDOWS FIREWALL", "FIREWALL WINDOWS", "WINDOWS-FIREWALL", "PARE-FEU WINDOWS")}</small>
                <strong className={network?.diagnostics?.firewallRulePresent ? "ok" : "warn"}>
                  {network?.diagnostics == null ? pick("Não verificado", "Not checked", "No verificado", "Nicht geprüft", "Non vérifié") : network.diagnostics.firewallRulePresent ? pick("Regra confirmada", "Rule confirmed", "Regla confirmada", "Regel bestätigt", "Règle confirmée") : pick("Regra ausente", "Rule missing", "Regla ausente", "Regel fehlt", "Règle absente")}
                </strong>
                <span>{pick("Entrada TCP", "TCP inbound", "Entrada TCP", "TCP-Eingang", "Entrée TCP")} {network?.hostPort ?? 27730} {pick("em todos os perfis de rede.", "on all network profiles.", "en todos los perfiles de red.", "für alle Netzwerkprofile.", "sur tous les profils réseau.")}</span>
              </div>
              <div>
                <small>{pick("PORTA LOCAL", "LOCAL PORT", "PUERTO LOCAL", "LOKALER PORT", "PORT LOCAL")}</small>
                <strong className={network?.diagnostics?.localPortListening ? "ok" : ""}>
                  {network?.diagnostics == null ? pick("Não verificado", "Not checked", "No verificado", "Nicht geprüft", "Non vérifié") : network.diagnostics.localPortListening ? pick("Ouvindo", "Listening", "Escuchando", "Lauscht", "À l’écoute") : pick("Sem listener", "No listener", "Sin listener", "Kein Listener", "Aucun listener")}
                </strong>
                <span>{network?.hostRunning ? pick("Host NavBR ativo.", "NavBR host active.", "Host NavBR activo.", "NavBR-Host aktiv.", "Hôte NavBR actif.") : pick("Nenhuma sala local hospedada agora.", "No local room hosted now.", "Ninguna sala local alojada ahora.", "Aktuell kein lokaler Raum gehostet.", "Aucune salle locale hébergée actuellement.")}</span>
              </div>
              <div>
                <small>UPNP</small>
                <strong className={network?.diagnostics?.upnpGatewayFound ? "ok" : ""}>
                  {network?.diagnostics == null ? pick("Não verificado", "Not checked", "No verificado", "Nicht geprüft", "Non vérifié") : network.diagnostics.upnpGatewayFound ? pick("Gateway encontrado", "Gateway found", "Gateway encontrado", "Gateway gefunden", "Passerelle trouvée") : pick("Gateway não encontrado", "Gateway not found", "Gateway no encontrado", "Gateway nicht gefunden", "Passerelle introuvable")}
                </strong>
                <span>{network?.automaticUpnpEnabled ? pick("Automático habilitado.", "Automatic enabled.", "Automático habilitado.", "Automatisch aktiviert.", "Automatique activé.") : pick("Automático desabilitado.", "Automatic disabled.", "Automático deshabilitado.", "Automatisch deaktiviert.", "Automatique désactivé.")}</span>
              </div>
              <div>
                <small>{pick("AMBIENTE WAN", "WAN ENVIRONMENT", "ENTORNO WAN", "WAN-UMGEBUNG", "ENVIRONNEMENT WAN")}</small>
                <strong>{network?.diagnostics?.environmentKind || "—"}</strong>
                <span>{network?.diagnostics?.gatewayExternalAddress || pick("IP externo não informado", "External IP not provided", "IP externa no informada", "Externe IP nicht angegeben", "IP externe non renseignée")}</span>
              </div>
            </div>

            <div className="network-actions">
              <button className="button primary" onClick={() => sendCommand("applyFirewallRule")}>
                {pick("Aplicar / corrigir Firewall TCP", "Apply / fix TCP Firewall", "Aplicar / corregir Firewall TCP", "TCP-Firewall anwenden / korrigieren", "Appliquer / corriger le pare-feu TCP")} {network?.hostPort ?? 27730}
              </button>
              <span>{network?.runningAsAdministrator ? pick("NavBR já está elevado.", "NavBR is already elevated.", "NavBR ya está elevado.", "NavBR läuft bereits erhöht.", "NavBR est déjà élevé.") : pick("O Windows solicitará permissão de administrador.", "Windows will request administrator permission.", "Windows solicitará permiso de administrador.", "Windows fordert Administratorrechte an.", "Windows demandera l’autorisation administrateur.")}</span>
            </div>

            {network?.diagnostics && (
              <>
                <div className="network-addresses">
                  <small>{pick("IPv4 LOCAL", "LOCAL IPv4", "IPv4 LOCAL", "LOKALES IPv4", "IPv4 LOCAL")}</small>
                  <div>
                    {network.diagnostics.localIpv4Addresses.length === 0
                      ? <code>—</code>
                      : network.diagnostics.localIpv4Addresses.map(address => <code key={address}>{address}</code>)}
                  </div>
                </div>
                <p className="network-note">{network.diagnostics.technicalNote}</p>
              </>
            )}
          </article>

          <article className="card network-upnp-card">
            <span className="eyebrow">{pick("ROTEADOR", "ROUTER", "ROUTER", "ROUTER", "ROUTEUR")}</span>
            <h3>NAT / UPnP</h3>
            <label className="diagnostics-toggle">
              <input
                type="checkbox"
                checked={network?.automaticUpnpEnabled ?? false}
                disabled={network?.hostRunning}
                onChange={event => sendCommand("setAutomaticUpnp", { enabled: event.target.checked })}
              />
              <span>{pick("Tentar mapear TCP automaticamente ao hospedar", "Automatically map TCP while hosting", "Mapear TCP automáticamente al alojar", "TCP beim Hosten automatisch mappen", "Mapper TCP automatiquement pendant l’hébergement")} {network?.hostPort ?? 27730}</span>
            </label>
            {network?.hostRunning && <p className="network-note">{pick("Pare a sala hospedada antes de alterar o UPnP.", "Stop the hosted room before changing UPnP.", "Detén la sala alojada antes de cambiar UPnP.", "Gehosteten Raum vor Änderung von UPnP stoppen.", "Arrêtez la salle hébergée avant de modifier UPnP.")}</p>}
            <div className="diagnostic-facts">
              <span><small>{pick("GATEWAY LOCAL", "LOCAL GATEWAY", "GATEWAY LOCAL", "LOKALES GATEWAY", "PASSERELLE LOCALE")}</small><strong>{network?.diagnostics?.gatewayLocalAddress || "—"}</strong></span>
              <span><small>{pick("IP EXTERNO", "EXTERNAL IP", "IP EXTERNA", "EXTERNE IP", "IP EXTERNE")}</small><strong>{network?.diagnostics?.gatewayExternalAddress || "—"}</strong></span>
              <span><small>{pick("TIPO", "TYPE", "TIPO", "TYP", "TYPE")}</small><strong>{network?.diagnostics?.environmentKind || "—"}</strong></span>
            </div>
          </article>

          <article className="card network-probe-card">
            <span className="eyebrow">{pick("TESTE EXTERNO", "EXTERNAL TEST", "PRUEBA EXTERNA", "EXTERNER TEST", "TEST EXTERNE")}</span>
            <h3>{pick("Alcance pela Internet", "Internet reachability", "Alcance por Internet", "Internet-Erreichbarkeit", "Accessibilité Internet")}</h3>
            <p>{network?.externalProbeConfigured
              ? pick("O teste externo é separado do Firewall e do UPnP e confirma a porta a partir de fora da sua rede.", "The external test is separate from Firewall and UPnP and confirms the port from outside your network.", "La prueba externa es independiente del Firewall y UPnP y confirma el puerto desde fuera de tu red.", "Der externe Test ist von Firewall und UPnP getrennt und bestätigt den Port von außerhalb deines Netzes.", "Le test externe est séparé du pare-feu et d’UPnP et confirme le port depuis l’extérieur de votre réseau.")
              : pick("Teste externo não configurado nesta instalação. Isso não impede sala local/LAN nem UPnP; apenas impede a confirmação automática a partir da Internet.", "External testing is not configured in this installation. This does not prevent local/LAN rooms or UPnP; it only prevents automatic confirmation from the Internet.", "La prueba externa no está configurada en esta instalación. Esto no impide salas locales/LAN ni UPnP; solo impide la confirmación automática desde Internet.", "Externe Prüfung ist in dieser Installation nicht konfiguriert. Lokale/LAN-Räume und UPnP funktionieren weiterhin; nur die automatische Bestätigung aus dem Internet fehlt.", "Le test externe n’est pas configuré dans cette installation. Cela n’empêche pas les salles locales/LAN ni UPnP ; seule la confirmation automatique depuis Internet est indisponible.")}</p>
            <button
              className="button ghost"
              disabled={!network?.externalProbeConfigured}
              onClick={() => sendCommand("runExternalPortProbe")}
            >
              {network?.externalProbeConfigured
                ? pick("Testar TCP 27730 externamente", "Test TCP 27730 externally", "Probar TCP 27730 externamente", "TCP 27730 extern testen", "Tester TCP 27730 depuis l’extérieur")
                : pick("Teste externo indisponível", "External test unavailable", "Prueba externa no disponible", "Externer Test nicht verfügbar", "Test externe indisponible")}
            </button>
            {network?.externalProbe && (
              <div className={`external-probe-result ${network.externalProbe.reachable ? "reachable" : "blocked"}`}>
                <strong>{network.externalProbe.reachable ? pick("Porta alcançável", "Port reachable", "Puerto accesible", "Port erreichbar", "Port accessible") : pick("Porta não alcançável", "Port not reachable", "Puerto no accesible", "Port nicht erreichbar", "Port inaccessible")}</strong>
                <span>{network.externalProbe.status} · {network.externalProbe.durationMilliseconds} ms</span>
                <small>{new Date(network.externalProbe.checkedAtUtc).toLocaleString()}</small>
              </div>
            )}
          </article>
        </section>
      )}

      {tab === "advanced" && (
        <section className="advanced-grid settings-advanced">
          <article className="card compact-card">
            <span className="eyebrow">{pick("PREFERÊNCIAS", "PREFERENCES", "PREFERENCIAS", "EINSTELLUNGEN", "PRÉFÉRENCES")}</span>
            <h3>{pick("Comportamento geral", "General behavior", "Comportamiento general", "Allgemeines Verhalten", "Comportement général")}</h3>
            <label className="diagnostics-toggle">
              <input
                type="checkbox"
                checked={system.legacyPreferences.advancedModeEnabled}
                onChange={event => sendCommand("saveLegacyPreferences", {
                  advancedModeEnabled: event.target.checked,
                  showDrivingTips: system.legacyPreferences.showDrivingTips
                })}
              />
              <span>{pick("Modo avançado", "Advanced mode", "Modo avanzado", "Erweiterter Modus", "Mode avancé")}</span>
            </label>
            <label className="diagnostics-toggle">
              <input
                type="checkbox"
                checked={system.legacyPreferences.showDrivingTips}
                onChange={event => sendCommand("saveLegacyPreferences", {
                  advancedModeEnabled: system.legacyPreferences.advancedModeEnabled,
                  showDrivingTips: event.target.checked
                })}
              />
              <span>{pick("Dicas de direção", "Driving tips", "Consejos de conducción", "Fahrtipps", "Conseils de conduite")}</span>
            </label>
            <p>{pick("Essas preferências são salvas automaticamente para os próximos usos.", "These preferences are saved automatically for future sessions.", "Estas preferencias se guardan automáticamente para próximos usos.", "Diese Einstellungen werden automatisch für kommende Sitzungen gespeichert.", "Ces préférences sont enregistrées automatiquement pour les prochaines utilisations.")}</p>
          </article>

          <article className="card compact-card">
            <span className="eyebrow">MULTIPLAYER</span>
            <h3>{pick("Rede e conectividade", "Network and connectivity", "Red y conectividad", "Netzwerk und Konnektivität", "Réseau et connectivité")}</h3>
            <p>{pick("Ajuste conexão, atalhos, voz e ônibus físicos na área de Rede.", "Configure connection, shortcuts, voice and physical buses in Network.", "Configura conexión, atajos, voz y autobuses físicos en Red.", "Verbindung, Tastenkürzel, Sprache und physische Busse findest du unter Netzwerk.", "Réglez la connexion, les raccourcis, la voix et les bus physiques dans Réseau.")}</p>
            <button className="button ghost" onClick={() => setTab("network")}>{pick("Abrir Rede", "Open Network", "Abrir Red", "Netzwerk öffnen", "Ouvrir Réseau")}</button>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">HUD</span>
            <h3>{pick("Personalização", "Customization", "Personalización", "Anpassung", "Personnalisation")}</h3>
            <p>{pick("Presets, tema, escala, opacidade e módulos já estão disponíveis na aba HUD.", "Presets, theme, scale, opacity and modules are available in the HUD tab.", "Presets, tema, escala, opacidad y módulos están disponibles en la pestaña HUD.", "Presets, Thema, Skalierung, Deckkraft und Module sind im HUD-Tab verfügbar.", "Préréglages, thème, échelle, opacité et modules sont disponibles dans l’onglet HUD.")}</p>
            <div className="settings-action-row">
              <button className="button ghost" onClick={() => setTab("hud")}>{pick("Abrir HUD", "Open HUD", "Abrir HUD", "HUD öffnen", "Ouvrir HUD")}</button>
              <button className="button ghost" onClick={() => sendCommand("toggleHudLayout")}>{pick("Mover HUD", "Move HUD", "Mover HUD", "HUD verschieben", "Déplacer le HUD")}</button>
            </div>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">ROADMAP</span>
            <h3>Roadmap Studio</h3>
            <p>{pick("Analise mapas, monte o roadmap e gere a visão vetorial das ruas em um só lugar.", "Analyze maps, assemble the roadmap and generate the vector road view in one place.", "Analiza mapas, monta el roadmap y genera la vista vectorial de las calles en un solo lugar.", "Analysiere Karten, erstelle die Roadmap und erzeuge die Vektoransicht der Straßen an einem Ort.", "Analysez les cartes, assemblez la roadmap et générez la vue vectorielle des routes au même endroit.")}</p>
            <button className="button ghost" onClick={() => setTab("roadmap")}>{pick("Abrir Roadmap Studio", "Open Roadmap Studio", "Abrir Roadmap Studio", "Roadmap Studio öffnen", "Ouvrir Roadmap Studio")}</button>
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
  const [tab, setTab] = useState<MultiplayerTab>("overview");
  const [chatText, setChatText] = useState("");
  const [serverUrl, setServerUrl] = useState("");
  const [roomId, setRoomId] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [privateRoom, setPrivateRoom] = useState(false);
  const [roomPassword, setRoomPassword] = useState("");
  const [roomSearch, setRoomSearch] = useState("");
  const [voiceChannel, setVoiceChannel] = useState("general");
  const [voiceRadius, setVoiceRadius] = useState(120);
  const [voiceDeafened, setVoiceDeafened] = useState(false);
  const [relayEnabled, setRelayEnabled] = useState(false);
  const [relayServerUrl, setRelayServerUrl] = useState("");
  const [chatHotkey, setChatHotkey] = useState("F9");
  const [voiceHotkey, setVoiceHotkey] = useState("F10");
  const [inviteNotice, setInviteNotice] = useState<string | null>(null);
  const [roomIntent, setRoomIntent] = useState<"create" | "join">("create");
  const [createRoomMode, setCreateRoomMode] = useState<"navbr" | "lan" | "host">("navbr");
  const defaultOnlineServer = "https://omsi-navbr-multiplayer-server.onrender.com";

  useEffect(() => {
    setServerUrl(current => current || multiplayer.serverUrl || "");
    setRoomId(current => current || multiplayer.roomId || "");
    setDisplayName(current => current || multiplayer.displayName || "");
  }, [multiplayer.serverUrl, multiplayer.roomId, multiplayer.displayName]);

  useEffect(() => {
    setVoiceChannel(multiplayer.voiceChannel || "general");
    setVoiceRadius(multiplayer.voiceProximityMeters || 120);
    setVoiceDeafened(multiplayer.voiceDeafened);
  }, [multiplayer.voiceChannel, multiplayer.voiceProximityMeters, multiplayer.voiceDeafened]);

  useEffect(() => {
    setRelayEnabled(multiplayer.relayEnabled);
    setRelayServerUrl(multiplayer.relayServerUrl || "");
    setChatHotkey(multiplayer.chatHotkey || "F9");
    setVoiceHotkey(multiplayer.voiceHotkey || "F10");
  }, [
    multiplayer.relayEnabled,
    multiplayer.relayServerUrl,
    multiplayer.chatHotkey,
    multiplayer.voiceHotkey
  ]);

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
        <div className="metric"><small>{multiplayer.transportMode === "dedicated-server" ? pick("SERVIDOR", "SERVER", "SERVIDOR", "SERVER", "SERVEUR") : "HOST"}</small><strong>{
          multiplayer.transportMode === "dedicated-server"
            ? pick("NavBR no Render", "NavBR on Render", "NavBR en Render", "NavBR auf Render", "NavBR sur Render")
            : multiplayer.hostRunning
              ? `TCP ${multiplayer.hostPort ?? 27730}`
              : pick("Não sou o host", "Not the host", "No soy el host", "Nicht der Host", "Pas l’hôte")
        }</strong></div>
        <div className="metric"><small>{pick("TRANSPORTE", "TRANSPORT", "TRANSPORTE", "TRANSPORT", "TRANSPORT")}</small><strong>{
          multiplayer.transportMode === "direct-host"
            ? pick("Host direto", "Direct host", "Host directo", "Direkter Host", "Hôte direct")
            : multiplayer.transportMode === "dedicated-server"
              ? pick("Servidor dedicado online", "Online dedicated server", "Servidor dedicado online", "Dedizierter Online-Server", "Serveur dédié en ligne")
              : multiplayer.transportMode === "relay"
                ? pick("Servidor online", "Online server", "Servidor online", "Online-Server", "Serveur en ligne")
              : multiplayer.transportMode === "remote-host"
                ? pick("Conectado ao host", "Connected to host", "Conectado al host", "Mit Host verbunden", "Connecté à l’hôte")
                : pick("Sem sessão", "No session", "Sin sesión", "Keine Sitzung", "Aucune session")
        }</strong></div>
      </section>

      <div className="mp-tabs" role="tablist">
        {([
          ["overview", "Visão geral"],
          ["room", pick("Sala", "Room", "Sala", "Raum", "Salle")],
          ["players", pick("Jogadores", "Players", "Jugadores", "Spieler", "Joueurs")],
          ["chat", pick("Chat & Voz", "Chat & Voice", "Chat y Voz", "Chat & Sprache", "Chat & Voix")],
          ["roleplay", pick("Personagem / RP", "Character / RP", "Personaje / RP", "Charakter / RP", "Personnage / RP")],
          ["advanced", pick("Avançado", "Advanced", "Avanzado", "Erweitert", "Avancé")]
        ] as [MultiplayerTab, string][]).map(([key, label]) => (
          <button key={key} className={tab === key ? "active" : ""} onClick={() => setTab(key)}>{label}</button>
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
                      onClick={() => {
                        if (createRoomMode === "navbr") {
                          const onlineUrl = relayServerUrl || defaultOnlineServer;
                          setRelayEnabled(true);
                          setRelayServerUrl(onlineUrl);
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
                        <span><strong>{pick("Meu PC", "My PC", "Mi PC", "Mein PC", "Mon PC")}</strong>{pick("Hospeda pela Internet e pode exigir UPnP/porta 27730.", "Hosts over the Internet and may require UPnP/port 27730.", "Aloja por Internet y puede requerir UPnP/puerto 27730.", "Hostet über das Internet und kann UPnP/Port 27730 benötigen.", "Héberge via Internet et peut nécessiter UPnP/port 27730.")}</span>
                      </div>
                    </details>

                    {createRoomMode === "navbr" && (
                      <details className="room-advanced-options">
                        <summary>{pick("Servidor personalizado", "Custom server", "Servidor personalizado", "Eigener Server", "Serveur personnalisé")}</summary>
                        <label className="password-field">
                          <span>{pick("URL do servidor", "Server URL", "URL del servidor", "Server-URL", "URL du serveur")}</span>
                          <input
                            value={relayServerUrl}
                            onChange={event => setRelayServerUrl(event.target.value)}
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
                      <button className="button primary" onClick={() => sendCommand("connectRoom", { serverUrl, roomId, displayName, roomPassword })}>
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
              </div>

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
              <button className="button ghost" onClick={() => sendCommand("refreshPublicRooms", { serverUrl })}>{pick("Atualizar", "Refresh", "Actualizar", "Aktualisieren", "Actualiser")}</button>
            </div>

            <input
              className="room-search"
              value={roomSearch}
              onChange={event => setRoomSearch(event.target.value)}
              placeholder={pick("Buscar sala, mapa, versão, ônibus ou HOF", "Search room, map, version, bus or HOF", "Buscar sala, mapa, versión, autobús o HOF", "Raum, Karte, Version, Bus oder HOF suchen", "Rechercher salle, carte, version, bus ou HOF")}
            />
            {state?.roomDirectory.error && <div className="directory-error">{state.roomDirectory.error}</div>}

            <div className="public-room-list">
              {publicRooms.length === 0 ? (
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
                    disabled={!room.directJoinAllowed}
                    title={!room.directJoinAllowed ? pick("Carregue a configuração compatível antes de entrar diretamente.", "Load a compatible configuration before joining directly.", "Carga una configuración compatible antes de entrar directamente.", "Vor direktem Beitritt eine kompatible Konfiguration laden.", "Chargez une configuration compatible avant de rejoindre directement.") : undefined}
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
                      player.physicalVehicleErrorMessage &&
                      player.physicalVehicleState !== "active" && (
                        <small className="physical-error-detail">
                          {player.physicalVehicleErrorMessage}
                        </small>
                      )}
                    {!player.isLocal &&
                      multiplayer.physicalVehiclesEnabled &&
                      player.physicalVehicleState !== "active" &&
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
            <div className="chat-log">
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
                  onChange={event => setVoiceRadius(Number(event.target.value))}
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
                    <span>{Math.round(player.gain * 100)}%</span>
                    <input
                      type="range"
                      min="0"
                      max="2"
                      step="0.05"
                      value={player.gain}
                      onChange={event => sendCommand("configureRemoteVoice", {
                        playerId: player.playerId,
                        muted: player.muted,
                        gain: Number(event.target.value)
                      })}
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
            <p>
              {multiplayer.physicalVehiclesEnabled
                ? multiplayer.physicalVehiclesAvailable
                  ? pick("Ativo e com plugin disponível.", "Enabled and plugin available.", "Activo y con plugin disponible.", "Aktiv und Plugin verfügbar.", "Activé et plugin disponible.")
                  : pick("Ativado; aguardando capacidade real do plugin OMSI.", "Enabled; waiting for real OMSI plugin capability.", "Activado; esperando capacidad real del plugin OMSI.", "Aktiviert; wartet auf echte OMSI-Plugin-Fähigkeit.", "Activé ; en attente de la capacité réelle du plugin OMSI.")
                : pick("Desativado. Nenhum ônibus remoto físico será criado no OMSI.", "Disabled. No physical remote bus will be created in OMSI.", "Desactivado. No se creará ningún autobús remoto físico en OMSI.", "Deaktiviert. Kein physischer Remote-Bus wird in OMSI erzeugt.", "Désactivé. Aucun bus distant physique ne sera créé dans OMSI.")}
            </p>
            <label className="privacy-toggle">
              <input
                type="checkbox"
                checked={multiplayer.physicalVehiclesEnabled}
                onChange={event => sendCommand("setPhysicalVehiclesEnabled", { enabled: event.target.checked })}
              />
              <span>{pick("Ativar teste físico de ônibus remotos", "Enable physical remote-bus test", "Activar prueba física de autobuses remotos", "Physischen Remote-Bus-Test aktivieren", "Activer le test physique des bus distants")}</span>
            </label>
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
        "Abra o NavBR e o OMSI 2, aguarde a detecção/telemetria, carregue mapa e ônibus e inicie a viagem. Durante o gameplay o HUD/minimapa acompanha os dados reais do OMSI e pode se ocultar quando necessário.",
        "Open NavBR and OMSI 2, wait for detection/telemetry, load a map and bus, and start the trip. During gameplay the HUD/minimap follows real OMSI data and may hide when needed.",
        "Abre NavBR y OMSI 2, espera la detección/telemetría, carga mapa y autobús e inicia el viaje. Durante el juego el HUD/minimapa sigue datos reales de OMSI.",
        "Starte NavBR und OMSI 2, warte auf Erkennung/Telemetrie, lade Karte und Bus und beginne die Fahrt. HUD/Minikarte nutzen echte OMSI-Daten.",
        "Ouvrez NavBR et OMSI 2, attendez la détection/télémétrie, chargez carte et bus puis démarrez le trajet. Le HUD/minicarte suit les données réelles d’OMSI."
      )
    },
    {
      title: pick("2. HUD, GPS e velocidade", "2. HUD, GPS and speed", "2. HUD, GPS y velocidad", "2. HUD, GPS und Geschwindigkeit", "2. HUD, GPS et vitesse"),
      body: pick(
        "Velocidade, linha, rota, destino, próxima parada e navegação vêm do estado real do OMSI. Configure preset, tema, escala, opacidade e módulos em Configurações > HUD; Mover HUD continua usando o overlay nativo.",
        "Speed, line, route, destination, next stop and navigation come from real OMSI state. Configure preset, theme, scale, opacity and modules under Settings > HUD; Move HUD still uses the native overlay.",
        "Velocidad, línea, ruta, destino, próxima parada y navegación vienen del estado real de OMSI. Configura el HUD en Configuración > HUD.",
        "Geschwindigkeit, Linie, Route, Ziel, nächste Haltestelle und Navigation stammen aus dem echten OMSI-Status. HUD-Einstellungen liegen unter Einstellungen > HUD.",
        "Vitesse, ligne, itinéraire, destination, prochain arrêt et navigation proviennent de l’état réel d’OMSI. Configurez le HUD dans Paramètres > HUD."
      )
    },
    {
      title: pick("3. Multiplayer", "3. Multiplayer", "3. Multijugador", "3. Multiplayer", "3. Multijoueur"),
      body: pick(
        "Crie uma sala online usando um servidor NavBR hospedado (por exemplo, Render) ou use o modo local TCP 27730. Salas privadas usam senha. Firewall, NAT/CGNAT e UPnP só são necessários no modo local.",
        "Create an online room using a hosted NavBR server (for example, Render), or use local TCP 27730 mode. Private rooms use a password. Firewall, NAT/CGNAT and UPnP are only needed for local hosting.",
        "Crea una sala online usando un servidor NavBR alojado (por ejemplo, Render), o usa el modo local TCP 27730. Firewall, NAT/CGNAT y UPnP solo son necesarios para alojamiento local.",
        "Erstelle einen Online-Raum über einen gehosteten NavBR-Server (zum Beispiel Render) oder nutze den lokalen TCP-27730-Modus. Firewall, NAT/CGNAT und UPnP sind nur für lokales Hosting nötig.",
        "Créez une salle en ligne via un serveur NavBR hébergé (par exemple Render), ou utilisez le mode local TCP 27730. Pare-feu, NAT/CGNAT et UPnP ne sont nécessaires que pour l’hébergement local."
      )
    },
    {
      title: pick("4. Chat e voz", "4. Chat and voice", "4. Chat y voz", "4. Chat und Sprache", "4. Chat et voix"),
      body: pick(
        `O atalho atual do chat é ${multiplayer.chatHotkey || "F9"} e o PTT é ${multiplayer.voiceHotkey || "F10"}. Em Multiplayer > Chat & Voz você configura canal, proximidade, microfone, saída, mute/volume por jogador e acompanha jitter/perda/FEC.`,
        `The current chat hotkey is ${multiplayer.chatHotkey || "F9"} and PTT is ${multiplayer.voiceHotkey || "F10"}. In Multiplayer > Chat & Voice you can configure channel, proximity, microphone, output, per-player mute/volume and monitor jitter/loss/FEC.`,
        `El atajo actual del chat es ${multiplayer.chatHotkey || "F9"} y PTT es ${multiplayer.voiceHotkey || "F10"}. En Multiplayer > Chat y Voz configuras canal, proximidad, dispositivos y mixer.`,
        `Der aktuelle Chat-Hotkey ist ${multiplayer.chatHotkey || "F9"}, PTT ist ${multiplayer.voiceHotkey || "F10"}. Unter Multiplayer > Chat & Sprache werden Kanal, Nähe, Geräte und Mixer eingestellt.`,
        `Le raccourci chat actuel est ${multiplayer.chatHotkey || "F9"} et le PTT ${multiplayer.voiceHotkey || "F10"}. Dans Multijoueur > Chat & Voix, configurez canal, proximité, périphériques et mixage.`
      )
    },
    {
      title: pick("5. Ônibus remoto físico — EXPERIMENTAL", "5. Physical remote bus — EXPERIMENTAL", "5. Autobús remoto físico — EXPERIMENTAL", "5. Physischer Remote-Bus — EXPERIMENTELL", "5. Bus distant physique — EXPÉRIMENTAL"),
      body: pick(
        "O teste físico vem desligado por padrão. Ambos os PCs precisam de mapa/ônibus compatíveis e do Plugin Bridge suportado. O NavBR não transfere conteúdo pago ou proprietário. A tela mostra a disponibilidade real do plugin antes de criar ônibus remotos.",
        "The physical test is off by default. Both PCs need compatible map/bus content and a supported Plugin Bridge. NavBR does not transfer paid or proprietary content. The UI reports the plugin’s real capability before spawning remote buses.",
        "La prueba física está desactivada por defecto. Ambos PCs necesitan mapa/autobús compatibles y Plugin Bridge compatible. NavBR no transfiere contenido de pago o propietario.",
        "Der physische Test ist standardmäßig aus. Beide PCs benötigen kompatible Karte/Bus und eine unterstützte Plugin Bridge. NavBR überträgt keine kostenpflichtigen/proprietären Inhalte.",
        "Le test physique est désactivé par défaut. Les deux PC doivent avoir carte/bus compatibles et un Plugin Bridge pris en charge. NavBR ne transfère aucun contenu payant/propriétaire."
      )
    },
    {
      title: pick("6. Diagnósticos automáticos", "6. Automatic diagnostics", "6. Diagnósticos automáticos", "6. Automatische Diagnose", "6. Diagnostics automatiques"),
      body: pick(
        "O envio de diagnósticos é opcional e pode ser ligado/desligado em Configurações > Diagnóstico. Ele registra dados técnicos permitidos para investigar falhas; não deve incluir chat, áudio, senhas, tokens ou arquivos pessoais. A fila local pode ser apagada.",
        "Diagnostics are optional and can be enabled/disabled under Settings > Diagnostics. They record permitted technical data for troubleshooting; they should not include chat, audio, passwords, tokens or personal files. The local queue can be purged.",
        "Los diagnósticos son opcionales y se controlan en Configuración > Diagnóstico. No deben incluir chat, audio, contraseñas, tokens ni archivos personales.",
        "Diagnosen sind optional und unter Einstellungen > Diagnose steuerbar. Chat, Audio, Passwörter, Tokens oder persönliche Dateien sollen nicht enthalten sein.",
        "Les diagnostics sont facultatifs et se règlent dans Paramètres > Diagnostic. Ils ne doivent pas contenir chat, audio, mots de passe, jetons ou fichiers personnels."
      )
    },
    {
      title: pick("7. Se algo não funcionar", "7. Troubleshooting", "7. Si algo no funciona", "7. Wenn etwas nicht funktioniert", "7. Si quelque chose ne fonctionne pas"),
      body: pick(
        "OMSI não detectado: confira a instalação e se Omsi.exe está aberto. HUD sem dados: entre no gameplay. Mapa/rota ausente: confira roadmap e viagem ativa. Multiplayer sem conexão: confira servidor/sala/senha, TCP 27730 e Rede. Ônibus físico ausente: valide mapa, modelo e plugin.",
        "OMSI not detected: check the installation and that Omsi.exe is running. HUD without data: enter gameplay. Missing map/route: check roadmap and active trip. Multiplayer connection: verify server/room/password, TCP 27730 and Network. Missing physical bus: validate map, model and plugin.",
        "OMSI no detectado: revisa instalación y Omsi.exe. Sin datos HUD: entra al juego. Sin mapa/ruta: revisa roadmap y viaje. Multiplayer: servidor/sala/contraseña, TCP 27730 y Red.",
        "OMSI nicht erkannt: Installation und Omsi.exe prüfen. HUD ohne Daten: Gameplay starten. Karte/Route fehlt: Roadmap/Fahrt prüfen. Multiplayer: Server/Raum/Passwort, TCP 27730 und Netzwerk prüfen.",
        "OMSI non détecté : vérifiez l’installation et Omsi.exe. HUD sans données : entrez en jeu. Carte/route absente : vérifiez roadmap/trajet. Multijoueur : serveur/salle/mot de passe, TCP 27730 et Réseau."
      )
    },
    {
      title: pick("8. Teste da comunidade", "8. Community testing", "8. Prueba comunitaria", "8. Community-Test", "8. Test communautaire"),
      body: pick(
        "Ao relatar um erro, informe o que estava fazendo, mapa, ônibus, sala, se RP/ônibus físico estavam ativos e, quando possível, anexe prints e logs. Isso ajuda a reproduzir a falha sem usar dados simulados.",
        "When reporting a problem, include what you were doing, map, bus, room, whether RP/physical buses were active, and screenshots/logs when possible. This helps reproduce the issue without simulated data.",
        "Al reportar un problema, indica qué hacías, mapa, autobús, sala, si RP/autobús físico estaban activos y adjunta capturas/logs cuando sea posible.",
        "Bei Fehlerberichten bitte Aktion, Karte, Bus, Raum, RP/physische Busse sowie möglichst Screenshots/Logs angeben.",
        "Pour signaler un problème, indiquez l’action, la carte, le bus, la salle, l’état RP/bus physique et joignez si possible captures/logs."
      )
    }
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
            "Escolha o idioma na barra lateral, confira os passos essenciais abaixo e conclua este primeiro acesso quando estiver pronto.",
            "Choose your language from the sidebar, review the essential steps below, and complete first run when ready.",
            "Elige el idioma en la barra lateral, revisa los pasos esenciales y completa el primer acceso cuando estés listo.",
            "Wähle die Sprache in der Seitenleiste, lies die wichtigsten Schritte und schließe den ersten Start ab.",
            "Choisissez la langue dans la barre latérale, consultez les étapes essentielles puis terminez le premier démarrage."
          )}</p>
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
  const [settingsTabRequest, setSettingsTabRequest] = useState<SettingsTab | null>(null);
  const [navigationViewRequest, setNavigationViewRequest] = useState<{ id: number; view: "2d" | "3d" } | null>(null);
  const [operationsTabRequest, setOperationsTabRequest] = useState<{ id: number; tab: OperationsTab } | null>(null);
  const [companyNetworkTabRequest, setCompanyNetworkTabRequest] = useState<{ id: number; tab: CompanyNetworkTab } | null>(null);
  const [pluginPromptDismissed, setPluginPromptDismissed] = useState(false);
  const lastNavigationRequestId = useRef<number | null>(null);

  const openSettingsTab = (tab: SettingsTab) => {
    setSettingsTabRequest(tab);
    setScreen("settings");
  };

  useEffect(() => subscribeToNavBrState(
    next => {
      setState(next);
      setCommandError(null);

      const navigationRequest = next.navigationRequest;
      if (navigationRequest && navigationRequest.id !== lastNavigationRequestId.current) {
        lastNavigationRequestId.current = navigationRequest.id;
        const requested = navigationRequest.screen;
        const requestedSettingsTab = requested?.startsWith("settings-")
          ? requested.slice("settings-".length) as SettingsTab
          : null;
        if (requestedSettingsTab && ["installations", "hud", "roadmap", "diagnostics", "network", "advanced"].includes(requestedSettingsTab)) {
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
    setCommandError
  ), []);

  return (
    <I18nProvider cultureName={state?.cultureName} languages={state?.supportedLanguages}>
    <div className="app-shell">
      <Sidebar screen={screen} setScreen={setScreen} appVersion={state?.appVersion} />
      <main>
        <PluginStartupPrompt
          state={state}
          dismissed={pluginPromptDismissed}
          onDismiss={() => setPluginPromptDismissed(true)}
          onOpenInstallations={() => openSettingsTab("installations")}
        />
        {screen === "home"
          ? <Home state={state} />
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
