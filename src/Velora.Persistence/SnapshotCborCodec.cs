using Velora.Runtime.Contracts;

namespace Velora.Persistence;

internal static class SnapshotCborCodec
{
    internal static RuntimeSnapshot Read(ref CborReader reader)
    {
        int fieldCount = reader.ReadMapLength(64);
        ulong id = 0;
        ulong scriptLine = 0;
        string label = string.Empty;
        string background = string.Empty;
        string speaker = string.Empty;
        string text = string.Empty;
        bool textComplete = false;
        long timestamp = 0;
        IReadOnlyDictionary<string, RuntimeValue> variables =
            new Dictionary<string, RuntimeValue>(StringComparer.Ordinal);
        IReadOnlyList<SpriteState> sprites = [];
        IReadOnlyList<AudioTrackState> audioTracks = [];

        for (int index = 0; index < fieldCount; index++)
        {
            string key = reader.ReadTextString();
            switch (key)
            {
                case "id":
                    id = reader.ReadUInt64();
                    break;
                case "script_line":
                    scriptLine = reader.ReadUInt64();
                    break;
                case "label":
                    label = reader.ReadTextString();
                    break;
                case "background":
                    background = reader.ReadTextString();
                    break;
                case "speaker":
                    speaker = reader.ReadTextString();
                    break;
                case "text":
                    text = reader.ReadTextString();
                    break;
                case "text_complete":
                    textComplete = reader.ReadBoolean();
                    break;
                case "timestamp":
                    timestamp = reader.ReadInt64();
                    break;
                case "variables":
                    variables = ReadVariables(ref reader);
                    break;
                case "sprites":
                    sprites = ReadSprites(ref reader);
                    break;
                case "audio":
                    audioTracks = ReadAudioTracks(ref reader);
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }

        return new RuntimeSnapshot
        {
            Id = id,
            ScriptLine = scriptLine,
            CurrentLabel = label,
            Variables = variables,
            BackgroundPath = background,
            Sprites = sprites,
            AudioTracks = audioTracks,
            CurrentSpeaker = speaker,
            CurrentText = text,
            TextComplete = textComplete,
            Timestamp = timestamp,
        };
    }

    internal static void Write(CborWriter writer, RuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(snapshot);

        writer.WriteMapHeader(11);
        writer.WriteTextString("id");
        writer.WriteUInt64(snapshot.Id);
        writer.WriteTextString("script_line");
        writer.WriteUInt64(snapshot.ScriptLine);
        writer.WriteTextString("label");
        writer.WriteTextString(snapshot.CurrentLabel);
        writer.WriteTextString("background");
        writer.WriteTextString(snapshot.BackgroundPath);
        writer.WriteTextString("speaker");
        writer.WriteTextString(snapshot.CurrentSpeaker);
        writer.WriteTextString("text");
        writer.WriteTextString(snapshot.CurrentText);
        writer.WriteTextString("text_complete");
        writer.WriteBoolean(snapshot.TextComplete);
        writer.WriteTextString("timestamp");
        writer.WriteInt64(snapshot.Timestamp);
        writer.WriteTextString("variables");
        WriteVariables(writer, snapshot.Variables);
        writer.WriteTextString("sprites");
        WriteSprites(writer, snapshot.Sprites);
        writer.WriteTextString("audio");
        WriteAudioTracks(writer, snapshot.AudioTracks);
    }

    private static Dictionary<string, RuntimeValue> ReadVariables(ref CborReader reader)
    {
        int count = reader.ReadMapLength(PersistenceLimits.MaxVariables);
        var variables = new Dictionary<string, RuntimeValue>(count, StringComparer.Ordinal);
        for (int index = 0; index < count; index++)
        {
            string name = reader.ReadTextString();
            variables[name] = RuntimeValueCborCodec.Read(ref reader);
        }

        return variables;
    }

    private static List<SpriteState> ReadSprites(ref CborReader reader)
    {
        int count = reader.ReadArrayLength(PersistenceLimits.MaxSprites);
        var sprites = new List<SpriteState>(count);
        for (int index = 0; index < count; index++)
        {
            sprites.Add(ReadSprite(ref reader));
        }

        return sprites;
    }

    private static SpriteState ReadSprite(ref CborReader reader)
    {
        int fieldCount = reader.ReadMapLength(64);
        string id = string.Empty;
        string imagePath = string.Empty;
        float x = 0.5f;
        float y = 0.5f;
        float alpha = 1.0f;
        float scaleX = 1.0f;
        float scaleY = 1.0f;
        float rotation = 0.0f;
        int zOrder = 0;
        bool flipped = false;

        for (int index = 0; index < fieldCount; index++)
        {
            string key = reader.ReadTextString();
            switch (key)
            {
                case "id": id = reader.ReadTextString(); break;
                case "image": imagePath = reader.ReadTextString(); break;
                case "x": x = reader.ReadSingle(); break;
                case "y": y = reader.ReadSingle(); break;
                case "alpha": alpha = reader.ReadSingle(); break;
                case "scale_x": scaleX = reader.ReadSingle(); break;
                case "scale_y": scaleY = reader.ReadSingle(); break;
                case "rotation": rotation = reader.ReadSingle(); break;
                case "z_order": zOrder = reader.ReadInt32(); break;
                case "flipped": flipped = reader.ReadBoolean(); break;
                default: reader.SkipValue(); break;
            }
        }

        return new SpriteState
        {
            Id = id,
            ImagePath = imagePath,
            X = x,
            Y = y,
            Alpha = alpha,
            ScaleX = scaleX,
            ScaleY = scaleY,
            Rotation = rotation,
            ZOrder = zOrder,
            Flipped = flipped,
        };
    }

    private static List<AudioTrackState> ReadAudioTracks(ref CborReader reader)
    {
        int count = reader.ReadArrayLength(PersistenceLimits.MaxAudioTracks);
        var tracks = new List<AudioTrackState>(count);
        for (int index = 0; index < count; index++)
        {
            tracks.Add(ReadAudioTrack(ref reader));
        }

        return tracks;
    }

    private static AudioTrackState ReadAudioTrack(ref CborReader reader)
    {
        int fieldCount = reader.ReadMapLength(32);
        string channel = string.Empty;
        string filePath = string.Empty;
        double position = 0.0;
        float volume = 1.0f;
        bool playing = false;

        for (int index = 0; index < fieldCount; index++)
        {
            string key = reader.ReadTextString();
            switch (key)
            {
                case "channel": channel = reader.ReadTextString(); break;
                case "file": filePath = reader.ReadTextString(); break;
                case "position": position = reader.ReadNumber(); break;
                case "volume": volume = reader.ReadSingle(); break;
                case "playing": playing = reader.ReadBoolean(); break;
                default: reader.SkipValue(); break;
            }
        }

        return new AudioTrackState
        {
            Channel = channel,
            FilePath = filePath,
            Position = position,
            Volume = volume,
            Playing = playing,
        };
    }

    private static void WriteVariables(
        CborWriter writer,
        IReadOnlyDictionary<string, RuntimeValue> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        if (variables.Count > PersistenceLimits.MaxVariables)
        {
            throw new InvalidDataException("The variable count exceeds the compatibility limit.");
        }

        writer.WriteMapHeader(variables.Count);
        foreach ((string name, RuntimeValue value) in variables)
        {
            writer.WriteTextString(name);
            RuntimeValueCborCodec.Write(writer, value);
        }
    }

    private static void WriteSprites(CborWriter writer, IReadOnlyList<SpriteState> sprites)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        if (sprites.Count > PersistenceLimits.MaxSprites)
        {
            throw new InvalidDataException("The sprite count exceeds the compatibility limit.");
        }

        writer.WriteArrayHeader(sprites.Count);
        foreach (SpriteState sprite in sprites)
        {
            ArgumentNullException.ThrowIfNull(sprite);
            writer.WriteMapHeader(10);
            writer.WriteTextString("id"); writer.WriteTextString(sprite.Id);
            writer.WriteTextString("image"); writer.WriteTextString(sprite.ImagePath);
            writer.WriteTextString("x"); writer.WriteSingle(sprite.X);
            writer.WriteTextString("y"); writer.WriteSingle(sprite.Y);
            writer.WriteTextString("alpha"); writer.WriteSingle(sprite.Alpha);
            writer.WriteTextString("scale_x"); writer.WriteSingle(sprite.ScaleX);
            writer.WriteTextString("scale_y"); writer.WriteSingle(sprite.ScaleY);
            writer.WriteTextString("rotation"); writer.WriteSingle(sprite.Rotation);
            writer.WriteTextString("z_order"); writer.WriteInt64(sprite.ZOrder);
            writer.WriteTextString("flipped"); writer.WriteBoolean(sprite.Flipped);
        }
    }

    private static void WriteAudioTracks(CborWriter writer, IReadOnlyList<AudioTrackState> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        if (tracks.Count > PersistenceLimits.MaxAudioTracks)
        {
            throw new InvalidDataException("The audio-track count exceeds the compatibility limit.");
        }

        writer.WriteArrayHeader(tracks.Count);
        foreach (AudioTrackState track in tracks)
        {
            ArgumentNullException.ThrowIfNull(track);
            writer.WriteMapHeader(5);
            writer.WriteTextString("channel"); writer.WriteTextString(track.Channel);
            writer.WriteTextString("file"); writer.WriteTextString(track.FilePath);
            writer.WriteTextString("position"); writer.WriteDouble(track.Position);
            writer.WriteTextString("volume"); writer.WriteSingle(track.Volume);
            writer.WriteTextString("playing"); writer.WriteBoolean(track.Playing);
        }
    }
}
