import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import { I18nProvider, useI18n } from "./i18n";
import {
  type NavBrCompanyMember,
  type NavBrGhostState,
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

type Screen = "home" | "navigation" | "roleplay" | "ghost" | "operations" | "companyNetwork" | "hardware" | "settings" | "multiplayer";
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
  chatHotkey: "F9",
  voiceHotkey: "F10",
  hotkeyOptions: [],
  relayEnabled: false,
  relayServerUrl: "",
  roleplayEnabled: false,
  localRoleplayActive: false,
  selectedRoleplayCharacter: null,
  playerCount: 0,
  players: [],
  sessionPoints: [],
  chat: []
};

function Sidebar({
  screen,
  setScreen
}: {
  screen: Screen;
  setScreen: (screen: Screen) => void;
}) {
  const { t, cultureName, languages, setLanguage } = useI18n();
  return (
    <aside className="sidebar">
      <div className="brand">
        <div className="brand-mark">N</div>
        <div><strong>NavBR</strong><span>OMSI Multiplayer</span></div>
      </div>
      <nav className="nav">
        <button className={`nav-item ${screen === "home" ? "active" : ""}`} onClick={() => setScreen("home")}>
          <b>⌂</b><span>{t("nav.home")}</span>
        </button>
        <button className={`nav-item ${screen === "navigation" ? "active" : ""}`} onClick={() => setScreen("navigation")}>
          <b>⌖</b><span>{t("nav.navigation")}</span>
        </button>
        <button className={`nav-item ${screen === "multiplayer" ? "active" : ""}`} onClick={() => setScreen("multiplayer")}>
          <b>◉</b><span>{t("nav.multiplayer")}</span>
        </button>
        <button className={`nav-item ${screen === "roleplay" ? "active" : ""}`} onClick={() => setScreen("roleplay")}>
          <b>♙</b><span>{t("nav.roleplay")}</span>
        </button>
        <button className={`nav-item ${screen === "ghost" ? "active" : ""}`} onClick={() => setScreen("ghost")}>
          <b>◈</b><span>{t("nav.ghost")}</span>
        </button>
        <button className={`nav-item ${screen === "operations" ? "active" : ""}`} onClick={() => setScreen("operations")}>
          <b>▣</b><span>{t("nav.operations")}</span>
        </button>
        <button className={`nav-item ${screen === "companyNetwork" ? "active" : ""}`} onClick={() => setScreen("companyNetwork")}>
          <b>◎</b><span>{t("nav.company")}</span>
        </button>
        <button className={`nav-item ${screen === "hardware" ? "active" : ""}`} onClick={() => setScreen("hardware")}>
          <b>⚡</b><span>{t("nav.hardware")}</span>
        </button>
        <button className={`nav-item ${screen === "settings" ? "active" : ""}`} onClick={() => setScreen("settings")}>
          <b>⚙</b><span>{t("nav.settings")}</span>
        </button>
      </nav>
      <div className="sidebar-footer">
        <i />
        <div className="sidebar-footer-main">
          <div><strong>Alpha.14</strong><small>React + WebView2</small></div>
          <label className="sidebar-language">
            <span>{t("common.language")}</span>
            <select value={cultureName} onChange={event => setLanguage(event.target.value)}>
              {languages.map(language => (
                <option key={language.cultureName} value={language.cultureName}>{language.displayName}</option>
              ))}
            </select>
          </label>
        </div>
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
          <button className="button ghost" onClick={() => sendCommand("refreshState")}>{t("common.refresh")}</button>
          <button className="button primary" disabled={Boolean(omsi?.running)} onClick={() => sendCommand("launchOmsi")}>
            {omsi?.running ? t("home.open") : t("home.launch")}
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
) => {
  switch (maneuver) {
    case "SlightLeft": return { arrow: "↖", title: pick("Mantenha à esquerda", "Keep left", "Mantente a la izquierda", "Links halten", "Restez à gauche") };
    case "Left": return { arrow: "←", title: pick("Vire à esquerda", "Turn left", "Gira a la izquierda", "Links abbiegen", "Tournez à gauche") };
    case "SharpLeft": return { arrow: "↙", title: pick("Curva forte à esquerda", "Sharp left", "Giro cerrado a la izquierda", "Scharf links", "Virage serré à gauche") };
    case "SlightRight": return { arrow: "↗", title: pick("Mantenha à direita", "Keep right", "Mantente a la derecha", "Rechts halten", "Restez à droite") };
    case "Right": return { arrow: "→", title: pick("Vire à direita", "Turn right", "Gira a la derecha", "Rechts abbiegen", "Tournez à droite") };
    case "SharpRight": return { arrow: "↘", title: pick("Curva forte à direita", "Sharp right", "Giro cerrado a la derecha", "Scharf rechts", "Virage serré à droite") };
    case "RejoinRoute": return { arrow: "↺", title: pick("Retorne para a rota", "Rejoin the route", "Vuelve a la ruta", "Zur Route zurückkehren", "Rejoignez l’itinéraire") };
    default: return { arrow: "↑", title: pick("Siga em frente", "Continue straight", "Sigue recto", "Geradeaus weiter", "Continuez tout droit") };
  }
};

function NavigationMap({ navigation }: { navigation: NavBrNavigationState }) {
  const { t, pick } = useI18n();
  const [mode, setMode] = useState<"follow" | "full">("follow");

  const geometry = useMemo(() => {
    const route = navigation.routePoints;
    const vehicle = navigation.vehicle;

    if (route.length < 2) {
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
    } else {
      const xs = route.map(point => point.x);
      const ys = route.map(point => -point.y);
      minX = Math.min(...xs);
      maxX = Math.max(...xs);
      minY = Math.min(...ys);
      maxY = Math.max(...ys);
      const pad = Math.max(80, Math.max(maxX - minX, maxY - minY) * 0.08);
      minX -= pad;
      maxX += pad;
      minY -= pad;
      maxY += pad;
    }

    const width = Math.max(120, maxX - minX);
    const height = Math.max(120, maxY - minY);
    const routePoints = route.map(point => `${point.x},${-point.y}`).join(" ");

    return {
      viewBox: `${minX} ${minY} ${width} ${height}`,
      routePoints
    };
  }, [navigation.routePoints, navigation.vehicle, mode]);

  return (
    <div className="navigation-map">
      <div className="navigation-map-toolbar">
        <button className={mode === "follow" ? "active" : ""} onClick={() => setMode("follow")}>{t("nav.followBus")}</button>
        <button className={mode === "full" ? "active" : ""} onClick={() => setMode("full")}>{t("nav.fullRoute")}</button>
      </div>

      {!geometry ? (
        <div className="map-center-message navigation-empty">
          <strong>{t("nav.routeUnavailable")}</strong>
          <span>{t("nav.routeUnavailableDetail")}</span>
        </div>
      ) : (
        <svg viewBox={geometry.viewBox} preserveAspectRatio="xMidYMid meet" aria-label={pick("Roadmap da rota ativa", "Active route roadmap", "Roadmap de la ruta activa", "Roadmap der aktiven Route", "Roadmap de l’itinéraire actif")}>
          <polyline className="nav-route-shadow" points={geometry.routePoints} />
          <polyline className="nav-route-line" points={geometry.routePoints} />

          {navigation.stopPoints.map((stop, index) => (
            <g key={`${stop.name}-${index}`} transform={`translate(${stop.x} ${-stop.y})`}>
              <circle className={`nav-stop ${stop.isNext ? "next" : ""}`} r={stop.isNext ? 15 : 9} />
              {stop.isNext && (
                <text className="nav-stop-label" x="20" y="-16">{stop.name}</text>
              )}
            </g>
          ))}

          {navigation.vehicle && (
            <g
              className="nav-vehicle"
              transform={`translate(${navigation.vehicle.x} ${-navigation.vehicle.y}) rotate(${navigation.vehicle.headingDegrees})`}
            >
              <circle r="23" className="nav-vehicle-halo" />
              <path d="M -12 -20 L 12 -20 L 14 13 L 0 23 L -14 13 Z" />
              <path className="nav-vehicle-heading" d="M 0 -32 L -7 -20 L 7 -20 Z" />
            </g>
          )}
        </svg>
      )}

      <div className="navigation-map-legend">
        <span><i className="route" /> {pick("Rota OMSI", "OMSI route", "Ruta OMSI", "OMSI-Route", "Itinéraire OMSI")}</span>
        <span><i className="stop" /> {pick("Paradas", "Stops", "Paradas", "Haltestellen", "Arrêts")}</span>
        <span><i className="bus" /> {pick("Seu ônibus", "Your bus", "Tu autobús", "Dein Bus", "Votre bus")}</span>
      </div>
    </div>
  );
}


function Navigation3DMap({ state }: { state: NavBrState["navigation3D"] }) {
  const { t, pick } = useI18n();
  const [camera, setCamera] = useState<"follow" | "aerial">("follow");
  const [zoom, setZoom] = useState(1);

  const scene = useMemo(() => {
    if (!state.bounds || !state.roadmapAvailable || !state.roadmapUrl) {
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
    const local = state.localVehicle
      ? { ...state.localVehicle, ...project(state.localVehicle.x, state.localVehicle.y) }
      : null;
    const remotes = state.remoteVehicles.map(vehicle => ({
      ...vehicle,
      ...project(vehicle.x, vehicle.y)
    }));

    let viewBox = `0 0 ${sceneWidth} ${sceneHeight}`;
    if (camera === "follow" && local) {
      const spanX = Math.max(150, 430 / zoom);
      const spanY = Math.max(110, 290 / zoom);
      const minX = Math.max(0, Math.min(sceneWidth - spanX, local.x - spanX / 2));
      const minY = Math.max(0, Math.min(sceneHeight - spanY, local.y - spanY * 0.58));
      viewBox = `${minX} ${minY} ${spanX} ${spanY}`;
    }

    return { sceneWidth, sceneHeight, route, local, remotes, viewBox };
  }, [state, camera, zoom]);

  if (!state.roadmapAvailable) {
    return (
      <div className="map-center-message navigation-empty nav3d-empty">
        <strong>{pick("Roadmap 3D indisponível", "3D roadmap unavailable", "Roadmap 3D no disponible", "3D-Roadmap nicht verfügbar", "Roadmap 3D indisponible")}</strong>
        <span>{pick("Gere o whole.roadmap.bmp em Configurações → Roadmap Studio para usar a visão 3D real.", "Generate whole.roadmap.bmp in Settings → Roadmap Studio to use the real 3D view.", "Genera whole.roadmap.bmp en Configuración → Roadmap Studio para usar la vista 3D real.", "Erzeuge whole.roadmap.bmp unter Einstellungen → Roadmap Studio für die echte 3D-Ansicht.", "Générez whole.roadmap.bmp dans Paramètres → Roadmap Studio pour utiliser la vue 3D réelle.")}</span>
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

  return (
    <div className="navigation-3d">
      <div className="navigation-map-toolbar nav3d-toolbar">
        <button className={camera === "follow" ? "active" : ""} onClick={() => setCamera("follow")}>{t("nav.followBus")}</button>
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
              href={state.roadmapUrl || undefined}
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
          </svg>
        </div>
      </div>

      <div className="nav3d-footer">
        <span><strong>{state.mapName || pick("Mapa OMSI", "OMSI map", "Mapa OMSI", "OMSI-Karte", "Carte OMSI")}</strong></span>
        <span>{state.routeAvailable ? pick("Rota real carregada", "Real route loaded", "Ruta real cargada", "Echte Route geladen", "Itinéraire réel chargé") : pick("Rota não resolvida", "Route not resolved", "Ruta no resuelta", "Route nicht aufgelöst", "Itinéraire non résolu")}</span>
        <span>{state.remoteCount} {pick("ônibus remoto(s) compatível(is)", "compatible remote bus(es)", "autobús(es) remoto(s) compatible(s)", "kompatible Remote-Busse", "bus distant(s) compatible(s)")}</span>
      </div>
    </div>
  );
}

function Navigation({ state }: { state: NavBrState | null }) {
  const { t, pick } = useI18n();
  const navigation = state?.navigation;
  const navigation3D = state?.navigation3D;
  const [mapView, setMapView] = useState<"2d" | "3d">("2d");
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
            : <NavigationMap navigation={navigation} />}
        </article>

        <aside className="navigation-side">
          <article className={`card maneuver-card ${navigation.maneuver === "RejoinRoute" ? "warning" : ""}`}>
            <span className="eyebrow">{navigation.maneuver === "RejoinRoute" ? pick("CORREÇÃO DE ROTA", "ROUTE CORRECTION", "CORRECCIÓN DE RUTA", "ROUTENKORREKTUR", "CORRECTION D’ITINÉRAIRE") : pick("PRÓXIMA MANOBRA", "NEXT MANEUVER", "PRÓXIMA MANIOBRA", "NÄCHSTES MANÖVER", "PROCHAINE MANŒUVRE")}</span>
            <div className="maneuver-main">
              <strong>{maneuver.arrow}</strong>
              <div>
                <h3>{maneuver.title}</h3>
                <p>
                  {navigation.maneuver === "RejoinRoute"
                    ? formatDistance(navigation.offRouteDistanceMeters)
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
  onNavigate
}: {
  state: NavBrState | null;
  error: string | null;
  onNavigate: (screen: Screen) => void;
}) {
  const { pick } = useI18n();
  const operations = state?.operations;
  const multiplayer = state?.multiplayer ?? fallbackMultiplayer;
  const [tab, setTab] = useState<OperationsTab>("overview");

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
                <span className="eyebrow">{pick("SESSÃO OPERACIONAL", "OPERATION SESSION", "SESIÓN OPERACIONAL", "BETRIEBSSITZUNG", "SESSION OPÉRATIONNELLE")}</span>
                <h3>{local?.mapName || state?.telemetry?.mapName || pick("Sem mapa ativo", "No active map", "Sin mapa activo", "Keine aktive Karte", "Aucune carte active")}</h3>
              </div>
              <span className={`live-pill ${operations.connected ? "" : "muted"}`}><span /> {operations.connected ? "LIVE" : "LOCAL"}</span>
            </div>
            <SessionMap points={multiplayer.sessionPoints} />
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
            <button className="button ghost" onClick={() => sendCommand("saveDriverProfile", {
              displayName: profileName,
              companyName: profileCompany
            })}>{pick("Salvar perfil", "Save profile", "Guardar perfil", "Profil speichern", "Enregistrer le profil")}</button>
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

function CompanyNetwork({ state, error }: { state: NavBrState | null; error: string | null }) {
  const { pick } = useI18n();
  const companyNetwork = state?.companyNetwork;
  const localCompany = state?.operations.company;
  const [tab, setTab] = useState<CompanyNetworkTab>("network");
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

  if (!companyNetwork?.available) return <div className="card empty-state">{pick("Aguardando o runtime da Rede da Empresa…", "Waiting for Company Network runtime…", "Esperando el runtime de la Red de Empresa…", "Warte auf Company-Network-Runtime…", "En attente du runtime Réseau Entreprise…")}</div>;

  const company = companyNetwork.company;
  const node = companyNetwork.node;
  const canCreateInvite = Boolean(node?.running && company?.canInvite);

  return (
    <>
      <header className="topbar company-network-header">
        <div><span className="eyebrow">NAVBR COMPANY NETWORK</span><h1>{pick("Rede da empresa", "Company network", "Red de empresa", "Unternehmensnetz", "Réseau entreprise")}</h1><p>{pick("Empresa online peer-hosted, identidade assinada e equipe administrada pelo backend nativo.", "Peer-hosted online company with signed identity and team managed by the native backend.", "Empresa online peer-hosted, identidad firmada y equipo gestionado por el backend nativo.", "Peer-gehostetes Online-Unternehmen mit signierter Identität und Teamverwaltung im nativen Backend.", "Entreprise en ligne peer-hosted avec identité signée et équipe gérée par le backend natif.")}</p></div>
        <div className="top-actions"><span className={`connection-pill ${node?.running ? "connected" : ""}`}><i /> {node?.running ? "Company Node TCP " + node.port : companyNetwork.membership ? pick("Vinculado", "Linked", "Vinculado", "Verknüpft", "Lié") : "Offline"}</span><button className="button ghost" onClick={() => sendCommand("refreshCompanyNetwork")}>{pick("Atualizar", "Refresh", "Actualizar", "Aktualisieren", "Actualiser")}</button></div>
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
          <article className="card company-node-card"><div className="section-heading"><div><span className="eyebrow">IDENTIDADE NAVBR</span><h3>{companyNetwork.identity?.displayName || pick("Motorista", "Driver", "Conductor", "Fahrer", "Conducteur")}</h3></div></div><code>{companyNetwork.identity?.playerId || "—"}</code><p>{pick("A chave privada permanece protegida no Windows e nunca é enviada ao React.", "The private key remains protected in Windows and is never sent to React.", "La clave privada permanece protegida en Windows y nunca se envía a React.", "Der private Schlüssel bleibt in Windows geschützt und wird nie an React gesendet.", "La clé privée reste protégée dans Windows et n’est jamais envoyée à React.")}</p></article>
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


function HudSettingsPanel({ hud }: { hud: NavBrHudState }) {
  const { t } = useI18n();
  const [draft, setDraft] = useState<NavBrHudState>(hud);
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    if (!dirty) {
      setDraft(hud);
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

  const save = () => {
    sendCommand("saveHudSettings", {
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
    });
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
            <span>{t("hud.anchor")}</span>
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
            <span><strong>{t("hud.height")}</strong><em>{draft.height < 1 ? t("hud.auto") : `${Math.round(draft.height)} px`}</em></span>
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
        <button className="button primary" disabled={!dirty} onClick={save}>
          {dirty ? t("hud.apply") : t("hud.applied")}
        </button>
        <button className="button ghost" onClick={() => {
          sendCommand("resetHudSettings");
          setDirty(false);
        }}>{t("common.reset")}</button>
        <button className="button ghost" onClick={() => sendCommand("toggleHudLayout")}>{t("hud.move")}</button>
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
              <div><span className="eyebrow">{pick("DESCOBERTA", "DISCOVERY", "DESCUBRIMIENTO", "ERKENNUNG", "DÉTECTION")}</span><h3>{pick("Encontrar OMSI 2", "Find OMSI 2", "Encontrar OMSI 2", "OMSI 2 finden", "Trouver OMSI 2")}</h3></div>
              <button className="button ghost" onClick={() => sendCommand("selectOmsiFolder")}>{pick("Selecionar pasta", "Select folder", "Seleccionar carpeta", "Ordner auswählen", "Sélectionner le dossier")}</button>
            </div>
            <p>{pick("O NavBR pode localizar instalações registradas, bibliotecas Steam e também validar uma pasta informada manualmente.", "NavBR can locate registered installations, Steam libraries and validate a manually supplied folder.", "NavBR puede localizar instalaciones registradas, bibliotecas Steam y validar una carpeta indicada manualmente.", "NavBR kann registrierte Installationen, Steam-Bibliotheken und einen manuell angegebenen Ordner prüfen.", "NavBR peut localiser les installations enregistrées, les bibliothèques Steam et valider un dossier indiqué manuellement.")}</p>
            <div className="discovery-actions">
              <input
                value={manualPath}
                onChange={event => setManualPath(event.target.value)}
                placeholder="Ex.: G:\Games\OMSI 2 Steam Edition"
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
            <p>{pick("O teste externo é separado do Firewall e do UPnP. Ele só funciona quando um serviço de callback externo está configurado.", "The external test is separate from Firewall and UPnP. It only works when an external callback service is configured.", "La prueba externa es independiente del Firewall y UPnP. Solo funciona cuando hay un servicio callback externo configurado.", "Der externe Test ist von Firewall und UPnP getrennt und funktioniert nur mit konfiguriertem externen Callback-Dienst.", "Le test externe est séparé du pare-feu et d’UPnP. Il ne fonctionne que si un service callback externe est configuré.")}</p>
            <button className="button ghost" onClick={() => sendCommand("runExternalPortProbe")}>{pick("Testar TCP 27730 externamente", "Test TCP 27730 externally", "Probar TCP 27730 externamente", "TCP 27730 extern testen", "Tester TCP 27730 depuis l’extérieur")}</button>
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
            <span className="eyebrow">MULTIPLAYER</span>
            <h3>{pick("Rede e conectividade", "Network and connectivity", "Red y conectividad", "Netzwerk und Konnektivität", "Réseau et connectivité")}</h3>
            <p>{pick("Firewall, NAT e UPnP já estão disponíveis na aba Rede. Relay e ônibus físico continuam no controlador nativo.", "Firewall, NAT and UPnP are available in the Network tab. Relay and physical bus remain in the native controller.", "Firewall, NAT y UPnP están disponibles en la pestaña Red. Relay y autobús físico permanecen en el controlador nativo.", "Firewall, NAT und UPnP sind im Netzwerktab verfügbar. Relay und physischer Bus bleiben im nativen Controller.", "Pare-feu, NAT et UPnP sont disponibles dans l’onglet Réseau. Relay et bus physique restent dans le contrôleur natif.")}</p>
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
            <p>{pick("Análise, montagem por tiles e geração vetorial pelas splines já usam os serviços nativos pela interface React.", "Analysis, tile assembly and vector generation from splines already use native services through the React interface.", "El análisis, montaje por tiles y generación vectorial por splines ya usan los servicios nativos desde la interfaz React.", "Analyse, Tile-Zusammenbau und Vektorerzeugung aus Splines verwenden bereits native Dienste über die React-Oberfläche.", "L’analyse, l’assemblage des tiles et la génération vectorielle par splines utilisent déjà les services natifs via l’interface React.")}</p>
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
    case "roleplay-returned-to-bus": return pick("Motorista retornou ao ônibus", "Driver returned to the bus", "El conductor volvió al autobús", "Fahrer ist zum Bus zurückgekehrt", "Le conducteur est retourné au bus");
    case "roleplay-character-selected": return pick("Personagem selecionado", "Character selected", "Personaje seleccionado", "Charakter ausgewählt", "Personnage sélectionné");
    case "roleplay-plugin-unavailable": return pick("Plugin Bridge sem suporte RP", "Plugin Bridge has no RP support", "Plugin Bridge sin soporte RP", "Plugin Bridge ohne RP-Unterstützung", "Plugin Bridge sans prise en charge RP");
    case "roleplay-character-required": return pick("Selecione um personagem", "Select a character", "Selecciona un personaje", "Charakter auswählen", "Sélectionnez un personnage");
    case "roleplay-waiting-telemetry": return pick("Aguardando telemetria do OMSI", "Waiting for OMSI telemetry", "Esperando telemetría de OMSI", "Warte auf OMSI-Telemetrie", "En attente de la télémétrie OMSI");
    case "roleplay-map-or-character-changed": return pick("Mapa/personagem alterado", "Map/character changed", "Mapa/personaje cambiado", "Karte/Charakter geändert", "Carte/personnage modifié");
    case "roleplay-control-lost": return pick("Controle do personagem perdido", "Character control lost", "Control del personaje perdido", "Charaktersteuerung verloren", "Contrôle du personnage perdu");
    case "roleplay-disabled": return pick("Recurso RP desativado", "RP feature disabled", "Función RP desactivada", "RP-Funktion deaktiviert", "Fonction RP désactivée");
    case "roleplay-enabled": return pick("Recurso RP ativado", "RP feature enabled", "Función RP activada", "RP-Funktion aktiviert", "Fonction RP activée");
    default: return status || pick("Pronto", "Ready", "Listo", "Bereit", "Prêt");
  }
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

  if (!roleplay) {
    return <div className="card empty-state">{pick("Aguardando estado do Personagem / RP…", "Waiting for Character / RP state…", "Esperando el estado del Personaje / RP…", "Warte auf Charakter-/RP-Status…", "En attente de l’état Personnage / RP…")}</div>;
  }

  const canStart = roleplay.enabled && roleplay.mapReady && roleplay.runtimeAvailable && Boolean(roleplay.selected) && !roleplay.active;
  const current = roleplay.current;

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
              {roleplay.runtimeAvailable ? pick("Bridge RP disponível", "RP Bridge available", "Bridge RP disponible", "RP-Bridge verfügbar", "Bridge RP disponible") : pick("Bridge RP indisponível", "RP Bridge unavailable", "Bridge RP no disponible", "RP-Bridge nicht verfügbar", "Bridge RP indisponible")}
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

          <div className="details-grid rp-status-grid">
            <div><small>{pick("MAPA", "MAP", "MAPA", "KARTE", "CARTE")}</small><strong>{state?.telemetry?.mapName || "—"}</strong></div>
            <div><small>STATUS</small><strong>{roleplayStatusLabel(roleplay.status, pick)}</strong></div>
            <div><small>{pick("MAPA PRONTO", "MAP READY", "MAPA LISTO", "KARTE BEREIT", "CARTE PRÊTE")}</small><strong>{roleplay.mapReady ? pick("Sim", "Yes", "Sí", "Ja", "Oui") : pick("Não", "No", "No", "Nein", "Non")}</strong></div>
            <div><small>MULTIPLAYER</small><strong>{multiplayer.connected ? multiplayer.roomId : pick("Não conectado", "Not connected", "No conectado", "Nicht verbunden", "Non connecté")}</strong></div>
          </div>

          {current && (
            <div className="rp-current-state">
              <span><small>{pick("ATIVIDADE", "ACTIVITY", "ACTIVIDAD", "AKTIVITÄT", "ACTIVITÉ")}</small><strong>{current.activity}</strong></span>
              <span><small>{pick("VELOCIDADE", "SPEED", "VELOCIDAD", "GESCHWINDIGKEIT", "VITESSE")}</small><strong>{format(current.speedMps * 3.6, 1)} km/h</strong></span>
              <span><small>{pick("DIREÇÃO", "HEADING", "DIRECCIÓN", "RICHTUNG", "DIRECTION")}</small><strong>{format(current.headingDegrees, 0)}°</strong></span>
              <span><small>HUMAN INDEX</small><strong>{current.humanIndex ?? "—"}</strong></span>
            </div>
          )}

          <div className="action-row rp-actions">
            {roleplay.active ? (
              <button className="button primary" onClick={() => sendCommand("stopRoleplay")}>{pick("Retornar ao ônibus", "Return to bus", "Volver al autobús", "Zum Bus zurückkehren", "Retourner au bus")}</button>
            ) : (
              <button className="button primary" disabled={!canStart} onClick={() => sendCommand("startRoleplay")}>{pick("Sair do ônibus", "Leave bus", "Salir del autobús", "Bus verlassen", "Sortir du bus")}</button>
            )}
            <button className="button ghost" onClick={() => sendCommand("refreshState")}>{pick("Atualizar catálogo", "Refresh catalog", "Actualizar catálogo", "Katalog aktualisieren", "Actualiser le catalogue")}</button>
          </div>

          {!roleplay.enabled && <p className="migration-note">{pick("O modo Personagem / RP está desativado nas configurações experimentais.", "Character / RP mode is disabled in experimental settings.", "El modo Personaje / RP está desactivado en la configuración experimental.", "Charakter-/RP-Modus ist in den experimentellen Einstellungen deaktiviert.", "Le mode Personnage / RP est désactivé dans les paramètres expérimentaux.")}</p>}
          {roleplay.enabled && !roleplay.mapReady && <p className="migration-note">{pick("Entre em um mapa do OMSI para carregar os personagens reais de Map.Drivers.", "Enter an OMSI map to load real Map.Drivers characters.", "Entra en un mapa de OMSI para cargar los personajes reales de Map.Drivers.", "Öffne eine OMSI-Karte, um echte Map.Drivers-Charaktere zu laden.", "Entrez dans une carte OMSI pour charger les personnages réels de Map.Drivers.")}</p>}
          {roleplay.mapReady && !roleplay.runtimeAvailable && <p className="migration-note">{pick("O Plugin Bridge precisa anunciar as capacidades de posse e transformação de personagem.", "Plugin Bridge must advertise character possession and transform capabilities.", "Plugin Bridge debe anunciar las capacidades de posesión y transformación del personaje.", "Plugin Bridge muss Fähigkeiten für Charakterübernahme und Transformation melden.", "Plugin Bridge doit annoncer les capacités de possession et de transformation du personnage.")}</p>}
        </article>

        <article className="card rp-character-card">
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
                  disabled={roleplay.active}
                  onClick={() => sendCommand("selectRoleplayCharacter", { characterId: character.id })}
                >
                  <span className="rp-character-avatar">♙</span>
                  <span>
                    <strong>{character.displayName}</strong>
                    <small>{character.isActiveDriver ? pick("Motorista ativo do mapa", "Active map driver", "Conductor activo del mapa", "Aktiver Kartenfahrer", "Conducteur actif de la carte") : character.sourceValue}</small>
                  </span>
                  <em>{character.selected ? pick("Selecionado", "Selected", "Seleccionado", "Ausgewählt", "Sélectionné") : pick("Usar", "Use", "Usar", "Verwenden", "Utiliser")}</em>
                </button>
              ))}
            </div>
          )}
          <p className="hardware-note">{pick("A seleção é válida somente para a sessão/mapa atual. O DefinitionPointer nativo não é persistido.", "Selection is valid only for the current session/map. The native DefinitionPointer is not persisted.", "La selección solo es válida para la sesión/mapa actual. El DefinitionPointer nativo no se conserva.", "Die Auswahl gilt nur für die aktuelle Sitzung/Karte. Der native DefinitionPointer wird nicht gespeichert.", "La sélection n’est valable que pour la session/carte actuelle. Le DefinitionPointer natif n’est pas persisté.")}</p>
        </article>
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
            <span><kbd>Esc</kbd><strong>{pick("Retornar ao ônibus", "Return to bus", "Volver al autobús", "Zum Bus zurückkehren", "Retourner au bus")}</strong></span>
          </div>
          <p>{pick("Os atalhos só são capturados quando o OMSI está em primeiro plano. O personagem permanece limitado à área segura ao redor do ônibus.", "Shortcuts are captured only while OMSI is in the foreground. The character remains limited to the safe area around the bus.", "Los atajos solo se capturan cuando OMSI está en primer plano. El personaje permanece limitado al área segura alrededor del autobús.", "Tastenkürzel werden nur erfasst, wenn OMSI im Vordergrund ist. Der Charakter bleibt auf den sicheren Bereich um den Bus begrenzt.", "Les raccourcis ne sont capturés que lorsque OMSI est au premier plan. Le personnage reste limité à la zone sûre autour du bus.")}</p>
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

    const useRelay = multiplayer.relayEnabled && !multiplayer.hostRunning;
    const activeServer = useRelay
      ? (multiplayer.serverUrl || multiplayer.relayServerUrl || serverUrl)
      : (multiplayer.inviteAddresses[0] || multiplayer.serverUrl || serverUrl);

    if (!activeServer) return null;

    const lines = [
      "NAVBR_INVITE_V1",
      `server=${activeServer}`,
      `room=${multiplayer.roomId}`
    ];
    if (!useRelay) {
      lines.push(`port=${multiplayer.hostPort ?? 27730}`);
    }
    lines.push(`mode=${useRelay ? "relay" : "peer-host"}`);
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
      if (mode === "relay") {
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
          <p>{pick("Estado real da sala, jogadores, chat, voz e personagem vindo do controlador C#.", "Real room, players, chat, voice and character state from the C# controller.", "Estado real de sala, jugadores, chat, voz y personaje desde el controlador C#.", "Echter Raum-, Spieler-, Chat-, Sprach- und Charakterstatus aus dem C#-Controller.", "État réel de la salle, des joueurs, du chat, de la voix et du personnage depuis le contrôleur C#.")}</p>
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
        <div className="metric"><small>HOST</small><strong>{multiplayer.hostRunning ? `TCP ${multiplayer.hostPort ?? 27730}` : pick("Local inativo", "Local inactive", "Local inactivo", "Lokal inaktiv", "Local inactif")}</strong></div>
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
            <SessionMap points={multiplayer.sessionPoints} />
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
        <section className="card mp-panel">
          <div className="section-heading">
            <div><span className="eyebrow">{pick("SALA", "ROOM", "SALA", "RAUM", "SALLE")}</span><h3>{pick("Conexão e host", "Connection and host", "Conexión y host", "Verbindung und Host", "Connexion et hôte")}</h3></div>
            <button className="button ghost" onClick={onOpenNetwork}>{pick("Rede / Firewall", "Network / Firewall", "Red / Firewall", "Netzwerk / Firewall", "Réseau / Pare-feu")}</button>
          </div>

          <div className="room-form-grid">
            <label>
              <span>{pick("Servidor", "Server", "Servidor", "Server", "Serveur")}</span>
              <input value={serverUrl} onChange={event => setServerUrl(event.target.value)} disabled={multiplayer.connected} placeholder="http://127.0.0.1:27730" />
            </label>
            <label>
              <span>{pick("Sala", "Room", "Sala", "Raum", "Salle")}</span>
              <input value={roomId} onChange={event => setRoomId(event.target.value)} disabled={multiplayer.connected} placeholder="navbr-1234" />
            </label>
            <label>
              <span>{pick("Apelido", "Display name", "Apodo", "Anzeigename", "Pseudo")}</span>
              <input value={displayName} onChange={event => setDisplayName(event.target.value)} disabled={multiplayer.connected} placeholder="Driver" />
            </label>
          </div>

          <div className="room-privacy-row">
            <label className="privacy-toggle">
              <input
                type="checkbox"
                checked={privateRoom}
                disabled={multiplayer.connected}
                onChange={event => setPrivateRoom(event.target.checked)}
              />
              <span>{pick("Criar sala privada", "Create private room", "Crear sala privada", "Privaten Raum erstellen", "Créer une salle privée")}</span>
            </label>
            <label className="password-field">
              <span>{pick("Senha da sala", "Room password", "Contraseña de sala", "Raumpasswort", "Mot de passe de la salle")}</span>
              <input
                type="password"
                value={roomPassword}
                disabled={multiplayer.connected}
                onChange={event => setRoomPassword(event.target.value)}
                placeholder={privateRoom ? pick("Mínimo 4 caracteres", "Minimum 4 characters", "Mínimo 4 caracteres", "Mindestens 4 Zeichen", "Minimum 4 caractères") : pick("Use ao entrar em sala privada", "Use when joining a private room", "Usar al entrar en sala privada", "Beim Beitritt zu privatem Raum verwenden", "À utiliser pour rejoindre une salle privée")}
              />
            </label>
          </div>

          <div className="room-privacy-row">
            <label className="privacy-toggle">
              <input
                type="checkbox"
                checked={relayEnabled}
                disabled={multiplayer.connected || multiplayer.hostRunning}
                onChange={event => {
                  const enabled = event.target.checked;
                  setRelayEnabled(enabled);
                  sendCommand("configureRelay", { enabled, relayServerUrl });
                }}
              />
              <span>{pick("Usar relay de aplicação (experimental)", "Use application relay (experimental)", "Usar relay de aplicación (experimental)", "Anwendungs-Relay verwenden (experimentell)", "Utiliser le relais applicatif (expérimental)")}</span>
            </label>
            <label className="password-field">
              <span>{pick("Servidor relay", "Relay server", "Servidor relay", "Relay-Server", "Serveur relais")}</span>
              <input
                value={relayServerUrl}
                disabled={!relayEnabled || multiplayer.connected || multiplayer.hostRunning}
                onChange={event => setRelayServerUrl(event.target.value)}
                onBlur={() => sendCommand("configureRelay", { enabled: relayEnabled, relayServerUrl })}
                placeholder="https://relay.example"
              />
            </label>
          </div>

          <div className="room-actions">
            {!multiplayer.connected ? (
              <>
                <button className="button primary" onClick={() => sendCommand("connectRoom", { serverUrl, roomId, displayName, roomPassword })}>{pick("Entrar na sala", "Join room", "Entrar en sala", "Raum beitreten", "Rejoindre la salle")}</button>
                <button className="button ghost" onClick={() => sendCommand("createLocalRoom", { roomId, displayName, isPrivate: privateRoom, roomPassword, useRelay: relayEnabled, relayServerUrl })}>
                  {relayEnabled ? pick("Criar sala via relay", "Create room via relay", "Crear sala vía relay", "Raum über Relay erstellen", "Créer la salle via relais") : pick("Criar sala local", "Create local room", "Crear sala local", "Lokalen Raum erstellen", "Créer une salle locale")}
                </button>
              </>
            ) : (
              <button className="button ghost danger" onClick={() => sendCommand(multiplayer.hostRunning ? "stopLocalHost" : "disconnectRoom")}>
                {multiplayer.hostRunning ? pick("Encerrar sala local", "Stop local room", "Cerrar sala local", "Lokalen Raum beenden", "Fermer la salle locale") : pick("Desconectar", "Disconnect", "Desconectar", "Trennen", "Déconnecter")}
              </button>
            )}
          </div>

          <div className="details-grid room-status-grid">
            <div><small>{pick("SERVIDOR ATIVO", "ACTIVE SERVER", "SERVIDOR ACTIVO", "AKTIVER SERVER", "SERVEUR ACTIF")}</small><strong>{multiplayer.serverUrl || "—"}</strong></div>
            <div><small>{pick("ID DA SALA", "ROOM ID", "ID DE SALA", "RAUM-ID", "ID DE SALLE")}</small><strong>{multiplayer.roomId || "—"}</strong></div>
            <div><small>{pick("APELIDO", "DISPLAY NAME", "APODO", "ANZEIGENAME", "PSEUDO")}</small><strong>{multiplayer.displayName || "—"}</strong></div>
            <div><small>{pick("ESTADO", "STATE", "ESTADO", "STATUS", "ÉTAT")}</small><strong>{statusLabel}</strong></div>
          </div>

          <div className="room-actions">
            <button className="button ghost" disabled={!multiplayer.connected} onClick={copyInvite}>📋 {pick("Copiar convite", "Copy invite", "Copiar invitación", "Einladung kopieren", "Copier l’invitation")}</button>
            <button className="button ghost" disabled={multiplayer.connected || multiplayer.hostRunning} onClick={pasteInvite}>📥 {pick("Colar convite", "Paste invite", "Pegar invitación", "Einladung einfügen", "Coller l’invitation")}</button>
          </div>
          {inviteNotice && <div className="migration-note">{inviteNotice}</div>}

          {multiplayer.inviteAddresses.length > 0 && (
            <div className="invite-box">
              <small>{pick("ENDEREÇOS PARA CONVITE", "INVITE ADDRESSES", "DIRECCIONES DE INVITACIÓN", "EINLADUNGSADRESSEN", "ADRESSES D’INVITATION")}</small>
              {multiplayer.inviteAddresses.map(address => <code key={address}>{address}</code>)}
            </div>
          )}
          <div className="public-room-browser">
            <div className="section-heading">
              <div>
                <span className="eyebrow">{pick("SALAS PÚBLICAS", "PUBLIC ROOMS", "SALAS PÚBLICAS", "ÖFFENTLICHE RÄUME", "SALLES PUBLIQUES")}</span>
                <h3>{pick("Encontrar operação ativa", "Find active operation", "Encontrar operación activa", "Aktiven Betrieb finden", "Trouver une opération active")}</h3>
              </div>
              <button className="button ghost" onClick={() => sendCommand("refreshPublicRooms", { serverUrl })}>{pick("Atualizar salas", "Refresh rooms", "Actualizar salas", "Räume aktualisieren", "Actualiser les salles")}</button>
            </div>

            <input
              className="room-search"
              value={roomSearch}
              onChange={event => setRoomSearch(event.target.value)}
              placeholder={pick("Buscar por sala, mapa, versão, ônibus ou HOF", "Search by room, map, version, bus or HOF", "Buscar por sala, mapa, versión, autobús o HOF", "Nach Raum, Karte, Version, Bus oder HOF suchen", "Rechercher par salle, carte, version, bus ou HOF")}
            />

            {state?.roomDirectory.error && (
              <div className="directory-error">{state.roomDirectory.error}</div>
            )}

            <div className="public-room-list">
              {publicRooms.length === 0 ? (
                <div className="empty-state">
                  {pick("Nenhuma sala pública carregada. Use “Atualizar salas” para consultar o servidor.", "No public room loaded. Use “Refresh rooms” to query the server.", "No hay salas públicas cargadas. Usa “Actualizar salas” para consultar el servidor.", "Keine öffentlichen Räume geladen. Nutze „Räume aktualisieren“, um den Server abzufragen.", "Aucune salle publique chargée. Utilisez « Actualiser les salles » pour interroger le serveur.")}
                </div>
              ) : publicRooms.map(room => (
                <div className="public-room-row" key={room.roomId}>
                  <button
                    className={`favorite-button ${room.favorite ? "active" : ""}`}
                    onClick={() => sendCommand("toggleRoomFavorite", { roomId: room.roomId })}
                    title={room.favorite ? pick("Remover dos favoritos", "Remove from favorites", "Quitar de favoritos", "Aus Favoriten entfernen", "Retirer des favoris") : pick("Adicionar aos favoritos", "Add to favorites", "Añadir a favoritos", "Zu Favoriten hinzufügen", "Ajouter aux favoris")}
                  >
                    {room.favorite ? "★" : "☆"}
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
                    <small>
                      NavBR {room.navbrVersion || "—"} · OMSI {room.omsiVersion || "—"} · Plugin {room.pluginProtocolVersion || "—"}
                    </small>
                    <em className={`compatibility-badge ${room.compatibility}`}>
                      {room.compatibility === "compatible" ? pick("Compatível", "Compatible", "Compatible", "Kompatibel", "Compatible") : room.compatibility === "warning" ? pick("Compatibilidade parcial", "Partial compatibility", "Compatibilidad parcial", "Teilweise kompatibel", "Compatibilité partielle") : pick("Requer ajuste local", "Requires local adjustment", "Requiere ajuste local", "Lokale Anpassung erforderlich", "Nécessite un ajustement local")}
                    </em>
                    {room.compatibilityIssues.length > 0 && (
                      <small className="compatibility-detail">{room.compatibilityIssues[0]}</small>
                    )}
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
          </div>

          <p className="migration-note">
            {pick("Salas públicas e privadas usam a ponte React. Firewall, NAT/UPnP e diagnósticos de rede usam a aba Rede React e os serviços C# nativos.", "Public and private rooms use the React bridge. Firewall, NAT/UPnP and network diagnostics use the React Network tab backed by native C# services.", "Las salas públicas y privadas usan el puente React. Firewall, NAT/UPnP y diagnósticos de red usan la pestaña Red de React y servicios C# nativos.", "Öffentliche und private Räume nutzen die React-Bridge. Firewall, NAT/UPnP und Netzwerkdiagnose laufen über den React-Netzwerktab mit nativen C#-Diensten.", "Les salles publiques et privées utilisent le bridge React. Pare-feu, NAT/UPnP et diagnostics réseau utilisent l’onglet Réseau React avec les services C# natifs.")}
          </p>
        </section>
      )}

      {tab === "players" && (
        <section className="card mp-panel">
          <div className="section-heading">
            <div><span className="eyebrow">{pick("JOGADORES", "PLAYERS", "JUGADORES", "SPIELER", "JOUEURS")}</span><h3>{visiblePlayers.length} {pick("na sessão", "in session", "en sesión", "in Sitzung", "dans la session")}</h3></div>
          </div>
          {visiblePlayers.length === 0 ? (
            <div className="empty-state">{pick("Nenhum jogador remoto disponível.", "No remote player available.", "Ningún jugador remoto disponible.", "Kein Remote-Spieler verfügbar.", "Aucun joueur distant disponible.")}</div>
          ) : (
            <div className="players-table">
              {visiblePlayers.map(player => (
                <div className="player-row" key={player.playerId}>
                  <span className={`avatar-dot ${player.roleplayActive ? "rp" : ""}`}>{player.displayName.slice(0, 1).toUpperCase()}</span>
                  <div className="player-main">
                    <strong>{player.displayName}</strong>
                    <small>{player.mapName || pick("Mapa não informado", "Map not provided", "Mapa no informado", "Karte nicht angegeben", "Carte non renseignée")} · {player.roleplayActive ? pick("Personagem / RP", "Character / RP", "Personaje / RP", "Charakter / RP", "Personnage / RP") : pick("No ônibus", "In the bus", "En el autobús", "Im Bus", "Dans le bus")}</small>
                  </div>
                  <span className={`voice-state ${player.speaking ? "speaking" : ""}`}>{player.speaking ? pick("Falando", "Speaking", "Hablando", "Spricht", "Parle") : player.voiceEnabled ? pick("Voz ativa", "Voice active", "Voz activa", "Sprache aktiv", "Voix active") : pick("Sem voz", "No voice", "Sin voz", "Keine Sprache", "Sans voix")}</span>
                  <span className="latency">{player.latencyMs == null ? "—" : `${player.latencyMs} ms`}</span>
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
            <h3>{multiplayer.voiceEnabled ? pick("Voz habilitada", "Voice enabled", "Voz habilitada", "Sprache aktiviert", "Voix activée") : pick("Voz desativada", "Voice disabled", "Voz desactivada", "Sprache deaktiviert", "Voix désactivée")}</h3>

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
            <p>{pick("Presets, tema, escala, opacidade e módulos são configurados na interface React.", "Presets, theme, scale, opacity and modules are configured in the React interface.", "Presets, tema, escala, opacidad y módulos se configuran en la interfaz React.", "Presets, Thema, Skalierung, Deckkraft und Module werden in der React-Oberfläche konfiguriert.", "Les presets, le thème, l’échelle, l’opacité et les modules se configurent dans l’interface React.")}</p>
            <button className="button ghost" onClick={onOpenHud}>{pick("Configurar HUD", "Configure HUD", "Configurar HUD", "HUD konfigurieren", "Configurer le HUD")}</button>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">{pick("REDE", "NETWORK", "RED", "NETZWERK", "RÉSEAU")}</span>
            <h3>{pick("Host local", "Local host", "Host local", "Lokaler Host", "Hôte local")}</h3>
            <p>{multiplayer.hostRunning ? `${pick("Escutando na porta TCP", "Listening on TCP port", "Escuchando en el puerto TCP", "Lauscht auf TCP-Port", "Écoute sur le port TCP")} ${multiplayer.hostPort ?? 27730}.` : pick("Host local não está ativo.", "Local host is not active.", "El host local no está activo.", "Lokaler Host ist nicht aktiv.", "L’hôte local n’est pas actif.")}</p>
            <button className="button ghost" onClick={onOpenNetwork}>{pick("Abrir Configurações", "Open Settings", "Abrir Configuración", "Einstellungen öffnen", "Ouvrir les paramètres")} &gt; {pick("Rede", "Network", "Red", "Netzwerk", "Réseau")}</button>
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
  const { t } = useI18n();
  const ghost: NavBrGhostState | undefined = state?.ghost;
  const [recordName, setRecordName] = useState("");
  const [playbackSpeed, setPlaybackSpeed] = useState(1);
  const [loop, setLoop] = useState(false);

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
              ● {t("ghost.recordTrip")}
            </button>
            <button
              className="button ghost"
              disabled={!ghost.recording}
              onClick={() => sendCommand("stopGhostRecording")}
            >
              ■ {t("ghost.stopSave")}
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
              ▶ {t("ghost.play")}
            </button>
            <button
              className="button ghost"
              disabled={!ghost.playing}
              onClick={() => sendCommand("stopGhostPlayback")}
            >
              ■ {t("ghost.stopPlayback")}
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
                <button
                  key={item.fileName}
                  className={`ghost-library-row ${item.selected ? "selected" : ""}`}
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
              ))}
            </div>
          )}

          {ghost.libraryInvalidCount > 0 && ghost.library.length > 0 && (
            <p className="migration-note">{t("ghost.invalidIgnored", { count: ghost.libraryInvalidCount })}</p>
          )}
        </article>

        <article className="card ghost-route-card">
          <div className="section-heading">
            <div><span className="eyebrow">{t("ghost.preview")}</span><h3>{t("ghost.recordedRoute")}</h3></div>
            <span className="route-source-pill">READ-ONLY</span>
          </div>
          <GhostRoutePreview points={selected?.routePoints || []} />
          <p className="migration-note">{t("ghost.previewNote")}</p>
        </article>
      </section>
    </>
  );
}

export default function App() {
  const [state, setState] = useState<NavBrState | null>(null);
  const [screen, setScreen] = useState<Screen>("home");
  const [commandError, setCommandError] = useState<string | null>(null);
  const [settingsTabRequest, setSettingsTabRequest] = useState<SettingsTab | null>(null);

  const openSettingsTab = (tab: SettingsTab) => {
    setSettingsTabRequest(tab);
    setScreen("settings");
  };

  useEffect(() => subscribeToNavBrState(
    next => {
      setState(next);
      setCommandError(null);

      const requested = next.navigationRequest?.screen;
      if (requested === "settings-installations") {
        setSettingsTabRequest("installations");
        setScreen("settings");
      } else if (requested && [
        "home",
        "navigation",
        "roleplay",
        "ghost",
        "operations",
        "companyNetwork",
        "hardware",
        "settings",
        "multiplayer"
      ].includes(requested)) {
        setScreen(requested as Screen);
      }
    },
    setCommandError
  ), []);

  return (
    <I18nProvider cultureName={state?.cultureName} languages={state?.supportedLanguages}>
    <div className="app-shell">
      <Sidebar screen={screen} setScreen={setScreen} />
      <main>
        {screen === "home"
          ? <Home state={state} />
          : screen === "navigation"
            ? <Navigation state={state} />
            : screen === "roleplay"
              ? <Roleplay state={state} error={commandError} />
              : screen === "ghost"
                ? <GhostReplay state={state} error={commandError} />
              : screen === "operations"
                ? <Operations state={state} error={commandError} onNavigate={setScreen} />
              : screen === "companyNetwork"
                ? <CompanyNetwork state={state} error={commandError} />
                : screen === "hardware"
                ? <Hardware state={state} error={commandError} />
                : screen === "settings"
                  ? <Settings state={state} error={commandError} requestedTab={settingsTabRequest} />
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
