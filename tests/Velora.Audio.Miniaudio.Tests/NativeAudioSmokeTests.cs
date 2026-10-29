using System.Runtime.InteropServices;
using Velora.Audio;

namespace Velora.Audio.Miniaudio.Tests;

public sealed class NativeAudioSmokeTests
{
    [Fact]
    public void RealShimOpensUnicodeWavSeeksAndDisposesWithoutAudioHardware()
    {
        string? libraryPath = Environment.GetEnvironmentVariable("VELORA_AUDIO_NATIVE_PATH");
        if (string.IsNullOrEmpty(libraryPath))
        {
            Assert.Skip("Set VELORA_AUDIO_NATIVE_PATH to the built native audio library.");
        }

        NativeLibrary.SetDllImportResolver(typeof(MiniaudioBackend).Assembly,
            (name, _, _) => name == "velora_audio_native" ? NativeLibrary.Load(libraryPath) : 0);
        string directory = Path.Combine(Path.GetTempPath(), $"velora-audio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "мелодия.wav");
        try
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                const int frames = 4800;
                const int dataSize = frames * 2 * sizeof(short);
                writer.Write("RIFF"u8);
                writer.Write(36 + dataSize);
                writer.Write("WAVEfmt "u8);
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)2);
                writer.Write(48000);
                writer.Write(48000 * 4);
                writer.Write((short)4);
                writer.Write((short)16);
                writer.Write("data"u8);
                writer.Write(dataSize);
                writer.Write(new byte[dataSize]);
            }

            using MiniaudioBackend backend = new(PInvokeMiniaudioNativeApi.Instance, useNullDevice: true);
            Assert.True(backend.Initialize().IsSuccess);
            Assert.True(backend.Open(new AudioTrackSnapshot("voice", path, AudioChannel.Voice,
                1, 1, 0, true, false)).IsSuccess);
            Assert.InRange(backend.GetDuration("voice").Value!.Value.TotalSeconds, 0.099, 0.101);
            Assert.True(backend.Seek("voice", TimeSpan.FromSeconds(0.05)).IsSuccess);
            Assert.InRange(backend.GetPosition("voice").Value!.Value.TotalSeconds, 0.049, 0.051);
            Assert.Empty(backend.Apply([new AudioMixSample("voice", AudioChannel.Voice, 0.4, false)]).Errors);
            Assert.True(backend.SuspendPlayback().IsSuccess);
            Assert.True(backend.ResumePlayback().IsSuccess);
            Assert.True(backend.CloseAll().IsSuccess);
            Assert.True(backend.CloseAll().IsSuccess);
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }
}
