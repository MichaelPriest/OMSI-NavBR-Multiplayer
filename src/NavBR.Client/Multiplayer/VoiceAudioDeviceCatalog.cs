using NAudio.Wave;

namespace NavBR.Client.Multiplayer;

internal sealed record VoiceAudioDevice(int DeviceNumber, string DisplayName);

internal static class VoiceAudioDeviceCatalog
{
    public static IReadOnlyList<VoiceAudioDevice> GetInputDevices()
    {
        var devices = new List<VoiceAudioDevice>();
        try
        {
            for (var index = 0; index < WaveIn.DeviceCount; index++)
            {
                var capabilities = WaveIn.GetCapabilities(index);
                devices.Add(new VoiceAudioDevice(index, capabilities.ProductName));
            }
        }
        catch
        {
            // Audio enumeration is best-effort. VoiceChatService still reports start failures.
        }

        return devices;
    }

    public static IReadOnlyList<VoiceAudioDevice> GetOutputDevices(string defaultLabel)
    {
        var devices = new List<VoiceAudioDevice>
        {
            new(-1, defaultLabel)
        };

        try
        {
            for (var index = 0; index < WaveOut.DeviceCount; index++)
            {
                var capabilities = WaveOut.GetCapabilities(index);
                devices.Add(new VoiceAudioDevice(index, capabilities.ProductName));
            }
        }
        catch
        {
            // Keep Windows default output available even if enumeration fails.
        }

        return devices;
    }

    public static int NormalizeInputDevice(int deviceNumber)
    {
        try
        {
            return WaveIn.DeviceCount > 0 && deviceNumber >= 0 && deviceNumber < WaveIn.DeviceCount
                ? deviceNumber
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static int NormalizeOutputDevice(int deviceNumber)
    {
        try
        {
            return deviceNumber >= 0 && deviceNumber < WaveOut.DeviceCount
                ? deviceNumber
                : -1;
        }
        catch
        {
            return -1;
        }
    }
}
