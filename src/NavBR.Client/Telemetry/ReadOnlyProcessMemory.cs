using System.ComponentModel;
using System.Numerics;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NavBR.Client.Omsi;

namespace NavBR.Client.Telemetry;

/// <summary>
/// Small Win32 memory reader that opens OMSI with read/query permissions only.
/// It deliberately exposes no WriteProcessMemory operation.
/// </summary>
internal sealed class ReadOnlyProcessMemory : IDisposable
{
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;
    private const uint StillActive = 259;

    private nint _processHandle;

    private ReadOnlyProcessMemory(nint processHandle, nint moduleBaseAddress, int processId)
    {
        _processHandle = processHandle;
        ModuleBaseAddress = moduleBaseAddress;
        ProcessId = processId;
    }

    public nint ModuleBaseAddress { get; }
    public int ProcessId { get; }

    public static ReadOnlyProcessMemory Open(OmsiProcessInfo processInfo)
    {
        if (!Omsi23004MemoryProfile.ConfigureFor(processInfo))
        {
            throw new NotSupportedException($"Unsupported OMSI executable version: {processInfo.FileVersion}");
        }

        using var process = Process.GetProcessById(processInfo.ProcessId);
        if (process.HasExited)
        {
            throw new InvalidOperationException("OMSI process already exited.");
        }

        var moduleBase = process.MainModule?.BaseAddress ?? nint.Zero;
        if (moduleBase == nint.Zero)
        {
            throw new InvalidOperationException("Could not determine OMSI module base address.");
        }

        var handle = OpenProcess(
            ProcessVmRead | ProcessQueryInformation,
            false,
            processInfo.ProcessId);

        if (handle == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcess failed for OMSI.");
        }

        return new ReadOnlyProcessMemory(handle, moduleBase, processInfo.ProcessId);
    }

    public bool IsProcessAlive
    {
        get
        {
            var handle = Volatile.Read(ref _processHandle);
            return handle != nint.Zero &&
                   GetExitCodeProcess(handle, out var exitCode) &&
                   exitCode == StillActive;
        }
    }

    public nint AddressFromRva(int rva) => nint.Add(ModuleBaseAddress, rva);

    public int ReadInt32(nint address)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        ReadBytes(address, bytes);
        return BitConverter.ToInt32(bytes);
    }

    public uint ReadUInt32(nint address)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        ReadBytes(address, bytes);
        return BitConverter.ToUInt32(bytes);
    }

    public static nint PointerFromUInt32(uint value) => unchecked((nint)(nuint)value);

    public float ReadSingle(nint address)
    {
        Span<byte> bytes = stackalloc byte[sizeof(float)];
        ReadBytes(address, bytes);
        return BitConverter.ToSingle(bytes);
    }

    public void ReadSingles(
        nint address,
        scoped Span<float> values)
    {
        if (values.Length == 0)
        {
            return;
        }

        var byteCount = checked(values.Length * sizeof(float));
        var bytes = new byte[byteCount];

        ReadBytes(address, bytes);
        for (var index = 0; index < values.Length; index++)
        {
            var offset = index * sizeof(float);
            values[index] = BitConverter.ToSingle(
                bytes,
                offset);
        }
    }

    public byte ReadByte(nint address)
    {
        Span<byte> bytes = stackalloc byte[1];
        ReadBytes(address, bytes);
        return bytes[0];
    }

    public MemoryVector3 ReadVector3(nint address)
    {
        Span<byte> bytes = stackalloc byte[12];
        ReadBytes(address, bytes);
        return new MemoryVector3(
            BitConverter.ToSingle(bytes[0..4]),
            BitConverter.ToSingle(bytes[4..8]),
            BitConverter.ToSingle(bytes[8..12]));
    }

    public MemoryQuaternion ReadQuaternion(nint address)
    {
        Span<byte> bytes = stackalloc byte[16];
        ReadBytes(address, bytes);
        return new MemoryQuaternion(
            BitConverter.ToSingle(bytes[0..4]),
            BitConverter.ToSingle(bytes[4..8]),
            BitConverter.ToSingle(bytes[8..12]),
            BitConverter.ToSingle(bytes[12..16]));
    }

    public Matrix4x4 ReadMatrix4x4(nint address)
    {
        Span<byte> bytes = stackalloc byte[64];
        ReadBytes(address, bytes);
        return new Matrix4x4(
            BitConverter.ToSingle(bytes[0..4]),
            BitConverter.ToSingle(bytes[4..8]),
            BitConverter.ToSingle(bytes[8..12]),
            BitConverter.ToSingle(bytes[12..16]),
            BitConverter.ToSingle(bytes[16..20]),
            BitConverter.ToSingle(bytes[20..24]),
            BitConverter.ToSingle(bytes[24..28]),
            BitConverter.ToSingle(bytes[28..32]),
            BitConverter.ToSingle(bytes[32..36]),
            BitConverter.ToSingle(bytes[36..40]),
            BitConverter.ToSingle(bytes[40..44]),
            BitConverter.ToSingle(bytes[44..48]),
            BitConverter.ToSingle(bytes[48..52]),
            BitConverter.ToSingle(bytes[52..56]),
            BitConverter.ToSingle(bytes[56..60]),
            BitConverter.ToSingle(bytes[60..64]));
    }

    public string? ReadDelphiUnicodeStringField(nint fieldAddress, int maxCharacters = 1024)
    {
        var stringPointer = ReadUInt32(fieldAddress);
        if (stringPointer <= 0x10000u)
        {
            return null;
        }

        var dataAddress = PointerFromUInt32(stringPointer);
        var length = ReadInt32(nint.Subtract(dataAddress, sizeof(int)));
        if (length <= 0 || length > maxCharacters)
        {
            return null;
        }

        var byteCount = checked(length * 2);
        var bytes = new byte[byteCount];

        ReadBytes(dataAddress, bytes);
        return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    }

    /// <summary>
    /// Reads a Delphi AnsiString field used by OMSI object definitions. The
    /// field stores a 32-bit pointer to the first character and Delphi keeps
    /// the character count immediately before that data pointer.
    /// </summary>
    public string? ReadDelphiAnsiStringField(nint fieldAddress, int maxCharacters = 1024)
    {
        try
        {
            var stringPointer = ReadUInt32(fieldAddress);
            if (stringPointer <= 0x10000u)
            {
                return null;
            }

            var dataAddress = PointerFromUInt32(stringPointer);
            var length = ReadInt32(nint.Subtract(dataAddress, sizeof(int)));
            if (length <= 0 || length > maxCharacters)
            {
                return null;
            }

            var bytes = new byte[length];

            ReadBytes(dataAddress, bytes);
            var value = Encoding.Latin1.GetString(bytes).TrimEnd('\0').Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public string? ReadNullTerminatedUnicodeStringField(nint fieldAddress, int maxCharacters = 256)
    {
        if (maxCharacters <= 0)
        {
            return null;
        }

        try
        {
            var stringPointer = ReadUInt32(fieldAddress);
            if (stringPointer <= 0x10000u)
            {
                return null;
            }

            var dataAddress = PointerFromUInt32(stringPointer);
            var maxBytes = checked(maxCharacters * 2);
            var bytes = new byte[maxBytes];

            var count = TryReadUnicodeTerminatedBlock(
                dataAddress,
                bytes,
                maxCharacters);
            if (count < 0)
            {
                // A single bounded block can fail near a memory-page boundary.
                // Fall back to the conservative pair-by-pair path rather than
                // rejecting a valid OMSI string.
                Span<byte> pair = stackalloc byte[2];
                count = 0;
                for (var index = 0; index < maxCharacters; index++)
                {
                    ReadBytes(
                        nint.Add(dataAddress, checked(index * 2)),
                        pair);
                    if (pair[0] == 0 && pair[1] == 0)
                    {
                        break;
                    }

                    bytes[count++] = pair[0];
                    bytes[count++] = pair[1];
                }
            }

            if (count == 0)
            {
                return null;
            }

            var value = Encoding.Unicode.GetString(bytes[..count]).Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a 32-bit pointer field pointing to OMSI's null-terminated ANSI
    /// timetable text. Latin-1 keeps the operation dependency-free and is a
    /// safe byte-for-byte fallback for route/line labels.
    /// </summary>
    public string? ReadNullTerminatedAnsiStringField(nint fieldAddress, int maxCharacters = 256)
    {
        if (maxCharacters <= 0)
        {
            return null;
        }

        try
        {
            var stringPointer = ReadUInt32(fieldAddress);
            if (stringPointer <= 0x10000u)
            {
                return null;
            }

            var dataAddress = PointerFromUInt32(stringPointer);
            var bytes = new byte[maxCharacters];

            var count = TryReadAnsiTerminatedBlock(
                dataAddress,
                bytes);
            if (count < 0)
            {
                // Same page-boundary fallback as the Unicode reader.
                count = 0;
                for (var index = 0; index < maxCharacters; index++)
                {
                    var value = ReadByte(nint.Add(dataAddress, index));
                    if (value == 0)
                    {
                        break;
                    }

                    bytes[count++] = value;
                }
            }

            if (count == 0)
            {
                return null;
            }

            var text = Encoding.Latin1.GetString(bytes[..count]).Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private int TryReadAnsiTerminatedBlock(
        nint address,
        scoped Span<byte> buffer)
    {
        try
        {
            ReadBytes(address, buffer);
            var terminator = buffer.IndexOf((byte)0);
            return terminator >= 0
                ? terminator
                : buffer.Length;
        }
        catch (Win32Exception)
        {
            return -1;
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    private int TryReadUnicodeTerminatedBlock(
        nint address,
        scoped Span<byte> buffer,
        int maxCharacters)
    {
        try
        {
            ReadBytes(address, buffer);
            var characters = Math.Min(
                maxCharacters,
                buffer.Length / 2);
            for (var index = 0; index < characters; index++)
            {
                var byteIndex = index * 2;
                if (buffer[byteIndex] == 0 &&
                    buffer[byteIndex + 1] == 0)
                {
                    return byteIndex;
                }
            }

            return characters * 2;
        }
        catch (Win32Exception)
        {
            return -1;
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    private byte[] ReadBytes(nint address, int count)
    {
        var buffer = new byte[count];
        ReadBytes(address, buffer);
        return buffer;
    }

    private void ReadBytes(nint address, scoped Span<byte> buffer)
    {
        var handle = Volatile.Read(ref _processHandle);
        ObjectDisposedException.ThrowIf(handle == nint.Zero, this);

        if (buffer.Length == 0)
        {
            return;
        }

        ref var firstByte = ref MemoryMarshal.GetReference(buffer);
        if (!ReadProcessMemory(
                handle,
                address,
                ref firstByte,
                buffer.Length,
                out var bytesRead))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"ReadProcessMemory failed at 0x{address.ToInt64():X}.");
        }

        if (bytesRead.ToInt64() != buffer.Length)
        {
            throw new InvalidOperationException(
                $"Short memory read at 0x{address.ToInt64():X}: expected {buffer.Length}, got {bytesRead.ToInt64()}.");
        }
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _processHandle, nint.Zero);
        if (handle != nint.Zero)
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(
        nint processHandle,
        nint baseAddress,
        ref byte buffer,
        int size,
        out nint numberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(
        nint processHandle,
        out uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}

internal readonly record struct MemoryVector3(float X, float Y, float Z);
internal readonly record struct MemoryQuaternion(float X, float Y, float Z, float W);
