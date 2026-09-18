export interface NavBrPlayer {
  playerId: string;
  displayName: string;
  roomId: string;
  mapName?: string | null;
  voiceEnabled?: boolean | null;
  latencyMs?: number | null;
  roleplayActive: boolean;
  speaking: boolean;
}

export interface NavBrChatMessage {
  playerId: string;
  displayName: string;
  text: string;
  timestampUtc: string;
  isSystem: boolean;
}

export interface NavBrMultiplayerState {
  available: boolean;
  connected: boolean;
  connectionState: string;
  serverUrl: string;
  roomId: string;
  displayName: string;
  hostRunning: boolean;
  hostPort?: number | null;
  inviteAddresses: string[];
  latencyMs?: number | null;
  voiceEnabled: boolean;
  voiceChannel: string;
  roleplayEnabled: boolean;
  localRoleplayActive: boolean;
  selectedRoleplayCharacter?: string | null;
  playerCount: number;
  players: NavBrPlayer[];
  chat: NavBrChatMessage[];
}

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
    line?: string | null;
    route?: string | null;
    destinationName?: string | null;
    nextStopName?: string | null;
    x: number;
    y: number;
    z: number;
    headingDegrees: number;
    speedKph: number;
  };
  multiplayer: NavBrMultiplayerState;
}

export type NavBrCommand =
  | "launchOmsi"
  | "refreshState"
  | "openMultiplayerCentral"
  | "openRoleplay"
  | "toggleHudLayout"
  | "openHudEditor"
  | "sendChat";

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

export function sendCommand(command: NavBrCommand, payload?: Record<string, unknown>) {
  window.chrome?.webview?.postMessage({ command, payload });
}

export function subscribeToNavBrState(
  callback: (state: NavBrState) => void,
  onError?: (message: string) => void
) {
  const listener = (event: MessageEvent) => {
    if (event.data?.type === "navbr-state" && event.data.payload) {
      callback(event.data.payload as NavBrState);
      return;
    }

    if (event.data?.type === "navbr-command-error" && event.data.message) {
      onError?.(String(event.data.message));
    }
  };

  window.chrome?.webview?.addEventListener("message", listener);
  sendCommand("refreshState");

  return () => {
    window.chrome?.webview?.removeEventListener("message", listener);
  };
}
