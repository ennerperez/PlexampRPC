using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace PlexampRPC {
    public partial class LogWindow : Window {
        private readonly LogWriter writer;
        private readonly ListBox logBox;
        private readonly Button saveButton;
        private readonly Button copyButton;

        public LogWindow() : this(new LogWriter(false)) {
        }

        public LogWindow(LogWriter logWriter) {
            InitializeComponent();

            writer = logWriter;
            logBox = this.FindControl<ListBox>("LogBox")!;
            saveButton = this.FindControl<Button>("SaveButton")!;
            copyButton = this.FindControl<Button>("CopyButton")!;

            logBox.ItemsSource = writer.Log;
            if (writer.Log.Count > 0)
                logBox.ScrollIntoView(writer.Log.Last());

            ((INotifyCollectionChanged)writer.Log).CollectionChanged += (_, _) => {
                if (writer.Log.Count > 0)
                    logBox.ScrollIntoView(writer.Log.Last());
            };

            saveButton.Click += async (_, _) => await writer.SaveAs(this);
            copyButton.Click += async (_, _) => await CopyToClipboard();
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        protected override async void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control)
                await CopyToClipboard();

            if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
                await writer.SaveAs(this);
        }

        private async Task CopyToClipboard() {
            StringBuilder sb = new();
            foreach (LogWriter.LogItem item in writer.Log) {
                if (item.Message.StartsWith('{'))
                    continue;
                sb.AppendLine($"[{item.Timestamp:HH:mm:ss}] {item.RawMessage}");
            }

            IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
                await clipboard.SetTextAsync(sb.ToString());
        }
    }

    public class LogWriter : TextWriter {
        public enum LogType {
            Info,
            Warn
        }

        public class LogItem(string inMessage) {
            public DateTime Timestamp { get; } = DateTime.Now;

            public LogType Type => RawMessage.StartsWith("WARN:") ? LogType.Warn : LogType.Info;

            public string Prefix => Type == LogType.Warn ? "WARN" : "INFO";
            public IBrush PrefixColor => Type == LogType.Warn ? Brushes.Orange : Brushes.White;

            public string Message => RawMessage.Replace("WARN:", "").Replace("INFO:", "").Trim();

            public string RawMessage { get; } = RemoveTags(inMessage).Trim();
        }

        public ObservableCollection<LogItem> Log = [];

        public LogWriter(bool redirectConsole = true) {
            if (redirectConsole)
                Console.SetOut(this);
        }

        public override void WriteLine(string? value) {
            Dispatcher.UIThread.Post(() => {
                if (Log.Count > 500) Log.RemoveAt(0);
                Log.Add(new LogItem(value ?? string.Empty));
            });
        }

        private string line = string.Empty;

        public override void Write(char value) {
            if (!value.Equals('\r') && !value.Equals('\n')) {
                line += value;
                return;
            }

            if (string.IsNullOrWhiteSpace(line)) {
                line = string.Empty;
                return;
            }

            if (line.Contains("Plex.ServerApi.Api.ApiService")) {
                line = string.Empty;
                return;
            }

            string capturedLine = line;
            Dispatcher.UIThread.Post(() => {
                if (Log.Count > 200)
                    Log.RemoveAt(0);

                if (!string.IsNullOrWhiteSpace(capturedLine))
                    Log.Add(new LogItem(capturedLine));
            });
            line = string.Empty;
        }

        public override Encoding Encoding {
            get { return Encoding.UTF8; }
        }

        private static string RemoveTags(string text) {
            string[] hiddenTags = ["\"id\"", "uuid", "token", "identifier", "secret", "address", "host", "port"];

            if (hiddenTags.Any(c => text.Contains(c, StringComparison.OrdinalIgnoreCase))) {
                if (text.Trim().StartsWith('{')) {
                    List<string> splitText = [.. text.Replace("{", "{,").Split(',')];
                    splitText.RemoveAll(u => hiddenTags.Any(c => u.Contains(c, StringComparison.OrdinalIgnoreCase)));
                    text = string.Join(',', splitText).Replace("{,", "{");
                }
                else if (text.Trim().StartsWith('<')) {
                    List<string> splitText = [.. text.Split()];
                    splitText.RemoveAll(u => hiddenTags.Any(c => u.Contains(c, StringComparison.OrdinalIgnoreCase)));
                    text = string.Join(' ', splitText);
                }
            }
            if (App.Token is not null)
                return text.Replace(App.Token, $"{App.Token?[..3]}...");
            return text;
        }

        public async Task SaveAs(Window owner) {
            FilePickerSaveOptions options = new() {
                SuggestedFileName = "log.txt",
                FileTypeChoices = [
                    new FilePickerFileType("Text") {
                        Patterns = ["*.txt"]
                    }
                ]
            };

            IStorageFile? file = await owner.StorageProvider.SaveFilePickerAsync(options);
            if (file is null)
                return;

            await using Stream stream = await file.OpenWriteAsync();
            await using StreamWriter streamWriter = new(stream, Encoding.UTF8);
            await streamWriter.WriteAsync(ToString());
        }

        public override string ToString() {
            StringBuilder sb = new();
            foreach (LogItem item in Log) {
                sb.AppendLine($"[{item.Timestamp:HH:mm:ss}] {item.RawMessage}");
            }
            return sb.ToString();
        }
    }
}
