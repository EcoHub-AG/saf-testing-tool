using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Media;
using StandardApiFrameworkTool.Helpers;
using StandardApiFrameworkTool.ViewModels;
using StandardApiFrameworkTool.Views;

namespace StandardApiFrameworkTool
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            using (var context = new AppDbContext())
            {
                DBInitializer.Initialize(context);
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();

                UiServices.RunOnUiThread = action => Dispatcher.UIThread.Post(action);
                UiServices.RunOnUiThreadAsync = action =>
                {
                    var tcs = new TaskCompletionSource<bool>();
                    Dispatcher.UIThread.Post(async () =>
                    {
                        try
                        {
                            await action();
                            tcs.SetResult(true);
                        }
                        catch (System.Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    });
                    return tcs.Task;
                };
                UiServices.SetIsProcessing = isProcessing =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (mainWindow.DataContext is MainViewModel vm)
                        {
                            vm.IsProcessing = isProcessing;
                        }
                    });
                UiServices.ShowNotificationAsync = request =>
                    Dispatcher.UIThread.InvokeAsync(() => ShowMessageAsync(mainWindow, request));

                mainWindow.DataContext = new MainViewModel();
                desktop.MainWindow = mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static Task ShowMessageAsync(Window owner, NotificationRequest request)
        {
            var dialog = new Window
            {
                Title = request.Title,
                Width = 480,
                Height = 220,
                CanResize = false,
                WindowStartupLocation = owner != null
                    ? WindowStartupLocation.CenterOwner
                    : WindowStartupLocation.CenterScreen
            };

            var title = new TextBlock
            {
                Text = request.Title,
                FontWeight = FontWeight.Bold,
                FontSize = 16
            };

            var message = new TextBlock
            {
                Text = request.Message,
                TextWrapping = TextWrapping.Wrap
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            okButton.Click += (_, __) => dialog.Close();

            dialog.Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children = { title, message, okButton }
            };

            if (owner != null)
            {
                return dialog.ShowDialog(owner);
            }

            dialog.Show();
            return Task.CompletedTask;
        }
    }
}
