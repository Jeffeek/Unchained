using Shouldly;
using Unchained.Ooxml.Text;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Text;

public sealed class NumberedBulletFormatTests
{
    [Fact]
    public void Defaults_ArabicStartingAtOne()
    {
        var numbered = new NumberedBulletFormat();
        numbered.Style.ShouldBe(NumberedBulletStyle.Arabic);
        numbered.StartAt.ShouldBe(1);
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var numbered = new NumberedBulletFormat
        {
            Style = NumberedBulletStyle.LetterLowerCasePeriod,
            StartAt = 5
        };
        numbered.Style.ShouldBe(NumberedBulletStyle.LetterLowerCasePeriod);
        numbered.StartAt.ShouldBe(5);
    }
}
