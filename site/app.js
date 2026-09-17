const repo = 'MichaelPriest/OMSI-NavBR-Multiplayer';
const fallbackTag = 'v0.3.0-alpha.13-test.1';

const fallbackRelease = {
  tag_name: fallbackTag,
  name: 'OMSI NavBR Multiplayer v0.3.0-alpha.13-test.1 — physical multiplayer test',
  prerelease: true,
  published_at: null,
  html_url: `https://github.com/${repo}/releases/tag/${fallbackTag}`,
  body: 'Alpha.13 Test 1 inicia a validação pública do ônibus remoto físico online.',
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

function formatNumber(value = 0) {
  return new Intl.NumberFormat('pt-BR').format(Number(value) || 0);
}

function formatBytes(bytes = 0) {
  if (!bytes) return '';
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = Number(bytes) || 0;
  let index = 0;
  while (value >= 1024 && index < units.length - 1) {
    value /= 1024;
    index++;
  }
  return `${value.toFixed(index > 1 ? 1 : 0)} ${units[index]}`;
}

function formatDate(value) {
  if (!value) return 'aguardando publicação';
  return new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit', month: 'long', year: 'numeric'
  }).format(new Date(value));
}

function summarizeBody(body = '') {
  return String(body)
    .replace(/#+\s*/g, '')
    .replace(/!\[(.*?)\]\(.*?\)/g, '')
    .replace(/\[(.*?)\]\(.*?\)/g, '$1')
    .replace(/\*+/g, '')
    .replace(/\s+/g, ' ')
    .trim() || 'Versão publicada para testes do projeto.';
}

function alphaKey(tag = '') {
  const match = String(tag).match(/alpha\.(\d+)/i);
  return match ? `alpha.${match[1]}` : null;
}

function alphaNumber(key = '') {
  const match = String(key).match(/alpha\.(\d+)/i);
  return match ? Number(match[1]) : -1;
}

function alphaLabel(key = '') {
  const match = String(key).match(/alpha\.(\d+)/i);
  return match ? `Alpha.${match[1]}` : 'Alpha';
}

function assetLabel(name = '') {
  if (/win-x86\.exe$/i.test(name)) return 'Cliente recomendado — EXE standalone';
  if (/NavBR-Multiplayer.*win-x86\.zip$/i.test(name)) return 'Cliente ZIP — alternativa';
  if (/NavBR-Plugin.*win-x86\.zip$/i.test(name)) return 'Plugin OMSI x86';
  if (/NavBR-Server.*win-x64\.zip$/i.test(name)) return 'Servidor dedicado — opcional';
  return name;
}

function releaseDownloadCount(release) {
  if (Number.isFinite(Number(release?.download_count))) return Number(release.download_count);
  return (release?.assets || [])
    .filter(asset => /\.(exe|zip)$/i.test(asset.name || ''))
    .reduce((sum, asset) => sum + (Number(asset.download_count) || 0), 0);
}

function renderAlphaDownloads(alphaDownloads, currentTag) {
  const container = document.getElementById('alpha-download-breakdown');
  if (!container) return;
  const currentKey = alphaKey(currentTag);
  const entries = Object.entries(alphaDownloads || {})
    .filter(([key]) => alphaNumber(key) >= 0)
    .sort((a, b) => alphaNumber(b[0]) - alphaNumber(a[0]));

  container.innerHTML = entries.length
    ? entries.map(([key, count]) => `
      <article class="alpha-download-card${key === currentKey ? ' current' : ''}">
        <span>${escapeHtml(alphaLabel(key))}</span>
        <strong>${escapeHtml(formatNumber(count))}</strong>
        <small>downloads acumulados</small>
      </article>`).join('')
    : '<p class="alpha-download-empty">Nenhuma contagem por Alpha disponível ainda.</p>';
}

function renderRelease(release) {
  const assets = (release.assets || []).filter(asset => /\.(exe|zip)$/i.test(asset.name || ''));
  const links = assets.map(asset => {
    const meta = [formatBytes(asset.size), `${formatNumber(asset.download_count)} downloads`].filter(Boolean).join(' • ');
    return `<a href="${escapeHtml(asset.browser_download_url)}" target="_blank" rel="noreferrer"><span><b>${escapeHtml(assetLabel(asset.name))}</b><small>${escapeHtml(asset.name)}</small></span><small>${escapeHtml(meta)}</small></a>`;
  }).join('');
  const summary = summarizeBody(release.body);
  return `<article class="release-card"><div class="release-meta"><span class="tag">${release.prerelease ? 'PRÉ-RELEASE' : 'RELEASE'}</span><small>${escapeHtml(formatDate(release.published_at))}</small></div><h3>${escapeHtml(release.name || release.tag_name)}</h3><div class="release-downloads">↓ ${escapeHtml(formatNumber(releaseDownloadCount(release)))} downloads desta versão</div><p>${escapeHtml(summary).slice(0, 280)}${summary.length > 280 ? '…' : ''}</p><div class="asset-list">${links || `<a href="${escapeHtml(release.html_url)}" target="_blank" rel="noreferrer"><span>Abrir release no GitHub</span><small>→</small></a>`}</div></article>`;
}

async function loadReleases() {
  let releases = [];
  let totalDownloads = 0;
  let alphaDownloads = {};

  try {
    const response = await fetch('releases.json', { cache: 'no-store' });
    if (response.ok) {
      const catalog = await response.json();
      releases = Array.isArray(catalog) ? catalog : (catalog.releases || []);
      if (!Array.isArray(catalog)) {
        totalDownloads = Number(catalog.total_downloads) || 0;
        alphaDownloads = catalog.alpha_downloads || {};
      }
    }
  } catch (_) {}

  releases = releases.filter(release => release && !release.draft);
  releases.sort((a, b) => new Date(b.published_at || 0) - new Date(a.published_at || 0));
  if (!releases.length) releases.push(fallbackRelease);

  const current = releases[0] || fallbackRelease;
  const currentAlpha = alphaKey(current.tag_name);
  const currentAlphaDownloads = Number(alphaDownloads?.[currentAlpha]) || 0;

  document.getElementById('latest-version')?.replaceChildren(document.createTextNode(current.tag_name || current.name));
  document.getElementById('latest-summary')?.replaceChildren(document.createTextNode(summarizeBody(current.body).slice(0, 220)));
  document.getElementById('total-downloads')?.replaceChildren(document.createTextNode(formatNumber(totalDownloads)));
  document.getElementById('alpha-downloads')?.replaceChildren(document.createTextNode(formatNumber(currentAlphaDownloads)));
  document.getElementById('alpha-downloads-label')?.replaceChildren(document.createTextNode(`downloads da ${alphaLabel(currentAlpha)}`));

  renderAlphaDownloads(alphaDownloads, current.tag_name);

  const standalone = (current.assets || []).find(asset => /win-x86\.exe$/i.test(asset.name || ''));
  const button = document.getElementById('latest-download');
  if (button) button.href = standalone?.browser_download_url || current.html_url;

  const list = document.getElementById('release-list');
  if (list) list.innerHTML = releases.slice(0, 8).map(renderRelease).join('');
}

loadReleases();
