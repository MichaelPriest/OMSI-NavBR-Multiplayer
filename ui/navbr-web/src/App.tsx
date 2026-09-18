import { FormEvent, useEffect, useMemo, useState } from "react";
import {
  type NavBrMultiplayerState,
  type NavBrState,
  sendCommand,
  subscribeToNavBrState
} from "./navbrBridge";

type Screen = "home" | "multiplayer";
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
  inviteAddresses: [],
  latencyMs: null,
  voiceEnabled: false,
  voiceChannel: "general",
  roleplayEnabled: false,
  localRoleplayActive: false,
  selectedRoleplayCharacter: null,
  playerCount: 0,
  players: [],
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
        <button className="nav-item" disabled><b>⌖</b><span>Navegação</span></button>
        <button className={`nav-item ${screen === "multiplayer" ? "active" : ""}`} onClick={() => setScreen("multiplayer")}>
          <b>◉</b><span>Multiplayer</span>
        </button>
        <button className="nav-item" onClick={() => { setScreen("multiplayer"); sendCommand("openRoleplay"); }}>
          <b>♙</b><span>Personagem / RP</span>
        </button>
        <button className="nav-item" disabled><b>▣</b><span>CCO</span></button>
        <button className="nav-item" disabled><b>⚙</b><span>Configurações</span></button>
      </nav>
      <div className="sidebar-footer">
        <i />
        <div><strong>Alpha.14</strong><small>React UI preview</small></div>
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

function Multiplayer({
  state,
  error
}: {
  state: NavBrState | null;
  error: string | null;
}) {
  const multiplayer = state?.multiplayer ?? fallbackMultiplayer;
  const telemetry = state?.telemetry;
  const [tab, setTab] = useState<MultiplayerTab>("overview");
  const [chatText, setChatText] = useState("");

  const statusLabel = multiplayer.connected
    ? "Conectado"
    : multiplayer.available
      ? multiplayer.connectionState
      : "Controlador inativo";

  const visiblePlayers = useMemo(
    () => multiplayer.players.slice().sort((a, b) => a.displayName.localeCompare(b.displayName)),
    [multiplayer.players]
  );

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
          <button className="button primary" onClick={() => sendCommand("openMultiplayerCentral")}>
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
            <div className="session-map-placeholder">
              <div className="map-grid-lines" />
              <div className="map-center-message">
                <strong>{telemetry?.mapName || "Sem mapa ativo"}</strong>
                <span>
                  {multiplayer.connected
                    ? `${multiplayer.playerCount} jogador(es) na sessão. O mapa web completo entra no próximo bloco da migração.`
                    : "Conecte a uma sala para acompanhar a operação compartilhada."}
                </span>
              </div>
            </div>
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
              <button className="text-action" onClick={() => sendCommand("openRoleplay")}>Abrir Personagem / RP →</button>
            </article>
          </aside>
        </section>
      )}

      {tab === "room" && (
        <section className="card mp-panel">
          <div className="section-heading">
            <div><span className="eyebrow">SALA</span><h3>Conexão e host</h3></div>
            <button className="button primary" onClick={() => sendCommand("openMultiplayerCentral")}>Gerenciar sala</button>
          </div>
          <div className="details-grid">
            <div><small>SERVIDOR</small><strong>{multiplayer.serverUrl || "—"}</strong></div>
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
          <p className="migration-note">
            Criar/entrar em sala ainda usa o formulário nativo nesta fase para preservar senha privada, firewall, UPnP e validações já testadas.
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
            <p>Canal atual: <strong>{multiplayer.voiceChannel || "general"}</strong></p>
            <p>Os controles de dispositivo, proximidade e push-to-talk permanecem no controlador nativo durante esta etapa.</p>
            <button className="button ghost" onClick={() => sendCommand("openMultiplayerCentral")}>Configurar voz</button>
          </aside>
        </section>
      )}

      {tab === "roleplay" && (
        <section className="card mp-panel rp-panel">
          <span className="eyebrow">PERSONAGEM / RP</span>
          <h3>{multiplayer.localRoleplayActive ? "Personagem ativo fora do ônibus" : "Motorista vinculado ao ônibus"}</h3>
          <p>
            {multiplayer.selectedRoleplayCharacter
              ? `Personagem selecionado: ${multiplayer.selectedRoleplayCharacter}.`
              : "Nenhum personagem selecionado para o mapa atual."}
          </p>
          <div className="action-row">
            <button className="button primary" onClick={() => sendCommand("openRoleplay")}>Abrir controles de Personagem / RP</button>
          </div>
        </section>
      )}

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
            <button className="button ghost" onClick={() => sendCommand("openHudEditor")}>Configurar HUD</button>
          </article>
          <article className="card compact-card">
            <span className="eyebrow">REDE</span>
            <h3>Host local</h3>
            <p>{multiplayer.hostRunning ? `Escutando na porta TCP ${multiplayer.hostPort ?? 27730}.` : "Host local não está ativo."}</p>
            <button className="button ghost" onClick={() => sendCommand("openMultiplayerCentral")}>Firewall / NAT / UPnP</button>
          </article>
        </section>
      )}
    </>
  );
}

export default function App() {
  const [state, setState] = useState<NavBrState | null>(null);
  const [screen, setScreen] = useState<Screen>("home");
  const [commandError, setCommandError] = useState<string | null>(null);

  useEffect(() => subscribeToNavBrState(
    next => {
      setState(next);
      setCommandError(null);
    },
    setCommandError
  ), []);

  return (
    <div className="app-shell">
      <Sidebar screen={screen} setScreen={setScreen} />
      <main>
        {screen === "home"
          ? <Home state={state} />
          : <Multiplayer state={state} error={commandError} />}
      </main>
    </div>
  );
}
