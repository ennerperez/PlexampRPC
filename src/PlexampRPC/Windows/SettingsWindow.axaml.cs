using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PlexampRPC.Utils;

namespace PlexampRPC {
    public partial class SettingsWindow : Window {

        private readonly CheckBox startupCheckBox;
        private readonly RadioButton radioListeningPlexamp;
        private readonly RadioButton radioListeningMusic;
        private readonly RadioButton radioListeningCustom;
        private readonly RadioButton radioStatusDetails;
        private readonly RadioButton radioStatusState;
        private readonly RadioButton radioStatusName;

        public SettingsWindow() {
            InitializeComponent();

            startupCheckBox = this.FindControl<CheckBox>("StartupCheckBox")!;
            radioListeningPlexamp = this.FindControl<RadioButton>("RadioListeningPlexamp")!;
            radioListeningMusic = this.FindControl<RadioButton>("RadioListeningMusic")!;
            radioListeningCustom = this.FindControl<RadioButton>("RadioListeningCustom")!;
            radioStatusDetails = this.FindControl<RadioButton>("RadioStatusDetails")!;
            radioStatusState = this.FindControl<RadioButton>("RadioStatusState")!;
            radioStatusName = this.FindControl<RadioButton>("RadioStatusName")!;

            Title += $" {App.Version}";

            CheckForStartup();
            startupCheckBox.IsCheckedChanged += (_, _) => {
                if (startupCheckBox.IsChecked == true)
                    StartOnStartup();
                else
                    DeleteStartupLauncher();
            };

            SetupListeningTo();
            SetupStatusDisplayType();
            SetupStatusDisplayTypeNames();

            DataContext = Config.Settings;
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        private void SetupListeningTo() {
            radioListeningPlexamp.IsCheckedChanged += (_, _) => { if (radioListeningPlexamp.IsChecked == true) Config.Settings.DiscordListeningTo = "Plexamp"; };
            radioListeningMusic.IsCheckedChanged += (_, _) => { if (radioListeningMusic.IsChecked == true) Config.Settings.DiscordListeningTo = "Music"; };
            radioListeningCustom.IsCheckedChanged += (_, _) => { if (radioListeningCustom.IsChecked == true) Config.Settings.DiscordListeningTo = "Custom"; };

            switch (Config.Settings.DiscordListeningTo) {
                case "Plexamp":
                    radioListeningPlexamp.IsChecked = true; break;
                case "Music":
                    radioListeningMusic.IsChecked = true; break;
                default:
                    radioListeningCustom.IsChecked = true; break;
            }
        }

        private void SetupStatusDisplayType() {
            radioStatusName.IsCheckedChanged += (_, _) => { if (radioStatusName.IsChecked == true) Config.Settings.StatusDisplayType = "Name"; };
            radioStatusState.IsCheckedChanged += (_, _) => { if (radioStatusState.IsChecked == true) Config.Settings.StatusDisplayType = "State"; };
            radioStatusDetails.IsCheckedChanged += (_, _) => { if (radioStatusDetails.IsChecked == true) Config.Settings.StatusDisplayType = "Details"; };

            switch (Config.Settings.StatusDisplayType) {
                case "State":
                    radioStatusState.IsChecked = true; break;
                case "Details":
                    radioStatusDetails.IsChecked = true; break;
                default:
                    radioStatusName.IsChecked = true; break;
            }
        }

        private void SetupStatusDisplayTypeNames() {
            radioStatusDetails.Content = Config.Settings.TemplateL1.ApplyPlaceholders();
            radioStatusState.Content = Config.Settings.TemplateL2.ApplyPlaceholders();
        }

        private void Template_LostFocus(object sender, RoutedEventArgs e) => SetupStatusDisplayTypeNames();

        private static void StartOnStartup() {
            string path = StartupLauncherPath;
            if (string.IsNullOrEmpty(path))
                return;

            string targetPath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(targetPath))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $"@echo off\r\nstart \"\" \"{targetPath}\" --startup\r\n");
        }

        private static void DeleteStartupLauncher() {
            string path = StartupLauncherPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }

        private void CheckForStartup() {
            if (File.Exists(StartupLauncherPath)) {
                startupCheckBox.IsChecked = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            if (e.Key == Key.F12) {
                string folder = Path.GetDirectoryName(Config.FilePath)!;
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
        }

        private static string StartupLauncherPath {
            get {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                return string.IsNullOrEmpty(startupFolder) ? string.Empty : Path.Combine(startupFolder, "PlexampRPC.cmd");
            }
        }
    }
}
