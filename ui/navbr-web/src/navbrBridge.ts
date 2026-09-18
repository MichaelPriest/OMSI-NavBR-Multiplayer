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

export interface NavBrOperationalReport {
  reportId: string;
  roomId: string;
  playerId: string;
  displayName: string;
  kind: string;
  severity: string;
  status: string;
  message?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  acknowledgedByPlayerId?: string | null;
}

export interface NavBrRemoteDriver {
  playerId: string;
  displayName: string;
  roomId: string;
  mapName?: string | null;
  vehicleName?: string | null;
  line?: string | null;
  route?: string | null;
  destination?: string | null;
  nextStop?: string | null;
  speedKph: number;
  delaySeconds?: number | null;
  headingDegrees: number;
  receivedAtUtc: string;
  stale: boolean;
  latestReport?: {
    reportId: string;
    kind: string;
    severity: string;
    status: string;
    message?: string | null;
  } | null;
}

export interface NavBrFleetVehicle {
  id: string;
  fleetNumber: string;
  vehicleModel: string;
  livery?: string | null;
  addedAt?: string | null;
  lastUsedAt?: string | null;
}

export interface NavBrOperationsState {
  connected: boolean;
  roomId?: string | null;
  updatedAtUtc: string;
  canManageReports: boolean;
  localOperation?: {
    inGame: boolean;
    mapName?: string | null;
    vehicleName?: string | null;
    line?: string | null;
    route?: string | null;
    destination?: string | null;
    nextStop?: string | null;
    currentStreet?: string | null;
    speedKph: number;
    delaySeconds?: number | null;
    doors: string;
    stopRequested: boolean;
    headingDegrees: number;
  } | null;
  drivers: NavBrRemoteDriver[];
  reports: NavBrOperationalReport[];
  company: {
    name: string;
    shortName: string;
    baseMap?: string | null;
    fleet: NavBrFleetVehicle[];
  };
  profile: {
    displayName: string;
    companyName?: string | null;
    totalDrivingSeconds: number;
    totalDistanceKm: number;
    trips: number;
    highestSpeedKph: number;
    averageMovingSpeedKph: number;
    lastMap?: string | null;
    lastLine?: string | null;
    lastRoute?: string | null;
    lastDrivenAt?: string | null;
  };
}

export interface NavBrOmsiInstallation {
  id: string;
  name: string;
  installDirectory: string;
  executablePath: string;
  executableExists: boolean;
  isPreferred: boolean;
  launchArguments?: string | null;
  lastUsedAtUtc?: string | null;
  isRunning: boolean;
}

export interface NavBrSystemState {
  installations: NavBrOmsiInstallation[];
  diagnostics: {
    enabled: boolean;
    logPath: string;
    logExists: boolean;
    logSizeBytes: number;
    logUpdatedAtUtc?: string | null;
  };
}

export interface NavBrHardwareState {
  protocol: string;
  connected: boolean;
  portName?: string | null;
  baudRate: number;
  autoReconnect: boolean;
  availablePorts: string[];
  lastError?: string | null;
  lastFrameSentAtUtc?: string | null;
  payloadPreview?: string | null;
  telemetry?: {
    line?: string | null;
    route?: string | null;
    destination?: string | null;
    currentStreet?: string | null;
    nextStop?: string | null;
    currentStopIndex?: number | null;
    stopRequested: boolean;
    speedKph: number;
    delaySeconds?: number | null;
    throttlePercent?: number | null;
    brakePercent?: number | null;
    doors: string;
    lights: string;
    turnSignal: string;
    hornActive: boolean;
    wipersActive: boolean;
    parkingBrakeActive: boolean;
    reverseGear: boolean;
  } | null;
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
  operations: NavBrOperationsState;
  system: NavBrSystemState;
  hardware: NavBrHardwareState;
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
  | "acknowledgeOperationalReport"
  | "resolveOperationalReport"
  | "saveCompany"
  | "registerCurrentVehicle"
  | "removeFleetVehicle"
  | "saveDriverProfile"
  | "discoverOmsiProfiles"
  | "launchOmsiProfile"
  | "setPreferredOmsiProfile"
  | "updateOmsiProfile"
  | "removeOmsiProfile"
  | "setDiagnosticsEnabled"
  | "flushDiagnostics"
  | "purgeDiagnostics"
  | "openOmsiProfiles"
  | "connectHardware"
  | "disconnectHardware"
  | "saveHardwareSelection"
  | "showLegacyShell"
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
