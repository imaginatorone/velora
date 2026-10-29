using Velora.Runtime.Contracts;

namespace Velora.Persistence;

public enum SaveLoadStatus
{
    Success,
    NotFound,
    Invalid,
}

/// <summary>A non-throwing result for user-selected, untrusted save files.</summary>
public sealed record SaveLoadResult
{
    private SaveLoadResult(
        SaveLoadStatus status,
        RuntimeSnapshot? snapshot,
        string? error)
    {
        Status = status;
        Snapshot = snapshot;
        Error = error;
    }

    public SaveLoadStatus Status { get; }

    public RuntimeSnapshot? Snapshot { get; }

    public string? Error { get; }

    public bool IsSuccess => Status == SaveLoadStatus.Success;

    internal static SaveLoadResult Success(RuntimeSnapshot snapshot) =>
        new(SaveLoadStatus.Success, snapshot, null);

    internal static SaveLoadResult NotFound() =>
        new(SaveLoadStatus.NotFound, null, null);

    internal static SaveLoadResult Invalid(string error) =>
        new(SaveLoadStatus.Invalid, null, error);
}

public sealed record ManualSaveInfo(
    int Slot,
    SaveLoadStatus Status,
    long Timestamp,
    string Speaker,
    string Preview,
    string? Error);
