using System.Text;
using Unchained.Drawing.Primitives.Extensions;
using Unchained.Ooxml;
using Unchained.Pptx.Engine;

namespace Unchained.Pptx.Export;

/// <summary>
///     Exports a <see cref="PresentationDocument" /> as a single self-contained HTML5 file: every slide
///     is rendered into one document, with keyboard (arrows / Space / Home / End), click, and on-screen
///     navigation, an optional slide counter, and a fullscreen toggle. Slides are scaled to fit the
///     viewport while preserving aspect ratio.
/// </summary>
internal static class PptxToHtmlPlayerWriter
{
    private const double EmuToPx = Emu.EmuToCssPx;

    public static byte[] Write(PresentationDocument document, HtmlPlayerSaveOptions options)
    {
        var slides = document.Slides;
        var slideW = document.SlideSize.Width.Value * EmuToPx;
        var slideH = document.SlideSize.Height.Value * EmuToPx;

        var included = Enumerable.Range(0, slides.Count)
            .Where(i => !slides[i].IsHidden || options.IncludeHiddenSlides)
            .ToList();

        // The per-slide writer takes an HtmlSaveOptions; mirror the relevant flags.
        var slideOpts = new HtmlSaveOptions { EmbedImages = options.EmbedImages };

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine($"<title>{ExportText.EscapeHtml(options.Title ?? HtmlPlayerDefaults.DefaultTitle)}</title>");
        WriteStyle(sb, slideW, slideH, options);
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        sb.AppendLine("<div id=\"deck\">");
        for (var idx = 0; idx < included.Count; idx++)
        {
            options.Progress?.Report((double)idx / included.Count);
            var slide = slides[included[idx]];
            var display = idx == 0 ? "block" : "none";
            sb.AppendLine($"<section class=\"slide-page\" data-index=\"{idx}\" style=\"display:{display}\">");
            sb.AppendLine("<div class=\"slide\">");
            PptxToHtmlWriter.WriteSlideContent(sb, slide, slideW, slideH, slideOpts);
            sb.AppendLine("</div>");
            sb.AppendLine("</section>");
        }

        sb.AppendLine("</div>");

        WriteChrome(sb, included.Count, options);
        WriteScript(sb, included.Count);

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        options.Progress?.Report(1.0);
        return sb.ToString().ToUtf8Span().ToArray();
    }

    private static void WriteStyle(
        StringBuilder sb,
        double slideW,
        double slideH,
        HtmlPlayerSaveOptions options
    )
    {
        sb.AppendLine("<style>");
        sb.AppendLine("*{box-sizing:border-box;margin:0;padding:0}");
        sb.AppendLine("html,body{width:100%;height:100%;background:#1e1e1e;overflow:hidden;font-family:sans-serif}");
        // Center the stage and scale slides to fit while preserving aspect ratio.
        sb.AppendLine("#deck{position:absolute;inset:0;display:flex;align-items:center;justify-content:center}");
        sb.AppendLine(".slide-page{position:absolute}");
        sb.AppendLine(
            $".slide{{position:relative;width:{slideW:F2}px;height:{slideH:F2}px;overflow:hidden;background:white;transform-origin:center center}}"
        );
        sb.AppendLine(".shape{position:absolute;overflow:hidden}");
        sb.AppendLine(".text-frame{width:100%;height:100%;padding:4px}");
        sb.AppendLine(".para{margin:0;line-height:1.25}");
        sb.AppendLine(
            "#chrome{position:fixed;bottom:0;left:0;right:0;display:flex;gap:12px;align-items:center;justify-content:center;padding:8px;background:rgba(0,0,0,.55);color:#eee;font-size:14px;user-select:none}"
        );
        sb.AppendLine(
            "#chrome button{background:#333;color:#eee;border:1px solid #555;border-radius:4px;padding:4px 10px;cursor:pointer;font-size:14px}"
        );
        sb.AppendLine("#chrome button:hover{background:#444}");
        sb.AppendLine("#counter{min-width:64px;text-align:center}");
        if (options.AdditionalCss != null) sb.AppendLine(options.AdditionalCss);
        sb.AppendLine("</style>");
    }

    private static void WriteChrome(StringBuilder sb, int count, HtmlPlayerSaveOptions options)
    {
        sb.AppendLine("<div id=\"chrome\">");
        sb.AppendLine("<button id=\"prev\" aria-label=\"Previous slide\">&#8592; Prev</button>");
        if (options.ShowSlideCounter)
            sb.AppendLine($"<span id=\"counter\">1 / {count}</span>");
        sb.AppendLine("<button id=\"next\" aria-label=\"Next slide\">Next &#8594;</button>");
        sb.AppendLine("<button id=\"fs\" aria-label=\"Toggle fullscreen\">&#9974; Fullscreen</button>");
        sb.AppendLine("</div>");
    }

    private static void WriteScript(StringBuilder sb, int count)
    {
        // Plain ES5-ish script; no external dependencies. Navigation + responsive scaling.
        sb.AppendLine("<script>");
        sb.AppendLine("(function(){");
        sb.AppendLine($"var total={count};var cur=0;");
        sb.AppendLine("var pages=document.querySelectorAll('.slide-page');");
        sb.AppendLine("var counter=document.getElementById('counter');");
        sb.AppendLine(
            "function show(i){if(i<0)i=0;if(i>total-1)i=total-1;pages[cur].style.display='none';pages[i].style.display='block';cur=i;if(counter)counter.textContent=(i+1)+' / '+total;scale();}"
        );
        sb.AppendLine(
            "function scale(){var p=pages[cur];var s=p.querySelector('.slide');if(!s)return;var sw=s.offsetWidth,sh=s.offsetHeight;var k=Math.min(window.innerWidth/sw,(window.innerHeight-48)/sh);s.style.transform='scale('+k+')';}"
        );
        sb.AppendLine("function next(){show(cur+1);}function prev(){show(cur-1);}");
        sb.AppendLine("document.getElementById('next').addEventListener('click',next);");
        sb.AppendLine("document.getElementById('prev').addEventListener('click',prev);");
        sb.AppendLine(
            "document.getElementById('fs').addEventListener('click',function(){if(!document.fullscreenElement){document.documentElement.requestFullscreen&&document.documentElement.requestFullscreen();}else{document.exitFullscreen&&document.exitFullscreen();}});"
        );
        sb.AppendLine(
            "document.addEventListener('keydown',function(e){switch(e.key){case'ArrowRight':case' ':case'PageDown':next();e.preventDefault();break;case'ArrowLeft':case'PageUp':prev();e.preventDefault();break;case'Home':show(0);break;case'End':show(total-1);break;}});"
        );
        sb.AppendLine("document.getElementById('deck').addEventListener('click',function(e){if(e.target.closest('#chrome'))return;next();});");
        sb.AppendLine("window.addEventListener('resize',scale);");
        sb.AppendLine("show(0);");
        sb.AppendLine("})();");
        sb.AppendLine("</script>");
    }
}
