import React, { useMemo, useState } from "react";
import { AdSlot, MonetizationScripts, ScrollProgress } from "./SiteChrome.jsx";
import { useReleaseCatalog } from "./hooks.js";
import { CURRENT_TAG, GITHUB_URL, RELEASES_PAGE, assetLabel, formatBytes } from "./lib.js";
import { COPY, LANGUAGES, resolveInitialLanguage } from "./i18n.js";

const PIX_KEY = "b07a9cc9-b10d-48a8-b201-d28bddc4399a";
const STRIPE_TEST_URL = "https://donate.stripe.com/test_bJecN7ejOf3yf9kaIU43S01";

function formatDate(value, locale) {
  if (!value) return "—";
  return new Intl.DateTimeFormat(locale, { day:"2-digit", month:"short", year:"numeric" }).format(new Date(value));
}
function pickAsset(release, regex) { return (release?.assets || []).find(asset => regex.test(asset?.name || "")); }

function Header({ t, language, setLanguage, current }) {
  const installer = pickAsset(current, /Setup-win-x86\.exe$/i);
  return <header className="nv3-header"><div className="nv3-shell nv3-header-inner">
    <a className="nv3-brand" href="#inicio"><img src="./assets/navbr.ico" alt="" /><span><strong>OMSI NavBR</strong><small>Multiplayer & Mobile Companion</small></span></a>
    <nav className="nv3-nav"><a href="#inicio">{t.nav.home}</a><a href="#recursos">{t.nav.features}</a><a href="#downloads">{t.nav.downloads}</a><a href="#historico">{t.nav.history}</a><a href="#contribua">{t.nav.contribute}</a><a href="#documentacao">{t.nav.docs}</a></nav>
    <div className="nv3-header-actions"><label className="nv3-language"><span>🌐</span><select value={language} onChange={e=>setLanguage(e.target.value)} aria-label="Language">{LANGUAGES.map(item=><option key={item.code} value={item.code}>{item.flag} {item.label}</option>)}</select></label><a className="nv3-button nv3-primary nv3-top-download" href={installer?.browser_download_url || RELEASES_PAGE} target="_blank" rel="noreferrer">↓ {t.nav.downloadNow}</a></div>
  </div></header>;
}

function Hero({ t, current }) {
  const installer = pickAsset(current, /Setup-win-x86\.exe$/i);
  return <section id="inicio" className="nv3-hero"><div className="nv3-shell nv3-hero-grid">
    <div className="nv3-hero-copy"><div className="nv3-badges"><span>{current?.tag_name || CURRENT_TAG}</span><span className="good">● {t.hero.badge}</span></div><h1>{t.hero.title}<small>{t.hero.subtitle}</small></h1><h2>{t.hero.lead}</h2><p>{t.hero.text}</p><div className="nv3-actions"><a className="nv3-button nv3-primary" href={installer?.browser_download_url || RELEASES_PAGE} target="_blank" rel="noreferrer">↓ {t.hero.primary}</a><a className="nv3-button nv3-secondary" href="#recursos">{t.hero.secondary}</a></div><div className="nv3-package-line">Windows · Plugin · Server · Mobile APK · Documentation</div></div>
    <div className="nv3-hero-visual"><img src="./assets/navbr-hero.webp" alt="OMSI NavBR concept presentation" /><div className="nv3-phone"><div className="nv3-phone-top"><b>NavBR</b><span>●</span></div><div className="nv3-phone-tabs"><i>MAP</i><i>BUS</i><i className="active">IBIS</i><i>VOICE</i></div><div className="nv3-lcd"><strong>LINHA     5500</strong><strong>CURSO       10</strong><strong>DESTINO TERMINAL</strong><small>PRÓX. PARADA</small></div><div className="nv3-keypad">{["1","2","3","↑","4","5","6","↓","7","8","9","CLR","0","←","→","ENT"].map(x=><b key={x}>{x}</b>)}</div></div></div>
  </div></section>;
}

function Disclaimer({ t }) { return <section className="nv3-shell nv3-disclaimer"><strong>ⓘ {t.disclaimer.title}</strong><span>{t.disclaimer.text}</span></section>; }
function Features({ t }) { const icons=["👥","🚌","📱","◉","⚙","🗺"]; return <section id="recursos" className="nv3-shell nv3-features">{t.cards.map(([title,text],i)=><article key={title}><span>{icons[i]}</span><h3>{title}</h3><p>{text}</p></article>)}</section>; }

function Mobile({ t }) {
  return <section className="nv3-shell nv3-mobile"><div className="nv3-mobile-art"><img src="./assets/concept-hud.svg" alt="Mobile Companion concept" /><span>CONCEPT / AI</span></div><div className="nv3-mobile-copy"><span className="nv3-eyebrow">{t.mobile.eyebrow}</span><h2>{t.mobile.title}</h2><p>{t.mobile.intro}</p><ul>{t.mobile.bullets.map(item=><li key={item}>✓ {item}</li>)}</ul></div><div className="nv3-mobile-art secondary"><img src="./assets/concept-multiplayer.svg" alt="Multiplayer mobile concept" /><span>CONCEPT / AI</span></div></section>;
}

function CurrentDownloads({ t, current }) {
  const installer=pickAsset(current,/Setup-win-x86\.exe$/i), apk=pickAsset(current,/\.apk$/i), standalone=pickAsset(current,/win-x86\.exe$/i);
  return <section id="downloads" className="nv3-shell nv3-section"><div className="nv3-title-row"><div><h2>{t.downloads.title}</h2><p>{t.downloads.intro}</p></div><a className="nv3-button nv3-primary small" href="#historico">{t.downloads.all}</a></div><div className="nv3-current-grid">
    <article className="current"><div><strong>{current?.tag_name || CURRENT_TAG}</strong><span>{t.downloads.current}</span></div><p>Mobile Companion Alpha 2 · IBIS · telemetry · multiplayer · voice/PTT</p>{installer&&<a href={installer.browser_download_url} target="_blank" rel="noreferrer">▣ {t.downloads.windows}<small>{formatBytes(installer.size)}</small></a>}{apk&&<a href={apk.browser_download_url} target="_blank" rel="noreferrer">⬢ {t.downloads.apk}<small>{formatBytes(apk.size)}</small></a>}<a className="details" href={current?.html_url || RELEASES_PAGE} target="_blank" rel="noreferrer">{t.downloads.details} →</a></article>
    {standalone&&<article><div><strong>Standalone</strong><span>Windows x86</span></div><p>{assetLabel(standalone.name)}</p><a href={standalone.browser_download_url} target="_blank" rel="noreferrer">↓ EXE<small>{formatBytes(standalone.size)}</small></a></article>}
    <article><div><strong>Plugin</strong><span>Native AOT x86</span></div><p>Plugin Bridge + OMSI native interop.</p><a href={current?.html_url || RELEASES_PAGE} target="_blank" rel="noreferrer">{t.downloads.openRelease} →</a></article>
  </div></section>;
}

function History({ t, releases, language }) {
  const [query,setQuery]=useState("");
  const visible=useMemo(()=>releases.filter(r=>!query || (r.tag_name+" "+(r.name||"")).toLowerCase().includes(query.toLowerCase())),[releases,query]);
  return <section id="historico" className="nv3-shell nv3-section nv3-history"><div className="nv3-title-row"><div><span className="nv3-eyebrow">{t.nav.history}</span><h2>{t.downloads.all}</h2></div><input value={query} onChange={e=>setQuery(e.target.value)} placeholder={t.downloads.search} /></div><div className="nv3-history-grid">{visible.length?visible.map((release,index)=>{
    const installer=pickAsset(release,/Setup-win-x86\.exe$/i)||pickAsset(release,/win-x86\.exe$/i), apk=pickAsset(release,/\.apk$/i);
    return <article key={release.tag_name} className={index===0?"latest":""}><div className="nv3-version-head"><div><strong>{release.tag_name}</strong><small>{formatDate(release.published_at,language)}</small></div>{index===0&&<span>{t.downloads.current}</span>}</div><p>{(release.name||release.tag_name).replace(/^OMSI NavBR Multiplayer\s*/i,"")}</p>{installer&&<a href={installer.browser_download_url} target="_blank" rel="noreferrer">▣ {t.downloads.windows}</a>}{apk&&<a href={apk.browser_download_url} target="_blank" rel="noreferrer">⬢ {t.downloads.apk}</a>}<a className="details" href={release.html_url} target="_blank" rel="noreferrer">{t.downloads.details} →</a></article>
  }):<p>{t.downloads.empty}</p>}</div></section>;
}

function Contribute({ t }) {
  const [copied,setCopied]=useState(false);
  const copy=async()=>{try{await navigator.clipboard.writeText(PIX_KEY);setCopied(true);setTimeout(()=>setCopied(false),1800);}catch{window.prompt(t.contribute.copy,PIX_KEY);}};
  return <section id="contribua" className="nv3-shell nv3-contribute"><div className="nv3-contribute-main"><span className="nv3-eyebrow">♥ {t.contribute.eyebrow}</span><h2>{t.contribute.title}</h2><p>{t.contribute.text}</p><div className="nv3-impact">{t.contribute.impact.map(x=><span key={x}>✓ {x}</span>)}</div></div><div className="nv3-donate-card pix"><h3>PIX</h3><p>{t.contribute.pixHelp}</p><code>{PIX_KEY}</code><button className="nv3-button nv3-secondary" onClick={copy}>{copied?t.contribute.copied:t.contribute.copy}</button></div><div className="nv3-donate-card stripe"><h3>Stripe</h3><span className="test">TEST MODE</span><p>{t.contribute.stripeHelp}</p><a className="nv3-button nv3-stripe" href={STRIPE_TEST_URL} target="_blank" rel="noreferrer">{t.contribute.stripeButton}</a></div></section>;
}

function Docs({ t }) {
  const links=[[t.docs.alpha,GITHUB_URL+"/blob/main/docs/ALPHA19_RELEASE_NOTES.md"],[t.docs.mobile,GITHUB_URL+"/blob/main/docs/MOBILE_COMPANION.md"],[t.docs.plugin,GITHUB_URL+"/blob/main/docs/OMSI_PLUGIN_EXPERIMENTAL.md"],[t.docs.github,GITHUB_URL]];
  return <section id="documentacao" className="nv3-shell nv3-section"><div className="nv3-title-row"><div><h2>{t.docs.title}</h2><p>{t.docs.text}</p></div></div><div className="nv3-docs">{links.map(([label,url])=><a key={label} href={url} target="_blank" rel="noreferrer"><span>DOC</span><strong>{label}</strong><b>↗</b></a>)}</div></section>;
}

function Footer({ t, language, setLanguage }) {
  return <footer className="nv3-footer"><div className="nv3-shell nv3-footer-grid"><div className="nv3-brand footer"><img src="./assets/navbr.ico" alt="" /><span><strong>OMSI NavBR</strong><small>{t.footer.project}</small></span></div><div><strong>Links</strong><a href="#inicio">{t.nav.home}</a><a href="#downloads">{t.nav.downloads}</a><a href="#historico">{t.nav.history}</a><a href="#contribua">{t.nav.contribute}</a></div><div><strong>{t.footer.languages}</strong><div className="nv3-language-pills">{LANGUAGES.map(l=><button className={language===l.code?"active":""} key={l.code} onClick={()=>setLanguage(l.code)}>{l.flag} {l.short}</button>)}</div><a href={GITHUB_URL} target="_blank" rel="noreferrer">GitHub ↗</a></div></div><div className="nv3-shell nv3-footer-bottom"><span>© OMSI NavBR Multiplayer</span><span>{t.footer.notice}</span></div></footer>;
}

export default function App() {
  const catalog=useReleaseCatalog();
  const [language,setLanguageState]=useState(resolveInitialLanguage);
  const setLanguage=value=>{setLanguageState(value);window.localStorage.setItem("navbr-site-language",value);document.documentElement.lang=value;};
  const t=COPY[language]||COPY.en;
  const current=useMemo(()=>catalog.releases.find(r=>r?.tag_name===CURRENT_TAG)||catalog.releases[0]||null,[catalog.releases]);
  return <><ScrollProgress/><MonetizationScripts/><Header t={t} language={language} setLanguage={setLanguage} current={current}/><main><Hero t={t} current={current}/><Disclaimer t={t}/><AdSlot name="top"/><Features t={t}/><Mobile t={t}/><CurrentDownloads t={t} current={current}/><AdSlot name="direct"/><History t={t} releases={catalog.releases} language={language}/><Docs t={t}/><AdSlot name="content"/><Contribute t={t}/></main><Footer t={t} language={language} setLanguage={setLanguage}/></>;
}
