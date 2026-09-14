using System.ComponentModel;
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

        var process = Process.GetProcessById(processInfo.ProcessId);
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

    public nint AddressFromRva(int rva) => nint.Add(ModuleBaseAddress, rva);

    public int ReadInt32(nint address) => BitConverter.ToInt32(ReadBytes(address, sizeof(int)), 0);

    public float ReadSingle(nint address) => BitConverter.ToSingle(ReadBytes(address, sizeof(float)), 0);

    public byte ReadByte(nint address) => ReadBytes(address, 1)[0];

    public MemoryVector3 ReadVector3(nint address)
    {
        var bytes = ReadBytes(address, 12);
        return new MemoryVector3(
            BitConverter.ToSingle(bytes, 0),
            BitConverter.ToSingle(bytes, 4),
            BitConverter.ToSingle(bytes, 8));
    }

    public MemoryQuaternion ReadQuaternion(nint address)
    {
        var bytes = ReadBytes(address, 16);
        return new MemoryQuaternion(
            BitConverter.ToSingle(bytes, 0),
            BitConverter.ToSingle(bytes, 4),
            BitConverter.ToSingle(bytes, 8),
            BitConverter.ToSingle(bytes, 12));
    }

    /// <summary>
    /// Reads an OMSI/Delphi UnicodeString field. The field stores a 32-bit
    /// pointer to UTF-16 data and Delphi stores the character count at ptr-4.
    /// </summary>
    public string? ReadDelphiUnicodeStringField(nint fieldAddress, int maxCharacters = 1024)
    {
        var stringPointer = ReadInt32(fieldAddress);
        if (stringPointer <= 0x10000)
        {
            return null;
        }

        var dataAddress = new nint(stringPointer);
        var length = ReadInt32(nint.Subtract(dataAddress, sizeof(int)));
        if (length <= 0 || length > maxCharacters)
        {
            return null;
        }

        var bytes = ReadBytes(dataAddress, checked(length * 2));
        return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    }

    private byte[] ReadBytes(nint address, int count)
    {
        ObjectDisposedException.ThrowIf(_processHandle == nint.Zero, this);

        var buffer = new byte[count];
        if (!ReadProcessMemory(_processHandle, address, buffer, count, out var bytesRead))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"ReadProcessMemory failed at 0x{address.ToInt64():X}.");
        }

        if (bytesRead.ToInt64() != count)
        {
            throw new InvalidOperationException(
                $"Short memory read at 0x{address.ToInt64():X}: expected {count}, got {bytesRead.ToInt64()}.");
        }

        return buffer;
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
        [Out] byte[] buffer,
        int size,
        out nint numberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}

internal readonly record struct MemoryVector3(float X, float Y, float Z);
internal readonly record struct MemoryQuaternion(float X, float Y, float Z, float W);
