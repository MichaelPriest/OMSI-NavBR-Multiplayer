using System.Net.Http;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

public enum MultiplayerNetworkErrorCode
{
    Unknown,
    InvalidServerUrl,
    HostPortInUse,
    ConnectionRefused,
    DnsResolutionFailed,
    Timeout,
    HostUnavailable,
    FirewallBlocked,
    UpnpUnavailable,
    UpnpMappingFailed
}

public sealed class MultiplayerNetworkException : Exception
{
    public MultiplayerNetworkException(
        MultiplayerNetworkErrorCode code,
        string technicalMessage,
        Exception? innerException = null)
        : base(NetworkErrorText.Describe(code, technicalMessage), innerException)
    {
        Code = code;
        TechnicalMessage = technicalMessage;
    }

    public MultiplayerNetworkErrorCode Code { get; }
    public string TechnicalMessage { get; }
}

internal static class MultiplayerNetworkErrorClassifier
{
    public static MultiplayerNetworkException WrapConnection(Exception error)
    {
        if (error is MultiplayerNetworkException known)
        {
            return known;
        }

        var root = Unwrap(error);
        var code = root switch
        {
            ArgumentException => MultiplayerNetworkErrorCode.InvalidServerUrl,
            TimeoutException => MultiplayerNetworkErrorCode.Timeout,
            TaskCanceledException => MultiplayerNetworkErrorCode.Timeout,
            SocketException socket when socket.SocketErrorCode == SocketError.ConnectionRefused
                => MultiplayerNetworkErrorCode.ConnectionRefused,
            SocketException socket when socket.SocketErrorCode == SocketError.HostNotFound ||
                                      socket.SocketErrorCode == SocketError.NoData
                => MultiplayerNetworkErrorCode.DnsResolutionFailed,
            SocketException socket when socket.SocketErrorCode == SocketError.TimedOut
                => MultiplayerNetworkErrorCode.Timeout,
            HttpRequestException http when http.InnerException is SocketException socket &&
                                           socket.SocketErrorCode == SocketError.ConnectionRefused
                => MultiplayerNetworkErrorCode.ConnectionRefused,
            HttpRequestException http when http.InnerException is SocketException socket &&
                                           (socket.SocketErrorCode == SocketError.HostNotFound ||
                                            socket.SocketErrorCode == SocketError.NoData)
                => MultiplayerNetworkErrorCode.DnsResolutionFailed,
            HttpRequestException => MultiplayerNetworkErrorCode.HostUnavailable,
            _ => MultiplayerNetworkErrorCode.Unknown
        };

        return new MultiplayerNetworkException(code, root.Message, error);
    }

    public static MultiplayerNetworkException WrapHost(Exception error, int port)
    {
        if (error is MultiplayerNetworkException known)
        {
            return known;
        }

        var root = Unwrap(error);
        var message = root.Message;
        var addressInUse = root is SocketException socket && socket.SocketErrorCode == SocketError.AddressAlreadyInUse ||
                           message.Contains("address already in use", StringComparison.OrdinalIgnoreCase) ||
                           message.Contains("endereço já está sendo usado", StringComparison.OrdinalIgnoreCase) ||
                           message.Contains($":{port}", StringComparison.OrdinalIgnoreCase) &&
                           message.Contains("bind", StringComparison.OrdinalIgnoreCase);

        return new MultiplayerNetworkException(
            addressInUse ? MultiplayerNetworkErrorCode.HostPortInUse : MultiplayerNetworkErrorCode.HostUnavailable,
            message,
            error);
    }

    private static Exception Unwrap(Exception error)
    {
        var current = error;
        while (current.InnerException is not null &&
               (current is AggregateException ||
                current is HttpRequestException ||
                current is IOException ||
                current is ConnectionAbortedException))
        {
            current = current.InnerException;
        }
        return current;
    }
}

internal static class NetworkErrorText
{
    public static string Describe(MultiplayerNetworkErrorCode code, string? technicalMessage = null)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        var text = (language, code) switch
        {
            ("pt", MultiplayerNetworkErrorCode.InvalidServerUrl) => "O endereço do servidor é inválido. Use um endereço HTTP ou HTTPS.",
            ("pt", MultiplayerNetworkErrorCode.HostPortInUse) => "A porta TCP 27730 já está em uso por outro programa ou outra instância do NavBR.",
            ("pt", MultiplayerNetworkErrorCode.ConnectionRefused) => "O servidor recusou a conexão. Confirme se a sala está hospedada e se a porta está correta.",
            ("pt", MultiplayerNetworkErrorCode.DnsResolutionFailed) => "Não foi possível localizar o endereço do servidor na rede.",
            ("pt", MultiplayerNetworkErrorCode.Timeout) => "A conexão demorou demais para responder. Verifique rede, roteador e firewall.",
            ("pt", MultiplayerNetworkErrorCode.HostUnavailable) => "O servidor multiplayer não está acessível neste momento.",
            ("pt", MultiplayerNetworkErrorCode.FirewallBlocked) => "O Windows Firewall ainda pode estar bloqueando a porta do NavBR.",
            ("pt", MultiplayerNetworkErrorCode.UpnpUnavailable) => "UPnP não foi encontrado no roteador. O encaminhamento de porta pode precisar ser manual.",
            ("pt", MultiplayerNetworkErrorCode.UpnpMappingFailed) => "O roteador respondeu ao UPnP, mas não aceitou o encaminhamento da porta do NavBR.",

            ("es", MultiplayerNetworkErrorCode.InvalidServerUrl) => "La dirección del servidor no es válida. Usa una dirección HTTP o HTTPS.",
            ("es", MultiplayerNetworkErrorCode.HostPortInUse) => "El puerto TCP 27730 ya está siendo utilizado por otro programa o instancia de NavBR.",
            ("es", MultiplayerNetworkErrorCode.ConnectionRefused) => "El servidor rechazó la conexión. Comprueba que la sala esté alojada y que el puerto sea correcto.",
            ("es", MultiplayerNetworkErrorCode.DnsResolutionFailed) => "No se pudo localizar la dirección del servidor en la red.",
            ("es", MultiplayerNetworkErrorCode.Timeout) => "La conexión tardó demasiado en responder. Revisa la red, el router y el firewall.",
            ("es", MultiplayerNetworkErrorCode.HostUnavailable) => "El servidor multijugador no está disponible en este momento.",
            ("es", MultiplayerNetworkErrorCode.FirewallBlocked) => "El Firewall de Windows todavía puede estar bloqueando el puerto de NavBR.",
            ("es", MultiplayerNetworkErrorCode.UpnpUnavailable) => "No se encontró UPnP en el router. Puede ser necesario configurar el puerto manualmente.",
            ("es", MultiplayerNetworkErrorCode.UpnpMappingFailed) => "El router respondió a UPnP, pero no aceptó el mapeo del puerto de NavBR.",

            ("de", MultiplayerNetworkErrorCode.InvalidServerUrl) => "Die Serveradresse ist ungültig. Verwende eine HTTP- oder HTTPS-Adresse.",
            ("de", MultiplayerNetworkErrorCode.HostPortInUse) => "TCP-Port 27730 wird bereits von einem anderen Programm oder einer NavBR-Instanz verwendet.",
            ("de", MultiplayerNetworkErrorCode.ConnectionRefused) => "Der Server hat die Verbindung abgelehnt. Prüfe, ob der Raum gehostet wird und der Port stimmt.",
            ("de", MultiplayerNetworkErrorCode.DnsResolutionFailed) => "Die Serveradresse konnte im Netzwerk nicht aufgelöst werden.",
            ("de", MultiplayerNetworkErrorCode.Timeout) => "Die Verbindung hat zu lange nicht geantwortet. Prüfe Netzwerk, Router und Firewall.",
            ("de", MultiplayerNetworkErrorCode.HostUnavailable) => "Der Mehrspieler-Server ist derzeit nicht erreichbar.",
            ("de", MultiplayerNetworkErrorCode.FirewallBlocked) => "Die Windows-Firewall könnte den NavBR-Port weiterhin blockieren.",
            ("de", MultiplayerNetworkErrorCode.UpnpUnavailable) => "UPnP wurde am Router nicht gefunden. Eine manuelle Portfreigabe kann nötig sein.",
            ("de", MultiplayerNetworkErrorCode.UpnpMappingFailed) => "Der Router antwortet auf UPnP, hat die NavBR-Portfreigabe aber nicht akzeptiert.",

            ("fr", MultiplayerNetworkErrorCode.InvalidServerUrl) => "L’adresse du serveur est invalide. Utilisez une adresse HTTP ou HTTPS.",
            ("fr", MultiplayerNetworkErrorCode.HostPortInUse) => "Le port TCP 27730 est déjà utilisé par un autre programme ou une autre instance de NavBR.",
            ("fr", MultiplayerNetworkErrorCode.ConnectionRefused) => "Le serveur a refusé la connexion. Vérifiez que la salle est hébergée et que le port est correct.",
            ("fr", MultiplayerNetworkErrorCode.DnsResolutionFailed) => "Impossible de localiser l’adresse du serveur sur le réseau.",
            ("fr", MultiplayerNetworkErrorCode.Timeout) => "La connexion a mis trop de temps à répondre. Vérifiez le réseau, le routeur et le pare-feu.",
            ("fr", MultiplayerNetworkErrorCode.HostUnavailable) => "Le serveur multijoueur n’est pas accessible pour le moment.",
            ("fr", MultiplayerNetworkErrorCode.FirewallBlocked) => "Le pare-feu Windows peut encore bloquer le port NavBR.",
            ("fr", MultiplayerNetworkErrorCode.UpnpUnavailable) => "UPnP n’a pas été trouvé sur le routeur. Une redirection manuelle peut être nécessaire.",
            ("fr", MultiplayerNetworkErrorCode.UpnpMappingFailed) => "Le routeur répond à UPnP mais n’a pas accepté la redirection du port NavBR.",

            (_, MultiplayerNetworkErrorCode.InvalidServerUrl) => "The server address is invalid. Use an HTTP or HTTPS address.",
            (_, MultiplayerNetworkErrorCode.HostPortInUse) => "TCP port 27730 is already in use by another program or NavBR instance.",
            (_, MultiplayerNetworkErrorCode.ConnectionRefused) => "The server refused the connection. Check that the room is being hosted and the port is correct.",
            (_, MultiplayerNetworkErrorCode.DnsResolutionFailed) => "The server address could not be resolved on the network.",
            (_, MultiplayerNetworkErrorCode.Timeout) => "The connection took too long to respond. Check the network, router and firewall.",
            (_, MultiplayerNetworkErrorCode.HostUnavailable) => "The multiplayer server is not reachable right now.",
            (_, MultiplayerNetworkErrorCode.FirewallBlocked) => "Windows Firewall may still be blocking the NavBR port.",
            (_, MultiplayerNetworkErrorCode.UpnpUnavailable) => "UPnP was not found on the router. Manual port forwarding may be required.",
            (_, MultiplayerNetworkErrorCode.UpnpMappingFailed) => "The router answered UPnP but did not accept the NavBR port mapping.",
            _ => string.IsNullOrWhiteSpace(technicalMessage)
                ? "Multiplayer network error."
                : $"Multiplayer network error: {technicalMessage}"
        };

        return text;
    }
}
