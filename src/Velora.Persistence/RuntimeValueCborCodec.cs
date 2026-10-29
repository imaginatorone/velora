using Velora.Runtime.Contracts;

namespace Velora.Persistence;

internal static class RuntimeValueCborCodec
{
    internal static RuntimeValue Read(ref CborReader reader)
    {
        int fieldCount = reader.ReadMapLength(16);
        string? declaredType = null;
        RuntimeValue? encodedValue = null;

        for (int index = 0; index < fieldCount; index++)
        {
            string key = reader.ReadTextString();
            switch (key)
            {
                case "type":
                    declaredType = reader.ReadTextString();
                    break;
                case "value":
                    encodedValue = ReadEncodedValue(ref reader);
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }

        return (declaredType, encodedValue) switch
        {
            ("string", RuntimeValue.StringValue value) => value,
            ("number", RuntimeValue.NumberValue value) => value,
            ("boolean", RuntimeValue.BooleanValue value) => value,
            (null, _) => throw new InvalidDataException("A persisted variable has no type field."),
            (_, null) => throw new InvalidDataException("A persisted variable has no value field."),
            _ => throw new InvalidDataException("A persisted variable type does not match its value."),
        };
    }

    internal static void Write(CborWriter writer, RuntimeValue value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteMapHeader(2);
        writer.WriteTextString("type");
        switch (value)
        {
            case RuntimeValue.StringValue stringValue:
                writer.WriteTextString("string");
                writer.WriteTextString("value");
                writer.WriteTextString(stringValue.Value);
                break;
            case RuntimeValue.NumberValue numberValue:
                writer.WriteTextString("number");
                writer.WriteTextString("value");
                writer.WriteDouble(numberValue.Value);
                break;
            case RuntimeValue.BooleanValue booleanValue:
                writer.WriteTextString("boolean");
                writer.WriteTextString("value");
                writer.WriteBoolean(booleanValue.Value);
                break;
            default:
                throw new InvalidDataException("Unknown runtime value kind.");
        }
    }

    private static RuntimeValue ReadEncodedValue(ref CborReader reader)
    {
        int majorType = reader.PeekMajorType();
        int additionalInformation = reader.PeekAdditionalInformation();
        if (majorType == 3)
        {
            return new RuntimeValue.StringValue(reader.ReadTextString());
        }

        if (majorType is 0 or 1 || (majorType == 7 && additionalInformation is 25 or 26 or 27))
        {
            return new RuntimeValue.NumberValue(reader.ReadNumber());
        }

        if (majorType == 7 && additionalInformation is 20 or 21)
        {
            return new RuntimeValue.BooleanValue(reader.ReadBoolean());
        }

        throw new InvalidDataException("A persisted variable value has an unsupported CBOR type.");
    }
}
