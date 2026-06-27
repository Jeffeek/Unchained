using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Unchained.Studio.Tests.Components;

/// <summary>
///     Base bUnit context for MudBlazor component tests. Registers MudBlazor services,
///     sets loose JS interop (MudBlazor calls JS on render), and disables the popover
///     provider requirement so components using <c>MudMenu</c>/<c>MudSelect</c> render
///     without a full <c>MudPopoverProvider</c> in the tree.
/// </summary>
public abstract class MudTestContext : BunitContext
{
    protected MudTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(options =>
            options.PopoverOptions.CheckForPopoverProvider = false);
    }
}
