using System;
using Shouldly;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Engine;

public sealed class DigitalSignatureInfoTests
{
    [Fact]
    public void Defaults()
    {
        var info = new DigitalSignatureInfo();
        info.SignerName.ShouldBe(string.Empty);
        info.SigningTime.ShouldBeNull();
        info.PartUri.ShouldBe(string.Empty);
        info.IsReadable.ShouldBeFalse();
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var when = new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);
        var info = new DigitalSignatureInfo
        {
            SignerName = "CN=Alice",
            SigningTime = when,
            PartUri = "/_xmlsignatures/sig1.xml",
            IsReadable = true
        };
        info.SignerName.ShouldBe("CN=Alice");
        info.SigningTime.ShouldBe(when);
        info.PartUri.ShouldBe("/_xmlsignatures/sig1.xml");
        info.IsReadable.ShouldBeTrue();
    }
}

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
        var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
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
        var props = new DocumentProperties();
        props.CustomProperties["str"] = "value";
        props.CustomProperties["int"] = 42;
        props.CustomProperties["bool"] = true;
        props.CustomProperties.Count.ShouldBe(3);
        props.CustomProperties["int"].ShouldBe(42);
    }
}
