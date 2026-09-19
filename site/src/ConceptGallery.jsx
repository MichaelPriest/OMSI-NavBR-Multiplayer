import React from "react";

const concepts = [
  {
    image: "./assets/concept-hud.svg",
    eyebrow: "HUD / NAVEGAÇÃO",
    title: "Informação operacional sem tirar o foco da condução",
    text: "Conceito de HUD para próxima parada, rota, velocidade e estado multiplayer."
  },
  {
    image: "./assets/concept-multiplayer.svg",
    eyebrow: "MULTIPLAYER",
    title: "Motoristas compartilhando a mesma operação",
    text: "Conceito da experiência com presença, voz, sala e posições sincronizadas."
  },
  {
    image: "./assets/concept-roleplay.svg",
    eyebrow: "PERSONAGEM / RP",
    title: "Continue a experiência fora do ônibus",
    text: "Conceito visual do modo RP com personagem e acompanhamento no mapa."
  }
];

export default function ConceptGallery() {
  return (
    <section id="produto-visual" className="section shell v2-concepts">
      <div className="v2-section-heading">
        <div><span className="eyebrow">Direção visual</span><h2>Uma interface pensada para acompanhar o simulador.</h2></div>
        <p>As imagens abaixo são conceitos gerados por IA e não representam capturas da implementação atual.</p>
      </div>

      <div className="v2-concept-grid">
        {concepts.map((concept, index) => (
          <article className={index === 0 ? "featured" : ""} key={concept.title}>
            <div className="v2-concept-image">
              <img src={concept.image} alt={concept.title} loading="lazy" />
              <span>CONCEITO IA</span>
            </div>
            <div className="v2-concept-copy">
              <small>{concept.eyebrow}</small>
              <h3>{concept.title}</h3>
              <p>{concept.text}</p>
            </div>
          </article>
        ))}
      </div>
      <p className="v2-concept-note">Conceitos visuais gerados por IA. A interface real pode diferir conforme a implementação e os dados disponíveis no OMSI.</p>
    </section>
  );
}
