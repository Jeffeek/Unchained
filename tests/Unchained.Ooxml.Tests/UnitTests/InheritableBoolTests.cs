using Shouldly;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests;

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
        // Same instance should compare equal
        InheritableBool.True.Equals(InheritableBool.True).ShouldBeTrue();

    [Fact]
    public void Equality_DifferentStates_AreNotEqual() =>
        (InheritableBool.True == InheritableBool.False).ShouldBeFalse();

    [Fact]
    public void ToString_Inherit_ReturnsInherit() =>
        InheritableBool.Inherit.ToString().ShouldBe("Inherit");

    [Fact]
    public void ToString_True_ReturnsTrue() =>
        InheritableBool.True.ToString().ShouldBe("True");

    [Fact]
    public void ToString_False_ReturnsFalse() =>
        InheritableBool.False.ToString().ShouldBe("False");

    [Fact]
    public void Inequality_DifferentStates_AreNotEqual() =>
        (InheritableBool.True != InheritableBool.False).ShouldBeTrue();

    [Fact]
    public void Inequality_SameState_IsEqual() =>
        (InheritableBool.True != InheritableBool.From(true)).ShouldBeFalse();

    [Fact]
    public void EqualsObject_SameState_ReturnsTrue() =>
        InheritableBool.True.Equals((object)InheritableBool.From(true)).ShouldBeTrue();

    [Fact]
    public void EqualsObject_DifferentType_ReturnsFalse() =>
        // ReSharper disable once SuspiciousTypeConversion.Global
        InheritableBool.True.Equals("True").ShouldBeFalse();

    [Fact]
    public void EqualsObject_Null_ReturnsFalse() =>
        InheritableBool.True.Equals(null).ShouldBeFalse();

    [Fact]
    public void GetHashCode_SameState_IsEqual() =>
        InheritableBool.True.GetHashCode().ShouldBe(InheritableBool.From(true).GetHashCode());

    [Fact]
    public void GetHashCode_DifferentStates_Differ() =>
        InheritableBool.True.GetHashCode().ShouldNotBe(InheritableBool.False.GetHashCode());
}
