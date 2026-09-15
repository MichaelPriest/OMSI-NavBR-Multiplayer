(() => {
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  function addProgressBar() {
    const bar = document.createElement('div');
    bar.className = 'site-progress';
    document.body.appendChild(bar);

    const update = () => {
      const scrollable = document.documentElement.scrollHeight - window.innerHeight;
      const ratio = scrollable > 0 ? window.scrollY / scrollable : 0;
      bar.style.width = `${Math.max(0, Math.min(1, ratio)) * 100}%`;
    };

    update();
    window.addEventListener('scroll', update, { passive: true });
    window.addEventListener('resize', update);
  }

  function injectAlpha11Section() {
    if (document.getElementById('alpha11')) return;

    const reference = document.getElementById('recursos') || document.getElementById('architecture');
    if (!reference?.parentNode) return;

    const section = document.createElement('section');
    section.id = 'alpha11';
    section.className = 'section alpha11-lab';
    section.innerHTML = `
      <div class="shell">
        <div class="alpha11-head">
          <div>
            <span class="eyebrow">Próxima geração • em desenvolvimento</span>
            <h2>Alpha.11: mais integrada ao <span>OMSI.</span></h2>
            <p class="section-lead">A alpha.10 continua sendo a versão oficial atual. A alpha.11 está sendo construída em snapshots internos com uma interface reorganizada, telemetria ampliada e novas ferramentas inspiradas em projetos da comunidade OMSI.</p>
          </div>
          <aside class="alpha11-status">
            <small>Estado da linha de desenvolvimento</small>
            <strong>feature/alpha11-deep-omsi-integration</strong>
            <p>Builds experimentais ficam separadas da release oficial até os testes reais no OMSI 2.3.004.</p>
          </aside>
        </div>

        <div class="alpha11-grid">
          <article class="alpha11-card">
            <div class="alpha-icon">▣</div>
            <h3>HUD modular de ônibus</h3>
            <p>Painel móvel com velocidade, combustível, acelerador, freio, portas, setas, luzes, ré e freio de estacionamento. Cada módulo pode ser ativado ou ocultado.</p>
            <span class="alpha-state ready">BASE IMPLEMENTADA</span>
          </article>
          <article class="alpha11-card">
            <div class="alpha-icon">▦</div>
            <h3>Roadmap Studio</h3>
            <p>Ferramenta integrada para montar <code>whole.roadmap.bmp</code> a partir dos roadmaps por tile sem depender do OMSI Editor, com análise, backup e preview.</p>
            <span class="alpha-state building">EM VALIDAÇÃO</span>
          </article>
          <article class="alpha11-card">
            <div class="alpha-icon">◈</div>
            <h3>Ghost 3D / Replay</h3>
            <p>Grava a viagem e reproduz os frames pela mesma ponte que será usada no futuro para o ônibus remoto físico multiplayer.</p>
            <span class="alpha-state experimental">EXPERIMENTAL</span>
          </article>
          <article class="alpha11-card">
            <div class="alpha-icon">▤</div>
            <h3>Perfis e instalações OMSI</h3>
            <p>Detecção Steam/Aerosoft, múltiplas instalações e launcher integrado, usando OMSI Launcher como referência de fluxo sem copiar código incompatível com a licença.</p>
            <span class="alpha-state ready">BASE IMPLEMENTADA</span>
          </article>
          <article class="alpha11-card">
            <div class="alpha-icon">⇄</div>
            <h3>Compatibilidade da sala</h3>
            <p>Mapa, fingerprint, veículo, HOF, protocolo e capabilities passam a fazer parte do diagnóstico entre jogadores antes do multiplayer físico.</p>
            <span class="alpha-state building">EM DESENVOLVIMENTO</span>
          </article>
          <article class="alpha11-card">
            <div class="alpha-icon">◎</div>
            <h3>Integração física futura</h3>
            <p>OmsiHook/Omsi-Extensions é referência para RoadVehicles, PlayerVehicle, MakeVehicle, posição e rotação. Escrita no OMSI continua bloqueada até o Ghost local ficar estável.</p>
            <span class="alpha-state experimental">LABORATÓRIO</span>
          </article>
        </div>

        <div class="alpha11-pipeline">
          <div class="alpha11-step"><b>01</b><strong>Telemetria</strong><span>Leitura defensiva e estados avançados.</span></div>
          <div class="alpha11-step"><b>02</b><strong>Ghost local</strong><span>Interpolação e lifecycle seguro.</span></div>
          <div class="alpha11-step"><b>03</b><strong>Compatibilidade</strong><span>Mapa, ônibus, HOF e plugin.</span></div>
          <div class="alpha11-step"><b>04</b><strong>Veículo remoto</strong><span>Spawn/update/despawn experimental.</span></div>
          <div class="alpha11-step"><b>05</b><strong>Sincronização visual</strong><span>Portas, luzes, setas e matriz.</span></div>
        </div>
      </div>`;

    reference.insertAdjacentElement('afterend', section);

    const nav = document.querySelector('.topbar nav');
    if (nav && !nav.querySelector('a[href="#alpha11"]')) {
      const link = document.createElement('a');
      link.href = '#alpha11';
      link.textContent = 'Alpha.11';
      const feedback = nav.querySelector('a[href="#feedback"]');
      nav.insertBefore(link, feedback || null);
    }
  }

  function setupRevealAnimations() {
    const selectors = [
      '.section',
      '.status-card',
      '.feature-grid article',
      '.download-choice',
      '.feedback-card',
      '.roadmap-grid article',
      '.alpha11-card',
      '.alpha11-step',
      '.release-card'
    ];
    const nodes = [...document.querySelectorAll(selectors.join(','))];

    if (prefersReducedMotion || !('IntersectionObserver' in window)) {
      nodes.forEach(node => node.classList.add('is-visible'));
      return;
    }

    nodes.forEach(node => node.classList.add('reveal-ready'));
    const observer = new IntersectionObserver(entries => {
      entries.forEach(entry => {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('is-visible');
        observer.unobserve(entry.target);
      });
    }, { threshold: 0.10, rootMargin: '0px 0px -6% 0px' });

    nodes.forEach((node, index) => {
      node.style.transitionDelay = `${Math.min(index % 6, 5) * 45}ms`;
      observer.observe(node);
    });
  }

  function setupActiveNavigation() {
    const links = [...document.querySelectorAll('.topbar nav a[href^="#"]')];
    const sections = links
      .map(link => ({ link, section: document.querySelector(link.getAttribute('href')) }))
      .filter(item => item.section);

    if (!('IntersectionObserver' in window)) return;

    const observer = new IntersectionObserver(entries => {
      const visible = entries
        .filter(entry => entry.isIntersecting)
        .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
      if (!visible) return;

      links.forEach(link => link.classList.remove('is-active'));
      const match = sections.find(item => item.section === visible.target);
      match?.link.classList.add('is-active');
    }, { threshold: [0.15, 0.35, 0.6], rootMargin: '-18% 0px -62% 0px' });

    sections.forEach(item => observer.observe(item.section));
  }

  function setupHeroMotion() {
    if (prefersReducedMotion) return;
    const visual = document.querySelector('.hero-visual');
    const consolePanel = document.querySelector('.project-console');
    const media = document.querySelector('.hero-media');
    if (!visual || !consolePanel || !media) return;

    media.addEventListener('pointermove', event => {
      const rect = media.getBoundingClientRect();
      const x = (event.clientX - rect.left) / rect.width - 0.5;
      const y = (event.clientY - rect.top) / rect.height - 0.5;
      visual.style.transform = `translate3d(${x * 6}px,${y * 5}px,0) rotateY(${x * 1.3}deg) rotateX(${-y * 1.1}deg)`;
      consolePanel.style.transform = `translate3d(${-x * 5}px,${-y * 4}px,0) perspective(1100px) rotateY(${-2.5 + x}deg) rotateX(${1 - y}deg)`;
    });

    media.addEventListener('pointerleave', () => {
      visual.style.transform = '';
      consolePanel.style.transform = '';
    });
  }

  function updateLegacyAlphaText() {
    document.querySelectorAll('.trust-grid b').forEach(element => {
      if (/alpha\.10-test\.\d+/i.test(element.textContent || '')) {
        element.textContent = 'alpha.10';
      }
    });
  }

  function boot() {
    addProgressBar();
    injectAlpha11Section();
    updateLegacyAlphaText();
    setupRevealAnimations();
    setupActiveNavigation();
    setupHeroMotion();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', boot, { once: true });
  } else {
    boot();
  }
})();
