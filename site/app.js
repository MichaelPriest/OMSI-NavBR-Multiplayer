const repo = 'MichaelPriest/OMSI-NavBR-Multiplayer';
const fallbackRelease = {
  tag_name: 'v0.3.0-alpha.10',
  name: 'OMSI NavBR Multiplayer v0.3.0-alpha.10',
  prerelease: true,
  published_at: '2026-09-15T18:00:00Z',
  html_url: `https://github.com/${repo}/releases/tag/v0.3.0-alpha.10`,
  body: 'Alpha.10 oficial com plugin Native AOT x86, instalação pelo próprio NavBR, HUD/GPS, multiplayer peer-host, chat e voz.',
  download_count: 0,
  assets: []
};

function escapeHtml(value = '') {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function formatBytes(bytes = 0) {
  if (!bytes) return '';
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let index = 0;
  while (value >= 1024 && index < units.length - 1) {
    value /= 1024;
    index++;
  }
  return `${value.toFixed(index > 1 ? 1 : 0)} ${units[index]}`;
}

function formatNumber(value = 0) {
  return new Intl.NumberFormat('pt-BR').format(Number(value) || 0);
}

function formatDate(date) {
  if (!date) return '';
  return new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit', month: 'long', year: 'numeric'
  }).format(new Date(date));
}

function summarizeBody(body = '') {
  const clean = body
    .replace(/#+\s*/g, '')
    .replace(/!\[(.*?)\]\(.*?\)/g, '')
    .replace(/\[(.*?)\]\(.*?\)/g, '$1')
    .replace(/\*+/g, '')
    .replace(/\s+/g, ' ')
    .trim();
  return clean || 'Versão publicada para testes do projeto.';
}

function assetLabel(name = '') {
  if (/win-x86\.exe$/i.test(name)) return 'Cliente recomendado — EXE standalone';
  if (/NavBR-Multiplayer.*win-x86\.zip$/i.test(name)) return 'Cliente ZIP — alternativa';
  if (/NavBR-Server.*win-x64\.zip$/i.test(name)) return 'Servidor dedicado — opcional';
  if (/Plugin.*win-x86\.zip$/i.test(name)) return 'Plugin OMSI — diagnóstico / fallback';
  return name;
}

function assetHelp(name = '') {
  if (/win-x86\.exe$/i.test(name)) {
    return 'Use para jogar, entrar em salas ou criar uma sala no próprio PC. O plugin pode ser instalado pelo próprio NavBR.';
  }
  if (/NavBR-Multiplayer.*win-x86\.zip$/i.test(name)) {
    return 'Mesmo cliente em pacote ZIP. É uma alternativa ao EXE standalone; não é necessário baixar os dois.';
  }
  if (/NavBR-Server.*win-x64\.zip$/i.test(name)) {
    return 'Somente para servidor dedicado em outra máquina/processo. Não é necessário para criar sala pelo cliente NavBR.';
  }
  return '';
}

function releaseDownloadCount(release) {
  if (Number.isFinite(Number(release?.download_count))) return Number(release.download_count);
  return (release?.assets || [])
    .filter(asset => /\.(exe|zip)$/i.test(asset.name || ''))
    .reduce((total, asset) => total + (Number(asset.download_count) || 0), 0);
}

function renderRelease(release) {
  const assets = (release.assets || []).filter(asset => /\.(exe|zip)$/i.test(asset.name || ''));
  const assetLinks = assets.map(asset => {
    const size = formatBytes(asset.size);
    const downloads = `${formatNumber(asset.download_count)} download${Number(asset.download_count) === 1 ? '' : 's'}`;
    const meta = [size, downloads].filter(Boolean).join(' • ');
    const help = assetHelp(asset.name);
    return `
      <a href="${escapeHtml(asset.browser_download_url)}" target="_blank" rel="noreferrer">
        <span><b>${escapeHtml(assetLabel(asset.name))}</b><small>${escapeHtml(asset.name)}</small>${help ? `<small>${escapeHtml(help)}</small>` : ''}</span>
        <small>${escapeHtml(meta)}</small>
      </a>`;
  }).join('');
  const summary = summarizeBody(release.body);
  const releaseDownloads = releaseDownloadCount(release);

  return `
    <article class="release-card motion-reveal">
      <div class="release-meta">
        <span class="tag">${release.prerelease ? 'ALPHA / TESTE' : 'ESTÁVEL'}</span>
        <small>${escapeHtml(formatDate(release.published_at))}</small>
      </div>
      <h3>${escapeHtml(release.name || release.tag_name)}</h3>
      <div class="release-downloads">↓ ${escapeHtml(formatNumber(releaseDownloads))} downloads desta versão</div>
      <p>${escapeHtml(summary).slice(0, 250)}${summary.length > 250 ? '…' : ''}</p>
      <div class="asset-list">
        ${assetLinks || `<a href="${escapeHtml(release.html_url)}" target="_blank" rel="noreferrer"><span>Abrir release no GitHub</span><small>→</small></a>`}
      </div>
    </article>`;
}

async function loadReleases() {
  let releases = [];
  let totalDownloads = 0;

  try {
    const response = await fetch('releases.json', { cache: 'no-store' });
    if (response.ok) {
      const catalog = await response.json();
      if (Array.isArray(catalog)) {
        releases = catalog;
      } else {
        releases = Array.isArray(catalog?.releases) ? catalog.releases : [];
        totalDownloads = Number(catalog?.total_downloads) || 0;
      }
    }
  } catch (_) {
    // O fallback mantém o site funcional mesmo se o catálogo ainda não estiver disponível.
  }

  if (!Array.isArray(releases) || releases.length === 0) releases = [fallbackRelease];
  releases = releases
    .filter(release => !/alpha\.10-test\./i.test(release?.tag_name || ''))
    .sort((a, b) => new Date(b?.published_at || 0) - new Date(a?.published_at || 0));
  if (releases.length === 0) releases = [fallbackRelease];
  if (!totalDownloads) totalDownloads = releases.reduce((total, release) => total + releaseDownloadCount(release), 0);

  const latest = releases[0];
  const latestVersion = document.getElementById('latest-version');
  const latestSummary = document.getElementById('latest-summary');
  if (latestVersion) latestVersion.textContent = latest.tag_name || latest.name;
  if (latestSummary) latestSummary.textContent = summarizeBody(latest.body).slice(0, 190);

  const totalDownloadsElement = document.getElementById('total-downloads');
  if (totalDownloadsElement) totalDownloadsElement.textContent = formatNumber(totalDownloads);

  const standalone = (latest.assets || []).find(asset => /win-x86\.exe$/i.test(asset.name || ''));
  const latestDownload = document.getElementById('latest-download');
  if (latestDownload) latestDownload.href = standalone?.browser_download_url || latest.html_url || `https://github.com/${repo}/releases`;

  const releaseList = document.getElementById('release-list');
  if (releaseList) releaseList.innerHTML = releases.slice(0, 6).map(renderRelease).join('');

  document.querySelectorAll('.trust-grid > div').forEach((item, index) => {
    if (index === 1) {
      const strong = item.querySelector('b');
      const span = item.querySelector('span');
      if (strong) strong.textContent = 'alpha.11';
      if (span) span.textContent = 'próxima alpha em desenvolvimento';
    }
  });
  observeMotionElements();
}

function setupPix() {
  const box = document.querySelector('.pix-box');
  const button = document.getElementById('copy-pix');
  const status = document.getElementById('pix-status');
  const key = box?.dataset.pix;
  if (!box || !button || !status || !key) return;

  button.addEventListener('click', async () => {
    try {
      await navigator.clipboard.writeText(key);
      const previous = button.textContent;
      button.textContent = 'Chave copiada ✓';
      setTimeout(() => { button.textContent = previous; }, 1800);
    } catch (_) {
      status.textContent = key;
      window.prompt('Copie a chave Pix:', key);
    }
  });
}

function installModernStyles() {
  if (document.querySelector('link[data-navbr-modern]')) return;
  const link = document.createElement('link');
  link.rel = 'stylesheet';
  link.href = 'modern.css';
  link.dataset.navbrModern = 'true';
  document.head.appendChild(link);
}

function setupAlpha11Preview() {
  if (document.getElementById('alpha11-preview')) return;
  const anchor = document.getElementById('roadmap');
  if (!anchor) return;

  const section = document.createElement('section');
  section.id = 'alpha11-preview';
  section.className = 'section shell alpha11-preview motion-reveal';
  section.innerHTML = `
    <div class="alpha11-preview-head">
      <div>
        <span class="alpha11-version-pill"><i></i> ALPHA.11 • EM DESENVOLVIMENTO</span>
        <h2>A próxima fase transforma o NavBR em uma central completa do OMSI.</h2>
        <p class="lead">A alpha.11 está sendo construída sobre a alpha.10 oficial, usando OMSI Launcher, OmsiHook/Omsi-Extensions e outros projetos OMSI públicos como referência técnica. Os recursos abaixo ainda estão em desenvolvimento e entram por snapshots testáveis.</p>
      </div>
      <aside class="alpha11-preview-note"><strong>Alpha.10 continua sendo o download recomendado.</strong>O conteúdo desta seção mostra o trabalho em andamento. Recursos experimentais de escrita/spawn físico permanecem bloqueados até passarem por testes locais e CI.</aside>
    </div>
    <div class="alpha11-grid">
      <article class="alpha11-card motion-reveal"><span class="icon">▦</span><h3>Roadmap Studio</h3><p>Gera <code>whole.roadmap.bmp</code> dentro do NavBR. Pode montar tiles existentes ou desenhar uma versão vetorial diretamente das splines, sem abrir o OMSI Editor.</p><small>SEM EDITOR • PREVIEW • BACKUP</small></article>
      <article class="alpha11-card motion-reveal"><span class="icon">◉</span><h3>Painel de ônibus móvel</h3><p>Velocidade, combustível, acelerador, freio, portas, setas, luzes e outros estados em um painel independente que pode ser movido, redimensionado e desativado.</p><small>HUD MODULAR • POSIÇÃO SALVA</small></article>
      <article class="alpha11-card motion-reveal"><span class="icon">◇</span><h3>Ghost 3D</h3><p>Grava e reproduz trajetos pela mesma bridge que será usada pelo multiplayer físico. É a bancada de testes antes de colocar ônibus remotos reais na rede.</p><small>REPLAY • INTERPOLAÇÃO • BRIDGE V2</small></article>
      <article class="alpha11-card motion-reveal"><span class="icon">⇄</span><h3>Integração OMSI profunda</h3><p>Perfis de instalação, telemetria avançada, manifesto de compatibilidade e protocolo de capabilities para habilitar somente o que cada instalação suporta.</p><small>OMSIHOOK / OMSILAUNCH • FAIL-CLOSED</small></article>
    </div>
    <div class="roadmap-studio-band motion-reveal">
      <div><h3>Roadmap sem OMSI Editor</h3><p>O novo fluxo lê <code>global.cfg</code>, a grade de tiles e os <code>[spline]</code>/<code>[spline_h]</code>. Quando roadmaps por tile já existem, também consegue combiná-los automaticamente.</p></div>
      <div class="roadmap-flow"><b>global.cfg</b><span>→</span><b>tiles .map</b><span>→</span><b>splines</b><span>→</span><b>whole.roadmap.bmp</b></div>
    </div>`;
  anchor.parentNode.insertBefore(section, anchor);

  const nav = document.querySelector('.topbar nav');
  if (nav && !nav.querySelector('a[href="#alpha11-preview"]')) {
    const link = document.createElement('a');
    link.href = '#alpha11-preview';
    link.textContent = 'Alpha.11';
    const roadmapLink = nav.querySelector('a[href="#roadmap"]');
    nav.insertBefore(link, roadmapLink || nav.lastElementChild);
  }
}

let motionObserver;
function observeMotionElements() {
  const targets = document.querySelectorAll('.section, .feature-grid article, .status-card, .download-choice, .feedback-card, .roadmap-grid article, .release-card, .integration-test-card, .alpha11-card, .roadmap-studio-band');
  targets.forEach(target => target.classList.add('motion-reveal'));

  if (!('IntersectionObserver' in window) || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    targets.forEach(target => target.classList.add('is-visible'));
    return;
  }

  motionObserver ??= new IntersectionObserver(entries => {
    entries.forEach(entry => {
      if (entry.isIntersecting) {
        entry.target.classList.add('is-visible');
        motionObserver.unobserve(entry.target);
      }
    });
  }, { threshold: 0.10, rootMargin: '0px 0px -5% 0px' });

  targets.forEach(target => {
    if (!target.classList.contains('is-visible')) motionObserver.observe(target);
  });
}

function setupScrollExperience() {
  const progress = document.createElement('div');
  progress.className = 'scroll-progress';
  document.body.appendChild(progress);

  const navLinks = [...document.querySelectorAll('.topbar nav a[href^="#"]')];
  const sections = navLinks
    .map(link => ({ link, target: document.querySelector(link.getAttribute('href')) }))
    .filter(item => item.target);

  const update = () => {
    const scrollable = Math.max(1, document.documentElement.scrollHeight - window.innerHeight);
    progress.style.width = `${Math.min(100, Math.max(0, window.scrollY / scrollable * 100))}%`;

    const marker = window.scrollY + 150;
    let active = sections[0];
    for (const section of sections) {
      if (section.target.offsetTop <= marker) active = section;
    }
    navLinks.forEach(link => link.classList.remove('active'));
    active?.link.classList.add('active');
  };

  window.addEventListener('scroll', update, { passive: true });
  window.addEventListener('resize', update);
  update();
}

installModernStyles();
setupAlpha11Preview();
setupPix();
setupScrollExperience();
observeMotionElements();
loadReleases();
