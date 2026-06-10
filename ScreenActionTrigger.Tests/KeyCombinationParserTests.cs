using FluentAssertions;
using ScreenActionTrigger.Input;
using Xunit;

namespace ScreenActionTrigger.Tests;

public class KeyCombinationParserTests
{
    [Fact]
    public void Parse_CtrlPlusA_ReturnsTwoParts()
    {
        var parts = KeyCombinationParser.Parse("CTRL+A");
        parts.Should().Equal("CTRL", "A");
    }

    [Fact]
    public void Parse_Empty_ReturnsEmpty()
    {
        KeyCombinationParser.Parse(null).Should().BeEmpty();
        KeyCombinationParser.Parse("  ").Should().BeEmpty();
    }
}
