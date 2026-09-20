import React from "react";
import { CURRENT_TAG, RELEASES_PAGE, formatNumber } from "./lib.js";

export function Hero({ current, standalone }) {
  return (
    <section id="inicio" className="v2-hero shell">
      <div className="v2-hero-copy">
        <div className="v2-kicker"><span className="live-dot" /> OMSI NavBR Multiplayer</div>
        <h1>Multiplayer, navegação e ferramentas operacionais<span> para o OMSI 2.</span></h1>
        <p className="v2-hero-lead">
          Um projeto independente que conecta motoristas, mapa, voz, HUD, operação e recursos experimentais de RP em uma interface moderna integrada ao OMSI.
        </p>
        <div className="v2-hero-actions">
          <a className="button primary v2-primary-cta" href={standalone?.browser_download_url || RELEASES_PAGE} target="_blank" rel="noreferrer">
            {standalone ? "Baixar NavBR para Windows" : "Ver downloads"}
          </a>
          <a className="button secondary" href="#recursos">Conhecer o projeto</a>
        </div>
        <div className="v2-hero-note">
          <span>{current?.tag_name || CURRENT_TAG}</span><i /><span>Windows x86</span><i /><span>OMSI 2.3.004</span>
        </div>
      </div>

      <div className="v2-hero-stage">
        <div className="v2-product-frame">
          <div className="v2-product-bar">
            <div className="v2-window-dots"><i /><i /><i /></div>
            <span>NavBR • Multiplayer</span>
            <small>{current ? "Alpha pública" : "Catálogo atualizando"}</small>
          </div>
          <picture>
            <source srcSet="./assets/navbr-hero.svg" type="image/svg+xml" />
            <img src="./assets/navbr-hero.webp" width="1600" height="900" fetchPriority="high" decoding="async" alt="Visão conceitual da interface do OMSI NavBR Multiplayer" />
          </picture>
          <div className="v2-product-caption">
            <div><strong>Interface React + WebView2</strong><span>Estado real do backend C# e integração OMSI.</span></div>
            <span className="v2-pill">{current?.tag_name || CURRENT_TAG}</span>
          </div>
        </div>
        <div className="v2-floating-card v2-float-a"><span>Multiplayer</span><strong>SignalR + salas reais</strong></div>
        <div className="v2-floating-card v2-float-b"><span>Plugin</span><strong>Native AOT x86</strong></div>
      </div>
    </section>
  );
}

export function TrustStrip({ current, alphaDownloads, totalDownloads }) {
  return (
    <section className="v2-trust">
      <div className="shell v2-trust-grid">
        <div><strong>{current?.tag_name || CURRENT_TAG}</strong><span>alpha pública de teste</span></div>
        <div><strong>{formatNumber(totalDownloads)}</strong><span>downloads acumulados</span></div>
        <div><strong>MIT</strong><span>código aberto</span></div>
        <div><strong>5 idiomas</strong><span>interface multilíngue</span></div>
        <div><strong>Projeto independente</strong><span>feito pela comunidade</span></div>
      </div>
    </section>
  );
}

export function StatusSection({ current }) {
  const items = [
    ["Interface", "React + WebView2", "Disponível", "ready"],
    ["Multiplayer", "LAN + online", "Em validação real", "testing"],
    ["Plugin OMSI", "Native AOT x86 + Bridge", "Experimental", "experimental"],
    ["Ônibus físico / RP", "integração nativa", "Em validação", "testing"]
  ];

  return (
    <section id="estado" className="section shell v2-status-section">
      <div className="v2-section-heading">
        <div><span className="eyebrow">Estado do projeto</span><h2>O que está disponível hoje.</h2></div>
        <p>O NavBR continua em Alpha. A Alpha.18 está pública para testes, mas multiplayer LAN/local e online ainda não foram validados ponta a ponta entre dois PCs/duas sessões reais do OMSI.</p>
      </div>
      <div className="v2-status-grid">
        {items.map(([label, value, state, kind]) => (
          <article key={label}>
            <div className="v2-status-top"><span>{label}</span><em className={kind}>{state}</em></div>
            <strong>{value}</strong>
          </article>
        ))}
      </div>
      <div className="v2-status-foot"><span>Release em destaque</span><strong>{current?.tag_name || CURRENT_TAG}</strong></div>
    </section>
  );
}
