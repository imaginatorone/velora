using System.Runtime.InteropServices;

namespace Velora.Audio.Miniaudio;

internal interface IMiniaudioNativeApi
{
    uint GetAbiVersion();

    string GetMiniaudioVersion();

    string DescribeResult(int result);

    int EngineCreate(uint sampleRate, uint channels, bool noDevice, out nint engine);

    void EngineDestroy(nint engine);

    int EngineSuspend(nint engine);

    int EngineResume(nint engine);

    int SoundOpenFile(nint engine, string path, bool streaming, out nint sound);

    void SoundDestroy(nint sound);

    int SoundStart(nint sound);

    int SoundStop(nint sound);

    int SoundSetVolume(nint sound, float volume);

    int SoundSetLooping(nint sound, bool looping);

    int SoundAtEnd(nint sound, out bool atEnd);

    int SoundGetDurationSeconds(nint sound, out double seconds);

    int SoundGetCursorSeconds(nint sound, out double seconds);

    int SoundSeekSeconds(nint sound, double seconds);
}

internal sealed partial class PInvokeMiniaudioNativeApi : IMiniaudioNativeApi
{
    public static PInvokeMiniaudioNativeApi Instance { get; } = new();

    private PInvokeMiniaudioNativeApi()
    {
    }

    public uint GetAbiVersion() => NativeMethods.GetAbiVersion();

    public string GetMiniaudioVersion() =>
        Marshal.PtrToStringUTF8(NativeMethods.GetMiniaudioVersion()) ?? string.Empty;

    public string DescribeResult(int result) =>
        Marshal.PtrToStringUTF8(NativeMethods.DescribeResult(result)) ?? string.Empty;

    public int EngineCreate(uint sampleRate, uint channels, bool noDevice, out nint engine) =>
        NativeMethods.EngineCreate(sampleRate, channels, noDevice ? 1 : 0, out engine);

    public void EngineDestroy(nint engine) => NativeMethods.EngineDestroy(engine);

    public int EngineSuspend(nint engine) => NativeMethods.EngineSuspend(engine);

    public int EngineResume(nint engine) => NativeMethods.EngineResume(engine);

    public int SoundOpenFile(nint engine, string path, bool streaming, out nint sound) =>
        NativeMethods.SoundOpenFile(engine, path, streaming ? 1 : 0, out sound);

    public void SoundDestroy(nint sound) => NativeMethods.SoundDestroy(sound);

    public int SoundStart(nint sound) => NativeMethods.SoundStart(sound);

    public int SoundStop(nint sound) => NativeMethods.SoundStop(sound);

    public int SoundSetVolume(nint sound, float volume) =>
        NativeMethods.SoundSetVolume(sound, volume);

    public int SoundSetLooping(nint sound, bool looping) =>
        NativeMethods.SoundSetLooping(sound, looping ? 1 : 0);

    public int SoundAtEnd(nint sound, out bool atEnd)
    {
        int result = NativeMethods.SoundAtEnd(sound, out int nativeAtEnd);
        atEnd = nativeAtEnd != 0;
        return result;
    }

    public int SoundGetDurationSeconds(nint sound, out double seconds) =>
        NativeMethods.SoundGetDurationSeconds(sound, out seconds);

    public int SoundGetCursorSeconds(nint sound, out double seconds) =>
        NativeMethods.SoundGetCursorSeconds(sound, out seconds);

    public int SoundSeekSeconds(nint sound, double seconds) =>
        NativeMethods.SoundSeekSeconds(sound, seconds);

    private static partial class NativeMethods
    {
        private const string LibraryName = "velora_audio_native";

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_get_abi_version")]
        internal static partial uint GetAbiVersion();

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_get_miniaudio_version")]
        internal static partial nint GetMiniaudioVersion();

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_result_description")]
        internal static partial nint DescribeResult(int result);

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_engine_create")]
        internal static partial int EngineCreate(
            uint sampleRate,
            uint channels,
            int noDevice,
            out nint engine);

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_engine_destroy")]
        internal static partial void EngineDestroy(nint engine);

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_engine_suspend")]
        internal static partial int EngineSuspend(nint engine);

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_engine_resume")]
        internal static partial int EngineResume(nint engine);

        [LibraryImport(
            LibraryName,
            EntryPoint = "velora_audio_sound_open_file",
            StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int SoundOpenFile(
            nint engine,
            string path,
            int streaming,
            out nint sound);

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_sound_destroy")]
        internal static partial void SoundDestroy(nint sound);

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_sound_start")]
        internal static partial int SoundStart(nint sound);

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_sound_stop")]
        internal static partial int SoundStop(nint sound);

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_sound_set_volume")]
        internal static partial int SoundSetVolume(nint sound, float volume);

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_sound_set_looping")]
        internal static partial int SoundSetLooping(nint sound, int looping);

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_sound_at_end")]
        internal static partial int SoundAtEnd(nint sound, out int atEnd);

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_sound_get_duration_seconds")]
        internal static partial int SoundGetDurationSeconds(nint sound, out double seconds);

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_sound_get_cursor_seconds")]
        internal static partial int SoundGetCursorSeconds(nint sound, out double seconds);

        [LibraryImport(LibraryName, EntryPoint = "velora_audio_sound_seek_seconds")]
        internal static partial int SoundSeekSeconds(nint sound, double seconds);
    }
}
