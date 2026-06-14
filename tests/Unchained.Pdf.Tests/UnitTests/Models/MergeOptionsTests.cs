using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class MergeOptionsTests
{
    [Fact]
    public void Default_CopiesOutlinesAndDestinations()
    {
        MergeOptions.Default.CopyOutlines.ShouldBeTrue();
        MergeOptions.Default.CopyNamedDestinations.ShouldBeTrue();
        MergeOptions.Default.OptimizeResources.ShouldBeFalse();
    }

    [Fact]
    public void Fast_SkipsOutlinesAndDestinations()
    {
        MergeOptions.Fast.CopyOutlines.ShouldBeFalse();
        MergeOptions.Fast.CopyNamedDestinations.ShouldBeFalse();
    }
}
