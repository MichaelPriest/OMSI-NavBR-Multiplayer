import React from "react";

const highlights = [
  {icon:"◉", title:"Multiplayer físico", text:"A Alpha.22 consolidou o fluxo de veículos remotos físicos e a validação com bots/AI seguindo o host dentro de uma sessão real do OMSI."},
  {icon:"▣", title:"HUD no contexto certo", text:"O HUD passou a respeitar melhor a janela de gameplay do OMSI, evitando aparecer por cima de outras janelas do jogo."},
  {icon:"⌁", title:"NavBR TP/TS", text:"Linha, sentido e rotas vindas do HOF entram no fluxo operacional, com atalhos próprios e integração ao HUD."},
  {icon:"▯", title:"Mobile Companion", text:"PWA e APK continuam integrados à telemetria, mapa, IBIS, voz e controles experimentais do ônibus na rede local."}
];

const shots = [
  {src:"./assets/navbr-mockup-approved.webp", title:"OMSI NavBR", text:"Visão geral da identidade visual do projeto e do Companion.", badge:"CONCEITO VISUAL"},
  {src:"./assets/concept-hud.svg", title:"HUD operacional", text:"Direção de interface para navegação, operação e telemetria sem tirar o foco da condução.", badge:"CONCEITO IA"},
  {src:"./assets/concept-multiplayer.svg", title:"Multiplayer", text:"Direção visual para presença de jogadores, sala, mapa e sincronização.", badge:"CONCEITO IA"},
  {src:"./assets/concept-roleplay.svg", title:"Personagem / RP", text:"Direção visual do modo personagem e da experiência fora do ônibus.", badge:"CONCEITO IA"}
];

export default function Alpha22Showcase() {
  return (
    <section className="a22-section" aria-labelledby="alpha22-title">
      <div className="v4-shell">
        <div className="a22-heading">
          <div>
            <span className="a22-kicker">ALPHA.22 • ESTADO ATUAL</span>
            <h2 id="alpha22-title">NavBR mais integrado ao OMSI, sem esconder o que ainda está em teste.</h2>
          </div>
          <p>O teste online com bots/AI seguindo o host já funcionou. A próxima validação importante continua sendo com jogadores reais em dois PCs e duas sessões OMSI.</p>
        </div>

        <div className="a22-highlight-grid">
          {highlights.map(item => (
            <article className="a22-highlight" key={item.title}>
              <span className="a22-highlight-icon">{item.icon}</span>
              <div><h3>{item.title}</h3><p>{item.text}</p></div>
            </article>
          ))}
        </div>

        <div className="a22-gallery-head">
          <div><span className="a22-kicker">VISUAL DO PROJETO</span><h3>Interface, HUD, multiplayer e Mobile Companion</h3></div>
          <p>Os cards abaixo usam os assets atualmente versionados no projeto. Imagens conceituais continuam identificadas como conceito.</p>
        </div>

        <div className="a22-gallery">
          {shots.map((shot,index) => (
            <figure className={index === 0 ? "a22-shot a22-shot-wide" : "a22-shot"} key={shot.title}>
              <div className="a22-shot-media"><img src={shot.src} alt={shot.title} loading="lazy"/><span>{shot.badge}</span></div>
              <figcaption><strong>{shot.title}</strong><p>{shot.text}</p></figcaption>
            </figure>
          ))}
        </div>

        <div className="a22-manual">
          <div className="a22-manual-icon">⌨</div>
          <div>
            <span className="a22-kicker">MANUAL OFICIAL</span>
            <h3>Atalhos de teclado e controles do NavBR</h3>
            <p>Chat, Push-to-Talk, HUD, NavBR TP/TS, personagem/RP, edição do HUD e detecção de conflitos com o keyboard.cfg.</p>
          </div>
          <a className="v4-button v4-primary" href="https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/blob/main/docs/KEYBOARD_SHORTCUTS.md" target="_blank" rel="noreferrer">Abrir manual ↗</a>
        </div>
      </div>
    </section>
  );
}
