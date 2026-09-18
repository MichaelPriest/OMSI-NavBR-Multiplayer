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

export interface NavBrSessionPoint {
  playerId: string;
  displayName: string;
  kind: "bus" | "roleplay";
  x: number;
  y: number;
  headingDegrees: number;
  speedKph: number;
  line?: string | null;
  isLocal: boolean;
  activity?: string | null;
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
  roomIsPrivate: boolean;
  inviteAddresses: string[];
  latencyMs?: number | null;
  voiceEnabled: boolean;
  voiceChannel: string;
  voiceProximityMeters: number;
  voiceDeafened: boolean;
  roleplayEnabled: boolean;
  localRoleplayActive: boolean;
  selectedRoleplayCharacter?: string | null;
  playerCount: number;
  players: NavBrPlayer[];
  sessionPoints: NavBrSessionPoint[];
  chat: NavBrChatMessage[];
}

export interface NavBrPublicRoom {
  roomId: string;
  playerCount: number;
  mapName?: string | null;
  mapCompatibilityId?: string | null;
  updatedAtUtc: string;
  omsiVersion?: string | null;
  navbrVersion?: string | null;
  vehiclePath?: string | null;
  vehicleCompatibilityId?: string | null;
  hofName?: string | null;
  hofCompatibilityId?: string | null;
  pluginProtocolVersion: number;
  favorite: boolean;
  compatibility: "compatible" | "warning" | "blocked";
  compatibilityIssues: string[];
  directJoinAllowed: boolean;
}

export interface NavBrRoomDirectory {
  serverUrl?: string | null;
  error?: string | null;
  rooms: NavBrPublicRoom[];
}

export interface NavBrNavigationPoint {
  x: number;
  y: number;
}

export interface NavBrNavigationStop {
  name: string;
  x: number;
  y: number;
  isNext: boolean;
}

export interface NavBrNavigationVehicle {
  x: number;
  y: number;
  headingDegrees: number;
  speedKph: number;
}

export interface NavBrNavigationState {
  available: boolean;
  mapName?: string | null;
  mapFolder?: string | null;
  line?: string | null;
  route?: string | null;
  destinationName?: string | null;
  nextStopName?: string | null;
  currentStreetName?: string | null;
  currentStopIndex?: number | null;
  isOnRoute: boolean;
  offRouteDistanceMeters: number;
  routeProgressPercent: number;
  distanceRemainingMeters: number;
  distanceToNextStopMeters?: number | null;
  maneuver: string;
  distanceToManeuverMeters?: number | null;
  etaToNextStopSeconds?: number | null;
  etaToRouteEndSeconds?: number | null;
  paceMetersPerSecond?: number | null;
  usesWorldCoordinates: boolean;
  tileSize?: number | null;
  routePoints: NavBrNavigationPoint[];
  stopPoints: NavBrNavigationStop[];
  vehicle?: NavBrNavigationVehicle | null;
  stopSequence: {
    routeResolved: boolean;
    totalStops: number;
    nextStopIndex?: number | null;
    upcomingStops: string[];
  };
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
  navigation: NavBrNavigationState;
  multiplayer: NavBrMultiplayerState;
  roomDirectory: NavBrRoomDirectory;
}

export type NavBrCommand =
  | "launchOmsi"
  | "refreshState"
  | "openMultiplayerCentral"
  | "openRoleplay"
  | "openNavigation3D"
  | "toggleHudLayout"
  | "openHudEditor"
  | "connectRoom"
  | "createLocalRoom"
  | "disconnectRoom"
  | "stopLocalHost"
  | "refreshPublicRooms"
  | "toggleRoomFavorite"
  | "setVoiceEnabled"
  | "configureVoice"
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
