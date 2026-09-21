export const REPO = "MichaelPriest/OMSI-NavBR-Multiplayer";
export const CURRENT_TAG = "v0.3.0-alpha.19";
export const GITHUB_URL = `https://github.com/${REPO}`;
export const RELEASES_PAGE = `${GITHUB_URL}/releases`;

export function formatBytes(bytes = 0) {
  if (!bytes) return "";
  const units = ["B", "KB", "MB", "GB"];
  let value = Number(bytes) || 0;
  let index = 0;
  while (value >= 1024 && index < units.length - 1) {
    value /= 1024;
    index += 1;
  }
  return `${value.toFixed(index > 1 ? 1 : 0)} ${units[index]}`;
}

export function formatNumber(value = 0) {
  return new Intl.NumberFormat("pt-BR").format(Number(value) || 0);
}

export function formatDate(value) {
  if (!value) return "aguardando publicação";
  return new Intl.DateTimeFormat("pt-BR", {
    day: "2-digit",
    month: "long",
    year: "numeric"
  }).format(new Date(value));
}

export function summarizeBody(body = "") {
  return String(body)
    .replace(/#+\s*/g, "")
    .replace(/!\[(.*?)\]\(.*?\)/g, "")
    .replace(/\[(.*?)\]\(.*?\)/g, "$1")
    .replace(/\*+/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

export function alphaKey(tag = "") {
  const match = String(tag).match(/alpha\.(\d+)/i);
  return match ? `alpha.${match[1]}`.toLowerCase() : null;
}

export function alphaNumber(key = "") {
  const match = String(key).match(/alpha\.(\d+)/i);
  return match ? Number(match[1]) : -1;
}

export function alphaLabel(tagOrKey = "") {
  const match = String(tagOrKey).match(/alpha\.(\d+)/i);
  return match ? `Alpha.${match[1]}` : "Alpha";
}

export function releaseDownloadCount(release) {
  if (Number.isFinite(Number(release?.download_count))) return Number(release.download_count);
  return (release?.assets || [])
    .filter(asset => /\.(exe|zip|apk)$/i.test(asset.name || ""))
    .reduce((total, asset) => total + (Number(asset.download_count) || 0), 0);
}

export function calculateAllAlphaDownloads(releases) {
  return releases.reduce((result, release) => {
    const key = alphaKey(release?.tag_name);
    if (!key) return result;
    result[key] = (Number(result[key]) || 0) + releaseDownloadCount(release);
    return result;
  }, {});
}

export function assetLabel(name = "") {
  if (/Setup-win-x86\.exe$/i.test(name)) return "Instalador Windows — recomendado";
  if (/win-x86\.exe$/i.test(name)) return "Cliente standalone — alternativa";
  if (/NavBR-Multiplayer.*win-x86\.zip$/i.test(name)) return "Cliente ZIP — alternativa";
  if (/NavBR-Server.*win-x64\.zip$/i.test(name)) return "Servidor dedicado — opcional";
  if (/NavBR-Plugin.*win-x86\.zip$/i.test(name)) return "Plugin OMSI x86";
  if (/NavBR-Mobile.*\.apk$/i.test(name)) return "Mobile Companion Android APK";
  if (/NavBR-Multiplayer-Simulator.*win-x64.*\.zip$/i.test(name)) return "Simulador Multiplayer — dev/test";
  return name;
}

export function assetHelp(name = "") {
  if (/Setup-win-x86\.exe$/i.test(name)) return "Instala o NavBR, inclui desinstalador e leva o pacote necessário para os testes.";
  if (/win-x86\.exe$/i.test(name)) return "Cliente standalone sem assistente de instalação.";
  if (/NavBR-Multiplayer.*win-x86\.zip$/i.test(name)) return "Mesmo cliente em pacote ZIP.";
  if (/NavBR-Server.*win-x64\.zip$/i.test(name)) return "Servidor dedicado opcional.";
  if (/NavBR-Plugin.*win-x86\.zip$/i.test(name)) return "Plugin Native AOT x86 + interop OMSI.";
  if (/NavBR-Mobile.*\.apk$/i.test(name)) return "APK Android do Mobile Companion Alpha 2.";
  if (/NavBR-Multiplayer-Simulator.*win-x64.*\.zip$/i.test(name)) return "Ferramenta exclusiva de desenvolvimento/teste.";
  return "";
}

export const features = [
  ["React + WebView2", "Shell principal desktop em React consumindo estado real do backend C#."],
  ["GPS / Roadmap 2D/3D", "Mapa, rota, paradas, manobras e visão 3D integrados ao React."],
  ["Central Multiplayer", "Salas públicas/privadas, jogadores, latência, chat, voz e RP reais."],
  ["HUD configurável", "Presets, módulos e escala aplicados ao vivo pelo store nativo."],
  ["Roadmap Studio", "Análise por tiles e geração vetorial usando os serviços C# existentes."],
  ["Ghost / Replay", "Gravação, biblioteca, analytics, prévia e replay físico pelo Plugin Bridge."],
  ["CCO + Empresa", "Operação, ocorrências, motoristas remotos, empresa, frota e perfil."],
  ["Instalações OMSI", "Perfis reais, seletor nativo de pasta e inicialização do OMSI."],
  ["Rede verificável", "Firewall, listener, NAT/CGNAT, UPnP e probe externo separados."],
  ["Personagem / RP", "Map.Drivers real, sair/retornar ao ônibus e movimento pelo bridge v3."],
  ["Hardware Cockpit", "Serial compartilhada e telemetria NAVBR_HW_V1 a 5 Hz."],
  ["Ônibus remoto físico", "Spawn/update/despawn experimental com opt-in e compatibilidade."]
];
