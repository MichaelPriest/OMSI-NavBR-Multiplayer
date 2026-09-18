import React from "react";

const concepts = [
  {
    image: "./assets/concept-hud.svg",
    eyebrow: "HUD NO JOGO",
    title: "Navegação e operação sem esconder o OMSI",
    text: "Conceito visual do HUD com próxima parada, rota, velocidade e estado multiplayer sobre a condução."
  },
  {
    image: "./assets/concept-multiplayer.svg",
    eyebrow: "MULTIPLAYER",
    title: "Ônibus remotos e sala em tempo real",
    text: "Conceito da experiência com outros motoristas, presença, voz e posições sincronizadas no mapa."
  },
  {
    image: "./assets/concept-roleplay.svg",
    eyebrow: "PERSONAGEM / RP",
    title: "Saia do ônibus e continue no mesmo mapa",
    text: "Conceito do modo RP com personagem ativo, marcador e câmera de acompanhamento no mapa 3D."
  }
];

export default function ConceptGallery() {
  return (
    <section id="como-fica-no-jogo" className="section shell concept-section">
      <div className="concept-heading">
        <div>
          <span className="eyebrow">Conceitos visuais gerados por IA</span>
          <h2>Uma visão conceitual de como o NavBR pode aparecer dentro do OMSI.</h2>
        </div>
        <p className="section-lead">
          Estas imagens são conceituais e foram criadas por inteligência artificial exclusivamente para ilustrar a direção visual do projeto. Elas não são capturas reais do OMSI nem do NavBR atual. O app continua usando dados reais do C# e do OMSI.
        </p>
      </div>

      <div className="concept-grid">
        {concepts.map(concept => (
          <article className="concept-card" key={concept.title}>
            <div className="concept-image-wrap">
              <img src={concept.image} alt={concept.title} loading="lazy" />
              <span>{concept.eyebrow} • CONCEITO IA</span>
            </div>
            <div className="concept-copy">
              <h3>{concept.title}</h3>
              <p>{concept.text}</p>
            </div>
          </article>
        ))}
      </div>

      <p className="concept-disclaimer">
        Imagens geradas por IA para ilustração conceitual. A interface, os ônibus, cenários, HUDs e personagens mostrados podem diferir da implementação real no OMSI/NavBR.
      </p>
    </section>
  );
}
