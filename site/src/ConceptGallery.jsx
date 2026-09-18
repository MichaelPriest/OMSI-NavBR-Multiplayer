import React from "react";

const concepts = [
  {
    image: "./assets/concept-hud.webp",
    eyebrow: "HUD NO JOGO",
    title: "Navegação e operação sem esconder o OMSI",
    text: "Conceito visual do HUD com próxima parada, rota, velocidade e estado multiplayer sobre a condução."
  },
  {
    image: "./assets/concept-multiplayer.webp",
    eyebrow: "MULTIPLAYER",
    title: "Ônibus remotos e sala em tempo real",
    text: "Conceito da experiência com outros motoristas, presença, voz e posições sincronizadas no mapa."
  },
  {
    image: "./assets/concept-roleplay.webp",
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
          <span className="eyebrow">Como pode ficar no jogo</span>
          <h2>Conceitos visuais da experiência NavBR dentro do OMSI.</h2>
        </div>
        <p className="section-lead">
          Estas imagens são conceituais. O app continua usando dados reais do C# e do OMSI; elas servem para mostrar a direção visual da Alpha.14.
        </p>
      </div>

      <div className="concept-grid">
        {concepts.map(concept => (
          <article className="concept-card" key={concept.title}>
            <div className="concept-image-wrap">
              <img src={concept.image} alt={concept.title} loading="lazy" />
              <span>{concept.eyebrow}</span>
            </div>
            <div className="concept-copy">
              <h3>{concept.title}</h3>
              <p>{concept.text}</p>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
