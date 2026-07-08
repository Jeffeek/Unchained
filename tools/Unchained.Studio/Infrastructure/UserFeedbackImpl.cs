using MudBlazor;

namespace Unchained.Studio.Infrastructure;

/// <inheritdoc cref="IUserFeedback" />
public sealed class UserFeedback(ISnackbar snackbar) : IUserFeedback
{
    public void Info(string msg) => snackbar.Add(msg, Severity.Info);
    public void Error(string msg) => snackbar.Add(msg, Severity.Error);
    public void Success(string msg) => snackbar.Add(msg, Severity.Success);
}
