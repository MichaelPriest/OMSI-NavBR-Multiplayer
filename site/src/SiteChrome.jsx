import React, { useEffect, useState } from "react";
import { GITHUB_URL } from "./lib.js";
import { useScrollProgress } from "./hooks.js";

export function ScrollProgress() {
  const width = useScrollProgress();
  return <div className="site-progress" style={{ width: `${width}%` }} aria-hidden="true" />;
}

export function Header({ activeSection }) {
  const navItems = [
    ["estado", "Alpha.14"],
    ["downloads-por-alpha", "Downloads"],
    ["download", "Builds"],
    ["recursos", "Recursos"],
    ["multiplayer", "Multiplayer"],
    ["documentacao", "Documentação"],
    ["contribua", "Contribua"]
  ];

  return (
    <>
      <aside className="navbr-support-strip" aria-label="Apoie o desenvolvimento do NavBR">
        <div className="shell navbr-support-strip-inner">
          <span><strong>NavBR é um projeto independente.</strong> Ajude a manter desenvolvimento, testes e infraestrutura.</span>
          <a href="#contribua">❤ Contribua</a>
        </div>
      </aside>
      <header className="topbar">
        <a className="brand" href="#inicio" aria-label="OMSI NavBR Multiplayer">
          <span className="brand-box"><img src="./assets/navbr.ico" alt="" /></span>
          <span className="brand-copy"><b>NavBR</b><small>OMSI Multiplayer</small></span>
        </a>
        <nav aria-label="Navegação principal">
          {navItems.map(([id, label]) => (
            <a key={id} href={`#${id}`} className={activeSection === id ? "is-active" : ""}>{label}</a>
          ))}
          <a href={GITHUB_URL} target="_blank" rel="noreferrer">GitHub</a>
        </nav>
      </header>
    </>
  );
}

export function AdSlot({ name }) {
  const [config, setConfig] = useState(null);

  useEffect(() => {
    let active = true;
    fetch("./monetization.json", { cache: "no-store" })
      .then(response => response.ok ? response.json() : null)
      .then(value => { if (active) setConfig(value); })
      .catch(() => { if (active) setConfig(null); });
    return () => { active = false; };
  }, []);

  const client = config?.adsense?.client?.trim();
  const slot = config?.adsense?.slots?.[name]?.trim();
  const enabled = config?.enabled === true && config?.provider === "adsense" && client && slot;

  useEffect(() => {
    if (!enabled) return;
    let script = document.querySelector("script[data-navbr-adsense]");
    if (!script) {
      script = document.createElement("script");
      script.async = true;
      script.crossOrigin = "anonymous";
      script.dataset.navbrAdsense = "true";
      script.src = `https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=${encodeURIComponent(client)}`;
      document.head.appendChild(script);
    }
    try {
      (window.adsbygoogle = window.adsbygoogle || []).push({});
    } catch {
      // Mantém o espaço reservado se a rede de anúncios não responder.
    }
  }, [enabled, client, slot]);

  return (
    <aside className="navbr-ad-slot shell" aria-label="Publicidade">
      <div className="navbr-ad-label">Publicidade</div>
      <div className="navbr-ad-content">
        {enabled ? (
          <ins
            className="adsbygoogle"
            style={{ display: "block" }}
            data-ad-client={client}
            data-ad-slot={slot}
            data-ad-format="auto"
            data-full-width-responsive="true"
          />
        ) : (
          <>
            <strong>Espaço publicitário</strong>
            <span>Reservado para apoiar desenvolvimento, infraestrutura e testes do NavBR.</span>
          </>
        )}
      </div>
    </aside>
  );
}

export function Footer() {
  return (
    <footer className="shell">
      <div className="footer-inner">
        <p>OMSI NavBR Multiplayer • projeto independente • MIT</p>
        <p>Site React/Vite alimentado pelo catálogo real de releases do GitHub.</p>
      </div>
    </footer>
  );
}
