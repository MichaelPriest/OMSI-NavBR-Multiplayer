const repo = 'MichaelPriest/OMSI-NavBR-Multiplayer';
const currentTag = 'v0.3.0-alpha.12-test.1';

const fallbackRelease = {
  tag_name: currentTag,
  name: 'OMSI NavBR Multiplayer v0.3.0-alpha.12-test.1 — community test',
  prerelease: true,
  published_at: '2026-09-16T19:06:31Z',
  html_url: `https://github.com/${repo}/releases/tag/${currentTag}`,
  body: 'Alpha.12 Test 1: novo shell/HUD, perfil e empresa virtual, CCO, peer-host, salas públicas/privadas, diagnóstico de rede, voz avançada e recursos experimentais da nova geração.',
  download_count: 0,
  assets: [
    {
      name: `OMSI-NavBR-Multiplayer-${currentTag}-win-x86.exe`,
      browser_download_url: `https://github.com/${repo}/releases/download/${currentTag}/OMSI-NavBR-Multiplayer-${currentTag}-win-x86.exe`,
      size: 85048515,
      download_count: 0
    },
    {
      name: `OMSI-NavBR-Multiplayer-${currentTag}-win-x86.zip`,
      browser_download_url: `https://github.com/${repo}/releases/download/${currentTag}/OMSI-NavBR-Multiplayer-${currentTag}-win-x86.zip`,
      size: 85380173,
      download_count: 0
    },
    {
      name: `OMSI-NavBR-Plugin-${currentTag}-win-x86.zip`,
      browser_download_url: `https://github.com/${repo}/releases/download/${currentTag}/OMSI-NavBR-Plugin-${currentTag}-win-x86.zip`,
      size: 5283624,
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
  if (/NavBR-Plugin.*win-x86\.zip$/i.test(name)) return 'Plugin OMSI x86';
  return name;
}

function assetHelp(name = '') {
  if (/win-x86\.exe$/i.test(name)) return 'Use para jogar e testar a Alpha.12. O plugin pode ser instalado/atualizado pelo próprio NavBR.';
  if (/NavBR-Multiplayer.*win-x86\.zip$/i.test(name)) return 'Mesmo cliente em pacote ZIP para uso extraído.';
  if (/NavBR-Server.*win-x64\.zip$/i.test(name)) return 'Servidor dedicado opcional. O modo padrão continua peer-host.';
  if (/NavBR-Plugin.*win-x86\.zip$/i.test(name)) return 'Pacote técnico do plugin Native AOT x86 e interop OMSI.';
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
    // O fallback abaixo mantém o portal utilizável durante deploys do catálogo.
  }

  if (!releases.some(release => release?.tag_name === currentTag)) {
    releases.push(fallbackRelease);
  }

  releases.sort((a, b) => new Date(b.published_at || 0) - new Date(a.published_at || 0));
  if (!totalDownloads) {
    totalDownloads = releases.reduce((total, release) => total + releaseDownloadCount(release), 0);
  }

  const current = releases.find(release => release?.tag_name === currentTag) || fallbackRelease;
  const versionElement = document.getElementById('latest-version');
  const summaryElement = document.getElementById('latest-summary');
  const downloadsElement = document.getElementById('total-downloads');
  const downloadButton = document.getElementById('latest-download');
  const releaseList = document.getElementById('release-list');

  if (versionElement) versionElement.textContent = current.tag_name || current.name;
  if (summaryElement) summaryElement.textContent = summarizeBody(current.body).slice(0, 220);
  if (downloadsElement) downloadsElement.textContent = formatNumber(totalDownloads);

  const standalone = (current.assets || []).find(asset => /win-x86\.exe$/i.test(asset.name || ''));
  if (downloadButton) downloadButton.href = standalone?.browser_download_url || current.html_url;
  if (releaseList) {
    releaseList.innerHTML = [current, ...releases.filter(release => release !== current)]
      .slice(0, 6)
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

loadReleases();
setupPix();