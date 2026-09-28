import React, { useMemo, useState } from "react";
import { AdSlot, MonetizationScripts, ScrollProgress } from "./SiteChrome.jsx";
import { useReleaseCatalog } from "./hooks.js";
import { CURRENT_TAG, GITHUB_URL, RELEASES_PAGE, formatNumber, releaseDownloadCount } from "./lib.js";
import { COPY, LANGUAGES, resolveInitialLanguage } from "./i18n.js";

const PIX_KEY = "b07a9cc9-b10d-48a8-b201-d28bddc4399a";
const STRIPE_URL = "https://donate.stripe.com/4gM9AUevYgaj9ab4C55wI00";

const SHOWCASE_COPY = {
  "pt-BR": {
    heroCaption: "ALPHA.22 • INTERFACE",
    eyebrow: "INTERFACE REAL",
    title: "A Alpha.22 por dentro.",
    intro: "As telas enviadas da versão atual viraram a base visual do novo site: navegação, multiplayer, CCO, empresa, hardware e HUD em uma apresentação mais próxima do produto real.",
    note: "Composição visual baseada nas telas reais da Alpha.22.",
    screens: {
      home: ["Central operacional", "Home"],
      navigation: ["GPS / Roadmap", "Navegação"],
      multiplayer: ["Central multiplayer", "Sessão NavBR"],
      company: ["Company Network", "Rede da empresa"],
      cco: ["Centro de controle operacional", "CCO"],
      hardware: ["Hardware Cockpit", "Painel físico"],
      hud: ["Configurações", "HUD imersivo"]
    },
    manual: {
      eyebrow: "MANUAL OFICIAL",
      title: "Manual em PDF, junto com a Alpha.22.",
      text: "Um guia direto do primeiro acesso aos recursos avançados: salas, navegação, voz, RP, empresa, CCO, hardware, HUD, atalhos, diagnóstico e estado do multiplayer físico.",
      pdfLabel: "Baixar manual PDF",
      onlineLabel: "Manual online",
      shortcuts: "Atalhos",
      bullets: ["Primeiros passos e instalação", "Multiplayer e compatibilidade", "HUD, TP/TS e atalhos", "Diagnóstico e logs"]
    }
  },
  en: {
    heroCaption: "ALPHA.22 • INTERFACE",
    eyebrow: "REAL INTERFACE",
    title: "Inside Alpha.22.",
    intro: "The current product screens now shape the public site: navigation, multiplayer, CCO, company network, hardware and HUD presented much closer to the real app.",
    note: "Visual composition based on the real Alpha.22 screens.",
    screens: {
      home: ["Operations center", "Home"],
      navigation: ["GPS / Roadmap", "Navigation"],
      multiplayer: ["Multiplayer center", "NavBR Session"],
      company: ["Company Network", "Company network"],
      cco: ["Operations control center", "CCO"],
      hardware: ["Hardware Cockpit", "Physical panel"],
      hud: ["Settings", "Immersive HUD"]
    },
    manual: {
      eyebrow: "OFFICIAL MANUAL",
      title: "A PDF manual for Alpha.22.",
      text: "A direct guide from first launch to advanced features: rooms, navigation, voice, RP, company network, CCO, hardware, HUD, shortcuts, diagnostics and physical multiplayer status.",
      pdfLabel: "Download PDF manual",
      onlineLabel: "Online manual",
      shortcuts: "Shortcuts",
      bullets: ["First steps and installation", "Multiplayer and compatibility", "HUD, TP/TS and shortcuts", "Diagnostics and logs"]
    }
  },
  de: {
    heroCaption: "ALPHA.22 • INTERFACE",
    eyebrow: "ECHTE OBERFLÄCHE",
    title: "Alpha.22 von innen.",
    intro: "Die aktuellen Produktansichten prägen jetzt die Website: Navigation, Multiplayer, CCO, Firmennetzwerk, Hardware und HUD näher an der echten App.",
    note: "Visuelle Komposition auf Basis der echten Alpha.22-Oberflächen.",
    screens: {
      home: ["Betriebszentrale", "Start"],
      navigation: ["GPS / Roadmap", "Navigation"],
      multiplayer: ["Multiplayer-Zentrale", "NavBR Sitzung"],
      company: ["Company Network", "Firmennetzwerk"],
      cco: ["Betriebsleitstelle", "CCO"],
      hardware: ["Hardware Cockpit", "Physisches Panel"],
      hud: ["Einstellungen", "Immersives HUD"]
    },
    manual: {
      eyebrow: "OFFIZIELLES HANDBUCH",
      title: "Das PDF-Handbuch zur Alpha.22.",
      text: "Vom ersten Start bis zu erweiterten Funktionen: Räume, Navigation, Sprache, RP, Firmennetzwerk, CCO, Hardware, HUD, Tastenkürzel und Diagnose.",
      pdfLabel: "PDF-Handbuch laden",
      onlineLabel: "Online-Handbuch",
      shortcuts: "Tastenkürzel",
      bullets: ["Erste Schritte und Installation", "Multiplayer und Kompatibilität", "HUD, TP/TS und Tastenkürzel", "Diagnose und Logs"]
    }
  },
  es: {
    heroCaption: "ALPHA.22 • INTERFAZ",
    eyebrow: "INTERFAZ REAL",
    title: "Alpha.22 por dentro.",
    intro: "Las pantallas actuales del producto pasan a formar parte del sitio: navegación, multijugador, CCO, red de empresa, hardware y HUD más cerca de la app real.",
    note: "Composición visual basada en las pantallas reales de Alpha.22.",
    screens: {
      home: ["Central operativa", "Inicio"],
      navigation: ["GPS / Roadmap", "Navegación"],
      multiplayer: ["Central multijugador", "Sesión NavBR"],
      company: ["Company Network", "Red de empresa"],
      cco: ["Centro de control operativo", "CCO"],
      hardware: ["Hardware Cockpit", "Panel físico"],
      hud: ["Configuración", "HUD inmersivo"]
    },
    manual: {
      eyebrow: "MANUAL OFICIAL",
      title: "Manual PDF para Alpha.22.",
      text: "Una guía desde el primer acceso hasta las funciones avanzadas: salas, navegación, voz, RP, empresa, CCO, hardware, HUD, atajos y diagnóstico.",
      pdfLabel: "Descargar manual PDF",
      onlineLabel: "Manual online",
      shortcuts: "Atajos",
      bullets: ["Primeros pasos e instalación", "Multijugador y compatibilidad", "HUD, TP/TS y atajos", "Diagnóstico y logs"]
    }
  },
  pl: {
    heroCaption: "ALPHA.22 • INTERFEJS",
    eyebrow: "PRAWDZIWY INTERFEJS",
    title: "Alpha.22 od środka.",
    intro: "Aktualne ekrany produktu stają się częścią strony: nawigacja, multiplayer, CCO, sieć firmy, hardware i HUD bliżej prawdziwej aplikacji.",
    note: "Kompozycja wizualna oparta na prawdziwych ekranach Alpha.22.",
    screens: {
      home: ["Centrum operacyjne", "Start"],
      navigation: ["GPS / Roadmap", "Nawigacja"],
      multiplayer: ["Centrum multiplayer", "Sesja NavBR"],
      company: ["Company Network", "Sieć firmy"],
      cco: ["Centrum kontroli", "CCO"],
      hardware: ["Hardware Cockpit", "Panel fizyczny"],
      hud: ["Ustawienia", "HUD immersyjny"]
    },
    manual: {
      eyebrow: "OFICJALNY PODRĘCZNIK",
      title: "Podręcznik PDF dla Alpha.22.",
      text: "Od pierwszego uruchomienia po funkcje zaawansowane: pokoje, nawigacja, głos, RP, firma, CCO, hardware, HUD, skróty i diagnostyka.",
      pdfLabel: "Pobierz podręcznik PDF",
      onlineLabel: "Podręcznik online",
      shortcuts: "Skróty",
      bullets: ["Pierwsze kroki i instalacja", "Multiplayer i zgodność", "HUD, TP/TS i skróty", "Diagnostyka i logi"]
    }
  },
  fr: {
    heroCaption: "ALPHA.22 • INTERFACE",
    eyebrow: "INTERFACE RÉELLE",
    title: "Alpha.22 de l'intérieur.",
    intro: "Les écrans actuels du produit structurent désormais le site : navigation, multijoueur, CCO, réseau d'entreprise, matériel et HUD plus proches de l'application réelle.",
    note: "Composition visuelle basée sur les écrans réels de l'Alpha.22.",
    screens: {
      home: ["Centre opérationnel", "Accueil"],
      navigation: ["GPS / Roadmap", "Navigation"],
      multiplayer: ["Centre multijoueur", "Session NavBR"],
      company: ["Company Network", "Réseau entreprise"],
      cco: ["Centre de contrôle", "CCO"],
      hardware: ["Hardware Cockpit", "Panneau physique"],
      hud: ["Paramètres", "HUD immersif"]
    },
    manual: {
      eyebrow: "MANUEL OFFICIEL",
      title: "Le manuel PDF de l'Alpha.22.",
      text: "Un guide du premier lancement aux fonctions avancées : salons, navigation, voix, RP, entreprise, CCO, matériel, HUD, raccourcis et diagnostic.",
      pdfLabel: "Télécharger le manuel PDF",
      onlineLabel: "Manuel en ligne",
      shortcuts: "Raccourcis",
      bullets: ["Premiers pas et installation", "Multijoueur et compatibilité", "HUD, TP/TS et raccourcis", "Diagnostic et logs"]
    }
  }
};


function pickAsset(release, regex) {
  return (release?.assets || []).find(asset => regex.test(asset?.name || ""));
}

function formatDate(value, locale) {
  if (!value) return "—";
  try {
    return new Intl.DateTimeFormat(locale, { day: "2-digit", month: "short", year: "numeric" }).format(new Date(value));
  } catch {
    return new Date(value).toLocaleDateString();
  }
}

function SiteHeader({ t, language, setLanguage, current }) {
  const installer = pickAsset(current, /Setup-win-x86\.exe$/i);
  const nav = [
    ["inicio", "⌂", t.nav.home],
    ["recursos", "ⓘ", t.nav.features],
    ["downloads", "⇩", t.nav.downloads],
    ["historico", "◷", t.nav.history],
    ["contribua", "♡", t.nav.contribute],
    ["documentacao", "▤", t.nav.docs]
  ];

  return (
    <header className="v4-header">
      <div className="v4-shell v4-header-inner">
        <a className="v4-brand" href="#inicio">
          <img src="./assets/navbr.ico" alt="" />
          <span><strong>OMSI NavBR</strong><small>Multiplayer & Mobile Companion</small></span>
        </a>

        <nav className="v4-nav" aria-label="Primary">
          {nav.map(([id, icon, label]) => (
            <a href={"#" + id} key={id}><b>{icon}</b><span>{label}</span></a>
          ))}
        </nav>

        <div className="v4-header-actions">
          <label className="v4-language-select">
            <span>◎</span>
            <select value={language} onChange={e => setLanguage(e.target.value)} aria-label="Language">
              {LANGUAGES.map(item => <option value={item.code} key={item.code}>{item.flag} {item.label}</option>)}
            </select>
          </label>
          <a className="v4-icon-download" href={installer?.browser_download_url || RELEASES_PAGE} target="_blank" rel="noreferrer" aria-label={t.nav.downloadNow}>⇩</a>
          <a className="v4-button v4-primary v4-top-download" href={installer?.browser_download_url || RELEASES_PAGE} target="_blank" rel="noreferrer">⇩ {t.nav.downloadNow}</a>
        </div>
      </div>
    </header>
  );
}

function Hero({ t, current, language, setLanguage }) {
  const installer = pickAsset(current, /Setup-win-x86\.exe$/i);
  return (
    <section id="inicio" className="v4-hero">
      <div className="v4-shell v4-hero-grid">
        <div className="v4-hero-copy">
          <div className="v4-badges">
            <span>◈ {current?.tag_name || CURRENT_TAG}</span>
            <span className="v4-badge-green">● {t.hero.badge}</span>
          </div>
          <h1>{t.hero.title}</h1>
          <h2>{t.hero.subtitle}</h2>
          <h3>{t.hero.lead}</h3>
          <p>{t.hero.text}</p>
          <div className="v4-actions">
            <a className="v4-button v4-primary" href={installer?.browser_download_url || RELEASES_PAGE} target="_blank" rel="noreferrer">⇩ {t.hero.primary}</a>
            <a className="v4-button v4-secondary" href="#recursos">{t.hero.secondary}</a>
          </div>
          <div className="v4-package-line">• Windows &nbsp;• Plugin &nbsp;• Server &nbsp;• Mobile APK &nbsp;• Documentation</div>
        </div>

        <figure className="v4-hero-art">
          <img src="./assets/navbr-mockup-approved.webp" alt="OMSI NavBR Alpha.22 product interface composition" />
          <figcaption>{SHOWCASE_COPY[language]?.heroCaption || SHOWCASE_COPY.en.heroCaption}</figcaption>
        </figure>

        <aside className="v4-language-rail">
          <strong>◎ {t.footer.languages}</strong>
          {LANGUAGES.map(item => (
            <button type="button" key={item.code} className={language === item.code ? "active" : ""} onClick={() => setLanguage(item.code)}>
              <span>{item.flag}</span><div><b>{item.label}</b><small>{item.code === "pt-BR" ? "Idioma principal" : item.code === "en" ? "Full support" : "Supported"}</small></div>
            </button>
          ))}
          <p>◎ OMSI NavBR<br/><small>Mais comunidades. Mais motoristas.</small></p>
        </aside>
      </div>
    </section>
  );
}

function ConceptNotice({ t }) {
  return (
    <section className="v4-shell v4-concept-notice">
      <span className="v4-info-icon">i</span>
      <div><strong>{t.disclaimer.title}</strong><p>{t.disclaimer.text}</p></div>
    </section>
  );
}

function ValidationNotice({ t }) {
  return (
    <section className="v4-shell v4-concept-notice">
      <span className="v4-info-icon">✓</span>
      <div><strong>{t.validation.title}</strong><p>{t.validation.text}</p></div>
    </section>
  );
}

function FeatureStrip({ t }) {
  const icons = ["♟", "▣", "▯", "◉", "⚙", "◆"];
  return (
    <section id="recursos" className="v4-shell v4-feature-strip">
      {t.cards.map(([title, text], index) => (
        <article key={title}>
          <div className="v4-feature-icon">{icons[index]}</div>
          <h3>{title}</h3>
          <p>{text}</p>
        </article>
      ))}
    </section>
  );
}

function ScreenPreview({ type, eyebrow, title, featured = false }) {
  const common = (
    <div className="v4-screen-shell">
      <aside className="v4-screen-sidebar">
        <span className="v4-screen-logo">N</span>
        <i />
        <i />
        <i className="active" />
        <i />
        <i />
      </aside>
      <div className="v4-screen-main">
        <div className="v4-screen-top">
          <div><small>{eyebrow}</small><strong>{title}</strong></div>
          <span>Alpha.22</span>
        </div>
        {type === "navigation" && (
          <div className="v4-screen-nav-layout">
            <div className="v4-screen-map"><b className="route r1" /><b className="route r2" /><b className="bus-dot" /></div>
            <div className="v4-screen-stack"><em>PRÓXIMA MANOBRA</em><strong>↑ Siga em frente</strong><i /><i /></div>
          </div>
        )}
        {type === "multiplayer" && (
          <>
            <div className="v4-screen-metrics"><span>SALA ATUAL<b>—</b></span><span>JOGADORES<b>0</b></span><span>LATÊNCIA<b>—</b></span><span>HOST<b>Offline</b></span></div>
            <div className="v4-screen-session"><div className="v4-screen-map grid-only" /><div><small>VOCÊ</small><strong>Sessão NavBR</strong><p>Chat, voz e RP em tempo real.</p></div></div>
          </>
        )}
        {type === "company" && (
          <>
            <div className="v4-screen-metrics"><span>EMPRESA<b>NavBR</b></span><span>CARGO<b>Presidente</b></span><span>MEMBROS<b>1</b></span></div>
            <div className="v4-screen-two"><div><small>IDENTIDADE NAVBR</small><strong>micha</strong><i /></div><div><small>COMPANY NODE</small><strong>TCP 27740</strong><button>Hospedar neste PC</button></div></div>
          </>
        )}
        {type === "cco" && (
          <>
            <div className="v4-screen-metrics five"><span>MOTORISTAS<b>0</b></span><span>OCORRÊNCIAS<b>0</b></span><span>CRÍTICAS<b>0</b></span><span>ATRASO<b>0</b></span><span>SEM TELEMETRIA<b>0</b></span></div>
            <div className="v4-screen-session"><div className="v4-screen-map grid-only"><b className="route r3" /></div><div><small>OPERAÇÃO LOCAL</small><strong>Nenhum ônibus detectado</strong><p>Sem linha · Sem rota</p></div></div>
          </>
        )}
        {type === "hardware" && (
          <div className="v4-screen-two hardware">
            <div><small>USB / SERIAL</small><strong>NAVBR_HW_V1</strong><label>COM1</label><label>115200</label><button>Conectar hardware</button></div>
            <div><small>TELEMETRIA AO VIVO</small><strong>Aguardando OMSI</strong><i /><i /><i /></div>
          </div>
        )}
        {type === "hud" && (
          <div className="v4-screen-hud-preview">
            <div className="v4-screen-hudbar"><span>LINHA<br/><b>—</b></span><span>ROTA / DESTINO<br/><b>Sem telemetria</b></span><span>PRÓXIMA<br/><b>—</b></span></div>
            <div className="v4-screen-road"><div className="v4-screen-map"><b className="route r1" /><b className="route r2" /><b className="bus-dot" /></div><div className="v4-screen-player-card">MULTIPLAYER<br/><b>Nenhum jogador</b><br/>PTT —</div></div>
          </div>
        )}
        {type === "home" && (
          <>
            <div className="v4-screen-home-card"><small>AGUARDANDO OMSI</small><strong>Nenhuma operação ativa</strong><p>Abra o OMSI para iniciar a telemetria.</p></div>
            <div className="v4-screen-metrics"><span>OMSI<b>Não detectado</b></span><span>MAPA<b>—</b></span><span>MULTIPLAYER<b>Desconectado</b></span></div>
          </>
        )}
      </div>
    </div>
  );
  return <article className={"v4-screen-preview" + (featured ? " featured" : "")}>{common}</article>;
}

function ProductShowcase({ language }) {
  const copy = SHOWCASE_COPY[language] || SHOWCASE_COPY.en;
  return (
    <section id="interface" className="v4-product-showcase">
      <div className="v4-shell">
        <div className="v4-showcase-head">
          <div><span className="v4-eyebrow">{copy.eyebrow}</span><h2>{copy.title}</h2></div>
          <p>{copy.intro}</p>
        </div>
        <div className="v4-real-ui-grid">
          <ScreenPreview type="navigation" eyebrow={copy.screens.navigation[0]} title={copy.screens.navigation[1]} featured />
          <ScreenPreview type="multiplayer" eyebrow={copy.screens.multiplayer[0]} title={copy.screens.multiplayer[1]} featured />
          <ScreenPreview type="company" eyebrow={copy.screens.company[0]} title={copy.screens.company[1]} />
          <ScreenPreview type="cco" eyebrow={copy.screens.cco[0]} title={copy.screens.cco[1]} />
          <ScreenPreview type="hardware" eyebrow={copy.screens.hardware[0]} title={copy.screens.hardware[1]} />
          <ScreenPreview type="hud" eyebrow={copy.screens.hud[0]} title={copy.screens.hud[1]} />
        </div>
        <p className="v4-showcase-note">◈ {copy.note}</p>
      </div>
    </section>
  );
}

function ManualDownload({ language }) {
  const copy = SHOWCASE_COPY[language] || SHOWCASE_COPY.en;
  const manualUrl = "./OMSI-NavBR-Multiplayer-Manual-Oficial-Alpha22.pdf";
  return (
    <section id="manual" className="v4-shell v4-manual-section">
      <div className="v4-manual-art" aria-hidden="true">
        <div className="v4-manual-book">
          <span>MANUAL OFICIAL</span>
          <strong>OMSI <b>NavBR</b></strong>
          <em>Multiplayer</em>
          <i>Guia oficial · Alpha.22</i>
          <div className="v4-manual-mini-screen"><ScreenPreview type="home" eyebrow="CENTRAL OPERACIONAL" title="Boa viagem." /></div>
          <small>PDF · GUIA COMPLETO</small>
        </div>
        <div className="v4-manual-pages">
          <div><b>01</b><strong>Primeiros passos</strong><span>Instalação e operação</span></div>
          <div><b>02</b><strong>Multiplayer</strong><span>Salas, jogadores e voz</span></div>
          <div><b>03</b><strong>HUD e operação</strong><span>Presets e módulos</span></div>
          <div><b>04</b><strong>Atalhos</strong><span>Teclas e diagnóstico</span></div>
        </div>
      </div>
      <div className="v4-manual-copy">
        <span className="v4-eyebrow">{copy.manual.eyebrow}</span>
        <h2>{copy.manual.title}</h2>
        <p>{copy.manual.text}</p>
        <ul>{copy.manual.bullets.map(item => <li key={item}>✓ {item}</li>)}</ul>
        <div className="v4-actions">
          <a className="v4-button v4-primary" href={manualUrl} download>▤ {copy.manual.pdfLabel}</a>
          <a className="v4-button v4-secondary" href={GITHUB_URL + "/blob/main/docs/MANUAL_DE_USO.md"} target="_blank" rel="noreferrer">{copy.manual.onlineLabel} ↗</a>
          <a className="v4-button v4-secondary" href={GITHUB_URL + "/blob/main/docs/KEYBOARD_SHORTCUTS.md"} target="_blank" rel="noreferrer">{copy.manual.shortcuts} ↗</a>
        </div>
      </div>
    </section>
  );
}

function MobileShowcase({ t }) {
  return (
    <section className="v4-shell v4-mobile-showcase">
      <figure className="v4-showcase-image v4-bus-image">
        <img src="./assets/concept-roleplay.svg" alt="Conceptual OMSI NavBR bus scene" />
        <figcaption><strong>SIMULAÇÃO MAIS VIVA,</strong><span>JUNTA COM AMIGOS.</span><small>CONCEPT / AI</small></figcaption>
      </figure>

      <div className="v4-mobile-copy">
        <h2>{t.mobile.title}</h2>
        <span className="v4-new-pill">◆ {t.mobile.eyebrow}</span>
        <p>{t.mobile.intro}</p>
        <ul>{t.mobile.bullets.map(item => <li key={item}><b>✓</b>{item}</li>)}</ul>
      </div>

      <figure className="v4-showcase-image v4-mobile-image">
        <img src="./assets/concept-multiplayer.svg" alt="Conceptual NavBR Mobile Companion interface" />
        <figcaption><small>CONCEPT / AI</small></figcaption>
      </figure>
    </section>
  );
}

function ReleaseCard({ release, t, language, isCurrent }) {
  const installer = pickAsset(release, /Setup-win-x86\.exe$/i) || pickAsset(release, /win-x86\.exe$/i);
  const apk = pickAsset(release, /\.apk$/i);
  return (
    <article className={"v4-release-card" + (isCurrent ? " is-current" : "")}>
      <div className="v4-release-head">
        <div><strong>{release?.tag_name || CURRENT_TAG}</strong><small>{formatDate(release?.published_at, language)}</small></div>
        {isCurrent && <span>{t.downloads.current}</span>}
      </div>
      <p>{(release?.name || release?.tag_name || "").replace(/^OMSI NavBR Multiplayer\s*/i, "") || "Public NavBR build"}</p>
      <small className="v4-release-count">{formatNumber(releaseDownloadCount(release))} downloads</small>
      {installer && <a href={installer.browser_download_url} target="_blank" rel="noreferrer">▣ {t.downloads.windows}</a>}
      {apk && <a href={apk.browser_download_url} target="_blank" rel="noreferrer">▯ {t.downloads.apk}</a>}
      <a className="v4-release-details" href={release?.html_url || RELEASES_PAGE} target="_blank" rel="noreferrer">{t.downloads.details} →</a>
    </article>
  );
}

function DownloadsAndVersions({ t, releases, current, language }) {
  const preview = useMemo(() => {
    const items = [...(releases || [])];
    const currentIndex = items.findIndex(r => r.tag_name === current?.tag_name);
    if (currentIndex > 0) {
      const item = items.splice(currentIndex, 1)[0];
      items.unshift(item);
    }
    return items.slice(0, 4);
  }, [releases, current]);

  return (
    <section id="downloads" className="v4-shell v4-downloads">
      <div className="v4-section-head">
        <div><h2>{t.downloads.title}</h2><p>{t.downloads.intro}</p></div>
        <a className="v4-button v4-primary v4-small-button" href="#historico">⇩ {t.downloads.all}</a>
      </div>
      <div className="v4-release-preview">
        {preview.length ? preview.map((release, index) => (
          <ReleaseCard key={release.tag_name} release={release} t={t} language={language} isCurrent={release.tag_name === current?.tag_name || index === 0} />
        )) : <p>{t.downloads.empty}</p>}
      </div>
    </section>
  );
}

function FullHistory({ t, releases, current, language }) {
  const [query, setQuery] = useState("");
  const visible = useMemo(() => (releases || []).filter(r => {
    const haystack = ((r.tag_name || "") + " " + (r.name || "")).toLowerCase();
    return !query.trim() || haystack.includes(query.trim().toLowerCase());
  }), [releases, query]);

  return (
    <section id="historico" className="v4-shell v4-history">
      <div className="v4-section-head">
        <div><span className="v4-eyebrow">{t.nav.history}</span><h2>{t.downloads.all}</h2></div>
        <input type="search" value={query} onChange={e => setQuery(e.target.value)} placeholder={t.downloads.search} />
      </div>
      <div className="v4-history-grid">
        {visible.map(release => <ReleaseCard key={release.tag_name} release={release} t={t} language={language} isCurrent={release.tag_name === current?.tag_name} />)}
      </div>
    </section>
  );
}

function Support({ t }) {
  const [copied, setCopied] = useState(false);
  const copyPix = async () => {
    try {
      await navigator.clipboard.writeText(PIX_KEY);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1600);
    } catch {
      window.prompt(t.contribute.copy, PIX_KEY);
    }
  };

  return (
    <section id="contribua" className="v4-shell v4-support">
      <div className="v4-support-main">
        <span className="v4-heart">♥</span>
        <div>
          <h2>{t.contribute.title}</h2>
          <p>{t.contribute.text}</p>
          <div className="v4-impact">{t.contribute.impact.map(item => <span key={item}>● {item}</span>)}</div>
        </div>
      </div>

      <div className="v4-donation-card">
        <div className="v4-donation-logo pix">◆</div>
        <div><h3>PIX</h3><p>{t.contribute.pixHelp}</p></div>
        <code>{PIX_KEY}</code>
        <button type="button" className="v4-button v4-secondary" onClick={copyPix}>{copied ? t.contribute.copied : t.contribute.copy}</button>
      </div>

      <div className="v4-donation-card">
        <div className="v4-donation-logo stripe">S</div>
        <div><h3>Stripe</h3><p>{t.contribute.stripeHelp}</p></div>
        <a className="v4-button v4-stripe" href={STRIPE_URL} target="_blank" rel="noreferrer">{t.contribute.stripeButton}</a>
      </div>
    </section>
  );
}

function Documentation({ t, language }) {
  const showcase = SHOWCASE_COPY[language] || SHOWCASE_COPY.en;
  const items = [
    [showcase.manual.pdfLabel, "./OMSI-NavBR-Multiplayer-Manual-Oficial-Alpha22.pdf"],
    [showcase.manual.onlineLabel, GITHUB_URL + "/blob/main/docs/MANUAL_DE_USO.md"],
    [t.docs.alpha, GITHUB_URL + "/blob/main/docs/ALPHA22_RELEASE_NOTES.md"],
    [t.docs.shortcuts, GITHUB_URL + "/blob/main/docs/KEYBOARD_SHORTCUTS.md"],
    [t.docs.mobile, GITHUB_URL + "/blob/main/docs/MOBILE_COMPANION.md"],
    [t.docs.plugin, GITHUB_URL + "/blob/main/docs/OMSI_PLUGIN_EXPERIMENTAL.md"],
    [t.docs.github, GITHUB_URL + "/blob/main/docs/MULTIPLAYER_STATUS.md"]
  ];
  return (
    <section id="documentacao" className="v4-shell v4-docs">
      <div className="v4-section-head"><div><h2>{t.docs.title}</h2><p>{t.docs.text}</p></div></div>
      <div className="v4-doc-grid">
        {items.map(([label, url]) => <a href={url} target="_blank" rel="noreferrer" key={label}><span>▤</span><strong>{label}</strong><b>↗</b></a>)}
      </div>
    </section>
  );
}

function Footer({ t, language, setLanguage }) {
  return (
    <footer className="v4-footer">
      <div className="v4-shell v4-footer-grid">
        <div className="v4-footer-brand">
          <img src="./assets/navbr.ico" alt="" />
          <div><strong>OMSI NavBR</strong><span>Multiplayer & Mobile Companion</span><small>{t.footer.project}</small></div>
        </div>
        <div><strong>Links</strong><a href="#inicio">{t.nav.home}</a><a href="#recursos">{t.nav.features}</a><a href="#downloads">{t.nav.downloads}</a><a href="#historico">{t.nav.history}</a><a href="#contribua">{t.nav.contribute}</a></div>
        <div><strong>{t.docs.title}</strong><a href="#documentacao">{t.docs.alpha}</a><a href="#documentacao">{t.docs.mobile}</a><a href={GITHUB_URL} target="_blank" rel="noreferrer">GitHub ↗</a></div>
        <div><strong>◎ {t.footer.languages}</strong><div className="v4-footer-langs">{LANGUAGES.map(item => <button type="button" key={item.code} className={language === item.code ? "active" : ""} onClick={() => setLanguage(item.code)}>{item.flag} {item.short}</button>)}</div><small>Mais comunidades. Mais histórias. Sem fronteiras.</small></div>
      </div>
      <div className="v4-shell v4-footer-bottom"><span>© OMSI NavBR Multiplayer</span><span>{t.footer.notice}</span></div>
    </footer>
  );
}

export default function App() {
  const catalog = useReleaseCatalog();
  const [language, setLanguageState] = useState(resolveInitialLanguage);
  const t = COPY[language] || COPY.en;
  const current = useMemo(() => catalog.releases.find(r => r?.tag_name === CURRENT_TAG) || catalog.releases[0] || null, [catalog.releases]);

  const setLanguage = value => {
    setLanguageState(value);
    window.localStorage.setItem("navbr-site-language", value);
    document.documentElement.lang = value;
  };

  return (
    <>
      <ScrollProgress />
      <MonetizationScripts />
      <SiteHeader t={t} language={language} setLanguage={setLanguage} current={current} />
      <main>
        <Hero t={t} current={current} language={language} setLanguage={setLanguage} />
        <ConceptNotice t={t} />
        <ValidationNotice t={t} />
        <AdSlot name="top" />
        <FeatureStrip t={t} />
        <ProductShowcase language={language} />
        <ManualDownload language={language} />
        <MobileShowcase t={t} />
        <AdSlot name="direct" />
        <DownloadsAndVersions t={t} releases={catalog.releases} current={current} language={language} />
        <FullHistory t={t} releases={catalog.releases} current={current} language={language} />
        <Documentation t={t} language={language} />
        <AdSlot name="content" />
        <Support t={t} />
      </main>
      <Footer t={t} language={language} setLanguage={setLanguage} />
    </>
  );
}
