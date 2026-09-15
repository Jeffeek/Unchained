using Shouldly;
using Unchained.Xlsx.Extensions.Highcharts.Models;
using Xunit;

namespace Unchained.Xlsx.Extensions.Tests.UnitTests;

/// <summary>Tests for plain Highcharts model members not reached through the converter graph.</summary>
public sealed class HighchartsModelsTests
{
    [Fact]
    public void AnnotationItem_ExposesAdditionalPropertiesProperty()
    {
        var item = new AnnotationItem
        {
            AdditionalProperties = new Dictionary<string, object> { ["labelrank"] = 3 }
        };

        item.AdditionalProperties.ShouldNotBeNull();
        item.AdditionalProperties.ShouldContainKeyAndValue("labelrank", 3);
    }
}
