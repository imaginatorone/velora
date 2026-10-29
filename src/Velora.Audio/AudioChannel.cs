namespace Velora.Audio;

public enum AudioChannel
{
    Bgm,
    Sfx,
    Voice,
}

public static class AudioChannelNames
{
    public static AudioChannel ParseLegacy(string channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (channel.Equals("bgm", StringComparison.Ordinal))
        {
            return AudioChannel.Bgm;
        }

        if (channel.Equals("voice", StringComparison.Ordinal))
        {
            return AudioChannel.Voice;
        }

        return AudioChannel.Sfx;
    }

    public static string ToLegacyName(this AudioChannel channel) => channel switch
    {
        AudioChannel.Bgm => "bgm",
        AudioChannel.Sfx => "sfx",
        AudioChannel.Voice => "voice",
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown audio channel."),
    };
}
