using System.IO.Compression;
using Unchained.Studio.Studio;

namespace Unchained.Studio.Tests.Helpers;

public sealed class ZipBuilderTests
{
    [Fact]
    public void Build_EmptyList_ReturnsValidZip()
    {
        var bytes = ZipBuilder.Build([]);

        using var archive = new ZipArchive(new MemoryStream(bytes));
        archive.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Build_SingleFile_CreatesEntry()
    {
        var bytes = ZipBuilder.Build([("hello.txt", "Hello"u8.ToArray())]);

        using var archive = new ZipArchive(new MemoryStream(bytes));
        var entry = archive.GetEntry("hello.txt");
        entry.ShouldNotBeNull();
        entry.Length.ShouldBe(5);
    }

    [Fact]
    public void Build_MultipleFiles_AllPresent()
    {
        var bytes = ZipBuilder.Build(
            [
                ("a.txt", [1]),
                ("b.txt", [2]),
                ("c.txt", [3])
            ]
        );

        using var archive = new ZipArchive(new MemoryStream(bytes));
        var names = archive.Entries.OrderBy(static e => e.Name).Select(static e => e.Name).ToList();
        names.ShouldBe(["a.txt", "b.txt", "c.txt"]);
    }

    [Fact]
    public void Build_NestedPaths_CreateFolders()
    {
        var bytes = ZipBuilder.Build(
            [
                ("folder/sub/file.txt", [1])
            ]
        );

        using var archive = new ZipArchive(new MemoryStream(bytes));
        var entry = archive.GetEntry("folder/sub/file.txt");
        entry.ShouldNotBeNull();
    }

    [Fact]
    public void Build_DataRoundTrips()
    {
        var bytes = ZipBuilder.Build([("msg.txt", "Hello, World!"u8.ToArray())]);

        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var stream = archive.GetEntry("msg.txt")!.Open();
        using var reader = new StreamReader(stream);
        reader.ReadToEnd().ShouldBe("Hello, World!");
    }
}
