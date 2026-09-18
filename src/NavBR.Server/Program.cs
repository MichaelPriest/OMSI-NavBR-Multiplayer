using NavBR.Server;

var renderPort = Environment.GetEnvironmentVariable("PORT");
var listenUrl = int.TryParse(renderPort, out var port) && port is > 0 and <= 65535
    ? $"http://0.0.0.0:{port}"
    : null;

var app = NavBRServerApplication.Build(args, listenUrl);
await app.RunAsync();
