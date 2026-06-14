using Unchained.Pdf.Models;

namespace Unchained.Studio.Models;

public sealed record EncryptResult(string UserPassword, string OwnerPassword, PdfEncryptionAlgorithm Algorithm);
