using System.IO.Pipes;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NavBR.Client.Multiplayer;
using NavBR.Client.Overlay;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.OpenOmsi;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

var roleplayControllerType =
    typeof(OmsiPluginBridgeServer).Assembly.GetType(
        "NavBR.Client.Multiplayer.RoleplayCharacterController",
        throwOnError: true)!;
var cameraHeadingResolver =
    roleplayControllerType.GetMethod(
        "TryResolveCameraHeadingFromMatrices",
        BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "RP camera heading resolver not found");

static double ResolveCameraHeading(
    MethodInfo resolver,
    Vector3 target)
{
    var view = Matrix4x4.CreateLookAt(
        Vector3.Zero,
        target,
        Vector3.UnitY);
    var projection = Matrix4x4.CreatePerspectiveFieldOfView(
        MathF.PI / 3f,
        16f / 9f,
        0.1f,
        1000f);
    object?[] args =
    [
        view,
        projection,
        DateTimeOffset.UtcNow,
        0d
    ];
    Require(
        (bool)(resolver.Invoke(null, args) ?? false),
        "RP camera heading resolver rejected a valid view matrix");
    return (double)(args[3] ?? double.NaN);
}

var rpHeadingNorth =
    ResolveCameraHeading(cameraHeadingResolver, Vector3.UnitZ);
var rpHeadingEast =
    ResolveCameraHeading(cameraHeadingResolver, Vector3.UnitX);
Require(
    Math.Abs(rpHeadingNorth) < 0.01d ||
    Math.Abs(rpHeadingNorth - 360d) < 0.01d,
    $"RP camera +Z heading must be 0 degrees, got {rpHeadingNorth:F3}");
Require(
    Math.Abs(rpHeadingEast - 90d) < 0.01d,
    $"RP camera +X heading must be 90 degrees, got {rpHeadingEast:F3}");

var identityReaderType = typeof(OmsiPluginBridgeServer).Assembly.GetType(
    "NavBR.Client.Telemetry.OmsiVehicleIdentityReader",
    throwOnError: true)!;
var normalizeVehiclePath = identityReaderType.GetMethod(
    "NormalizeVehiclePath",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException("vehicle identity normalizer not found");

var splitOmsiIdentity = (string?)normalizeVehiclePath.Invoke(
    null,
    [@"C:\Program Files (x86)\OMSI 2", "MAN_EN92_main.bus", null, @"Vehicles\MAN_NL_NG"]);
Require(
    string.Equals(
        splitOmsiIdentity,
        @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
        StringComparison.OrdinalIgnoreCase),
    "split OMSI MyPath + Obj vehicle identity was not normalized");

var roadVehicleIdentity = (string?)normalizeVehiclePath.Invoke(
    null,
    [@"C:\Program Files (x86)\OMSI 2", null, "MAN_EN92_main.bus", @"Vehicles\MAN_NL_NG"]);
Require(
    string.Equals(
        roadVehicleIdentity,
        @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
        StringComparison.OrdinalIgnoreCase),
    "RoadVehicle.FileName + MyPath vehicle identity was not normalized");

var completeOmsiIdentity = (string?)normalizeVehiclePath.Invoke(
    null,
    [@"C:\Program Files (x86)\OMSI 2", null, @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus", @"Vehicles\MAN_NL_NG"]);
Require(
    string.Equals(
        completeOmsiIdentity,
        @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
        StringComparison.OrdinalIgnoreCase),
    "complete OMSI vehicle identity path regressed");

var fingerprintVehicle = identityReaderType.GetMethod(
    "TryFingerprintInstalledVehicle",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException("installed vehicle fingerprint helper not found");
var fingerprintRoot = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-fingerprint-" + Guid.NewGuid().ToString("N"));
var fingerprintRelative = Path.Combine(
    "Vehicles",
    "MAN_NL_NG",
    "MAN_EN92_main.bus");
var fingerprintPath = Path.Combine(
    fingerprintRoot,
    fingerprintRelative);
Directory.CreateDirectory(Path.GetDirectoryName(fingerprintPath)!);
var fingerprintBytes = Encoding.UTF8.GetBytes(
    "[friendlyname]\r\nNavBR openOMSI fingerprint smoke\r\n");
File.WriteAllBytes(fingerprintPath, fingerprintBytes);
try
{
    var expectedFingerprint =
        "sha256:" +
        Convert.ToHexString(
            SHA256.HashData(fingerprintBytes))
        .ToLowerInvariant();
    var actualFingerprint = (string?)fingerprintVehicle.Invoke(
        null,
        [fingerprintRoot, fingerprintRelative.Replace('\\', '/')]);
    Require(
        string.Equals(
            actualFingerprint,
            expectedFingerprint,
            StringComparison.Ordinal),
        "openOMSI installed vehicle SHA-256 fingerprint regressed");

    var escapedFingerprint = (string?)fingerprintVehicle.Invoke(
        null,
        [fingerprintRoot, @"..\outside.bus"]);
    Require(
        escapedFingerprint is null,
        "vehicle fingerprint helper accepted a path outside the content root");
}
finally
{
    Directory.Delete(fingerprintRoot, recursive: true);
}



var varTableBuilderType =
    typeof(OmsiPluginBridgeServer).Assembly.GetType(
        "NavBR.Client.Multiplayer.OpenOmsiVarTableManifestBuilder",
        throwOnError: true)!;
var tryBuildVarTable =
    varTableBuilderType.GetMethod(
        "TryBuild",
        BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "openOMSI VarTable manifest builder not found");
var engineFed =
    varTableBuilderType.GetMethod(
        "EngineFed",
        BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "openOMSI engine-fed filter not found");

Require(
    (bool)(engineFed.Invoke(null, ["door_0"]) ?? false) &&
    (bool)(engineFed.Invoke(null, ["AI_Light"]) ?? false) &&
    !(bool)(engineFed.Invoke(null, ["engine_n"]) ?? true) &&
    !(bool)(engineFed.Invoke(null, ["wiperpos"]) ?? true),
    "openOMSI engine-fed variable filter diverged");

var varTableRoot = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-vartable-" + Guid.NewGuid().ToString("N"));
var varTableBusDir = Path.Combine(varTableRoot, "Vehicles", "VarTableSmoke");
var varTableModelDir = Path.Combine(varTableBusDir, "model");
var varTableProgramDir = Path.Combine(varTableRoot, "program");
Directory.CreateDirectory(varTableBusDir);
Directory.CreateDirectory(varTableModelDir);
Directory.CreateDirectory(varTableProgramDir);
File.WriteAllText(
    Path.Combine(varTableProgramDir, "varlist_roadvehicle.txt"),
    "Velocity\nAI_Light\n");
File.WriteAllText(
    Path.Combine(varTableProgramDir, "stringvarlist_roadvehicle.txt"),
    "ident\nnumber\n");
File.WriteAllText(
    Path.Combine(varTableBusDir, "vars.txt"),
    "door_0\nengine_n\nwiperpos\nmy_custom\n");
File.WriteAllText(
    Path.Combine(varTableBusDir, "strings.txt"),
    "destination\nIBIS_line\n");
File.WriteAllText(
    Path.Combine(varTableModelDir, "Smoke.cfg"),
    "[matl_change]\nsmoke.bmp\n0\nmy_custom\n\n[visible]\nwiperpos\n");
File.WriteAllText(
    Path.Combine(varTableBusDir, "Smoke.bus"),
    "[model]\nmodel\\Smoke.cfg\n\n[varnamelist]\n1\nvars.txt\n\n[stringvarnamelist]\n1\nstrings.txt\n");

try
{
    var manifest = tryBuildVarTable.Invoke(
        null,
        [
            varTableRoot,
            @"Vehicles\VarTableSmoke\Smoke.bus"
        ]) ?? throw new InvalidOperationException(
            "openOMSI VarTable manifest was not built");

    var hashProperty = manifest.GetType().GetProperty("Hash")
        ?? throw new InvalidOperationException("VarTable Hash property missing");
    var floatNamesProperty = manifest.GetType().GetProperty("FloatNames")
        ?? throw new InvalidOperationException("VarTable FloatNames property missing");
    var stringNamesProperty = manifest.GetType().GetProperty("StringNames")
        ?? throw new InvalidOperationException("VarTable StringNames property missing");

    var actualVarHash = (uint)(hashProperty.GetValue(manifest)
        ?? throw new InvalidOperationException("VarTable hash missing"));
    var actualFloatNames = (string[])(floatNamesProperty.GetValue(manifest)
        ?? Array.Empty<string>());
    var actualStringNames = (string[])(stringNamesProperty.GetValue(manifest)
        ?? Array.Empty<string>());

    Require(
        actualFloatNames.SequenceEqual(
            new[] { "engine_n", "wiperpos", "my_custom" },
            StringComparer.OrdinalIgnoreCase),
        "VarTable float filtering/order diverged from openOMSI");
    Require(
        actualStringNames.SequenceEqual(
            new[] { "ident", "number", "destination", "IBIS_line" },
            StringComparer.OrdinalIgnoreCase),
        "VarTable string order diverged from openOMSI");

    static uint OpenOmsiVarHash(IEnumerable<string> names)
    {
        var hash = 0x811C9DC5u;
        foreach (var name in names)
        {
            foreach (var b in Encoding.UTF8
                         .GetBytes(name.ToLowerInvariant())
                         .Append((byte)0))
            {
                hash = unchecked((hash ^ b) * 0x01000193u);
            }
        }
        return hash;
    }

    var expectedVarHash = OpenOmsiVarHash(
        actualFloatNames.Concat(actualStringNames));

    Require(
        actualVarHash == expectedVarHash,
        $"VarTable FNV-1a mismatch: got {actualVarHash:X8}, expected {expectedVarHash:X8}");


    var syncTableBuilderType =
        typeof(OmsiPluginBridgeServer).Assembly.GetType(
            "NavBR.Client.Multiplayer.OpenOmsiSyncTableManifestBuilder",
            throwOnError: true)!;
    var tryBuildSyncTable =
        syncTableBuilderType.GetMethod(
            "TryBuild",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "openOMSI SyncTable manifest builder not found");
    var syncManifest =
        tryBuildSyncTable.Invoke(
            null,
            [
                varTableRoot,
                @"Vehicles\VarTableSmoke\Smoke.bus",
                manifest
            ])
        ?? throw new InvalidOperationException(
            "openOMSI SyncTable manifest was not built");

    static T ReadManifestProperty<T>(
        object instance,
        string propertyName) =>
        (T)(instance.GetType().GetProperty(propertyName)?.GetValue(instance)
            ?? throw new InvalidOperationException(
                $"SyncTable property {propertyName} missing"));

    var syncHash = ReadManifestProperty<uint>(syncManifest, "Hash");
    var syncVarTableHash =
        ReadManifestProperty<uint>(syncManifest, "VarTableHash");
    var lampIds = ReadManifestProperty<ushort[]>(syncManifest, "LampIds");
    var switchIds = ReadManifestProperty<ushort[]>(syncManifest, "SwitchIds");
    var valueIds = ReadManifestProperty<ushort[]>(syncManifest, "ValueIds");
    var doorIds = ReadManifestProperty<ushort[]>(syncManifest, "DoorIds");
    var engineNId = ReadManifestProperty<ushort?>(syncManifest, "EngineNId");
    var lampNames = ReadManifestProperty<string[]>(syncManifest, "LampNames");
    var switchNames = ReadManifestProperty<string[]>(syncManifest, "SwitchNames");

    Require(
        syncHash != 0 &&
        syncVarTableHash == actualVarHash &&
        lampIds.Length == 1 &&
        switchIds.Length == 1 &&
        doorIds.Length == 1 &&
        lampNames.SequenceEqual(
            new[] { "my_custom" },
            StringComparer.OrdinalIgnoreCase) &&
        switchNames.SequenceEqual(
            new[] { "wiperpos" },
            StringComparer.OrdinalIgnoreCase),
        "openOMSI visual SyncTable manifest did not preserve lamp/switch/door layout");

    var sampledValues = new Dictionary<ushort, float>();
    foreach (var id in lampIds)
    {
        sampledValues[id] = 1f;
    }
    foreach (var id in switchIds)
    {
        sampledValues[id] = 1f;
    }
    foreach (var id in valueIds)
    {
        sampledValues[id] = 0.5f;
    }
    foreach (var id in doorIds)
    {
        sampledValues[id] = 1f;
    }
    if (engineNId is ushort rpmId)
    {
        sampledValues[rpmId] = 1500f;
    }

    var scriptSnapshot = new LocalOmsiScriptVarsSnapshot(
        DateTimeOffset.UtcNow,
        actualVarHash,
        sampledValues.Keys.ToArray(),
        sampledValues.Values.ToArray(),
        [],
        []);

    var multiplayerServiceType = typeof(MultiplayerClientService);
    var buildVisualSnapshot =
        multiplayerServiceType.GetMethod(
            "BuildOpenOmsiVisualSnapshot",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "local openOMSI visual snapshot builder not found");
    var visualSnapshot =
        buildVisualSnapshot.Invoke(
            null,
            [syncManifest, scriptSnapshot])
        ?? throw new InvalidOperationException(
            "local openOMSI visual snapshot was not produced");

    Require(
        buildVisualSnapshot.Invoke(
            null,
            [
                syncManifest,
                scriptSnapshot with
                {
                    VarTableHash = actualVarHash ^ 0x00000001u
                }
            ]) is null,
        "visual SyncTable accepted a snapshot from a different VarTable");
    Require(
        buildVisualSnapshot.Invoke(
            null,
            [
                syncManifest,
                scriptSnapshot with
                {
                    CapturedAtUtc =
                        DateTimeOffset.UtcNow -
                        TimeSpan.FromSeconds(3)
                }
            ]) is null,
        "visual SyncTable accepted a stale local PublicVars snapshot");

    var telemetry = new VehicleTelemetry(
        "sync-local",
        DateTimeOffset.UtcNow,
        "Grundorf",
        "SyncTableSmoke",
        null,
        null,
        100d,
        200d,
        0d,
        90d,
        0d,
        true,
        VehiclePath: @"Vehicles\VarTableSmoke\Smoke.bus");
    var presence = new PlayerPresence(
        "sync-local",
        "Sync Local",
        "sync-room",
        "Grundorf",
        DateTimeOffset.UtcNow);
    var frame = new PlayerTelemetryFrame(
        presence,
        telemetry);

    var buildLocalInfo =
        multiplayerServiceType.GetMethod(
            "BuildOpenOmsiLocalInfo",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "local openOMSI INFO builder not found");
    var localInfo =
        (OpenOmsiLanVehicleInfo?)buildLocalInfo.Invoke(
            null,
            [
                (ushort)27,
                frame,
                syncHash
            ])
        ?? throw new InvalidOperationException(
            "local openOMSI INFO was not produced");
    Require(
        localInfo.SyncTableHash == syncHash,
        "local openOMSI INFO did not publish the visual SyncTable hash");

    var buildLocalState =
        multiplayerServiceType.GetMethod(
            "BuildOpenOmsiLocalState",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "local openOMSI STATE builder not found");
    var localState =
        (OpenOmsiLanVehicleState?)buildLocalState.Invoke(
            null,
            [
                (ushort)27,
                (ushort)9,
                frame,
                visualSnapshot,
                12_345u
            ])
        ?? throw new InvalidOperationException(
            "local openOMSI STATE was not produced");

    Require(
        localState.Lamps.Count == lampIds.Length &&
        localState.Switches.Count == switchIds.Length &&
        localState.Values.Count == valueIds.Length &&
        localState.Doors.Count == doorIds.Length &&
        Math.Abs(localState.EngineRpm - 1500f) < 0.01f,
        "local visual snapshot was not mapped into openOMSI STATE arrays");

    var statePacket = OpenOmsiLanStateCodec.Encode(localState);
    Require(
        OpenOmsiLanStateCodec.TryDecode(
            statePacket,
            out var decodedVisualState),
        "openOMSI visual STATE did not survive v6 encode/decode");
    Require(
        decodedVisualState.Lamps.Count == lampIds.Length &&
        decodedVisualState.Switches.Count == switchIds.Length &&
        decodedVisualState.Values.Count == valueIds.Length &&
        decodedVisualState.Doors.Count == doorIds.Length &&
        decodedVisualState.Lamps.All(value => Math.Abs(value - 1f) < 0.001f) &&
        decodedVisualState.Switches.All(value => Math.Abs(value - 1f) < 0.001f) &&
        decodedVisualState.Doors.All(value => Math.Abs(value - 1f) < 0.001f),
        "openOMSI visual STATE arrays changed across the v6 wire codec");

    var hasCompatibleVisualState =
        multiplayerServiceType.GetMethod(
            "HasCompatibleOpenOmsiVisualState",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "remote openOMSI SyncTable compatibility guard not found");
    Require(
        (bool)(hasCompatibleVisualState.Invoke(
            null,
            [decodedVisualState, syncManifest]) ?? false),
        "remote openOMSI visual SyncTable rejected a matching STATE");
    Require(
        !(bool)(hasCompatibleVisualState.Invoke(
            null,
            [
                decodedVisualState with { Lamps = [] },
                syncManifest
            ]) ?? true),
        "remote openOMSI visual SyncTable accepted mismatched STATE cardinality");
}
finally
{
    Directory.Delete(varTableRoot, recursive: true);
}

var interpolatorType =
    typeof(OmsiPluginBridgeServer).Assembly.GetType(
        "NavBR.Client.Multiplayer.OpenOmsiRemoteStateInterpolator",
        throwOnError: true)!;
var interpolator =
    Activator.CreateInstance(
        interpolatorType,
        nonPublic: true)
    ?? throw new InvalidOperationException(
        "openOMSI remote state interpolator could not be created");
var pushAndInterpolate =
    interpolatorType.GetMethod(
        "TryPushAndInterpolate",
        BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new InvalidOperationException(
        "openOMSI remote state interpolation entrypoint not found");

bool PushState(
    OpenOmsiLanVehicleState state,
    out OpenOmsiLanVehicleState? rendered)
{
    object?[] args = [state, null];
    var accepted =
        (bool)(pushAndInterpolate.Invoke(
            interpolator,
            args) ?? false);
    rendered =
        args[1] as OpenOmsiLanVehicleState;
    return accepted;
}

var orderedState =
    OpenOmsiLanVehicleState.Empty(42, 1) with
    {
        Flags = OpenOmsiLanProtocol.FlagVehicle,
        X = 10d,
        Y = 20d,
        SpeedKph = 25f,
        SentMilliseconds = 1_000u
    };
Require(
    PushState(
        orderedState,
        out var orderedRendered) &&
    orderedRendered is not null,
    "openOMSI interpolator rejected the first ordered state");

var staleState =
    orderedState with
    {
        Sequence = 2,
        X = 9d,
        SentMilliseconds = 900u
    };
Require(
    !PushState(staleState, out _),
    "openOMSI interpolator accepted a stale/reordered state");

var laterState =
    orderedState with
    {
        Sequence = 3,
        X = 30d,
        SentMilliseconds = 50_000u
    };
Require(
    PushState(laterState, out _),
    "openOMSI interpolator rejected a later state");

var restartedClockState =
    orderedState with
    {
        Sequence = 1,
        X = 5d,
        SentMilliseconds = 1_000u
    };
Require(
    PushState(
        restartedClockState,
        out var restartRendered) &&
    restartRendered is not null,
    "openOMSI interpolator did not reset after remote clock restart");

var updaterType =
    typeof(OmsiPluginBridgeServer).Assembly.GetType(
        "NavBR.Client.Updates.NavBRAutoUpdateService",
        throwOnError: true)!;
var compareReleaseVersions =
    updaterType.GetMethod(
        "CompareReleaseVersions",
        BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "application updater version comparator not found");
var tryGetExpectedSha256 =
    updaterType.GetMethod(
        "TryGetExpectedSha256",
        BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "application updater checksum parser not found");
var isReleaseAllowedForChannel =
    updaterType.GetMethod(
        "IsReleaseAllowedForChannel",
        BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "application updater channel filter not found");

int CompareUpdateVersions(string left, string right) =>
    (int)(compareReleaseVersions.Invoke(
        null,
        [left, right])
        ?? throw new InvalidOperationException(
            "application updater version comparator returned null"));

Require(
    CompareUpdateVersions(
        "v0.3.0-alpha.26",
        "0.3.0-alpha.25") > 0,
    "application updater did not order Alpha.26 after Alpha.25");
Require(
    CompareUpdateVersions(
        "0.3.1-alpha.1",
        "0.3.0-alpha.99") > 0,
    "application updater did not prioritize a newer core version");
Require(
    CompareUpdateVersions(
        "0.3.0",
        "0.3.0-alpha.99") > 0,
    "application updater did not prioritize a stable build over its prerelease");
Require(
    CompareUpdateVersions(
        "0.3.0-beta.1",
        "0.3.0-alpha.99") > 0,
    "application updater prerelease ordering regressed");
Require(
    (bool)(isReleaseAllowedForChannel.Invoke(
        null,
        ["0.3.0", "stable"])
        ?? false),
    "stable update channel rejected a stable release");
Require(
    !(bool)(isReleaseAllowedForChannel.Invoke(
        null,
        ["0.3.0-alpha.26", "stable"])
        ?? true),
    "stable update channel accepted an Alpha release");
Require(
    (bool)(isReleaseAllowedForChannel.Invoke(
        null,
        ["0.3.0-alpha.26", "alpha"])
        ?? false),
    "public Alpha update channel rejected an Alpha release");
Require(
    (bool)(isReleaseAllowedForChannel.Invoke(
        null,
        ["0.3.0", "alpha"])
        ?? false),
    "public Alpha update channel rejected a stable release");


var updaterChecksumText =
    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  other.zip\r\n" +
    "6e891050d649e22513156281a8de64fe4e2dbeb90380a714608ce73b34b83a92  OMSI-NavBR-Multiplayer-v0.3.0-alpha.25-Setup-win-x86.exe\r\n";
object?[] updaterChecksumArgs =
[
    updaterChecksumText,
    "OMSI-NavBR-Multiplayer-v0.3.0-alpha.25-Setup-win-x86.exe",
    null
];
Require(
    (bool)(tryGetExpectedSha256.Invoke(
        null,
        updaterChecksumArgs)
        ?? false),
    "application updater could not read the official installer checksum");
Require(
    string.Equals(
        updaterChecksumArgs[2] as string,
        "6e891050d649e22513156281a8de64fe4e2dbeb90380a714608ce73b34b83a92",
        StringComparison.Ordinal),
    "application updater returned the wrong installer checksum");

var mainWindowType =
    typeof(OmsiPluginBridgeServer).Assembly.GetType(
        "NavBR.Client.MainWindow",
        throwOnError: true)!;
var sanitizeDiagnosticText =
    mainWindowType.GetMethod(
        "SanitizeDiagnosticText",
        BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "diagnostic privacy sanitizer not found");
var diagnosticSanitizerInput =
    """
    password=hunter2
    "roomId":"navbr-secret-room"
    player=player-secret
    Authorization: Bearer bearer-secret-value
    ipv4=192.168.10.20
    ipv6=2001:db8::42
    email=driver@example.com
    path=C:\Users\Driver\secret.txt
    unc=\\nas\private\driver.txt
    """;
var sanitizedDiagnosticText =
    (string?)sanitizeDiagnosticText.Invoke(
        null,
        [diagnosticSanitizerInput])
    ?? throw new InvalidOperationException(
        "diagnostic privacy sanitizer returned null");
foreach (var sensitiveValue in new[]
{
    "hunter2",
    "navbr-secret-room",
    "player-secret",
    "bearer-secret-value",
    "192.168.10.20",
    "2001:db8::42",
    "driver@example.com",
    @"C:\Users\Driver\secret.txt",
    @"\\nas\private\driver.txt"
})
{
    Require(
        !sanitizedDiagnosticText.Contains(
            sensitiveValue,
            StringComparison.OrdinalIgnoreCase),
        $"diagnostic sanitizer leaked '{sensitiveValue}'");
}
Require(
    sanitizedDiagnosticText.Contains("[redacted]", StringComparison.Ordinal) &&
    sanitizedDiagnosticText.Contains("[ip]", StringComparison.Ordinal) &&
    sanitizedDiagnosticText.Contains("[email]", StringComparison.Ordinal) &&
    sanitizedDiagnosticText.Contains("[path]", StringComparison.Ordinal),
    "diagnostic sanitizer did not emit expected privacy markers");

var buildHudSettingsFromWeb =
    mainWindowType.GetMethod(
        "BuildHudSettingsFromWeb",
        BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "React HUD settings builder not found");
using var hudPayloadDocument = JsonDocument.Parse(
    """
    {
      "enabled": true,
      "preset": "normal",
      "theme": "navbr-modern",
      "anchor": "bottom-right",
      "autoScale": true,
      "showFuel": true,
      "showPedals": false,
      "showStatus": true,
      "showMinimap": false,
      "showMultiplayer": false,
      "showAlerts": true,
      "showSideIndicators": false,
      "mapZoom": 1.75,
      "minimapStyle": "circular",
      "telematrixEnabled": true,
      "telematrixTheme": 2,
      "telematrixSize": 1
    }
    """);
var hudPayload = hudPayloadDocument.RootElement.Clone();
var reactHudSettings =
    (MultiplayerSettings?)buildHudSettingsFromWeb.Invoke(
        null,
        [hudPayload])
    ?? throw new InvalidOperationException(
        "React HUD settings builder returned null");
Require(
    reactHudSettings.DashboardSettingsVersion == 4,
    "React HUD settings builder regressed to a legacy dashboard version");
Require(
    Math.Abs(reactHudSettings.HudZoom - 1.75d) < 0.001d,
    "React HUD settings builder lost GPS base zoom");
Require(
    string.Equals(
        reactHudSettings.DashboardMinimapStyle,
        "circular",
        StringComparison.OrdinalIgnoreCase),
    "React HUD settings builder lost circular GPS shape");
Require(
    reactHudSettings.TelematrixSettingsVersion == 1 &&
    reactHudSettings.TelematrixWidgetEnabled &&
    reactHudSettings.TelematrixTheme == 2 &&
    reactHudSettings.TelematrixSize == 1,
    "React HUD settings builder lost TeleMatrix settings");

var hudOverlayType =
    typeof(OmsiPluginBridgeServer).Assembly.GetType(
        "NavBR.Client.Overlay.HudOverlayWindow",
        throwOnError: true)!;
Require(
    hudOverlayType.GetMethod(
        "UpdateLocalRoadTraffic",
        BindingFlags.Public | BindingFlags.Instance) is not null,
    "HUD overlay lost local OMSI road-traffic feed support");

var speedZoomMethod =
    hudOverlayType.GetMethod(
        "ComputeHudSpeedZoomFactor",
        BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "HUD speed-sensitive zoom helper not found");
var lowSpeedZoom =
    (double?)speedZoomMethod.Invoke(null, [0d]) ?? double.NaN;
var highSpeedZoom =
    (double?)speedZoomMethod.Invoke(null, [80d]) ?? double.NaN;
Require(
    double.IsFinite(lowSpeedZoom) &&
    double.IsFinite(highSpeedZoom) &&
    lowSpeedZoom > highSpeedZoom &&
    lowSpeedZoom <= 1.18d + 0.001d &&
    highSpeedZoom >= 0.72d - 0.001d,
    "HUD speed-sensitive GPS zoom is invalid");

var trafficHeadingMethod =
    hudOverlayType.GetMethod(
        "TrafficQuaternionToHeadingDegrees",
        BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "HUD traffic quaternion heading helper not found");
var ninetyDegrees = Math.Sqrt(0.5d);
var heading90 =
    (double?)trafficHeadingMethod.Invoke(
        null,
        [0d, ninetyDegrees, 0d, ninetyDegrees])
    ?? double.NaN;
Require(
    double.IsFinite(heading90) &&
    Math.Abs(heading90 - 90d) < 0.01d,
    "HUD AI traffic marker heading conversion is invalid");

var trafficReaderType =
    typeof(OmsiPluginBridgeServer).Assembly.GetType(
        "NavBR.Client.Telemetry.OmsiRoadTrafficReader",
        throwOnError: true)!;
Require(
    trafficReaderType.GetMethod(
        "Read",
        BindingFlags.Public | BindingFlags.Static) is not null,
    "OMSI road-traffic reader not available for GPS traffic markers");

var appliedHudPreset =
    HudProfileCatalog.ApplyPreset(
        MultiplayerSettings.CreateDefault(),
        HudProfileCatalog.DefaultPreset);
Require(
    appliedHudPreset.DashboardSettingsVersion == 4,
    "HUD preset application regressed to a legacy dashboard settings version");

var normalizeMultiplayerSettings =
    typeof(MultiplayerSettingsStore).GetMethod(
        "Normalize",
        BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "multiplayer settings normalizer not found");
var legacyDefaultHud = MultiplayerSettings.CreateDefault() with
{
    DashboardSettingsVersion = 3,
    DashboardAnchor = "free",
    DashboardX = 0.02d,
    DashboardY = 0.58d,
    DashboardShowMinimap = true,
    DashboardShowMultiplayer = true,
    DashboardShowAlerts = true,
    DashboardShowSideIndicators = true,
    DashboardMinimapScale = 1d,
    DashboardMultiplayerScale = 1d,
    DashboardAlertsScale = 1d,
    DashboardSideIndicatorsScale = 1d
};
var migratedDefaultHud =
    (MultiplayerSettings?)normalizeMultiplayerSettings.Invoke(
        null,
        [legacyDefaultHud])
    ?? throw new InvalidOperationException(
        "HUD settings migration returned null");
Require(
    migratedDefaultHud.DashboardSettingsVersion == 4 &&
    string.Equals(
        migratedDefaultHud.DashboardAnchor,
        "bottom-right",
        StringComparison.OrdinalIgnoreCase) &&
    !migratedDefaultHud.DashboardShowMinimap &&
    !migratedDefaultHud.DashboardShowMultiplayer &&
    !migratedDefaultHud.DashboardShowSideIndicators,
    "default Alpha.26 HUD layout was not migrated away from overlapping duplicate widgets");

var customizedHud =
    (MultiplayerSettings?)normalizeMultiplayerSettings.Invoke(
        null,
        [legacyDefaultHud with { DashboardX = 0.42d }])
    ?? throw new InvalidOperationException(
        "custom HUD settings migration returned null");
Require(
    string.Equals(
        customizedHud.DashboardAnchor,
        "free",
        StringComparison.OrdinalIgnoreCase) &&
    customizedHud.DashboardShowMinimap &&
    customizedHud.DashboardShowMultiplayer &&
    customizedHud.DashboardShowSideIndicators,
    "HUD cleanup migration overwrote a customized layout");

var untouchedLegacyTelematrix =
    (MultiplayerSettings?)normalizeMultiplayerSettings.Invoke(
        null,
        [MultiplayerSettings.CreateDefault() with
        {
            TelematrixSettingsVersion = 0,
            TelematrixWidgetEnabled = true,
            TelematrixTheme = 0,
            TelematrixSize = 0,
            TelematrixAutoDirection = true,
            TelematrixManualLine = null,
            TelematrixManualDirection = "TP"
        }])
    ?? throw new InvalidOperationException(
        "TeleMatrix settings migration returned null");
Require(
    untouchedLegacyTelematrix.TelematrixSettingsVersion == 1 &&
    !untouchedLegacyTelematrix.TelematrixWidgetEnabled,
    "untouched legacy TeleMatrix was not migrated to opt-in");

var customizedLegacyTelematrix =
    (MultiplayerSettings?)normalizeMultiplayerSettings.Invoke(
        null,
        [MultiplayerSettings.CreateDefault() with
        {
            TelematrixSettingsVersion = 0,
            TelematrixWidgetEnabled = true,
            TelematrixTheme = 1
        }])
    ?? throw new InvalidOperationException(
        "custom TeleMatrix settings migration returned null");
Require(
    customizedLegacyTelematrix.TelematrixWidgetEnabled &&
    customizedLegacyTelematrix.TelematrixTheme == 1,
    "TeleMatrix cleanup migration overwrote customized settings");

var coordinatorType =
    typeof(OmsiPluginBridgeServer).Assembly.GetType(
        "NavBR.Client.Multiplayer.RemotePhysicalVehicleCoordinator",
        throwOnError: true)!;
var recoverPhysicalPose = coordinatorType.GetMethod(
    "TryRecoverExplicitPhysicalPose",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "physical pose recovery helper not found");

var sparsePhysicalTelemetry = new VehicleTelemetry(
    PlayerId: "remote-smoke",
    Timestamp: DateTimeOffset.UtcNow,
    MapName: "Grundorf",
    VehicleName: "NL202 - EN92",
    Line: null,
    Route: null,
    X: 325d,
    Y: 1.5d,
    Z: 442d,
    HeadingDegrees: 45d,
    SpeedKph: 0d,
    IsInGame: true,
    GridX: 1,
    GridY: 2,
    TileX: 201.6d,
    TileY: 164.1d,
    PhysicalGridX: 1,
    PhysicalGridY: 2);

object?[] recoverArgs =
[
    sparsePhysicalTelemetry,
    null
];
var recoveredPhysicalPose =
    (bool)(recoverPhysicalPose.Invoke(
        null,
        recoverArgs)
        ?? false);
Require(
    recoveredPhysicalPose,
    "explicit PhysicalGrid + TileXY pose was not recovered");
var recoveredTelemetry =
    recoverArgs[1] as VehicleTelemetry
    ?? throw new InvalidOperationException(
        "physical pose recovery did not return telemetry");
Require(
    Math.Abs((recoveredTelemetry.LocalX ?? double.NaN) - 201.6d) < 0.001d &&
    Math.Abs((recoveredTelemetry.LocalY ?? double.NaN) - 1.5d) < 0.001d &&
    Math.Abs((recoveredTelemetry.LocalZ ?? double.NaN) - 164.1d) < 0.001d,
    "physical pose recovery did not preserve the proven Kachel-local position");
Require(
    recoveredTelemetry.RotationX is double &&
    recoveredTelemetry.RotationY is double recoveredRotationY &&
    recoveredTelemetry.RotationZ is double &&
    recoveredTelemetry.RotationW is double recoveredRotationW &&
    double.IsFinite(recoveredRotationY) &&
    double.IsFinite(recoveredRotationW),
    "physical pose recovery did not reconstruct a finite yaw quaternion");

var ambiguousPhysicalTelemetry =
    sparsePhysicalTelemetry with
    {
        PhysicalGridX = null,
        PhysicalGridY = null
    };
object?[] ambiguousArgs =
[
    ambiguousPhysicalTelemetry,
    null
];
Require(
    !(bool)(recoverPhysicalPose.Invoke(
        null,
        ambiguousArgs)
        ?? true),
    "physical pose recovery accepted TileXY without explicit PhysicalGrid");

var crossingPhysicalTelemetry =
    sparsePhysicalTelemetry with
    {
        GridX = 2,
        GridY = 2,
        PhysicalGridX = 2,
        PhysicalGridY = 2,
        TileX = 0.4d,
        TileY = 164.2d,
        LocalX = null,
        LocalY = null,
        LocalZ = null,
        RotationX = null,
        RotationY = null,
        RotationZ = null,
        RotationW = null,
        SpeedKph = 28d
    };
object?[] crossingArgs =
[
    crossingPhysicalTelemetry,
    null
];
Require(
    (bool)(recoverPhysicalPose.Invoke(
        null,
        crossingArgs)
        ?? false),
    "Kachel boundary frame was not recovered from the new coherent physical/navigation grid.");
var crossingRecovered =
    crossingArgs[1] as VehicleTelemetry
    ?? throw new InvalidOperationException(
        "Kachel boundary recovery did not return telemetry.");
Near(
    crossingRecovered.LocalX ?? double.NaN,
    0.4d,
    0.001d,
    "Kachel boundary local X");
Near(
    crossingRecovered.LocalZ ?? double.NaN,
    164.2d,
    0.001d,
    "Kachel boundary local Z");
Require(
    crossingRecovered.PhysicalGridX == 2 &&
    crossingRecovered.PhysicalGridY == 2,
    "Kachel boundary recovery lost the new physical grid.");

var mismatchedKachelTelemetry =
    crossingPhysicalTelemetry with
    {
        GridX = 1,
        GridY = 2
    };
object?[] mismatchedKachelArgs =
[
    mismatchedKachelTelemetry,
    null
];
Require(
    !(bool)(recoverPhysicalPose.Invoke(
        null,
        mismatchedKachelArgs)
        ?? true),
    "physical pose recovery paired Navigation TileXY with a different PhysicalGrid.");

var resolvePhysicalUpdateInterval =
    coordinatorType.GetMethod(
        "ResolvePhysicalUpdateInterval",
        BindingFlags.NonPublic |
        BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "physical update cadence resolver not found");
var movingPhysicalInterval =
    (TimeSpan)(resolvePhysicalUpdateInterval.Invoke(
        null,
        [crossingPhysicalTelemetry])
        ?? throw new InvalidOperationException(
            "physical update cadence resolver returned null"));
Require(
    Math.Abs(movingPhysicalInterval.TotalMilliseconds - 50d) < 0.01d,
    $"moving physical telemetry must stay at 20 Hz; got {movingPhysicalInterval.TotalMilliseconds:F1} ms.");

var hofLocal = new OmsiCompatibilityManifest(
    OmsiVersion: "2.3.004",
    NavBRVersion: "smoke",
    MapName: "Grundorf",
    MapCompatibilityId: "sha256:map",
    VehiclePath: @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
    VehicleCompatibilityId: "sha256:vehicle",
    HofName: "Grundorf.hof",
    HofCompatibilityId: "sha256:hof-a",
    PluginProtocolVersion: PluginBridgeProtocol.Version,
    PluginDeployment: "OMSI2-X86",
    Capabilities: Array.Empty<string>());
var hofRemote = hofLocal with
{
    HofCompatibilityId = "sha256:hof-b"
};
var hofPhysicalReport =
    OmsiCompatibilityEvaluator.Compare(
        hofLocal,
        hofRemote,
        requireVehicleForPhysicalMultiplayer: false);
Require(
    hofPhysicalReport.IsCompatible &&
    !hofPhysicalReport.HasBlockingIssues,
    "HOF mismatch incorrectly blocked physical multiplayer.");
Require(
    hofPhysicalReport.Issues.Any(issue =>
        issue.Code == "hof-mismatch" &&
        issue.Severity == CompatibilityIssueSeverity.Warning),
    "HOF mismatch must remain visible as a warning without blocking spawn.");

var openOmsiEnvironmentType =
    typeof(OmsiPluginBridgeServer).Assembly.GetType(
        "NavBR.Client.OpenOmsi.OpenOmsiEnvironmentLocator",
        throwOnError: true)!;
var resolveInstalledVehicle =
    openOmsiEnvironmentType.GetMethod(
        "ResolveInstalledVehicleFile",
        BindingFlags.Public |
        BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "openOMSI installed vehicle resolver not found");
var assetRoot = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-asset-" +
    Guid.NewGuid().ToString("N"));
var assetRelative = Path.Combine(
    "Vehicles",
    "NavBR_Smoke",
    "Smoke.bus");
var assetPath = Path.Combine(
    assetRoot,
    assetRelative);
Directory.CreateDirectory(
    Path.GetDirectoryName(assetPath)!);
File.WriteAllText(
    assetPath,
    "[friendlyname]\r\nNavBR Asset Smoke\r\n");
var previousOmsiRoot =
    Environment.GetEnvironmentVariable("OMSI_ROOT");
try
{
    Environment.SetEnvironmentVariable(
        "OMSI_ROOT",
        assetRoot);

    var resolvedAsset = (string?)resolveInstalledVehicle.Invoke(
        null,
        [assetRelative.Replace('\\', '/')]);
    Require(
        string.Equals(
            Path.GetFullPath(resolvedAsset ?? string.Empty),
            Path.GetFullPath(assetPath),
            StringComparison.OrdinalIgnoreCase),
        "openOMSI content-root vehicle resolver did not find an installed .bus.");

    var escapedAsset = (string?)resolveInstalledVehicle.Invoke(
        null,
        [@"..\outside.bus"]);
    Require(
        escapedAsset is null,
        "openOMSI content-root vehicle resolver accepted path traversal.");

    var invalidAsset = (string?)resolveInstalledVehicle.Invoke(
        null,
        [@"Vehicles\NavBR_Smoke\Smoke.cfg"]);
    Require(
        invalidAsset is null,
        "openOMSI content-root vehicle resolver accepted a non-vehicle extension.");
}
finally
{
    Environment.SetEnvironmentVariable(
        "OMSI_ROOT",
        previousOmsiRoot);
    Directory.Delete(
        assetRoot,
        recursive: true);
}

var multiRootResolverType =
    typeof(OmsiPluginBridgeServer).Assembly.GetType(
        "NavBR.Client.Multiplayer.OpenOmsiRemoteVehicleAssetResolver",
        throwOnError: true)!;
var multiRootResolve = multiRootResolverType.GetMethod(
    "ResolveAsync",
    BindingFlags.Public | BindingFlags.Instance)
    ?? throw new InvalidOperationException(
        "openOMSI multi-root SHA vehicle resolver not found");
var multiRootA = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-multi-a-" +
    Guid.NewGuid().ToString("N"));
var multiRootB = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-multi-b-" +
    Guid.NewGuid().ToString("N"));
var reportedRelative =
    Path.Combine("Vehicles", "RemotePack", "Remote.bus");
var relocatedRelative =
    Path.Combine("Vehicles", "LocalPack", "RenamedRemote.bus");
var wrongPath = Path.Combine(
    multiRootA,
    reportedRelative);
var correctPath = Path.Combine(
    multiRootB,
    relocatedRelative);
Directory.CreateDirectory(
    Path.GetDirectoryName(wrongPath)!);
Directory.CreateDirectory(
    Path.GetDirectoryName(correctPath)!);
var wrongBytes = Encoding.UTF8.GetBytes(
    "[friendlyname]\r\nWrong local content\r\n");
var correctBytes = Encoding.UTF8.GetBytes(
    "[friendlyname]\r\nSame remote content under another path\r\n");
File.WriteAllBytes(wrongPath, wrongBytes);
File.WriteAllBytes(correctPath, correctBytes);
try
{
    var expectedRemoteFingerprint =
        "sha256:" +
        Convert.ToHexString(
            SHA256.HashData(correctBytes))
        .ToLowerInvariant();
    Func<IReadOnlyList<string>> rootsSource =
        () => new[] { multiRootA, multiRootB };
    var resolver = Activator.CreateInstance(
        multiRootResolverType,
        rootsSource)
        ?? throw new InvalidOperationException(
            "openOMSI multi-root SHA resolver could not be created");

    var resolveTask = multiRootResolve.Invoke(
        resolver,
        [
            reportedRelative,
            expectedRemoteFingerprint,
            CancellationToken.None
        ]) as Task<string?>
        ?? throw new InvalidOperationException(
            "openOMSI multi-root SHA resolver did not return Task<string?>");
    var resolvedRemotePath = await resolveTask;
    Require(
        string.Equals(
            resolvedRemotePath,
            relocatedRelative.Replace('/', '\\'),
            StringComparison.OrdinalIgnoreCase),
        $"openOMSI SHA resolver did not relocate the remote vehicle. Got '{resolvedRemotePath}'.");

    var mismatchedFingerprint =
        "sha256:" + new string('0', 64);
    var mismatchTask = multiRootResolve.Invoke(
        resolver,
        [
            reportedRelative,
            mismatchedFingerprint,
            CancellationToken.None
        ]) as Task<string?>
        ?? throw new InvalidOperationException(
            "openOMSI multi-root mismatch resolver did not return Task<string?>");
    Require(
        await mismatchTask is null,
        "openOMSI SHA resolver accepted a locally different vehicle definition");

    var shadowedRelative =
        Path.Combine(
            "Vehicles",
            "ShadowPack",
            "Shadowed.bus");
    var shadowingPath =
        Path.Combine(
            multiRootA,
            shadowedRelative);
    var shadowedCorrectPath =
        Path.Combine(
            multiRootB,
            shadowedRelative);
    Directory.CreateDirectory(
        Path.GetDirectoryName(shadowingPath)!);
    Directory.CreateDirectory(
        Path.GetDirectoryName(shadowedCorrectPath)!);
    var shadowingBytes =
        Encoding.UTF8.GetBytes(
            "[friendlyname]\r\nHigher-priority wrong override\r\n");
    var shadowedCorrectBytes =
        Encoding.UTF8.GetBytes(
            "[friendlyname]\r\nLower-priority correct vehicle\r\n");
    File.WriteAllBytes(
        shadowingPath,
        shadowingBytes);
    File.WriteAllBytes(
        shadowedCorrectPath,
        shadowedCorrectBytes);
    var shadowedExpectedFingerprint =
        "sha256:" +
        Convert.ToHexString(
            SHA256.HashData(
                shadowedCorrectBytes))
        .ToLowerInvariant();

    var shadowedTask =
        multiRootResolve.Invoke(
            resolver,
            [
                shadowedRelative,
                shadowedExpectedFingerprint,
                CancellationToken.None
            ]) as Task<string?>
        ?? throw new InvalidOperationException(
            "openOMSI shadowed SHA resolver did not return Task<string?>");
    Require(
        await shadowedTask is null,
        "openOMSI SHA resolver accepted a lower-priority vehicle hidden by different content.");
}
finally
{
    Directory.Delete(multiRootA, recursive: true);
    Directory.Delete(multiRootB, recursive: true);
}

var mapCatalogType =
    typeof(NavBR.Client.Maps.OmsiMapCatalog);
var fingerprintMap = mapCatalogType
    .GetMethods(
        BindingFlags.NonPublic |
        BindingFlags.Static)
    .SingleOrDefault(method =>
        method.Name ==
            "TryFingerprintInstalledMap" &&
        method.GetParameters() is var parameters &&
        parameters.Length == 2 &&
        parameters[0].ParameterType ==
            typeof(string))
    ?? throw new InvalidOperationException(
        "single-root installed map fingerprint helper not found");
var fingerprintMergedMap = mapCatalogType
    .GetMethods(
        BindingFlags.NonPublic |
        BindingFlags.Static)
    .SingleOrDefault(method =>
        method.Name ==
            "TryFingerprintInstalledMap" &&
        method.GetParameters() is var parameters &&
        parameters.Length == 2 &&
        parameters[0].ParameterType ==
            typeof(IReadOnlyList<string>))
    ?? throw new InvalidOperationException(
        "multi-root installed map fingerprint helper not found");
var mapRoot = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-map-fingerprint-" +
    Guid.NewGuid().ToString("N"));
var mapDirectory = Path.Combine(
    mapRoot,
    "maps",
    "Grundorf");
Directory.CreateDirectory(mapDirectory);
var globalCfgPath =
    Path.Combine(mapDirectory, "global.cfg");
var tileAPath =
    Path.Combine(mapDirectory, "tile_-1_0.map");
var tileBPath =
    Path.Combine(mapDirectory, "tile_0_0.map");
var globalCfgBytes = Encoding.UTF8.GetBytes(
    "[name]\r\nGrundorf\r\n[friendlyname]\r\nGrundorf Smoke\r\n");
File.WriteAllBytes(globalCfgPath, globalCfgBytes);
File.WriteAllBytes(tileAPath, new byte[17]);
File.WriteAllBytes(tileBPath, new byte[29]);
try
{
    using var mapHash =
        IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
    mapHash.AppendData(globalCfgBytes);
    foreach (var tilePath in new[]
             {
                 tileAPath,
                 tileBPath
             }
             .OrderBy(
                 path => Path.GetFileName(path),
                 StringComparer.OrdinalIgnoreCase))
    {
        var name =
            Path.GetFileName(tilePath)
                .ToUpperInvariant();
        var length =
            new FileInfo(tilePath).Length;
        mapHash.AppendData(
            Encoding.UTF8.GetBytes(
                $"\n{name}:{length}"));
    }

    var expectedMapFingerprint =
        Convert.ToHexString(
            mapHash.GetHashAndReset())
        .ToLowerInvariant();

    var actualMapFingerprint = (string?)fingerprintMap.Invoke(
        null,
        [mapRoot, "maps/Grundorf/global.cfg"]);
    Require(
        string.Equals(
            actualMapFingerprint,
            expectedMapFingerprint,
            StringComparison.Ordinal),
        "openOMSI installed map fingerprint regressed");

    var mapByFolderFingerprint = (string?)fingerprintMap.Invoke(
        null,
        [mapRoot, "Grundorf"]);
    Require(
        string.Equals(
            mapByFolderFingerprint,
            expectedMapFingerprint,
            StringComparison.Ordinal),
        "openOMSI map folder fingerprint did not match raw LAN map path");

    var escapedMapFingerprint = (string?)fingerprintMap.Invoke(
        null,
        [mapRoot, @"..\outside"]);
    Require(
        escapedMapFingerprint is null,
        "map fingerprint helper accepted a path outside the content root");

    var overlayRoot = Path.Combine(
        Path.GetTempPath(),
        "NavBR-openOMSI-map-overlay-" +
        Guid.NewGuid().ToString("N"));
    var overlayDirectory = Path.Combine(
        overlayRoot,
        "maps",
        "Grundorf");
    Directory.CreateDirectory(overlayDirectory);
    var overlayTileB =
        Path.Combine(
            overlayDirectory,
            "tile_0_0.map");
    File.WriteAllBytes(
        overlayTileB,
        new byte[41]);
    try
    {
        using var mergedHash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);
        mergedHash.AppendData(globalCfgBytes);
        foreach (var item in new[]
                 {
                     (
                         Name: "tile_-1_0.map",
                         Length: 17L),
                     (
                         Name: "tile_0_0.map",
                         Length: 41L)
                 }
                 .OrderBy(
                     item => item.Name,
                     StringComparer.OrdinalIgnoreCase))
        {
            mergedHash.AppendData(
                Encoding.UTF8.GetBytes(
                    $"\n{item.Name.ToUpperInvariant()}:{item.Length}"));
        }

        var expectedMergedMapFingerprint =
            Convert.ToHexString(
                mergedHash.GetHashAndReset())
            .ToLowerInvariant();

        var actualMergedMapFingerprint =
            (string?)fingerprintMergedMap.Invoke(
                null,
                [
                    new[]
                    {
                        overlayRoot,
                        mapRoot
                    },
                    "maps/Grundorf/global.cfg"
                ]);
        Require(
            string.Equals(
                actualMergedMapFingerprint,
                expectedMergedMapFingerprint,
                StringComparison.Ordinal),
            "openOMSI merged content-root map fingerprint did not honor overlay priority");
    }
    finally
    {
        Directory.Delete(
            overlayRoot,
            recursive: true);
    }
}
finally
{
    Directory.Delete(mapRoot, recursive: true);
}

var routeTraceRoot = Path.Combine(
    Path.GetTempPath(),
    "NavBR-route-grid-smoke-" +
    Guid.NewGuid().ToString("N"));
var routeTraceMapDirectory = Path.Combine(
    routeTraceRoot,
    "maps",
    "RouteGridSmoke");
var routeTraceTtData = Path.Combine(
    routeTraceMapDirectory,
    "TTData");
Directory.CreateDirectory(routeTraceTtData);
var routeTraceGlobal = Path.Combine(
    routeTraceMapDirectory,
    "global.cfg");
File.WriteAllText(
    routeTraceGlobal,
    """
    [name]
    RouteGridSmoke
    [map]
    183
    104
    tile_183_104.map
    [map]
    183
    105
    tile_183_105.map
    """);
// Exercise the real OMSI TTR -> tile [spline] -> .sli lane geometry
// pipeline. Empty tile files used to make this integration test fail before
// it could ever verify route selection or GPS guidance.
var routeTraceSplines = Path.Combine(routeTraceRoot, "Splines");
Directory.CreateDirectory(routeTraceSplines);
File.WriteAllText(
    Path.Combine(routeTraceSplines, "NavBR-Smoke-Road.sli"),
    string.Concat(Enumerable.Range(0, 14).Select(_ =>
        "[path]\n0\n0\n0\n")));
File.WriteAllText(
    Path.Combine(routeTraceMapDirectory, "tile_183_104.map"),
    """
    [spline]
    0
    Splines\NavBR-Smoke-Road.sli
    733660
    -1
    -1
    150
    0
    140
    0
    160
    0
    """);
File.WriteAllText(
    Path.Combine(routeTraceMapDirectory, "tile_183_105.map"),
    """
    [spline]
    0
    Splines\NavBR-Smoke-Road.sli
    733661
    -1
    -1
    150
    0
    0
    0
    160
    0
    """);
File.WriteAllText(
    Path.Combine(routeTraceTtData, "SmokeTrack.ttr"),
    """
    0:
    [track_entry]
    733660
    13
    183
    104
    16.1000000000
    0

    1:
    [track_entry]
    733661
    0
    183
    105
    25.0000000000
    0
    """);
File.WriteAllText(
    Path.Combine(routeTraceTtData, "DirectionA.ttp"),
    """
    [trip]
    SmokeTrack
    Terminal A
    100
    """);
File.WriteAllText(
    Path.Combine(routeTraceTtData, "DirectionB.ttp"),
    """
    [trip]
    OtherTrack
    Terminal B
    100
    """);
try
{
    var routeTraceMap = new NavBR.Client.Maps.OmsiMapInfo(
        "RouteGridSmoke",
        "RouteGridSmoke",
        routeTraceMapDirectory,
        routeTraceGlobal,
        null,
        2,
        null);
    var routeTraceLayout = new NavBR.Client.Maps.OmsiMapLayout(
        183,
        104,
        183,
        105,
        false,
        300d);
    var nativeGridTrace =
        NavBR.Client.Maps.OmsiRouteTraceReader.TryRead(
            routeTraceMap,
            routeTraceLayout,
            "SmokeTrack");
    Require(
        nativeGridTrace.Count >= 2 &&
        nativeGridTrace[0].GridX == 183 &&
        nativeGridTrace[0].GridY == 104 &&
        nativeGridTrace[^1].GridX == 183 &&
        nativeGridTrace[^1].GridY == 105,
        "native OMSI TTR GridX/GridY track entries were not parsed correctly");

    // OMSI can expose a direction/IBIS route code instead of the .ttr name.
    // With two trips on the same line, the destination must disambiguate the
    // correct track rather than leaving RouteAvailable false.
    var destinationResolvedTrace =
        NavBR.Client.Maps.OmsiRouteTraceReader.TryRead(
            routeTraceMap,
            routeTraceLayout,
            "01",
            "100",
            "Terminal A");
    Require(
        destinationResolvedTrace.Count >= 2 &&
        destinationResolvedTrace[0].GridX == 183 &&
        destinationResolvedTrace[0].GridY == 104,
        "active OMSI route code + destination did not resolve the correct TTR trace");

    var routePipelineTelemetry =
        new VehicleTelemetry(
            PlayerId: "route-pipeline",
            Timestamp: DateTimeOffset.UtcNow,
            MapName: "RouteGridSmoke",
            VehicleName: "Route Test Bus",
            Line: "100",
            Route: "01",
            X: 0d,
            Y: 0d,
            Z: 0d,
            HeadingDegrees: 0d,
            SpeedKph: 20d,
            IsInGame: true,
            GridX: 183,
            GridY: 104,
            TileX: 150d,
            TileY: 150d,
            DestinationName: "Terminal A");
    var routePipelineSnapshot =
        NavBR.Client.Maps.NavBRNavigationEngine.Evaluate(
            routePipelineTelemetry,
            routeTraceLayout,
            destinationResolvedTrace);
    Require(
        routePipelineSnapshot.RouteAvailable &&
        routePipelineSnapshot.IsOnRoute,
        "TTData -> route trace -> NavBRNavigationSnapshot pipeline did not produce an active route");
}
finally
{
    Directory.Delete(routeTraceRoot, recursive: true);
}

var openOmsiPoseRoot = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-world-pose-" +
    Guid.NewGuid().ToString("N"));
var openOmsiPoseMapDirectory = Path.Combine(
    openOmsiPoseRoot,
    "maps",
    "WorldPoseSmoke");
Directory.CreateDirectory(
    openOmsiPoseMapDirectory);
var openOmsiPoseGlobal = Path.Combine(
    openOmsiPoseMapDirectory,
    "global.cfg");
var openOmsiPoseTile = Path.Combine(
    openOmsiPoseMapDirectory,
    "tile_1_1.map");
File.WriteAllText(
    openOmsiPoseGlobal,
    """
    [name]
    WorldPoseSmoke
    [friendlyname]
    World Pose Smoke
    [map]
    1
    1
    tile_1_1.map
    """);
File.WriteAllText(
    openOmsiPoseTile,
    "; NavBR openOMSI world-pose smoke tile");
try
{
    var poseMap =
        new NavBR.Client.Maps.OmsiMapCatalog()
            .Discover(openOmsiPoseRoot)
            .Single();
    Require(
        !string.IsNullOrWhiteSpace(
            poseMap.CompatibilityId),
        "World-pose smoke map did not produce a compatibility fingerprint.");

    var openOmsiWorldTelemetry =
        new VehicleTelemetry(
            PlayerId: "openomsi-world",
            Timestamp: DateTimeOffset.UtcNow,
            MapName: "WorldPoseSmoke",
            VehicleName: "World Pose Bus",
            Line: null,
            Route: null,
            X: 325.0,
            Y: 442.0,
            Z: 1.5,
            HeadingDegrees: 45.0,
            SpeedKph: 18.0,
            IsInGame: true,
            MapCompatibilityId:
                poseMap.CompatibilityId);

    var roadResolverType =
        typeof(OmsiPluginBridgeServer).Assembly.GetType(
            "NavBR.Client.Maps.OmsiPhysicalRoadAnchorResolver",
            throwOnError: true)!;
    Func<string?> poseRootSource =
        () => openOmsiPoseRoot;
    var roadResolver =
        Activator.CreateInstance(
            roadResolverType,
            poseRootSource)
        ?? throw new InvalidOperationException(
            "openOMSI world-pose resolver could not be created.");
    var resolveOpenOmsiWorldAnchor =
        roadResolverType.GetMethod(
            "TryResolveOpenOmsiWorldAnchor",
            BindingFlags.Public |
            BindingFlags.Instance)
        ?? throw new InvalidOperationException(
            "openOMSI world-pose resolver method not found.");

    object?[] poseResolveArguments =
        [openOmsiWorldTelemetry, null];
    var poseResolved =
        (bool)(resolveOpenOmsiWorldAnchor.Invoke(
            roadResolver,
            poseResolveArguments)
            ?? false);
    Require(
        poseResolved &&
        poseResolveArguments[1] is not null,
        "openOMSI world pose did not resolve into a real OMSI Kachel.");

    var anchorValue =
        poseResolveArguments[1]!;
    var anchorType =
        anchorValue.GetType();
    static double ReadAnchorDouble(
        Type type,
        object value,
        string name) =>
        Convert.ToDouble(
            type.GetProperty(name)!
                .GetValue(value),
            System.Globalization.CultureInfo.InvariantCulture);
    static int ReadAnchorInt(
        Type type,
        object value,
        string name) =>
        Convert.ToInt32(
            type.GetProperty(name)!
                .GetValue(value),
            System.Globalization.CultureInfo.InvariantCulture);

    Require(
        ReadAnchorInt(
            anchorType,
            anchorValue,
            "GridX") == 1 &&
        ReadAnchorInt(
            anchorType,
            anchorValue,
            "GridY") == 1,
        "openOMSI world pose resolved to the wrong OMSI Kachel.");
    Near(
        ReadAnchorDouble(
            anchorType,
            anchorValue,
            "LocalX"),
        25.0,
        0.001,
        "openOMSI local X");
    Near(
        ReadAnchorDouble(
            anchorType,
            anchorValue,
            "LocalY"),
        1.5,
        0.001,
        "openOMSI local height Y");
    Near(
        ReadAnchorDouble(
            anchorType,
            anchorValue,
            "LocalZ"),
        142.0,
        0.001,
        "openOMSI local Z");
    Near(
        ReadAnchorDouble(
            anchorType,
            anchorValue,
            "HeadingDegrees"),
        45.0,
        0.001,
        "openOMSI anchor heading");

    // Regression: the GPS must feed the projected Kachel/lane anchor into
    // navigation, not continue evaluating the raw world-only telemetry.
    // This mirrors the HUD pipeline after TryGetGpsDisplayAnchor.
    var navigationGridX =
        ReadAnchorInt(anchorType, anchorValue, "GridX");
    var navigationGridY =
        ReadAnchorInt(anchorType, anchorValue, "GridY");
    var navigationTileX =
        ReadAnchorDouble(anchorType, anchorValue, "LocalX");
    var navigationTileY =
        ReadAnchorDouble(anchorType, anchorValue, "LocalZ");
    var anchoredNavigationTelemetry =
        openOmsiWorldTelemetry with
        {
            GridX = navigationGridX,
            GridY = navigationGridY,
            TileX = navigationTileX,
            TileY = navigationTileY
        };
    var navigationLayout =
        new NavBR.Client.Maps.OmsiMapLayout(
            1,
            1,
            1,
            1,
            false,
            300d);
    NavBR.Client.Maps.OmsiRouteTracePoint[] navigationTrace =
    [
        new(1, 1, 5d, navigationTileY),
        new(1, 1, 295d, navigationTileY)
    ];
    var navigationSnapshot =
        NavBR.Client.Maps.NavBRNavigationEngine.Evaluate(
            anchoredNavigationTelemetry,
            navigationLayout,
            navigationTrace);
    Require(
        navigationSnapshot.RouteAvailable,
        "GPS route became unavailable after applying the projected road/Kachel anchor.");
    Require(
        navigationSnapshot.IsOnRoute &&
        navigationSnapshot.OffRouteDistanceMeters < 0.01d,
        "GPS navigation did not evaluate the player on the same projected road anchor used by the marker.");

    var openOmsiPresence =
        new PlayerPresence(
            "openomsi-world",
            "openOMSI World Driver",
            "ci-world-pose",
            "WorldPoseSmoke",
            DateTimeOffset.UtcNow,
            MapCompatibilityId:
                poseMap.CompatibilityId,
            Compatibility:
                new OmsiCompatibilityManifest(
                    OmsiVersion: "openOMSI",
                    NavBRVersion: "ci",
                    MapName: "WorldPoseSmoke",
                    MapCompatibilityId:
                        poseMap.CompatibilityId,
                    VehiclePath: null,
                    VehicleCompatibilityId: null,
                    HofName: null,
                    HofCompatibilityId: null,
                    PluginProtocolVersion:
                        PluginBridgeProtocol.Version,
                    PluginDeployment:
                        "OPENOMSI-X64",
                    Capabilities:
                    [
                        PluginBridgeProtocol
                            .CapabilityOpenOmsiStandardPlugin
                    ]));
    var openOmsiFrame =
        new PlayerTelemetryFrame(
            openOmsiPresence,
            openOmsiWorldTelemetry);

    var openOmsiCoordinatorType =
        typeof(OmsiPluginBridgeServer).Assembly.GetType(
            "NavBR.Client.Multiplayer.RemotePhysicalVehicleCoordinator",
            throwOnError: true)!;
    // The production converter is an instance member because rear-section
    // conversion uses the coordinator's own physical road anchor resolver.
    // Exercise that same code path with the real WorldPoseSmoke map.
    var openOmsiCoordinator =
        Activator.CreateInstance(
            openOmsiCoordinatorType,
            poseRootSource)
        ?? throw new InvalidOperationException(
            "openOMSI physical vehicle coordinator could not be created.");
    var applyOpenOmsiWorldAnchor =
        openOmsiCoordinatorType.GetMethod(
            "ApplyOpenOmsiWorldAnchor",
            BindingFlags.NonPublic |
            BindingFlags.Instance)
        ?? throw new InvalidOperationException(
            "openOMSI -> OMSI2 pose converter not found.");
    var convertedFrame =
        (PlayerTelemetryFrame)(
            applyOpenOmsiWorldAnchor.Invoke(
                openOmsiCoordinator,
                [openOmsiFrame, anchorValue])
            ?? throw new InvalidOperationException(
                "openOMSI -> OMSI2 pose converter returned null."));
    var convertedTelemetry =
        convertedFrame.Telemetry;

    Near(
        convertedTelemetry.X,
        325.0,
        0.001,
        "converted OMSI world X");
    Near(
        convertedTelemetry.Y,
        1.5,
        0.001,
        "converted OMSI vertical Y");
    Near(
        convertedTelemetry.Z,
        442.0,
        0.001,
        "converted OMSI world Z");
    Require(
        convertedTelemetry.PhysicalGridX == 1 &&
        convertedTelemetry.PhysicalGridY == 1,
        "Converted openOMSI pose lost physical OMSI GridX/GridY.");
    Near(
        convertedTelemetry.LocalX ?? double.NaN,
        25.0,
        0.001,
        "converted OMSI LocalX");
    Near(
        convertedTelemetry.LocalY ?? double.NaN,
        1.5,
        0.001,
        "converted OMSI LocalY");
    Near(
        convertedTelemetry.LocalZ ?? double.NaN,
        142.0,
        0.001,
        "converted OMSI LocalZ");

    var headingHalfRadians =
        45.0 * Math.PI / 360.0;
    Near(
        convertedTelemetry.RotationX ??
            double.NaN,
        0.0,
        0.000001,
        "converted rotation X");
    Near(
        convertedTelemetry.RotationY ??
            double.NaN,
        Math.Sin(
            headingHalfRadians),
        0.000001,
        "converted rotation Y");
    Near(
        convertedTelemetry.RotationZ ??
            double.NaN,
        0.0,
        0.000001,
        "converted rotation Z");
    Near(
        convertedTelemetry.RotationW ??
            double.NaN,
        Math.Cos(
            headingHalfRadians),
        0.000001,
        "converted rotation W");

    var outsideTileTelemetry =
        openOmsiWorldTelemetry with
        {
            X = 625.0,
            Y = 442.0
        };
    object?[] outsideArguments =
        [outsideTileTelemetry, null];
    Require(
        !(bool)(resolveOpenOmsiWorldAnchor.Invoke(
            roadResolver,
            outsideArguments)
            ?? false),
        "openOMSI world-pose resolver accepted a world point outside loaded OMSI tiles.");
}
finally
{
    Directory.Delete(
        openOmsiPoseRoot,
        recursive: true);
}

await using var server = new OmsiPluginBridgeServer();
server.Start();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await using var pipe = new NamedPipeClientStream(
    ".",
    PluginBridgeProtocol.PipeName,
    PipeDirection.InOut,
    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

await pipe.ConnectAsync(5_000, cts.Token);

using var reader = new StreamReader(
    pipe,
    new UTF8Encoding(false),
    detectEncodingFromByteOrderMarks: false,
    bufferSize: 4096,
    leaveOpen: true);
using var writer = new StreamWriter(
    pipe,
    new UTF8Encoding(false),
    bufferSize: 4096,
    leaveOpen: true)
{
    AutoFlush = true
};

var pluginHello = new PluginBridgeMessage(
    PluginBridgeProtocol.PluginHello,
    PluginBridgeProtocol.Version,
    ProcessId: 4242,
    ComponentVersion: "smoke-test");

await writer.WriteLineAsync(JsonSerializer.Serialize(pluginHello));

var helloLine = await reader.ReadLineAsync(cts.Token);
var clientHello = JsonSerializer.Deserialize<PluginBridgeMessage>(
    helloLine ?? throw new InvalidOperationException("client-hello not received"));

Require(clientHello is not null, "client-hello could not be parsed");
Require(clientHello!.Type == PluginBridgeProtocol.ClientHello, "unexpected client hello type");
Require(clientHello.ProtocolVersion == PluginBridgeProtocol.Version, "protocol version mismatch");

var pluginStatus = new PluginBridgeMessage(
    PluginBridgeProtocol.PluginStatus,
    PluginBridgeProtocol.Version,
    ProcessId: 4242,
    ComponentVersion: "smoke-test",
    TimestampUnixMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    SpeedKph: 37.5,
    SystemVariableCallbacks: 123,
    RemoteVehicleCount: 2,
    CompatibleRemoteVehicleCount: 1,
    StaleRemovedCount: 3,
    LastSystemVariableIndex: 7,
    StopRequested: true,
    Capabilities:
    [
        PluginBridgeProtocol.CapabilityAdvancedTelemetry,
        PluginBridgeProtocol.CapabilityCharacterPossession,
        PluginBridgeProtocol.CapabilityCharacterTransform,
        PluginBridgeProtocol.CapabilityPhysicalMultiplayerV25
    ]);

await writer.WriteLineAsync(JsonSerializer.Serialize(pluginStatus));

OmsiPluginBridgeConnectionInfo? info = null;
for (var attempt = 0; attempt < 30; attempt++)
{
    info = server.GetConnectionInfo();
    if (info.LastStatus is not null)
    {
        break;
    }

    await Task.Delay(100, cts.Token);
}

Require(info is not null, "bridge info was not available");
Require(info!.IsConnected, "bridge did not report connected state");
Require(info.PluginProcessId == 4242, "plugin PID was not retained from handshake");
Require(info.PluginComponentVersion == "smoke-test", "plugin version was not retained");
Require(info.LastStatus is not null, "plugin-status was not stored");
Require(info.LastStatus!.SystemVariableCallbacks == 123, "callback count mismatch");
Require(info.LastStatus.RemoteVehicleCount == 2, "remote count mismatch");
Require(info.LastStatus.CompatibleRemoteVehicleCount == 1, "compatible remote count mismatch");
Require(info.LastStatus.StaleRemovedCount == 3, "stale count mismatch");
Require(info.LastStatus.LastSystemVariableIndex == 7, "system variable index mismatch");
Require(info.LastStatus.SpeedKph == 37.5, "plugin Velocity status mismatch");
Require(info.LastStatus.StopRequested == true, "stop request status mismatch");
Require(server.SupportsCapability(PluginBridgeProtocol.CapabilityCharacterPossession),
    "runtime character-possession capability was not refreshed from plugin status");
Require(server.SupportsCapability(PluginBridgeProtocol.CapabilityCharacterTransform),
    "runtime character-transform capability was not refreshed from plugin status");
Require(server.SupportsCapability(PluginBridgeProtocol.CapabilityPhysicalMultiplayerV25),
    "runtime physical-multiplayer-v25 capability was not refreshed from plugin status");

Require(
    PluginBridgeProtocol.IsRecoverablePhysicalMotionError(
        PluginBridgeProtocol.ErrorMotionReadbackUnavailable),
    "motion readback unavailable must remain recoverable");
Require(
    PluginBridgeProtocol.IsRecoverablePhysicalMotionError(
        PluginBridgeProtocol.ErrorMotionTransformMismatch),
    "motion transform mismatch must remain recoverable");
Require(
    PluginBridgeProtocol.IsRecoverablePhysicalMotionError(
        PluginBridgeProtocol.ErrorMotionTileMismatch),
    "motion tile mismatch must remain recoverable");
Require(
    PluginBridgeProtocol.IsRecoverablePhysicalMotionError(
        PluginBridgeProtocol.ErrorMotionWorldOriginUnavailable),
    "Kachel world-origin wait must remain recoverable");
Require(
    !PluginBridgeProtocol.IsRecoverablePhysicalMotionError("vehicle-not-owned"),
    "ownership failures must not be classified as recoverable motion drift");

var motionVectorMessage = new PluginBridgeMessage(
    PluginBridgeProtocol.UpdateRemoteVehicle,
    PluginBridgeProtocol.Version,
    PhysicalGridX: 17,
    PhysicalGridY: -23,
    VelocityX: 1.25,
    VelocityY: -0.5,
    VelocityZ: 3.75,
    AccelerationLocalX: 0.1,
    AccelerationLocalY: 0.2,
    AccelerationLocalZ: -0.3);
var motionVectorJson = JsonSerializer.Serialize(motionVectorMessage);
var motionVectorRoundTrip =
    JsonSerializer.Deserialize<PluginBridgeMessage>(motionVectorJson);
Require(motionVectorRoundTrip?.PhysicalGridX == 17, "physical grid X did not round-trip");
Require(motionVectorRoundTrip?.PhysicalGridY == -23, "physical grid Y did not round-trip");
Require(motionVectorRoundTrip?.VelocityX == 1.25, "velocity X did not round-trip");
Require(motionVectorRoundTrip?.VelocityY == -0.5, "velocity Y did not round-trip");
Require(motionVectorRoundTrip?.VelocityZ == 3.75, "velocity Z did not round-trip");
Require(motionVectorRoundTrip?.AccelerationLocalX == 0.1, "Acc_Local X did not round-trip");
Require(motionVectorRoundTrip?.AccelerationLocalY == 0.2, "Acc_Local Y did not round-trip");
Require(motionVectorRoundTrip?.AccelerationLocalZ == -0.3, "Acc_Local Z did not round-trip");

var spoofedStatus = pluginStatus with
{
    ProcessId = 9999,
    TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    SystemVariableCallbacks = 999
};
await writer.WriteLineAsync(JsonSerializer.Serialize(spoofedStatus));
await Task.Delay(250, cts.Token);

var afterSpoof = server.GetConnectionInfo();
Require(afterSpoof.LastStatus?.SystemVariableCallbacks == 123,
    "runtime status from a PID different from the handshake was accepted");

var invalidCountersStatus = pluginStatus with
{
    TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    SystemVariableCallbacks = 777,
    RemoteVehicleCount = -1
};
await writer.WriteLineAsync(JsonSerializer.Serialize(invalidCountersStatus));
await Task.Delay(250, cts.Token);

var afterInvalidCounters = server.GetConnectionInfo();
Require(afterInvalidCounters.LastStatus?.SystemVariableCallbacks == 123,
    "runtime status with invalid counters was accepted");

var localVarsConfig = new PluginBridgeMessage(
    PluginBridgeProtocol.ConfigureLocalVehicleVars,
    PluginBridgeProtocol.Version,
    VarTableHash: 0x1234ABCDu,
    VariableIndices: new ushort[] { 2, 5, 9 },
    StringVariableIndices: new ushort[] { 1, 6 });
var localVarsConfigRead = reader.ReadLineAsync(cts.Token).AsTask();
await server.SendMessageAsync(localVarsConfig, cts.Token);
var localVarsConfigLine = await localVarsConfigRead;
var receivedLocalVarsConfig =
    JsonSerializer.Deserialize<PluginBridgeMessage>(
        localVarsConfigLine ??
        throw new InvalidOperationException(
            "local VARS configuration not received"));
Require(
    receivedLocalVarsConfig?.Type ==
        PluginBridgeProtocol.ConfigureLocalVehicleVars,
    "unexpected local VARS configuration message type");
Require(
    receivedLocalVarsConfig?.VarTableHash == 0x1234ABCDu,
    "local VARS configuration hash mismatch");
Require(
    receivedLocalVarsConfig?.VariableIndices?.SequenceEqual(
        new ushort[] { 2, 5, 9 }) == true,
    "local VARS configuration ids mismatch");
Require(
    receivedLocalVarsConfig?.StringVariableIndices?.SequenceEqual(
        new ushort[] { 1, 6 }) == true,
    "local VARS configuration string ids mismatch");

var remoteVarsMessage = new PluginBridgeMessage(
    PluginBridgeProtocol.RemoteVehicleVars,
    PluginBridgeProtocol.Version,
    PlayerId: "remote-vars-smoke",
    VarTableHash: 0x89ABCDEFu,
    VariableIndices: new ushort[] { 3, 7 },
    VariableValues: new float[] { 1.25f, -0.5f },
    StringVariableIndices: new ushort[] { 2 },
    StringVariableValues: new[] { "Linha 875A" });
var remoteVarsRead = reader.ReadLineAsync(cts.Token).AsTask();
await server.SendMessageAsync(remoteVarsMessage, cts.Token);
var remoteVarsLine = await remoteVarsRead;
var receivedRemoteVars =
    JsonSerializer.Deserialize<PluginBridgeMessage>(
        remoteVarsLine ??
        throw new InvalidOperationException(
            "remote VARS message not received"));
Require(
    receivedRemoteVars?.Type ==
        PluginBridgeProtocol.RemoteVehicleVars,
    "unexpected remote VARS message type");
Require(
    receivedRemoteVars?.PlayerId == "remote-vars-smoke" &&
    receivedRemoteVars.VarTableHash == 0x89ABCDEFu,
    "remote VARS identity/hash mismatch");
Require(
    receivedRemoteVars.VariableIndices?.SequenceEqual(
        new ushort[] { 3, 7 }) == true &&
    receivedRemoteVars.VariableValues?.SequenceEqual(
        new float[] { 1.25f, -0.5f }) == true &&
    receivedRemoteVars.StringVariableIndices?.SequenceEqual(
        new ushort[] { 2 }) == true &&
    receivedRemoteVars.StringVariableValues?.SequenceEqual(
        new[] { "Linha 875A" }) == true,
    "remote VARS payload mismatch");

var remoteVisualState = new PluginBridgeMessage(
    PluginBridgeProtocol.RemoteVehicleState,
    PluginBridgeProtocol.Version,
    PlayerId: "remote-visual-smoke",
    SyncTableHash: 0x13572468u,
    SyncLamps: new float[] { 1f, 0.25f },
    SyncSwitches: new float[] { 0.75f },
    SyncValues: new float[] { 12.5f, -3f },
    SyncDoors: new float[] { 1f, 0f },
    SyncLampVariableIndices: new ushort[] { 10, 11 },
    SyncSwitchVariableIndices: new ushort[] { 20 },
    SyncValueVariableIndices: new ushort[] { 30, 31 },
    SyncDoorVariableIndices: new ushort[] { 40, 41 });
var remoteVisualRead = reader.ReadLineAsync(cts.Token).AsTask();
await server.SendMessageAsync(remoteVisualState, cts.Token);
var remoteVisualLine = await remoteVisualRead;
var receivedRemoteVisual =
    JsonSerializer.Deserialize<PluginBridgeMessage>(
        remoteVisualLine ??
        throw new InvalidOperationException(
            "remote visual SyncTable state not received"));
Require(
    receivedRemoteVisual?.Type ==
        PluginBridgeProtocol.RemoteVehicleState &&
    receivedRemoteVisual.PlayerId == "remote-visual-smoke" &&
    receivedRemoteVisual.SyncTableHash == 0x13572468u,
    "remote visual SyncTable identity/hash mismatch");
Require(
    receivedRemoteVisual.SyncLamps?.SequenceEqual(
        new float[] { 1f, 0.25f }) == true &&
    receivedRemoteVisual.SyncSwitches?.SequenceEqual(
        new float[] { 0.75f }) == true &&
    receivedRemoteVisual.SyncValues?.SequenceEqual(
        new float[] { 12.5f, -3f }) == true &&
    receivedRemoteVisual.SyncDoors?.SequenceEqual(
        new float[] { 1f, 0f }) == true,
    "remote visual SyncTable values mismatch");
Require(
    receivedRemoteVisual.SyncLampVariableIndices?.SequenceEqual(
        new ushort[] { 10, 11 }) == true &&
    receivedRemoteVisual.SyncSwitchVariableIndices?.SequenceEqual(
        new ushort[] { 20 }) == true &&
    receivedRemoteVisual.SyncValueVariableIndices?.SequenceEqual(
        new ushort[] { 30, 31 }) == true &&
    receivedRemoteVisual.SyncDoorVariableIndices?.SequenceEqual(
        new ushort[] { 40, 41 }) == true,
    "remote visual SyncTable ids mismatch");

LocalOmsiScriptVarsSnapshotStore.Clear();
var localVarsSnapshot = new PluginBridgeMessage(
    PluginBridgeProtocol.LocalVehicleVars,
    PluginBridgeProtocol.Version,
    ProcessId: 4242,
    TimestampUnixMilliseconds:
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    VarTableHash: 0xCAFEBABEu,
    VariableIndices: new ushort[] { 1, 4 },
    VariableValues: new float[] { 0.75f, 22.5f },
    StringVariableIndices: new ushort[] { 0, 3 },
    StringVariableValues: new[] { "NBR-1001", "Centro" });
await writer.WriteLineAsync(
    JsonSerializer.Serialize(localVarsSnapshot));

LocalOmsiScriptVarsSnapshot? storedLocalVars = null;
for (var attempt = 0; attempt < 30; attempt++)
{
    storedLocalVars = LocalOmsiScriptVarsSnapshotStore.Latest;
    if (storedLocalVars is not null)
    {
        break;
    }

    await Task.Delay(50, cts.Token);
}
Require(
    storedLocalVars is not null,
    "local VARS snapshot was not stored by the bridge");
Require(
    storedLocalVars!.VarTableHash == 0xCAFEBABEu &&
    storedLocalVars.VariableIndices.SequenceEqual(
        new ushort[] { 1, 4 }) &&
    storedLocalVars.VariableValues.SequenceEqual(
        new float[] { 0.75f, 22.5f }) &&
    storedLocalVars.StringVariableIndices.SequenceEqual(
        new ushort[] { 0, 3 }) &&
    storedLocalVars.StringVariableValues.SequenceEqual(
        new[] { "NBR-1001", "Centro" }),
    "local VARS snapshot contents mismatch");

// A missing OMSI StringVar must not prevent valid PublicVars from crossing
// the real plugin pipe; conversely, a vehicle may expose only StringVars.
LocalOmsiScriptVarsSnapshotStore.Clear();
var floatsOnlyVars = localVarsSnapshot with
{
    TimestampUnixMilliseconds =
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    VariableIndices = new ushort[] { 4 },
    VariableValues = new float[] { 12.5f },
    StringVariableIndices = [],
    StringVariableValues = []
};
await writer.WriteLineAsync(JsonSerializer.Serialize(floatsOnlyVars));
LocalOmsiScriptVarsSnapshot? floatsOnlyStored = null;
for (var attempt = 0; attempt < 30; attempt++)
{
    floatsOnlyStored = LocalOmsiScriptVarsSnapshotStore.Latest;
    if (floatsOnlyStored is not null)
    {
        break;
    }

    await Task.Delay(50, cts.Token);
}
Require(
    floatsOnlyStored is not null &&
    floatsOnlyStored.VariableIndices.SequenceEqual(new ushort[] { 4 }) &&
    floatsOnlyStored.VariableValues.SequenceEqual(new float[] { 12.5f }) &&
    floatsOnlyStored.StringVariableIndices.Length == 0 &&
    floatsOnlyStored.StringVariableValues.Length == 0,
    "partial local VARS lost valid floats when StringVars were unavailable");

LocalOmsiScriptVarsSnapshotStore.Clear();
var stringsOnlyVars = localVarsSnapshot with
{
    TimestampUnixMilliseconds =
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    VariableIndices = [],
    VariableValues = [],
    StringVariableIndices = new ushort[] { 3 },
    StringVariableValues = new[] { "Centro" }
};
await writer.WriteLineAsync(JsonSerializer.Serialize(stringsOnlyVars));
LocalOmsiScriptVarsSnapshot? stringsOnlyStored = null;
for (var attempt = 0; attempt < 30; attempt++)
{
    stringsOnlyStored = LocalOmsiScriptVarsSnapshotStore.Latest;
    if (stringsOnlyStored is not null)
    {
        break;
    }

    await Task.Delay(50, cts.Token);
}
Require(
    stringsOnlyStored is not null &&
    stringsOnlyStored.VariableIndices.Length == 0 &&
    stringsOnlyStored.VariableValues.Length == 0 &&
    stringsOnlyStored.StringVariableIndices.SequenceEqual(new ushort[] { 3 }) &&
    stringsOnlyStored.StringVariableValues.SequenceEqual(new[] { "Centro" }),
    "partial local VARS lost valid strings when PublicVars were unavailable");

var trafficVehicle = new TrafficVehicleState(
    "traffic-17",
    "Vehicles\\MAN_NL_NG\\MAN_NL263.bus",
    "sha256:test-vehicle",
    1234.5,
    2345.6,
    12.3,
    34.5,
    45.6,
    12.3,
    0,
    0,
    0,
    1,
    37.5,
    LightFlags: 3,
    TurnSignal: 1);

var trafficMessage = new PluginBridgeMessage(
    PluginBridgeProtocol.TrafficSnapshotState,
    PluginBridgeProtocol.Version,
    MapName: "Berlin-Spandau",
    MapCompatibilityId: "sha256:test-map",
    TimestampUnixMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    AuthorityPlayerId: "host-player",
    Sequence: 42,
    TrafficVehicles: [trafficVehicle]);

var trafficRead = reader.ReadLineAsync(cts.Token).AsTask();
await server.SendMessageAsync(trafficMessage, cts.Token);
var trafficLine = await trafficRead;
var receivedTraffic = JsonSerializer.Deserialize<PluginBridgeMessage>(
    trafficLine ?? throw new InvalidOperationException("traffic snapshot not received"));

Require(receivedTraffic?.Type == PluginBridgeProtocol.TrafficSnapshotState,
    "unexpected traffic message type");
Require(receivedTraffic?.AuthorityPlayerId == "host-player", "traffic authority mismatch");
Require(receivedTraffic?.Sequence == 42, "traffic sequence mismatch");
Require(receivedTraffic?.TrafficVehicles?.Length == 1, "traffic vehicle count mismatch");
Require(receivedTraffic?.TrafficVehicles?[0].TrafficId == "traffic-17", "traffic vehicle id mismatch");

var clearTraffic = new PluginBridgeMessage(
    PluginBridgeProtocol.ClearTrafficVehicles,
    PluginBridgeProtocol.Version);
var clearRead = reader.ReadLineAsync(cts.Token).AsTask();
await server.SendMessageAsync(clearTraffic, cts.Token);
var clearLine = await clearRead;
var receivedClear = JsonSerializer.Deserialize<PluginBridgeMessage>(
    clearLine ?? throw new InvalidOperationException("traffic clear not received"));
Require(receivedClear?.Type == PluginBridgeProtocol.ClearTrafficVehicles,
    "traffic clear message type mismatch");

Console.WriteLine("Plugin bridge v3 smoke test passed: handshake + status + local/remote VARS + traffic delivery + rejection checks.");

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void Near(
    double actual,
    double expected,
    double tolerance,
    string label)
{
    if (!double.IsFinite(actual) ||
        !double.IsFinite(expected) ||
        !double.IsFinite(tolerance) ||
        tolerance < 0d ||
        Math.Abs(actual - expected) > tolerance)
    {
        throw new InvalidOperationException(
            $"{label} mismatch: expected {expected}, got {actual} (tolerance {tolerance}).");
    }
}
