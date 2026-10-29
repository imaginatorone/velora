namespace Velora.Runtime;

internal static class TweenEasing
{
    internal static double Evaluate(string name, double value) => name switch
    {
        "linear" => value,
        "ease_in" => value * value * value,
        "ease_in_out" => value < 0.5
            ? 4 * value * value * value
            : (value - 1) * (2 * value - 2) * (2 * value - 2) + 1,
        "bounce" => EaseOutBounce(value),
        "elastic" => EaseOutElastic(value),
        "back" => EaseOutBack(value),
        _ => EaseOut(value),
    };

    private static double EaseOut(double value)
    {
        double shifted = value - 1;
        return shifted * shifted * shifted + 1;
    }

    private static double EaseOutElastic(double value)
    {
        if (value is 0 or 1)
        {
            return value;
        }

        return Math.Pow(2, -10 * value) *
            Math.Sin((value - 0.075) * (2 * Math.PI) / 0.3) + 1;
    }

    private static double EaseOutBounce(double value)
    {
        if (value < 1 / 2.75)
        {
            return 7.5625 * value * value;
        }

        if (value < 2 / 2.75)
        {
            value -= 1.5 / 2.75;
            return 7.5625 * value * value + 0.75;
        }

        if (value < 2.5 / 2.75)
        {
            value -= 2.25 / 2.75;
            return 7.5625 * value * value + 0.9375;
        }

        value -= 2.625 / 2.75;
        return 7.5625 * value * value + 0.984375;
    }

    private static double EaseOutBack(double value)
    {
        const double overshoot = 1.70158;
        double shifted = value - 1;
        return shifted * shifted * ((overshoot + 1) * shifted + overshoot) + 1;
    }
}
