const repo = 'MichaelPriest/OMSI-NavBR-Multiplayer';
const currentTag = 'v0.3.0-alpha.14-test.3';

const fallbackRelease = {
  tag_name: currentTag,
  name: 'OMSI NavBR Multiplayer v0.3.0-alpha.14-test.3 — character roleplay test',
  prerelease: true,
  published_at: null,
  html_url: `https://github.com/${repo}/releases/tag/${currentTag}`,
  body: 'Alpha.14 Test 3 valida a Central Multiplayer remodelada, Plugin/RP v3 e movimento multiplayer, preservando o ônibus remoto físico.',
  download_count: 0,
  assets: [
    {
      name: `OMSI-NavBR-Multiplayer-${currentTag}-win-x86.exe`,
      browser_download_url: `https://github.com/${repo}/releases/download/${currentTag}/OMSI-NavBR-Multiplayer-${currentTag}-win-x86.exe`,
      size: 0,
      download_count: 0
    },
    {
      name: `OMSI-NavBR-Multiplayer-${currentTag}-win-x86.zip`,
      browser_download_url: `https://github.com/${repo}/releases/download/${currentTag}/OMSI-NavBR-Multiplayer-${currentTag}-win-x86.zip`,
      size: 0,
      download_count: 0
    },
    {
      name: `OMSI-NavBR-Plugin-${currentTag}-win-x86.zip`,
      browser_download_url: `https://github.com/${repo}/releases/download/${currentTag}/OMSI-NavBR-Plugin-${currentTag}-win-x86.zip`,
      size: 0,
      download_count: 0
    },
    {
      name: `OMSI-NavBR-Server-${currentTag}-win-x64.zip`,
      browser_download_url: `https://github.com/${repo}/releases/download/${currentTag}/OMSI-NavBR-Server-${currentTag}-win-x64.zip`,
      size: 0,
      download_count: 0
    }
  ]
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
  if (!date) return 'aguardando publicação';
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

function alphaKey(tag = '') {
  const match = String(tag).match(/alpha\.(\d+)/i);
  return match ? `alpha.${match[1]}`.toLowerCase() : null;
}

function alphaNumber(key = '') {
  const match = String(key).match(/alpha\.(\d+)/i);
  return match ? Number(match[1]) : -1;
}

function alphaLabel(tagOrKey = '') {
  const match = String(tagOrKey).match(/alpha\.(\d+)/i);
  return match ? `Alpha.${match[1]}` : 'Alpha';
}

function assetLabel(name = '') {
  if (/win-x86\.exe$/i.test(name)) return 'Cliente recomendado — EXE standalone';
  if (/NavBR-Multiplayer.*win-x86\.zip$/i.test(name)) return 'Cliente ZIP — alternativa';
  if (/NavBR-Server.*win-x64\.zip$/i.test(name)) return 'Servidor dedicado — opcional';
  if (/NavBR-Plugin.*win-x86\.zip$/i.test(name)) return 'Plugin OMSI x86';
  return name;
}

function assetHelp(name = '') {
  if (/win-x86\.exe$/i.test(name)) return 'Use para jogar e testar a Alpha.14. O plugin pode ser instalado/atualizado pelo próprio NavBR.';
  if (/NavBR-Multiplayer.*win-x86\.zip$/i.test(name)) return 'Mesmo cliente em pacote ZIP para uso extraído.';
  if (/NavBR-Server.*win-x64\.zip$/i.test(name)) return 'Servidor dedicado opcional. O modo padrão continua peer-host.';
  if (/NavBR-Plugin.*win-x86\.zip$/i.test(name)) return 'Pacote técnico do plugin Native AOT x86 e interop OMSI para os testes físicos.';
  return '';
}

function releaseDownloadCount(release) {
  if (Number.isFinite(Number(release?.download_count))) return Number(release.download_count);
  return (release?.assets || [])
    .filter(asset => /\.(exe|zip)$/i.test(asset.name || ''))
    .reduce((total, asset) => total + (Number(asset.download_count) || 0), 0);
}

function calculateAlphaDownloads(releases, key) {
  if (!key) return 0;
  return releases
    .filter(release => alphaKey(release?.tag_name) === key)
    .reduce((total, release) => total + releaseDownloadCount(release), 0);
}

function calculateAllAlphaDownloads(releases) {
  return releases.reduce((result, release) => {
    const key = alphaKey(release?.tag_name);
    if (!key) return result;
    result[key] = (Number(result[key]) || 0) + releaseDownloadCount(release);
    return result;
  }, {});
}

function renderAlphaDownloadBreakdown(alphaDownloads) {
  const container = document.getElementById('alpha-download-breakdown');
  if (!container) return;

  const entries = Object.entries(alphaDownloads || {})
    .filter(([key]) => alphaNumber(key) >= 0)
    .sort((a, b) => alphaNumber(b[0]) - alphaNumber(a[0]));

  if (!entries.length) {
    container.innerHTML = '<p class="alpha-download-empty">As contagens por Alpha aparecerão após a atualização do catálogo.</p>';
    return;
  }

  container.innerHTML = entries.map(([key, count]) => `
    <article class="alpha-download-card${key === alphaKey(currentTag) ? ' current' : ''}">
      <span>${escapeHtml(alphaLabel(key))}</span>
      <strong>${escapeHtml(formatNumber(count))}</strong>
      <small>downloads acumulados</small>
    </article>`).join('');
}

function renderRelease(release) {
  const assets = (release.assets || []).filter(asset => /\.(exe|zip)$/i.test(asset.name || ''));
  const assetLinks = assets.map(asset => {
    const meta = [formatBytes(asset.size), `${formatNumber(asset.download_count)} downloads`].filter(Boolean).join(' • ');
    const help = assetHelp(asset.name);
    return `
      <a href="${escapeHtml(asset.browser_download_url)}" target="_blank" rel="noreferrer">
        <span><b>${escapeHtml(assetLabel(asset.name))}</b><small>${escapeHtml(asset.name)}</small>${help ? `<small>${escapeHtml(help)}</small>` : ''}</span>
        <small>${escapeHtml(meta)}</small>
      </a>`;
  }).join('');

  const summary = summarizeBody(release.body);
  return `
    <article class="release-card">
      <div class="release-meta">
        <span class="tag">${release.prerelease ? 'PRÉ-RELEASE' : 'RELEASE'}</span>
        <small>${escapeHtml(formatDate(release.published_at))}</small>
      </div>
      <h3>${escapeHtml(release.name || release.tag_name)}</h3>
      <div class="release-downloads">↓ ${escapeHtml(formatNumber(releaseDownloadCount(release)))} downloads desta versão</div>
      <p>${escapeHtml(summary).slice(0, 280)}${summary.length > 280 ? '…' : ''}</p>
      <div class="asset-list">${assetLinks || `<a href="${escapeHtml(release.html_url)}" target="_blank" rel="noreferrer"><span>Abrir release no GitHub</span><small>→</small></a>`}</div>
    </article>`;
}

async function loadReleases() {
  let releases = [];
  let totalDownloads = 0;
  let alphaDownloads = {};

  try {
    const response = await fetch('releases.json', { cache: 'no-store' });
    if (response.ok) {
      const catalog = await response.json();
      if (Array.isArray(catalog)) {
        releases = catalog;
      } else {
        releases = Array.isArray(catalog?.releases) ? catalog.releases : [];
        totalDownloads = Number(catalog?.total_downloads) || 0;
        alphaDownloads = catalog?.alpha_downloads && typeof catalog.alpha_downloads === 'object'
          ? catalog.alpha_downloads
          : {};
      }
    }
  } catch (_) {
    // O fallback abaixo mantém o portal utilizável durante deploys do catálogo.
  }

  if (!releases.some(release => release?.tag_name === currentTag)) {
    releases.push(fallbackRelease);
  }

  releases.sort((a, b) => new Date(b.published_at || 0) - new Date(a.published_at || 0));
  if (!totalDownloads) {
    totalDownloads = releases.reduce((total, release) => total + releaseDownloadCount(release), 0);
  }
  if (!Object.keys(alphaDownloads).length) {
    alphaDownloads = calculateAllAlphaDownloads(releases);
  }

  const currentAlphaKey = alphaKey(currentTag);
  const currentAlphaDownloads = Number(alphaDownloads?.[currentAlphaKey]) ||
    calculateAlphaDownloads(releases, currentAlphaKey);
  const current = releases.find(release => release?.tag_name === currentTag) || fallbackRelease;
  const versionElement = document.getElementById('latest-version');
  const summaryElement = document.getElementById('latest-summary');
  const downloadsElement = document.getElementById('total-downloads');
  const alphaDownloadsElement = document.getElementById('alpha-downloads');
  const alphaDownloadsLabelElement = document.getElementById('alpha-downloads-label');
  const downloadButton = document.getElementById('latest-download');
  const releaseList = document.getElementById('release-list');

  if (versionElement) versionElement.textContent = current.tag_name || current.name;
  if (summaryElement) summaryElement.textContent = summarizeBody(current.body).slice(0, 220);
  if (downloadsElement) downloadsElement.textContent = formatNumber(totalDownloads);
  if (alphaDownloadsElement) alphaDownloadsElement.textContent = formatNumber(currentAlphaDownloads);
  if (alphaDownloadsLabelElement) alphaDownloadsLabelElement.textContent = `downloads da ${alphaLabel(currentTag)}`;
  renderAlphaDownloadBreakdown(alphaDownloads);

  const standalone = (current.assets || []).find(asset => /win-x86\.exe$/i.test(asset.name || ''));
  if (downloadButton) downloadButton.href = standalone?.browser_download_url || current.html_url;
  if (releaseList) {
    releaseList.innerHTML = [current, ...releases.filter(release => release !== current)]
      .slice(0, 8)
      .map(renderRelease)
      .join('');
  }
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
      status.textContent = 'Chave copiada ✓';
      setTimeout(() => { status.textContent = ''; }, 1800);
    } catch (_) {
      window.prompt('Copie a chave Pix:', key);
    }
  });
}

function createFundingBar() {
  if (document.querySelector('.navbr-support-strip')) return;

  const strip = document.createElement('aside');
  strip.className = 'navbr-support-strip';
  strip.setAttribute('aria-label', 'Apoie o desenvolvimento do NavBR');
  strip.innerHTML = `
    <div class="navbr-support-strip-inner shell">
      <span><strong>NavBR é um projeto independente.</strong> Ajude a manter desenvolvimento, testes e infraestrutura.</span>
      <a href="#contribua">❤ Contribua</a>
    </div>`;

  const topbar = document.querySelector('.topbar');
  if (topbar) {
    topbar.before(strip);
    document.body.classList.add('navbr-support-enabled');
  } else {
    document.body.prepend(strip);
  }
}

function createAdSlot(slotName) {
  const wrapper = document.createElement('aside');
  wrapper.className = 'navbr-ad-slot shell';
  wrapper.dataset.navbrAdSlot = slotName;
  wrapper.setAttribute('aria-label', 'Publicidade');
  wrapper.innerHTML = `
    <div class="navbr-ad-label">Publicidade</div>
    <div class="navbr-ad-content">
      <strong>Espaço publicitário</strong>
      <span>Este espaço ajudará a financiar o desenvolvimento e a infraestrutura do NavBR.</span>
    </div>`;
  return wrapper;
}

function mountAdSlots() {
  if (document.querySelector('[data-navbr-ad-slot="top"]')) return;

  const trustStrip = document.querySelector('.trust-strip');
  const hardware = document.getElementById('hardware');
  const topAd = createAdSlot('top');
  const contentAd = createAdSlot('content');

  if (trustStrip) trustStrip.after(topAd);
  else document.querySelector('main')?.prepend(topAd);

  if (hardware) hardware.before(contentAd);
  else document.getElementById('contribua')?.before(contentAd);
}

function loadAdSense(config) {
  const client = config?.adsense?.client?.trim();
  if (!client || document.querySelector('script[data-navbr-adsense]')) return false;

  const script = document.createElement('script');
  script.async = true;
  script.crossOrigin = 'anonymous';
  script.dataset.navbrAdsense = 'true';
  script.src = `https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=${encodeURIComponent(client)}`;
  document.head.appendChild(script);

  const slots = config.adsense.slots || {};
  document.querySelectorAll('[data-navbr-ad-slot]').forEach(wrapper => {
    const slotName = wrapper.dataset.navbrAdSlot;
    const slotId = slots[slotName]?.trim();
    if (!slotId) return;

    const content = wrapper.querySelector('.navbr-ad-content');
    if (!content) return;
    content.innerHTML = '';

    const ad = document.createElement('ins');
    ad.className = 'adsbygoogle';
    ad.style.display = 'block';
    ad.dataset.adClient = client;
    ad.dataset.adSlot = slotId;
    ad.dataset.adFormat = 'auto';
    ad.dataset.fullWidthResponsive = 'true';
    content.appendChild(ad);

    try {
      (window.adsbygoogle = window.adsbygoogle || []).push({});
    } catch (_) {
      // O placeholder permanece estruturalmente seguro mesmo se a rede não responder.
    }
  });

  return true;
}

async function setupMonetization() {
  createFundingBar();
  mountAdSlots();

  try {
    const response = await fetch('monetization.json', { cache: 'no-store' });
    if (!response.ok) return;
    const config = await response.json();
    if (config?.enabled === true && config?.provider === 'adsense') {
      loadAdSense(config);
    }
  } catch (_) {
    // Sem configuração, o site mostra apenas os espaços publicitários reservados.
  }
}

loadReleases();
setupPix();
setupMonetization();