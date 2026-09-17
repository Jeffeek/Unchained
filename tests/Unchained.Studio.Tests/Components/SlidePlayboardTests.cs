using Microsoft.Extensions.DependencyInjection;
using Unchained.Pptx.Engine;
using Unchained.Studio.Components.Pptx;
using Unchained.Studio.Infrastructure;
using Unchained.Studio.Studio.Pptx;
using Unchained.Studio.Tests.Services;

namespace Unchained.Studio.Tests.Components;

/// <summary>Render smoke test for the <see cref="SlidePlayboard" /> component with a blank slide.</summary>
public sealed class SlidePlayboardTests : MudTestContext
{
    [Fact]
    public void Render_WithBlankSlide_RendersWithoutError()
    {
        Services.AddSingleton<IUserFeedback>(new FakeUserFeedback());

        using var processor = new PresentationProcessor();
        var document = processor.CreateBlank();
        var slide = document.Slides.AddBlank(document.Masters[0].Layouts[0]);

        var cut = Render<SlidePlayboard>(pb => pb
            .Add(static c => c.Document, document)
            .Add(static c => c.Slide, slide)
            .Add(static c => c.State, new SlidePlayboardState())
        );

        cut.Markup.ShouldNotBeNullOrWhiteSpace();
    }
}
