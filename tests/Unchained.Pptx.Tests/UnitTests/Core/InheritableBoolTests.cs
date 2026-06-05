using Shouldly;
using Unchained.Pptx.Core;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Core;

public sealed class InheritableBoolTests
{
    [Fact]
    public void Inherit_IsNotSet()
    {
        InheritableBool.Inherit.IsSet.ShouldBeFalse();
        InheritableBool.Inherit.Value.ShouldBeNull();
    }

    [Fact]
    public void True_IsSetAndValueIsTrue()
    {
        InheritableBool.True.IsSet.ShouldBeTrue();
        InheritableBool.True.Value.ShouldBe(true);
    }

    [Fact]
    public void False_IsSetAndValueIsFalse()
    {
        InheritableBool.False.IsSet.ShouldBeTrue();
        InheritableBool.False.Value.ShouldBe(false);
    }

    [Fact]
    public void From_NullBool_ReturnsInherit() =>
        InheritableBool.From(null).ShouldBe(InheritableBool.Inherit);

    [Fact]
    public void From_TrueBool_ReturnsTrue() =>
        InheritableBool.From(true).ShouldBe(InheritableBool.True);

    [Fact]
    public void From_FalseBool_ReturnsFalse() =>
        InheritableBool.From(false).ShouldBe(InheritableBool.False);

    [Fact]
    public void Equality_SameInstance_IsEqual() =>
        (InheritableBool.True == InheritableBool.True).ShouldBeTrue();

    [Fact]
    public void Equality_DifferentStates_AreNotEqual() =>
        (InheritableBool.True == InheritableBool.False).ShouldBeFalse();

    [Fact]
    public void ToString_Inherit_ReturnsInherit() =>
        InheritableBool.Inherit.ToString().ShouldBe("Inherit");

    [Fact]
    public void ToString_True_ReturnsTrue() =>
        InheritableBool.True.ToString().ShouldBe("True");
}
