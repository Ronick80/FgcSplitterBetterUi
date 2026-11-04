using System.Globalization;
using FgcKeyboardSplitter.Theme;

namespace FgcKeyboardSplitter.Tests;

public class UpperCaseConverterTests
{
    private static string Convert(object value) =>
        (string)new UpperCaseConverter().Convert(value, typeof(string), null!, CultureInfo.InvariantCulture);

    [Fact]
    public void UpperCasesText() => Assert.Equal("GMMK PRO #2", Convert("Gmmk Pro #2"));

    [Fact]
    public void KeepsTheMicroSignSoMicrosecondsDoNotReadAsMilliseconds() => Assert.Equal("AVERAGE (µs)", Convert("Average (µs)"));

    [Fact]
    public void NonTextBecomesEmpty() => Assert.Equal(string.Empty, Convert(42));
}
