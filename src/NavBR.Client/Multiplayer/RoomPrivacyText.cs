using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

internal static class RoomPrivacyText
{
    public static string PrivateRoomLabel => Pick(
        "Criar sala privada",
        "Create private room",
        "Crear sala privada",
        "Privaten Raum erstellen",
        "Créer un salon privé");

    public static string PrivacyDescription => Pick(
        "Ao criar, exige senha para novos participantes. A senha fica apenas nesta sessão e não é incluída no convite.",
        "When creating, require a password for new participants. The password stays only in this session and is not included in the invite.",
        "Al crear, exige contraseña a los nuevos participantes. La contraseña queda solo en esta sesión y no se incluye en la invitación.",
        "Beim Erstellen ist für neue Teilnehmer ein Passwort erforderlich. Es bleibt nur in dieser Sitzung und wird nicht in die Einladung aufgenommen.",
        "À la création, un mot de passe est demandé aux nouveaux participants. Il reste uniquement dans cette session et n’est pas inclus dans l’invitation.");

    public static string PasswordLabel => Pick(
        "Senha da sala",
        "Room password",
        "Contraseña de la sala",
        "Raumpasswort",
        "Mot de passe du salon");

    public static string PasswordHint => Pick(
        "Use 4–128 caracteres para criar uma sala privada. Para entrar em uma sala pública, deixe vazio.",
        "Use 4–128 characters to create a private room. Leave it empty when joining a public room.",
        "Use 4–128 caracteres para crear una sala privada. Déjelo vacío al entrar en una sala pública.",
        "Für einen privaten Raum 4–128 Zeichen verwenden. Beim Beitritt zu einem öffentlichen Raum leer lassen.",
        "Utilisez 4 à 128 caractères pour créer un salon privé. Laissez vide pour rejoindre un salon public.");

    public static string DraftPublic => Pick(
        "Próxima criação: sala pública.",
        "Next creation: public room.",
        "Próxima creación: sala pública.",
        "Nächste Erstellung: öffentlicher Raum.",
        "Prochaine création : salon public.");

    public static string DraftPrivate => Pick(
        "Próxima criação: sala privada protegida por senha.",
        "Next creation: password-protected private room.",
        "Próxima creación: sala privada protegida por contraseña.",
        "Nächste Erstellung: passwortgeschützter privater Raum.",
        "Prochaine création : salon privé protégé par mot de passe.");

    public static string ConnectedPrivate => Pick(
        "Sala atual: privada • acesso por senha.",
        "Current room: private • password access.",
        "Sala actual: privada • acceso con contraseña.",
        "Aktueller Raum: privat • Passwortzugang.",
        "Salon actuel : privé • accès par mot de passe.");

    public static string ConnectedPublic => Pick(
        "Sala atual: pública.",
        "Current room: public.",
        "Sala actual: pública.",
        "Aktueller Raum: öffentlich.",
        "Salon actuel : public.");

    public static string PasswordTooShort => Pick(
        "Para criar uma sala privada, informe uma senha com pelo menos 4 caracteres.",
        "To create a private room, enter a password with at least 4 characters.",
        "Para crear una sala privada, escriba una contraseña de al menos 4 caracteres.",
        "Zum Erstellen eines privaten Raums ein Passwort mit mindestens 4 Zeichen eingeben.",
        "Pour créer un salon privé, saisissez un mot de passe d’au moins 4 caractères.");

    public static string TransportNotice => Pick(
        "A senha controla a entrada na sala; em peer-host HTTP ela não substitui criptografia de transporte.",
        "The password controls room entry; on HTTP peer-hosting it does not replace transport encryption.",
        "La contraseña controla la entrada; en alojamiento peer HTTP no sustituye el cifrado de transporte.",
        "Das Passwort steuert den Raumzugang; bei HTTP-Peer-Hosting ersetzt es keine Transportverschlüsselung.",
        "Le mot de passe contrôle l’accès au salon ; en hébergement pair HTTP, il ne remplace pas le chiffrement du transport.");

    public static string DescribeServerError(string message)
    {
        if (Contains(message, "NAVBR_ROOM_PASSWORD_REQUIRED"))
        {
            return Pick(
                "Esta sala é privada. Informe a senha para entrar.",
                "This room is private. Enter its password to join.",
                "Esta sala es privada. Introduzca la contraseña para entrar.",
                "Dieser Raum ist privat. Zum Beitreten das Passwort eingeben.",
                "Ce salon est privé. Saisissez le mot de passe pour le rejoindre.");
        }

        if (Contains(message, "NAVBR_ROOM_PASSWORD_INVALID"))
        {
            return Pick(
                "Senha da sala incorreta.",
                "Incorrect room password.",
                "Contraseña de la sala incorrecta.",
                "Falsches Raumpasswort.",
                "Mot de passe du salon incorrect.");
        }

        if (Contains(message, "NAVBR_ROOM_PRIVATE_PASSWORD_REQUIRED"))
        {
            return PasswordTooShort;
        }

        if (Contains(message, "NAVBR_ROOM_ALREADY_PUBLIC"))
        {
            return Pick(
                "Já existe uma sala pública com esse identificador. Escolha outro nome para criar a privada.",
                "A public room already uses this identifier. Choose another name for the private room.",
                "Ya existe una sala pública con este identificador. Elija otro nombre para la sala privada.",
                "Unter dieser Kennung existiert bereits ein öffentlicher Raum. Für den privaten Raum einen anderen Namen wählen.",
                "Un salon public utilise déjà cet identifiant. Choisissez un autre nom pour le salon privé.");
        }

        if (Contains(message, "NAVBR_ROOM_POLICY_LIMIT"))
        {
            return Pick(
                "O host atingiu temporariamente o limite de salas. Tente novamente depois que salas vazias forem liberadas.",
                "The host temporarily reached the room limit. Try again after empty rooms are released.",
                "El host alcanzó temporalmente el límite de salas. Inténtelo de nuevo cuando se liberen salas vacías.",
                "Der Host hat vorübergehend das Raumlimit erreicht. Erneut versuchen, nachdem leere Räume freigegeben wurden.",
                "L’hôte a temporairement atteint la limite de salons. Réessayez après la libération de salons vides.");
        }

        if (Contains(message, "NAVBR_ROOM_INVALID_REQUEST") || Contains(message, "NAVBR_ROOM_ACCESS_DENIED"))
        {
            return Pick(
                "Não foi possível autorizar a entrada nessa sala.",
                "Room access could not be authorized.",
                "No se pudo autorizar el acceso a la sala.",
                "Der Raumzugang konnte nicht autorisiert werden.",
                "L’accès au salon n’a pas pu être autorisé.");
        }

        return message;
    }

    private static bool Contains(string text, string code) =>
        text.Contains(code, StringComparison.OrdinalIgnoreCase);

    private static string Pick(
        string pt,
        string en,
        string es,
        string de,
        string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
