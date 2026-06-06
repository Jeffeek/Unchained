using MudBlazor.Services;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Rendering.Engine;
using Unchained.Studio.Services;

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
builder.Services.AddSingleton<PdfRenderer>();

// Studio services — scoped to one Blazor circuit (one browser tab)
builder.Services.AddScoped<SessionStateService>();
builder.Services.AddScoped<RenderingService>();
builder.Services.AddScoped<FileExportService>();

// Pdfium reference renderer — singleton (one Pdfium library per process)
builder.Services.AddSingleton<PdfiumReferenceRenderer>();

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
