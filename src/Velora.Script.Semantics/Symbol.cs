using System.Collections.ObjectModel;

namespace Velora.Script.Semantics;

/// <summary>
/// A resolved, case-sensitive VelScript name.
/// </summary>
public sealed class Symbol
{
    private readonly ReadOnlyCollection<SymbolDefinition> _definitions;

    internal Symbol(
        string name,
        SymbolKind kind,
        SymbolDefinition[] definitions,
        bool isImplicit)
    {
        Name = name;
        Kind = kind;
        _definitions = Array.AsReadOnly(definitions);
        IsImplicit = isImplicit;
    }

    public string Name { get; }

    public SymbolKind Kind { get; }

    /// <summary>
    /// Gets every source definition in deterministic source order.
    /// </summary>
    public IReadOnlyList<SymbolDefinition> Definitions => _definitions;

    /// <summary>
    /// Gets the last source definition. This is the compatibility-era winner for duplicate names.
    /// </summary>
    public SymbolDefinition? EffectiveDefinition => _definitions.Count == 0 ? null : _definitions[^1];

    /// <summary>
    /// Gets whether the runtime supplies this symbol without a source declaration.
    /// </summary>
    public bool IsImplicit { get; }

    public override string ToString() => $"{Kind} {Name}";
}
