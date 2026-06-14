using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Xml;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Xml;

public sealed class OoXmlHelperTests
{
    private static XElement Element(params XAttribute[] attributes) =>
        new("e", attributes.Cast<object>().ToArray());

    [Fact]
    public void OoxmlRotationToDegrees_Converts()
    {
        OoXmlHelper.OoxmlRotationToDegrees(2_700_000).ShouldBe(45.0, 0.001);
        OoXmlHelper.OoxmlRotationToDegrees(0).ShouldBe(0.0);
    }

    [Fact]
    public void DegreesToOoxmlRotation_Converts()
    {
        OoXmlHelper.DegreesToOoxmlRotation(45).ShouldBe(2_700_000);
        OoXmlHelper.DegreesToOoxmlRotation(90).ShouldBe(5_400_000);
    }

    [Fact]
    public void Rotation_RoundTrips() => OoXmlHelper.OoxmlRotationToDegrees(OoXmlHelper.DegreesToOoxmlRotation(30)).ShouldBe(30.0, 0.001);

    [Fact]
    public void ToUtf8Bytes_And_ParseXml_RoundTrip()
    {
        var doc = new XDocument(new XElement("root", new XAttribute("a", "1")));
        var bytes = doc.ToUtf8Bytes();
        bytes.ShouldNotBeEmpty();

        var parsed = OoXmlHelper.ParseXml(bytes);
        parsed.Root.ShouldNotBeNull();
        parsed.Root!.Name.LocalName.ShouldBe("root");
        parsed.Root.Attribute("a")!.Value.ShouldBe("1");
    }

    [Fact]
    public void GetAttr_Present_ReturnsValue() =>
        Element(new XAttribute("x", "hello")).GetAttr("x").ShouldBe("hello");

    [Fact]
    public void GetAttr_Absent_ReturnsNull() =>
        Element().GetAttr("missing").ShouldBeNull();

    [Fact]
    public void GetAttr_WithDefault_UsesDefaultWhenAbsent() =>
        Element().GetAttr("missing", "fallback").ShouldBe("fallback");

    [Fact]
    public void GetAttrLong_ParsesValue() =>
        Element(new XAttribute("n", "123456789012")).GetAttrLong("n").ShouldBe(123456789012L);

    [Fact]
    public void GetAttrLong_Unparseable_ReturnsNull() =>
        Element(new XAttribute("n", "abc")).GetAttrLong("n").ShouldBeNull();

    [Fact]
    public void GetAttrLong_WithDefault_UsesDefault() =>
        Element().GetAttrLong("n", 42L).ShouldBe(42L);

    [Fact]
    public void GetAttrInt_ParsesValue() =>
        Element(new XAttribute("n", "777")).GetAttrInt("n").ShouldBe(777);

    [Fact]
    public void GetAttrInt_WithDefault_UsesDefault() =>
        Element().GetAttrInt("n", 9).ShouldBe(9);

    [Fact]
    public void GetAttrDouble_ParsesInvariant()
    {
        var value = Element(new XAttribute("d", "3.14")).GetAttrDouble("d");
        value.ShouldNotBeNull();
        value.Value.ShouldBe(3.14, 0.001);
    }

    [Fact]
    public void GetAttrDouble_Absent_ReturnsNull() =>
        Element().GetAttrDouble("d").ShouldBeNull();

    [
        Theory,
        InlineData("1", true),
        InlineData("true", true),
        InlineData("TRUE", true),
        InlineData("0", false),
        InlineData("false", false)
    ]
    public void GetAttrBool_ParsesKnownForms(string raw, bool expected) =>
        Element(new XAttribute("b", raw)).GetAttrBool("b").ShouldBe(expected);

    [Fact]
    public void GetAttrBool_Absent_ReturnsNull() =>
        Element().GetAttrBool("b").ShouldBeNull();

    [Fact]
    public void GetAttrBool_Unrecognised_ReturnsNull() =>
        Element(new XAttribute("b", "maybe")).GetAttrBool("b").ShouldBeNull();

    [Fact]
    public void Child_Present_ReturnsElement()
    {
        var parent = new XElement("p", new XElement("c"));
        parent.Child("c").ShouldNotBeNull();
    }

    [Fact]
    public void Child_Absent_ReturnsNull() =>
        new XElement("p").Child("c").ShouldBeNull();

    [Fact]
    public void RequiredChild_Present_ReturnsElement()
    {
        var parent = new XElement("p", new XElement("c"));
        parent.RequiredChild("c").Name.LocalName.ShouldBe("c");
    }

    [Fact]
    public void RequiredChild_Absent_Throws() =>
        Should.Throw<OoXmlException>(static () => new XElement("p").RequiredChild("c"));

    [Fact]
    public void Children_ReturnsAllMatching()
    {
        var parent = new XElement("p", new XElement("c"), new XElement("c"), new XElement("d"));
        parent.Children("c").Count().ShouldBe(2);
    }

    [Fact]
    public void GetAttrEmu_Present_WrapsValue() =>
        Element(new XAttribute("v", "914400")).GetAttrEmu("v").ShouldBe(new Emu(914400));

    [Fact]
    public void GetAttrEmu_Absent_ReturnsZero() =>
        Element().GetAttrEmu("v").ShouldBe(Emu.Zero);
}
