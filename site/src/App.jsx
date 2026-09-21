import React, { useMemo, useState } from "react";
import { AdSlot, MonetizationScripts, ScrollProgress } from "./SiteChrome.jsx";
import { useReleaseCatalog } from "./hooks.js";
import { CURRENT_TAG, GITHUB_URL, RELEASES_PAGE, formatNumber, releaseDownloadCount } from "./lib.js";
import { COPY, LANGUAGES, resolveInitialLanguage } from "./i18n.js";

const PIX_KEY = "b07a9cc9-b10d-48a8-b201-d28bddc4399a";
const STRIPE_URL = "https://donate.stripe.com/4gM9AUevYgaj9ab4C55wI00";

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
          <img src="./assets/navbr-mockup-approved.webp" alt="OMSI NavBR conceptual cockpit and mobile companion artwork" />
          <figcaption>CONCEPT IMAGE / AI</figcaption>
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

function Documentation({ t }) {
  const items = [
    [t.docs.alpha, GITHUB_URL + "/blob/main/docs/ALPHA19_RELEASE_NOTES.md"],
    [t.docs.mobile, GITHUB_URL + "/blob/main/docs/MOBILE_COMPANION.md"],
    [t.docs.plugin, GITHUB_URL + "/blob/main/docs/OMSI_PLUGIN_EXPERIMENTAL.md"],
    [t.docs.github, GITHUB_URL]
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
        <AdSlot name="top" />
        <FeatureStrip t={t} />
        <MobileShowcase t={t} />
        <AdSlot name="direct" />
        <DownloadsAndVersions t={t} releases={catalog.releases} current={current} language={language} />
        <FullHistory t={t} releases={catalog.releases} current={current} language={language} />
        <Documentation t={t} />
        <AdSlot name="content" />
        <Support t={t} />
      </main>
      <Footer t={t} language={language} setLanguage={setLanguage} />
    </>
  );
}
