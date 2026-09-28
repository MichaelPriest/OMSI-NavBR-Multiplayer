import React from "react";

const highlights = [
  {icon:"◉", title:"Multiplayer físico", text:"A Alpha.22 consolidou o fluxo de veículos remotos físicos e a validação com bots/AI seguindo o host dentro de uma sessão real do OMSI."},
  {icon:"▣", title:"HUD no contexto certo", text:"O HUD passou a respeitar melhor a janela de gameplay do OMSI, evitando aparecer por cima de outras janelas do jogo."},
  {icon:"⌁", title:"NavBR TP/TS", text:"Linha, sentido e rotas vindas do HOF entram no fluxo operacional, com atalhos próprios e integração ao HUD."},
  {icon:"▯", title:"Mobile Companion", text:"PWA e APK continuam integrados à telemetria, mapa, IBIS, voz e controles experimentais do ônibus na rede local."}
];

const shots = [
  {
    src:"./assets/screens/alpha22/home.webp",
    title:"Central operacional Alpha.22",
    text:"Tela inicial real do aplicativo Windows, com estado do OMSI, mapa, multiplayer e atalhos rápidos para os módulos principais.",
    badge:"CAPTURA REAL • ALPHA.22"
  },
  {
    src:"./assets/screens/alpha22/alpha22-multiplayer.webp",
    title:"Navegação, jogadores, voz e RP",
    text:"Quatro capturas reais reunidas: Navegação, lista de jogadores, Chat & Voz e Personagem / RP.",
    badge:"4 CAPTURAS REAIS"
  },
  {
    src:"./assets/screens/alpha22/alpha22-tools-hud.webp",
    title:"HUD, ferramentas e cockpit",
    text:"Quatro capturas reais reunidas: controles avançados do multiplayer, Hardware Cockpit, seleção de HUD e prévia do preset.",
    badge:"4 CAPTURAS REAIS"
  }
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
          <div>
            <span className="a22-kicker">TELAS REAIS DO APP</span>
            <h3>Alpha.22 por dentro</h3>
          </div>
          <p>Estas imagens são capturas reais do aplicativo Windows. Estados sem telemetria refletem o OMSI fechado no momento da captura; não são dados simulados.</p>
        </div>

        <div className="a22-gallery">
          {shots.map(shot => (
            <figure className="a22-shot" key={shot.title}>
              <div className="a22-shot-media">
                <img src={shot.src} alt={shot.title} loading="lazy"/>
                <span>{shot.badge}</span>
              </div>
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
