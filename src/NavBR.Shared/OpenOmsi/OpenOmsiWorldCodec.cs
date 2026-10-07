using System.Buffers.Binary;

namespace NavBR.Shared.OpenOmsi;

public enum OpenOmsiWorldActivity : byte
{
    Stand = 0,
    Walk = 1,
    Sit = 2,
    Run = 3
}

public enum OpenOmsiWorldPersonPlaceKind : byte
{
    Foot = 0,
    Vehicle = 1,
    PlayerBus = 2
}

public sealed record OpenOmsiWorldCarState(
    uint Id,
    double X,
    double Y,
    double Z,
    float HeadingDegrees,
    float PitchDegrees,
    float BankDegrees,
    float SpeedMetersPerSecond,
    float SteeringDegrees,
    byte TurnSignal,
    bool Brake,
    bool Lights,
    sbyte AtStation);

public sealed record OpenOmsiWorldPersonState(
    uint Id,
    OpenOmsiWorldActivity Activity,
    OpenOmsiWorldPersonPlaceKind PlaceKind,
    double X,
    double Y,
    double Z,
    float HeadingDegrees,
    float SpeedMetersPerSecond,
    long? WaitingStopObjectId = null,
    byte? WaitingPlace = null,
    uint VehicleId = 0,
    byte? Seat = null);

public sealed record OpenOmsiWorldLightState(
    long ObjectId,
    double CycleSeconds,
    bool Held);

public sealed record OpenOmsiWorldFrame(
    ushort Sequence,
    uint HostMilliseconds,
    IReadOnlyList<OpenOmsiWorldCarState> Cars,
    IReadOnlyList<OpenOmsiWorldPersonState> People,
    IReadOnlyList<OpenOmsiWorldLightState> Lights,
    IReadOnlyList<(bool IsPerson, uint Id)> Gone,
    bool? ParkedComplete = null,
    IReadOnlyList<uint>? ParkedMapIds = null)
{
    public static OpenOmsiWorldFrame Empty(ushort sequence = 0, uint hostMilliseconds = 0) =>
        new(
            sequence,
            hostMilliseconds,
            Array.Empty<OpenOmsiWorldCarState>(),
            Array.Empty<OpenOmsiWorldPersonState>(),
            Array.Empty<OpenOmsiWorldLightState>(),
            Array.Empty<(bool IsPerson, uint Id)>());
}

public static class OpenOmsiWorldCodec
{
    public const byte Magic = 0xB4;
    public const int HeaderBytes = 18;
    public const int MaxDatagramBytes = 1180;
    public const uint MaxId = (1u << 24) - 1u;
    public const uint PlayerBusBit = 1u << 31;

    private const int CarBits =
        24 + 19 + 19 + 17 + 12 + 8 + 8 + 11 + 8 + 2 + 1 + 1 + 2;
    private const int PersonFootBits =
        24 + 2 + 2 + 19 + 19 + 17 + 8 + 6 + 1;
    private const int PersonWaitBits = 32 + 8;
    private const int PersonAboardBits =
        24 + 2 + 2 + 24 + 12 + 13 + 10 + 8 + 8;
    private const int LightBits = 32 + 17 + 1;
    private const int GoneBits = 25;
    private const int CountBits = 7 + 8 + 6 + 6;

    public static IReadOnlyList<byte[]> Encode(OpenOmsiWorldFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var (anchorX, anchorY, anchorZ) = ResolveAnchor(frame);
        var cars = frame.Cars
            .Where(car => Fits(anchorX, anchorY, anchorZ, car.X, car.Y, car.Z))
            .ToArray();
        var people = frame.People
            .Where(person =>
                person.PlaceKind != OpenOmsiWorldPersonPlaceKind.Foot ||
                Fits(
                    anchorX,
                    anchorY,
                    anchorZ,
                    person.X,
                    person.Y,
                    person.Z))
            .ToArray();

        var budget =
            (MaxDatagramBytes - HeaderBytes) * 8 -
            CountBits;
        var output = new List<byte[]>();
        var carIndex = 0;
        var personIndex = 0;
        var first = true;

        do
        {
            var left = budget;
            var lights = first
                ? frame.Lights.Take(63).ToArray()
                : Array.Empty<OpenOmsiWorldLightState>();
            var gone = first
                ? frame.Gone.Take(63).ToArray()
                : Array.Empty<(bool IsPerson, uint Id)>();

            var parkedIds =
                first && frame.ParkedComplete is not null
                    ? (frame.ParkedMapIds ?? Array.Empty<uint>())
                        .Take(127)
                        .ToArray()
                    : Array.Empty<uint>();
            var hasParked =
                first && frame.ParkedComplete is not null;

            left = Math.Max(
                0,
                left -
                lights.Length * LightBits -
                gone.Length * GoneBits -
                1 -
                (hasParked ? 8 + parkedIds.Length * 32 : 0));

            var carStart = carIndex;
            while (carIndex < cars.Length &&
                   carIndex - carStart < 127 &&
                   left >= CarBits)
            {
                left -= CarBits;
                carIndex++;
            }

            var personStart = personIndex;
            while (personIndex < people.Length &&
                   personIndex - personStart < 255)
            {
                var bits = PersonBits(people[personIndex]);
                if (left < bits)
                {
                    break;
                }

                left -= bits;
                personIndex++;
            }

            var header = new byte[HeaderBytes];
            header[0] = Magic;
            header[1] = OpenOmsiLanProtocol.ProtocolVersion;
            BinaryPrimitives.WriteUInt16LittleEndian(
                header.AsSpan(2, 2),
                frame.Sequence);
            BinaryPrimitives.WriteUInt32LittleEndian(
                header.AsSpan(4, 4),
                frame.HostMilliseconds);
            BinaryPrimitives.WriteInt32LittleEndian(
                header.AsSpan(8, 4),
                anchorX);
            BinaryPrimitives.WriteInt32LittleEndian(
                header.AsSpan(12, 4),
                anchorY);
            BinaryPrimitives.WriteInt16LittleEndian(
                header.AsSpan(16, 2),
                anchorZ);

            var writer = new BitWriter(header);
            writer.Put((ulong)(carIndex - carStart), 7);
            for (var i = carStart; i < carIndex; i++)
            {
                WriteCar(
                    writer,
                    cars[i],
                    anchorX,
                    anchorY,
                    anchorZ);
            }

            writer.Put((ulong)(personIndex - personStart), 8);
            for (var i = personStart; i < personIndex; i++)
            {
                WritePerson(
                    writer,
                    people[i],
                    anchorX,
                    anchorY,
                    anchorZ);
            }

            writer.Put((ulong)lights.Length, 6);
            foreach (var light in lights)
            {
                writer.Put(unchecked((uint)light.ObjectId), 32);
                writer.PutSigned(
                    Quantize(light.CycleSeconds, 0.05d),
                    17);
                writer.Put(light.Held ? 1UL : 0UL, 1);
            }

            writer.Put((ulong)gone.Length, 6);
            foreach (var item in gone)
            {
                writer.Put(item.IsPerson ? 1UL : 0UL, 1);
                writer.Put(Math.Min(item.Id, MaxId), 24);
            }

            writer.Put(hasParked ? 1UL : 0UL, 1);
            if (hasParked)
            {
                var complete =
                    frame.ParkedComplete == true &&
                    (frame.ParkedMapIds?.Count ?? 0) <= 127;
                writer.Put(complete ? 1UL : 0UL, 1);
                writer.Put((ulong)parkedIds.Length, 7);
                foreach (var id in parkedIds)
                {
                    writer.Put(id, 32);
                }
            }

            var datagram = writer.Finish();
            if (datagram.Length <= MaxDatagramBytes)
            {
                output.Add(datagram);
            }

            first = false;
        }
        while (carIndex < cars.Length ||
               personIndex < people.Length);

        return output;
    }

    public static bool TryDecode(
        ReadOnlySpan<byte> data,
        out OpenOmsiWorldFrame frame)
    {
        frame = default!;
        if (data.Length < HeaderBytes ||
            data.Length > MaxDatagramBytes ||
            data[0] != Magic ||
            data[1] != OpenOmsiLanProtocol.ProtocolVersion)
        {
            return false;
        }

        var sequence =
            BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(2, 2));
        var hostMilliseconds =
            BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
        var anchorX =
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4));
        var anchorY =
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(12, 4));
        var anchorZ =
            BinaryPrimitives.ReadInt16LittleEndian(data.Slice(16, 2));

        var reader = new BitReader(data[HeaderBytes..]);
        try
        {
            var cars = new List<OpenOmsiWorldCarState>();
            var carCount = checked((int)reader.Get(7));
            for (var i = 0; i < carCount; i++)
            {
                cars.Add(ReadCar(
                    reader,
                    anchorX,
                    anchorY,
                    anchorZ));
            }

            var people = new List<OpenOmsiWorldPersonState>();
            var personCount = checked((int)reader.Get(8));
            for (var i = 0; i < personCount; i++)
            {
                people.Add(ReadPerson(
                    reader,
                    anchorX,
                    anchorY,
                    anchorZ));
            }

            var lights = new List<OpenOmsiWorldLightState>();
            var lightCount = checked((int)reader.Get(6));
            for (var i = 0; i < lightCount; i++)
            {
                var objectId = unchecked((int)reader.Get(32));
                var time = reader.GetSigned(17) * 0.05d;
                var held = reader.Get(1) == 1;
                lights.Add(
                    new OpenOmsiWorldLightState(
                        objectId,
                        time,
                        held));
            }

            var gone = new List<(bool IsPerson, uint Id)>();
            var goneCount = checked((int)reader.Get(6));
            for (var i = 0; i < goneCount; i++)
            {
                gone.Add(
                    (
                        reader.Get(1) == 1,
                        checked((uint)reader.Get(24))
                    ));
            }

            bool? parkedComplete = null;
            IReadOnlyList<uint>? parkedMapIds = null;
            if (reader.TryGet(1, out var hasParked) &&
                hasParked == 1)
            {
                parkedComplete = reader.Get(1) == 1;
                var count = checked((int)reader.Get(7));
                var parked = new List<uint>(count);
                for (var i = 0; i < count; i++)
                {
                    parked.Add(checked((uint)reader.Get(32)));
                }
                parkedMapIds = parked;
            }

            frame = new OpenOmsiWorldFrame(
                sequence,
                hostMilliseconds,
                cars,
                people,
                lights,
                gone,
                parkedComplete,
                parkedMapIds);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static void WriteCar(
        BitWriter writer,
        OpenOmsiWorldCarState car,
        int ax,
        int ay,
        short az)
    {
        writer.Put(Math.Min(car.Id, MaxId), 24);
        writer.PutFixed(car.X - ax, 0.01d, 19);
        writer.PutFixed(car.Y - ay, 0.01d, 19);
        writer.PutFixed(car.Z - az, 0.01d, 17);

        var heading =
            double.IsFinite(car.HeadingDegrees)
                ? ((car.HeadingDegrees % 360d) + 360d) % 360d
                : 0d;
        writer.Put(
            (ulong)Math.Round(heading / 360d * 4096d) % 4096UL,
            12);
        writer.PutFixed(car.PitchDegrees, 0.1d, 8);
        writer.PutFixed(car.BankDegrees, 0.1d, 8);
        writer.PutFixed(car.SpeedMetersPerSecond, 0.05d, 11);
        writer.PutFixed(car.SteeringDegrees, 0.5d, 8);
        writer.Put((ulong)Math.Min(car.TurnSignal, (byte)3), 2);
        writer.Put(car.Brake ? 1UL : 0UL, 1);
        writer.Put(car.Lights ? 1UL : 0UL, 1);
        writer.Put(
            car.AtStation switch
            {
                1 => 1UL,
                -1 => 2UL,
                _ => 0UL
            },
            2);
    }

    private static OpenOmsiWorldCarState ReadCar(
        BitReader reader,
        int ax,
        int ay,
        short az)
    {
        var id = checked((uint)reader.Get(24));
        var x = ax + reader.GetFixed(0.01d, 19);
        var y = ay + reader.GetFixed(0.01d, 19);
        var z = az + reader.GetFixed(0.01d, 17);
        var heading =
            (float)(reader.Get(12) * 360d / 4096d);
        var pitch = (float)reader.GetFixed(0.1d, 8);
        var bank = (float)reader.GetFixed(0.1d, 8);
        var speed = (float)reader.GetFixed(0.05d, 11);
        var steer = (float)reader.GetFixed(0.5d, 8);
        var blinker = checked((byte)reader.Get(2));
        var brake = reader.Get(1) == 1;
        var lights = reader.Get(1) == 1;
        var atStation = reader.Get(2) switch
        {
            1 => (sbyte)1,
            2 => (sbyte)-1,
            _ => (sbyte)0
        };
        return new OpenOmsiWorldCarState(
            id,
            x,
            y,
            z,
            heading,
            pitch,
            bank,
            speed,
            steer,
            blinker,
            brake,
            lights,
            atStation);
    }

    private static void WritePerson(
        BitWriter writer,
        OpenOmsiWorldPersonState person,
        int ax,
        int ay,
        short az)
    {
        writer.Put(Math.Min(person.Id, MaxId), 24);
        writer.Put((ulong)person.PlaceKind, 2);
        writer.Put((ulong)person.Activity, 2);

        if (person.PlaceKind == OpenOmsiWorldPersonPlaceKind.Foot)
        {
            writer.PutFixed(person.X - ax, 0.01d, 19);
            writer.PutFixed(person.Y - ay, 0.01d, 19);
            writer.PutFixed(person.Z - az, 0.01d, 17);
            var heading =
                double.IsFinite(person.HeadingDegrees)
                    ? ((person.HeadingDegrees % 360d) + 360d) % 360d
                    : 0d;
            writer.Put(
                (ulong)Math.Round(heading / 360d * 256d) % 256UL,
                8);
            writer.PutUnsigned(
                Quantize(person.SpeedMetersPerSecond, 0.05d),
                6);

            var waiting =
                person.WaitingStopObjectId is not null &&
                person.WaitingPlace is not null;
            writer.Put(waiting ? 1UL : 0UL, 1);
            if (waiting)
            {
                writer.Put(
                    unchecked((uint)person.WaitingStopObjectId!.Value),
                    32);
                writer.Put(person.WaitingPlace!.Value, 8);
            }
            return;
        }

        writer.Put(Math.Min(person.VehicleId, MaxId), 24);
        writer.PutFixed(person.X, 0.01d, 12);
        writer.PutFixed(person.Y, 0.01d, 13);
        writer.PutFixed(person.Z, 0.01d, 10);
        var aboardHeading =
            double.IsFinite(person.HeadingDegrees)
                ? ((person.HeadingDegrees % 360d) + 360d) % 360d
                : 0d;
        writer.Put(
            (ulong)Math.Round(aboardHeading / 360d * 256d) % 256UL,
            8);
        writer.Put(
            person.Seat is byte seat
                ? (ulong)Math.Min(seat, (byte)254)
                : 255UL,
            8);
    }

    private static OpenOmsiWorldPersonState ReadPerson(
        BitReader reader,
        int ax,
        int ay,
        short az)
    {
        var id = checked((uint)reader.Get(24));
        var place =
            (OpenOmsiWorldPersonPlaceKind)reader.Get(2);
        var activity =
            (OpenOmsiWorldActivity)reader.Get(2);

        if (place == OpenOmsiWorldPersonPlaceKind.Foot)
        {
            var x = ax + reader.GetFixed(0.01d, 19);
            var y = ay + reader.GetFixed(0.01d, 19);
            var z = az + reader.GetFixed(0.01d, 17);
            var heading =
                (float)(reader.Get(8) * 360d / 256d);
            var speed =
                (float)(reader.Get(6) * 0.05d);
            long? stop = null;
            byte? waitingPlace = null;
            if (reader.Get(1) == 1)
            {
                stop = checked((long)reader.Get(32));
                waitingPlace = checked((byte)reader.Get(8));
            }

            return new OpenOmsiWorldPersonState(
                id,
                activity,
                place,
                x,
                y,
                z,
                heading,
                speed,
                stop,
                waitingPlace);
        }

        if (place is not OpenOmsiWorldPersonPlaceKind.Vehicle and
            not OpenOmsiWorldPersonPlaceKind.PlayerBus)
        {
            throw new InvalidDataException("Invalid WORLD person place.");
        }

        var bus = checked((uint)reader.Get(24));
        var px = reader.GetFixed(0.01d, 12);
        var py = reader.GetFixed(0.01d, 13);
        var pz = reader.GetFixed(0.01d, 10);
        var ph =
            (float)(reader.Get(8) * 360d / 256d);
        var seatRaw = checked((byte)reader.Get(8));
        return new OpenOmsiWorldPersonState(
            id,
            activity,
            place,
            px,
            py,
            pz,
            ph,
            0f,
            VehicleId: bus,
            Seat: seatRaw == 255 ? null : seatRaw);
    }

    private static int PersonBits(
        OpenOmsiWorldPersonState person) =>
        person.PlaceKind switch
        {
            OpenOmsiWorldPersonPlaceKind.Foot =>
                PersonFootBits +
                (person.WaitingStopObjectId is not null &&
                 person.WaitingPlace is not null
                    ? PersonWaitBits
                    : 0),
            OpenOmsiWorldPersonPlaceKind.Vehicle or
                OpenOmsiWorldPersonPlaceKind.PlayerBus =>
                PersonAboardBits,
            _ => int.MaxValue
        };

    private static (int X, int Y, short Z) ResolveAnchor(
        OpenOmsiWorldFrame frame)
    {
        var firstCar = frame.Cars.FirstOrDefault();
        if (firstCar is not null)
        {
            return (
                RoundInt32(firstCar.X),
                RoundInt32(firstCar.Y),
                ClampInt16(RoundInt32(firstCar.Z)));
        }

        var person = frame.People.FirstOrDefault(
            item =>
                item.PlaceKind ==
                OpenOmsiWorldPersonPlaceKind.Foot);
        if (person is not null)
        {
            return (
                RoundInt32(person.X),
                RoundInt32(person.Y),
                ClampInt16(RoundInt32(person.Z)));
        }

        return (0, 0, 0);
    }

    private static bool Fits(
        int ax,
        int ay,
        short az,
        double x,
        double y,
        double z) =>
        double.IsFinite(x) &&
        double.IsFinite(y) &&
        double.IsFinite(z) &&
        Math.Abs(x - ax) < 2600d &&
        Math.Abs(y - ay) < 2600d &&
        Math.Abs(z - az) < 650d;

    private static int RoundInt32(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return (int)Math.Clamp(
            Math.Round(value),
            int.MinValue,
            int.MaxValue);
    }

    private static short ClampInt16(int value) =>
        (short)Math.Clamp(
            value,
            short.MinValue,
            short.MaxValue);

    private static long Quantize(double value, double step)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return checked((long)Math.Round(value / step));
    }

    private sealed class BitWriter
    {
        private readonly List<byte> _buffer;
        private ulong _accumulator;
        private int _bits;

        public BitWriter(ReadOnlySpan<byte> header)
        {
            _buffer = new List<byte>(header.Length + 256);
            foreach (var value in header)
            {
                _buffer.Add(value);
            }
        }

        public void Put(ulong value, int bits)
        {
            if (bits is < 1 or > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(bits));
            }

            var mask =
                bits == 32
                    ? uint.MaxValue
                    : (1UL << bits) - 1UL;
            _accumulator |=
                (value & mask) << _bits;
            _bits += bits;
            while (_bits >= 8)
            {
                _buffer.Add((byte)_accumulator);
                _accumulator >>= 8;
                _bits -= 8;
            }
        }

        public void PutSigned(long value, int bits)
        {
            var max = (1L << (bits - 1)) - 1L;
            var clamped =
                Math.Clamp(
                    value,
                    -max - 1L,
                    max);
            Put(unchecked((ulong)clamped), bits);
        }

        public void PutUnsigned(long value, int bits)
        {
            var max = (1L << bits) - 1L;
            Put(
                unchecked((ulong)Math.Clamp(value, 0L, max)),
                bits);
        }

        public void PutFixed(
            double value,
            double step,
            int bits) =>
            PutSigned(
                Quantize(value, step),
                bits);

        public byte[] Finish()
        {
            if (_bits > 0)
            {
                _buffer.Add((byte)_accumulator);
            }
            return _buffer.ToArray();
        }
    }

    private ref struct BitReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _bit;

        public BitReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _bit = 0;
        }

        public ulong Get(int bits)
        {
            if (!TryGet(bits, out var value))
            {
                throw new InvalidDataException("Cut openOMSI WORLD bit stream.");
            }
            return value;
        }

        public bool TryGet(int bits, out ulong value)
        {
            value = 0;
            if (bits is < 1 or > 32 ||
                _bit + bits > _data.Length * 8)
            {
                return false;
            }

            for (var k = 0; k < bits; k++)
            {
                var position = _bit + k;
                value |=
                    (ulong)((_data[position / 8] >>
                             (position % 8)) & 1)
                    << k;
            }

            _bit += bits;
            return true;
        }

        public long GetSigned(int bits)
        {
            var value = checked((long)Get(bits));
            var sign = 1L << (bits - 1);
            return (value ^ sign) - sign;
        }

        public double GetFixed(
            double step,
            int bits) =>
            GetSigned(bits) * step;
    }
}
