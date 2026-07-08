namespace Unchained.Studio.Infrastructure;

/// <summary>
///     Centralized user feedback for Studio tabs.
///     Replaces duplicated SetBusy/ClearBusy/ShowInfo/ShowError across tabs.
/// </summary>
public interface IUserFeedback
{
    void Info(string msg);
    void Error(string msg);
    void Success(string msg);
}
