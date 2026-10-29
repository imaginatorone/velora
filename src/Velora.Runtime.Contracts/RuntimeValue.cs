namespace Velora.Runtime.Contracts;

/// <summary>Represents the three value kinds supported by compatibility-era VelScript.</summary>
public abstract record RuntimeValue
{
    private RuntimeValue()
    {
    }

    public sealed record StringValue(string Value) : RuntimeValue;

    public sealed record NumberValue(double Value) : RuntimeValue;

    public sealed record BooleanValue(bool Value) : RuntimeValue;
}
