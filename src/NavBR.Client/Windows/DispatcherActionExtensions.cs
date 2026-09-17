using System.Windows.Threading;

namespace NavBR.Client.Windows;

internal static class DispatcherActionExtensions
{
    public static DispatcherOperation BeginInvoke(
        this Dispatcher dispatcher,
        DispatcherPriority priority,
        Action action)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(action);
        return dispatcher.BeginInvoke(action, priority);
    }
}
