using Avalonia.Threading;

namespace AmphetamineNet;

internal static class UiDispatch
{
    public static void Post(Action action) => Dispatcher.UIThread.Post(action);

    public static void Invoke(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Invoke(action);
    }
}
