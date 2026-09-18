const $ = (id) => document.getElementById(id);

const formatNumber = (value, digits = 1) =>
  typeof value === "number" && Number.isFinite(value)
    ? value.toFixed(digits)
    : "—";

function send(command) {
  if (window.chrome?.webview) {
    window.chrome.webview.postMessage({ command });
  }
}

function render(state) {
  const omsi = state?.omsi ?? {};
  const telemetry = state?.telemetry ?? null;

  $("omsiState").textContent = omsi.running ? "Em execução" : "Não detectado";
  $("omsiVersion").textContent = omsi.version ? `Versão ${omsi.version}` : "Versão —";

  $("telemetryState").textContent = telemetry
    ? (telemetry.inGame ? "Conectada" : "OMSI sem viagem ativa")
    : "Aguardando";
  $("mapValue").textContent = telemetry?.mapName || "—";
  $("positionValue").textContent = telemetry
    ? `X ${formatNumber(telemetry.x, 2)} · Y ${formatNumber(telemetry.y, 2)}`
    : "Posição indisponível";
  $("headingValue").textContent = telemetry
    ? `Direção ${formatNumber(telemetry.headingDegrees, 1)}°`
    : "Direção —";
  $("speedValue").textContent = telemetry ? formatNumber(telemetry.speedKph, 0) : "--";

  $("tableMap").textContent = telemetry?.mapName || "—";
  $("tableX").textContent = formatNumber(telemetry?.x, 2);
  $("tableY").textContent = formatNumber(telemetry?.y, 2);
  $("tableZ").textContent = formatNumber(telemetry?.z, 2);

  const active = Boolean(omsi.running && telemetry?.inGame);
  $("operationBadge").textContent = active ? "Operação ativa" : (omsi.running ? "OMSI detectado" : "Aguardando OMSI");
  $("operationBadge").classList.toggle("online", Boolean(omsi.running));
  $("operationTitle").textContent = active
    ? (telemetry.mapName || "Viagem em andamento")
    : (omsi.running ? "OMSI está aberto" : "Nenhuma operação ativa");
  $("operationSubtitle").textContent = active
    ? "Telemetria recebida diretamente do cliente NavBR."
    : (omsi.running
      ? "Aguardando o OMSI entrar em uma viagem com telemetria disponível."
      : "Abra o OMSI para iniciar a telemetria e carregar os dados reais da viagem.");

  const launchText = omsi.running ? "OMSI aberto" : "Executar OMSI";
  $("launchButton").textContent = launchText;
  $("heroLaunchButton").textContent = launchText;
  $("launchButton").disabled = Boolean(omsi.running);
  $("heroLaunchButton").disabled = Boolean(omsi.running);
}

["launchButton", "heroLaunchButton"].forEach((id) => {
  $(id).addEventListener("click", () => send("launchOmsi"));
});
$("refreshButton").addEventListener("click", () => send("refreshState"));

if (window.chrome?.webview) {
  window.chrome.webview.addEventListener("message", (event) => {
    if (event.data?.type === "navbr-state") {
      render(event.data.payload);
    }
  });
  send("refreshState");
}
