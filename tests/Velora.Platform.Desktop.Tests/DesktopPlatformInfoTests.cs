using System.Globalization;
using Velora.Platform.Desktop;

namespace Velora.Platform.Desktop.Tests;

public sealed class DesktopPlatformInfoTests
{
    [Fact]
    public void UsesInjectedIdentityLocaleAndLocalClock()
    {
        DateTimeOffset localTime = new(2030, 5, 6, 7, 8, 9, TimeSpan.FromHours(3));
        var platform = new DesktopPlatformInfo(
            "Mira",
            "ru-RU",
            new FixedTimeProvider(localTime));

        Assert.Equal("Mira", platform.UserName);
        Assert.Equal("ru-RU", platform.PreferredLocale);
        Assert.Equal("07:08", platform.LocalTimeText);
    }

    [Fact]
    public void LocalTimeFormattingDoesNotDependOnCurrentCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            var platform = new DesktopPlatformInfo(
                string.Empty,
                "en",
                new FixedTimeProvider(new DateTimeOffset(2030, 1, 1, 23, 4, 0, TimeSpan.Zero)));

            Assert.Equal("23:04", platform.LocalTimeText);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset localNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone("Test", localNow.Offset, "Test", "Test");

        public override DateTimeOffset GetUtcNow() => localNow.ToUniversalTime();
    }
}
