using System.Globalization;
using Velora.Runtime.Contracts;

namespace Velora.Runtime;

internal static class RuntimeValueSemantics
{
    internal static bool IsTruthy(RuntimeValue value) => value switch
    {
        RuntimeValue.BooleanValue boolean => boolean.Value,
        RuntimeValue.NumberValue number => number.Value != 0,
        RuntimeValue.StringValue text => text.Value.Length != 0 && text.Value != "false",
        _ => false,
    };

    internal static double ToNumber(RuntimeValue value) => value switch
    {
        RuntimeValue.NumberValue number => number.Value,
        RuntimeValue.BooleanValue boolean => boolean.Value ? 1 : 0,
        RuntimeValue.StringValue text when TryParseLegacyNumber(text.Value, out double number) => number,
        _ => 0,
    };

    internal static bool TryToFiniteNumber(RuntimeValue value, out double result)
    {
        result = value switch
        {
            RuntimeValue.NumberValue number => number.Value,
            RuntimeValue.BooleanValue boolean => boolean.Value ? 1 : 0,
            RuntimeValue.StringValue text when TryParseLegacyNumber(text.Value, out double parsed) => parsed,
            _ => double.NaN,
        };
        return double.IsFinite(result);
    }

    internal static string ToEqualityText(RuntimeValue value) => value switch
    {
        RuntimeValue.StringValue text => text.Value,
        RuntimeValue.NumberValue number => number.Value.ToString("F6", CultureInfo.InvariantCulture),
        RuntimeValue.BooleanValue boolean => boolean.Value ? "true" : "false",
        _ => string.Empty,
    };

    internal static string ToInterpolationText(RuntimeValue value) => value switch
    {
        RuntimeValue.StringValue text => text.Value,
        RuntimeValue.BooleanValue boolean => boolean.Value ? "true" : "false",
        RuntimeValue.NumberValue number => FormatInterpolatedNumber(number.Value),
        _ => string.Empty,
    };

    internal static bool TryParseLegacyNumber(string text, out double value)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Consume the numeric prefix once. An overflowing complete number must
        // fail, not be retried as a shorter (and different) finite number.
        int start = 0;
        while (start < text.Length && text[start] is ' ' or '\t' or '\r' or '\n' or '\v' or '\f')
        {
            start++;
        }

        int end = start;
        if (end < text.Length && text[end] is '+' or '-')
        {
            end++;
        }

        int digits = 0;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
            digits++;
        }

        if (end < text.Length && text[end] == '.')
        {
            end++;
            while (end < text.Length && char.IsAsciiDigit(text[end]))
            {
                end++;
                digits++;
            }
        }

        if (digits > 0 && end < text.Length && text[end] is 'e' or 'E')
        {
            int exponent = end + 1;
            if (exponent < text.Length && text[exponent] is '+' or '-')
            {
                exponent++;
            }

            int exponentDigits = exponent;
            while (exponent < text.Length && char.IsAsciiDigit(text[exponent]))
            {
                exponent++;
            }

            if (exponent > exponentDigits)
            {
                end = exponent;
            }
        }

        if (digits > 0 && double.TryParse(text.AsSpan(start, end - start),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static string FormatInterpolatedNumber(double value)
    {
        if (value >= int.MinValue && value <= int.MaxValue && value == Math.Truncate(value))
        {
            return ((int)value).ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString("F6", CultureInfo.InvariantCulture);
    }
}
