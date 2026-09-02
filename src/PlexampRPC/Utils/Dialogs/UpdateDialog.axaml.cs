using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DyviniaUtils.Dialogs {
    public partial class UpdateDialog : Window {
        private readonly TextBlock header;
        private readonly TextBlock releaseNotes;
        private readonly Button installButton;
        private readonly Button webpageButton;
        private readonly Button ignoreButton;

        private bool result;

        public UpdateDialog() {
            InitializeComponent();

            header = this.FindControl<TextBlock>("Header")!;
            releaseNotes = this.FindControl<TextBlock>("ReleaseNotes")!;
            installButton = this.FindControl<Button>("InstallButton")!;
            webpageButton = this.FindControl<Button>("WebpageButton")!;
            ignoreButton = this.FindControl<Button>("IgnoreButton")!;

            installButton.Click += OnClose;
            ignoreButton.Click += OnClose;
        }

        public UpdateDialog(string repoAuthor, string repoName) : this() {
            Title += $" {repoName}";

            webpageButton.Click += (_, _) => Process.Start(new ProcessStartInfo($"https://github.com/{repoAuthor}/{repoName}/releases/latest") { UseShellExecute = true });

            _ = GetUpdateInfo(repoAuthor, repoName);
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        private async Task GetUpdateInfo(string repoAuthor, string repoName) {
            try {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("User-Agent", "request");
                using JsonDocument github = JsonDocument.Parse(await client.GetStringAsync($"https://api.github.com/repos/{repoAuthor}/{repoName}/releases/latest"));
                JsonElement root = github.RootElement;

                header.Text = root.TryGetProperty("name", out JsonElement name) ? name.GetString() : "Update";
                releaseNotes.Text = root.TryGetProperty("body", out JsonElement body) ? body.GetString() : "Open GitHub to view release notes.";
            }
            catch (Exception e) {
                releaseNotes.Text = $"Unable to load release notes.\n{e.Message}";
            }
        }

        public static async Task<bool> ShowAsync(string repoAuthor, string repoName) {
            Window? owner = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            UpdateDialog window = new(repoAuthor, repoName);
            if (owner is not null && owner.IsVisible)
                return await window.ShowDialog<bool>(owner);

            TaskCompletionSource<bool> resultSource = new();
            window.Closed += (_, _) => resultSource.TrySetResult(window.result);
            window.Show();
            return await resultSource.Task;
        }

        private void OnClose(object? sender, RoutedEventArgs e) {
            result = sender == installButton;
            Close(result);
        }
    }
}
