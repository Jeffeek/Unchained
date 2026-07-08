using Unchained.Pdf.Rendering.Abstractions;
using Unchained.Pdf.Rendering.Engine;

namespace Unchained.Pdf.Rendering;

public static class PdfRendererFactory
{
    public static IPdfRenderer CreateRenderer() => new PdfiumPdfRenderer();
}
