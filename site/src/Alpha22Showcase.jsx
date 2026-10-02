import React from "react";

const highlights = [
  {icon:"▣", title:"Multiplayer físico OMSI 2", text:"A Alpha.25 reforça o fluxo físico com identidade real do veículo, transição de Kachel e telemetria móvel a 20 Hz. O teste ponta a ponta com dois jogadores reais continua pendente."},
  {icon:"◈", title:"NavBR para openOMSI", text:"Gateway LAN v6 e plugin dedicado mantêm o openOMSI como caminho paralelo de integração, sem substituir a compatibilidade com o OMSI 2 x86."},
  {icon:"⇄", title:"React + WebView2", text:"A interface oficial desktop usa React/WebView2 consumindo o estado real do backend, preservando os recursos novos trazidos durante a evolução da plataforma."},
  {icon:"◎", title:"HUD NavBR In-Game", text:"HUD configurável, presets e módulos voltados ao contexto da janela do jogo, com aplicação ao vivo pelo estado nativo do cliente."},
  {icon:"⌁", title:"Operações, CCO e Empresa", text:"Ferramentas de operação, ocorrências, perfil, empresa, frota e suporte ao motorista reunidas no mesmo ecossistema NavBR."},
  {icon:"◇", title:"Ghost e Replay", text:"Gravação, biblioteca, analytics, prévia e replay físico fazem parte das ferramentas experimentais conectadas ao Plugin Bridge."},
  {icon:"⚙", title:"Hardware Cockpit", text:"Integração de cockpit e telemetria de hardware continuam disponíveis junto do controle de desempenho e das rotinas nativas."},
  {icon:"▯", title:"Mobile Companion", text:"PWA e APK acompanham GPS, mapa, IBIS, voz, multiplayer e telemetria pela mesma rede local, com controles experimentais protegidos."}
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
            <span className="a22-kicker">ALPHA.25 • ESTADO ATUAL</span>
            <h2 id="alpha22-title">Alpha.25 reúne OMSI 2, openOMSI, multiplayer, operação, HUD e Mobile em uma plataforma única.</h2>
          </div>
          <p>Os smokes automatizados do multiplayer físico passaram, incluindo transição de Kachel, telemetria em movimento a 20 Hz e HOF não bloqueante. O teste ponta a ponta com jogadores reais em dois PCs e duas sessões OMSI ainda está pendente.</p>
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
            <h3>Capturas reais da linha Alpha.22</h3>
          </div>
          <p>Estas imagens continuam como capturas reais de referência da Alpha.22. Elas mostram módulos que permanecem no projeto, mas não devem ser interpretadas como screenshots atualizadas da Alpha.25.</p>
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
