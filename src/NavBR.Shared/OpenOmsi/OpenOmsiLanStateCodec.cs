namespace NavBR.Shared.OpenOmsi;

/// <summary>
/// Binary STATE codec compatible with the public openOMSI LAN protocol v6.
///
/// Bit fields are written least-significant bit first, matching omsi-net/wire.rs.
/// Derived from the MIT-licensed openOMSI protocol implementation; see
/// licenses/openOMSI-LICENSE.txt.
/// </summary>
public static class OpenOmsiLanStateCodec
{
    private const int FlagBits = 10;

    public static byte[] Encode(OpenOmsiLanVehicleState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var id = state.PlayerId;
        if (id == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "openOMSI LAN player id 0 is reserved.");
        }

        var header = new byte[OpenOmsiLanProtocol.StateHeaderBytes];
        header[0] = OpenOmsiLanProtocol.StateMagic;
        header[1] = OpenOmsiLanProtocol.ProtocolVersion;
        BitConverter.TryWriteBytes(header.AsSpan(2, 2), id);
        BitConverter.TryWriteBytes(header.AsSpan(4, 2), state.Sequence);

        var writer = new BitWriter(header);
        var flags = state.Flags & ((1u << FlagBits) - 1u);
        writer.Put(flags, FlagBits);

        if ((flags & OpenOmsiLanProtocol.FlagVehicle) == 0)
        {
            PutWalker(writer, state.Walker);
            PutTail(writer, state);
            return ValidateSize(writer.Finish());
        }

        writer.PutFixed(state.X, 0.01d, 32);
        writer.PutFixed(state.Y, 0.01d, 32);
        writer.PutFixed(state.Z, 0.01d, 24);
        writer.Put(QuantizeHeading(state.HeadingDegrees), 16);
        writer.PutFixed(state.PitchDegrees, 0.01d, 12);
        writer.PutFixed(state.BankDegrees, 0.01d, 12);
        writer.PutFixed(state.SpeedKph, 0.05d, 14);
        writer.PutFixed(state.SteeringDegrees, 0.05d, 11);
        writer.PutUnsigned(state.HeadLightLevel, 2);
        writer.PutUnsigned(state.InteriorLightLevel, 2);
        writer.PutUnsigned(state.TurnSignal, 2);
        writer.PutUnsigned(
            double.IsFinite(state.EngineRpm)
                ? (long)Math.Round(state.EngineRpm / 5d)
                : 0,
            10);
        writer.PutUnit(state.Throttle, 5);
        writer.PutUnit(state.Brake, 5);
        writer.PutUnsigned(state.Passengers, 8);

        var doors = state.Doors
            .Take(OpenOmsiLanProtocol.MaxDoors)
            .ToArray();
        writer.Put((ulong)doors.Length, 3);
        foreach (var door in doors)
        {
            writer.PutUnit(door, 4);
        }

        var suspension = state.Suspension
            .Take(OpenOmsiLanProtocol.MaxWheels)
            .ToArray();
        writer.Put((ulong)suspension.Length, 4);
        foreach (var travel in suspension)
        {
            writer.PutFixed(travel, 0.005d, 7);
        }

        var rear = state.RearSections
            .Take(OpenOmsiLanProtocol.MaxRearSections)
            .ToArray();
        writer.Put((ulong)rear.Length, 2);
        var roundedX = RoundFixed(state.X, 0.01d);
        var roundedY = RoundFixed(state.Y, 0.01d);
        var roundedZ = RoundFixed(state.Z, 0.01d);
        foreach (var section in rear)
        {
            writer.PutFixed(section.X - roundedX, 0.01d, 16);
            writer.PutFixed(section.Y - roundedY, 0.01d, 16);
            writer.PutFixed(section.Z - roundedZ, 0.01d, 12);
            writer.Put(QuantizeHeading(section.HeadingDegrees), 16);
        }

        var lamps = state.Lamps
            .Take(OpenOmsiLanProtocol.MaxLamps)
            .ToArray();
        writer.Put((ulong)lamps.Length, 7);
        foreach (var lamp in lamps)
        {
            writer.PutUnit(lamp, 2);
        }

        var switches = state.Switches
            .Take(OpenOmsiLanProtocol.MaxSwitches)
            .ToArray();
        writer.Put((ulong)switches.Length, 5);
        foreach (var value in switches)
        {
            writer.PutFixed(value, 1d, 4);
        }

        var values = state.Values
            .Take(OpenOmsiLanProtocol.MaxValues)
            .ToArray();
        writer.Put((ulong)values.Length, 6);
        foreach (var value in values)
        {
            writer.Put(ToHalfBits(value), 16);
        }

        PutWalker(writer, state.Walker);
        PutTail(writer, state);
        return ValidateSize(writer.Finish());
    }

    public static bool TryDecode(
        ReadOnlySpan<byte> data,
        out OpenOmsiLanVehicleState state)
    {
        state = default!;
        if (data.Length < OpenOmsiLanProtocol.StateHeaderBytes + 2 ||
            data.Length > OpenOmsiLanProtocol.MaxStateBytes ||
            data[0] != OpenOmsiLanProtocol.StateMagic ||
            data[1] != OpenOmsiLanProtocol.ProtocolVersion)
        {
            return false;
        }

        var id = BitConverter.ToUInt16(data.Slice(2, 2));
        if (id == 0)
        {
            return false;
        }

        var sequence = BitConverter.ToUInt16(data.Slice(4, 2));
        var reader = new BitReader(
            data[OpenOmsiLanProtocol.StateHeaderBytes..].ToArray());

        if (!reader.TryGet(FlagBits, out var flagsRaw))
        {
            return false;
        }

        var flags = (uint)flagsRaw;
        var decoded = OpenOmsiLanVehicleState.Empty(id, sequence) with
        {
            Flags = flags
        };

        if ((flags & OpenOmsiLanProtocol.FlagVehicle) == 0)
        {
            if (!TryGetWalker(ref reader, out var walker))
            {
                return false;
            }

            decoded = decoded with { Walker = walker };
            TryGetTail(ref reader, decoded, out decoded);
            state = decoded;
            return true;
        }

        if (!reader.TryGetFixed(0.01d, 32, out var x) ||
            !reader.TryGetFixed(0.01d, 32, out var y) ||
            !reader.TryGetFixed(0.01d, 24, out var z) ||
            !reader.TryGet(16, out var headingRaw) ||
            !reader.TryGetFixed(0.01d, 12, out var pitch) ||
            !reader.TryGetFixed(0.01d, 12, out var bank) ||
            !reader.TryGetFixed(0.05d, 14, out var speed) ||
            !reader.TryGetFixed(0.05d, 11, out var steering) ||
            !reader.TryGet(2, out var head) ||
            !reader.TryGet(2, out var interior) ||
            !reader.TryGet(2, out var turnSignal) ||
            !reader.TryGet(10, out var rpm) ||
            !reader.TryGetUnit(5, out var throttle) ||
            !reader.TryGetUnit(5, out var brake) ||
            !reader.TryGet(8, out var passengers))
        {
            return false;
        }

        if (!reader.TryGet(3, out var doorCount) ||
            doorCount > OpenOmsiLanProtocol.MaxDoors)
        {
            return false;
        }

        var doors = new float[(int)doorCount];
        for (var index = 0; index < doors.Length; index++)
        {
            if (!reader.TryGetUnit(4, out doors[index]))
            {
                return false;
            }
        }

        if (!reader.TryGet(4, out var wheelCount) ||
            wheelCount > OpenOmsiLanProtocol.MaxWheels)
        {
            return false;
        }

        var suspension = new float[(int)wheelCount];
        for (var index = 0; index < suspension.Length; index++)
        {
            if (!reader.TryGetFixed(
                    0.005d,
                    7,
                    out var travel))
            {
                return false;
            }

            suspension[index] = (float)travel;
        }

        if (!reader.TryGet(2, out var rearCount) ||
            rearCount > OpenOmsiLanProtocol.MaxRearSections)
        {
            return false;
        }

        var rear = new OpenOmsiLanPartPose[(int)rearCount];
        for (var index = 0; index < rear.Length; index++)
        {
            if (!reader.TryGetFixed(0.01d, 16, out var dx) ||
                !reader.TryGetFixed(0.01d, 16, out var dy) ||
                !reader.TryGetFixed(0.01d, 12, out var dz) ||
                !reader.TryGet(16, out var rearHeading))
            {
                return false;
            }

            rear[index] = new OpenOmsiLanPartPose(
                x + dx,
                y + dy,
                z + dz,
                DecodeHeading(rearHeading));
        }

        if (!reader.TryGet(7, out var lampCount) ||
            lampCount > OpenOmsiLanProtocol.MaxLamps)
        {
            return false;
        }

        var lamps = new float[(int)lampCount];
        for (var index = 0; index < lamps.Length; index++)
        {
            if (!reader.TryGetUnit(2, out lamps[index]))
            {
                return false;
            }
        }

        if (!reader.TryGet(5, out var switchCount) ||
            switchCount > OpenOmsiLanProtocol.MaxSwitches)
        {
            return false;
        }

        var switches = new float[(int)switchCount];
        for (var index = 0; index < switches.Length; index++)
        {
            if (!reader.TryGetSigned(4, out var value))
            {
                return false;
            }

            switches[index] = value;
        }

        if (!reader.TryGet(6, out var valueCount) ||
            valueCount > OpenOmsiLanProtocol.MaxValues)
        {
            return false;
        }

        var values = new float[(int)valueCount];
        for (var index = 0; index < values.Length; index++)
        {
            if (!reader.TryGet(16, out var bits))
            {
                return false;
            }

            values[index] = FromHalfBits((ushort)bits);
        }

        if (!TryGetWalker(ref reader, out var vehicleWalker))
        {
            return false;
        }

        decoded = decoded with
        {
            X = x,
            Y = y,
            Z = z,
            HeadingDegrees = DecodeHeading(headingRaw),
            PitchDegrees = (float)pitch,
            BankDegrees = (float)bank,
            SpeedKph = (float)speed,
            SteeringDegrees = (float)steering,
            HeadLightLevel = (byte)head,
            InteriorLightLevel = (byte)interior,
            TurnSignal = (byte)turnSignal,
            EngineRpm = (float)rpm * 5f,
            Throttle = throttle,
            Brake = brake,
            Passengers = (byte)passengers,
            Doors = doors,
            Suspension = suspension,
            RearSections = rear,
            Lamps = lamps,
            Switches = switches,
            Values = values,
            Walker = vehicleWalker
        };

        TryGetTail(ref reader, decoded, out decoded);
        state = decoded;
        return true;
    }

    public static bool IsSequenceNewer(ushort candidate, ushort previous) =>
        candidate != previous &&
        unchecked((ushort)(candidate - previous)) < 0x8000;

    private static void PutWalker(
        BitWriter writer,
        OpenOmsiLanWalker? walker)
    {
        if (walker is null)
        {
            writer.Put(0, 1);
            return;
        }

        writer.Put(1, 1);
        writer.PutFixed(walker.X, 0.01d, 32);
        writer.PutFixed(walker.Y, 0.01d, 32);
        writer.PutFixed(walker.Z, 0.01d, 24);
        writer.Put(QuantizeHeading(walker.HeadingDegrees), 16);
        writer.PutFixed(walker.SpeedMps, 0.05d, 9);
        writer.Put(walker.Seated ? 1UL : 0UL, 1);
    }

    private static bool TryGetWalker(
        ref BitReader reader,
        out OpenOmsiLanWalker? walker)
    {
        walker = null;
        if (!reader.TryGet(1, out var present))
        {
            return false;
        }

        if (present == 0)
        {
            return true;
        }

        if (!reader.TryGetFixed(0.01d, 32, out var x) ||
            !reader.TryGetFixed(0.01d, 32, out var y) ||
            !reader.TryGetFixed(0.01d, 24, out var z) ||
            !reader.TryGet(16, out var heading) ||
            !reader.TryGetFixed(0.05d, 9, out var speed) ||
            !reader.TryGet(1, out var seated))
        {
            return false;
        }

        walker = new OpenOmsiLanWalker(
            x,
            y,
            z,
            DecodeHeading(heading),
            (float)speed,
            float.NaN,
            seated != 0,
            null,
            null,
            null);
        return true;
    }

    private static void PutTail(
        BitWriter writer,
        OpenOmsiLanVehicleState state)
    {
        writer.Put(1, 1);
        writer.Put(state.SentMilliseconds, 32);

        if (state.Walker?.AboardOwner is ushort owner &&
            state.Walker.AboardLocal is { Length: >= 3 } local)
        {
            writer.Put(1, 1);
            writer.Put(owner, 16);
            writer.PutFixed(local[0], 0.005d, 14);
            writer.PutFixed(local[1], 0.005d, 14);
            writer.PutFixed(local[2], 0.005d, 14);
            if (state.Walker.Seat is ushort seat)
            {
                writer.Put(1, 1);
                writer.Put(seat, 10);
            }
            else
            {
                writer.Put(0, 1);
            }
        }
        else
        {
            writer.Put(0, 1);
        }

        writer.Put(1, 1);
        foreach (var door in state.Doors.Take(OpenOmsiLanProtocol.MaxDoors))
        {
            writer.PutUnit(door, 8);
        }

        if (state.Walker is not null)
        {
            writer.Put(1, 1);
            var course = float.IsFinite(state.Walker.CourseDegrees)
                ? state.Walker.CourseDegrees
                : state.Walker.HeadingDegrees;
            writer.Put(QuantizeHeading(course), 16);
        }
        else
        {
            writer.Put(0, 1);
        }
    }

    private static bool TryGetTail(
        ref BitReader reader,
        OpenOmsiLanVehicleState source,
        out OpenOmsiLanVehicleState result)
    {
        result = source;

        // Older senders may end before the tail. That is valid.
        if (!reader.TryGet(1, out var tailPresent) ||
            tailPresent == 0)
        {
            return true;
        }

        if (!reader.TryGet(32, out var sentMilliseconds) ||
            !reader.TryGet(1, out var aboardPresent))
        {
            return true;
        }

        var walker = source.Walker;
        if (aboardPresent != 0)
        {
            if (!reader.TryGet(16, out var owner) ||
                !reader.TryGetFixed(0.005d, 14, out var localX) ||
                !reader.TryGetFixed(0.005d, 14, out var localY) ||
                !reader.TryGetFixed(0.005d, 14, out var localZ) ||
                !reader.TryGet(1, out var seatPresent))
            {
                result = source with
                {
                    SentMilliseconds = (uint)sentMilliseconds
                };
                return true;
            }

            ushort? seat = null;
            if (seatPresent != 0)
            {
                if (!reader.TryGet(10, out var seatValue))
                {
                    result = source with
                    {
                        SentMilliseconds = (uint)sentMilliseconds
                    };
                    return true;
                }

                seat = (ushort)seatValue;
            }

            if (walker is not null)
            {
                walker = walker with
                {
                    AboardOwner = (ushort)owner,
                    AboardLocal =
                    [
                        (float)localX,
                        (float)localY,
                        (float)localZ
                    ],
                    Seat = seat
                };
            }
        }

        var doors = source.Doors;
        if (reader.TryGet(1, out var fineDoorsPresent) &&
            fineDoorsPresent != 0)
        {
            var fine = new float[doors.Count];
            var complete = true;
            for (var index = 0; index < fine.Length; index++)
            {
                if (!reader.TryGetUnit(8, out fine[index]))
                {
                    complete = false;
                    break;
                }
            }

            if (complete)
            {
                doors = fine;
            }

            if (reader.TryGet(1, out var coursePresent) &&
                coursePresent != 0 &&
                walker is not null &&
                reader.TryGet(16, out var course))
            {
                walker = walker with
                {
                    CourseDegrees = DecodeHeading(course)
                };
            }
        }

        result = source with
        {
            SentMilliseconds = (uint)sentMilliseconds,
            Doors = doors,
            Walker = walker
        };
        return true;
    }

    private static byte[] ValidateSize(byte[] data)
    {
        if (data.Length > OpenOmsiLanProtocol.MaxStateBytes)
        {
            throw new InvalidOperationException(
                $"openOMSI STATE exceeds {OpenOmsiLanProtocol.MaxStateBytes} bytes.");
        }

        return data;
    }

    private static ulong QuantizeHeading(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        var normalized = value % 360d;
        if (normalized < 0d)
        {
            normalized += 360d;
        }

        return (ulong)Math.Round(normalized / 360d * 65_536d) %
               65_536UL;
    }

    private static float DecodeHeading(ulong value) =>
        (float)(value * 360d / 65_536d);

    private static double RoundFixed(double value, double step) =>
        double.IsFinite(value)
            ? Math.Round(value / step) * step
            : 0d;

    private static ushort ToHalfBits(float value)
    {
        if (!float.IsFinite(value))
        {
            value = 0f;
        }

        value = Math.Clamp(value, -65_504f, 65_504f);
        return BitConverter.HalfToUInt16Bits((Half)value);
    }

    private static float FromHalfBits(ushort value)
    {
        var decoded = (float)BitConverter.UInt16BitsToHalf(value);
        return float.IsFinite(decoded) ? decoded : 0f;
    }

    private sealed class BitWriter
    {
        private readonly List<byte> _buffer;
        private ulong _accumulator;
        private int _bitCount;

        public BitWriter(IEnumerable<byte> header)
        {
            _buffer = new List<byte>(header);
        }

        public void Put(ulong value, int bits)
        {
            if (bits is < 1 or > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(bits));
            }

            var mask = bits == 32
                ? uint.MaxValue
                : (1UL << bits) - 1UL;
            _accumulator |= (value & mask) << _bitCount;
            _bitCount += bits;

            while (_bitCount >= 8)
            {
                _buffer.Add((byte)_accumulator);
                _accumulator >>= 8;
                _bitCount -= 8;
            }
        }

        public void PutSigned(long value, int bits)
        {
            var max = (1L << (bits - 1)) - 1L;
            var min = -max - 1L;
            Put(unchecked((ulong)Math.Clamp(value, min, max)), bits);
        }

        public void PutUnsigned(long value, int bits)
        {
            var max = (1L << bits) - 1L;
            Put((ulong)Math.Clamp(value, 0L, max), bits);
        }

        public void PutFixed(double value, double step, int bits)
        {
            var quantized = double.IsFinite(value)
                ? Math.Round(value / step)
                : 0d;
            var max = (1L << (bits - 1)) - 1L;
            var min = -max - 1L;
            PutSigned(
                (long)Math.Clamp(quantized, min, max),
                bits);
        }

        public void PutUnit(float value, int bits)
        {
            var top = (1 << bits) - 1;
            var safe = float.IsFinite(value)
                ? Math.Clamp(value, 0f, 1f)
                : 0f;
            Put(
                (ulong)Math.Round(safe * top),
                bits);
        }

        public byte[] Finish()
        {
            if (_bitCount > 0)
            {
                _buffer.Add((byte)_accumulator);
                _accumulator = 0;
                _bitCount = 0;
            }

            return _buffer.ToArray();
        }
    }

    private sealed class BitReader
    {
        private readonly byte[] _data;
        private int _bit;

        public BitReader(byte[] data)
        {
            _data = data;
        }

        public bool TryGet(int bits, out ulong value)
        {
            value = 0;
            if (bits is < 1 or > 32 ||
                _bit + bits > _data.Length * 8)
            {
                return false;
            }

            for (var index = 0; index < bits; index++)
            {
                var offset = _bit + index;
                value |=
                    (ulong)((_data[offset / 8] >>
                             (offset % 8)) & 1)
                    << index;
            }

            _bit += bits;
            return true;
        }

        public bool TryGetSigned(int bits, out float value)
        {
            value = 0f;
            if (!TryGet(bits, out var raw))
            {
                return false;
            }

            var sign = 1L << (bits - 1);
            var signed =
                (long)((raw ^ (ulong)sign) - (ulong)sign);
            value = signed;
            return true;
        }

        public bool TryGetFixed(
            double step,
            int bits,
            out double value)
        {
            value = 0d;
            if (!TryGet(bits, out var raw))
            {
                return false;
            }

            var sign = 1L << (bits - 1);
            var signed =
                (long)((raw ^ (ulong)sign) - (ulong)sign);
            value = signed * step;
            return true;
        }

        public bool TryGetUnit(int bits, out float value)
        {
            value = 0f;
            if (!TryGet(bits, out var raw))
            {
                return false;
            }

            value = (float)raw / ((1 << bits) - 1);
            return true;
        }
    }
}

public sealed record OpenOmsiLanVehicleState(
    ushort PlayerId,
    ushort Sequence,
    uint Flags,
    double X,
    double Y,
    double Z,
    float HeadingDegrees,
    float PitchDegrees,
    float BankDegrees,
    float SpeedKph,
    float SteeringDegrees,
    byte HeadLightLevel,
    byte InteriorLightLevel,
    byte TurnSignal,
    float EngineRpm,
    float Throttle,
    float Brake,
    byte Passengers,
    IReadOnlyList<float> Doors,
    IReadOnlyList<float> Suspension,
    IReadOnlyList<OpenOmsiLanPartPose> RearSections,
    IReadOnlyList<float> Lamps,
    IReadOnlyList<float> Switches,
    IReadOnlyList<float> Values,
    OpenOmsiLanWalker? Walker,
    uint SentMilliseconds)
{
    public static OpenOmsiLanVehicleState Empty(
        ushort playerId,
        ushort sequence) =>
        new(
            playerId,
            sequence,
            0,
            0d,
            0d,
            0d,
            0f,
            0f,
            0f,
            0f,
            0f,
            0,
            0,
            0,
            0f,
            0f,
            0f,
            0,
            [],
            [],
            [],
            [],
            [],
            [],
            null,
            0);
}

public sealed record OpenOmsiLanPartPose(
    double X,
    double Y,
    double Z,
    float HeadingDegrees);

public sealed record OpenOmsiLanWalker(
    double X,
    double Y,
    double Z,
    float HeadingDegrees,
    float SpeedMps,
    float CourseDegrees,
    bool Seated,
    ushort? AboardOwner,
    float[]? AboardLocal,
    ushort? Seat);
