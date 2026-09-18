import React from "react";
import {
  CURRENT_TAG,
  RELEASES_PAGE,
  alphaLabel,
  formatNumber
} from "./lib.js";

export function Hero({ current, standalone }) {
  return (
    <section id="inicio" className="hero shell hero-project">
      <div className="hero-copy">
        <div className="hero-kicker">
          <span className="live-dot" />
          {current ? "Alpha.14 • release pública atual • React/WebView2" : "Alpha.14 • catálogo atualizando • React/WebView2"}
        </div>
        <h1>NavBR Alpha.14. <span>Interface React, multiplayer público e integração OMSI v3.</span></h1>
        <p className="hero-lead">
          Home, GPS/roadmap real 2D/3D, Central Multiplayer, voz, CCO, Ghost/Replay,
          Hardware Cockpit, Instalações OMSI, HUD, Roadmap Studio, diagnóstico de rede e Personagem/RP.
        </p>
        <div className="actions">
          <a className="button primary" href={standalone?.browser_download_url || RELEASES_PAGE} target="_blank" rel="noreferrer">
            {standalone ? "Baixar Alpha.14 — EXE standalone" : "Ver releases"}
          </a>
          <a className="button secondary" href="#multiplayer">Como testar em 2 PCs</a>
        </div>
        <div className="hero-meta">
          <span>OMSI 2.3.004</span><span>Cliente x86</span><span>Peer-host TCP 27730</span>
          <span>Plugin Bridge v3</span><span>React/WebView2</span><span>5 idiomas</span>
        </div>
      </div>

      <div className="hero-media">
        <figure className="hero-visual">
          <picture>
            <source srcSet="./assets/navbr-hero.svg" type="image/svg+xml" />
            <img src="./assets/navbr-hero.webp" width="1600" height="900" fetchPriority="high" decoding="async" alt="OMSI NavBR Multiplayer com GPS, HUD, CCO e multiplayer" />
          </picture>
          <figcaption>Sem dados fake em produção: quando o backend não tem valor real, a interface informa indisponibilidade.</figcaption>
        </figure>
        <div className="project-console" aria-label="Estado atual do projeto">
          <div className="console-head"><span>NAVBR • ALPHA.14</span><span className="console-live"><i /> {current ? "RELEASE PÚBLICA" : "ATUALIZANDO"}</span></div>
          <div className="console-body">
            <div className="console-row ok"><span>Shell React/WebView2</span><strong>ATIVO</strong></div>
            <div className="console-row ok"><span>Plugin Bridge</span><strong>V3</strong></div>
            <div className="console-row ok"><span>Multiplayer/SignalR</span><strong>REAL</strong></div>
            <div className="console-row"><span>RP físico avançado</span><strong>EXPERIMENTAL</strong></div>
          </div>
        </div>
      </div>
    </section>
  );
}

export function TrustStrip({ current, alphaDownloads, totalDownloads }) {
  return (
    <section className="trust-strip">
      <div className="shell trust-grid">
        <div><b>{CURRENT_TAG}</b><span>{current ? "release pública atual" : "aguardando catálogo"}</span></div>
        <div><b>{formatNumber(alphaDownloads)}</b><span>downloads da {alphaLabel(CURRENT_TAG)}</span></div>
        <div><b>{formatNumber(totalDownloads)}</b><span>downloads acumulados</span></div>
        <div><b>win-x86</b><span>cliente OMSI compatível</span></div>
        <div><b>MIT</b><span>projeto independente</span></div>
      </div>
    </section>
  );
}

export function StatusSection({ current }) {
  return (
    <section id="estado" className="section shell">
      <span className="eyebrow">Estado atual</span>
      <h2>Alpha.14 com shell React e serviços nativos integrados.</h2>
      <p className="section-lead">
        O WPF permanece como host técnico onde ainda é necessário, mas a navegação e os módulos de usuário estão consolidados no React.
      </p>
      <div className="status-grid site-status-grid">
        <article className="status-card"><span>INTERFACE</span><strong>React/WebView2</strong><small>WPF antigo fora da navegação normal</small></article>
        <article className="status-card"><span>MULTIPLAYER</span><strong>SignalR real</strong><small>salas, presença, chat, voz e telemetria</small></article>
        <article className="status-card"><span>PLUGIN</span><strong>Native AOT x86</strong><small>Bridge v3 + interop v3</small></article>
        <article className="status-card"><span>RELEASE</span><strong>{current ? "Alpha.14 pública" : "Atualizando"}</strong><small>recompilada antes da publicação</small></article>
      </div>
    </section>
  );
}
