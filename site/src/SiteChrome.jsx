import React, { useEffect, useRef, useState } from "react";
import { GITHUB_URL } from "./lib.js";
import { useScrollProgress } from "./hooks.js";

export function ScrollProgress() {
  const width = useScrollProgress();
  return <div className="site-progress" style={{ width: `${width}%` }} aria-hidden="true" />;
}

export function Header({ activeSection, onOpenDownloads }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const navItems = [
    ["recursos", "Recursos"],
    ["multiplayer", "Multiplayer"],
    ["download", "Downloads"],
    ["todas-versoes", "Versões"],
    ["roadmap", "Roadmap"],
    ["documentacao", "Documentação"]
  ];

  return (
    <>
      <aside className="v2-utility-bar" aria-label="Informações do projeto">
        <div className="shell">
          <span><strong>OMSI NavBR</strong> • projeto independente e open source</span>
          <a href="#contribua">Apoiar via Pix</a>
        </div>
      </aside>

      <header className="topbar v2-topbar">
        <a className="brand" href="#inicio" aria-label="OMSI NavBR Multiplayer">
          <span className="brand-box"><img src="./assets/navbr.ico" alt="" /></span>
          <span className="brand-copy"><b>NavBR</b><small>OMSI Multiplayer</small></span>
        </a>

        <nav className="v2-desktop-nav" aria-label="Navegação principal">
          {navItems.map(([id, label]) => (
            <a key={id} href={"#" + id} className={activeSection === id ? "is-active" : ""}>{label}</a>
          ))}
          <a href={GITHUB_URL} target="_blank" rel="noreferrer">GitHub</a>
        </nav>

        <div className="v2-header-actions">
          <button className="button primary v2-header-download" type="button" onClick={onOpenDownloads}>Baixar NavBR</button>
          <button
            className="v2-menu-button"
            type="button"
            aria-label="Abrir menu"
            aria-expanded={mobileOpen}
            onClick={() => setMobileOpen(value => !value)}
          >
            <i /><i /><i />
          </button>
        </div>

        {mobileOpen && (
          <nav className="v2-mobile-nav" aria-label="Navegação móvel">
            {navItems.map(([id, label]) => (
              <a key={id} href={"#" + id} onClick={() => setMobileOpen(false)}>{label}</a>
            ))}
            <a href="#contribua" onClick={() => setMobileOpen(false)}>Contribua</a>
            <a href={GITHUB_URL} target="_blank" rel="noreferrer">GitHub ↗</a>
          </nav>
        )}
      </header>
    </>
  );
}

export function MonetizationScripts() {
  useEffect(() => {
    const scripts = [
      {
        id: "navbr-profitablerate-ed6a145b",
        src: "https://pl31372716.profitableratecpmnetwork.com/ed/6a/14/ed6a145b997a12d36e14a919de76a3ca.js"
      },
      {
        id: "navbr-profitablerate-e700431c",
        src: "https://pl31372717.profitableratecpmnetwork.com/e7/00/43/e700431c5e6534141987ce3a30b0adfd.js"
      }
    ];

    scripts.forEach(({ id, src }) => {
      if (document.getElementById(id)) return;
      const script = document.createElement("script");
      script.id = id;
      script.src = src;
      script.async = true;
      document.body.appendChild(script);
    });
  }, []);

  return null;
}

export function AdSlot({ name }) {
  const [config, setConfig] = useState(null);
  const nativeAdRef = useRef(null);

  useEffect(() => {
    let active = true;
    fetch("./monetization.json", { cache: "no-store" })
      .then(response => response.ok ? response.json() : null)
      .then(value => { if (active) setConfig(value); })
      .catch(() => { if (active) setConfig(null); });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    if (name !== "top" || !nativeAdRef.current) return;

    const containerId = "container-fe112edda848f13dd1ca9fb2279fbed7";
    if (!document.getElementById(containerId)) {
      const container = document.createElement("div");
      container.id = containerId;
      nativeAdRef.current.appendChild(container);
    }

    if (!document.querySelector('script[data-navbr-native-ad="fe112edda848f13dd1ca9fb2279fbed7"]')) {
      const script = document.createElement("script");
      script.async = true;
      script.dataset.cfasync = "false";
      script.dataset.navbrNativeAd = "fe112edda848f13dd1ca9fb2279fbed7";
      script.src = "https://pl31372719.profitableratecpmnetwork.com/fe112edda848f13dd1ca9fb2279fbed7/invoke.js";
      nativeAdRef.current.appendChild(script);
    }
  }, [name]);

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

  if (name === "direct") {
    return (
      <aside className="navbr-ad-slot shell sponsored-link" aria-label="Publicidade">
        <div className="navbr-ad-label">Publicidade</div>
        <div className="navbr-ad-content">
          <strong>Conteúdo patrocinado</strong>
          <span>Este acesso ajuda a financiar hospedagem, testes e desenvolvimento do NavBR.</span>
          <a
            className="button secondary"
            href="https://www.profitableratecpmnetwork.com/a8tvv2zu8n?key=65f851ce1b0f30c80d91f114566cfb88"
            target="_blank"
            rel="sponsored noreferrer"
          >
            Ver oferta patrocinada
          </a>
        </div>
      </aside>
    );
  }

  if (name === "top") {
    return (
      <aside className="navbr-ad-slot shell" aria-label="Publicidade">
        <div className="navbr-ad-label">Publicidade</div>
        <div className="navbr-ad-content navbr-native-ad" ref={nativeAdRef} />
      </aside>
    );
  }

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
    <footer className="v2-footer">
      <div className="shell v2-footer-grid">
        <div className="v2-footer-brand">
          <a className="brand" href="#inicio">
            <span className="brand-box"><img src="./assets/navbr.ico" alt="" /></span>
            <span className="brand-copy"><b>NavBR</b><small>OMSI Multiplayer</small></span>
          </a>
          <p>Projeto independente para OMSI 2. Código aberto sob licença MIT.</p>
        </div>

        <div>
          <strong>Produto</strong>
          <a href="#recursos">Recursos</a>
          <a href="#multiplayer">Multiplayer</a>
          <a href="#roadmap">Roadmap</a>
        </div>

        <div>
          <strong>Downloads</strong>
          <a href="#download">Versão atual</a>
          <a href="#todas-versoes">Todas as versões</a>
          <a href={GITHUB_URL + "/releases"} target="_blank" rel="noreferrer">GitHub Releases ↗</a>
        </div>

        <div>
          <strong>Projeto</strong>
          <a href="#documentacao">Documentação</a>
          <a href="#contribua">Contribua</a>
          <a href={GITHUB_URL} target="_blank" rel="noreferrer">Código-fonte ↗</a>
        </div>
      </div>

      <div className="shell v2-footer-bottom">
        <span>OMSI NavBR Multiplayer</span>
        <span>OMSI é uma marca de seus respectivos proprietários. NavBR é um projeto independente.</span>
      </div>
    </footer>
  );
}
