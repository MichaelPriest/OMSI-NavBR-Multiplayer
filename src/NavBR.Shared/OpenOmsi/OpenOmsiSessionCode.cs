using System.Net;
using System.Text;

namespace NavBR.Shared.OpenOmsi;

/// <summary>
/// openOMSI-compatible session code for one IPv4 endpoint.
/// Layout matches omsi-net v6: mixed session id + masked protocol/address/port + CRC.
/// </summary>
public sealed record OpenOmsiSessionCode(
    byte Protocol,
    IPAddress Address,
    ushort Port,
    ulong Session)
{
    private const ushort LayoutMark = 0x4F43;
    private const ulong Mask48 = 0xFFFF_FFFF_FFFFUL;
    private static readonly ulong[] MixConstants =
    [
        0x9E37_79B9_7F4BUL,
        0xC2B2_AE3D_27D5UL
    ];
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string Encode()
    {
        var ip = Address.MapToIPv4().GetAddressBytes();
        var mixed = Mix48(Session & Mask48);
        Span<byte> sid = stackalloc byte[8];
        WriteUInt64BigEndian(sid, mixed);

        var body = new List<byte>(15);
        body.AddRange(sid[2..8].ToArray());

        var plain = new byte[7];
        plain[0] = Protocol;
        Array.Copy(ip, 0, plain, 1, 4);
        plain[5] = (byte)(Port >> 8);
        plain[6] = (byte)Port;

        var mask = CodeMask(sid[2..8], plain.Length);
        for (var i = 0; i < plain.Length; i++)
        {
            body.Add((byte)(plain[i] ^ mask[i]));
        }

        var crc = (ushort)(Crc16(body) ^ LayoutMark);
        body.Add((byte)(crc >> 8));
        body.Add((byte)crc);
        return ToText(body);
    }

    public static bool TryDecode(string? text, out OpenOmsiSessionCode code)
    {
        code = default!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = new string(text
            .Where(ch => !char.IsWhiteSpace(ch) && ch is not '-' and not '_')
            .Select(char.ToUpperInvariant)
            .ToArray());
        if (normalized.StartsWith("OMSI", StringComparison.Ordinal))
        {
            normalized = normalized[4..];
        }

        if (normalized.Length != 24)
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

        var bytes = new byte[15];
        for (var i = 0; i < bytes.Length; i++)
        {
            byte value = 0;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (byte)((value << 1) | bits[i * 8 + bit]);
            }
            bytes[i] = value;
        }

        var body = bytes[..13];
        var crc = (ushort)((bytes[13] << 8) | bytes[14]);
        if ((ushort)(Crc16(body) ^ LayoutMark) != crc)
        {
            return false;
        }

        var sid = new byte[8];
        Array.Copy(body, 0, sid, 2, 6);
        var mixed = ReadUInt64BigEndian(sid);
        var session = Unmix48(mixed);

        var mask = CodeMask(body.AsSpan(0, 6), 7);
        var plain = new byte[7];
        for (var i = 0; i < plain.Length; i++)
        {
            plain[i] = (byte)(body[6 + i] ^ mask[i]);
        }

        var protocol = plain[0];
        var address = new IPAddress(plain.AsSpan(1, 4));
        var port = (ushort)((plain[5] << 8) | plain[6]);
        if (protocol == 0 || port == 0)
        {
            return false;
        }

        code = new OpenOmsiSessionCode(protocol, address, port, session);
        return true;
    }

    private static string ToText(IReadOnlyList<byte> bytes)
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
                    ? (bytes[absolute / 8] >> (7 - absolute % 8)) & 1
                    : 0;
                value = (value << 1) | next;
            }
            chars.Add(Alphabet[value]);
        }

        while (chars.Count % 4 != 0)
        {
            chars.Add(Alphabet[0]);
        }

        var groups = Enumerable.Range(0, chars.Count / 4)
            .Select(i => new string(chars.Skip(i * 4).Take(4).ToArray()));
        return "OMSI-" + string.Join("-", groups);
    }

    private static ushort Crc16(IEnumerable<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var value in data)
        {
            crc ^= (ushort)(value << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (ushort)((crc & 0x8000) != 0
                    ? (crc << 1) ^ 0x1021
                    : crc << 1);
            }
        }
        return crc;
    }

    private static byte[] CodeMask(ReadOnlySpan<byte> session, int length)
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
            z = unchecked(z + 0x9E37_79B9_7F4A_7C15UL);
            var x = z;
            x = unchecked((x ^ (x >> 30)) * 0xBF58_476D_1CE4_E5B9UL);
            x = unchecked((x ^ (x >> 27)) * 0x94D0_49BB_1331_11EBUL);
            output[i] = (byte)((x ^ (x >> 31)) >> ((i % 8) * 3));
        }
        return output;
    }

    private static ulong Mix48(ulong value)
    {
        value &= Mask48;
        foreach (var constant in MixConstants)
        {
            value ^= value >> 24;
            value = unchecked(value * constant) & Mask48;
        }
        return (value ^ (value >> 24)) & Mask48;
    }

    private static ulong Unmix48(ulong value)
    {
        value &= Mask48;
        for (var i = MixConstants.Length - 1; i >= 0; i--)
        {
            value ^= value >> 24;
            value = unchecked(value * Inverse48(MixConstants[i])) & Mask48;
        }
        return (value ^ (value >> 24)) & Mask48;
    }

    private static ulong Inverse48(ulong constant)
    {
        var inverse = constant;
        for (var i = 0; i < 5; i++)
        {
            inverse = unchecked(inverse * (2UL - unchecked(constant * inverse)));
        }
        return inverse & Mask48;
    }

    private static void WriteUInt64BigEndian(Span<byte> destination, ulong value)
    {
        for (var i = 7; i >= 0; i--)
        {
            destination[i] = (byte)value;
            value >>= 8;
        }
    }

    private static ulong ReadUInt64BigEndian(ReadOnlySpan<byte> source)
    {
        ulong value = 0;
        foreach (var item in source)
        {
            value = (value << 8) | item;
        }
        return value;
    }
}
