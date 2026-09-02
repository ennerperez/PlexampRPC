using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace DyviniaUtils.Dialogs {
    public partial class ExceptionDialog : Window {
        public ExceptionDialog() : this(new Exception("Exception"), "Exception", null, false) {
        }

        public ExceptionDialog(Exception ex, string title, string? messagePrefix, bool isCrash) {
            InitializeComponent();

            TextBlock headerText = this.FindControl<TextBlock>("HeaderText")!;
            TextBox exceptionText = this.FindControl<TextBox>("ExceptionText")!;
            Button copyButton = this.FindControl<Button>("CopyButton")!;
            Button closeButton = this.FindControl<Button>("CloseButton")!;

            Title = title;
            if (!isCrash)
                headerText.IsVisible = false;
            else
                headerText.Text = $"{title} has crashed";

            string message = ex.Message;
            if (messagePrefix != null)
                message = messagePrefix + Environment.NewLine + message;
            if (ex.InnerException != null)
                message += Environment.NewLine + Environment.NewLine + ex.InnerException;
            message += Environment.NewLine + Environment.NewLine + ex.StackTrace;
            exceptionText.Text = message;

            if (isCrash) closeButton.Click += (_, _) => Environment.Exit(0);
            else closeButton.Click += (_, _) => Close();
            copyButton.Click += async (_, _) => {
                IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is not null)
                    await clipboard.SetTextAsync(message);
            };
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        public static void Show(Exception ex, string title, string? messagePrefix = null, bool isCrash = false) {
            Dispatcher.UIThread.Post(() => {
                Window? owner = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;
                ExceptionDialog window = new(ex, title, messagePrefix, isCrash);
                if (owner is not null && owner.IsVisible)
                    _ = window.ShowDialog(owner);
                else
                    window.Show();
            });
        }

        public static void UnhandledException(Exception exception) {
            Show(exception, Assembly.GetEntryAssembly()?.GetName().Name ?? "Exception", null, true);
        }
    }
}
