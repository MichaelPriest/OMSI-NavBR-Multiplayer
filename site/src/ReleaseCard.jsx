import React from "react";
import {
  assetHelp,
  assetLabel,
  formatBytes,
  formatDate,
  formatNumber,
  releaseDownloadCount,
  summarizeBody
} from "./lib.js";

export default function ReleaseCard({ release }) {
  const assets = (release.assets || []).filter(asset => /\.(exe|zip)$/i.test(asset.name || ""));
  const summary = summarizeBody(release.body);

  return (
    <article className="release-card">
      <div className="release-meta">
        <span className="tag">{release.prerelease ? "PRÉ-RELEASE" : "RELEASE"}</span>
        <small>{formatDate(release.published_at)}</small>
      </div>
      <h3>{release.name || release.tag_name}</h3>
      <div className="release-downloads">
        ↓ {formatNumber(releaseDownloadCount(release))} downloads desta versão
      </div>
      <p>{summary ? `${summary.slice(0, 280)}${summary.length > 280 ? "…" : ""}` : "Versão publicada para testes do projeto."}</p>
      <div className="asset-list">
        {assets.length ? assets.map(asset => (
          <a key={asset.name} href={asset.browser_download_url} target="_blank" rel="noreferrer">
            <span>
              <b>{assetLabel(asset.name)}</b>
              <small>{asset.name}</small>
              {assetHelp(asset.name) && <small>{assetHelp(asset.name)}</small>}
            </span>
            <small>
              {[formatBytes(asset.size), `${formatNumber(asset.download_count)} downloads`]
                .filter(Boolean)
                .join(" • ")}
            </small>
          </a>
        )) : (
          <a href={release.html_url} target="_blank" rel="noreferrer">
            <span>Abrir release no GitHub</span><small>→</small>
          </a>
        )}
      </div>
    </article>
  );
}
