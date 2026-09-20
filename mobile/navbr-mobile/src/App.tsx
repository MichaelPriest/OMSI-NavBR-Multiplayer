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
    routePoints: Point[]; rejoinPoints: Point[];
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

type Tab = "gps" | "bus" | "ibis" | "multi" | "voice" | "status";
type DiscoveryResult = { host: string; httpPort: number; pairingCode: string };
interface NavBrDiscoveryPlugin { discover(options?: { timeoutMs?: number }): Promise<DiscoveryResult> }
const NavBrDiscovery = registerPlugin<NavBrDiscoveryPlugin>("NavBrDiscovery");

const fmtDistance = (v?: number | null) => v == null ? "—" : v >= 1000 ? `${(v / 1000).toFixed(1)} km` : `${Math.round(v)} m`;
const fmtEta = (v?: number | null) => v == null ? "—" : `${Math.max(0, Math.round(v / 60))} min`;
const fmtPercent = (v?: number | null) => v == null ? "—" : `${Math.round(v)}%`;
const clampPct = (v?: number | null) => Math.max(0, Math.min(100, v ?? 0));
const friendlyControlName = (value: string) =>
  value
    .replace(/^bus_/i, "")
    .replace(/^cockpit_/i, "")
    .replace(/[_\-.]+/g, " ")
    .replace(/\b\w/g, c => c.toUpperCase());

function RouteMap({ state }: { state: MobileState["navigation"] }) {
  const points = state.routePoints || [], rejoin = state.rejoinPoints || [], vehicle = state.vehicle;
  const bounds = useMemo(() => {
    const all = [...points, ...rejoin, ...(vehicle ? [vehicle] : [])];
    if (!all.length) return null;
    const xs = all.map(p => p.x), ys = all.map(p => p.y);
    const minX0 = Math.min(...xs), maxX0 = Math.max(...xs), minY0 = Math.min(...ys), maxY0 = Math.max(...ys);
    const px = Math.max(20, (maxX0 - minX0) * .08), py = Math.max(20, (maxY0 - minY0) * .08);
    return { minX: minX0 - px, minY: minY0 - py, width: Math.max(1, maxX0 - minX0 + px * 2), height: Math.max(1, maxY0 - minY0 + py * 2) };
  }, [points, rejoin, vehicle?.x, vehicle?.y]);

  if (!bounds || !vehicle) return <div className="map-empty">Aguardando rota e posição reais do OMSI…</div>;
  const mp = (p: Point) => `${p.x},${bounds.minY + bounds.height - (p.y - bounds.minY)}`;
  const vy = bounds.minY + bounds.height - (vehicle.y - bounds.minY);
  return <svg className="route-map" viewBox={`${bounds.minX} ${bounds.minY} ${bounds.width} ${bounds.height}`} preserveAspectRatio="xMidYMid meet">
    <polyline className="route-line" points={points.map(mp).join(" ")} />
    {rejoin.length > 1 && <polyline className="rejoin-line" points={rejoin.map(mp).join(" ")} />}
    <g transform={`translate(${vehicle.x} ${vy}) rotate(${vehicle.headingDegrees || 0})`}>
      <circle r="13" className="bus-ring" /><path d="M0 -13 L8 9 L0 5 L-8 9 Z" className="bus-arrow" />
    </g>
  </svg>;
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
      <circle r={p.isLocal ? 18 : 14} className={p.isLocal ? "session-local" : p.kind === "roleplay" ? "session-rp" : "session-remote"} />
      <path d="M0 -14 L8 9 L0 5 L-8 9 Z" className="session-arrow" transform={`rotate(${p.headingDegrees || 0})`} />
      <text y="-24" textAnchor="middle" className="session-name">{p.displayName}</text>
      <text y="32" textAnchor="middle" className="session-detail">{p.line || p.activity || ""}</text>
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
  const [tab, setTab] = useState<Tab>("gps");
  const [error, setError] = useState<string | null>(null);
  const [discovering, setDiscovering] = useState(false);
  const [pttHeld, setPttHeld] = useState(false);
  const [controlFilter, setControlFilter] = useState("");
  const [ibisFilter, setIbisFilter] = useState("");
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
    const read = async () => {
      try {
        const response = await fetch(`${serverBase}/api/mobile/state`, { headers: { "X-NavBR-Mobile-Code": pairing }, cache: "no-store", mode: "cors" });
        if (response.status === 401) throw new Error("Código de pareamento inválido.");
        if (!response.ok) throw new Error(`NavBR respondeu HTTP ${response.status}.`);
        const next = await response.json() as MobileState;
        if (active) { setState(next); setError(null); }
      } catch (e) { if (active) setError(e instanceof Error ? e.message : "Falha ao acessar o NavBR no PC."); }
    };
    void read();
    const timer = window.setInterval(read, 700);
    return () => { active = false; window.clearInterval(timer); };
  }, [pairing, serverBase]);

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
  const availableControls = (state?.vehicleControls.detectedEvents || [])
    .filter(name => !controlFilter.trim() || name.toLowerCase().includes(controlFilter.trim().toLowerCase()))
    .sort((a, b) => {
      const favoriteOrder = Number(controlFavorites.includes(b)) - Number(controlFavorites.includes(a));
      return favoriteOrder || a.localeCompare(b);
    });

  const availableIbisControls = (state?.ibis.detectedEvents || [])
    .filter(name => !ibisFilter.trim() || name.toLowerCase().includes(ibisFilter.trim().toLowerCase()))
    .sort((a, b) => a.localeCompare(b));

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
        <div className="map-card"><RouteMap state={nav || { available: false, isOnRoute: false, offRouteDistanceMeters: 0, routeProgressPercent: 0, distanceRemainingMeters: 0, maneuver: "None", routePoints: [], rejoinPoints: [] }} /></div>
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
        <div className="ibis-head"><span>IBIS MOBILE</span><b>{state?.ibis.writable ? "CONTROLES REAIS" : "LEITURA"}</b></div>
        <div className="ibis-display"><label>LINHA<strong>{state?.ibis.line || "—"}</strong></label><label>ROTA / CURSO<strong>{state?.ibis.route || "—"}</strong></label><label>DESTINO<strong>{state?.ibis.destination || "—"}</strong></label><label>HOF<strong>{state?.ibis.hof || "—"}</strong></label><label>PRÓXIMA PARADA<strong>{state?.ibis.nextStop || "—"}</strong></label><label>ATRASO<strong>{state?.ibis.delaySeconds == null ? "—" : `${state.ibis.delaySeconds > 0 ? "+" : ""}${state.ibis.delaySeconds}s`}</strong></label></div>
        <div className="vehicle-controls-card">
          <div className="section-title"><div><small>TECLAS REAIS DO VEÍCULO</small><h2>IBIS / AFR / Matrix</h2></div><b className={state?.ibis.writable ? "good" : "bad"}>{state?.ibis.writable ? "LIBERADO" : "BLOQUEADO"}</b></div>
          <input className="control-search" value={ibisFilter} onChange={e => setIbisFilter(e.target.value)} placeholder="Buscar tecla/evento do IBIS…" />
          {availableIbisControls.length === 0
            ? <div className="map-empty compact-empty">Nenhum evento real de IBIS/AFR/Matrix foi identificado neste veículo.</div>
            : <div className="vehicle-control-grid">
                {availableIbisControls.map(eventName => <div className="vehicle-control-item" key={eventName}>
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
        </div>
        {!state?.ibis.writable && <div className="ibis-warning">
          {!state?.vehicleControls.enabled
            ? "Ative “Controles do ônibus pelo celular (EXPERIMENTAL)” no NavBR do PC para liberar as teclas reais do IBIS."
            : !state?.vehicleControls.capabilityAvailable
              ? "O Plugin Bridge ainda não informou a capacidade local-vehicle-trigger."
              : "Leitura real ativa, mas este ônibus não expôs eventos reconhecidos de IBIS/AFR/Matrix."}
        </div>}
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
