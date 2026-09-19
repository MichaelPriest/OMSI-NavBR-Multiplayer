export interface NavBrPlayer {
  playerId: string;
  displayName: string;
  roomId: string;
  mapName?: string | null;
  voiceEnabled?: boolean | null;
  latencyMs?: number | null;
  roleplayActive: boolean;
  physicalVehicleSpawned: boolean;
  physicalVehicleState?: string | null;
  physicalVehicleErrorCode?: string | null;
  physicalVehiclePartCount?: number | null;
  physicalVehicleExpectedPartCount?: number | null;
  physicalVehicleUpdatedAtUtc?: string | null;
  speaking: boolean;
  isLocal: boolean;
  line?: string | null;
  route?: string | null;
  destinationName?: string | null;
  nextStopName?: string | null;
  vehicleName?: string | null;
  speedKph?: number | null;
  delaySeconds?: number | null;
  telemetryAgeSeconds?: number | null;
  telemetryStale: boolean;
  distanceText?: string | null;
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
  hostReachability: "inactive" | "checking" | "lan-only" | "upnp-mapped-unverified" | "internet-address-available";
  internetInviteAddress?: string | null;
  upnpMapped: boolean;
  upnpMessage?: string | null;
  externalProbeConfigured: boolean;
  roomIsPrivate: boolean;
  inviteAddresses: string[];
  latencyMs?: number | null;
  voiceEnabled: boolean;
  voiceChannel: string;
  voiceProximityMeters: number;
  voiceDeafened: boolean;
  voiceInputDeviceNumber: number;
  voiceOutputDeviceNumber: number;
  voiceInputDevices: { deviceNumber: number; displayName: string }[];
  voiceOutputDevices: { deviceNumber: number; displayName: string }[];
  voiceMixers: {
    playerId: string;
    displayName: string;
    muted: boolean;
    gain: number;
    speaking: boolean;
  }[];
  voicePushToTalkActive: boolean;
  voiceQuality: {
    activeStreams: number;
    receivedPackets: number;
    playedPackets: number;
    fecRecoveredPackets: number;
    estimatedLostPackets: number;
    latePackets: number;
    duplicatePackets: number;
    averageJitterMilliseconds: number;
    targetBufferMilliseconds: number;
    estimatedLossPercent: number;
  };
  chatHotkey: string;
  voiceHotkey: string;
  hotkeyOptions: string[];
  relayEnabled: boolean;
  relayServerUrl: string;
  physicalVehiclesEnabled: boolean;
  physicalVehiclesAvailable: boolean;
  networkQuality: {
    level: string;
    roundTripMs?: number | null;
    jitterMs?: number | null;
    lossPercent: number;
    samples: number;
    updatedAtUtc: string;
  };
  sessionAuthority: {
    roomOwnerPlayerId?: string | null;
    roomOwnerDisplayName?: string | null;
    trafficAuthorityPlayerId?: string | null;
    trafficAuthorityDisplayName?: string | null;
    isRoomOwner: boolean;
    isTrafficAuthority: boolean;
  };
  transportMode: "none" | "direct-host" | "remote-host" | "relay" | "dedicated-server";
  roomCompatibility: {
    level: "none" | "waiting" | "compatible" | "partial" | "warning" | "blocked";
    remoteCount: number;
    blocking: number;
    warnings: number;
    partial: number;
    affectedAreas: string[];
  };
  sessionOperationalState?: {
    authorityPlayerId: string;
    sequence: number;
    serverTimestampUtc: string;
    mapName?: string | null;
    mapCompatibilityId?: string | null;
    line?: string | null;
    route?: string | null;
    destinationName?: string | null;
    nextStopName?: string | null;
  } | null;
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
  roadmapAvailable: boolean;
  roadmapUrl?: string | null;
  roadmapFallbackUrl?: string | null;
  bounds?: {
    minX: number;
    minY: number;
    maxX: number;
    maxY: number;
  } | null;
  routePoints: NavBrNavigationPoint[];
  routeDiagnostic?: {
    mode: string;
    trackName?: string | null;
    line?: string | null;
    lookupValue?: string | null;
    entryCount: number;
    pointCount: number;
  } | null;
  rejoinAvailable: boolean;
  rejoinDistanceMeters?: number | null;
  rejoinPoints: NavBrNavigationPoint[];
  rejoinPoint?: NavBrNavigationPoint | null;
  stopPoints: NavBrNavigationStop[];
  vehicle?: NavBrNavigationVehicle | null;
  stopSequence: {
    routeResolved: boolean;
    totalStops: number;
    nextStopIndex?: number | null;
    upcomingStops: string[];
  };
}

export interface NavBrNavigation3DVehicle {
  x: number;
  y: number;
  headingDegrees: number;
  speedKph: number;
  line?: string | null;
}

export interface NavBrNavigation3DRemoteVehicle extends NavBrNavigation3DVehicle {
  playerId: string;
  displayName: string;
}

export interface NavBrNavigation3DRoleplayCharacter {
  x: number;
  y: number;
  z: number;
  headingDegrees: number;
  speedMps: number;
  activity: string;
  characterName?: string | null;
}

export interface NavBrNavigation3DRemoteRoleplayCharacter extends NavBrNavigation3DRoleplayCharacter {
  playerId: string;
  displayName: string;
}

export interface NavBrNavigation3DState {
  available: boolean;
  mapName?: string | null;
  mapFolder?: string | null;
  roadmapAvailable: boolean;
  roadmapUrl?: string | null;
  roadmapFallbackUrl?: string | null;
  bounds?: {
    minX: number;
    minY: number;
    maxX: number;
    maxY: number;
  } | null;
  routePoints: NavBrNavigationPoint[];
  localVehicle?: NavBrNavigation3DVehicle | null;
  localRoleplayCharacter?: NavBrNavigation3DRoleplayCharacter | null;
  remoteVehicles: NavBrNavigation3DRemoteVehicle[];
  remoteRoleplayCharacters: NavBrNavigation3DRemoteRoleplayCharacter[];
  routeAvailable: boolean;
  remoteCount: number;
  remoteRoleplayCount: number;
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
  tripHistory: {
    startedAtUtc: string;
    endedAtUtc: string;
    drivingSeconds: number;
    distanceKm: number;
    highestSpeedKph: number;
    mapName?: string | null;
    line?: string | null;
    route?: string | null;
    vehicleName?: string | null;
  }[];
  profileTransfer: {
    notice?: string | null;
    pending?: {
      displayName: string;
      companyName?: string | null;
      includesTripHistory: boolean;
      tripCount: number;
      sourceVersion: number;
    } | null;
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

export interface NavBrHudPreset {
  id: string;
  displayName: string;
  themeId: string;
  inspiration: string;
  description: string;
  width: number;
  scale: number;
  opacity: number;
  showFuel: boolean;
  showPedals: boolean;
  showStatus: boolean;
  showMinimap: boolean;
  showMultiplayer: boolean;
  showAlerts: boolean;
  showSideIndicators: boolean;
}

export interface NavBrHudState {
  enabled: boolean;
  preset: string;
  theme: string;
  anchor: string;
  scale: number;
  width: number;
  height: number;
  opacity: number;
  autoScale: boolean;
  showFuel: boolean;
  showPedals: boolean;
  showStatus: boolean;
  showMinimap: boolean;
  showMultiplayer: boolean;
  showAlerts: boolean;
  showSideIndicators: boolean;
  minimapScale: number;
  multiplayerScale: number;
  alertsScale: number;
  sideIndicatorsScale: number;
  presets: NavBrHudPreset[];
  themes: { id: string; displayName: string }[];
  anchors: { id: string; displayName: string }[];
}

export interface NavBrSystemState {
  installationsNotice?: string | null;
  pluginInstallation: {
    state: "missing" | "partial" | "outdated" | "installed" | "untracked" | "unknown" | "error";
    requiredFilesFound: number;
    requiredFilesTotal: number;
    manifestPresent: boolean;
    pluginsDirectory: string;
    omsiRoot?: string | null;
    embeddedPackageAvailable: boolean;
    installAvailable: boolean;
    installBlockReason?: "package-missing" | "omsi-not-found" | "omsi-running" | null;
    omsiRunning: boolean;
  };
  installations: NavBrOmsiInstallation[];
  hud: NavBrHudState;
  diagnostics: {
    enabled: boolean;
    logPath: string;
    logExists: boolean;
    logSizeBytes: number;
    logUpdatedAtUtc?: string | null;
  };
  sessionHealthNotice?: string | null;
  sessionHealth: {
    omsiActive: boolean;
    multiplayerConnected: boolean;
    pluginConnected: boolean;
    pluginVersion?: string | null;
    remoteDrivers: number;
    remoteTelemetryAgeSeconds?: number | null;
    latencyMs?: number | null;
    jitterMs?: number | null;
    lossPercent?: number | null;
    telemetryRateHz?: number | null;
    networkLevel: string;
    samples: number;
    updatedAtUtc: string;
  };
  legacyPreferences: {
    firstRunCompleted: boolean;
    advancedModeEnabled: boolean;
    showDrivingTips: boolean;
  };
}

export interface NavBrRoadmapStudioState {
  maps: {
    folderName: string;
    displayName: string;
    directoryPath: string;
    tileCount: number;
    compatibilityId?: string | null;
    roadmapPath?: string | null;
    roadmapExists: boolean;
  }[];
  selectedFolder?: string | null;
  busy: boolean;
  progress?: number | null;
  status?: string | null;
  error?: string | null;
  analysis?: {
    mapDirectory: string;
    outputPath: string;
    tileImageCount: number;
    minGridX: number;
    minGridY: number;
    maxGridX: number;
    maxGridY: number;
    tilePixelWidth: number;
    tilePixelHeight: number;
    outputPixelWidth: number;
    outputPixelHeight: number;
    missingTileImages: number;
    existingWholeRoadmap: boolean;
    estimatedBytes: number;
    canBuildFromTiles: boolean;
  } | null;
  result?: {
    mode: "tiles" | "vector";
    outputPath: string;
    backupPath?: string | null;
    pixelWidth: number;
    pixelHeight: number;
    elapsedSeconds: number;
    fileSizeBytes?: number | null;
    tileImagesUsed?: number | null;
    missingTileImages?: number | null;
    tileFilesRead?: number | null;
    splinesDrawn?: number | null;
  } | null;
}

export interface NavBrGhostState {
  recording: boolean;
  frameCount: number;
  playing: boolean;
  selectedPath?: string | null;
  status?: string | null;
  error?: string | null;
  ghostDirectory: string;
  libraryInvalidCount: number;
  library: {
    fileName: string;
    name: string;
    recordedAtUtc: string;
    mapName?: string | null;
    vehicleName?: string | null;
    durationSeconds: number;
    estimatedDistanceKm: number;
    averageSpeedKph: number;
    maximumSpeedKph: number;
    frameCount: number;
    selected: boolean;
  }[];
  selected?: {
    name: string;
    recordedAtUtc: string;
    mapName?: string | null;
    mapCompatibilityId?: string | null;
    vehicleName?: string | null;
    vehiclePath?: string | null;
    vehicleCompatibilityId?: string | null;
    hofName?: string | null;
    hofCompatibilityId?: string | null;
    durationSeconds: number;
    frameCount: number;
    line?: string | null;
    routePoints: {
      x: number;
      z: number;
      offsetMilliseconds: number;
    }[];
    analytics?: {
      durationSeconds: number;
      estimatedDistanceKm: number;
      averageSpeedKph: number;
      maximumSpeedKph: number;
      validSpeedSamples: number;
    } | null;
  } | null;
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

export interface NavBrRoleplayCharacter {
  id: string;
  displayName: string;
  sourceValue: string;
  isActiveDriver: boolean;
  selected: boolean;
}

export interface NavBrRoleplayState {
  enabled: boolean;
  mapReady: boolean;
  mapKey?: string | null;
  runtimeAvailable: boolean;
  active: boolean;
  terrainFollowing: boolean;
  nativeAnimation?: {
    aiMode: number;
    aiModeEx: number;
    aiSubMode: number;
    sollSpeedMps: number;
    actSpeedMps: number;
    lastMovedDistanceMeters: number;
    animationState: number;
    activityLegRaw?: number | null;
    activityArmUmbrellaRaw?: number | null;
    activityArmKiRaw?: number | null;
    activityHeadKiRaw?: number | null;
    legacyFieldsDrivenByNavBr: boolean;
  } | null;
  nativeActivityObservation?: {
    samples: number;
    movingSamples: number;
    transitionCount: number;
    movingTransitionCount: number;
    changedThisFrame: boolean;
    lastTransitionAtUtc?: string | null;
  } | null;
  busDistanceMeters?: number | null;
  enterBusRangeMeters: number;
  canEnterBus: boolean;
  interactionRuntimeAvailable: boolean;
  interactionRangeMeters: number;
  canInteractWithBus: boolean;
  interactions: { name: string }[];
  lastInteraction?: {
    name: string;
    succeeded: boolean;
    status?: string | null;
  } | null;
  status?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  selected?: {
    id: string;
    displayName: string;
    sourceValue: string;
    isActiveDriver: boolean;
  } | null;
  characters: NavBrRoleplayCharacter[];
  current?: {
    characterId?: string | null;
    characterName?: string | null;
    mapName?: string | null;
    mapCompatibilityId?: string | null;
    localX: number;
    localY: number;
    localZ: number;
    headingDegrees: number;
    speedMps: number;
    activity: string;
    isActive: boolean;
    humanIndex?: number | null;
    timestamp: string;
  } | null;
}

export interface NavBrNetworkState {
  hostPort: number;
  hostRunning: boolean;
  runningAsAdministrator: boolean;
  automaticUpnpEnabled: boolean;
  externalProbeConfigured: boolean;
  externalProbeServiceOrigin?: string | null;
  message?: string | null;
  error?: string | null;
  diagnostics?: {
    localIpv4Addresses: string[];
    localPortListening: boolean;
    firewallRulePresent: boolean;
    automaticUpnpEnabled: boolean;
    upnpGatewayFound: boolean;
    gatewayLocalAddress?: string | null;
    gatewayExternalAddress?: string | null;
    environmentKind: string;
    externalPortVerified: boolean;
    technicalNote: string;
  } | null;
  externalProbe?: {
    reachable: boolean;
    port: number;
    status: string;
    checkedAtUtc: string;
    durationMilliseconds: number;
  } | null;
}

export interface NavBrCompanyMember {
  playerId: string;
  displayName: string;
  role: string;
  permissions: string;
  joinedAtUtc: string;
  lastSeenAtUtc: string;
  isSelf: boolean;
  isOwner: boolean;
  canChangeRole: boolean;
  canRemove: boolean;
}

export interface NavBrCompanyNetworkState {
  available: boolean;
  identity?: {
    playerId: string;
    displayName: string;
    createdAtUtc: string;
  } | null;
  membership?: {
    companyId: string;
    companyName: string;
    nodeUrl: string;
    role: string;
    joinedAtUtc: string;
  } | null;
  node?: {
    running: boolean;
    port: number;
    localUrl: string;
    lanUrls: string[];
  } | null;
  company?: {
    companyId: string;
    name: string;
    shortName: string;
    ownerPlayerId: string;
    createdAtUtc: string;
    updatedAtUtc: string;
    memberCount: number;
    selfRole?: string | null;
    canInvite: boolean;
    canManageRoles: boolean;
    canRemoveMembers: boolean;
    members: NavBrCompanyMember[];
  } | null;
  assignableRoles: string[];
  invite?: {
    code: string;
    payload?: string | null;
  } | null;
}

export interface NavBrState {
  generatedAtUtc?: string;
  navigationRequest?: {
    id: number;
    screen: string;
  } | null;
  appVersion?: string | null;
  cultureName?: string | null;
  supportedLanguages: {
    cultureName: string;
    displayName: string;
  }[];
  shell: {
    topmost: boolean;
  };
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
  navigation3D: NavBrNavigation3DState;
  operations: NavBrOperationsState;
  system: NavBrSystemState;
  roadmapStudio: NavBrRoadmapStudioState;
  ghost: NavBrGhostState;
  hardware: NavBrHardwareState;
  network: NavBrNetworkState;
  companyNetwork: NavBrCompanyNetworkState;
  roleplay: NavBrRoleplayState;
  multiplayer: NavBrMultiplayerState;
  roomDirectory: NavBrRoomDirectory;
}

export type NavBrCommand =
  | "launchOmsi"
  | "refreshState"
  | "setLanguage"
  | "refreshOmsiDetection"
  | "setShellTopmost"
  | "ensureMultiplayerController"
  | "openRoleplay"
  | "setRoleplayEnabled"
  | "selectRoleplayCharacter"
  | "startRoleplay"
  | "enterRoleplayBus"
  | "triggerRoleplayVehicle"
  | "stopRoleplay"
  | "openNavigation3D"
  | "toggleHudLayout"
  | "saveHudSettings"
  | "resetHudSettings"
  | "analyzeRoadmap"
  | "buildRoadmapTiles"
  | "buildRoadmapVector"
  | "openRoadmapFolder"
  | "startGhostRecording"
  | "stopGhostRecording"
  | "cancelGhostRecording"
  | "selectGhostFile"
  | "refreshGhostLibrary"
  | "selectGhostLibraryItem"
  | "importGhostReplay"
  | "playGhost"
  | "stopGhostPlayback"
  | "openGhostFolder"
  | "connectRoom"
  | "createOnlineRoom"
  | "createLocalRoom"
  | "disconnectRoom"
  | "stopLocalHost"
  | "refreshPublicRooms"
  | "toggleRoomFavorite"
  | "setVoiceEnabled"
  | "configureVoice"
  | "configureVoiceDevices"
  | "configureRemoteVoice"
  | "configureMultiplayerHotkeys"
  | "configureRelay"
  | "setPhysicalVehiclesEnabled"
  | "submitOperationalReport"
  | "resolveMyOperationalReports"
  | "acknowledgeOperationalReport"
  | "resolveOperationalReport"
  | "saveCompany"
  | "registerCurrentVehicle"
  | "removeFleetVehicle"
  | "saveDriverProfile"
  | "exportDriverProfile"
  | "selectDriverProfileImport"
  | "applyDriverProfileImport"
  | "cancelDriverProfileImport"
  | "installOmsiPlugin"
  | "discoverOmsiProfiles"
  | "selectOmsiFolder"
  | "selectOmsiExecutable"
  | "openOmsiProfileFolder"
  | "launchOmsiProfile"
  | "setPreferredOmsiProfile"
  | "updateOmsiProfile"
  | "removeOmsiProfile"
  | "setDiagnosticsEnabled"
  | "flushDiagnostics"
  | "purgeDiagnostics"
  | "openFeedback"
  | "exportSessionHealth"
  | "saveLegacyPreferences"
  | "completeFirstRun"
  | "connectHardware"
  | "disconnectHardware"
  | "saveHardwareSelection"
  | "refreshNetworkDiagnostics"
  | "applyFirewallRule"
  | "setAutomaticUpnp"
  | "runExternalPortProbe"
  | "refreshCompanyNetwork"
  | "startCompanyNode"
  | "stopCompanyNode"
  | "createCompanyInvite"
  | "joinCompany"
  | "changeCompanyMemberRole"
  | "removeCompanyMember"
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
