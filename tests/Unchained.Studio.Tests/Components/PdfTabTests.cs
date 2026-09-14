using Microsoft.Extensions.DependencyInjection;
using Unchained.Pdf.Engine;
using Unchained.Pptx.Engine;
using Unchained.Studio.Components.Pdf;
using Unchained.Studio.Infrastructure;
using Unchained.Studio.Services;
using Unchained.Studio.Tests.Services;
using Unchained.Xlsx.Engine;

namespace Unchained.Studio.Tests.Components;

/// <summary>Render smoke test for the <see cref="PdfTab" /> component with no document loaded.</summary>
public sealed class PdfTabTests : MudTestContext
{
    [Fact]
    public void Render_NoDocument_RendersEmptyState()
    {
        var session = new SessionStateService(
            new DocumentProcessor(),
            new PresentationProcessor(),
            new SpreadsheetProcessor(),
            new RenderingService(new FakePdfRenderer())
        );
        Services.AddSingleton(session);
        Services.AddSingleton(new FileExportService(JSInterop.JSRuntime));
        Services.AddSingleton<IUserFeedback>(new FakeUserFeedback());
        Services.AddSingleton<IStudioDialogs>(new FakeStudioDialogs());

        var cut = Render<PdfTab>();

        cut.Markup.ShouldNotBeNullOrWhiteSpace();
    }
}
