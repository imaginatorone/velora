using Velora.Assets.Diagnostics;

namespace Velora.Assets;

public sealed record AssetLookupResult(AssetEntry? Entry, AssetDiagnostic? Diagnostic)
{
    public bool IsSuccess => Entry is not null && Diagnostic is null;
}
