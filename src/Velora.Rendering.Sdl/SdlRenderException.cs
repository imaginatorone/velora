namespace Velora.Rendering.Sdl;

/// <summary>A native SDL failure translated without leaking binding types.</summary>
public sealed class SdlRenderException : InvalidOperationException
{
    internal SdlRenderException(string operation, string? nativeError, Exception? innerException = null)
        : base(CreateMessage(operation, nativeError), innerException)
    {
        Operation = operation;
        NativeError = string.IsNullOrWhiteSpace(nativeError) ? "Unknown SDL error." : nativeError;
    }

    public string Operation { get; }

    public string NativeError { get; }

    private static string CreateMessage(string operation, string? nativeError)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        string detail = string.IsNullOrWhiteSpace(nativeError) ? "Unknown SDL error." : nativeError;
        return $"SDL operation '{operation}' failed: {detail}";
    }
}

public sealed record SdlInitializationError(string Operation, string Message);
