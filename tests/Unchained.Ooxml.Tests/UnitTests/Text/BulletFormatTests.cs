using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Text;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Text;

public sealed class BulletFormatTests
{
    [Fact]
    public void Defaults_TypeIsNone_OptionalsAreNull()
    {
        var bullet = new BulletFormat();
        bullet.Type.ShouldBe(BulletType.None);
        bullet.Character.ShouldBeNull();
        bullet.Font.ShouldBeNull();
        bullet.Color.ShouldBeNull();
        bullet.SizePercent.ShouldBeNull();
        bullet.Numbered.ShouldBeNull();
    }

    [Fact]
    public void CharacterBullet_RoundTripsAllProperties()
    {
        var color = ColorSpec.FromRgb(0x10, 0x20, 0x30);
        var bullet = new BulletFormat
        {
            Type = BulletType.Character,
            Character = "•",
            Font = "Arial",
            Color = color,
            SizePercent = 120
        };

        bullet.Type.ShouldBe(BulletType.Character);
        bullet.Character.ShouldBe("•");
        bullet.Font.ShouldBe("Arial");
        bullet.Color.ShouldBe(color);
        bullet.SizePercent.ShouldBe(120);
    }

    [Fact]
    public void NumberedBullet_HoldsNumberedFormat()
    {
        var bullet = new BulletFormat
        {
            Type = BulletType.Numbered,
            Numbered = new NumberedBulletFormat { Style = NumberedBulletStyle.RomanUpperCase, StartAt = 3 }
        };

        bullet.Type.ShouldBe(BulletType.Numbered);
        bullet.Numbered.ShouldNotBeNull();
        bullet.Numbered.Style.ShouldBe(NumberedBulletStyle.RomanUpperCase);
        bullet.Numbered.StartAt.ShouldBe(3);
    }
}
