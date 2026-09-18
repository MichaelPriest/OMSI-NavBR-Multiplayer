import React from "react";
import {
  alphaLabel,
  alphaNumber,
  assetHelp,
  assetLabel,
  features,
  formatBytes,
  formatNumber
} from "./lib.js";

export function AlphaDownloads({ currentAlphaKey, alphaDownloads, loading }) {
  const entries = Object.entries(alphaDownloads || {})
    .filter(([key]) => alphaNumber(key) >= 0)
    .sort((a, b) => alphaNumber(b[0]) - alphaNumber(a[0]));

  return (
    <section id="downloads-por-alpha" className="section shell">
      <span className="eyebrow">Histórico</span>
      <h2>Downloads separados por Alpha.</h2>
      <div className="alpha-download-grid">
        {entries.length ? entries.map(([key, count]) => (
          <article key={key} className={`alpha-download-card${key === currentAlphaKey ? " current" : ""}`}>
            <span>{alphaLabel(key)}</span>
            <strong>{formatNumber(count)}</strong>
            <small>downloads acumulados</small>
          </article>
        )) : (
          <p className="section-lead">{loading ? "Carregando catálogo…" : "As contagens aparecerão após a atualização do catálogo."}</p>
        )}
      </div>
    </section>
  );
}

export function Downloads({ currentAssets, loading, error, releasesPage }) {
  return (
    <section id="download" className="section shell">
      <span className="eyebrow">Builds</span>
      <h2>Baixe a Alpha.14 Test 3.</h2>
      <p className="section-lead">
        Use o EXE standalone para o teste normal. Plugin, servidor dedicado e simulador ficam disponíveis separadamente.
      </p>
      <div className="release-grid">
        {currentAssets.length ? currentAssets.map(asset => (
          <article className="release-card" key={asset.name}>
            <span className="tag">{assetLabel(asset.name)}</span>
            <h3>{asset.name}</h3>
            <p>{assetHelp(asset.name)}</p>
            <div className="release-downloads">
              {formatBytes(asset.size)} • {formatNumber(asset.download_count)} downloads
            </div>
            <a className="button primary" href={asset.browser_download_url} target="_blank" rel="noreferrer">Baixar</a>
          </article>
        )) : (
          <article className="release-card">
            <h3>{loading ? "Carregando builds…" : "Build não encontrada no catálogo"}</h3>
            <p>{error ? "O catálogo não respondeu. Abra o GitHub para baixar manualmente." : "Aguarde a atualização do catálogo."}</p>
            <a className="button secondary" href={releasesPage} target="_blank" rel="noreferrer">Abrir releases</a>
          </article>
        )}
      </div>
    </section>
  );
}

export function Features() {
  return (
    <section id="recursos" className="section shell">
      <span className="eyebrow">Recursos</span>
      <h2>O que entra na Alpha.14.</h2>
      <div className="feature-grid">
        {features.map(([title, text], index) => (
          <article key={title}>
            <div className="feature-icon">{String(index + 1).padStart(2, "0")}</div>
            <h3>{title}</h3>
            <p>{text}</p>
          </article>
        ))}
      </div>
    </section>
  );
}
