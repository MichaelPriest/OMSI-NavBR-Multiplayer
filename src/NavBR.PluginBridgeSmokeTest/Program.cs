using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

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
    var applyOpenOmsiWorldAnchor =
        openOmsiCoordinatorType.GetMethod(
            "ApplyOpenOmsiWorldAnchor",
            BindingFlags.NonPublic |
            BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "openOMSI -> OMSI2 pose converter not found.");
    var convertedFrame =
        (PlayerTelemetryFrame)(
            applyOpenOmsiWorldAnchor.Invoke(
                null,
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

Console.WriteLine("Plugin bridge v3 smoke test passed: handshake + runtime capability refresh + status + traffic delivery + rejection checks.");

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
