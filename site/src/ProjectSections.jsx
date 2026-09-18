import React, { useState } from "react";
import ReleaseCard from "./ReleaseCard.jsx";
import { GITHUB_URL } from "./lib.js";

export function MultiplayerSection() {
  const steps = [
    ["PC A cria a sala", "Abra Multiplayer > Sala, escolha mapa/operação e crie a sessão."],
    ["PC B entra", "Use o endereço/convite ou o diretório público e confirme compatibilidade."],
    ["Confirme os dois sentidos", "Observe jogadores, movimento, voz/chat, RP e despawn/reconexão."]
  ];

  return (
    <section id="multiplayer" className="section shell split-section">
      <div>
        <span className="eyebrow">Teste multiplayer</span>
        <h2>Comece em dois PCs na mesma rede local.</h2>
        <p className="section-lead">
          O modo padrão continua peer-host. Para validar ônibus físico e RP, use o mesmo mapa e versões compatíveis de plugin.
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
        <a className="button primary" href={`${GITHUB_URL}/blob/feature/alpha14-roleplay/docs/ALPHA14_TEST3_COMMUNITY.md`} target="_blank" rel="noreferrer">Roteiro Test 3</a>
        <a className="button secondary" href={`${GITHUB_URL}/blob/feature/alpha14-roleplay/docs/ALPHA14_MASTER_SCOPE.md`} target="_blank" rel="noreferrer">Escopo Alpha.14</a>
        <a className="button secondary" href={`${GITHUB_URL}/blob/feature/alpha14-roleplay/docs/OMSI_PLUGIN_EXPERIMENTAL.md`} target="_blank" rel="noreferrer">Plugin experimental</a>
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
    <section id="contribua" className="section shell support-wrap">
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
