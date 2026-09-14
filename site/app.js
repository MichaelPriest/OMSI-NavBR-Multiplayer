const repo = 'MichaelPriest/OMSI-NavBR-Multiplayer';
const fallbackRelease = {
  tag_name: 'v0.3.0-alpha.5',
  name: 'OMSI NavBR Multiplayer v0.3.0-alpha.5',
  prerelease: true,
  published_at: '2026-09-14T20:31:18Z',
  html_url: `https://github.com/${repo}/releases/tag/v0.3.0-alpha.5`,
  body: 'HUD focado no OMSI, atalhos protegidos contra conflitos e melhorias no multiplayer peer-host.',
  assets: [
    {
      name: 'OMSI-NavBR-Multiplayer-v0.3.0-alpha.5-win-x86.exe',
      browser_download_url: `https://github.com/${repo}/releases/download/v0.3.0-alpha.5/OMSI-NavBR-Multiplayer-v0.3.0-alpha.5-win-x86.exe`,
      size: 82231482
    },
    {
      name: 'OMSI-NavBR-Multiplayer-v0.3.0-alpha.5-win-x86.zip',
      browser_download_url: `https://github.com/${repo}/releases/download/v0.3.0-alpha.5/OMSI-NavBR-Multiplayer-v0.3.0-alpha.5-win-x86.zip`,
      size: 83078042
    },
    {
      name: 'OMSI-NavBR-Server-v0.3.0-alpha.5-win-x64.zip',
      browser_download_url: `https://github.com/${repo}/releases/download/v0.3.0-alpha.5/OMSI-NavBR-Server-v0.3.0-alpha.5-win-x64.zip`,
      size: 50165717
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

function formatDate(date) {
  if (!date) return '';
  return new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit', month: 'long', year: 'numeric'
  }).format(new Date(date));
}

function summarizeBody(body = '') {
  const clean = body
    .replace(/#+\s*/g, '')
    .replace(/\[(.*?)\]\(.*?\)/g, '$1')
    .replace(/\*+/g, '')
    .replace(/\s+/g, ' ')
    .trim();
  return clean || 'Versão publicada para testes do projeto.';
}

function assetLabel(name = '') {
  if (/win-x86\.exe$/i.test(name)) return 'EXE standalone';
  if (/NavBR-Multiplayer.*win-x86\.zip$/i.test(name)) return 'Cliente ZIP';
  if (/NavBR-Server.*win-x64\.zip$/i.test(name)) return 'Servidor ZIP';
  return name;
}

function renderRelease(release) {
  const assets = (release.assets || []).filter(asset => /\.(exe|zip)$/i.test(asset.name || ''));
  const assetLinks = assets.map(asset => `
    <a href="${escapeHtml(asset.browser_download_url)}" target="_blank" rel="noreferrer">
      <span><b>${escapeHtml(assetLabel(asset.name))}</b><small>${escapeHtml(asset.name)}</small></span>
      <small>${escapeHtml(formatBytes(asset.size))}</small>
    </a>`).join('');
  const summary = summarizeBody(release.body);

  return `
    <article class="release-card">
      <div class="release-meta">
        <span class="tag">${release.prerelease ? 'ALPHA / TESTE' : 'ESTÁVEL'}</span>
        <small>${escapeHtml(formatDate(release.published_at))}</small>
      </div>
      <h3>${escapeHtml(release.name || release.tag_name)}</h3>
      <p>${escapeHtml(summary).slice(0, 250)}${summary.length > 250 ? '…' : ''}</p>
      <div class="asset-list">
        ${assetLinks || `<a href="${escapeHtml(release.html_url)}" target="_blank" rel="noreferrer"><span>Abrir release no GitHub</span><small>→</small></a>`}
      </div>
    </article>`;
}

async function loadReleases() {
  let releases = [];
  try {
    const response = await fetch('releases.json', { cache: 'no-store' });
    if (response.ok) releases = await response.json();
  } catch (_) {
    // O fallback mantém o site funcional mesmo se o catálogo ainda não estiver disponível.
  }

  if (!Array.isArray(releases) || releases.length === 0) releases = [fallbackRelease];

  const latest = releases[0];
  document.getElementById('latest-version').textContent = latest.tag_name || latest.name;
  document.getElementById('latest-summary').textContent = summarizeBody(latest.body).slice(0, 190);

  const standalone = (latest.assets || []).find(asset => /win-x86\.exe$/i.test(asset.name || ''));
  const latestDownload = document.getElementById('latest-download');
  latestDownload.href = standalone?.browser_download_url || latest.html_url || `https://github.com/${repo}/releases`;

  document.getElementById('release-list').innerHTML = releases.slice(0, 6).map(renderRelease).join('');
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

loadReleases();
setupPix();
