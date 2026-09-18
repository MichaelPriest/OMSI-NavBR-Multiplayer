import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
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
  return (
    <aside className="sidebar">
      <div className="brand">
        <div className="brand-mark">N</div>
        <div><strong>NavBR</strong><span>OMSI Multiplayer</span></div>
      </div>
      <nav className="nav">
        <button className={`nav-item ${screen === "home" ? "active" : ""}`} onClick={() => setScreen("home")}>
          <b>⌂</b><span>Início</span>
        </button>
        <button className={`nav-item ${screen === "navigation" ? "active" : ""}`} onClick={() => setScreen("navigation")}>
          <b>⌖</b><span>Navegação</span>
        </button>
        <button className={`nav-item ${screen === "multiplayer" ? "active" : ""}`} onClick={() => setScreen("multiplayer")}>
          <b>◉</b><span>Multiplayer</span>
        </button>
        <button className={`nav-item ${screen === "roleplay" ? "active" : ""}`} onClick={() => setScreen("roleplay")}>
          <b>♙</b><span>Personagem / RP</span>
        </button>
        <button className={`nav-item ${screen === "ghost" ? "active" : ""}`} onClick={() => setScreen("ghost")}>
          <b>◈</b><span>Ghost / Replay</span>
        </button>
        <button className={`nav-item ${screen === "operations" ? "active" : ""}`} onClick={() => setScreen("operations")}>
          <b>▣</b><span>CCO</span>
        </button>
        <button className={`nav-item ${screen === "companyNetwork" ? "active" : ""}`} onClick={() => setScreen("companyNetwork")}>
          <b>◎</b><span>Rede da empresa</span>
        </button>
        <button className={`nav-item ${screen === "hardware" ? "active" : ""}`} onClick={() => setScreen("hardware")}>
          <b>⚡</b><span>Hardware Cockpit</span>
        </button>
        <button className={`nav-item ${screen === "settings" ? "active" : ""}`} onClick={() => setScreen("settings")}>
          <b>⚙</b><span>Configurações</span>
        </button>
      </nav>
      <div className="sidebar-footer">
        <i />
        <div><strong>Alpha.14</strong><small>React + WebView2</small></div>
      </div>
    </aside>
  );
}

function Home({ state }: { state: NavBrState | null }) {
  const omsi = state?.omsi;
  const telemetry = state?.telemetry;
  const active = Boolean(omsi?.running && telemetry?.inGame);

  return (
    <>
      <header className="topbar">
        <div>
          <span className="eyebrow">CENTRAL OPERACIONAL</span>
          <h1>Boa viagem.</h1>
          <p>Acompanhe o OMSI e continue sua operação pelo NavBR.</p>
        </div>
        <div className="top-actions">
          <button className="button ghost" onClick={() => sendCommand("refreshState")}>Atualizar</button>
          <button className="button primary" disabled={Boolean(omsi?.running)} onClick={() => sendCommand("launchOmsi")}>
            {omsi?.running ? "OMSI aberto" : "Executar OMSI"}
          </button>
        </div>
      </header>

      <section className="hero card">
        <div>
          <span className={`badge ${omsi?.running ? "online" : ""}`}>
            {active ? "Operação ativa" : omsi?.running ? "OMSI detectado" : "Aguardando OMSI"}
          </span>
          <h2>{active ? telemetry?.mapName || "Viagem em andamento" : omsi?.running ? "OMSI está aberto" : "Nenhuma operação ativa"}</h2>
          <p>
            {active
              ? "Telemetria recebida diretamente do cliente NavBR."
              : omsi?.running
                ? "Aguardando o OMSI entrar em uma viagem com telemetria disponível."
                : "Abra o OMSI para iniciar a telemetria e carregar os dados reais da viagem."}
          </p>
          {active && (
            <div className="operation-strip">
              <span><small>LINHA</small><strong>{telemetry?.line || "—"}</strong></span>
              <span><small>ROTA</small><strong>{telemetry?.route || "—"}</strong></span>
              <span><small>DESTINO</small><strong>{telemetry?.destinationName || "—"}</strong></span>
              <span><small>PRÓXIMA PARADA</small><strong>{telemetry?.nextStopName || "—"}</strong></span>
            </div>
          )}
        </div>
        <div className="speed-panel">
          <span>VELOCIDADE</span>
          <strong>{telemetry ? format(telemetry.speedKph, 0) : "--"}</strong>
          <small>km/h</small>
        </div>
      </section>

      <section className="status-grid">
        <article className="card status-card">
          <span className="card-label">OMSI</span>
          <strong>{omsi?.running ? "Em execução" : "Não detectado"}</strong>
          <small>{omsi?.version ? `Versão ${omsi.version}` : "Versão —"}</small>
        </article>
        <article className="card status-card">
          <span className="card-label">MAPA</span>
          <strong>{telemetry?.mapName || "—"}</strong>
          <small>{telemetry ? `X ${format(telemetry.x, 2)} · Y ${format(telemetry.y, 2)}` : "Posição indisponível"}</small>
        </article>
        <article className="card status-card">
          <span className="card-label">MULTIPLAYER</span>
          <strong>{state?.multiplayer.connected ? state.multiplayer.roomId : "Desconectado"}</strong>
          <small>{state?.multiplayer.connected ? `${state.multiplayer.playerCount} jogador(es)` : "Nenhuma sala ativa"}</small>
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

const maneuverLabel = (maneuver: string) => {
  switch (maneuver) {
    case "SlightLeft": return { arrow: "↖", title: "Mantenha à esquerda" };
    case "Left": return { arrow: "←", title: "Vire à esquerda" };
    case "SharpLeft": return { arrow: "↙", title: "Curva forte à esquerda" };
    case "SlightRight": return { arrow: "↗", title: "Mantenha à direita" };
    case "Right": return { arrow: "→", title: "Vire à direita" };
    case "SharpRight": return { arrow: "↘", title: "Curva forte à direita" };
    case "RejoinRoute": return { arrow: "↺", title: "Retorne para a rota" };
    default: return { arrow: "↑", title: "Siga em frente" };
  }
};

function NavigationMap({ navigation }: { navigation: NavBrNavigationState }) {
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
        <button className={mode === "follow" ? "active" : ""} onClick={() => setMode("follow")}>Seguir ônibus</button>
        <button className={mode === "full" ? "active" : ""} onClick={() => setMode("full")}>Rota completa</button>
      </div>

      {!geometry ? (
        <div className="map-center-message navigation-empty">
          <strong>Rota ainda não resolvida</strong>
          <span>O NavBR só desenha o trajeto quando encontra geometria real da rota ativa nos arquivos do mapa OMSI.</span>
        </div>
      ) : (
        <svg viewBox={geometry.viewBox} preserveAspectRatio="xMidYMid meet" aria-label="Roadmap da rota ativa">
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
        <span><i className="route" /> Rota OMSI</span>
        <span><i className="stop" /> Paradas</span>
        <span><i className="bus" /> Seu ônibus</span>
      </div>
    </div>
  );
}

function Navigation({ state }: { state: NavBrState | null }) {
  const navigation = state?.navigation;
  const telemetry = state?.telemetry;
  const maneuver = maneuverLabel(navigation?.maneuver || "None");
  const routeActive = Boolean(navigation?.available);

  if (!navigation) {
    return <div className="card empty-state">Aguardando estado de navegação…</div>;
  }

  return (
    <>
      <header className="topbar navigation-header">
        <div>
          <span className="eyebrow">GPS / ROADMAP</span>
          <h1>Navegação</h1>
          <p>Rota, paradas e orientação calculadas a partir do mapa e da viagem reais do OMSI.</p>
        </div>
        <div className="top-actions">
          <span className={`connection-pill ${routeActive ? "connected" : ""}`}>
            <i /> {routeActive ? navigation.isOnRoute ? "Na rota" : "Fora da rota" : "Sem rota"}
          </span>
          <button className="button ghost" onClick={() => sendCommand("openNavigation3D")}>Mapa 3D</button>
        </div>
      </header>

      <section className="navigation-metrics">
        <div className="metric"><small>LINHA</small><strong>{navigation.line || telemetry?.line || "—"}</strong></div>
        <div className="metric"><small>ROTA</small><strong>{navigation.route || telemetry?.route || "—"}</strong></div>
        <div className="metric"><small>DESTINO</small><strong>{navigation.destinationName || "—"}</strong></div>
        <div className="metric"><small>PROGRESSO</small><strong>{routeActive ? `${format(navigation.routeProgressPercent, 0)}%` : "—"}</strong></div>
        <div className="metric"><small>RESTANTE</small><strong>{routeActive ? formatDistance(navigation.distanceRemainingMeters) : "—"}</strong></div>
      </section>

      <section className="navigation-layout">
        <article className="card navigation-map-card">
          <div className="section-heading">
            <div>
              <span className="eyebrow">MAPA DA ROTA</span>
              <h3>{navigation.mapName || telemetry?.mapName || "Mapa OMSI"}</h3>
            </div>
            <span className="route-source-pill">GEOMETRIA OMSI</span>
          </div>
          <NavigationMap navigation={navigation} />
        </article>

        <aside className="navigation-side">
          <article className={`card maneuver-card ${navigation.maneuver === "RejoinRoute" ? "warning" : ""}`}>
            <span className="eyebrow">{navigation.maneuver === "RejoinRoute" ? "CORREÇÃO DE ROTA" : "PRÓXIMA MANOBRA"}</span>
            <div className="maneuver-main">
              <strong>{maneuver.arrow}</strong>
              <div>
                <h3>{maneuver.title}</h3>
                <p>
                  {navigation.maneuver === "RejoinRoute"
                    ? formatDistance(navigation.offRouteDistanceMeters)
                    : navigation.distanceToManeuverMeters != null
                      ? `em ${formatDistance(navigation.distanceToManeuverMeters)}`
                      : navigation.currentStreetName || "Continue pela rota"}
                </p>
              </div>
            </div>
          </article>

          <article className="card next-stop-card">
            <span className="eyebrow">PRÓXIMA PARADA</span>
            <h3>{navigation.nextStopName || "—"}</h3>
            <div className="next-stop-stats">
              <span><small>DISTÂNCIA</small><strong>{formatDistance(navigation.distanceToNextStopMeters)}</strong></span>
              <span><small>ETA</small><strong>{formatEta(navigation.etaToNextStopSeconds)}</strong></span>
            </div>
          </article>

          <article className="card route-progress-card">
            <div className="section-heading compact">
              <div><span className="eyebrow">VIAGEM</span><h3>{navigation.destinationName || "Destino não informado"}</h3></div>
            </div>
            <div className="route-progress-track"><i style={{ width: `${Math.max(0, Math.min(100, navigation.routeProgressPercent))}%` }} /></div>
            <div className="route-progress-meta">
              <span>{formatDistance(navigation.distanceRemainingMeters)} restantes</span>
              <span>{formatEta(navigation.etaToRouteEndSeconds)}</span>
            </div>
            {navigation.currentStreetName && <p className="current-street">Agora: {navigation.currentStreetName}</p>}
          </article>
        </aside>
      </section>

      <section className="card upcoming-stops-card">
        <div className="section-heading">
          <div><span className="eyebrow">ITINERÁRIO</span><h3>Próximas paradas</h3></div>
          <span className="stop-count">{navigation.stopSequence.totalStops || 0} paradas na rota</span>
        </div>
        {navigation.stopSequence.upcomingStops.length === 0 ? (
          <div className="empty-state compact-empty">Sequência de paradas ainda não resolvida para esta viagem.</div>
        ) : (
          <div className="upcoming-stops">
            {navigation.stopSequence.upcomingStops.map((stop, index) => (
              <div className={index === 0 ? "next" : ""} key={`${stop}-${index}`}>
                <span>{navigation.stopSequence.nextStopIndex != null ? navigation.stopSequence.nextStopIndex + index + 1 : index + 1}</span>
                <strong>{stop}</strong>
                {index === 0 && <small>PRÓXIMA</small>}
              </div>
            ))}
          </div>
        )}
      </section>
    </>
  );
}

function SessionMap({ points }: { points: NavBrSessionPoint[] }) {
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
          <strong>Sem posições válidas</strong>
          <span>Ônibus e personagens só aparecem quando há telemetria real, recente e compatível.</span>
        </div>
      </div>
    );
  }

  return (
    <div className="session-map-real">
      <div className="map-grid-lines" />
      <svg viewBox="0 0 100 100" role="img" aria-label="Mapa relativo da sessão">
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
            <text className="session-label">{point.isLocal ? "Você" : point.displayName}</text>
            <text y="3.3" className="session-label-detail">
              {point.kind === "roleplay"
                ? `${point.activity || "RP"} · ${format(point.speedKph, 0)} km/h`
                : `${point.line ? `Linha ${point.line} · ` : ""}${format(point.speedKph, 0)} km/h`}
            </text>
          </g>
        ))}
      </svg>
      <div className="map-legend">
        <span><i className="legend-local" /> Você</span>
        <span><i className="legend-bus" /> Ônibus</span>
        <span><i className="legend-rp" /> Personagem</span>
      </div>
    </div>
  );
}


type OperationsTab = "overview" | "drivers" | "reports" | "company";

function formatDelay(seconds: number | undefined | null) {
  if (seconds == null || !Number.isFinite(seconds)) return "—";
  const abs = Math.abs(Math.round(seconds));
  const minutes = Math.floor(abs / 60);
  const remainder = abs % 60;
  const value = minutes > 0 ? `${minutes}m ${remainder.toString().padStart(2, "0")}s` : `${remainder}s`;
  return seconds > 0 ? `+${value}` : seconds < 0 ? `-${value}` : "No horário";
}

function reportSeverityLabel(severity: string) {
  if (severity === "Critical") return "Crítica";
  if (severity === "Attention") return "Atenção";
  return "Informativa";
}

function reportStatusLabel(status: string) {
  if (status === "Acknowledged") return "Reconhecida";
  if (status === "Resolved") return "Resolvida";
  return "Aberta";
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
    return <div className="card empty-state">Aguardando dados do CCO…</div>;
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
          <span className="eyebrow">CENTRO DE CONTROLE OPERACIONAL</span>
          <h1>CCO</h1>
          <p>Operação local, motoristas da sessão e ocorrências recebidas pelo backend NavBR.</p>
        </div>
        <div className="top-actions">
          <span className={`connection-pill ${operations.connected ? "connected" : ""}`}>
            <i /> {operations.connected ? operations.roomId || "Sessão ativa" : "Sem sessão"}
          </span>
          <button className="button ghost" onClick={() => onNavigate("multiplayer")}>Multiplayer</button>
        </div>
      </header>

      {error && <div className="command-error">{error}</div>}

      <section className="cco-metrics">
        <div className="metric"><small>MOTORISTAS REMOTOS</small><strong>{operations.drivers.length}</strong></div>
        <div className="metric"><small>OCORRÊNCIAS ABERTAS</small><strong>{activeReports.length}</strong></div>
        <div className="metric"><small>CRÍTICAS</small><strong>{criticalReports.length}</strong></div>
        <div className="metric"><small>ATRASO &gt; 2 MIN</small><strong>{delayedDrivers.length}</strong></div>
        <div className="metric"><small>SEM TELEMETRIA</small><strong>{staleDrivers.length}</strong></div>
      </section>

      <div className="mp-tabs cco-tabs" role="tablist">
        {([
          ["overview", "Visão geral"],
          ["drivers", "Motoristas"],
          ["reports", "Ocorrências"],
          ["company", "Empresa / Frota"]
        ] as [OperationsTab, string][]).map(([key, label]) => (
          <button key={key} className={tab === key ? "active" : ""} onClick={() => setTab(key)}>{label}</button>
        ))}
      </div>

      {tab === "overview" && (
        <section className="cco-overview-grid">
          <article className="card cco-map-card">
            <div className="section-heading">
              <div>
                <span className="eyebrow">SESSÃO OPERACIONAL</span>
                <h3>{local?.mapName || state?.telemetry?.mapName || "Sem mapa ativo"}</h3>
              </div>
              <span className={`live-pill ${operations.connected ? "" : "muted"}`}><span /> {operations.connected ? "LIVE" : "LOCAL"}</span>
            </div>
            <SessionMap points={multiplayer.sessionPoints} />
          </article>

          <aside className="cco-side-stack">
            <article className="card compact-card">
              <span className="eyebrow">OPERAÇÃO LOCAL</span>
              <h3>{local?.vehicleName || "Nenhum ônibus detectado"}</h3>
              <p>{local?.line ? `Linha ${local.line}` : "Sem linha"} · {local?.route || "Sem rota"}</p>
              <p>{local?.destination || "Destino não informado"}</p>
            </article>

            <article className="card compact-card cco-speed-card">
              <span className="eyebrow">AGORA</span>
              <div className="cco-live-values">
                <span><strong>{local ? format(local.speedKph, 0) : "—"}</strong><small>km/h</small></span>
                <span><strong>{formatDelay(local?.delaySeconds)}</strong><small>atraso</small></span>
              </div>
              <p>{local?.currentStreet || local?.nextStop || "Aguardando telemetria operacional"}</p>
            </article>

            <article className="card compact-card">
              <span className="eyebrow">EMPRESA</span>
              <h3>{operations.company.name || "Empresa não configurada"}</h3>
              <p>{operations.company.fleet.length} veículo(s) cadastrados</p>
              <button className="text-action" onClick={() => setTab("company")}>Abrir Empresa / Frota →</button>
            </article>
          </aside>
        </section>
      )}

      {tab === "drivers" && (
        <section className="card cco-panel">
          <div className="section-heading">
            <div><span className="eyebrow">MOTORISTAS</span><h3>Operação remota da sala</h3></div>
            <span className="stop-count">{operations.drivers.length} conectado(s)</span>
          </div>

          {operations.drivers.length === 0 ? (
            <div className="empty-state">Nenhum motorista remoto com telemetria real disponível.</div>
          ) : (
            <div className="drivers-table">
              {operations.drivers.map(driver => (
                <div className={`driver-row ${driver.stale ? "stale" : ""}`} key={driver.playerId}>
                  <span className="driver-avatar">{driver.displayName.slice(0, 1).toUpperCase()}</span>
                  <div className="driver-primary">
                    <strong>{driver.displayName}</strong>
                    <small>{driver.vehicleName || "Ônibus não informado"} · {driver.mapName || "Mapa —"}</small>
                  </div>
                  <div className="driver-service">
                    <strong>{driver.line || "—"}</strong>
                    <small>{driver.route || driver.destination || "Sem rota"}</small>
                  </div>
                  <div className="driver-live">
                    <strong>{format(driver.speedKph, 0)} km/h</strong>
                    <small className={(driver.delaySeconds ?? 0) > 120 ? "late" : ""}>{formatDelay(driver.delaySeconds)}</small>
                  </div>
                  <div className="driver-status">
                    {driver.latestReport ? (
                      <span className={`report-chip ${driver.latestReport.severity.toLowerCase()}`}>
                        {reportSeverityLabel(driver.latestReport.severity)}
                      </span>
                    ) : driver.stale ? (
                      <span className="report-chip stale">Sem atualização</span>
                    ) : (
                      <span className="report-chip ok">Normal</span>
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
              <span className="eyebrow">OCORRÊNCIAS</span>
              <h3>Assistência e incidentes da sessão</h3>
            </div>
            <span className={`authority-pill ${operations.canManageReports ? "enabled" : ""}`}>
              {operations.canManageReports ? "Autoridade CCO" : "Somente leitura"}
            </span>
          </div>

          {operations.reports.length === 0 ? (
            <div className="empty-state">Nenhuma ocorrência recebida nesta sessão.</div>
          ) : (
            <div className="reports-list">
              {operations.reports.map(report => (
                <article className={`report-card ${report.severity.toLowerCase()} ${report.status.toLowerCase()}`} key={report.reportId}>
                  <div className="report-card-top">
                    <div>
                      <span className={`report-chip ${report.severity.toLowerCase()}`}>{reportSeverityLabel(report.severity)}</span>
                      <strong>{report.displayName}</strong>
                    </div>
                    <span className="report-status">{reportStatusLabel(report.status)}</span>
                  </div>
                  <h4>{report.kind === "Incident" ? "Incidente" : "Pedido de assistência"}</h4>
                  <p>{report.message || "Sem mensagem adicional."}</p>
                  <small>{new Date(report.updatedAtUtc).toLocaleString()}</small>

                  {operations.canManageReports && report.status !== "Resolved" && (
                    <div className="report-actions">
                      {report.status === "Open" && (
                        <button className="button ghost compact" onClick={() => sendCommand("acknowledgeOperationalReport", { reportId: report.reportId })}>
                          Reconhecer
                        </button>
                      )}
                      <button className="button primary compact" onClick={() => sendCommand("resolveOperationalReport", { reportId: report.reportId })}>
                        Resolver
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
              <div><span className="eyebrow">EMPRESA VIRTUAL</span><h3>Identidade operacional</h3></div>
            </div>
            <div className="company-form">
              <label><span>Nome</span><input value={companyName} onChange={event => setCompanyName(event.target.value)} /></label>
              <label><span>Sigla</span><input value={companyShortName} onChange={event => setCompanyShortName(event.target.value)} /></label>
              <label className="wide"><span>Mapa base</span><input value={companyBaseMap} onChange={event => setCompanyBaseMap(event.target.value)} placeholder="Opcional" /></label>
            </div>
            <button className="button primary" onClick={() => sendCommand("saveCompany", {
              name: companyName,
              shortName: companyShortName,
              baseMap: companyBaseMap
            })}>Salvar empresa</button>
          </article>

          <article className="card company-card">
            <div className="section-heading">
              <div><span className="eyebrow">PERFIL</span><h3>Motorista</h3></div>
            </div>
            <div className="company-form">
              <label className="wide"><span>Nome no NavBR</span><input value={profileName} onChange={event => setProfileName(event.target.value)} /></label>
              <label className="wide"><span>Empresa do perfil</span><input value={profileCompany} onChange={event => setProfileCompany(event.target.value)} /></label>
            </div>
            <div className="profile-stats">
              <span><small>VIAGENS</small><strong>{operations.profile.trips}</strong></span>
              <span><small>DISTÂNCIA</small><strong>{operations.profile.totalDistanceKm.toFixed(1)} km</strong></span>
              <span><small>MÉDIA</small><strong>{operations.profile.averageMovingSpeedKph.toFixed(1)} km/h</strong></span>
              <span><small>MÁXIMA</small><strong>{operations.profile.highestSpeedKph.toFixed(0)} km/h</strong></span>
            </div>
            <button className="button ghost" onClick={() => sendCommand("saveDriverProfile", {
              displayName: profileName,
              companyName: profileCompany
            })}>Salvar perfil</button>
          </article>

          <article className="card fleet-card">
            <div className="section-heading">
              <div><span className="eyebrow">FROTA</span><h3>{operations.company.fleet.length} veículo(s)</h3></div>
            </div>

            <div className="register-vehicle">
              <div>
                <small>ÔNIBUS ATUAL DO OMSI</small>
                <strong>{local?.vehicleName || "Nenhum ônibus detectado"}</strong>
              </div>
              <input value={fleetNumber} onChange={event => setFleetNumber(event.target.value)} placeholder="Prefixo / número" />
              <input value={fleetLivery} onChange={event => setFleetLivery(event.target.value)} placeholder="Pintura (opcional)" />
              <button className="button primary" disabled={!local?.vehicleName} onClick={() => {
                sendCommand("registerCurrentVehicle", { fleetNumber, livery: fleetLivery });
                setFleetNumber("");
                setFleetLivery("");
              }}>Cadastrar atual</button>
            </div>

            {operations.company.fleet.length === 0 ? (
              <div className="empty-state compact-empty">Nenhum veículo cadastrado na frota.</div>
            ) : (
              <div className="fleet-list">
                {operations.company.fleet.map(vehicle => (
                  <div className="fleet-row" key={vehicle.id}>
                    <span className="fleet-number">{vehicle.fleetNumber}</span>
                    <div><strong>{vehicle.vehicleModel}</strong><small>{vehicle.livery || "Pintura não informada"}</small></div>
                    <small>{vehicle.lastUsedAt ? `Último uso: ${new Date(vehicle.lastUsedAt).toLocaleDateString()}` : "Sem uso registrado"}</small>
                    <button className="button ghost compact danger" onClick={() => sendCommand("removeFleetVehicle", { vehicleId: vehicle.id })}>Remover</button>
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

const companyRoleLabels: Record<string, string> = {
  President: "Presidente",
  VicePresident: "Vice-Presidente",
  Director: "Diretoria",
  OperationsManager: "Gerente Operacional",
  Dispatcher: "CCO / Dispatcher",
  Supervisor: "Fiscal / Supervisor",
  SeniorDriver: "Motorista Sênior",
  Driver: "Motorista",
  Trainee: "Aprendiz"
};

function CompanyMemberRow({ member, assignableRoles }: { member: NavBrCompanyMember; assignableRoles: string[] }) {
  const [role, setRole] = useState(member.role);
  useEffect(() => setRole(member.role), [member.role]);

  return (
    <div className={`company-member-row ${member.isSelf ? "self" : ""}`}>
      <span className="company-member-avatar">{member.displayName.slice(0, 1).toUpperCase()}</span>
      <div className="company-member-main">
        <strong>{member.displayName}{member.isSelf ? " · Você" : ""}</strong>
        <small>{member.playerId}{member.isOwner ? " · OWNER" : ""}</small>
      </div>
      <div className="company-member-role">
        <span>{companyRoleLabels[member.role] || member.role}</span>
        <small>{member.permissions || "Sem permissões administrativas"}</small>
      </div>
      {member.canChangeRole ? (
        <div className="company-member-actions">
          <select value={role} onChange={event => setRole(event.target.value)}>
            {assignableRoles.map(item => <option key={item} value={item}>{companyRoleLabels[item] || item}</option>)}
          </select>
          <button className="button ghost compact" disabled={role === member.role} onClick={() => sendCommand("changeCompanyMemberRole", { playerId: member.playerId, role })}>Aplicar cargo</button>
          {member.canRemove && <button className="button ghost compact danger" onClick={() => { if (window.confirm("Remover " + member.displayName + " da empresa?")) sendCommand("removeCompanyMember", { playerId: member.playerId }); }}>Remover</button>}
        </div>
      ) : <span className="company-member-locked">{member.isOwner ? "Protegido" : "Sem permissão"}</span>}
    </div>
  );
}

function CompanyNetwork({ state, error }: { state: NavBrState | null; error: string | null }) {
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

  if (!companyNetwork?.available) return <div className="card empty-state">Aguardando o runtime da Rede da Empresa…</div>;

  const company = companyNetwork.company;
  const node = companyNetwork.node;
  const canCreateInvite = Boolean(node?.running && company?.canInvite);

  return (
    <>
      <header className="topbar company-network-header">
        <div><span className="eyebrow">NAVBR COMPANY NETWORK</span><h1>Rede da empresa</h1><p>Empresa online peer-hosted, identidade assinada e equipe administrada pelo backend nativo.</p></div>
        <div className="top-actions"><span className={`connection-pill ${node?.running ? "connected" : ""}`}><i /> {node?.running ? "Company Node TCP " + node.port : companyNetwork.membership ? "Vinculado" : "Offline"}</span><button className="button ghost" onClick={() => sendCommand("refreshCompanyNetwork")}>Atualizar</button></div>
      </header>
      {error && <div className="command-error">{error}</div>}
      <section className="company-network-metrics">
        <div className="metric"><small>EMPRESA</small><strong>{company?.name || localCompany?.name || "—"}</strong></div>
        <div className="metric"><small>CARGO</small><strong>{company?.selfRole ? companyRoleLabels[company.selfRole] || company.selfRole : companyNetwork.membership?.role ? companyRoleLabels[companyNetwork.membership.role] || companyNetwork.membership.role : "—"}</strong></div>
        <div className="metric"><small>MEMBROS</small><strong>{company?.memberCount ?? 0}</strong></div>
        <div className="metric"><small>NODE</small><strong>{node?.running ? "Online" : "Offline"}</strong></div>
      </section>
      <div className="mp-tabs" role="tablist"><button className={tab === "network" ? "active" : ""} onClick={() => setTab("network")}>Rede</button><button className={tab === "team" ? "active" : ""} onClick={() => setTab("team")}>Equipe</button></div>
      {tab === "network" && (
        <section className="company-network-layout">
          <article className="card company-node-card"><div className="section-heading"><div><span className="eyebrow">IDENTIDADE NAVBR</span><h3>{companyNetwork.identity?.displayName || "Motorista"}</h3></div></div><code>{companyNetwork.identity?.playerId || "—"}</code><p>A chave privada permanece protegida no Windows e nunca é enviada ao React.</p></article>
          <article className="card company-node-card"><div className="section-heading"><div><span className="eyebrow">COMPANY NODE</span><h3>TCP 27740</h3></div><span className={`hardware-state-pill ${node?.running ? "connected" : ""}`}>{node?.running ? "ONLINE" : "OFFLINE"}</span></div><p>O nó da empresa é independente da sala multiplayer TCP 27730.</p><div className="company-node-actions">{node?.running ? <button className="button ghost danger" onClick={() => sendCommand("stopCompanyNode")}>Parar Company Node</button> : <button className="button primary" disabled={!localCompany?.name} onClick={() => sendCommand("startCompanyNode")}>Hospedar empresa neste PC</button>}</div>{!localCompany?.name && <p className="network-note">Configure primeiro a Empresa/Frota no CCO.</p>}{node?.running && <div className="company-node-addresses">{[node.localUrl, ...node.lanUrls].filter(Boolean).filter((value, index, all) => all.indexOf(value) === index).map(url => <code key={url}>{url}</code>)}</div>}</article>
          <article className="card company-join-card"><span className="eyebrow">ENTRAR EM EMPRESA ONLINE</span><h3>Convite assinado</h3><label><span>Endereço do Company Node</span><input value={nodeUrl} onChange={event => setNodeUrl(event.target.value)} placeholder="http://192.168.0.10:27740" /></label><label><span>Código do convite</span><input value={inviteCode} onChange={event => setInviteCode(event.target.value)} placeholder="NBR-...." /></label><button className="button primary" disabled={!nodeUrl.trim() || !inviteCode.trim()} onClick={() => sendCommand("joinCompany", { nodeUrl, inviteCode })}>Entrar na empresa</button></article>
          <article className="card company-invite-card"><span className="eyebrow">CONVIDAR</span><h3>Novo membro</h3><p>Convites expiram em 7 dias e são criados para um único uso.</p><label><span>Cargo inicial</span><select value={inviteRole} disabled={!canCreateInvite} onChange={event => setInviteRole(event.target.value)}>{companyNetwork.assignableRoles.map(role => <option key={role} value={role}>{companyRoleLabels[role] || role}</option>)}</select></label><button className="button ghost" disabled={!canCreateInvite} onClick={() => sendCommand("createCompanyInvite", { role: inviteRole })}>Criar convite</button>{companyNetwork.invite && <div className="company-invite-result"><strong>{companyNetwork.invite.code}</strong><pre>{companyNetwork.invite.payload}</pre><button className="button ghost compact" onClick={() => { if (companyNetwork.invite?.payload) void navigator.clipboard?.writeText(companyNetwork.invite.payload); }}>Copiar convite</button></div>}</article>
        </section>
      )}
      {tab === "team" && <section className="card company-team-card"><div className="section-heading"><div><span className="eyebrow">EQUIPE</span><h3>{company?.name || "Empresa Online"}</h3></div><span className="stop-count">{company?.memberCount ?? 0} membro(s)</span></div>{!company || company.members.length === 0 ? <div className="empty-state">Nenhum quadro de membros foi carregado. Atualize a Rede da Empresa.</div> : <div className="company-members-list">{company.members.map(member => <CompanyMemberRow key={member.playerId} member={member} assignableRoles={companyNetwork.assignableRoles} />)}</div>}</section>}
    </>
  );
}

const hardwareBaudRates = [9600, 19200, 38400, 57600, 115200, 230400];

function Hardware({ state, error }: { state: NavBrState | null; error: string | null }) {
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
    return <div className="card empty-state">Aguardando estado do Hardware Cockpit…</div>;
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
          <h1>Painel físico</h1>
          <p>Bridge serial compartilhada para Arduino, ESP32, letreiros, LEDs e computador de bordo.</p>
        </div>
        <div className="top-actions">
          <span className={`connection-pill ${hardware.connected ? "connected" : ""}`}>
            <i /> {hardware.connected ? `${hardware.portName} @ ${hardware.baudRate}` : "Serial desconectada"}
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
              {hardware.connected ? "5 Hz ativo" : "Aguardando conexão"}
            </span>
          </div>

          <div className="hardware-controls">
            <label>
              <span>Porta COM</span>
              <select
                value={portName}
                disabled={hardware.connected}
                onChange={event => {
                  const value = event.target.value;
                  setPortName(value);
                  persistSelection(value, baudRate, autoReconnect);
                }}
              >
                <option value="">Selecione…</option>
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
              <span>Reconectar automaticamente na mesma COM</span>
            </label>
          </div>

          <div className="hardware-actions">
            {hardware.connected ? (
              <button className="button ghost danger" onClick={() => sendCommand("disconnectHardware")}>Desconectar</button>
            ) : (
              <button
                className="button primary"
                disabled={!portName}
                onClick={() => sendCommand("connectHardware", { portName, baudRate, autoReconnect })}
              >
                Conectar hardware
              </button>
            )}
            <button className="button ghost" onClick={() => sendCommand("refreshState")}>Atualizar portas</button>
          </div>

          <p className="hardware-note">
            O NavBR nunca troca silenciosamente para outra porta COM. O auto-reconnect tenta apenas a porta explicitamente escolhida.
          </p>
        </article>

        <article className="card hardware-live-card">
          <div className="section-heading">
            <div><span className="eyebrow">TELEMETRIA AO VIVO</span><h3>{telemetry ? "Quadro atual" : "Aguardando OMSI"}</h3></div>
            {hardware.lastFrameSentAtUtc && <small>Último envio: {new Date(hardware.lastFrameSentAtUtc).toLocaleTimeString()}</small>}
          </div>

          <div className="hardware-live-grid">
            <span><small>LINHA</small><strong>{telemetry?.line || "—"}</strong></span>
            <span><small>DESTINO</small><strong>{telemetry?.destination || "—"}</strong></span>
            <span><small>PRÓXIMA PARADA</small><strong>{telemetry?.nextStop || "—"}</strong></span>
            <span><small>RUA ATUAL</small><strong>{telemetry?.currentStreet || "—"}</strong></span>
            <span><small>VELOCIDADE</small><strong>{telemetry ? `${format(telemetry.speedKph, 1)} km/h` : "—"}</strong></span>
            <span className={telemetry?.stopRequested ? "attention" : ""}><small>PARADA SOLICITADA</small><strong>{telemetry ? telemetry.stopRequested ? "SIM" : "Não" : "—"}</strong></span>
            <span><small>PORTAS</small><strong>{telemetry?.doors || "—"}</strong></span>
            <span><small>SETA</small><strong>{telemetry?.turnSignal || "—"}</strong></span>
          </div>
        </article>
      </section>

      <section className="card hardware-payload-card">
        <div className="section-heading">
          <div><span className="eyebrow">PREVIEW TÉCNICO</span><h3>Pacote enviado ao cockpit</h3></div>
          <span className="route-source-pill">JSON LINES</span>
        </div>
        <pre>{hardware.payloadPreview || `{"protocol":"${hardware.protocol}","state":"waiting-for-telemetry"}`}</pre>
      </section>
    </>
  );
}

type SettingsTab = "installations" | "hud" | "roadmap" | "diagnostics" | "network" | "advanced";

function OmsiProfileCard({ profile }: { profile: NavBrOmsiInstallation }) {
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
            {profile.isPreferred && <span className="install-badge preferred">Preferido</span>}
            {profile.isRunning && <span className="install-badge running">Em execução</span>}
            {!profile.executableExists && <span className="install-badge invalid">Omsi.exe ausente</span>}
          </div>
          <h3>{profile.name}</h3>
          <code>{profile.installDirectory}</code>
        </div>
        <button
          className="button primary compact"
          disabled={!profile.executableExists}
          onClick={() => sendCommand("launchOmsiProfile", { profileId: profile.id })}
        >
          {profile.isRunning ? "Ativar OMSI" : "Executar"}
        </button>
      </div>

      <div className="installation-edit-grid">
        <label>
          <span>Nome do perfil</span>
          <input value={name} onChange={event => setName(event.target.value)} />
        </label>
        <label>
          <span>Argumentos de inicialização</span>
          <input value={launchArguments} onChange={event => setLaunchArguments(event.target.value)} placeholder="Opcional" />
        </label>
      </div>

      <div className="installation-actions">
        <button className="button ghost compact" onClick={() => sendCommand("updateOmsiProfile", {
          profileId: profile.id,
          name,
          launchArguments
        })}>Salvar perfil</button>
        {!profile.isPreferred && (
          <button className="button ghost compact" onClick={() => sendCommand("setPreferredOmsiProfile", { profileId: profile.id })}>
            Tornar preferido
          </button>
        )}
        <button className="button ghost compact" onClick={() => sendCommand("openOmsiProfileFolder", { profileId: profile.id })}>
          Abrir pasta
        </button>
        <button className="button ghost compact danger" onClick={() => sendCommand("removeOmsiProfile", { profileId: profile.id })}>
          Remover
        </button>
      </div>

      {profile.lastUsedAtUtc && (
        <small className="install-last-used">Último uso: {new Date(profile.lastUsedAtUtc).toLocaleString()}</small>
      )}
    </article>
  );
}


function HudSettingsPanel({ hud }: { hud: NavBrHudState }) {
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
    ["showFuel", "Combustível"],
    ["showPedals", "Acelerador / freio"],
    ["showStatus", "Indicadores"],
    ["showMinimap", "Minimapa integrado"],
    ["showMultiplayer", "Multiplayer no painel"],
    ["showAlerts", "Alertas discretos"],
    ["showSideIndicators", "Indicadores laterais"]
  ] as const;

  return (
    <section className="hud-settings-layout">
      <article className="card hud-settings-card">
        <div className="section-heading">
          <div><span className="eyebrow">HUD</span><h3>Identidade e comportamento</h3></div>
          <span className={`hardware-state-pill ${draft.enabled ? "connected" : ""}`}>
            {draft.enabled ? "Ativo" : "Desativado"}
          </span>
        </div>

        <label className="diagnostics-toggle hud-enabled-toggle">
          <input
            type="checkbox"
            checked={draft.enabled}
            onChange={event => patch({ enabled: event.target.checked })}
          />
          <span>Exibir painel do ônibus no HUD</span>
        </label>

        <div className="hud-select-grid">
          <label className="voice-field">
            <span>Estilo</span>
            <select value={draft.preset} onChange={event => applyPreset(event.target.value)}>
              {hud.presets.map(item => <option key={item.id} value={item.id}>{item.displayName}</option>)}
            </select>
          </label>
          <label className="voice-field">
            <span>Tema</span>
            <select value={draft.theme} onChange={event => patch({ theme: event.target.value })}>
              {hud.themes.map(item => <option key={item.id} value={item.id}>{item.displayName}</option>)}
            </select>
          </label>
          <label className="voice-field">
            <span>Ancoragem</span>
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
            <span>Escala automática pela resolução</span>
          </label>
        </div>

        <div className="hud-preview-line">
          <strong>{hud.presets.find(item => item.id === draft.preset)?.displayName || draft.preset}</strong>
          <span>{Math.round(draft.width)} px · {Math.round(draft.scale * 100)}% · {Math.round(draft.opacity * 100)}%</span>
        </div>
      </article>

      <article className="card hud-settings-card">
        <span className="eyebrow">TAMANHO DO PAINEL</span>
        <div className="hud-slider-list">
          <label>
            <span><strong>Escala geral</strong><em>{Math.round(draft.scale * 100)}%</em></span>
            <input type="range" min="0.6" max="1.8" step="0.05" value={draft.scale}
              onChange={event => patch({ scale: Number(event.target.value) })} />
          </label>
          <label>
            <span><strong>Largura</strong><em>{Math.round(draft.width)} px</em></span>
            <input type="range" min="280" max="960" step="10" value={draft.width}
              onChange={event => patch({ width: Number(event.target.value) })} />
          </label>
          <label>
            <span><strong>Altura</strong><em>{draft.height < 1 ? "Automática" : `${Math.round(draft.height)} px`}</em></span>
            <input type="range" min="0" max="720" step="10" value={draft.height}
              onChange={event => patch({ height: Number(event.target.value) })} />
          </label>
          <label>
            <span><strong>Opacidade</strong><em>{Math.round(draft.opacity * 100)}%</em></span>
            <input type="range" min="0.35" max="1" step="0.05" value={draft.opacity}
              onChange={event => patch({ opacity: Number(event.target.value) })} />
          </label>
        </div>
      </article>

      <article className="card hud-settings-card">
        <span className="eyebrow">MÓDULOS VISÍVEIS</span>
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
        <span className="eyebrow">ESCALA DOS WIDGETS</span>
        <div className="hud-slider-list">
          {([
            ["minimapScale", "Minimapa"],
            ["multiplayerScale", "Multiplayer"],
            ["alertsScale", "Alertas"],
            ["sideIndicatorsScale", "Indicadores laterais"]
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
          {dirty ? "Aplicar HUD" : "HUD aplicado"}
        </button>
        <button className="button ghost" onClick={() => {
          sendCommand("resetHudSettings");
          setDirty(false);
        }}>Restaurar padrão</button>
        <button className="button ghost" onClick={() => sendCommand("toggleHudLayout")}>Mover HUD no OMSI</button>
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

function roadmapStatusLabel(status: string | null | undefined) {
  switch (status) {
    case "analysis-ready": return "Análise pronta";
    case "analysis-no-tile-images": return "Sem imagens de tile";
    case "analysis-failed": return "Falha na análise";
    case "building-tiles": return "Montando roadmap por tiles";
    case "building-vector": return "Gerando roadmap vetorial";
    case "tiles-built": return "Roadmap por tiles concluído";
    case "vector-built": return "Roadmap vetorial concluído";
    case "tiles-build-failed": return "Falha na geração por tiles";
    case "vector-build-failed": return "Falha na geração vetorial";
    default: return status || "Pronto";
  }
}

function RoadmapStudioPanel({ roadmap }: { roadmap: NavBrRoadmapStudioState }) {
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
            <h3>Gerar whole.roadmap.bmp</h3>
          </div>
          <span className={`hardware-state-pill ${roadmap.busy ? "connected" : ""}`}>
            {roadmap.busy ? "Processando" : roadmapStatusLabel(roadmap.status)}
          </span>
        </div>

        {roadmap.maps.length === 0 ? (
          <div className="empty-state">Nenhum mapa OMSI foi catalogado. Detecte/inicie uma instalação do OMSI e atualize o estado.</div>
        ) : (
          <>
            <label className="voice-field roadmap-map-select">
              <span>Mapa OMSI</span>
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
                <span><small>PASTA</small><strong>{selectedMap.folderName}</strong></span>
                <span><small>TILES DO MAPA</small><strong>{selectedMap.tileCount}</strong></span>
                <span><small>WHOLE ROADMAP</small><strong>{selectedMap.roadmapExists ? "Existe" : "Ausente"}</strong></span>
              </div>
            )}

            <div className="roadmap-actions">
              <button
                className="button ghost"
                disabled={!selectedFolder || roadmap.busy}
                onClick={() => sendCommand("analyzeRoadmap", { folderName: selectedFolder })}
              >
                Analisar tiles
              </button>
              <button
                className="button primary"
                disabled={!selectedFolder || roadmap.busy || !analysis?.canBuildFromTiles}
                onClick={() => sendCommand("buildRoadmapTiles", { folderName: selectedFolder })}
              >
                Montar pelas imagens
              </button>
              <button
                className="button ghost"
                disabled={!selectedFolder || roadmap.busy}
                onClick={() => sendCommand("buildRoadmapVector", { folderName: selectedFolder })}
              >
                Gerar vetorial pelas splines
              </button>
              <button
                className="button ghost"
                disabled={!selectedFolder || roadmap.busy}
                onClick={() => sendCommand("openRoadmapFolder", { folderName: selectedFolder })}
              >
                Abrir pasta
              </button>
            </div>

            {roadmap.busy && (
              <div className="roadmap-progress">
                <div><span>{roadmapStatusLabel(roadmap.status)}</span><strong>{Math.round(progress)}%</strong></div>
                <div className="roadmap-progress-track"><i style={{ width: `${progress}%` }} /></div>
              </div>
            )}
          </>
        )}

        {roadmap.error && <div className="directory-error">{roadmap.error}</div>}
      </article>

      <article className="card roadmap-analysis-card">
        <div className="section-heading">
          <div><span className="eyebrow">ANÁLISE</span><h3>Tiles e saída</h3></div>
        </div>

        {!analysis ? (
          <div className="empty-state">Selecione um mapa e use “Analisar tiles”. O modo vetorial continua disponível mesmo sem imagens roadmap por tile.</div>
        ) : (
          <>
            <div className="roadmap-analysis-grid">
              <span><small>IMAGENS DE TILE</small><strong>{analysis.tileImageCount}</strong></span>
              <span><small>POSIÇÕES SEM IMAGEM</small><strong>{analysis.missingTileImages}</strong></span>
              <span><small>GRADE X</small><strong>{analysis.minGridX} … {analysis.maxGridX}</strong></span>
              <span><small>GRADE Y</small><strong>{analysis.minGridY} … {analysis.maxGridY}</strong></span>
              <span><small>TILE</small><strong>{analysis.tilePixelWidth > 0 ? `${analysis.tilePixelWidth}×${analysis.tilePixelHeight}` : "—"}</strong></span>
              <span><small>SAÍDA</small><strong>{analysis.outputPixelWidth > 0 ? `${analysis.outputPixelWidth}×${analysis.outputPixelHeight}` : "—"}</strong></span>
              <span><small>ESTIMATIVA</small><strong>{formatFileSize(analysis.estimatedBytes)}</strong></span>
              <span><small>BACKUP NECESSÁRIO</small><strong>{analysis.existingWholeRoadmap ? "Sim" : "Não"}</strong></span>
            </div>
            <code className="roadmap-output-path">{analysis.outputPath}</code>
          </>
        )}
      </article>

      <article className="card roadmap-result-card">
        <div className="section-heading">
          <div><span className="eyebrow">ÚLTIMA GERAÇÃO</span><h3>Resultado real</h3></div>
        </div>
        {!result ? (
          <div className="empty-state">Nenhum roadmap foi gerado nesta sessão.</div>
        ) : (
          <>
            <div className="roadmap-analysis-grid">
              <span><small>MODO</small><strong>{result.mode === "tiles" ? "Imagens de tile" : "Vetorial / splines"}</strong></span>
              <span><small>DIMENSÃO</small><strong>{result.pixelWidth}×{result.pixelHeight}</strong></span>
              <span><small>TAMANHO</small><strong>{formatFileSize(result.fileSizeBytes)}</strong></span>
              <span><small>TEMPO</small><strong>{result.elapsedSeconds.toFixed(1)} s</strong></span>
              {result.tileImagesUsed != null && <span><small>TILES USADOS</small><strong>{result.tileImagesUsed}</strong></span>}
              {result.missingTileImages != null && <span><small>VAZIOS</small><strong>{result.missingTileImages}</strong></span>}
              {result.tileFilesRead != null && <span><small>TILES LIDOS</small><strong>{result.tileFilesRead}</strong></span>}
              {result.splinesDrawn != null && <span><small>SPLINES</small><strong>{result.splinesDrawn}</strong></span>}
            </div>
            <code className="roadmap-output-path">{result.outputPath}</code>
            {result.backupPath && <p className="roadmap-backup">Backup: <code>{result.backupPath}</code></p>}
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
    return <div className="card empty-state">Aguardando configurações do sistema…</div>;
  }

  const logSize = system.diagnostics.logSizeBytes >= 1024 * 1024
    ? `${(system.diagnostics.logSizeBytes / (1024 * 1024)).toFixed(1)} MB`
    : `${Math.max(0, system.diagnostics.logSizeBytes / 1024).toFixed(1)} KB`;

  return (
    <>
      <header className="topbar settings-header">
        <div>
          <span className="eyebrow">SISTEMA NAVBR</span>
          <h1>Configurações</h1>
          <p>Instalações do OMSI, diagnóstico e atalhos avançados mantidos pelo backend C#.</p>
        </div>
        <div className="top-actions">
          <span className={`connection-pill ${state?.omsi.running ? "connected" : ""}`}>
            <i /> {state?.omsi.running ? "OMSI detectado" : "OMSI fechado"}
          </span>
        </div>
      </header>

      {error && <div className="command-error">{error}</div>}

      <div className="mp-tabs settings-tabs" role="tablist">
        {([
          ["installations", "Instalações OMSI"],
          ["hud", "HUD"],
          ["roadmap", "Roadmap"],
          ["diagnostics", "Diagnóstico"],
          ["network", "Rede"],
          ["advanced", "Avançado"]
        ] as [SettingsTab, string][]).map(([key, label]) => (
          <button key={key} className={tab === key ? "active" : ""} onClick={() => setTab(key)}>{label}</button>
        ))}
      </div>

      {tab === "installations" && (
        <section className="settings-installations">
          <article className="card discovery-card">
            <div className="section-heading">
              <div><span className="eyebrow">DESCOBERTA</span><h3>Encontrar OMSI 2</h3></div>
              <button className="button ghost" onClick={() => sendCommand("selectOmsiFolder")}>Selecionar pasta</button>
            </div>
            <p>O NavBR pode localizar instalações registradas, bibliotecas Steam e também validar uma pasta informada manualmente.</p>
            <div className="discovery-actions">
              <input
                value={manualPath}
                onChange={event => setManualPath(event.target.value)}
                placeholder="Ex.: G:\Games\OMSI 2 Steam Edition"
              />
              <button className="button primary" onClick={() => sendCommand("discoverOmsiProfiles", { path: manualPath })}>
                {manualPath.trim() ? "Adicionar / descobrir" : "Descobrir automaticamente"}
              </button>
            </div>
          </article>

          <div className="installation-list">
            {system.installations.length === 0 ? (
              <div className="card empty-state">Nenhum perfil OMSI cadastrado. Use a descoberta acima para localizar uma instalação real.</div>
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
            <span className="eyebrow">PRIVACIDADE</span>
            <h3>Diagnóstico remoto</h3>
            <p>
              O envio é opt-in. Quando desativado, o NavBR não registra eventos para envio remoto e limpa a fila local de diagnóstico.
            </p>
            <label className="diagnostics-toggle">
              <input
                type="checkbox"
                checked={system.diagnostics.enabled}
                onChange={event => sendCommand("setDiagnosticsEnabled", { enabled: event.target.checked })}
              />
              <span>{system.diagnostics.enabled ? "Diagnóstico remoto ativado" : "Diagnóstico remoto desativado"}</span>
            </label>
            <div className="diagnostics-actions">
              <button className="button ghost" disabled={!system.diagnostics.enabled} onClick={() => sendCommand("flushDiagnostics")}>
                Tentar enviar fila agora
              </button>
              <button className="button ghost danger" onClick={() => sendCommand("purgeDiagnostics")}>
                Limpar fila de diagnóstico
              </button>
            </div>
          </article>

          <article className="card diagnostics-log-card">
            <span className="eyebrow">LOG LOCAL</span>
            <h3>navbr.log</h3>
            <div className="diagnostic-facts">
              <span><small>ARQUIVO</small><strong>{system.diagnostics.logExists ? "Disponível" : "Ainda não criado"}</strong></span>
              <span><small>TAMANHO</small><strong>{system.diagnostics.logExists ? logSize : "—"}</strong></span>
              <span><small>ATUALIZAÇÃO</small><strong>{system.diagnostics.logUpdatedAtUtc ? new Date(system.diagnostics.logUpdatedAtUtc).toLocaleString() : "—"}</strong></span>
            </div>
            <code>{system.diagnostics.logPath}</code>
            <p>O log local continua existindo independentemente do consentimento de diagnóstico remoto e é usado para suporte técnico local.</p>
          </article>
        </section>
      )}

      {tab === "network" && (
        <section className="network-layout">
          <article className="card network-overview-card">
            <div className="section-heading">
              <div><span className="eyebrow">CONECTIVIDADE</span><h3>TCP {network?.hostPort ?? 27730}</h3></div>
              <button className="button ghost" onClick={() => sendCommand("refreshNetworkDiagnostics")}>Atualizar diagnóstico</button>
            </div>

            {network?.message && <div className="network-message">{network.message}</div>}
            {network?.error && <div className="directory-error">{network.error}</div>}

            <div className="network-status-grid">
              <div>
                <small>FIREWALL WINDOWS</small>
                <strong className={network?.diagnostics?.firewallRulePresent ? "ok" : "warn"}>
                  {network?.diagnostics == null ? "Não verificado" : network.diagnostics.firewallRulePresent ? "Regra confirmada" : "Regra ausente"}
                </strong>
                <span>Entrada TCP {network?.hostPort ?? 27730} em todos os perfis de rede.</span>
              </div>
              <div>
                <small>PORTA LOCAL</small>
                <strong className={network?.diagnostics?.localPortListening ? "ok" : ""}>
                  {network?.diagnostics == null ? "Não verificado" : network.diagnostics.localPortListening ? "Ouvindo" : "Sem listener"}
                </strong>
                <span>{network?.hostRunning ? "Host NavBR ativo." : "Nenhuma sala local hospedada agora."}</span>
              </div>
              <div>
                <small>UPNP</small>
                <strong className={network?.diagnostics?.upnpGatewayFound ? "ok" : ""}>
                  {network?.diagnostics == null ? "Não verificado" : network.diagnostics.upnpGatewayFound ? "Gateway encontrado" : "Gateway não encontrado"}
                </strong>
                <span>{network?.automaticUpnpEnabled ? "Automático habilitado." : "Automático desabilitado."}</span>
              </div>
              <div>
                <small>AMBIENTE WAN</small>
                <strong>{network?.diagnostics?.environmentKind || "—"}</strong>
                <span>{network?.diagnostics?.gatewayExternalAddress || "IP externo não informado"}</span>
              </div>
            </div>

            <div className="network-actions">
              <button className="button primary" onClick={() => sendCommand("applyFirewallRule")}>
                Aplicar / corrigir Firewall TCP {network?.hostPort ?? 27730}
              </button>
              <span>{network?.runningAsAdministrator ? "NavBR já está elevado." : "O Windows solicitará permissão de administrador."}</span>
            </div>

            {network?.diagnostics && (
              <>
                <div className="network-addresses">
                  <small>IPv4 LOCAL</small>
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
            <span className="eyebrow">ROTEADOR</span>
            <h3>NAT / UPnP</h3>
            <label className="diagnostics-toggle">
              <input
                type="checkbox"
                checked={network?.automaticUpnpEnabled ?? false}
                disabled={network?.hostRunning}
                onChange={event => sendCommand("setAutomaticUpnp", { enabled: event.target.checked })}
              />
              <span>Tentar mapear TCP {network?.hostPort ?? 27730} automaticamente ao hospedar</span>
            </label>
            {network?.hostRunning && <p className="network-note">Pare a sala hospedada antes de alterar o UPnP.</p>}
            <div className="diagnostic-facts">
              <span><small>GATEWAY LOCAL</small><strong>{network?.diagnostics?.gatewayLocalAddress || "—"}</strong></span>
              <span><small>IP EXTERNO</small><strong>{network?.diagnostics?.gatewayExternalAddress || "—"}</strong></span>
              <span><small>TIPO</small><strong>{network?.diagnostics?.environmentKind || "—"}</strong></span>
            </div>
          </article>

          <article className="card network-probe-card">
            <span className="eyebrow">TESTE EXTERNO</span>
            <h3>Alcance pela Internet</h3>
            <p>O teste externo é separado do Firewall e do UPnP. Ele só funciona quando um serviço de callback externo está configurado.</p>
            <button className="button ghost" onClick={() => sendCommand("runExternalPortProbe")}>Testar TCP 27730 externamente</button>
            {network?.externalProbe && (
              <div className={`external-probe-result ${network.externalProbe.reachable ? "reachable" : "blocked"}`}>
                <strong>{network.externalProbe.reachable ? "Porta alcançável" : "Porta não alcançável"}</strong>
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
            <h3>Rede e conectividade</h3>
            <p>Firewall, NAT e UPnP já estão disponíveis na aba Rede. Relay e ônibus físico continuam no controlador nativo.</p>
            <button className="button ghost" onClick={() => setTab("network")}>Abrir Rede</button>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">HUD</span>
            <h3>Personalização</h3>
            <p>Presets, tema, escala, opacidade e módulos já estão disponíveis na aba HUD.</p>
            <div className="settings-action-row">
              <button className="button ghost" onClick={() => setTab("hud")}>Abrir HUD</button>
              <button className="button ghost" onClick={() => sendCommand("toggleHudLayout")}>Mover HUD</button>
            </div>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">ROADMAP</span>
            <h3>Roadmap Studio</h3>
            <p>Análise, montagem por tiles e geração vetorial pelas splines já usam os serviços nativos pela interface React.</p>
            <button className="button ghost" onClick={() => setTab("roadmap")}>Abrir Roadmap Studio</button>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">FALLBACK</span>
            <h3>Interface WPF</h3>
            <p>Abre o shell técnico anterior caso seja necessário comparar comportamento ou acessar uma área ainda não migrada.</p>
            <button className="button ghost" onClick={() => sendCommand("showLegacyShell")}>Abrir interface WPF</button>
          </article>
        </section>
      )}
    </>
  );
}


function roleplayStatusLabel(status: string | null | undefined) {
  switch (status) {
    case "roleplay-active": return "Personagem ativo";
    case "roleplay-returned-to-bus": return "Motorista retornou ao ônibus";
    case "roleplay-character-selected": return "Personagem selecionado";
    case "roleplay-plugin-unavailable": return "Plugin Bridge sem suporte RP";
    case "roleplay-character-required": return "Selecione um personagem";
    case "roleplay-waiting-telemetry": return "Aguardando telemetria do OMSI";
    case "roleplay-map-or-character-changed": return "Mapa/personagem alterado";
    case "roleplay-control-lost": return "Controle do personagem perdido";
    case "roleplay-disabled": return "Recurso RP desativado";
    case "roleplay-enabled": return "Recurso RP ativado";
    default: return status || "Pronto";
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
  const roleplay = state?.roleplay;
  const multiplayer = state?.multiplayer ?? fallbackMultiplayer;

  if (!roleplay) {
    return <div className="card empty-state">Aguardando estado do Personagem / RP…</div>;
  }

  const canStart = roleplay.enabled && roleplay.mapReady && roleplay.runtimeAvailable && Boolean(roleplay.selected) && !roleplay.active;
  const current = roleplay.current;

  return (
    <>
      {!embedded && (
        <header className="topbar roleplay-header">
          <div>
            <span className="eyebrow">PERSONAGEM / RP</span>
            <h1>Motorista fora do ônibus</h1>
            <p>Seleção e controle do personagem usando o estado real do OMSI e do Plugin Bridge v3.</p>
          </div>
          <div className="top-actions">
            <span className={`connection-pill ${roleplay.active ? "connected" : ""}`}>
              <i /> {roleplay.active ? "Fora do ônibus" : "No ônibus"}
            </span>
          </div>
        </header>
      )}

      {error && <div className="command-error">{error}</div>}

      <section className={embedded ? "rp-web-grid embedded" : "rp-web-grid"}>
        <article className="card rp-status-card">
          <div className="section-heading">
            <div>
              <span className="eyebrow">ESTADO</span>
              <h3>{roleplay.active ? current?.characterName || roleplay.selected?.displayName || "Personagem ativo" : roleplay.selected?.displayName || "Nenhum personagem selecionado"}</h3>
            </div>
            <span className={`hardware-state-pill ${roleplay.runtimeAvailable ? "connected" : ""}`}>
              {roleplay.runtimeAvailable ? "Bridge RP disponível" : "Bridge RP indisponível"}
            </span>
          </div>

          <label className="rp-enable-toggle">
            <input
              type="checkbox"
              checked={roleplay.enabled}
              onChange={event => sendCommand("setRoleplayEnabled", { enabled: event.target.checked })}
            />
            <span>
              <strong>Ativar Personagem / RP</strong>
              <small>Habilita o modo experimental sem depender da janela Multiplayer WPF.</small>
            </span>
          </label>

          <div className="details-grid rp-status-grid">
            <div><small>MAPA</small><strong>{state?.telemetry?.mapName || "—"}</strong></div>
            <div><small>STATUS</small><strong>{roleplayStatusLabel(roleplay.status)}</strong></div>
            <div><small>MAPA PRONTO</small><strong>{roleplay.mapReady ? "Sim" : "Não"}</strong></div>
            <div><small>MULTIPLAYER</small><strong>{multiplayer.connected ? multiplayer.roomId : "Não conectado"}</strong></div>
          </div>

          {current && (
            <div className="rp-current-state">
              <span><small>ATIVIDADE</small><strong>{current.activity}</strong></span>
              <span><small>VELOCIDADE</small><strong>{format(current.speedMps * 3.6, 1)} km/h</strong></span>
              <span><small>DIREÇÃO</small><strong>{format(current.headingDegrees, 0)}°</strong></span>
              <span><small>HUMAN INDEX</small><strong>{current.humanIndex ?? "—"}</strong></span>
            </div>
          )}

          <div className="action-row rp-actions">
            {roleplay.active ? (
              <button className="button primary" onClick={() => sendCommand("stopRoleplay")}>Retornar ao ônibus</button>
            ) : (
              <button className="button primary" disabled={!canStart} onClick={() => sendCommand("startRoleplay")}>Sair do ônibus</button>
            )}
            <button className="button ghost" onClick={() => sendCommand("refreshState")}>Atualizar catálogo</button>
          </div>

          {!roleplay.enabled && <p className="migration-note">O modo Personagem / RP está desativado nas configurações experimentais.</p>}
          {roleplay.enabled && !roleplay.mapReady && <p className="migration-note">Entre em um mapa do OMSI para carregar os personagens reais de Map.Drivers.</p>}
          {roleplay.mapReady && !roleplay.runtimeAvailable && <p className="migration-note">O Plugin Bridge precisa anunciar as capacidades de posse e transformação de personagem.</p>}
        </article>

        <article className="card rp-character-card">
          <div className="section-heading">
            <div><span className="eyebrow">MAP.DRIVERS</span><h3>Personagens disponíveis</h3></div>
            <span className="stop-count">{roleplay.characters.length}</span>
          </div>

          {roleplay.characters.length === 0 ? (
            <div className="empty-state">Nenhum personagem disponível para o mapa atual.</div>
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
                    <small>{character.isActiveDriver ? "Motorista ativo do mapa" : character.sourceValue}</small>
                  </span>
                  <em>{character.selected ? "Selecionado" : "Usar"}</em>
                </button>
              ))}
            </div>
          )}
          <p className="hardware-note">A seleção é válida somente para a sessão/mapa atual. O DefinitionPointer nativo não é persistido.</p>
        </article>
      </section>

      {!embedded && (
        <section className="card rp-controls-card">
          <div className="section-heading">
            <div><span className="eyebrow">CONTROLES</span><h3>Durante o RP</h3></div>
          </div>
          <div className="rp-key-grid">
            <span><kbd>W</kbd><strong>Andar para frente</strong></span>
            <span><kbd>S</kbd><strong>Andar para trás</strong></span>
            <span><kbd>A / D</kbd><strong>Virar</strong></span>
            <span><kbd>Shift</kbd><strong>Correr</strong></span>
            <span><kbd>Esc</kbd><strong>Retornar ao ônibus</strong></span>
          </div>
          <p>Os atalhos só são capturados quando o OMSI está em primeiro plano. O personagem permanece limitado à área segura ao redor do ônibus.</p>
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

  const statusLabel = multiplayer.connected
    ? "Conectado"
    : multiplayer.available
      ? multiplayer.connectionState
      : "Controlador inativo";

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

  return (
    <>
      <header className="topbar multiplayer-header">
        <div>
          <span className="eyebrow">CENTRAL MULTIPLAYER</span>
          <h1>Sessão NavBR</h1>
          <p>Estado real da sala, jogadores, chat, voz e personagem vindo do controlador C#.</p>
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
            {multiplayer.available ? "Controles da sala" : "Ativar multiplayer"}
          </button>
        </div>
      </header>

      {error && <div className="command-error">{error}</div>}

      <section className="session-metrics">
        <div className="metric"><small>SALA ATUAL</small><strong>{multiplayer.connected ? multiplayer.roomId : "—"}</strong></div>
        <div className="metric"><small>MAPA LOCAL</small><strong>{telemetry?.mapName || "—"}</strong></div>
        <div className="metric"><small>JOGADORES</small><strong>{multiplayer.playerCount}</strong></div>
        <div className="metric"><small>LATÊNCIA</small><strong>{multiplayer.latencyMs == null ? "—" : `${format(multiplayer.latencyMs, 0)} ms`}</strong></div>
        <div className="metric"><small>HOST</small><strong>{multiplayer.hostRunning ? `TCP ${multiplayer.hostPort ?? 27730}` : "Local inativo"}</strong></div>
      </section>

      <div className="mp-tabs" role="tablist">
        {([
          ["overview", "Visão geral"],
          ["room", "Sala"],
          ["players", "Jogadores"],
          ["chat", "Chat & Voz"],
          ["roleplay", "Personagem / RP"],
          ["advanced", "Avançado"]
        ] as [MultiplayerTab, string][]).map(([key, label]) => (
          <button key={key} className={tab === key ? "active" : ""} onClick={() => setTab(key)}>{label}</button>
        ))}
      </div>

      {tab === "overview" && (
        <section className="mp-grid">
          <article className="card mp-main-card">
            <div className="section-heading">
              <div><span className="eyebrow">SESSÃO AO VIVO</span><h3>Operação compartilhada</h3></div>
              <span className={`live-pill ${multiplayer.connected ? "" : "muted"}`}><span /> {multiplayer.connected ? "LIVE" : "OFFLINE"}</span>
            </div>
            <SessionMap points={multiplayer.sessionPoints} />
          </article>

          <aside className="mp-side-stack">
            <article className="card compact-card">
              <span className="eyebrow">VOCÊ</span>
              <h3>{multiplayer.displayName || "Motorista"}</h3>
              <p>{telemetry?.line ? `Linha ${telemetry.line}` : "Sem linha ativa"}</p>
              <p>{telemetry?.route || telemetry?.destinationName || "Aguardando rota"}</p>
            </article>
            <article className="card compact-card">
              <span className="eyebrow">VOZ</span>
              <h3>{multiplayer.voiceEnabled ? "Ativa" : "Desativada"}</h3>
              <p>Canal {multiplayer.voiceChannel || "general"}</p>
            </article>
            <article className="card compact-card">
              <span className="eyebrow">PERSONAGEM</span>
              <h3>{multiplayer.localRoleplayActive ? "Fora do ônibus" : "No ônibus"}</h3>
              <button className="text-action" onClick={() => setTab("roleplay")}>Abrir Personagem / RP →</button>
            </article>
          </aside>
        </section>
      )}

      {tab === "room" && (
        <section className="card mp-panel">
          <div className="section-heading">
            <div><span className="eyebrow">SALA</span><h3>Conexão e host</h3></div>
            <button className="button ghost" onClick={onOpenNetwork}>Rede / Firewall</button>
          </div>

          <div className="room-form-grid">
            <label>
              <span>Servidor</span>
              <input value={serverUrl} onChange={event => setServerUrl(event.target.value)} disabled={multiplayer.connected} placeholder="http://127.0.0.1:27730" />
            </label>
            <label>
              <span>Sala</span>
              <input value={roomId} onChange={event => setRoomId(event.target.value)} disabled={multiplayer.connected} placeholder="navbr-1234" />
            </label>
            <label>
              <span>Apelido</span>
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
              <span>Criar sala privada</span>
            </label>
            <label className="password-field">
              <span>Senha da sala</span>
              <input
                type="password"
                value={roomPassword}
                disabled={multiplayer.connected}
                onChange={event => setRoomPassword(event.target.value)}
                placeholder={privateRoom ? "Mínimo 4 caracteres" : "Use ao entrar em sala privada"}
              />
            </label>
          </div>

          <div className="room-actions">
            {!multiplayer.connected ? (
              <>
                <button className="button primary" onClick={() => sendCommand("connectRoom", { serverUrl, roomId, displayName, roomPassword })}>Entrar na sala</button>
                <button className="button ghost" onClick={() => sendCommand("createLocalRoom", { roomId, displayName, isPrivate: privateRoom, roomPassword })}>Criar sala local</button>
              </>
            ) : (
              <button className="button ghost danger" onClick={() => sendCommand(multiplayer.hostRunning ? "stopLocalHost" : "disconnectRoom")}>
                {multiplayer.hostRunning ? "Encerrar sala local" : "Desconectar"}
              </button>
            )}
          </div>

          <div className="details-grid room-status-grid">
            <div><small>SERVIDOR ATIVO</small><strong>{multiplayer.serverUrl || "—"}</strong></div>
            <div><small>ID DA SALA</small><strong>{multiplayer.roomId || "—"}</strong></div>
            <div><small>APELIDO</small><strong>{multiplayer.displayName || "—"}</strong></div>
            <div><small>ESTADO</small><strong>{statusLabel}</strong></div>
          </div>

          {multiplayer.inviteAddresses.length > 0 && (
            <div className="invite-box">
              <small>ENDEREÇOS PARA CONVITE</small>
              {multiplayer.inviteAddresses.map(address => <code key={address}>{address}</code>)}
            </div>
          )}
          <div className="public-room-browser">
            <div className="section-heading">
              <div>
                <span className="eyebrow">SALAS PÚBLICAS</span>
                <h3>Encontrar operação ativa</h3>
              </div>
              <button className="button ghost" onClick={() => sendCommand("refreshPublicRooms", { serverUrl })}>Atualizar salas</button>
            </div>

            <input
              className="room-search"
              value={roomSearch}
              onChange={event => setRoomSearch(event.target.value)}
              placeholder="Buscar por sala, mapa, versão, ônibus ou HOF"
            />

            {state?.roomDirectory.error && (
              <div className="directory-error">{state.roomDirectory.error}</div>
            )}

            <div className="public-room-list">
              {publicRooms.length === 0 ? (
                <div className="empty-state">
                  Nenhuma sala pública carregada. Use “Atualizar salas” para consultar o servidor.
                </div>
              ) : publicRooms.map(room => (
                <div className="public-room-row" key={room.roomId}>
                  <button
                    className={`favorite-button ${room.favorite ? "active" : ""}`}
                    onClick={() => sendCommand("toggleRoomFavorite", { roomId: room.roomId })}
                    title={room.favorite ? "Remover dos favoritos" : "Adicionar aos favoritos"}
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
                    <span>{room.mapName || "Mapa não informado"} · {room.playerCount} jogador(es)</span>
                    <small>
                      NavBR {room.navbrVersion || "—"} · OMSI {room.omsiVersion || "—"} · Plugin {room.pluginProtocolVersion || "—"}
                    </small>
                    <em className={`compatibility-badge ${room.compatibility}`}>
                      {room.compatibility === "compatible" ? "Compatível" : room.compatibility === "warning" ? "Compatibilidade parcial" : "Requer ajuste local"}
                    </em>
                    {room.compatibilityIssues.length > 0 && (
                      <small className="compatibility-detail">{room.compatibilityIssues[0]}</small>
                    )}
                  </button>
                  <button
                    className="button compact"
                    disabled={!room.directJoinAllowed}
                    title={!room.directJoinAllowed ? "Carregue a configuração compatível antes de entrar diretamente." : undefined}
                    onClick={() => {
                      setRoomId(room.roomId);
                      setPrivateRoom(false);
                      setRoomPassword("");
                      sendCommand("connectRoom", { serverUrl, roomId: room.roomId, displayName, roomPassword: "" });
                    }}
                  >
                    Entrar
                  </button>
                </div>
              ))}
            </div>
          </div>

          <p className="migration-note">
            Salas públicas e privadas já usam a ponte React. Firewall, NAT/UPnP e diagnósticos avançados continuam no controlador nativo enquanto essas telas são migradas.
          </p>
        </section>
      )}

      {tab === "players" && (
        <section className="card mp-panel">
          <div className="section-heading">
            <div><span className="eyebrow">JOGADORES</span><h3>{visiblePlayers.length} na sessão</h3></div>
          </div>
          {visiblePlayers.length === 0 ? (
            <div className="empty-state">Nenhum jogador remoto disponível.</div>
          ) : (
            <div className="players-table">
              {visiblePlayers.map(player => (
                <div className="player-row" key={player.playerId}>
                  <span className={`avatar-dot ${player.roleplayActive ? "rp" : ""}`}>{player.displayName.slice(0, 1).toUpperCase()}</span>
                  <div className="player-main">
                    <strong>{player.displayName}</strong>
                    <small>{player.mapName || "Mapa não informado"} · {player.roleplayActive ? "Personagem / RP" : "No ônibus"}</small>
                  </div>
                  <span className={`voice-state ${player.speaking ? "speaking" : ""}`}>{player.speaking ? "Falando" : player.voiceEnabled ? "Voz ativa" : "Sem voz"}</span>
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
              <div><span className="eyebrow">CHAT</span><h3>Mensagens da sala</h3></div>
            </div>
            <div className="chat-log">
              {multiplayer.chat.length === 0 ? (
                <div className="empty-state">Nenhuma mensagem recebida.</div>
              ) : multiplayer.chat.map((message, index) => (
                <div className={`chat-message ${message.isSystem ? "system" : ""}`} key={`${message.timestampUtc}-${index}`}>
                  <div><strong>{message.isSystem ? "NavBR" : message.displayName}</strong><time>{new Date(message.timestampUtc).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</time></div>
                  <p>{message.text}</p>
                </div>
              ))}
            </div>
            <form className="chat-compose" onSubmit={submitChat}>
              <input value={chatText} onChange={event => setChatText(event.target.value)} disabled={!multiplayer.connected} placeholder={multiplayer.connected ? "Escreva uma mensagem…" : "Conecte-se para conversar"} />
              <button className="button primary" disabled={!multiplayer.connected || !chatText.trim()}>Enviar</button>
            </form>
          </article>
          <aside className="card voice-card">
            <span className="eyebrow">VOZ</span>
            <h3>{multiplayer.voiceEnabled ? "Voz habilitada" : "Voz desativada"}</h3>

            <label className="voice-toggle">
              <input
                type="checkbox"
                checked={multiplayer.voiceEnabled}
                onChange={event => sendCommand("setVoiceEnabled", { enabled: event.target.checked })}
              />
              <span>Ativar voz na sala</span>
            </label>

            <label className="voice-field">
              <span>Canal</span>
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
                <option value="general">Geral</option>
                <option value="company">Empresa/equipe</option>
                <option value="dispatch">CCO</option>
                <option value="proximity">Proximidade</option>
              </select>
            </label>

            {voiceChannel === "proximity" && (
              <label className="voice-field">
                <span>Raio de proximidade: {voiceRadius.toFixed(0)} m</span>
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
              <span>Silenciar áudio remoto</span>
            </label>

            <div className="voice-device-grid">
              <label className="voice-field">
                <span>Microfone</span>
                <select
                  value={multiplayer.voiceInputDeviceNumber}
                  onChange={event => sendCommand("configureVoiceDevices", {
                    inputDeviceNumber: Number(event.target.value),
                    outputDeviceNumber: multiplayer.voiceOutputDeviceNumber
                  })}
                >
                  {multiplayer.voiceInputDevices.length === 0
                    ? <option value={0}>Nenhum microfone detectado</option>
                    : multiplayer.voiceInputDevices.map(device => (
                      <option key={device.deviceNumber} value={device.deviceNumber}>{device.displayName}</option>
                    ))}
                </select>
              </label>

              <label className="voice-field">
                <span>Saída de áudio</span>
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
                <div><span className="eyebrow">MIXER</span><h3>Jogadores</h3></div>
              </div>
              {multiplayer.voiceMixers.length === 0 ? (
                <div className="empty-state compact-empty">Nenhum jogador remoto para ajustar.</div>
              ) : multiplayer.voiceMixers.map(player => (
                <div className="voice-mixer-row" key={player.playerId}>
                  <div>
                    <strong>{player.displayName}</strong>
                    <small>{player.speaking ? "Falando agora" : player.muted ? "Mutado" : "Áudio ativo"}</small>
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
            <h3>Mover HUD</h3>
            <p>Ativa o modo de reposicionamento do overlay nativo.</p>
            <button className="button ghost" onClick={() => sendCommand("toggleHudLayout")}>Mover HUD</button>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">HUD</span>
            <h3>Configurar HUD</h3>
            <p>Abre o editor nativo de escala, opacidade e módulos.</p>
            <button className="button ghost" onClick={onOpenHud}>Configurar HUD</button>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">REDE</span>
            <h3>Host local</h3>
            <p>{multiplayer.hostRunning ? `Escutando na porta TCP ${multiplayer.hostPort ?? 27730}.` : "Host local não está ativo."}</p>
            <button className="button ghost" onClick={onOpenNetwork}>Abrir Configurações &gt; Rede</button>
          </article>
        </section>
      )}
    </>
  );
}


function ghostStatusLabel(status: string | null | undefined) {
  switch (status) {
    case "recording": return "Gravando viagem";
    case "recording-saved": return "Ghost salvo";
    case "recording-save-failed": return "Falha ao salvar Ghost";
    case "recording-cancelled": return "Gravação cancelada";
    case "ghost-loaded": return "Ghost carregado";
    case "ghost-imported": return "Ghost importado";
    case "ghost-load-failed": return "Falha ao abrir Ghost";
    case "playback-starting": return "Iniciando Ghost 3D";
    case "playback-stopping": return "Parando Ghost 3D";
    case "playback-stopped": return "Ghost 3D interrompido";
    case "playback-completed": return "Ghost 3D concluído";
    case "playback-failed": return "Ghost 3D indisponível";
    default: return status || "Pronto";
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
  const ghost: NavBrGhostState | undefined = state?.ghost;
  const [recordName, setRecordName] = useState("");
  const [playbackSpeed, setPlaybackSpeed] = useState(1);
  const [loop, setLoop] = useState(false);

  useEffect(() => {
    sendCommand("refreshGhostLibrary");
  }, []);

  if (!ghost) {
    return <div className="card empty-state">Aguardando estado do Ghost / Replay…</div>;
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
          <h1>Grave uma viagem real e reproduza em 3D.</h1>
          <p>A gravação usa somente a telemetria local. O replay físico só escreve quando o Plugin Bridge aceita spawn/transform.</p>
        </div>
        <div className="top-actions">
          <span className={`connection-pill ${ghost.recording || ghost.playing ? "connected" : ""}`}>
            <i /> {ghost.recording ? `Gravando · ${ghost.frameCount} frames` : ghost.playing ? "Ghost 3D ativo" : ghostStatusLabel(ghost.status)}
          </span>
        </div>
      </header>

      {(error || ghost.error) && <div className="command-error">{error || ghost.error}</div>}

      <section className="ghost-layout">
        <article className="card ghost-record-card">
          <div className="section-heading">
            <div><span className="eyebrow">GRAVAÇÃO</span><h3>Telemetria real do OMSI</h3></div>
            <span className={`hardware-state-pill ${state?.telemetry?.inGame ? "connected" : ""}`}>
              {state?.telemetry?.inGame ? "OMSI pronto" : "Aguardando mapa/ônibus"}
            </span>
          </div>

          <label className="voice-field">
            <span>Nome da gravação</span>
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
              ● Gravar viagem
            </button>
            <button
              className="button ghost"
              disabled={!ghost.recording}
              onClick={() => sendCommand("stopGhostRecording")}
            >
              ■ Parar e salvar
            </button>
            <button
              className="button ghost danger"
              disabled={!ghost.recording}
              onClick={() => sendCommand("cancelGhostRecording")}
            >
              Cancelar gravação
            </button>
          </div>

          <div className="ghost-record-stats">
            <span><small>FRAMES</small><strong>{ghost.frameCount.toLocaleString()}</strong></span>
            <span><small>CADÊNCIA</small><strong>100 ms</strong></span>
            <span><small>MODO</small><strong>Somente leitura</strong></span>
          </div>
        </article>

        <article className="card ghost-file-card">
          <div className="section-heading">
            <div><span className="eyebrow">ARQUIVO GHOST</span><h3>{selected?.name || "Nenhum Ghost carregado"}</h3></div>
          </div>

          <div className="ghost-actions">
            <button
              className="button ghost"
              disabled={ghost.recording || ghost.playing}
              onClick={() => sendCommand("selectGhostFile")}
            >
              Abrir Ghost
            </button>
            <button className="button ghost" onClick={() => sendCommand("openGhostFolder")}>
              Abrir pasta de Ghosts
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
            <div><span className="eyebrow">GHOST 3D</span><h3>Reprodução física experimental</h3></div>
            <span className={`hardware-state-pill ${ghost.playing ? "connected" : ""}`}>
              {ghost.playing ? "Reproduzindo" : "Parado"}
            </span>
          </div>

          <label className="voice-field">
            <span>Velocidade: {playbackSpeed.toFixed(1)}×</span>
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
            <span>Repetir continuamente</span>
          </label>

          <div className="ghost-actions">
            <button
              className="button primary"
              disabled={!canPlay}
              onClick={() => sendCommand("playGhost", { playbackSpeed, loop })}
            >
              ▶ Reproduzir Ghost 3D
            </button>
            <button
              className="button ghost"
              disabled={!ghost.playing}
              onClick={() => sendCommand("stopGhostPlayback")}
            >
              ■ Parar reprodução
            </button>
          </div>

          <p className="migration-note">
            O NavBR falha de forma segura se o Plugin Bridge não aceitar spawn ou transforms do Ghost.
          </p>
        </article>

        <article className="card ghost-analytics-card">
          <div className="section-heading">
            <div><span className="eyebrow">ANALYTICS</span><h3>Métricas do replay</h3></div>
          </div>

          {!analytics ? (
            <div className="empty-state">Abra ou grave um Ghost para calcular as métricas reais do arquivo.</div>
          ) : (
            <div className="ghost-analytics-grid">
              <span><small>DISTÂNCIA ESTIMADA</small><strong>{analytics.estimatedDistanceKm.toFixed(2)} km</strong></span>
              <span><small>VELOCIDADE MÉDIA</small><strong>{analytics.averageSpeedKph.toFixed(1)} km/h</strong></span>
              <span><small>VELOCIDADE MÁXIMA</small><strong>{analytics.maximumSpeedKph.toFixed(1)} km/h</strong></span>
              <span><small>AMOSTRAS VÁLIDAS</small><strong>{analytics.validSpeedSamples.toLocaleString()}</strong></span>
            </div>
          )}
        </article>

      <section className="ghost-layout ghost-secondary">
        <article className="card ghost-library-card">
          <div className="section-heading">
            <div><span className="eyebrow">BIBLIOTECA</span><h3>Replays locais</h3></div>
            <div className="ghost-library-actions">
              <button
                className="button ghost compact"
                disabled={ghost.recording || ghost.playing}
                onClick={() => sendCommand("importGhostReplay")}
              >
                Importar
              </button>
              <button
                className="button ghost compact"
                onClick={() => sendCommand("refreshGhostLibrary")}
              >
                Atualizar
              </button>
            </div>
          </div>

          {ghost.library.length === 0 ? (
            <div className="empty-state">
              {ghost.libraryInvalidCount > 0
                ? `Nenhum replay válido. ${ghost.libraryInvalidCount} arquivo(s) incompatível(is) ignorado(s).`
                : "Nenhum replay gravado na biblioteca local."}
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
            <p className="migration-note">{ghost.libraryInvalidCount} arquivo(s) incompatível(is) foram ignorados.</p>
          )}
        </article>

        <article className="card ghost-route-card">
          <div className="section-heading">
            <div><span className="eyebrow">PRÉVIA LOCAL</span><h3>Trajeto gravado</h3></div>
            <span className="route-source-pill">READ-ONLY</span>
          </div>
          <GhostRoutePreview points={selected?.routePoints || []} />
          <p className="migration-note">A prévia usa coordenadas X/Z dos frames reais e não envia comandos para o OMSI.</p>
        </article>
      </section>
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
      if (requested && [
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
  );
}
