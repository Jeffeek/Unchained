using Shouldly;
using Unchained.Pptx.Models;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Models;

public sealed class DocumentPropertiesTests
{
    [Fact]
    public void Defaults_AllOptionalsNull()
    {
        var props = new DocumentProperties();
        props.Title.ShouldBeNull();
        props.Author.ShouldBeNull();
        props.Created.ShouldBeNull();
        props.RevisionNumber.ShouldBeNull();
        props.SlideCount.ShouldBe(0);
        props.CustomProperties.ShouldBeEmpty();
    }

    [Fact]
    public void CoreProperties_RoundTrip()
    {
        // ReSharper disable BadListLineBreaks
        var created = new DateTimeOffset(
            2026,
            1,
            1,
            0,
            0,
            0,
            TimeSpan.Zero
        );
        // ReSharper restore BadListLineBreaks
        var props = new DocumentProperties
        {
            Title = "Deck",
            Subject = "Q2",
            Author = "Bob",
            LastModifiedBy = "Carol",
            Keywords = "a,b",
            Description = "desc",
            Category = "Sales",
            ContentStatus = "Final",
            Created = created
        };
        props.Title.ShouldBe("Deck");
        props.Author.ShouldBe("Bob");
        props.LastModifiedBy.ShouldBe("Carol");
        props.Category.ShouldBe("Sales");
        props.ContentStatus.ShouldBe("Final");
        props.Created.ShouldBe(created);
    }

    [Fact]
    public void CustomProperties_AcceptsVariedTypes()
    {
        var props = new DocumentProperties
        {
            CustomProperties =
            {
                ["str"] = "value",
                ["int"] = 42,
                ["bool"] = true
            }
        };
        props.CustomProperties.Count.ShouldBe(3);
        props.CustomProperties["int"].ShouldBe(42);
    }

    [Fact]
    public void ExtendedAndDateProperties_RoundTrip()
    {
        var printed = new DateTimeOffset(
            2026,
            2,
            3,
            4,
            5,
            6,
            TimeSpan.Zero
        );
        var modified = new DateTimeOffset(
            2026,
            3,
            4,
            0,
            0,
            0,
            TimeSpan.Zero
        );
        var props = new DocumentProperties
        {
            LastPrinted = printed,
            Modified = modified,
            Company = "Acme",
            Manager = "Dana",
            ApplicationName = "Unchained",
            RevisionNumber = 7
        };
        props.LastPrinted.ShouldBe(printed);
        props.Modified.ShouldBe(modified);
        props.Company.ShouldBe("Acme");
        props.Manager.ShouldBe("Dana");
        props.ApplicationName.ShouldBe("Unchained");
        props.RevisionNumber.ShouldBe(7);
    }

    [Fact]
    public void Statistics_AreSettableInternally()
    {
        var props = new DocumentProperties
        {
            SlideCount = 10,
            HiddenSlideCount = 2,
            NoteCount = 3
        };
        props.SlideCount.ShouldBe(10);
        props.HiddenSlideCount.ShouldBe(2);
        props.NoteCount.ShouldBe(3);
    }
}
