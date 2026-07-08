using MudBlazor.Services;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Rendering.Abstractions;
using Unchained.Pptx.Engine;
using Unchained.Studio.Features.Xlsx;
using Unchained.Studio.Infrastructure;
using Unchained.Studio.Services;
using Unchained.Xlsx.Engine;

FeatureFlags.EnablePdfiumCompare = false;

var builder = WebApplication.CreateBuilder(args);

// ── Classic Blazor Server (not Blazor Web App hybrid) ─────────────────────────
// Blazor Web App hybrid mode has a race window: document-level events fire
// before the SignalR circuit is established, causing "No interop methods
// are registered for renderer N". In classic Blazor Server, the circuit is
// established BEFORE components render — no race window exists.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddMudServices();

// Unchained processors — singleton; stateless and thread-safe
builder.Services.AddSingleton<DocumentProcessor>();
builder.Services.AddSingleton<IPdfRenderer>(static _ => Unchained.Pdf.Rendering.PdfRendererFactory.CreateRenderer());
builder.Services.AddSingleton<PresentationProcessor>();
builder.Services.AddSingleton<SpreadsheetProcessor>();

// Studio services — scoped to one Blazor circuit (one browser tab)
builder.Services.AddScoped<SessionStateService>();
builder.Services.AddScoped<RenderingService>();
builder.Services.AddScoped<FileExportService>();
builder.Services.AddScoped<ThemeService>();

// Studio infrastructure — scoped to one Blazor circuit
builder.Services.AddScoped<IUserFeedback, UserFeedback>();
builder.Services.AddScoped<IStudioDialogs, StudioDialogs>();

// Xlsx editor — scoped per circuit, instantiated lazily via factory
builder.Services.AddScoped(static sp =>
    {
        var session = sp.GetRequiredService<SessionStateService>();
        var dialogs = sp.GetRequiredService<IStudioDialogs>();
        var feedback = sp.GetRequiredService<IUserFeedback>();
        return new XlsxEditorViewModel(session, dialogs, feedback, () => session.Xlsx?.Document.Sheets[session.Xlsx.CurrentSheet - 1]);
    }
);

// Pdfium reference renderer — created lazily by components when the feature is enabled.
// No DI registration needed; components call `new PdfiumReferenceRenderer()` directly.

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
