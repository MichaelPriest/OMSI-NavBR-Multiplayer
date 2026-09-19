import React, { useMemo, useState } from "react";
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
      <span className="eyebrow">Histórico por geração</span>
      <h2>Downloads acumulados por Alpha.</h2>
      <p className="section-lead">
        Um resumo rápido de quais gerações do NavBR tiveram builds públicas.
      </p>
      <div className="alpha-download-grid">
        {entries.length ? entries.map(([key, count]) => (
          <a
            href="#todas-versoes"
            key={key}
            className={`alpha-download-card${key === currentAlphaKey ? " current" : ""}`}
          >
            <span>{alphaLabel(key)}</span>
            <strong>{formatNumber(count)}</strong>
            <small>downloads acumulados</small>
          </a>
        )) : (
          <p className="section-lead">{loading ? "Carregando catálogo…" : "As contagens aparecerão após a atualização do catálogo."}</p>
        )}
      </div>
    </section>
  );
}

export function Downloads({ currentAssets, current, loading, error, releasesPage }) {
  return (
    <section id="download" className="section shell downloads-current-section">
      <div className="section-title-row">
        <div>
          <span className="eyebrow">Download atual</span>
          <h2>Baixe a versão pública mais recente.</h2>
        </div>
        <a className="button secondary" href="#todas-versoes">Ver todas as versões</a>
      </div>
      <p className="section-lead">
        Para jogar e testar normalmente, use o EXE standalone. Os demais pacotes são alternativas ou ferramentas específicas.
      </p>

      <div className="current-release-banner">
        <div>
          <span className="tag">VERSÃO ATUAL</span>
          <strong>{current?.tag_name || "Alpha.14"}</strong>
          <small>{current?.name || "Catálogo da versão atual"}</small>
        </div>
        <a href={current?.html_url || releasesPage} target="_blank" rel="noreferrer">Abrir release no GitHub →</a>
      </div>

      <div className="release-grid current-download-grid">
        {currentAssets.length ? currentAssets.map((asset, index) => (
          <article className={`release-card current-asset-card${index === 0 ? " recommended" : ""}`} key={asset.name}>
            <div className="release-meta">
              <span className="tag">{index === 0 ? "RECOMENDADO" : assetLabel(asset.name)}</span>
              <small>{formatBytes(asset.size)}</small>
            </div>
            <h3>{assetLabel(asset.name)}</h3>
            <p>{assetHelp(asset.name)}</p>
            <small className="asset-filename">{asset.name}</small>
            <div className="release-downloads">{formatNumber(asset.download_count)} downloads</div>
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

export function AllVersions({ releases, loading, error, releasesPage }) {
  const [query, setQuery] = useState("");
  const [alphaFilter, setAlphaFilter] = useState("all");

  const alphaOptions = useMemo(
    () => Array.from(new Set((releases || []).map(release => alphaKey(release?.tag_name)).filter(Boolean)))
      .sort((a, b) => alphaNumber(b) - alphaNumber(a)),
    [releases]
  );

  const filteredReleases = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();
    return (releases || []).filter(release => {
      const key = alphaKey(release?.tag_name);
      if (alphaFilter !== "all" && key !== alphaFilter) return false;
      if (!normalizedQuery) return true;
      const haystack = [release?.tag_name, release?.name]
        .filter(Boolean)
        .join(" ")
        .toLowerCase();
      return haystack.includes(normalizedQuery);
    });
  }, [releases, query, alphaFilter]);

  const groups = filteredReleases.reduce((result, release) => {
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
    <section id="todas-versoes" className="section shell all-versions-section">
      <div className="v2-section-heading">
        <div>
          <span className="eyebrow">Arquivo de versões</span>
          <h2>Todas as versões públicas, em um só lugar.</h2>
        </div>
        <p>
          Pesquise por versão ou filtre por Alpha. Os downloads continuam apontando para os arquivos originais do GitHub Releases.
        </p>
      </div>

      <div className="v2-release-toolbar">
        <label>
          <span>Buscar versão</span>
          <input
            type="search"
            value={query}
            onChange={event => setQuery(event.target.value)}
            placeholder="Ex.: alpha.14-test.4"
          />
        </label>
        <label>
          <span>Filtrar por Alpha</span>
          <select value={alphaFilter} onChange={event => setAlphaFilter(event.target.value)}>
            <option value="all">Todas as Alphas</option>
            {alphaOptions.map(key => <option key={key} value={key}>{alphaLabel(key)}</option>)}
          </select>
        </label>
        <div className="v2-release-count">
          <strong>{filteredReleases.length}</strong>
          <span>de {releases?.length || 0} versões</span>
        </div>
      </div>

      <div className="version-archive" aria-label="Todas as versões públicas do NavBR">
        {groupedEntries.length ? groupedEntries.map(([key, items]) => (
          <section className="version-group" key={key}>
            <div className="version-group-head">
              <div><span className="eyebrow">Versões</span><h3>{key === "outros" ? "Outras versões" : alphaLabel(key)}</h3></div>
              <span>{items.length} {items.length === 1 ? "resultado" : "resultados"}</span>
            </div>
            <div className="release-grid version-release-grid">
              {items.map(release => <ReleaseCard key={release.tag_name} release={release} />)}
            </div>
          </section>
        )) : (
          <article className="v2-empty-state">
            <strong>{loading ? "Carregando versões…" : "Nenhuma versão encontrada"}</strong>
            <p>{error || "Altere os filtros ou abra o histórico completo no GitHub."}</p>
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
