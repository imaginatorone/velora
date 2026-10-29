namespace Velora.Persistence;

/// <summary>
/// Byte order of the machine that created a host-native VSAV v1 payload. The committed synthetic
/// contract fixture is explicitly <see cref="LittleEndian"/>; historical files did not store this
/// metadata in their header.
/// </summary>
public enum LegacyV1ByteOrder
{
    LittleEndian,
    BigEndian,
}
