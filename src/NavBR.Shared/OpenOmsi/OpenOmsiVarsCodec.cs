using System.Buffers.Binary;
using System.Text;

namespace NavBR.Shared.OpenOmsi;

public sealed record OpenOmsiVarsFrame(
    ushort PlayerId,
    uint TableHash,
    IReadOnlyList<(ushort Index, float Value)> Floats,
    IReadOnlyList<(ushort Index, string Value)> Strings);

public static class OpenOmsiVarsCodec
{
    public const byte Magic = 0xB5;
    private const int HeaderBytes = 11;
    private const int Room = 1300;
    private const int MaxStringBytes = 255;

    public static bool TryDecode(
        ReadOnlySpan<byte> data,
        out OpenOmsiVarsFrame frame)
    {
        frame = default!;
        if (data.Length < HeaderBytes + 2 ||
            data[0] != Magic ||
            data[1] != OpenOmsiLanProtocol.ProtocolVersion)
        {
            return false;
        }

        var rawId = BinaryPrimitives.ReadUInt32LittleEndian(data[2..6]);
        if (rawId is 0 or > ushort.MaxValue)
        {
            return false;
        }

        var table = BinaryPrimitives.ReadUInt32LittleEndian(data[6..10]);
        var floats = new List<(ushort, float)>();
        var strings = new List<(ushort, string)>();
        var at = HeaderBytes;

        try
        {
            switch (data[10])
            {
                case 0:
                {
                    var count = ReadUInt16(data, ref at);
                    for (var i = 0; i < count; i++)
                    {
                        var index = ReadUInt16(data, ref at);
                        var value = ReadSingle(data, ref at);
                        floats.Add((index, value));
                    }
                    break;
                }

                case 1:
                {
                    var first = ReadUInt16(data, ref at);
                    var count = ReadUInt16(data, ref at);
                    for (var i = 0; i < count; i++)
                    {
                        var index = checked((ushort)(first + i));
                        var value = ReadSingle(data, ref at);
                        floats.Add((index, value));
                    }
                    break;
                }

                case 2:
                {
                    var count = ReadUInt16(data, ref at);
                    for (var i = 0; i < count; i++)
                    {
                        var index = ReadUInt16(data, ref at);
                        var length = ReadUInt16(data, ref at);
                        if (length > MaxStringBytes || at + length > data.Length)
                        {
                            return false;
                        }

                        var value = Encoding.UTF8.GetString(data.Slice(at, length));
                        if (value.Any(char.IsControl))
                        {
                            value = new string(value.Where(ch => !char.IsControl(ch)).ToArray());
                        }
                        strings.Add((index, value));
                        at += length;
                    }
                    break;
                }

                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }

        if (at != data.Length)
        {
            return false;
        }

        frame = new OpenOmsiVarsFrame(
            checked((ushort)rawId),
            table,
            floats,
            strings);
        return true;
    }

    internal static byte[] Header(
        ushort playerId,
        uint tableHash,
        byte kind)
    {
        var data = new byte[HeaderBytes];
        data[0] = Magic;
        data[1] = OpenOmsiLanProtocol.ProtocolVersion;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(2, 4), playerId);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(6, 4), tableHash);
        data[10] = kind;
        return data;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, ref int at)
    {
        if (at + 2 > data.Length)
        {
            throw new InvalidDataException("cut VARS u16");
        }

        var value = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(at, 2));
        at += 2;
        return value;
    }

    private static float ReadSingle(ReadOnlySpan<byte> data, ref int at)
    {
        if (at + 4 > data.Length)
        {
            throw new InvalidDataException("cut VARS f32");
        }

        var bits = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(at, 4));
        at += 4;
        return BitConverter.Int32BitsToSingle(bits);
    }

    internal const int MaxPayloadBytes = Room;
    internal const int HeaderSize = HeaderBytes;
    internal const int MaxTextBytes = MaxStringBytes;
}

public sealed class OpenOmsiVarsSender
{
    private const float EverySeconds = 0.1f;
    private const uint KeyEvery = 5;
    private const uint StringsKeyEvery = 10;

    private uint _tableHash;
    private float[] _floats = [];
    private string[] _strings = [];
    private float _accumulator;
    private uint _ticks;
    private int _deltaFrom;
    private int _keyFloat;
    private int _keyString;

    public IReadOnlyList<byte[]> Tick(
        ushort playerId,
        uint tableHash,
        IReadOnlyList<ushort> floatIds,
        IReadOnlyList<float> floats,
        IReadOnlyList<ushort> stringIds,
        IReadOnlyList<string> strings,
        float deltaSeconds)
    {
        if (tableHash != _tableHash ||
            _floats.Length != floats.Count ||
            _strings.Length != strings.Count)
        {
            _tableHash = tableHash;
            _floats = Enumerable.Repeat(float.NaN, floats.Count).ToArray();
            _strings = Enumerable.Repeat("\0", strings.Count).ToArray();
            _accumulator = EverySeconds;
            _ticks = 0;
            _deltaFrom = 0;
            _keyFloat = 0;
            _keyString = 0;
        }

        _accumulator += Math.Max(0f, deltaSeconds);
        if (_accumulator < EverySeconds)
        {
            return Array.Empty<byte[]>();
        }

        _accumulator = 0f;
        _ticks = unchecked(_ticks + 1);
        var output = new List<byte[]>();
        var countFloats = Math.Min(floats.Count, floatIds.Count);

        if (countFloats > 0)
        {
            var data = new List<byte>(OpenOmsiVarsCodec.MaxPayloadBytes);
            data.AddRange(OpenOmsiVarsCodec.Header(playerId, tableHash, 0));
            data.Add(0);
            data.Add(0);

            ushort count = 0;
            var k = _deltaFrom % countFloats;
            var looked = 0;
            while (looked < countFloats &&
                   data.Count + 6 <= OpenOmsiVarsCodec.MaxPayloadBytes)
            {
                var value = floats[k];
                if (!Same(value, _floats[k]))
                {
                    WriteUInt16(data, floatIds[k]);
                    WriteSingle(data, value);
                    _floats[k] = value;
                    count++;
                }

                k = (k + 1) % countFloats;
                looked++;
            }

            _deltaFrom = k;
            if (count > 0)
            {
                WriteUInt16At(data, OpenOmsiVarsCodec.HeaderSize, count);
                output.Add(data.ToArray());
            }
        }

        if (countFloats > 0 && _ticks % KeyEvery == 0)
        {
            var first = _keyFloat % countFloats;
            var take = Math.Min(
                (OpenOmsiVarsCodec.MaxPayloadBytes -
                 OpenOmsiVarsCodec.HeaderSize - 4) / 4,
                countFloats - first);
            var run = 1;
            while (run < take &&
                   floatIds[first + run] == floatIds[first] + run)
            {
                run++;
            }

            var data = new List<byte>(OpenOmsiVarsCodec.MaxPayloadBytes);
            data.AddRange(OpenOmsiVarsCodec.Header(playerId, tableHash, 1));
            WriteUInt16(data, floatIds[first]);
            WriteUInt16(data, checked((ushort)run));
            for (var k = first; k < first + run; k++)
            {
                WriteSingle(data, floats[k]);
                _floats[k] = floats[k];
            }

            _keyFloat = (first + run) % countFloats;
            output.Add(data.ToArray());
        }

        var countStrings = Math.Min(strings.Count, stringIds.Count);
        if (countStrings > 0)
        {
            var key = _ticks % StringsKeyEvery == 0;
            var data = new List<byte>(OpenOmsiVarsCodec.MaxPayloadBytes);
            data.AddRange(OpenOmsiVarsCodec.Header(playerId, tableHash, 2));
            data.Add(0);
            data.Add(0);
            ushort count = 0;

            bool Put(int k)
            {
                var bytes = CutUtf8(strings[k] ?? string.Empty);
                if (data.Count + 4 + bytes.Length >
                    OpenOmsiVarsCodec.MaxPayloadBytes)
                {
                    return false;
                }

                WriteUInt16(data, stringIds[k]);
                WriteUInt16(data, checked((ushort)bytes.Length));
                data.AddRange(bytes);
                count++;
                return true;
            }

            for (var k = 0; k < countStrings; k++)
            {
                if (!string.Equals(strings[k], _strings[k], StringComparison.Ordinal) &&
                    Put(k))
                {
                    _strings[k] = strings[k] ?? string.Empty;
                }
            }

            if (key)
            {
                var k = _keyString % countStrings;
                for (var looked = 0; looked < countStrings; looked++)
                {
                    if (!Put(k))
                    {
                        break;
                    }

                    _strings[k] = strings[k] ?? string.Empty;
                    k = (k + 1) % countStrings;
                }
                _keyString = k;
            }

            if (count > 0)
            {
                WriteUInt16At(data, OpenOmsiVarsCodec.HeaderSize, count);
                output.Add(data.ToArray());
            }
        }

        return output;
    }

    private static bool Same(float a, float b) =>
        BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b) ||
        (float.IsNaN(a) && float.IsNaN(b)) ||
        Math.Abs(a - b) <= 1.0e-6f * Math.Max(Math.Abs(a), 1f);

    private static byte[] CutUtf8(string value)
    {
        var clean = new string(value.Where(ch => !char.IsControl(ch)).ToArray());
        var bytes = Encoding.UTF8.GetBytes(clean);
        if (bytes.Length <= OpenOmsiVarsCodec.MaxTextBytes)
        {
            return bytes;
        }

        var length = OpenOmsiVarsCodec.MaxTextBytes;
        while (length > 0)
        {
            try
            {
                return new UTF8Encoding(false, true).GetBytes(
                    Encoding.UTF8.GetString(bytes, 0, length));
            }
            catch (EncoderFallbackException)
            {
                length--;
            }
        }

        return Array.Empty<byte>();
    }

    private static void WriteUInt16(List<byte> data, ushort value)
    {
        data.Add((byte)value);
        data.Add((byte)(value >> 8));
    }

    private static void WriteUInt16At(
        List<byte> data,
        int index,
        ushort value)
    {
        data[index] = (byte)value;
        data[index + 1] = (byte)(value >> 8);
    }

    private static void WriteSingle(List<byte> data, float value)
    {
        var bits = BitConverter.SingleToInt32Bits(value);
        data.Add((byte)bits);
        data.Add((byte)(bits >> 8));
        data.Add((byte)(bits >> 16));
        data.Add((byte)(bits >> 24));
    }
}
