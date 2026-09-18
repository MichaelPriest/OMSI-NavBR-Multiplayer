import { useEffect, useState } from "react";
import {
  type NavBrState,
  sendCommand,
  subscribeToNavBrState
} from "./navbrBridge";

const format = (value: number | undefined, digits = 1) =>
  typeof value === "number" && Number.isFinite(value) ? value.toFixed(digits) : "—";

export default function App() {
  const [state, setState] = useState<NavBrState | null>(null);

  useEffect(() => subscribeToNavBrState(setState), []);

  const omsi = state?.omsi;
  const telemetry = state?.telemetry;
  const active = Boolean(omsi?.running && telemetry?.inGame);

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">N</div>
          <div><strong>NavBR</strong><span>OMSI Multiplayer</span></div>
        </div>
        <nav className="nav">
          <button className="nav-item active">⌂ <span>Início</span></button>
          <button className="nav-item" disabled>⌖ <span>Navegação</span></button>
          <button className="nav-item" disabled>◉ <span>Multiplayer</span></button>
          <button className="nav-item" disabled>♙ <span>Personagem / RP</span></button>
          <button className="nav-item" disabled>▣ <span>CCO</span></button>
          <button className="nav-item" disabled>⚙ <span>Configurações</span></button>
        </nav>
        <div className="sidebar-footer">
          <i />
          <div><strong>Alpha.14</strong><small>React UI preview</small></div>
        </div>
      </aside>

      <main>
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
            <span className="card-label">TELEMETRIA</span>
            <strong>{telemetry ? telemetry.inGame ? "Conectada" : "Sem viagem ativa" : "Aguardando"}</strong>
            <small>{telemetry ? `Direção ${format(telemetry.headingDegrees)}°` : "Direção —"}</small>
          </article>
        </section>
      </main>
    </div>
  );
}
