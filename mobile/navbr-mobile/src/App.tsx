import { FormEvent, useEffect, useMemo, useState } from "react";

type Point = { x: number; y: number };
type MobileState = {
  schema: string;
  version: number;
  generatedAtUtc: string;
  omsi: { detected: boolean; inGame: boolean; mapName?: string | null };
  plugin: { connected: boolean; version?: string | null };
  vehicle?: {
    mapName?: string | null; vehicleName?: string | null; line?: string | null; route?: string | null;
    destinationName?: string | null; nextStopName?: string | null; hofName?: string | null;
    speedKph: number; headingDegrees: number; delaySeconds?: number | null; currentStreetName?: string | null;
    fuelPercent?: number | null; isInGame: boolean;
  } | null;
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
  ibis: {
    available: boolean; writable: boolean; writeReason?: string | null; line?: string | null; route?: string | null;
    destination?: string | null; hof?: string | null; nextStop?: string | null; delaySeconds?: number | null;
  };
};

type Tab = "gps" | "ibis" | "status";
const fmtDistance = (v?: number | null) => v == null ? "—" : v >= 1000 ? `${(v/1000).toFixed(1)} km` : `${Math.round(v)} m`;
const fmtEta = (v?: number | null) => v == null ? "—" : `${Math.max(0,Math.round(v/60))} min`;

function RouteMap({ state }: { state: MobileState["navigation"] }) {
  const points = state.routePoints || [];
  const rejoin = state.rejoinPoints || [];
  const vehicle = state.vehicle;
  const bounds = useMemo(() => {
    const all = [...points, ...rejoin, ...(vehicle ? [vehicle] : [])];
    if (!all.length) return null;
    const xs = all.map(p => p.x), ys = all.map(p => p.y);
    const minX0 = Math.min(...xs), maxX0 = Math.max(...xs), minY0 = Math.min(...ys), maxY0 = Math.max(...ys);
    const px = Math.max(20,(maxX0-minX0)*.08), py = Math.max(20,(maxY0-minY0)*.08);
    return { minX:minX0-px,minY:minY0-py,width:Math.max(1,maxX0-minX0+px*2),height:Math.max(1,maxY0-minY0+py*2) };
  }, [points,rejoin,vehicle?.x,vehicle?.y]);

  if (!bounds || !vehicle) return <div className="map-empty">Aguardando rota e posição reais do OMSI…</div>;
  const mp = (p:Point) => `${p.x},${bounds.minY+bounds.height-(p.y-bounds.minY)}`;
  const vy = bounds.minY+bounds.height-(vehicle.y-bounds.minY);
  return <svg className="route-map" viewBox={`${bounds.minX} ${bounds.minY} ${bounds.width} ${bounds.height}`} preserveAspectRatio="xMidYMid meet">
    <polyline className="route-line" points={points.map(mp).join(" ")} />
    {rejoin.length > 1 && <polyline className="rejoin-line" points={rejoin.map(mp).join(" ")} />}
    <g transform={`translate(${vehicle.x} ${vy}) rotate(${vehicle.headingDegrees||0})`}>
      <circle r="13" className="bus-ring" />
      <path d="M0 -13 L8 9 L0 5 L-8 9 Z" className="bus-arrow" />
    </g>
  </svg>;
}

export default function App() {
  const [pairing,setPairing] = useState(() => localStorage.getItem("navbr-mobile-pairing") || "");
  const [draft,setDraft] = useState(pairing);
  const [state,setState] = useState<MobileState|null>(null);
  const [tab,setTab] = useState<Tab>("gps");
  const [error,setError] = useState<string|null>(null);

  useEffect(() => {
    if (!pairing) return;
    let active = true;
    const read = async () => {
      try {
        const response = await fetch("./api/mobile/state",{headers:{"X-NavBR-Mobile-Code":pairing},cache:"no-store"});
        if (response.status === 401) throw new Error("Código de pareamento inválido.");
        if (!response.ok) throw new Error(`NavBR respondeu HTTP ${response.status}.`);
        const next = await response.json() as MobileState;
        if (active) { setState(next); setError(null); }
      } catch (e) {
        if (active) setError(e instanceof Error ? e.message : "Falha ao acessar o NavBR no PC.");
      }
    };
    void read();
    const timer = window.setInterval(read,700);
    return () => { active=false; window.clearInterval(timer); };
  },[pairing]);

  const pair = (e:FormEvent) => {
    e.preventDefault();
    const value=draft.trim().toUpperCase();
    localStorage.setItem("navbr-mobile-pairing",value);
    setPairing(value);
  };

  if (!pairing || (!state && error?.includes("pareamento"))) {
    return <main className="pair-shell">
      <div className="brand-mark">N</div>
      <span className="eyebrow">NAVBR MOBILE COMPANION</span>
      <h1>Parear com o PC</h1>
      <p>Digite o código mostrado no card <strong>Mobile Companion</strong> das Configurações do NavBR.</p>
      <form onSubmit={pair}><input autoFocus value={draft} onChange={e=>setDraft(e.target.value)} placeholder="A1B2C3D4" maxLength={8}/><button>Conectar</button></form>
      {error && <div className="error">{error}</div>}
    </main>;
  }

  const nav=state?.navigation, vehicle=state?.vehicle;
  return <main className="app-shell">
    <header className="mobile-header">
      <div><span className="eyebrow">NAVBR MOBILE</span><strong>{vehicle?.line||"—"} <i>·</i> {vehicle?.route||"—"}</strong></div>
      <span className={`live-dot ${state?.omsi.inGame?"online":""}`}>{state?.omsi.inGame?"OMSI":"AGUARDANDO"}</span>
    </header>
    {error && <div className="error compact">{error}</div>}
    <section className="content">
      {tab==="gps" && <div>
        <div className="hero-card"><div><small>PRÓXIMA PARADA</small><h2>{nav?.nextStopName||vehicle?.nextStopName||"Aguardando rota"}</h2><span>{nav?.currentStreetName||vehicle?.currentStreetName||state?.omsi.mapName||"—"}</span></div><div className="speed"><strong>{Math.round(vehicle?.speedKph||0)}</strong><span>km/h</span></div></div>
        <div className="maneuver-card"><div className="maneuver-icon">↑</div><div><small>{nav?.maneuver||"SEM MANOBRA"}</small><strong>{fmtDistance(nav?.distanceToManeuverMeters)}</strong></div><div className={nav?.isOnRoute?"route-ok":"route-off"}>{nav?.isOnRoute?"NA ROTA":`FORA ${fmtDistance(nav?.offRouteDistanceMeters)}`}</div></div>
        <div className="map-card"><RouteMap state={nav || {available:false,isOnRoute:false,offRouteDistanceMeters:0,routeProgressPercent:0,distanceRemainingMeters:0,maneuver:"None",routePoints:[],rejoinPoints:[]}} /></div>
        <div className="metric-grid"><div><small>PRÓXIMA</small><strong>{fmtDistance(nav?.distanceToNextStopMeters)}</strong></div><div><small>ETA</small><strong>{fmtEta(nav?.etaToNextStopSeconds)}</strong></div><div><small>RESTANTE</small><strong>{fmtDistance(nav?.distanceRemainingMeters)}</strong></div><div><small>PROGRESSO</small><strong>{Math.round(nav?.routeProgressPercent||0)}%</strong></div></div>
        {!!nav?.stopSequence?.upcomingStops?.length && <div className="stops-card"><small>PRÓXIMAS PARADAS</small>{nav.stopSequence.upcomingStops.map((s,i)=><div key={s+i}><b>{i+1}</b><span>{s}</span></div>)}</div>}
      </div>}
      {tab==="ibis" && <div><div className="ibis-head"><span>IBIS MOBILE</span><b>{state?.ibis.writable?"OPERACIONAL":"LEITURA"}</b></div><div className="ibis-display"><label>LINHA<strong>{state?.ibis.line||"—"}</strong></label><label>ROTA / CURSO<strong>{state?.ibis.route||"—"}</strong></label><label>DESTINO<strong>{state?.ibis.destination||"—"}</strong></label><label>HOF<strong>{state?.ibis.hof||"—"}</strong></label><label>PRÓXIMA PARADA<strong>{state?.ibis.nextStop||"—"}</strong></label><label>ATRASO<strong>{state?.ibis.delaySeconds==null?"—":`${state.ibis.delaySeconds>0?"+":""}${state.ibis.delaySeconds}s`}</strong></label></div>{!state?.ibis.writable&&<div className="ibis-warning">Dados reais do OMSI. Escrita bloqueada até existir capacidade IBIS nativa segura no Plugin Bridge.</div>}</div>}
      {tab==="status" && <div className="status-page"><StatusRow label="OMSI detectado" value={state?.omsi.detected?"SIM":"NÃO"} ok={!!state?.omsi.detected}/><StatusRow label="Dentro do mapa" value={state?.omsi.inGame?"SIM":"NÃO"} ok={!!state?.omsi.inGame}/><StatusRow label="Plugin Bridge" value={state?.plugin.connected?"CONECTADO":"DESCONECTADO"} ok={!!state?.plugin.connected}/><StatusRow label="Plugin" value={state?.plugin.version||"—"}/><StatusRow label="Mapa" value={state?.omsi.mapName||"—"}/><StatusRow label="Ônibus" value={vehicle?.vehicleName||"—"}/><StatusRow label="HOF" value={vehicle?.hofName||"—"}/><button className="forget" onClick={()=>{localStorage.removeItem("navbr-mobile-pairing");setPairing("");setState(null)}}>Desparear este celular</button></div>}
    </section>
    <nav className="bottom-nav"><button className={tab==="gps"?"active":""} onClick={()=>setTab("gps")}><span>⌖</span>GPS</button><button className={tab==="ibis"?"active":""} onClick={()=>setTab("ibis")}><span>▣</span>IBIS</button><button className={tab==="status"?"active":""} onClick={()=>setTab("status")}><span>●</span>Status</button></nav>
  </main>;
}
function StatusRow({label,value,ok}:{label:string;value:string;ok?:boolean}){return <div className="status-row"><span>{label}</span><strong className={ok===undefined?"":ok?"good":"bad"}>{value}</strong></div>}
