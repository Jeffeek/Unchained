using System.Runtime.CompilerServices;

// Shared test-infrastructure project. Several fixtures/builders are internal; expose them
// to both consuming test assemblies.
[assembly: InternalsVisibleTo("Unchained.Pdf.Tests")]
[assembly: InternalsVisibleTo("Unchained.Pdf.Rendering.Tests")]
[assembly: InternalsVisibleTo("Unchained.Drawing.Text.Tests")]
