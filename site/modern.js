(() => {
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const repo = 'MichaelPriest/OMSI-NavBR-Multiplayer';
  const alpha11Test = {
    tag: 'v0.3.0-alpha.11-test.1',
    releaseUrl: `https://github.com/${repo}/releases/tag/v0.3.0-alpha.11-test.1`,
    exeUrl: `https://github.com/${repo}/releases/download/v0.3.0-alpha.11-test.1/OMSI-NavBR-Multiplayer-v0.3.0-alpha.11-test.1-win-x86.exe`,
    clientZipUrl: `https://github.com/${repo}/releases/download/v0.3.0-alpha.11-test.1/OMSI-NavBR-Multiplayer-v0.3.0-alpha.11-test.1-win-x86.zip`,
    serverZipUrl: `https://github.com/${repo}/releases/download/v0.3.0-alpha.11-test.1/OMSI-NavBR-Server-v0.3.0-alpha.11-test.1-win-x64.zip`,
    pluginZipUrl: `https://github.com/${repo}/releases/download/v0.3.0-alpha.11-test.1/OMSI-NavBR-Plugin-Experimental-v0.3.0-alpha.11-test.1-win-x86.zip`
  };

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
            <span class="eyebrow">Teste público disponível</span>
            <h2>Alpha.11 Test 1: a nova integração com o <span>OMSI.</span></h2>
            <p class="section-lead">A primeira build pública da série alpha.11 já está liberada. Ela mantém a alpha.10 como release oficial atual, mas abre a linha de testes para HUD/mapa renovado, rota completa, pontos de parada, telemetria 3D exata e a infraestrutura inicial de tráfego AI sincronizado pelo host.</p>
            <div class="actions">
              <a class="button primary" href="${alpha11Test.exeUrl}" target="_blank" rel="noreferrer">Baixar Alpha.11 Test 1</a>
              <a class="button secondary" href="${alpha11Test.releaseUrl}" target="_blank" rel="noreferrer">Abrir pré-release</a>
            </div>
          </div>
          <aside class="alpha11-status">
            <small>Build pública atual</small>
            <strong>${alpha11Test.tag}</strong>
            <p>CI verde, cliente standalone, ZIP do cliente, servidor x64 e plugin experimental publicados separadamente.</p>
          </aside>
        </div>

        <div class="alpha11-grid">
          <article class="alpha11-card">
            <div class="alpha-icon">▣</div>
            <h3>HUD e minimapa renovados</h3>
            <p>Preserva o comportamento funcional da alpha.8 e usa aprendizado da janela real de gameplay para esconder o HUD em menus, opções e diálogos internos do OMSI.</p>
            <span class="alpha-state ready">TESTAR AGORA</span>
          </article>
          <article class="alpha11-card">
            <div class="alpha-icon">H</div>
            <h3>Pontos de parada e rota completa</h3>
            <p>Leitura das paradas do mapa, ícone OMSI H, destaque da próxima parada e visão geral da rota ativa com enquadramento completo.</p>
            <span class="alpha-state ready">IMPLEMENTADO</span>
          </article>
          <article class="alpha11-card">
            <div class="alpha-icon">3D</div>
            <h3>Telemetria remota 3D exata</h3>
            <p>Identidade do ônibus, pose local nativa e quaternion do OMSI já percorrem a ponte multiplayer. A criação física do segundo ônibus ainda permanece protegida pelo backend experimental.</p>
            <span class="alpha-state experimental">INFRAESTRUTURA PRONTA</span>
          </article>
          <article class="alpha11-card">
            <div class="alpha-icon">AI</div>
            <h3>Tráfego com autoridade do host</h3>
            <p>O host passa a ser a autoridade do tráfego rodoviário capturado, com snapshots de veículos AI, modelo, posição, rotação, velocidade, luzes e setas.</p>
            <span class="alpha-state building">EM TESTE</span>
          </article>
          <article class="alpha11-card">
            <div class="alpha-icon">⇄</div>
            <h3>Conteúdo compatível entre jogadores</h3>
            <p>A arquitetura futura prevê manifesto, hash e transferência pelo host para conteúdo que possa ser redistribuído, com detecção de dependências ausentes antes de entrar na sessão.</p>
            <span class="alpha-state building">PLANEJADO</span>
          </article>
          <article class="alpha11-card">
            <div class="alpha-icon">⚙</div>
            <h3>Eficiência e otimização</h3>
            <p>LOD, culling por distância, frequência adaptativa, menos polling e sincronização por interesse entram na próxima etapa depois da validação funcional da base.</p>
            <span class="alpha-state experimental">PRÓXIMA ETAPA</span>
          </article>
        </div>

        <div class="alpha11-pipeline">
          <div class="alpha11-step"><b>01</b><strong>Test 1</strong><span>HUD, mapa, paradas, rota e base multiplayer.</span></div>
          <div class="alpha11-step"><b>02</b><strong>Veículo remoto</strong><span>Spawn/update/despawn físico experimental.</span></div>
          <div class="alpha11-step"><b>03</b><strong>Tráfego host</strong><span>AI autoritativa sem duplicação local.</span></div>
          <div class="alpha11-step"><b>04</b><strong>Conteúdo</strong><span>Manifesto, hashes e assets permitidos.</span></div>
          <div class="alpha11-step"><b>05</b><strong>Otimização</strong><span>LOD, rede adaptativa e menor custo no OMSI.</span></div>
        </div>

        <div class="actions" style="margin-top:22px">
          <a class="button secondary" href="${alpha11Test.clientZipUrl}" target="_blank" rel="noreferrer">Cliente ZIP</a>
          <a class="button secondary" href="${alpha11Test.serverZipUrl}" target="_blank" rel="noreferrer">Servidor x64</a>
          <a class="button secondary" href="${alpha11Test.pluginZipUrl}" target="_blank" rel="noreferrer">Plugin experimental</a>
        </div>
      </div>`;

    reference.insertAdjacentElement('afterend', section);

    const nav = document.querySelector('.topbar nav');
    if (nav && !nav.querySelector('a[href="#alpha11"]')) {
      const link = document.createElement('a');
      link.href = '#alpha11';
      link.textContent = 'Alpha.11 Test';
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
    const trustCells = document.querySelectorAll('.trust-grid > div');
    const testCell = trustCells[1];
    const testVersion = testCell?.querySelector('b');
    const testLabel = testCell?.querySelector('span');
    if (testVersion) testVersion.textContent = 'alpha.11-test.1';
    if (testLabel) testLabel.textContent = 'teste público atual';

    const pluginNav = document.querySelector('.topbar nav a[href="#plugin"]');
    if (pluginNav) pluginNav.textContent = 'Plugin';

    const heroKicker = document.querySelector('.hero-kicker');
    if (heroKicker) heroKicker.innerHTML = '<span class="live-dot"></span> Alpha.11 Test 1 disponível para teste';

    const consoleRows = [...document.querySelectorAll('.project-console .console-row.exp')];
    const bridgeValue = consoleRows[1]?.querySelector('strong');
    if (bridgeValue) bridgeValue.textContent = 'ALPHA.11 TEST 1';

    const experimentalStatus = document.querySelector('.status-card.status-exp');
    if (experimentalStatus) {
      experimentalStatus.innerHTML = `
        <span class="status-label">ALPHA.11 TEST 1</span>
        <h3>Integração aprofundada</h3>
        <p>Bridge v2, identidade do veículo, pose 3D nativa, paradas, rota completa e canal inicial de tráfego AI com autoridade do host.</p>
        <ul><li>Plugin Native AOT x86</li><li>Telemetria 3D exata</li><li>Snapshots de tráfego do host</li><li>Spawn físico ainda experimental</li></ul>`;
    }
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
