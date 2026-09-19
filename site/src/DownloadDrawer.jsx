import React from "react";
import { assetHelp, assetLabel, formatBytes, formatNumber } from "./lib.js";

export default function DownloadDrawer({ open, assets, currentTag, loading, error, releasesPage, onClose }) {
  if (!open) return null;

  return (
    <div className="download-drawer-backdrop" role="presentation" onMouseDown={onClose}>
      <section
        className="download-drawer"
        role="dialog"
        aria-modal="true"
        aria-labelledby="download-drawer-title"
        onMouseDown={event => event.stopPropagation()}
      >
        <div className="download-drawer-head">
          <div>
            <span className="eyebrow">Downloads</span>
            <h2 id="download-drawer-title">{currentTag || "NavBR"}</h2>
            <p>Escolha o pacote da versão atual. Para jogar e testar normalmente, use o EXE standalone.</p>
          </div>
          <button className="download-drawer-close" type="button" onClick={onClose} aria-label="Fechar downloads">×</button>
        </div>

        <div className="download-drawer-grid">
          {assets.length ? assets.map((asset, index) => (
            <article className={`download-drawer-card${index === 0 ? " recommended" : ""}`} key={asset.name}>
              <span>{index === 0 ? "RECOMENDADO" : assetLabel(asset.name)}</span>
              <h3>{assetLabel(asset.name)}</h3>
              <p>{assetHelp(asset.name)}</p>
              <small>{formatBytes(asset.size)} • {formatNumber(asset.download_count)} downloads</small>
              <a className="button primary" href={asset.browser_download_url} target="_blank" rel="noreferrer">Baixar</a>
            </article>
          )) : (
            <article className="download-drawer-card">
              <h3>{loading ? "Carregando builds…" : "Builds indisponíveis"}</h3>
              <p>{error ? "O catálogo não respondeu. Abra as releases no GitHub." : "Aguarde a atualização do catálogo."}</p>
              <a className="button secondary" href={releasesPage} target="_blank" rel="noreferrer">Abrir releases</a>
            </article>
          )}
        </div>

        <div className="download-drawer-foot">
          <a href="#download" onClick={onClose}>Ver detalhes, servidor, plugin e histórico ↓</a>
        </div>
      </section>
    </div>
  );
}
