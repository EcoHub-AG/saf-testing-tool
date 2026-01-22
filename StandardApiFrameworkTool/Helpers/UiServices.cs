using System;
using System.Threading.Tasks;

namespace StandardApiFrameworkTool.Helpers
{
    public enum NotificationType
    {
        Info,
        Error
    }

    public sealed record NotificationRequest(string Title, string Message, NotificationType Type);

    public static class UiServices
    {
        public static Action<Action> RunOnUiThread { get; set; } = action => action();
        public static Func<Func<Task>, Task> RunOnUiThreadAsync { get; set; } = async action => await action();
        public static Action<bool> SetIsProcessing { get; set; } = _ => { };
        public static Func<NotificationRequest, Task> ShowNotificationAsync { get; set; } = _ => Task.CompletedTask;

        public static Task ShowInfoAsync(string message, string title = "Info")
        {
            return ShowNotificationAsync(new NotificationRequest(title, message, NotificationType.Info));
        }

        public static Task ShowErrorAsync(string message, string title = "Error")
        {
            return ShowNotificationAsync(new NotificationRequest(title, message, NotificationType.Error));
        }

        public static void ShowInfo(string message, string title = "Info")
        {
            _ = ShowInfoAsync(message, title);
        }

        public static void ShowError(string message, string title = "Error")
        {
            _ = ShowErrorAsync(message, title);
        }
    }
}
