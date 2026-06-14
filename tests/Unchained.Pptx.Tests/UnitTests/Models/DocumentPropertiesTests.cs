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
}
