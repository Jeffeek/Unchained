using Unchained.Pdf.Core;
using Unchained.Pdf.Document;

namespace Unchained.Pdf.Engine.PageResources;

/// <summary>
///     Low-level PDF object resolution and numeric-conversion helpers shared by the
///     page-resource readers (<see cref="PageFontResolver" />, <see cref="PageImageExtractor" />,
///     <see cref="PageColorSpaceResolver" />, <see cref="PageShadingResolver" /> and others).
///     Each method takes the owning <see cref="PdfDocumentCore" /> explicitly so the readers
///     can stay stateless.
/// </summary>
internal static class PdfResolve
{
    extension(PdfDocumentCore core)
    {
        internal PdfDictionary? ResolveDict(PdfObject? obj) => obj switch
        {
            PdfDictionary d => d,
            PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
            _ => null
        };

        internal PdfObject? ResolveAny(PdfObject? obj) =>
            obj is PdfIndirectReference r ? core.ResolveIndirect(r.ObjectNumber).Value : obj;

        internal PdfDictionary? ResolveDictOrStreamDict(PdfObject? obj) =>
            ResolveAny(core, obj) switch
            {
                PdfStream s => s.Dictionary,
                PdfDictionary d => d,
                _ => null
            };
    }

    extension(PdfObject? obj)
    {
        internal double ReadIntOrReal() => obj switch
        {
            PdfInteger i => i.Value,
            PdfReal r => r.Value,
            _ => 0
        };

        internal float ReadFloat() => (float)ReadIntOrReal(obj);

        internal float[]? ReadFloatArray() => obj is PdfArray a
            ? a.Elements.Select(ReadFloat).ToArray()
            : null;
    }

    extension(PdfStream form)
    {
        internal double[] GetFormBBox()
        {
            if (form.Dictionary[PdfName.BBox] is not PdfArray bbox || bbox.Elements.Count < 4)
                return [0, 0, 1, 1];

            return
            [
                bbox.Elements[0].ReadIntOrReal(),
                bbox.Elements[1].ReadIntOrReal(),
                bbox.Elements[2].ReadIntOrReal(),
                bbox.Elements[3].ReadIntOrReal()
            ];
        }

        internal double[] GetFormMatrix()
        {
            if (form.Dictionary[PdfName.Matrix] is not PdfArray m || m.Elements.Count < 6)
                return [1, 0, 0, 1, 0, 0];

            return
            [
                m.Elements[0].ReadIntOrReal(),
                m.Elements[1].ReadIntOrReal(),
                m.Elements[2].ReadIntOrReal(),
                m.Elements[3].ReadIntOrReal(),
                m.Elements[4].ReadIntOrReal(),
                m.Elements[5].ReadIntOrReal()
            ];
        }
    }

    // Resolves the /ColorSpace entry to a canonical name string.
    // Handles both direct names (/DeviceRGB) and arrays ([/ICCBased <stream>],
    // [/Indexed /DeviceRGB …]) by reading the base-space name or channel count.
    extension(PdfDocumentCore core)
    {
        internal string? ReadColorSpace(PdfDictionary dict)
        {
            var csObj = dict["ColorSpace"];

            switch (csObj)
            {
                case PdfName name:
                    return name.Value;
                case PdfIndirectReference r:
                    csObj = core.ResolveIndirect(r.ObjectNumber).Value;
                break;
            }

            if (csObj is not PdfArray { Count: >= 1 } arr)
                return null;

            var kind = (arr[0] as PdfName)?.Value;

            switch (kind)
            {
                case "ICCBased" when arr.Count >= 2:
                {
                    // The ICC stream's /N entry gives the number of color channels.
                    var iccStream = arr[1] is PdfIndirectReference iccRef
                        ? core.ResolveIndirect(iccRef.ObjectNumber).Value as PdfStream
                        : arr[1] as PdfStream;
                    var n = (int)(iccStream?.Dictionary.Get<PdfInteger>(PdfName.N)?.Value ?? 0);
                    return n switch { 1 => "DeviceGray", 3 => "DeviceRGB", 4 => "DeviceCMYK", _ => null };
                }
                case "Indexed" when arr.Count >= 2:
                    return (arr[1] as PdfName)?.Value; // return base space
            }

            return null;
        }

        internal string? ResolveBaseSpaceName(PdfObject? obj)
        {
            if (obj is PdfIndirectReference r)
                obj = core.ResolveIndirect(r.ObjectNumber).Value;

            switch (obj)
            {
                case PdfName n:
                    return n.Value;
                case PdfArray { Count: >= 1 } arr:
                {
                    var kind = (arr[0] as PdfName)?.Value;
                    switch (kind)
                    {
                        case "ICCBased" when arr.Count >= 2:
                        {
                            var iccStream = arr[1] is PdfIndirectReference iccRef
                                ? core.ResolveIndirect(iccRef.ObjectNumber).Value as PdfStream
                                : arr[1] as PdfStream;
                            var nn = (int)(iccStream?.Dictionary.Get<PdfInteger>(PdfName.N)?.Value ?? 0);
                            return nn switch { 1 => "DeviceGray", 3 => "DeviceRGB", 4 => "DeviceCMYK", _ => null };
                        }
                        case "CalRGB" or "Lab":
                            return "DeviceRGB";
                        case "CalGray":
                            return "DeviceGray";
                    }

                    break;
                }
            }

            return null;
        }
    }

    // Resolves the base colour space of an Indexed space to a Device* name.
}
