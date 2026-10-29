using Velora.Platform;
using Velora.Runtime.Contracts;

namespace Velora.Persistence;

/// <summary>
/// Maps semantic save targets to host-owned atomic storage. Quick saves retain
/// legacy slot-zero compatibility; autosaves use a separate rotating history.
/// </summary>
public sealed class SaveRepository
{
    public const int DefaultAutosaveDepth = 3;
    public const int MaximumAutosaveDepth = 20;

    private static readonly StoragePath PersistentPath = StoragePath.Parse("persistent.vsav");
    private readonly ISaveStorage _storage;

    public SaveRepository(ISaveStorage storage, int autosaveDepth = DefaultAutosaveDepth)
    {
        ArgumentNullException.ThrowIfNull(storage);
        if (autosaveDepth is < 1 or > MaximumAutosaveDepth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(autosaveDepth),
                autosaveDepth,
                $"Autosave depth must range from 1 through {MaximumAutosaveDepth}.");
        }

        _storage = storage;
        AutosaveDepth = autosaveDepth;
    }

    public int AutosaveDepth { get; }

    public void Save(SaveTarget target, RuntimeSnapshot snapshot)
    {
        ValidateTarget(target);
        ArgumentNullException.ThrowIfNull(snapshot);

        byte[] encoded = EncodeSnapshot(snapshot);
        if (target.Kind == SaveTargetKind.Auto)
        {
            RotateAutosaves();
        }

        _storage.WriteAtomic(GetPath(target), encoded);
    }

    public SaveLoadResult Load(SaveTarget target)
    {
        ValidateTarget(target);
        return LoadPath(GetPath(target));
    }

    public SaveLoadResult LoadAutosave(int generation)
    {
        if (generation < 0 || generation >= AutosaveDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(generation));
        }

        return LoadPath(GetAutosavePath(generation));
    }

    public bool Delete(SaveTarget target)
    {
        ValidateTarget(target);
        return _storage.Delete(GetPath(target));
    }

    public IReadOnlyList<ManualSaveInfo> ListManualSlots()
    {
        var slots = new ManualSaveInfo[SaveTarget.MaximumManualSlot + 1];
        for (int slot = SaveTarget.MinimumManualSlot; slot <= SaveTarget.MaximumManualSlot; slot++)
        {
            SaveLoadResult result = Load(SaveTarget.Manual(slot));
            RuntimeSnapshot? snapshot = result.Snapshot;
            slots[slot] = new ManualSaveInfo(
                slot,
                result.Status,
                snapshot?.Timestamp ?? 0,
                snapshot?.CurrentSpeaker ?? string.Empty,
                snapshot?.CurrentText ?? string.Empty,
                result.Error);
        }

        return slots;
    }

    public void SavePersistent(IReadOnlyDictionary<string, RuntimeValue> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        using var output = new MemoryStream();
        PersistentFileWriterV2.Write(output, variables);
        _storage.WriteAtomic(PersistentPath, output.ToArray());
    }

    public IReadOnlyDictionary<string, RuntimeValue> LoadPersistent()
    {
        if (!_storage.TryRead(PersistentPath, out ReadOnlyMemory<byte> bytes))
        {
            return new Dictionary<string, RuntimeValue>(StringComparer.Ordinal);
        }

        using var input = new MemoryStream(bytes.ToArray(), writable: false);
        return PersistentFileReaderV2.Read(input);
    }

    private static byte[] EncodeSnapshot(RuntimeSnapshot snapshot)
    {
        using var output = new MemoryStream();
        SaveFileWriterV2.Write(output, snapshot);
        return output.ToArray();
    }

    private void RotateAutosaves()
    {
        for (int generation = AutosaveDepth - 1; generation > 0; generation--)
        {
            StoragePath destination = GetAutosavePath(generation);
            StoragePath source = GetAutosavePath(generation - 1);
            if (_storage.TryRead(source, out ReadOnlyMemory<byte> data))
            {
                _storage.WriteAtomic(destination, data.Span);
            }
            else
            {
                _ = _storage.Delete(destination);
            }
        }
    }

    private SaveLoadResult LoadPath(StoragePath path)
    {
        if (!_storage.TryRead(path, out ReadOnlyMemory<byte> bytes))
        {
            return SaveLoadResult.NotFound();
        }

        try
        {
            using var input = new MemoryStream(bytes.ToArray(), writable: false);
            return SaveLoadResult.Success(SaveFileReader.Read(input));
        }
        catch (InvalidDataException exception)
        {
            return SaveLoadResult.Invalid(exception.Message);
        }
    }

    private static StoragePath GetPath(SaveTarget target) => target.Kind switch
    {
        SaveTargetKind.Manual => GetManualPath(target.ManualSlot),
        SaveTargetKind.Quick => GetManualPath(0),
        SaveTargetKind.Auto => GetAutosavePath(0),
        _ => throw new ArgumentException("The save target is uninitialized.", nameof(target)),
    };

    private static StoragePath GetManualPath(int slot) => StoragePath.Parse($"save_{slot}.vsav");

    private static StoragePath GetAutosavePath(int generation) =>
        StoragePath.Parse($"autosaves/save_{generation}.vsav");

    private static void ValidateTarget(SaveTarget target)
    {
        if (!target.IsValid ||
            (target.Kind == SaveTargetKind.Manual &&
             target.ManualSlot is < SaveTarget.MinimumManualSlot or > SaveTarget.MaximumManualSlot))
        {
            throw new ArgumentException("The save target is uninitialized or invalid.", nameof(target));
        }
    }
}
