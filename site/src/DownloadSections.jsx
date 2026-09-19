import React from "react";
import ReleaseCard from "./ReleaseCard.jsx";
import {
  alphaKey,
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

export function Downloads({ currentAssets, releases, loading, error, releasesPage }) {
  const groups = (releases || []).reduce((result, release) => {
    const key = alphaKey(release?.tag_name) || "outros";
    if (!result[key]) result[key] = [];
    result[key].push(release);
    return result;
  }, {});

  const groupedEntries = Object.entries(groups).sort((a, b) => {
    if (a[0] === "outros") return 1;
    if (b[0] === "outros") return -1;
    return alphaNumber(b[0]) - alphaNumber(a[0]);
  });

  return (
    <section id="download" className="section shell">
      <span className="eyebrow">Downloads</span>
      <h2>Todas as versões publicadas do NavBR.</h2>
      <p className="section-lead">
        A versão atual aparece primeiro. Abaixo, o histórico completo mantém cada Alpha e versão de teste disponível com os arquivos publicados originalmente no GitHub Releases.
      </p>

      <div className="download-current">
        <div className="download-current-head">
          <div>
            <span className="tag">VERSÃO ATUAL</span>
            <h3>Alpha.14 pública</h3>
          </div>
          <a className="button secondary" href={releasesPage} target="_blank" rel="noreferrer">Ver no GitHub</a>
        </div>
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
      </div>

      <div className="version-archive" aria-label="Histórico completo de downloads">
        <div className="version-archive-head">
          <div>
            <span className="eyebrow">Arquivo de versões</span>
            <h3>{releases?.length || 0} versões públicas disponíveis</h3>
          </div>
          <p>Inclui Alphas anteriores e builds de teste que continuam publicadas.</p>
        </div>

        {groupedEntries.length ? groupedEntries.map(([key, items]) => (
          <section className="version-group" key={key}>
            <div className="version-group-head">
              <h3>{key === "outros" ? "Outras versões" : alphaLabel(key)}</h3>
              <span>{items.length} {items.length === 1 ? "versão" : "versões"}</span>
            </div>
            <div className="release-grid version-release-grid">
              {items.map(release => <ReleaseCard key={release.tag_name} release={release} />)}
            </div>
          </section>
        )) : (
          <article className="release-card">
            <h3>{loading ? "Carregando histórico…" : "Histórico indisponível"}</h3>
            <p>{error || "O catálogo ainda não possui versões publicadas."}</p>
            <a className="button secondary" href={releasesPage} target="_blank" rel="noreferrer">Abrir GitHub Releases</a>
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
