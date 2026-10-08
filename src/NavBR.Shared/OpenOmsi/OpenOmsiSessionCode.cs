using System.Net;

namespace NavBR.Shared.OpenOmsi;

/// <summary>
/// openOMSI-compatible v6 session code. One-address codes keep the original
/// 24-character layout; current openOMSI codes may carry up to three IPv4
/// addresses so VPN/LAN candidates can be tried without a separate invite.
/// </summary>
public sealed record OpenOmsiSessionCode(
    byte Protocol,
    IPAddress Address,
    ushort Port,
    ulong Session)
{
    private const ushort LayoutMark = 0x4F43;
    private const ushort MultiLayoutMark = 0x4D41;
    private const ulong Mask48 = 0xFFFF_FFFF_FFFFUL;
    private const int MaxAddresses = 3;
    private static readonly ulong[] MixConstants =
    [
        0x9E37_79B9_7F4BUL,
        0xC2B2_AE3D_27D5UL
    ];
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// All IPv4 candidates carried by the code, best first. Existing callers
    /// that only set <see cref="Address"/> still encode exactly one address.
    /// </summary>
    public IReadOnlyList<IPAddress> Addresses { get; init; } =
        Array.Empty<IPAddress>();

    public IReadOnlyList<IPAddress> EffectiveAddresses
    {
        get
        {
            var source = Addresses.Count > 0
                ? Addresses
                : new[] { Address };

            var result = new List<IPAddress>(MaxAddresses);
            foreach (var address in source)
            {
                var ipv4 = address.MapToIPv4();
                if (result.Any(existing => existing.Equals(ipv4)))
                {
                    continue;
                }

                result.Add(ipv4);
                if (result.Count == MaxAddresses)
                {
                    break;
                }
            }

            if (result.Count == 0)
            {
                result.Add(IPAddress.Loopback);
            }

            return result;
        }
    }

    public IReadOnlyList<IPEndPoint> Endpoints =>
        EffectiveAddresses
            .Select(address => new IPEndPoint(address, Port))
            .ToArray();

    public string Encode()
    {
        var ips = EffectiveAddresses;
        var mixed = Mix48(Session & Mask48);
        Span<byte> sid = stackalloc byte[8];
        WriteUInt64BigEndian(sid, mixed);

        var body = new List<byte>(11 + 4 * ips.Count);
        body.AddRange(sid[2..8].ToArray());

        var plain = new List<byte>(5 + 4 * ips.Count)
        {
            Protocol
        };

        ushort mark;
        if (ips.Count == 1)
        {
            plain.AddRange(ips[0].GetAddressBytes());
            plain.Add((byte)(Port >> 8));
            plain.Add((byte)Port);
            mark = LayoutMark;
        }
        else
        {
            plain.Add((byte)(Port >> 8));
            plain.Add((byte)Port);
            foreach (var ip in ips)
            {
                plain.AddRange(ip.GetAddressBytes());
            }
            mark = MultiLayoutMark;
        }

        var mask = CodeMask(sid[2..8], plain.Count);
        for (var i = 0; i < plain.Count; i++)
        {
            body.Add((byte)(plain[i] ^ mask[i]));
        }

        var crc = (ushort)(Crc16(body) ^ mark);
        body.Add((byte)(crc >> 8));
        body.Add((byte)crc);
        return ToText(body);
    }

    public static bool TryDecode(
        string? text,
        out OpenOmsiSessionCode code)
    {
        code = default!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = new string(
            text
                .Where(ch =>
                    !char.IsWhiteSpace(ch) &&
                    ch is not '-' and not '_')
                .Select(char.ToUpperInvariant)
                .ToArray());
        if (normalized.StartsWith("OMSI", StringComparison.Ordinal))
        {
            normalized = normalized[4..];
        }

        if (!TryUnpad(normalized, out normalized))
        {
            return false;
        }

        var bits = new List<int>(normalized.Length * 5);
        foreach (var ch in normalized)
        {
            var index = Alphabet.IndexOf(ch);
            if (index < 0)
            {
                return false;
            }

            for (var bit = 4; bit >= 0; bit--)
            {
                bits.Add((index >> bit) & 1);
            }
        }

        var byteCount = bits.Count / 8;
        if (byteCount is not 15 and not 19 and not 23)
        {
            return false;
        }

        var bytes = new byte[byteCount];
        for (var i = 0; i < bytes.Length; i++)
        {
            byte value = 0;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (byte)((value << 1) | bits[i * 8 + bit]);
            }
            bytes[i] = value;
        }

        var body = bytes.AsSpan(0, bytes.Length - 2).ToArray();
        var crc = (ushort)(
            (bytes[^2] << 8) |
            bytes[^1]);
        var sum = Crc16(body);

        byte protocol;
        ushort port;
        ulong session;
        List<IPAddress> addresses;

        if (byteCount == 15 &&
            (ushort)(sum ^ LayoutMark) == crc)
        {
            var plain = Unmask(body, 6);
            if (plain.Length != 7)
            {
                return false;
            }

            protocol = plain[0];
            addresses =
            [
                new IPAddress(plain.AsSpan(1, 4))
            ];
            port = (ushort)((plain[5] << 8) | plain[6]);
            session = DecodeMixedSession(body);
        }
        else if (byteCount == 15 && sum == crc)
        {
            // Legacy first layout: protocol, IPv4, port and the 48-bit
            // session id were all stored in the clear. Current openOMSI still
            // accepts these codes for backward compatibility.
            protocol = body[0];
            addresses =
            [
                new IPAddress(body.AsSpan(1, 4))
            ];
            port = (ushort)((body[5] << 8) | body[6]);

            Span<byte> sid = stackalloc byte[8];
            body.AsSpan(7, 6).CopyTo(sid[2..]);
            session = ReadUInt64BigEndian(sid);
        }
        else if (byteCount > 15 &&
                 (ushort)(sum ^ MultiLayoutMark) == crc)
        {
            var plain = Unmask(body, 6);
            if (plain.Length < 11 ||
                (plain.Length - 3) % 4 != 0)
            {
                return false;
            }

            protocol = plain[0];
            port = (ushort)((plain[1] << 8) | plain[2]);
            addresses = [];
            for (var at = 3;
                 at + 4 <= plain.Length &&
                 addresses.Count < MaxAddresses;
                 at += 4)
            {
                addresses.Add(
                    new IPAddress(
                        plain.AsSpan(at, 4)));
            }

            if (addresses.Count is < 2 or > MaxAddresses)
            {
                return false;
            }

            session = DecodeMixedSession(body);
        }
        else
        {
            return false;
        }

        if (protocol == 0 ||
            port == 0 ||
            addresses.Count == 0)
        {
            return false;
        }

        code = new OpenOmsiSessionCode(
            protocol,
            addresses[0],
            port,
            session & Mask48)
        {
            Addresses = addresses
        };
        return true;
    }

    private static byte[] Unmask(
        IReadOnlyList<byte> body,
        int from)
    {
        if (from < 0 || from > body.Count)
        {
            return [];
        }

        var session = body
            .Take(6)
            .ToArray();
        var mask = CodeMask(
            session,
            body.Count - from);
        var plain = new byte[body.Count - from];
        for (var i = 0; i < plain.Length; i++)
        {
            plain[i] =
                (byte)(body[from + i] ^ mask[i]);
        }

        return plain;
    }

    private static ulong DecodeMixedSession(
        IReadOnlyList<byte> body)
    {
        Span<byte> sid = stackalloc byte[8];
        for (var i = 0; i < 6; i++)
        {
            sid[i + 2] = body[i];
        }

        return Unmix48(ReadUInt64BigEndian(sid));
    }

    private static bool TryUnpad(
        string value,
        out string unpadded)
    {
        unpadded = string.Empty;
        var rawLengths = new[] { 24, 31, 37 };

        if (rawLengths.Contains(value.Length))
        {
            unpadded = value;
            return true;
        }

        foreach (var rawLength in rawLengths)
        {
            var writtenLength =
                ((rawLength + 3) / 4) * 4;
            if (value.Length != writtenLength ||
                rawLength >= value.Length)
            {
                continue;
            }

            if (value.AsSpan(rawLength)
                .ToArray()
                .All(ch => ch == Alphabet[0]))
            {
                unpadded = value[..rawLength];
                return true;
            }
        }

        return false;
    }

    private static string ToText(
        IReadOnlyList<byte> bytes)
    {
        var bitCount = bytes.Count * 8;
        var charCount = (bitCount + 4) / 5;
        var chars = new List<char>(charCount);

        for (var i = 0; i < charCount; i++)
        {
            var value = 0;
            for (var bit = 0; bit < 5; bit++)
            {
                var absolute = i * 5 + bit;
                var next = absolute < bitCount
                    ? (bytes[absolute / 8] >>
                       (7 - absolute % 8)) & 1
                    : 0;
                value = (value << 1) | next;
            }

            chars.Add(Alphabet[value]);
        }

        while (chars.Count % 4 != 0)
        {
            chars.Add(Alphabet[0]);
        }

        var groups = Enumerable.Range(
                0,
                chars.Count / 4)
            .Select(i =>
                new string(
                    chars
                        .Skip(i * 4)
                        .Take(4)
                        .ToArray()));
        return "OMSI-" + string.Join("-", groups);
    }

    private static ushort Crc16(
        IEnumerable<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var value in data)
        {
            crc ^= (ushort)(value << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (ushort)(
                    (crc & 0x8000) != 0
                        ? (crc << 1) ^ 0x1021
                        : crc << 1);
            }
        }

        return crc;
    }

    private static byte[] CodeMask(
        ReadOnlySpan<byte> session,
        int length)
    {
        ulong z = 0;
        foreach (var value in session)
        {
            z = (z << 8) | value;
        }

        z ^= 0x5DEE_CE66_D1CE_4E5BUL;

        var output = new byte[length];
        for (var i = 0; i < output.Length; i++)
        {
            z = unchecked(
                z + 0x9E37_79B9_7F4A_7C15UL);
            var x = z;
            x = unchecked(
                (x ^ (x >> 30)) *
                0xBF58_476D_1CE4_E5B9UL);
            x = unchecked(
                (x ^ (x >> 27)) *
                0x94D0_49BB_1331_11EBUL);
            output[i] = (byte)(
                (x ^ (x >> 31)) >>
                ((i % 8) * 3));
        }

        return output;
    }

    private static ulong Mix48(ulong value)
    {
        value &= Mask48;
        foreach (var constant in MixConstants)
        {
            value ^= value >> 24;
            value =
                unchecked(value * constant) &
                Mask48;
        }

        return (value ^ (value >> 24)) & Mask48;
    }

    private static ulong Unmix48(ulong value)
    {
        value &= Mask48;
        for (var i = MixConstants.Length - 1;
             i >= 0;
             i--)
        {
            value ^= value >> 24;
            value =
                unchecked(
                    value *
                    Inverse48(
                        MixConstants[i])) &
                Mask48;
        }

        return (value ^ (value >> 24)) & Mask48;
    }

    private static ulong Inverse48(
        ulong constant)
    {
        var inverse = constant;
        for (var i = 0; i < 5; i++)
        {
            inverse = unchecked(
                inverse *
                (2UL -
                 unchecked(
                     constant * inverse)));
        }

        return inverse & Mask48;
    }

    private static void WriteUInt64BigEndian(
        Span<byte> destination,
        ulong value)
    {
        for (var i = 7; i >= 0; i--)
        {
            destination[i] = (byte)value;
            value >>= 8;
        }
    }

    private static ulong ReadUInt64BigEndian(
        ReadOnlySpan<byte> source)
    {
        ulong value = 0;
        foreach (var item in source)
        {
            value = (value << 8) | item;
        }

        return value;
    }
}
