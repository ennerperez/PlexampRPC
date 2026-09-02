using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DyviniaUtils.Dialogs {
    public enum DialogSound {
        None,
        Notify,
        Error
    }

    public enum MessageBoxButton {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel
    }

    public enum MessageBoxResult {
        None,
        OK,
        Cancel,
        Yes,
        No
    }

    public partial class MessageBoxDialog : Window {

        private readonly Button okButton;
        private readonly Button yesButton;
        private readonly Button noButton;
        private readonly Button cancelButton;
        private readonly TextBlock messageText;

        private MessageBoxResult result = MessageBoxResult.None;

        public MessageBoxDialog() : this(string.Empty, string.Empty, MessageBoxButton.OK, DialogSound.None) {
        }

        public MessageBoxDialog(string message, string title, MessageBoxButton buttons, DialogSound sound) {
            InitializeComponent();

            okButton = this.FindControl<Button>("OKButton")!;
            yesButton = this.FindControl<Button>("YesButton")!;
            noButton = this.FindControl<Button>("NoButton")!;
            cancelButton = this.FindControl<Button>("CancelButton")!;
            messageText = this.FindControl<TextBlock>("MessageText")!;

            ButtonVisibility(buttons);

            Title = title;
            messageText.Text = message;

            okButton.Click += OnClose;
            yesButton.Click += OnClose;
            noButton.Click += OnClose;
            cancelButton.Click += OnClose;
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        public static async Task<MessageBoxResult> ShowAsync(string message, string title, MessageBoxButton buttons, DialogSound sound = DialogSound.None) {
            Window? owner = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            return await ShowAsync(message, title, owner, buttons, sound);
        }

        public static async Task<MessageBoxResult> ShowAsync(string message, string title, Window? owner, MessageBoxButton buttons, DialogSound sound = DialogSound.None) {
            MessageBoxDialog window = new(message, title, buttons, sound);
            if (owner is not null)
                return await window.ShowDialog<MessageBoxResult>(owner);

            TaskCompletionSource<MessageBoxResult> resultSource = new();
            window.Closed += (_, _) => resultSource.TrySetResult(window.result);
            window.Show();
            return await resultSource.Task;
        }

        private void OnClose(object? sender, RoutedEventArgs e) {
            if (sender == okButton)
                result = MessageBoxResult.OK;
            else if (sender == yesButton)
                result = MessageBoxResult.Yes;
            else if (sender == noButton)
                result = MessageBoxResult.No;
            else if (sender == cancelButton)
                result = MessageBoxResult.Cancel;
            else
                result = MessageBoxResult.None;

            Close(result);
        }

        private void ButtonVisibility(MessageBoxButton button) {
            switch (button) {
                case MessageBoxButton.OK:
                    cancelButton.IsVisible = false;
                    noButton.IsVisible = false;
                    yesButton.IsVisible = false;
                    okButton.Focus();
                    break;
                case MessageBoxButton.OKCancel:
                    noButton.IsVisible = false;
                    yesButton.IsVisible = false;
                    cancelButton.Focus();
                    break;
                case MessageBoxButton.YesNo:
                    okButton.IsVisible = false;
                    cancelButton.IsVisible = false;
                    noButton.Focus();
                    break;
                case MessageBoxButton.YesNoCancel:
                    okButton.IsVisible = false;
                    cancelButton.Focus();
                    break;
                default:
                    break;
            }
        }
    }
}
