(() => {
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  function addProgressBar() {
    if (document.querySelector('.site-progress')) return;

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

  function setupRevealAnimations() {
    const selectors = [
      '.section',
      '.status-card',
      '.feature-grid article',
      '.download-choice',
      '.feedback-card',
      '.roadmap-grid article',
      '.release-card',
      '#top-pix-support'
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

  function boot() {
    addProgressBar();
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
