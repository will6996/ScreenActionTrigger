using FluentAssertions;
using ScreenActionTrigger.Core;
using Xunit;

namespace ScreenActionTrigger.Tests;

public class ColorHexHelperTests
{
    [Theory]
    [InlineData("FF0000", "#FF0000")]
    [InlineData("#00FF00", "#00FF00")]
    [InlineData("  #0000FF  ", "#0000FF")]
    public void TryNormalize_ValidHex_ReturnsNormalized(string input, string expected)
    {
        ColorHexHelper.TryNormalize(input, out var normalized).Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ZZZZZZ")]
    [InlineData("12345")]
    public void TryNormalize_InvalidHex_ReturnsFalse(string input)
    {
        ColorHexHelper.TryNormalize(input, out _).Should().BeFalse();
    }
}
