const repo = 'MichaelPriest/OMSI-NavBR-Multiplayer';
const fallbackRelease = {
  tag_name: 'v0.3.0-alpha.9',
  name: 'OMSI NavBR Multiplayer v0.3.0-alpha.9',
  prerelease: true,
  published_at: '2026-09-15T03:11:33Z',
  html_url: `https://github.com/${repo}/releases/tag/v0.3.0-alpha.9`,
  body: 'Alpha.9 com refinamento de navegação, próxima parada, multiplayer peer-host, chat e voz para testes reais.',
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
  return name;
}

function assetHelp(name = '') {
  if (/win-x86\.exe$/i.test(name)) {
    return 'Use para jogar, entrar em salas ou criar uma sala no próprio PC. Não precisa baixar o servidor.';
  }
  if (/NavBR-Multiplayer.*win-x86\.zip$/i.test(name)) {
    return 'Mesmo cliente em pacote ZIP. É uma alternativa ao EXE standalone; não é necessário baixar os dois.';
  }
  if (/NavBR-Server.*win-x64\.zip$/i.test(name)) {
    return 'Somente para servidor dedicado em outra máquina/processo. Não é necessário para criar sala pelo cliente NavBR; extraia e mantenha todos os arquivos do ZIP.';
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
    <article class="release-card">
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
  if (!totalDownloads) totalDownloads = releases.reduce((total, release) => total + releaseDownloadCount(release), 0);

  const latest = releases[0];
  document.getElementById('latest-version').textContent = latest.tag_name || latest.name;
  document.getElementById('latest-summary').textContent = summarizeBody(latest.body).slice(0, 190);

  const totalDownloadsElement = document.getElementById('total-downloads');
  if (totalDownloadsElement) totalDownloadsElement.textContent = formatNumber(totalDownloads);

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
