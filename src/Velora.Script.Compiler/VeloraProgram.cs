using System.Collections.Immutable;
using Velora.Runtime.Contracts;

namespace Velora.Script.Compiler;

public sealed record SourceMapEntry(
    InstructionId InstructionId,
    SourceLocation Source,
    string? ContainingLabel);

/// <summary>A dense source map ordered by instruction ordinal.</summary>
public sealed class VeloraSourceMap
{
    private readonly ImmutableArray<SourceMapEntry> _entries;

    internal VeloraSourceMap(IEnumerable<SourceMapEntry> entries)
    {
        _entries = entries.OrderBy(static entry => entry.InstructionId.Ordinal).ToImmutableArray();
    }

    public ImmutableArray<SourceMapEntry> Entries => _entries;

    public SourceMapEntry GetEntry(InstructionId instructionId)
    {
        if (!instructionId.IsValid || instructionId.Ordinal >= _entries.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(instructionId));
        }

        SourceMapEntry entry = _entries[instructionId.Ordinal];
        if (entry.InstructionId != instructionId)
        {
            throw new InvalidOperationException("The source map is not dense by instruction ordinal.");
        }

        return entry;
    }
}

/// <summary>An immutable, executable, source-mapped Velora IR program.</summary>
public sealed class VeloraProgram
{
    internal VeloraProgram(
        IEnumerable<VeloraInstruction> instructions,
        IEnumerable<InstructionId> initializationInstructionIds,
        InstructionId? entryInstructionId,
        string? entryLabel,
        IEnumerable<GlobalDefinition> definitions,
        ProgramFingerprint fingerprint)
    {
        Instructions = instructions.ToImmutableArray();
        InitializationInstructionIds = initializationInstructionIds.ToImmutableArray();
        EntryInstructionId = entryInstructionId;
        EntryLabel = entryLabel;
        Definitions = definitions.ToImmutableArray();
        Fingerprint = fingerprint;
        SourceMap = new VeloraSourceMap(Instructions.Select(static instruction =>
            new SourceMapEntry(instruction.Id, instruction.Source, instruction.ContainingLabel)));
    }

    public ImmutableArray<VeloraInstruction> Instructions { get; }

    /// <summary>
    /// Gets the restricted source-order preamble executed before <see cref="EntryInstructionId"/>.
    /// It is empty when the effective <c>start</c> label is absent.
    /// </summary>
    public ImmutableArray<InstructionId> InitializationInstructionIds { get; }

    public InstructionId? EntryInstructionId { get; }

    public string? EntryLabel { get; }

    /// <summary>Gets globals sorted by kind and then by ordinal name.</summary>
    public ImmutableArray<GlobalDefinition> Definitions { get; }

    public ProgramFingerprint Fingerprint { get; }

    public VeloraSourceMap SourceMap { get; }

    public VeloraInstruction GetInstruction(InstructionId instructionId)
    {
        if (!instructionId.IsValid || instructionId.Ordinal >= Instructions.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(instructionId));
        }

        return Instructions[instructionId.Ordinal];
    }
}

/// <summary>A compiler result that always retains a recoverable program and complete diagnostics.</summary>
public sealed class VeloraCompilation
{
    internal VeloraCompilation(VeloraProgram program, IEnumerable<CompilationDiagnostic> diagnostics)
    {
        Program = program;
        Diagnostics = diagnostics.ToImmutableArray();
    }

    public VeloraProgram Program { get; }

    public ImmutableArray<CompilationDiagnostic> Diagnostics { get; }

    public bool Succeeded => !Diagnostics.Any(static diagnostic =>
        diagnostic.Severity == CompilationDiagnosticSeverity.Error);
}
