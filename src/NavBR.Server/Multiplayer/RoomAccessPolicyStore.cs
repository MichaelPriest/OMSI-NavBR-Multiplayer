using System.Security.Cryptography;
using System.Text;

namespace NavBR.Server.Multiplayer;

internal sealed record RoomAccessDecision(
    bool Allowed,
    bool IsPrivate,
    string? OwnerPlayerId,
    string? ErrorCode = null);

internal sealed record RoomAccessDescriptor(
    bool IsPrivate,
    string? OwnerPlayerId);

internal sealed class RoomAccessPolicyStore
{
    private const int MaxPolicies = 2048;
    private const int PasswordIterations = 100_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private readonly object _sync = new();
    private readonly Dictionary<string, RoomPolicy> _policies =
        new(StringComparer.OrdinalIgnoreCase);

    public RoomAccessDecision Authorize(
        string roomId,
        string playerId,
        bool createPrivateRoom,
        string? password,
        MultiplayerRoomRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        lock (_sync)
        {
            PruneUnusedPoliciesUnsafe(registry);

            if (_policies.TryGetValue(roomId, out var existing))
            {
                if (createPrivateRoom && !existing.IsPrivate)
                {
                    return new RoomAccessDecision(
                        false,
                        false,
                        existing.OwnerPlayerId,
                        "NAVBR_ROOM_ALREADY_PUBLIC");
                }

                if (existing.IsPrivate)
                {
                    if (string.IsNullOrEmpty(password))
                    {
                        return new RoomAccessDecision(
                            false,
                            true,
                            existing.OwnerPlayerId,
                            "NAVBR_ROOM_PASSWORD_REQUIRED");
                    }

                    if (password.Length > 128 || !VerifyPassword(password, existing))
                    {
                        return new RoomAccessDecision(
                            false,
                            true,
                            existing.OwnerPlayerId,
                            "NAVBR_ROOM_PASSWORD_INVALID");
                    }
                }

                existing.PendingJoins++;
                existing.LastActivityUtc = DateTimeOffset.UtcNow;
                return new RoomAccessDecision(
                    true,
                    existing.IsPrivate,
                    existing.OwnerPlayerId);
            }

            if (_policies.Count >= MaxPolicies)
            {
                return new RoomAccessDecision(
                    false,
                    false,
                    null,
                    "NAVBR_ROOM_POLICY_LIMIT");
            }

            if (createPrivateRoom)
            {
                if (string.IsNullOrWhiteSpace(password) || password.Length is < 4 or > 128)
                {
                    return new RoomAccessDecision(
                        false,
                        true,
                        playerId,
                        "NAVBR_ROOM_PRIVATE_PASSWORD_REQUIRED");
                }

                var salt = RandomNumberGenerator.GetBytes(SaltBytes);
                var hash = HashPassword(password, salt);
                _policies[roomId] = new RoomPolicy(
                    isPrivate: true,
                    ownerPlayerId: playerId,
                    salt,
                    hash,
                    pendingJoins: 1);
                return new RoomAccessDecision(true, true, playerId);
            }

            _policies[roomId] = new RoomPolicy(
                isPrivate: false,
                ownerPlayerId: playerId,
                salt: null,
                passwordHash: null,
                pendingJoins: 1);
            return new RoomAccessDecision(true, false, playerId);
        }
    }

    public void CompleteAuthorization(string roomId, MultiplayerRoomRegistry registry)
    {
        lock (_sync)
        {
            if (!_policies.TryGetValue(roomId, out var policy))
            {
                return;
            }

            if (policy.PendingJoins > 0)
            {
                policy.PendingJoins--;
            }
            policy.LastActivityUtc = DateTimeOffset.UtcNow;
            CleanupIfEmptyUnsafe(roomId, registry);
        }
    }

    public void CleanupIfEmpty(string roomId, MultiplayerRoomRegistry registry)
    {
        lock (_sync)
        {
            CleanupIfEmptyUnsafe(roomId, registry);
        }
    }

    public RoomAccessDescriptor Describe(string roomId, MultiplayerRoomRegistry registry)
    {
        lock (_sync)
        {
            if (!_policies.TryGetValue(roomId, out var policy))
            {
                return new RoomAccessDescriptor(false, null);
            }

            var players = registry.GetRoomPlayers(roomId);
            if (players.Count > 0 &&
                !players.Any(player => string.Equals(
                    player.PlayerId,
                    policy.OwnerPlayerId,
                    StringComparison.OrdinalIgnoreCase)))
            {
                policy.OwnerPlayerId = players[0].PlayerId;
            }

            policy.LastActivityUtc = DateTimeOffset.UtcNow;
            return new RoomAccessDescriptor(policy.IsPrivate, policy.OwnerPlayerId);
        }
    }

    private void CleanupIfEmptyUnsafe(string roomId, MultiplayerRoomRegistry registry)
    {
        if (!_policies.TryGetValue(roomId, out var policy) || policy.PendingJoins > 0)
        {
            return;
        }

        if (registry.GetRoomPlayers(roomId).Count == 0)
        {
            _policies.Remove(roomId);
        }
    }

    private void PruneUnusedPoliciesUnsafe(MultiplayerRoomRegistry registry)
    {
        if (_policies.Count < MaxPolicies / 2)
        {
            return;
        }

        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(30);
        var stale = _policies
            .Where(pair =>
                pair.Value.PendingJoins == 0 &&
                pair.Value.LastActivityUtc < cutoff &&
                registry.GetRoomPlayers(pair.Key).Count == 0)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var roomId in stale)
        {
            _policies.Remove(roomId);
        }
    }

    private static bool VerifyPassword(string password, RoomPolicy policy)
    {
        if (policy.Salt is null || policy.PasswordHash is null)
        {
            return false;
        }

        var candidate = HashPassword(password, policy.Salt);
        return CryptographicOperations.FixedTimeEquals(candidate, policy.PasswordHash);
    }

    private static byte[] HashPassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            HashBytes);

    private sealed class RoomPolicy
    {
        public RoomPolicy(
            bool isPrivate,
            string ownerPlayerId,
            byte[]? salt,
            byte[]? passwordHash,
            int pendingJoins)
        {
            IsPrivate = isPrivate;
            OwnerPlayerId = ownerPlayerId;
            Salt = salt;
            PasswordHash = passwordHash;
            PendingJoins = pendingJoins;
            LastActivityUtc = DateTimeOffset.UtcNow;
        }

        public bool IsPrivate { get; }
        public string OwnerPlayerId { get; set; }
        public byte[]? Salt { get; }
        public byte[]? PasswordHash { get; }
        public int PendingJoins { get; set; }
        public DateTimeOffset LastActivityUtc { get; set; }
    }
}
