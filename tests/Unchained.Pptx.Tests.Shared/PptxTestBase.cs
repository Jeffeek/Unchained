using Unchained.Pptx.Engine;

namespace Unchained.Pptx.Tests.Shared;

/// <summary>Base class for all Unchained.Pptx tests. Provides a shared processor instance.</summary>
public abstract class PptxTestBase : IDisposable
{
    /// <summary>The presentation processor under test.</summary>
    protected PresentationProcessor Processor { get; } = new();

    /// <inheritdoc />
    public void Dispose() => Processor.Dispose();
}
