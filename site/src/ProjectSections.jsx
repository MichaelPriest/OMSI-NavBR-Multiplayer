import React, { useState } from "react";
import ReleaseCard from "./ReleaseCard.jsx";
import { GITHUB_URL } from "./lib.js";

export function MultiplayerSection() {
  const steps = [
    ["Servidor NavBR", "Servidor dedicado oficial no Render. Não exige portas no PC, mas a infraestrutura atual é gratuita e limitada para Alpha/testes."],
    ["LAN", "Seu PC executa o NavBR.Server para jogadores na mesma rede local, sem depender do servidor oficial."],
    ["Online através do Host", "Seu PC executa o servidor e recebe jogadores pela Internet; pode exigir UPnP, Firewall ou redirecionamento da TCP 27730."]
  ];

  return (
    <section id="multiplayer" className="section shell split-section">
      <div>
        <span className="eyebrow">3 modos de multiplayer</span>
        <h2>Escolha onde a sessão será hospedada.</h2>
        <p className="section-lead">
          O Servidor NavBR oficial usa atualmente o plano gratuito do Render e pode atingir limites de capacidade. No futuro, o projeto poderá oferecer uma assinatura oficial com maior capacidade e estabilidade; ainda não há preço, plano ou data definidos.
        </p>
      </div>
      <div className="steps">
        {steps.map(([title, text], index) => (
          <article key={title}>
            <b>{index + 1}</b>
            <div><h3>{title}</h3><p>{text}</p></div>
          </article>
        ))}
      </div>
    </section>
  );
}

export function DocumentationSection({ releases }) {
  return (
    <section id="documentacao" className="section shell">
      <span className="eyebrow">Documentação</span>
      <h2>Alpha.14 documentada e simulável.</h2>
      <p className="section-lead">
        Roteiro de teste, escopo mestre, plugin experimental e simulador permanecem versionados junto ao código.
      </p>
      <div className="actions">
        <a className="button primary" href={`${GITHUB_URL}/blob/main/docs/ALPHA14_COMMUNITY.md`} target="_blank" rel="noreferrer">Roteiro Alpha.14</a>
        <a className="button secondary" href={`${GITHUB_URL}/blob/main/docs/ALPHA14_MASTER_SCOPE.md`} target="_blank" rel="noreferrer">Escopo Alpha.14</a>
        <a className="button secondary" href={`${GITHUB_URL}/blob/main/docs/OMSI_PLUGIN_EXPERIMENTAL.md`} target="_blank" rel="noreferrer">Plugin experimental</a>
      </div>
      <div className="release-history">
        <h3>Releases recentes</h3>
        <div className="release-grid">
          {releases.slice(0, 6).map(release => (
            <ReleaseCard key={release.tag_name} release={release} />
          ))}
        </div>
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
    <section id="contribua" className="section shell support-wrap support-top">
      <div className="support-card">
        <div>
          <span className="eyebrow">Apoie o projeto</span>
          <h2>Ajude o NavBR a continuar evoluindo.</h2>
          <p>Contribuições são voluntárias e ajudam com desenvolvimento, infraestrutura e testes.</p>
        </div>
        <div className="pix-box">
          <span>PIX — CHAVE ALEATÓRIA</span>
          <strong>{pixKey}</strong>
          <button className="button primary" type="button" onClick={copyPix}>Copiar chave Pix</button>
          <small className="pix-status" aria-live="polite">{copyStatus}</small>
        </div>
      </div>
    </section>
  );
}
