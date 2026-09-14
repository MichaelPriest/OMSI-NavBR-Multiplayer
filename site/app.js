const repo = 'MichaelPriest/OMSI-NavBR-Multiplayer';
const fallbackRelease = {
  tag_name: 'v0.2.0-alpha.3',
  name: 'OMSI NavBR Multiplayer v0.2.0-alpha.3',
  prerelease: true,
  published_at: '2026-09-14T14:49:23Z',
  html_url: `https://github.com/${repo}/releases/tag/v0.2.0-alpha.3`,
  body: 'EXE standalone, correção do ícone do aplicativo e melhorias no processo de release.',
  assets: [
    {
      name: 'OMSI-NavBR-Multiplayer-v0.2.0-alpha.3-win-x86.exe',
      browser_download_url: `https://github.com/${repo}/releases/download/v0.2.0-alpha.3/OMSI-NavBR-Multiplayer-v0.2.0-alpha.3-win-x86.exe`,
      size: 60791581
    },
    {
      name: 'OMSI-NavBR-Multiplayer-v0.2.0-alpha.3-win-x86.zip',
      browser_download_url: `https://github.com/${repo}/releases/download/v0.2.0-alpha.3/OMSI-NavBR-Multiplayer-v0.2.0-alpha.3-win-x86.zip`,
      size: 60829486
    },
    {
      name: 'OMSI-NavBR-Server-v0.2.0-alpha.3-win-x64.zip',
      browser_download_url: `https://github.com/${repo}/releases/download/v0.2.0-alpha.3/OMSI-NavBR-Server-v0.2.0-alpha.3-win-x64.zip`,
      size: 50149057
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

function renderRelease(release) {
  const assets = (release.assets || []).filter(asset =>
    /\.(exe|zip)$/i.test(asset.name || '')
  );

  const assetLinks = assets.map(asset => `
    <a href="${escapeHtml(asset.browser_download_url)}" target="_blank" rel="noreferrer">
      <span>${escapeHtml(asset.name)}</span>
      <small>${escapeHtml(formatBytes(asset.size))}</small>
    </a>`).join('');

  return `
    <article class="release-card">
      <div class="release-meta">
        <span class="tag">${release.prerelease ? 'ALPHA / TESTE' : 'ESTÁVEL'}</span>
        <small>${escapeHtml(formatDate(release.published_at))}</small>
      </div>
      <h3>${escapeHtml(release.name || release.tag_name)}</h3>
      <p>${escapeHtml(summarizeBody(release.body)).slice(0, 280)}${summarizeBody(release.body).length > 280 ? '…' : ''}</p>
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
    // fallback abaixo mantém o site utilizável mesmo sem o JSON gerado pelo deploy.
  }

  if (!Array.isArray(releases) || releases.length === 0) {
    releases = [fallbackRelease];
  }

  const latest = releases[0];
  document.getElementById('latest-version').textContent = latest.tag_name || latest.name;
  document.getElementById('latest-summary').textContent = summarizeBody(latest.body).slice(0, 180);

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
