import React, { useState } from "react";
import { GITHUB_URL, formatDate, summarizeBody } from "./lib.js";

export function ProductHighlights() {
  const items = [
    ["01", "Multiplayer integrado", "Salas, presença, chat, voz e telemetria compartilhando o mesmo estado operacional."],
    ["02", "Navegação no mapa real", "Roadmap, rota, paradas e retorno à rota usando dados do mapa carregado no OMSI."],
    ["03", "Operação e HUD", "Painéis, HUD configurável, CCO, perfil, empresa e ferramentas para condução."],
    ["04", "Integração nativa", "Plugin Bridge e interop x86 para recursos experimentais que precisam conversar diretamente com o OMSI."]
  ];

  return (
    <section id="recursos" className="section shell v2-highlights">
      <div className="v2-section-heading">
        <div><span className="eyebrow">O produto</span><h2>Um ecossistema em torno da condução.</h2></div>
        <p>A proposta do NavBR é reunir multiplayer e ferramentas operacionais sem substituir o simulador: o OMSI continua sendo a autoridade da condução.</p>
      </div>
      <div className="v2-highlight-grid">
        {items.map(([number, title, text]) => (
          <article key={title}><span>{number}</span><h3>{title}</h3><p>{text}</p></article>
        ))}
      </div>
    </section>
  );
}

export function MultiplayerSection() {
  const modes = [
    ["Servidor NavBR", "Conecte-se ao servidor oficial de testes sem hospedar uma sala no próprio PC.", "Mais simples"],
    ["LAN", "Crie uma sessão na mesma rede local para validar multiplayer entre máquinas próximas.", "Rede local"],
    ["Host pela Internet", "Hospede a sessão no seu PC e permita conexões externas quando a rede estiver configurada.", "Avançado"]
  ];

  return (
    <section id="multiplayer" className="section shell v2-multiplayer">
      <div className="v2-section-heading">
        <div><span className="eyebrow">Multiplayer</span><h2>Três formas de jogar em conjunto.</h2></div>
        <p>Cada modo tem um objetivo claro. Na Alpha.18, LAN/local e os modos online ainda estão em validação pública e não devem ser tratados como comprovados ponta a ponta.</p>
      </div>
      <div className="v2-public-alpha-warning">
        <strong>Estado de validação</strong>
        <span>Implementação disponível para testes. Ainda faltam testes reproduzíveis com dois computadores reais para validar LAN, Servidor NavBR, Host pela Internet e ônibus remoto físico.</span>
      </div>
      <div className="v2-mode-grid">
        {modes.map(([title, text, badge], index) => (
          <article key={title}>
            <div className="v2-mode-number">0{index + 1}</div>
            <span className="v2-mode-badge">{badge}</span>
            <h3>{title}</h3>
            <p>{text}</p>
          </article>
        ))}
      </div>
      <div className="v2-flow">
        <span>OMSI 2</span><i>→</i><span>NavBR Client</span><i>→</i><span>Servidor / Host</span><i>→</i><span>Outros motoristas</span>
      </div>
    </section>
  );
}

export function NewsSection({ releases = [] }) {
  const items = releases.slice(0, 4);
  return (
    <section id="novidades" className="section shell v2-news">
      <div className="v2-section-heading">
        <div><span className="eyebrow">Novidades</span><h2>O que mudou nas últimas builds.</h2></div>
        <a className="text-link" href="#todas-versoes">Ver histórico completo →</a>
      </div>
      <div className="v2-news-list">
        {items.map((release, index) => {
          const summary = summarizeBody(release.body);
          const shortSummary = summary ? summary.slice(0, 210) + (summary.length > 210 ? "…" : "") : "Build pública do NavBR.";
          return (
            <article key={release.tag_name}>
              <div className="v2-news-index">{String(index + 1).padStart(2, "0")}</div>
              <div className="v2-news-main">
                <div className="v2-news-meta"><span>{release.tag_name}</span><small>{formatDate(release.published_at)}</small></div>
                <h3>{release.name || release.tag_name}</h3>
                <p>{shortSummary}</p>
              </div>
              <a href={release.html_url} target="_blank" rel="noreferrer" aria-label={"Abrir " + release.tag_name}>↗</a>
            </article>
          );
        })}
      </div>
    </section>
  );
}

export function RoadmapSection() {
  const columns = [
    ["Agora", "Correções e validação", ["Ônibus físico remoto", "Roadmap/GPS e retorno à rota", "Personagem/RP", "Instalação e atualização do plugin"]],
    ["Próximo", "Consolidação", ["UX das salas", "Diagnósticos de runtime", "Compatibilidade entre veículos", "Melhorias no servidor e CCO"]],
    ["Futuro", "Expansão", ["NavBR Mobile Companion", "IBIS Mobile", "Segundo monitor / GPS", "Recursos móveis de operação"]]
  ];

  return (
    <section id="roadmap" className="section shell v2-roadmap">
      <div className="v2-section-heading">
        <div><span className="eyebrow">Roadmap</span><h2>Construído por etapas, sem promessas artificiais.</h2></div>
        <p>O foco atual é corrigir e validar o que já existe antes de ampliar o produto.</p>
      </div>
      <div className="v2-roadmap-grid">
        {columns.map(([title, subtitle, items], index) => (
          <article key={title} className={index === 0 ? "active" : ""}>
            <span>{title}</span><h3>{subtitle}</h3><ul>{items.map(item => <li key={item}>{item}</li>)}</ul>
          </article>
        ))}
      </div>
    </section>
  );
}

export function DocumentationSection() {
  const links = [
    ["Alpha.18", "Notas, limitações e estado da versão pública", GITHUB_URL + "/blob/main/docs/ALPHA18_RELEASE_NOTES.md"],
    ["Validação Alpha.18", "Roteiro para testar LAN, online, plugin e ônibus físico", GITHUB_URL + "/blob/main/docs/ALPHA18_COMMUNITY.md"],
    ["Plugin OMSI", "Integração experimental e diagnóstico", GITHUB_URL + "/blob/main/docs/OMSI_PLUGIN_EXPERIMENTAL.md"],
    ["Mobile Companion", "Roadmap do smartphone e IBIS", GITHUB_URL + "/blob/main/docs/MOBILE_COMPANION.md"]
  ];

  return (
    <section id="documentacao" className="section shell v2-docs">
      <div className="v2-section-heading">
        <div><span className="eyebrow">Documentação</span><h2>Detalhes para quem quer testar, entender ou contribuir.</h2></div>
        <a className="button secondary" href={GITHUB_URL} target="_blank" rel="noreferrer">Abrir GitHub</a>
      </div>
      <div className="v2-doc-grid">
        {links.map(([title, text, href]) => (
          <a href={href} target="_blank" rel="noreferrer" key={title}>
            <span>Documento</span><h3>{title}</h3><p>{text}</p><b>Consultar →</b>
          </a>
        ))}
      </div>
    </section>
  );
}

export function SupportSection() {
  const pixKey = "b07a9cc9-b10d-48a8-b201-d28bddc4399a";
  const [copyStatus, setCopyStatus] = useState("");

  const copyPix = async () => {
    try {
      await navigator.clipboard.writeText(pixKey);
      setCopyStatus("Chave copiada ✓");
      window.setTimeout(() => setCopyStatus(""), 1800);
    } catch {
      window.prompt("Copie a chave Pix:", pixKey);
    }
  };

  return (
    <section id="contribua" className="section shell v2-support">
      <div className="v2-support-card">
        <div>
          <span className="eyebrow">Apoie o projeto</span>
          <h2>Ajude a manter desenvolvimento, testes e infraestrutura.</h2>
          <p>O NavBR é um projeto independente. Contribuições via Pix são voluntárias e não desbloqueiam recursos exclusivos.</p>
        </div>
        <div className="v2-pix-card">
          <span>PIX • CHAVE ALEATÓRIA</span>
          <strong>{pixKey}</strong>
          <button className="button primary" type="button" onClick={copyPix}>Copiar chave Pix</button>
          <small aria-live="polite">{copyStatus || "Contribuição voluntária"}</small>
        </div>
      </div>
    </section>
  );
}
