import React from "react";
import {
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
  const shortSummary = summary ? summary.slice(0, 150) + (summary.length > 150 ? "…" : "") : "Versão pública do NavBR.";

  return (
    <article className="release-card v2-release-card">
      <div className="release-meta">
        <span className="tag">{release.prerelease ? "ALPHA" : "RELEASE"}</span>
        <small>{formatDate(release.published_at)}</small>
      </div>

      <div className="v2-release-title">
        <small>{release.tag_name}</small>
        <h3>{release.name || release.tag_name}</h3>
      </div>

      <p>{shortSummary}</p>
      <div className="release-downloads">{formatNumber(releaseDownloadCount(release))} downloads</div>

      <div className="asset-list">
        {assets.length ? assets.map(asset => (
          <a key={asset.name} href={asset.browser_download_url} target="_blank" rel="noreferrer">
            <span>
              <b>{assetLabel(asset.name)}</b>
              <small>{formatBytes(asset.size)}</small>
            </span>
            <strong>Baixar</strong>
          </a>
        )) : (
          <a href={release.html_url} target="_blank" rel="noreferrer">
            <span><b>Abrir release</b><small>GitHub Releases</small></span>
            <strong>↗</strong>
          </a>
        )}
      </div>
    </article>
  );
}
