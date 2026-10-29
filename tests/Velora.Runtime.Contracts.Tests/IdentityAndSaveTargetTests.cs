using Velora.Runtime.Contracts;

namespace Velora.Runtime.Contracts.Tests;

public sealed class IdentityAndSaveTargetTests
{
    [Fact]
    public void DefaultInstructionIdIsInvalidAndCannotAliasOrdinalZero()
    {
        InstructionId invalid = default;
        InstructionId first = InstructionId.FromOrdinal(0);

        Assert.False(invalid.IsValid);
        Assert.Equal(-1, invalid.Ordinal);
        Assert.Equal("<invalid>", invalid.ToString());
        Assert.True(first.IsValid);
        Assert.Equal(0, first.Ordinal);
        Assert.NotEqual(invalid, first);
        Assert.True(invalid < first);
    }

    [Fact]
    public void InstructionIdIsAnOrdinalWithinOneProgramOnly()
    {
        Assert.Equal(InstructionId.FromOrdinal(42), InstructionId.FromOrdinal(42));
        Assert.True(InstructionId.FromOrdinal(41) < InstructionId.FromOrdinal(42));
        Assert.Equal(int.MaxValue, InstructionId.FromOrdinal(int.MaxValue).Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => InstructionId.FromOrdinal(-1));
    }

    [Fact]
    public void ProgramFingerprintUsesCompilerVersionAndCanonicalHashForIdentity()
    {
        string upperHash = new('A', ProgramFingerprint.ContentHashLength);
        var first = new ProgramFingerprint("10.0.0", upperHash);
        var same = new ProgramFingerprint("10.0.0", upperHash.ToLowerInvariant());
        var otherCompiler = new ProgramFingerprint("10.0.1", upperHash);

        Assert.Equal(new string('a', ProgramFingerprint.ContentHashLength), first.ContentHash);
        Assert.Equal(first, same);
        Assert.NotEqual(first, otherCompiler);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void ProgramFingerprintRejectsInvalidContentHash(string contentHash)
    {
        Assert.Throws<ArgumentException>(() => new ProgramFingerprint("compiler", contentHash));
    }

    [Fact]
    public void SaveTargetPreservesLegacyManualQuickAndAutoSemantics()
    {
        Assert.False(default(SaveTarget).IsValid);
        Assert.Equal(-1, default(SaveTarget).ManualSlot);

        SaveTarget manual = SaveTarget.Manual(0);
        Assert.True(manual.IsValid);
        Assert.Equal(SaveTargetKind.Manual, manual.Kind);
        Assert.Equal(0, manual.ManualSlot);

        Assert.Equal(SaveTargetKind.Quick, SaveTarget.Quick.Kind);
        Assert.Equal(-1, SaveTarget.Quick.ManualSlot);
        Assert.Equal(SaveTargetKind.Auto, SaveTarget.Auto.Kind);
        Assert.Equal(-1, SaveTarget.Auto.ManualSlot);
        Assert.Throws<ArgumentOutOfRangeException>(() => SaveTarget.Manual(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaveTarget.Manual(21));
    }
}
