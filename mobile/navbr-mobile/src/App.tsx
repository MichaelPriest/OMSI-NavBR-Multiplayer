import { Capacitor, registerPlugin } from "@capacitor/core";
import { FormEvent, useEffect, useMemo, useState } from "react";

type Point = { x: number; y: number };
type SessionPoint = {
  playerId: string; displayName: string; kind: string; x: number; y: number;
  headingDegrees: number; speedKph: number; line?: string | null; isLocal: boolean; activity?: string | null;
};
type Player = {
  playerId: string; displayName: string; mapName?: string | null; line?: string | null; route?: string | null;
  destinationName?: string | null; nextStopName?: string | null; vehicleName?: string | null; speedKph?: number | null;
  latencyMs?: number | null; delaySeconds?: number | null; speaking: boolean; isLocal: boolean; roleplayActive: boolean;
  physicalVehicleSpawned: boolean; telemetryStale: boolean; distanceText?: string | null;
};
type VoiceMixer = { playerId: string; displayName: string; muted: boolean; gain: number; speaking: boolean };

type MobileState = {
  schema: string;
  version: number;
  generatedAtUtc: string;
  omsi: { detected: boolean; inGame: boolean; mapName?: string | null };
  plugin: { connected: boolean; version?: string | null; capabilities: string[] };
  hud?: {
    enabled: boolean; navBrTpTsEnabled: boolean; navBrTpTsTheme: number; navBrTpTsSize: number;
    navBrTpTsAutoDirection: boolean; navBrTpTsManualLine?: string | null; navBrTpTsManualDirection: string;
  };
  operation?: {
    available: boolean; capturedAtUtc?: string | null; cabinTemperatureC?: number | null; passengerCount?: number | null;
    scheduleActive?: boolean | null; simulationTime?: number | null; simulationDay?: number | null;
    simulationMonth?: number | null; simulationYear?: number | null; simulationPaused?: boolean | null;
    ibisLineCourse?: string | null; ibisRouteCode?: string | null; ibisTerminusName?: string | null;
    ibisDelayMinutes?: string | null; ibisDelaySeconds?: string | null; ibisDelayState?: string | null;
    hofRoutes?: { line: string; route: string; description: string; destinationCode?: string | null; hofFile: string }[];
  };
  vehicle?: {
    mapName?: string | null; vehicleName?: string | null; vehiclePath?: string | null; line?: string | null; route?: string | null;
    destinationName?: string | null; nextStopName?: string | null; hofName?: string | null;
    speedKph: number; headingDegrees: number; delaySeconds?: number | null; currentStreetName?: string | null;
    currentStopIndex?: number | null; fuelPercent?: number | null; accelerationMps2?: number | null;
    throttlePercent?: number | null; brakePercent?: number | null; steeringDegrees?: number | null;
    doors: string; lights: string; turnSignal: string; hornActive: boolean; wipersActive: boolean;
    parkingBrakeActive: boolean; reverseGear: boolean; stopRequested: boolean; isInGame: boolean;
  } | null;
  vehicleControls: {
    writable: boolean;
    enabled: boolean;
    capabilityAvailable: boolean;
    writeReason?: string | null;
    detectedEvents: string[];
  };
  navigation: {
    available: boolean; mapName?: string | null; line?: string | null; route?: string | null;
    destinationName?: string | null; nextStopName?: string | null; currentStreetName?: string | null;
    isOnRoute: boolean; offRouteDistanceMeters: number; routeProgressPercent: number;
    distanceRemainingMeters: number; distanceToNextStopMeters?: number | null; maneuver: string;
    distanceToManeuverMeters?: number | null; etaToNextStopSeconds?: number | null;
    roadmapAvailable?: boolean;
    bounds?: { minX: number; minY: number; maxX: number; maxY: number } | null;
    routePoints: Point[]; rejoinPoints: Point[];
    stopPoints?: Array<Point & { name: string; isNext: boolean }>;
    vehicle?: (Point & { headingDegrees: number; speedKph: number }) | null;
    stopSequence?: { routeResolved: boolean; totalStops: number; upcomingStops: string[] };
  };
  multiplayer: {
    available: boolean; connected: boolean; connectionState: string; roomId?: string | null; displayName?: string | null;
    latencyMs?: number | null; playerCount: number; players: Player[]; sessionPoints: SessionPoint[];
    voiceEnabled: boolean; voiceChannel: string; voiceProximityMeters: number; voiceDeafened: boolean;
    voicePushToTalkActive: boolean; voiceMixers: VoiceMixer[];
    networkQuality?: { level: string; roundTripMs?: number | null; jitterMs?: number | null; lossPercent?: number | null };
  };
  ibis: {
    available: boolean; writable: boolean; writeReason?: string | null; controlMode?: string | null;
    detectedEvents: string[]; line?: string | null; route?: string | null;
    destination?: string | null; hof?: string | null; nextStop?: string | null; delaySeconds?: number | null;
  };
};

type Tab = "gps" | "bus" | "ibis" | "ops" | "multi" | "voice" | "status";
type DiscoveryResult = { host: string; httpPort: number; pairingCode: string };
interface NavBrDiscoveryPlugin { discover(options?: { timeoutMs?: number }): Promise<DiscoveryResult> }
const NavBrDiscovery = registerPlugin<NavBrDiscoveryPlugin>("NavBrDiscovery");

const fmtDistance = (v?: number | null) => v == null ? "—" : v >= 1000 ? `${(v / 1000).toFixed(1)} km` : `${Math.round(v)} m`;
const fmtEta = (v?: number | null) => v == null ? "—" : `${Math.max(0, Math.round(v / 60))} min`;
const fmtPercent = (v?: number | null) => v == null ? "—" : `${Math.round(v)}%`;
const fmtSimulationTime = (v?: number | null) => {
  if (v == null || !Number.isFinite(v) || v < 0) return "—";
  const totalMinutes = v <= 24.5 ? v * 60 : v <= 86400 ? v / 60 : NaN;
  if (!Number.isFinite(totalMinutes)) return "—";
  const normalized = ((Math.round(totalMinutes) % 1440) + 1440) % 1440;
  return `${String(Math.floor(normalized / 60)).padStart(2, "0")}:${String(normalized % 60).padStart(2, "0")}`;
};
const fmtOperationalDelay = (op?: MobileState["operation"], fallbackSeconds?: number | null) => {
  if (op?.scheduleActive === false) return "SEM HORÁRIO";
  if (op?.ibisDelayMinutes) {
    const sec = op.ibisDelaySeconds ? `:${op.ibisDelaySeconds.padStart(2, "0")}` : "";
    return `${op.ibisDelayMinutes}${sec}${op.ibisDelayState ? ` · ${op.ibisDelayState}` : ""}`;
  }
  if (fallbackSeconds == null) return op?.scheduleActive ? "SEM DADOS" : "—";
  if (Math.abs(fallbackSeconds) < 3) return "NO HORÁRIO";
  const min = fallbackSeconds / 60;
  return `${min > 0 ? "+" : ""}${min.toFixed(1)} min`;
};
const clampPct = (v?: number | null) => Math.max(0, Math.min(100, v ?? 0));
const friendlyControlName = (value: string) =>
  value
    .replace(/^bus_/i, "")
    .replace(/^cockpit_/i, "")
    .replace(/[_\-.]+/g, " ")
    .replace(/\b\w/g, c => c.toUpperCase());

type IbisProfile = {
  id: "classic" | "atron" | "almex" | "efad" | "matrix";
  name: string;
  subtitle: string;
};

type IbisKeyDefinition = {
  id: string;
  label: string;
  secondary?: string;
  aliases: string[];
  wide?: boolean;
};

const IBIS_FUNCTION_KEYS: IbisKeyDefinition[] = [
  { id: "language", label: "Sprache", secondary: "Wunsch", aliases: ["sprache", "wunsch", "language", "request"] },
  { id: "clock", label: "Uhrzeit", aliases: ["uhrzeit", "clock", "time"] },
  { id: "route", label: "Route", aliases: ["route", "kurs route"] },
  { id: "line", label: "Linie", secondary: "Kurs", aliases: ["linie", "line", "kurs"] },
  { id: "destination", label: "Ziel", aliases: ["ziel", "destination", "dest"] },
  { id: "forward-mute", label: "Vor", secondary: "Stumm", aliases: ["vor stumm", "forward mute", "vor", "forward"] },
  { id: "next-stop", label: "Fortsch.", secondary: "H-Stelle", aliases: ["haltestelle", "next stop", "nextstop", "fortschalt", "advance stop"] },
  { id: "back-mute", label: "Rück", secondary: "Stumm", aliases: ["rueck stumm", "ruck stumm", "back mute", "rueck", "back"] },
  { id: "zone", label: "Zone", aliases: ["zone"] },
  { id: "subzone", label: "Teilzone", secondary: "Richtig", aliases: ["teilzone", "richtig", "subzone"] }
];

const IBIS_NUMERIC_KEYS: IbisKeyDefinition[] = [
  { id: "1", label: "1", aliases: ["1"] },
  { id: "2", label: "2", aliases: ["2"] },
  { id: "3", label: "3", aliases: ["3"] },
  { id: "4", label: "4", aliases: ["4"] },
  { id: "5", label: "5", aliases: ["5"] },
  { id: "6", label: "6", aliases: ["6"] },
  { id: "7", label: "7", aliases: ["7"] },
  { id: "8", label: "8", aliases: ["8"] },
  { id: "9", label: "9", secondary: "Kanal", aliases: ["9"] },
  { id: "delete", label: "Löschen", secondary: "DEL", aliases: ["loeschen", "loschen", "clear", "clr", "delete", "del", "korrektur", "cancel"] },
  { id: "0", label: "0", secondary: "Uhrzeit/Datum", aliases: ["0", "uhrzeit datum", "time date"] },
  { id: "enter", label: "Eingabe", secondary: "Quitt.", aliases: ["eingabe", "quitt", "enter", "ok", "confirm", "bestaetigen"] }
];

const ibisTokens = (value: string) =>
  value.toLowerCase()
    .replace(/[^a-z0-9]+/g, " ")
    .trim()
    .split(/\s+/)
    .filter(Boolean);

const detectIbisProfile = (events: string[]): IbisProfile => {
  const all = events.join(" ").toLowerCase();
  if (all.includes("atron")) return { id: "atron", name: "ATRON", subtitle: "perfil detectado" };
  if (all.includes("almex")) return { id: "almex", name: "ALMEX", subtitle: "perfil detectado" };
  if (all.includes("efad") || all.includes("fahrscheindrucker")) return { id: "efad", name: "EFAD / AFR", subtitle: "perfil detectado" };
  if (all.includes("lawo") || all.includes("krueger") || all.includes("matrix")) return { id: "matrix", name: "MATRIX", subtitle: "perfil detectado" };
  return { id: "classic", name: "IBIS 2", subtitle: "layout clássico OMSI" };
};

const resolveIbisEvent = (events: string[], definition: IbisKeyDefinition) => {
  let best: { eventName: string; score: number } | null = null;
  for (const eventName of events) {
    const tokens = ibisTokens(eventName);
    const normalized = tokens.join(" ");
    for (const alias of definition.aliases) {
      const aliasTokens = ibisTokens(alias);
      const exactTokenMatch = aliasTokens.length === 1 && tokens.includes(aliasTokens[0]);
      const phraseMatch = aliasTokens.length > 1 && normalized.includes(aliasTokens.join(" "));
      const suffixMatch = normalized.endsWith(aliasTokens.join(" "));
      const score = suffixMatch ? 30 : phraseMatch ? 24 : exactTokenMatch ? 18 : 0;
      if (score > 0 && (!best || score > best.score)) best = { eventName, score };
    }
  }
  return best?.eventName || null;
};

function RouteMap({
  state,
  roadmapUrl
}: {
  state: MobileState["navigation"];
  roadmapUrl?: string | null;
}) {
  const points = state.routePoints || [];
  const rejoin = state.rejoinPoints || [];
  const stops = state.stopPoints || [];
  const vehicle = state.vehicle;
  const mapBounds = state.bounds || null;

  const displayY = (value: number) =>
    mapBounds
      ? mapBounds.minY + mapBounds.maxY - value
      : -value;

  const bounds = useMemo(() => {
    const all = [
      ...points.map(point => ({ x: point.x, y: displayY(point.y) })),
      ...rejoin.map(point => ({ x: point.x, y: displayY(point.y) })),
      ...stops.map(point => ({ x: point.x, y: displayY(point.y) })),
      ...(vehicle ? [{ x: vehicle.x, y: displayY(vehicle.y) }] : [])
    ];
    if (!all.length) {
      if (!mapBounds) return null;
      return {
        minX: mapBounds.minX,
        minY: mapBounds.minY,
        width: Math.max(1, mapBounds.maxX - mapBounds.minX),
        height: Math.max(1, mapBounds.maxY - mapBounds.minY)
      };
    }

    const xs = all.map(point => point.x);
    const ys = all.map(point => point.y);
    const minX0 = Math.min(...xs);
    const maxX0 = Math.max(...xs);
    const minY0 = Math.min(...ys);
    const maxY0 = Math.max(...ys);
    const px = Math.max(45, (maxX0 - minX0) * 0.12);
    const py = Math.max(45, (maxY0 - minY0) * 0.12);

    return {
      minX: minX0 - px,
      minY: minY0 - py,
      width: Math.max(90, maxX0 - minX0 + px * 2),
      height: Math.max(90, maxY0 - minY0 + py * 2)
    };
  }, [
    points,
    rejoin,
    stops,
    vehicle?.x,
    vehicle?.y,
    mapBounds?.minX,
    mapBounds?.minY,
    mapBounds?.maxX,
    mapBounds?.maxY
  ]);

  if (!bounds || !vehicle) {
    return (
      <div className="map-empty">
        {roadmapUrl
          ? "Roadmap carregado. Aguardando posição real do ônibus…"
          : "Aguardando mapa, rota e posição reais do OMSI…"}
      </div>
    );
  }

  const mp = (point: Point) => `${point.x},${displayY(point.y)}`;
  const vehicleY = displayY(vehicle.y);
  const mapWidth = mapBounds ? Math.max(1, mapBounds.maxX - mapBounds.minX) : 0;
  const mapHeight = mapBounds ? Math.max(1, mapBounds.maxY - mapBounds.minY) : 0;

  return (
    <svg
      className="route-map"
      viewBox={`${bounds.minX} ${bounds.minY} ${bounds.width} ${bounds.height}`}
      preserveAspectRatio="xMidYMid meet"
    >
      {roadmapUrl && mapBounds && (
        <image
          className="roadmap-image"
          href={roadmapUrl}
          x={mapBounds.minX}
          y={mapBounds.minY}
          width={mapWidth}
          height={mapHeight}
          preserveAspectRatio="none"
        />
      )}
      <polyline className="route-line" points={points.map(mp).join(" ")} />
      {rejoin.length > 1 && (
        <polyline className="rejoin-line" points={rejoin.map(mp).join(" ")} />
      )}
      {stops.map((stop, index) => (
        <g key={`${stop.name}-${index}`} transform={`translate(${stop.x} ${displayY(stop.y)})`}>
          <circle r={stop.isNext ? 9 : 5.5} className={stop.isNext ? "route-stop next" : "route-stop"} />
          {stop.isNext && (
            <text y="-13" textAnchor="middle" className="route-stop-label">
              {stop.name}
            </text>
          )}
        </g>
      ))}
      <g transform={`translate(${vehicle.x} ${vehicleY}) rotate(${vehicle.headingDegrees || 0})`}>
        <circle r="13" className="bus-ring" />
        <path d="M0 -13 L8 9 L0 5 L-8 9 Z" className="bus-arrow" />
      </g>
    </svg>
  );
}

function SessionMap({ points }: { points: SessionPoint[] }) {
  const projected = useMemo(() => {
    if (!points.length) return [];
    const minX = Math.min(...points.map(p => p.x)), maxX = Math.max(...points.map(p => p.x));
    const minY = Math.min(...points.map(p => p.y)), maxY = Math.max(...points.map(p => p.y));
    const dx = Math.max(1, maxX - minX), dy = Math.max(1, maxY - minY);
    return points.map(p => ({
      ...p,
      sx: 55 + ((p.x - minX) / dx) * 890,
      sy: 545 - ((p.y - minY) / dy) * 490
    }));
  }, [points]);

  if (!projected.length) return <div className="map-empty">Nenhum player compatível no mapa agora.</div>;
  return <svg className="session-map" viewBox="0 0 1000 600">
    <rect x="0" y="0" width="1000" height="600" rx="28" className="session-bg" />
    {projected.map(p => <g key={p.playerId} transform={`translate(${p.sx} ${p.sy})`}>
      <circle r={p.isLocal ? 20 : 16} className={p.isLocal ? "session-local" : p.kind === "roleplay" ? "session-rp" : "session-remote"} />
      <circle r={p.isLocal ? 13 : 10} className="session-halo" />
      <path d="M0 -14 L8 10 L0 6 L-8 10 Z" className="session-arrow" transform={`rotate(${p.headingDegrees || 0})`} />
      <circle r="2.6" className="session-center" />
      <text y="-26" textAnchor="middle" className="session-name">{p.displayName}</text>
      <text y="34" textAnchor="middle" className="session-detail">{p.line || p.activity || ""}</text>
    </g>)}
  </svg>;
}

function Gauge({ label, value, text }: { label: string; value?: number | null; text?: string }) {
  return <div className="gauge-card"><div><small>{label}</small><strong>{text ?? fmtPercent(value)}</strong></div><div className="gauge-track"><span style={{ width: `${clampPct(value)}%` }} /></div></div>;
}

const normalizeServer = (value: string) => {
  const raw = value.trim();
  if (!raw) return "";
  const withScheme = /^https?:\/\//i.test(raw) ? raw : `http://${raw}`;
  const url = new URL(withScheme);
  if (!url.port) url.port = "27731";
  return url.origin;
};

export default function App() {
  const native = Capacitor.isNativePlatform();
  const browserOrigin = typeof window !== "undefined" && /^https?:$/i.test(window.location.protocol) ? window.location.origin : "";
  const [serverBase, setServerBase] = useState(() => localStorage.getItem("navbr-mobile-server") || (native ? "" : browserOrigin));
  const [serverDraft, setServerDraft] = useState(serverBase);
  const [pairing, setPairing] = useState(() => localStorage.getItem("navbr-mobile-pairing") || "");
  const [draft, setDraft] = useState(pairing);
  const [state, setState] = useState<MobileState | null>(null);
  const [roadmapObjectUrl, setRoadmapObjectUrl] = useState<string | null>(null);
  const [tab, setTab] = useState<Tab>("gps");
  const [operationLineDraft, setOperationLineDraft] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [discovering, setDiscovering] = useState(false);
  const [pttHeld, setPttHeld] = useState(false);
  const [controlFilter, setControlFilter] = useState("");
  const [ibisFilter, setIbisFilter] = useState("");
  const [ibisTechnicalOpen, setIbisTechnicalOpen] = useState(false);
  const [controlFavorites, setControlFavorites] = useState<string[]>(() => {
    try {
      const parsed = JSON.parse(localStorage.getItem("navbr-mobile-control-favorites") || "[]");
      return Array.isArray(parsed) ? parsed.filter((v): v is string => typeof v === "string").slice(0, 32) : [];
    } catch {
      return [];
    }
  });

  const autoDiscover = async () => {
    if (!native) return;
    setDiscovering(true); setError(null);
    try {
      const found = await NavBrDiscovery.discover({ timeoutMs: 3500 });
      const resolved = `http://${found.host}:${found.httpPort || 27731}`;
      const code = (found.pairingCode || "").trim().toUpperCase();
      if (!found.host || !code) throw new Error("Resposta inválida.");
      localStorage.setItem("navbr-mobile-server", resolved);
      localStorage.setItem("navbr-mobile-pairing", code);
      setServerBase(resolved); setServerDraft(resolved); setPairing(code); setDraft(code);
    } catch {
      setError("NavBR não encontrado automaticamente na rede. O modo manual continua disponível.");
    } finally { setDiscovering(false); }
  };

  const sendCommand = async (action: string, payload: Record<string, unknown> = {}) => {
    if (!serverBase || !pairing) return false;
    try {
      const response = await fetch(`${serverBase}/api/mobile/command`, {
        method: "POST",
        headers: { "Content-Type": "application/json", "X-NavBR-Mobile-Code": pairing },
        body: JSON.stringify({ action, ...payload }),
        cache: "no-store",
        mode: "cors"
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const result = await response.json() as { success?: boolean; error?: string | null };
      if (result.success === false) throw new Error(result.error || "Comando recusado.");
      return true;
    } catch (e) {
      setError(`Comando mobile: ${e instanceof Error ? e.message : "falhou"}`);
      return false;
    }
  };

  useEffect(() => {
    if (!native || serverBase || pairing) return;
    void autoDiscover();
    const timer = window.setInterval(() => { if (!serverBase && !pairing) void autoDiscover(); }, 6000);
    return () => window.clearInterval(timer);
  }, [native, serverBase, pairing]);

  useEffect(() => {
    if (!pairing || !serverBase) return;
    let active = true;
    let reading = false;

    const read = async () => {
      if (reading || !active) return;
      reading = true;
      try {
        const response = await fetch(`${serverBase}/api/mobile/state`, {
          headers: { "X-NavBR-Mobile-Code": pairing },
          cache: "no-store",
          mode: "cors"
        });
        if (response.status === 401) throw new Error("Código de pareamento inválido.");
        if (!response.ok) throw new Error(`NavBR respondeu HTTP ${response.status}.`);
        const next = await response.json() as MobileState;
        if (active) {
          setState(next);
          setError(null);
        }
      } catch (e) {
        if (active) setError(e instanceof Error ? e.message : "Falha ao acessar o NavBR no PC.");
      } finally {
        reading = false;
      }
    };

    const resume = () => {
      if (document.visibilityState === "visible") {
        void read();
      }
    };

    void read();
    const timer = window.setInterval(() => {
      if (document.visibilityState === "visible") {
        void read();
      }
    }, 700);

    document.addEventListener("visibilitychange", resume);
    window.addEventListener("online", resume);
    window.addEventListener("pageshow", resume);

    return () => {
      active = false;
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", resume);
      window.removeEventListener("online", resume);
      window.removeEventListener("pageshow", resume);
    };
  }, [pairing, serverBase]);
  useEffect(() => {
    if (!pairing || !serverBase || !state?.navigation.roadmapAvailable) {
      setRoadmapObjectUrl(null);
      return;
    }

    const controller = new AbortController();
    let active = true;
    let createdUrl: string | null = null;

    const loadRoadmap = async () => {
      try {
        const response = await fetch(`${serverBase}/api/mobile/roadmap`, {
          headers: { "X-NavBR-Mobile-Code": pairing },
          cache: "no-store",
          mode: "cors",
          signal: controller.signal
        });
        if (!response.ok) {
          throw new Error(`Roadmap HTTP ${response.status}`);
        }

        const blob = await response.blob();
        createdUrl = URL.createObjectURL(blob);
        if (active) {
          setRoadmapObjectUrl(createdUrl);
        }
      } catch (error) {
        if (!controller.signal.aborted && active) {
          setRoadmapObjectUrl(null);
        }
      }
    };

    void loadRoadmap();
    return () => {
      active = false;
      controller.abort();
      if (createdUrl) {
        URL.revokeObjectURL(createdUrl);
      }
    };
  }, [
    pairing,
    serverBase,
    state?.navigation.mapName,
    state?.navigation.roadmapAvailable
  ]);

  useEffect(() => {
    const releasePttOnSuspend = () => {
      if (document.visibilityState !== "visible") {
        setPttHeld(false);
      }
    };
    document.addEventListener("visibilitychange", releasePttOnSuspend);
    window.addEventListener("pagehide", releasePttOnSuspend);
    return () => {
      document.removeEventListener("visibilitychange", releasePttOnSuspend);
      window.removeEventListener("pagehide", releasePttOnSuspend);
    };
  }, []);


  useEffect(() => {
    setOperationLineDraft(state?.hud?.navBrTpTsManualLine || "");
  }, [state?.hud?.navBrTpTsManualLine]);

  useEffect(() => {
    if (!pttHeld) return;
    void sendCommand("voice-ptt", { active: true });
    const timer = window.setInterval(() => void sendCommand("voice-ptt", { active: true }), 700);
    return () => {
      window.clearInterval(timer);
      void sendCommand("voice-ptt", { active: false });
    };
  }, [pttHeld, serverBase, pairing]);

  const toggleControlFavorite = (eventName: string) => {
    setControlFavorites(current => {
      const next = current.includes(eventName)
        ? current.filter(item => item !== eventName)
        : [...current, eventName].slice(-32);
      localStorage.setItem("navbr-mobile-control-favorites", JSON.stringify(next));
      return next;
    });
  };

  const pair = (e: FormEvent) => {
    e.preventDefault();
    const value = draft.trim().toUpperCase();
    let resolved = "";
    try { resolved = normalizeServer(serverDraft || browserOrigin); }
    catch { setError("Endereço do PC inválido."); return; }
    if (!resolved) { setError("Informe o endereço do PC onde o NavBR está aberto."); return; }
    localStorage.setItem("navbr-mobile-server", resolved);
    localStorage.setItem("navbr-mobile-pairing", value);
    setServerBase(resolved); setPairing(value); setError(null);
  };

  if (!pairing || !serverBase || (!state && error?.includes("pareamento"))) {
    return <main className="pair-shell">
      <div className="brand-mark">N</div><span className="eyebrow">NAVBR MOBILE ALPHA 2</span><h1>Conectar ao PC</h1>
      <p>{native ? "O app procura o NavBR automaticamente na mesma rede." : "Digite o código mostrado no NavBR."}</p>
      {native && <button type="button" className="auto-discover" disabled={discovering} onClick={() => void autoDiscover()}>{discovering ? "Procurando NavBR…" : "Procurar automaticamente"}</button>}
      <form onSubmit={pair}>
        {native && <input value={serverDraft} onChange={e => setServerDraft(e.target.value)} placeholder="Fallback: IP do PC · 192.168.0.10" inputMode="url" />}
        <input value={draft} onChange={e => setDraft(e.target.value)} placeholder="Fallback: código · A1B2C3D4" maxLength={8} />
        <button>Conectar manualmente</button>
      </form>
      {error && <div className="error">{error}</div>}
    </main>;
  }

  const nav = state?.navigation, vehicle = state?.vehicle, mp = state?.multiplayer;
  const op = state?.operation;
  const hud = state?.hud;
  const availableControls = (state?.vehicleControls.detectedEvents || [])
    .filter(name => !controlFilter.trim() || name.toLowerCase().includes(controlFilter.trim().toLowerCase()))
    .sort((a, b) => {
      const favoriteOrder = Number(controlFavorites.includes(b)) - Number(controlFavorites.includes(a));
      return favoriteOrder || a.localeCompare(b);
    });

  const availableIbisControls = (state?.ibis.detectedEvents || [])
    .filter(name => !ibisFilter.trim() || name.toLowerCase().includes(ibisFilter.trim().toLowerCase()))
    .sort((a, b) => a.localeCompare(b));

  const ibisProfile = detectIbisProfile(state?.ibis.detectedEvents || []);
  const ibisFunctionKeys = IBIS_FUNCTION_KEYS.map(definition => ({
    ...definition,
    eventName: resolveIbisEvent(state?.ibis.detectedEvents || [], definition)
  }));
  const ibisNumericKeys = IBIS_NUMERIC_KEYS.map(definition => ({
    ...definition,
    eventName: resolveIbisEvent(state?.ibis.detectedEvents || [], definition)
  }));
  const mappedIbisEvents = new Set(
    [...ibisFunctionKeys, ...ibisNumericKeys].flatMap(key => key.eventName ? [key.eventName] : [])
  );
  const unmappedIbisControls = availableIbisControls.filter(eventName => !mappedIbisEvents.has(eventName));

  return <main className="app-shell">
    <header className="mobile-header">
      <div><span className="eyebrow">NAVBR MOBILE · ALPHA 2</span><strong>{vehicle?.line || "—"} <i>·</i> {vehicle?.route || "—"}</strong></div>
      <span className={`live-dot ${state?.omsi.inGame ? "online" : ""}`}>{state?.omsi.inGame ? "OMSI" : "AGUARDANDO"}</span>
    </header>
    {error && <div className="error compact">{error}</div>}

    <section className="content">
      {tab === "gps" && <div>
        <div className="hero-card"><div><small>PRÓXIMA PARADA</small><h2>{nav?.nextStopName || vehicle?.nextStopName || "Aguardando rota"}</h2><span>{nav?.currentStreetName || vehicle?.currentStreetName || state?.omsi.mapName || "—"}</span></div><div className="speed"><strong>{Math.round(vehicle?.speedKph || 0)}</strong><span>km/h</span></div></div>
        <div className="maneuver-card"><div className="maneuver-icon">↑</div><div><small>{nav?.maneuver || "SEM MANOBRA"}</small><strong>{fmtDistance(nav?.distanceToManeuverMeters)}</strong></div><div className={nav?.isOnRoute ? "route-ok" : "route-off"}>{nav?.isOnRoute ? "NA ROTA" : `FORA ${fmtDistance(nav?.offRouteDistanceMeters)}`}</div></div>
        <div className="map-card"><RouteMap
          state={nav || { available: false, isOnRoute: false, offRouteDistanceMeters: 0, routeProgressPercent: 0, distanceRemainingMeters: 0, maneuver: "None", routePoints: [], rejoinPoints: [] }}
          roadmapUrl={roadmapObjectUrl}
        /></div>
        <div className="metric-grid"><div><small>PRÓXIMA</small><strong>{fmtDistance(nav?.distanceToNextStopMeters)}</strong></div><div><small>ETA</small><strong>{fmtEta(nav?.etaToNextStopSeconds)}</strong></div><div><small>RESTANTE</small><strong>{fmtDistance(nav?.distanceRemainingMeters)}</strong></div><div><small>PROGRESSO</small><strong>{Math.round(nav?.routeProgressPercent || 0)}%</strong></div></div>
        {!!nav?.stopSequence?.upcomingStops?.length && <div className="stops-card"><small>PRÓXIMAS PARADAS</small>{nav.stopSequence.upcomingStops.map((s, i) => <div key={s + i}><b>{i + 1}</b><span>{s}</span></div>)}</div>}
      </div>}

      {tab === "bus" && <div className="bus-page">
        <div className="bus-hero"><div><small>ÔNIBUS</small><h2>{vehicle?.vehicleName || "Aguardando veículo"}</h2><span>{vehicle?.currentStreetName || state?.omsi.mapName || "—"}</span></div><strong>{Math.round(vehicle?.speedKph || 0)}<small> km/h</small></strong></div>
        <div className="gauge-grid">
          <Gauge label="COMBUSTÍVEL" value={vehicle?.fuelPercent} />
          <Gauge label="ACELERADOR" value={vehicle?.throttlePercent} />
          <Gauge label="FREIO" value={vehicle?.brakePercent} />
          <Gauge label="DIREÇÃO" value={vehicle?.steeringDegrees == null ? null : Math.min(100, Math.abs(vehicle.steeringDegrees))} text={vehicle?.steeringDegrees == null ? "—" : `${Math.round(vehicle.steeringDegrees)}°`} />
        </div>
        <div className="vehicle-status-grid">
          <StatusChip label="PORTAS" value={vehicle?.doors || "—"} active={!!vehicle?.doors && vehicle.doors !== "None"} />
          <StatusChip label="LUZES" value={vehicle?.lights || "—"} active={!!vehicle?.lights && vehicle.lights !== "None"} />
          <StatusChip label="SETA" value={vehicle?.turnSignal || "Off"} active={!!vehicle?.turnSignal && vehicle.turnSignal !== "Off"} />
          <StatusChip label="LIMPADOR" value={vehicle?.wipersActive ? "LIGADO" : "DESLIGADO"} active={!!vehicle?.wipersActive} />
          <StatusChip label="FREIO MÃO" value={vehicle?.parkingBrakeActive ? "ATIVO" : "SOLTO"} active={!!vehicle?.parkingBrakeActive} />
          <StatusChip label="RÉ" value={vehicle?.reverseGear ? "ENGATADA" : "NÃO"} active={!!vehicle?.reverseGear} />
          <StatusChip label="PARADA" value={vehicle?.stopRequested ? "SOLICITADA" : "NÃO"} active={!!vehicle?.stopRequested} />
          <StatusChip label="BUZINA" value={vehicle?.hornActive ? "ATIVA" : "NÃO"} active={!!vehicle?.hornActive} />
        </div>
        <div className="telemetry-card"><StatusRow label="Aceleração" value={vehicle?.accelerationMps2 == null ? "—" : `${vehicle.accelerationMps2.toFixed(2)} m/s²`} /><StatusRow label="Rumo" value={vehicle?.headingDegrees == null ? "—" : `${Math.round(vehicle.headingDegrees)}°`} /><StatusRow label="Atraso" value={vehicle?.delaySeconds == null ? "—" : `${vehicle.delaySeconds > 0 ? "+" : ""}${vehicle.delaySeconds}s`} /><StatusRow label="Eventos reais detectados" value={String(state?.vehicleControls.detectedEvents.length || 0)} /></div>

        <div className="vehicle-controls-card">
          <div className="section-title">
            <div><small>CONTROLES REAIS</small><h2>Painel do veículo</h2></div>
            <b className={state?.vehicleControls.writable ? "good" : "bad"}>{state?.vehicleControls.writable ? "LIBERADO" : "BLOQUEADO"}</b>
          </div>
          <input className="control-search" value={controlFilter} onChange={e => setControlFilter(e.target.value)} placeholder="Buscar evento do ônibus…" />
          {availableControls.length === 0
            ? <div className="map-empty compact-empty">Nenhum mouse event real encontrado para este veículo.</div>
            : <div className="vehicle-control-grid">
                {availableControls.map(eventName => {
                  const favorite = controlFavorites.includes(eventName);
                  return <div className={`vehicle-control-item ${favorite ? "favorite" : ""}`} key={eventName}>
                    <button className="favorite-button" onClick={() => toggleControlFavorite(eventName)} aria-label="Favoritar controle">{favorite ? "★" : "☆"}</button>
                    <button
                      className="vehicle-trigger-button"
                      disabled={!state?.vehicleControls.writable}
                      onPointerDown={e => {
                        e.currentTarget.setPointerCapture(e.pointerId);
                        navigator.vibrate?.(12);
                        void sendCommand("vehicle-trigger", { triggerName: eventName, active: true });
                      }}
                      onPointerUp={() => void sendCommand("vehicle-trigger", { triggerName: eventName, active: false })}
                      onPointerCancel={() => void sendCommand("vehicle-trigger", { triggerName: eventName, active: false })}
                      onLostPointerCapture={() => void sendCommand("vehicle-trigger", { triggerName: eventName, active: false })}
                    >
                      <strong>{friendlyControlName(eventName)}</strong>
                      <small>{eventName}</small>
                    </button>
                  </div>;
                })}
              </div>}
        </div>

        {!state?.vehicleControls.writable && <div className="ibis-warning">
          {!state?.vehicleControls.enabled
            ? "Ative “Controles do ônibus pelo celular (EXPERIMENTAL)” no NavBR do PC."
            : !state?.vehicleControls.capabilityAvailable
              ? "A autorização está ligada, mas o Plugin Bridge ainda não informou a capacidade local-vehicle-trigger."
              : "O NavBR ainda não confirmou eventos reais utilizáveis para este ônibus."}
        </div>}
      </div>}

      {tab === "ibis" && <div className="ibis-page">
        <div className="ibis-head"><span>IBIS MOBILE</span><b>{state?.ibis.writable ? "CONECTADO AO VEÍCULO" : "LEITURA"}</b></div>

        <div className={`ibis-console profile-${ibisProfile.id}`}>
          <div className="ibis-console-top">
            <div className="ibis-brand">
              <span className="ibis-brand-main">{ibisProfile.name}</span>
              <small>{ibisProfile.subtitle}</small>
            </div>
            <div className={`ibis-led ${state?.ibis.writable ? "on" : ""}`}><i />{state?.ibis.writable ? "BEREIT" : "READ"}</div>
          </div>

          <div className="ibis-device-grid">
            <div className="ibis-main-panel">
              <div className="ibis-lcd" role="status" aria-label="Visor do IBIS">
                <div className="ibis-lcd-row ibis-lcd-primary">
                  <span>LIN {state?.ibis.line || "----"}</span>
                  <span>KRS {state?.ibis.route || "--"}</span>
                </div>
                <div className="ibis-lcd-destination">{state?.ibis.destination || "KEIN ZIEL / SEM DESTINO"}</div>
                <div className="ibis-lcd-row">
                  <span>{state?.ibis.nextStop || "Aguardando próxima parada"}</span>
                  <span>{state?.ibis.delaySeconds == null ? "--:--" : `${state.ibis.delaySeconds > 0 ? "+" : ""}${state.ibis.delaySeconds}s`}</span>
                </div>
                <div className="ibis-lcd-footer">HOF {state?.ibis.hof || "—"} · EVENTOS {state?.ibis.detectedEvents.length || 0}</div>
              </div>

              <div className="ibis-function-pad">
                {ibisFunctionKeys.map((key, index) => <button
                  key={key.id}
                  type="button"
                  className={`ibis-key ibis-function-key function-${index + 1} ${key.eventName ? "mapped" : "unmapped"}`}
                  disabled={!state?.ibis.writable || !key.eventName}
                  title={key.eventName || "Função não exposta por este ônibus"}
                  onPointerDown={e => {
                    if (!key.eventName) return;
                    e.currentTarget.setPointerCapture(e.pointerId);
                    navigator.vibrate?.(12);
                    void sendCommand("ibis-trigger", { triggerName: key.eventName, active: true });
                  }}
                  onPointerUp={() => key.eventName && void sendCommand("ibis-trigger", { triggerName: key.eventName, active: false })}
                  onPointerCancel={() => key.eventName && void sendCommand("ibis-trigger", { triggerName: key.eventName, active: false })}
                  onLostPointerCapture={() => key.eventName && void sendCommand("ibis-trigger", { triggerName: key.eventName, active: false })}
                >
                  <strong>{key.label}</strong>
                  {key.secondary && <small>{key.secondary}</small>}
                  <em>{key.eventName ? "●" : "×"}</em>
                </button>)}
              </div>
            </div>

            <div className="ibis-number-pad">
              {ibisNumericKeys.map(key => <button
                key={key.id}
                type="button"
                className={`ibis-key ibis-number-key key-${key.id} ${key.eventName ? "mapped" : "unmapped"}`}
                disabled={!state?.ibis.writable || !key.eventName}
                title={key.eventName || "Função não exposta por este ônibus"}
                onPointerDown={e => {
                  if (!key.eventName) return;
                  e.currentTarget.setPointerCapture(e.pointerId);
                  navigator.vibrate?.(12);
                  void sendCommand("ibis-trigger", { triggerName: key.eventName, active: true });
                }}
                onPointerUp={() => key.eventName && void sendCommand("ibis-trigger", { triggerName: key.eventName, active: false })}
                onPointerCancel={() => key.eventName && void sendCommand("ibis-trigger", { triggerName: key.eventName, active: false })}
                onLostPointerCapture={() => key.eventName && void sendCommand("ibis-trigger", { triggerName: key.eventName, active: false })}
              >
                <strong>{key.label}</strong>
                {key.secondary && <small>{key.secondary}</small>}
                <em>{key.eventName ? "●" : "×"}</em>
              </button>)}
            </div>
          </div>

          <div className="ibis-console-legend">
            <span><i className="mapped-dot" /> função real mapeada</span>
            <span><i className="unmapped-dot" /> não exposta neste ônibus</span>
          </div>
        </div>

        <div className="ibis-live-strip">
          <div><small>LINHA</small><strong>{state?.ibis.line || "—"}</strong></div>
          <div><small>CURSO</small><strong>{state?.ibis.route || "—"}</strong></div>
          <div><small>ATRASO</small><strong>{state?.ibis.delaySeconds == null ? "—" : `${state.ibis.delaySeconds > 0 ? "+" : ""}${state.ibis.delaySeconds}s`}</strong></div>
        </div>

        <button type="button" className="ibis-technical-toggle" onClick={() => setIbisTechnicalOpen(v => !v)}>
          {ibisTechnicalOpen ? "Ocultar eventos técnicos" : `Eventos técnicos (${unmappedIbisControls.length})`}
        </button>

        {ibisTechnicalOpen && <div className="vehicle-controls-card ibis-technical-panel">
          <div className="section-title"><div><small>DIAGNÓSTICO</small><h2>Eventos reais adicionais</h2></div><b>{ibisProfile.name}</b></div>
          <input className="control-search" value={ibisFilter} onChange={e => setIbisFilter(e.target.value)} placeholder="Filtrar eventos reais…" />
          {unmappedIbisControls.length === 0
            ? <div className="map-empty compact-empty">Todos os eventos detectados já estão associados ao painel.</div>
            : <div className="vehicle-control-grid">
                {unmappedIbisControls.map(eventName => <div className="vehicle-control-item" key={eventName}>
                  <button
                    className="vehicle-trigger-button"
                    disabled={!state?.ibis.writable}
                    onPointerDown={e => {
                      e.currentTarget.setPointerCapture(e.pointerId);
                      navigator.vibrate?.(12);
                      void sendCommand("ibis-trigger", { triggerName: eventName, active: true });
                    }}
                    onPointerUp={() => void sendCommand("ibis-trigger", { triggerName: eventName, active: false })}
                    onPointerCancel={() => void sendCommand("ibis-trigger", { triggerName: eventName, active: false })}
                    onLostPointerCapture={() => void sendCommand("ibis-trigger", { triggerName: eventName, active: false })}
                  >
                    <strong>{friendlyControlName(eventName)}</strong>
                    <small>{eventName}</small>
                  </button>
                </div>)}
              </div>}
        </div>}

        {!state?.ibis.writable && <div className="ibis-warning">
          {!state?.vehicleControls.enabled
            ? "Ative “Controles do ônibus pelo celular (EXPERIMENTAL)” no NavBR do PC. O painel permanece em leitura até a autorização ser habilitada."
            : !state?.vehicleControls.capabilityAvailable
              ? "O Plugin Bridge ainda não informou a capacidade local-vehicle-trigger."
              : "O visor continua real, porém este ônibus não expôs teclas IBIS reconhecíveis no catálogo [mouseevent]."}
        </div>}
      </div>}

      {tab === "ops" && <div className="ops-page">
        <div className="ops-hero">
          <div><small>OPERAÇÃO AO VIVO</small><h2>{op?.ibisLineCourse || vehicle?.line || "—"}</h2><span>{op?.ibisTerminusName || vehicle?.destinationName || "Sem destino"}</span></div>
          <div className="ops-speed"><strong>{Math.round(vehicle?.speedKph || 0)}</strong><span>km/h</span></div>
        </div>

        <div className="ops-grid">
          <div><small>HORA</small><strong>{fmtSimulationTime(op?.simulationTime)}</strong><span>{op?.simulationDay && op?.simulationMonth && op?.simulationYear ? `${String(op.simulationDay).padStart(2, "0")}/${String(op.simulationMonth).padStart(2, "0")}/${op.simulationYear}` : "—"}</span></div>
          <div><small>TEMP. INTERNA</small><strong>{op?.cabinTemperatureC == null ? "—" : `${op.cabinTemperatureC.toFixed(1)} °C`}</strong><span>{op?.available ? "OMSI" : "sem dados"}</span></div>
          <div><small>PASSAGEIROS</small><strong>{op?.passengerCount ?? "—"}</strong><span>{op?.scheduleActive === false ? "sem horário" : "em operação"}</span></div>
          <div><small>PONTUALIDADE</small><strong>{fmtOperationalDelay(op, vehicle?.delaySeconds)}</strong><span>{op?.simulationPaused ? "SIMULAÇÃO PAUSADA" : op?.ibisDelayState || "monitorando"}</span></div>
        </div>

        <div className="ops-control-card">
          <div className="section-title"><div><small>HUD DO PC</small><h2>Sobreposição NavBR</h2></div><b className={hud?.enabled ? "good" : "bad"}>{hud?.enabled ? "ATIVO" : "OCULTO"}</b></div>
          <div className="ops-actions">
            <button className={hud?.enabled ? "active-control" : ""} onClick={() => void sendCommand("hud-enabled", { enabled: !hud?.enabled })}>{hud?.enabled ? "Ocultar HUD" : "Mostrar HUD"}</button>
            <button className={hud?.navBrTpTsEnabled ? "active-control" : ""} onClick={() => void sendCommand("navbr-tpts-configure", { enabled: !hud?.navBrTpTsEnabled })}>{hud?.navBrTpTsEnabled ? "Ocultar NavBR TP/TS" : "Mostrar NavBR TP/TS"}</button>
          </div>
          <div className="ops-actions three">
            <button onClick={() => void sendCommand("navbr-tpts-configure", { theme: ((hud?.navBrTpTsTheme || 0) + 1) % 3 })}>Tema {["Menta","Âmbar","Gelo"][hud?.navBrTpTsTheme || 0]}</button>
            <button onClick={() => void sendCommand("navbr-tpts-configure", { size: ((hud?.navBrTpTsSize || 0) + 1) % 3 })}>Tamanho {["Normal","Grande","Compacto"][hud?.navBrTpTsSize || 0]}</button>
            <button className={hud?.navBrTpTsAutoDirection ? "active-control" : ""} onClick={() => void sendCommand("navbr-tpts-configure", { autoDirection: !hud?.navBrTpTsAutoDirection })}>{hud?.navBrTpTsAutoDirection ? "TP/TS Auto" : "TP/TS Manual"}</button>
          </div>
          <div className="ops-manual">
            <label>LINHA MANUAL<input value={operationLineDraft} onChange={e => setOperationLineDraft(e.target.value.slice(0, 24))} placeholder={vehicle?.line || "Ex.: 76"} /></label>
            <div className="ops-direction">
              <button className={hud?.navBrTpTsManualDirection !== "TS" ? "active-control" : ""} onClick={() => void sendCommand("navbr-tpts-configure", { autoDirection: false, direction: "TP", line: operationLineDraft || undefined })}>TP</button>
              <button className={hud?.navBrTpTsManualDirection === "TS" ? "active-control" : ""} onClick={() => void sendCommand("navbr-tpts-configure", { autoDirection: false, direction: "TS", line: operationLineDraft || undefined })}>TS</button>
              <button onClick={() => void sendCommand("navbr-tpts-configure", { line: operationLineDraft || undefined, direction: hud?.navBrTpTsManualDirection || "TP", autoDirection: !!hud?.navBrTpTsAutoDirection })}>Salvar</button>
            </div>
          </div>
        </div>

        <div className="ops-control-card">
          <div className="section-title"><div><small>HOF DO ÔNIBUS</small><h2>Rotas disponíveis</h2></div><b>{op?.hofRoutes?.length || 0}</b></div>
          <div className="player-list">
            {(op?.hofRoutes || []).slice(0, 64).map(route => (
              <button className="player-card" key={`${route.line}|${route.route}`}
                onClick={() => { setOperationLineDraft(route.line); void sendCommand("navbr-tpts-configure", { line: route.line, autoDirection: false }); }}>
                <div className="player-main"><strong>{route.line} · rota {route.route}</strong><span>{route.description || "Sem descrição"}</span><small>{route.hofFile}{route.destinationCode ? ` · destino ${route.destinationCode}` : ""}</small></div>
              </button>
            ))}
          </div>
        </div>

        <div className="telemetry-card ops-status">
          <StatusRow label="Telemetria operacional" value={op?.available ? "ATUAL" : "AGUARDANDO"} ok={!!op?.available} />
          <StatusRow label="Linha / curso" value={op?.ibisLineCourse || state?.ibis.line || "—"} />
          <StatusRow label="Rota IBIS" value={op?.ibisRouteCode || state?.ibis.route || "—"} />
          <StatusRow label="Terminal" value={op?.ibisTerminusName || state?.ibis.destination || "—"} />
          <StatusRow label="Horário ativo" value={op?.scheduleActive == null ? "—" : op.scheduleActive ? "SIM" : "NÃO"} ok={op?.scheduleActive ?? undefined} />
        </div>
      </div>}

      {tab === "multi" && <div className="multi-page">
        <div className="section-title"><div><small>MULTIPLAYER</small><h2>{mp?.roomId || "Sem sala"}</h2></div><b className={mp?.connected ? "good" : "bad"}>{mp?.connected ? `${mp.playerCount} ONLINE` : "OFFLINE"}</b></div>
        <div className="multiplayer-map-card"><SessionMap points={mp?.sessionPoints || []} /></div>
        <div className="quality-row"><span>Ping <b>{mp?.latencyMs == null ? "—" : `${Math.round(mp.latencyMs)} ms`}</b></span><span>Rede <b>{mp?.networkQuality?.level || "—"}</b></span><span>Perda <b>{mp?.networkQuality?.lossPercent == null ? "—" : `${mp.networkQuality.lossPercent.toFixed(1)}%`}</b></span></div>
        <div className="player-list">{(mp?.players || []).map(p => <div className="player-card" key={p.playerId}><div className={`player-avatar ${p.speaking ? "speaking" : ""}`}>{p.displayName.slice(0, 1).toUpperCase()}</div><div className="player-main"><strong>{p.displayName}{p.isLocal ? " · Você" : ""}</strong><span>{p.line || "—"} · {p.route || "—"} · {p.vehicleName || "Ônibus não informado"}</span><small>{p.destinationName || p.mapName || "—"} · {p.distanceText || ""}</small></div><div className="player-side"><b>{p.speedKph == null ? "—" : `${Math.round(p.speedKph)}`}</b><small>km/h</small></div></div>)}</div>
      </div>}

      {tab === "voice" && <div className="voice-page">
        <div className="section-title"><div><small>RÁDIO / VOZ</small><h2>{mp?.voiceChannel || "general"}</h2></div><b className={mp?.voiceEnabled ? "good" : "bad"}>{mp?.voiceEnabled ? "ATIVA" : "DESLIGADA"}</b></div>
        <div className="voice-actions">
          <button className={mp?.voiceEnabled ? "active-control" : ""} onClick={() => void sendCommand("voice-enabled", { enabled: !mp?.voiceEnabled })}>{mp?.voiceEnabled ? "Desligar voz" : "Ligar voz"}</button>
          <button className={mp?.voiceDeafened ? "active-warning" : ""} onClick={() => void sendCommand("voice-configure", { channel: mp?.voiceChannel || "general", proximityMeters: mp?.voiceProximityMeters || 120, deafened: !mp?.voiceDeafened })}>{mp?.voiceDeafened ? "Recepção mutada" : "Ouvir canal"}</button>
        </div>
        <label className="voice-select">CANAL<select value={mp?.voiceChannel || "general"} onChange={e => void sendCommand("voice-configure", { channel: e.target.value, proximityMeters: mp?.voiceProximityMeters || 120, deafened: !!mp?.voiceDeafened })}><option value="general">Geral</option><option value="company">Empresa</option><option value="dispatch">CCO / Despacho</option><option value="proximity">Proximidade</option></select></label>
        <button className={`ptt-button ${pttHeld || mp?.voicePushToTalkActive ? "pressed" : ""}`} disabled={!mp?.connected || !mp?.voiceEnabled}
          onPointerDown={e => { e.currentTarget.setPointerCapture(e.pointerId); setPttHeld(true); navigator.vibrate?.(25); }}
          onPointerUp={() => setPttHeld(false)} onPointerCancel={() => setPttHeld(false)} onLostPointerCapture={() => setPttHeld(false)}>
          <span>🎙</span><strong>{pttHeld || mp?.voicePushToTalkActive ? "FALANDO" : "SEGURE PARA FALAR"}</strong><small>usa o microfone configurado no PC</small>
        </button>
        <div className="mixer-list">{(mp?.voiceMixers || []).map(m => <div className="mixer-card" key={m.playerId}><div><strong>{m.displayName}</strong><span>{m.speaking ? "Falando agora" : "Silencioso"}</span></div><button className={m.muted ? "muted" : ""} onClick={() => void sendCommand("voice-remote", { playerId: m.playerId, muted: !m.muted, gain: m.gain })}>{m.muted ? "Desmutar" : "Mutar"}</button></div>)}</div>
      </div>}

      {tab === "status" && <div className="status-page">
        <StatusRow label="OMSI detectado" value={state?.omsi.detected ? "SIM" : "NÃO"} ok={!!state?.omsi.detected} />
        <StatusRow label="Dentro do mapa" value={state?.omsi.inGame ? "SIM" : "NÃO"} ok={!!state?.omsi.inGame} />
        <StatusRow label="Plugin Bridge" value={state?.plugin.connected ? "CONECTADO" : "DESCONECTADO"} ok={!!state?.plugin.connected} />
        <StatusRow label="Plugin" value={state?.plugin.version || "—"} />
        <StatusRow label="Contrato mobile" value={`v${state?.version || 0}`} ok={(state?.version || 0) >= 3} />
        <StatusRow label="HUD do PC" value={hud?.enabled ? "ATIVO" : "OCULTO"} ok={!!hud?.enabled} />
        <StatusRow label="Operação OMSI" value={op?.available ? "ATUAL" : "AGUARDANDO"} ok={!!op?.available} />
        <StatusRow label="Capacidades plugin" value={String(state?.plugin.capabilities.length || 0)} />
        <StatusRow label="Mapa" value={state?.omsi.mapName || "—"} />
        <StatusRow label="Ônibus" value={vehicle?.vehicleName || "—"} />
        <StatusRow label="HOF" value={vehicle?.hofName || "—"} />
        <StatusRow label="Sala" value={mp?.connected ? mp.roomId || "Conectada" : "Offline"} ok={!!mp?.connected} />
        <button className="forget" onClick={() => { localStorage.removeItem("navbr-mobile-pairing"); localStorage.removeItem("navbr-mobile-server"); setPairing(""); setServerBase(""); setServerDraft(""); setState(null); }}>Desparear este celular</button>
      </div>}
    </section>

    <nav className="bottom-nav">
      <button className={tab === "gps" ? "active" : ""} onClick={() => setTab("gps")}><span>⌖</span>GPS</button>
      <button className={tab === "bus" ? "active" : ""} onClick={() => setTab("bus")}><span>▰</span>Ônibus</button>
      <button className={tab === "ibis" ? "active" : ""} onClick={() => setTab("ibis")}><span>▣</span>IBIS</button>
      <button className={tab === "ops" ? "active" : ""} onClick={() => setTab("ops")}><span>◫</span>Operação</button>
      <button className={tab === "multi" ? "active" : ""} onClick={() => setTab("multi")}><span>◎</span>Multi</button>
      <button className={tab === "voice" ? "active" : ""} onClick={() => setTab("voice")}><span>◉</span>Voz</button>
      <button className={tab === "status" ? "active" : ""} onClick={() => setTab("status")}><span>●</span>Status</button>
    </nav>
  </main>;
}

function StatusRow({ label, value, ok }: { label: string; value: string; ok?: boolean }) {
  return <div className="status-row"><span>{label}</span><strong className={ok === undefined ? "" : ok ? "good" : "bad"}>{value}</strong></div>;
}
function StatusChip({ label, value, active }: { label: string; value: string; active: boolean }) {
  return <div className={`status-chip ${active ? "on" : ""}`}><small>{label}</small><strong>{value}</strong></div>;
}
