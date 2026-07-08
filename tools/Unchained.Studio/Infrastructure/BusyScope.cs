namespace Unchained.Studio.Infrastructure;

/// <summary>
///     Scoped busy-state tracker. Replaces _busy/_busyMessage + SetBusy/ClearBusy across tabs.
///     Constructed per component (not injected) with a callback that applies the busy state.
///     Wiring (inside a component):
///     private BusyScope _busy = null!;
///     protected override void OnInitialized() =>
///     _busy = new BusyScope(msg => { _busyMessage = msg ?? string.Empty; _isBusy = msg is not null; StateHasChanged();
///     });
///     Usage:
///     using var _ = _busy.Begin("Loading…");
///     await DoWork();
///     // automatically clears on dispose, even if an exception escapes.
/// </summary>
public sealed class BusyScope(Action<string?> setState)
{
    private readonly Action<string?> _setState = setState ?? throw new ArgumentNullException(nameof(setState));

    /// <summary>
    ///     Begins a named busy scope. Dispose (via <c>using</c>) to release.
    ///     If a scope is already active, the new one nests — the last scope wins.
    /// </summary>
    public IDisposable Begin(string message)
    {
        _setState(message);
        return new ScopeToken(this);
    }

    private sealed class ScopeToken(BusyScope parent) : IDisposable
    {
        public void Dispose() => parent._setState(null);
    }
}
