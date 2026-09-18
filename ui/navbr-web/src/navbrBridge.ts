export interface NavBrState {
  generatedAtUtc?: string;
  appVersion?: string | null;
  omsi: {
    running: boolean;
    processId?: number | null;
    version?: string | null;
    installDirectory?: string | null;
    compatible: boolean;
  };
  telemetry: null | {
    inGame: boolean;
    mapName?: string | null;
    x: number;
    y: number;
    z: number;
    headingDegrees: number;
    speedKph: number;
  };
}

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: unknown) => void;
        addEventListener: (
          type: "message",
          listener: (event: MessageEvent) => void
        ) => void;
        removeEventListener: (
          type: "message",
          listener: (event: MessageEvent) => void
        ) => void;
      };
    };
  }
}

export function sendCommand(command: "launchOmsi" | "refreshState") {
  window.chrome?.webview?.postMessage({ command });
}

export function subscribeToNavBrState(callback: (state: NavBrState) => void) {
  const listener = (event: MessageEvent) => {
    if (event.data?.type === "navbr-state" && event.data.payload) {
      callback(event.data.payload as NavBrState);
    }
  };

  window.chrome?.webview?.addEventListener("message", listener);
  sendCommand("refreshState");

  return () => window.chrome?.webview?.removeEventListener("message", listener);
}
