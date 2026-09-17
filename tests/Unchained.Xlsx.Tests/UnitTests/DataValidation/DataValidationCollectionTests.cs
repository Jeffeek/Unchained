using Shouldly;
using Unchained.Xlsx.Models;
using Xunit;
using DV = Unchained.Xlsx.DataValidation;

namespace Unchained.Xlsx.Tests.UnitTests.DataValidation;

/// <summary>Tests for <see cref="DV.DataValidationCollection" /> operations.</summary>
public sealed class DataValidationCollectionTests
{
    [Fact]
    public void Count_EmptyCollection_ReturnsZero()
    {
        var collection = new DV.DataValidationCollection();
        collection.Count.ShouldBe(0);
    }

    [Fact]
    public void Add_AddsValidation()
    {
        var collection = new DV.DataValidationCollection();
        var validation = new DV.DataValidation { Type = DataValidationType.List };

        var result = collection.Add(validation);

        result.ShouldBeSameAs(validation);
        collection.Count.ShouldBe(1);
        collection[0].ShouldBeSameAs(validation);
    }

    [Fact]
    public void Add_NullValidation_Throws()
    {
        var collection = new DV.DataValidationCollection();
        Should.Throw<ArgumentNullException>(() => collection.Add(null!));
    }

    [Fact]
    public void Indexer_ReturnsCorrectValidation()
    {
        var collection = new DV.DataValidationCollection();
        var v1 = new DV.DataValidation { Type = DataValidationType.List };
        var v2 = new DV.DataValidation { Type = DataValidationType.Whole };
        collection.Add(v1);
        collection.Add(v2);

        collection[0].ShouldBeSameAs(v1);
        collection[1].ShouldBeSameAs(v2);
    }

    [Fact]
    public void Remove_RemovesValidation()
    {
        var collection = new DV.DataValidationCollection();
        var validation = new DV.DataValidation();
        collection.Add(validation);

        collection.Remove(validation);

        collection.Count.ShouldBe(0);
    }

    [Fact]
    public void Remove_NotInCollection_DoesNotThrow()
    {
        var collection = new DV.DataValidationCollection();
        var validation = new DV.DataValidation();

        Should.NotThrow(() => collection.Remove(validation));
    }

    [Fact]
    public void GetEnumerator_Generic_EnumeratesValidations()
    {
        var collection = new DV.DataValidationCollection
        {
            new DV.DataValidation(),
            new DV.DataValidation()
        };

        collection.Count.ShouldBe(2);
    }

    [Fact]
    public void GetEnumerator_NonGeneric_EnumeratesValidations()
    {
        var collection = new DV.DataValidationCollection { new DV.DataValidation() };

        collection.Cast<object>().Count().ShouldBe(1);
    }
}
