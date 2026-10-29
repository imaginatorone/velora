using System.Collections.ObjectModel;

namespace Velora.Runtime.Contracts;

internal static class ContractCollections
{
    internal static IReadOnlyList<T> CopyList<T>(
        IEnumerable<T> values,
        string parameterName,
        Action<T>? validate = null)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        T[] copy = values.ToArray();
        if (validate is not null)
        {
            foreach (T value in copy)
            {
                validate(value);
            }
        }

        return Array.AsReadOnly(copy);
    }

    internal static IReadOnlyDictionary<string, T> CopyOrdinalDictionary<T>(
        IEnumerable<KeyValuePair<string, T>> values,
        string parameterName,
        Action<string, T>? validate = null)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        var copy = new SortedDictionary<string, T>(StringComparer.Ordinal);
        foreach ((string key, T value) in values)
        {
            validate?.Invoke(key, value);
            if (!copy.TryAdd(key, value))
            {
                throw new ArgumentException($"The key '{key}' occurs more than once.", parameterName);
            }
        }

        return new ReadOnlyDictionary<string, T>(copy);
    }

    internal static IReadOnlyDictionary<string, T> CopyOrdinalDictionary<T>(
        IReadOnlyDictionary<string, T> values,
        string parameterName,
        Action<string, T>? validate = null) =>
        CopyOrdinalDictionary(values.AsEnumerable(), parameterName, validate);

    internal static void RequireNonEmpty(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, parameterName);
    }

    internal static void RequireFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must be finite.");
        }
    }

    internal static void RequireFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must be finite.");
        }
    }

    internal static void RequireNonNegative(TimeSpan value, string parameterName)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The duration must not be negative.");
        }
    }

    internal static void ValidateRuntimeValue(RuntimeValue? value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        switch (value)
        {
            case RuntimeValue.StringValue stringValue:
                ArgumentNullException.ThrowIfNull(stringValue.Value, parameterName);
                break;
            case RuntimeValue.NumberValue numberValue:
                RequireFinite(numberValue.Value, parameterName);
                break;
            case RuntimeValue.BooleanValue:
                break;
            default:
                throw new ArgumentException("The runtime value kind is unsupported.", parameterName);
        }
    }

    internal static SpriteState ValidateSprite(
        SpriteState? sprite,
        string parameterName,
        bool requireNonEmptyIdentity = true)
    {
        ArgumentNullException.ThrowIfNull(sprite, parameterName);
        ArgumentNullException.ThrowIfNull(sprite.Id, parameterName);
        ArgumentNullException.ThrowIfNull(sprite.ImagePath, parameterName);
        if (requireNonEmptyIdentity)
        {
            RequireNonEmpty(sprite.Id, parameterName);
        }

        RequireFinite(sprite.X, parameterName);
        RequireFinite(sprite.Y, parameterName);
        RequireFinite(sprite.Alpha, parameterName);
        RequireFinite(sprite.ScaleX, parameterName);
        RequireFinite(sprite.ScaleY, parameterName);
        RequireFinite(sprite.Rotation, parameterName);
        return sprite;
    }

    internal static AudioTrackState ValidateAudioTrack(
        AudioTrackState? track,
        string parameterName,
        bool requireNonEmptyIdentity = true)
    {
        ArgumentNullException.ThrowIfNull(track, parameterName);
        ArgumentNullException.ThrowIfNull(track.Channel, parameterName);
        ArgumentNullException.ThrowIfNull(track.FilePath, parameterName);
        if (requireNonEmptyIdentity)
        {
            RequireNonEmpty(track.Channel, parameterName);
        }

        RequireFinite(track.Position, parameterName);
        RequireFinite(track.Volume, parameterName);
        return track;
    }
}
