using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DiscordRPC;
using PlexampRPC.Data;
using PlexampRPC.Utils;

namespace PlexampRPC {
    public partial class MainWindow : Window {

        private static readonly HttpClient httpClient = new();

        private static readonly JsonSerializerOptions serializerOptions = new() { WriteIndented = true };

        private readonly Image userIcon;
        private readonly TextBlock discordUsername;
        private readonly Image discordAvatar;
        private readonly TextBlock statusTextBox;
        private readonly ComboBox userServerComboBox;
        private readonly StackPanel userInfoPanel;
        private readonly ProgressBar loadingImage;
        private readonly Grid discordStatus;
        private readonly Image previewArt;
        private readonly TextBlock previewL1;
        private readonly TextBlock previewL2;
        private readonly TextBlock previewL3;
        private readonly TextBlock previewListeningTo;
        private readonly TextBlock previewStatusListeningTo;
        private readonly StackPanel previewTime;
        private readonly TextBlock previewTimeStart;
        private readonly TextBlock previewTimeEnd;
        private readonly ProgressBar previewTimeProgress;
        private readonly Grid previewPaused;

        private bool forceClose;

        private PlexResourceData? SelectedResource => userServerComboBox.SelectedItem as PlexResourceData;

        public Uri? SelectedAddress {
            get {
                if (!string.IsNullOrWhiteSpace(Config.Settings.PlexAddress))
                    return new UriBuilder(Config.Settings.PlexAddress).Uri;

                return Config.Settings.LocalAddress ? SelectedResource?.LocalUri : SelectedResource?.Uri;
            }
        }

        public MainWindow() {
            InitializeComponent();

            userIcon = this.FindControl<Image>("UserIcon")!;
            discordUsername = this.FindControl<TextBlock>("DiscordUsername")!;
            discordAvatar = this.FindControl<Image>("DiscordAvatar")!;
            statusTextBox = this.FindControl<TextBlock>("StatusTextBox")!;
            userServerComboBox = this.FindControl<ComboBox>("UserServerComboBox")!;
            userInfoPanel = this.FindControl<StackPanel>("UserInfoPanel")!;
            loadingImage = this.FindControl<ProgressBar>("LoadingImage")!;
            discordStatus = this.FindControl<Grid>("DiscordStatus")!;
            previewArt = this.FindControl<Image>("PreviewArt")!;
            previewL1 = this.FindControl<TextBlock>("PreviewL1")!;
            previewL2 = this.FindControl<TextBlock>("PreviewL2")!;
            previewL3 = this.FindControl<TextBlock>("PreviewL3")!;
            previewListeningTo = this.FindControl<TextBlock>("PreviewListeningTo")!;
            previewStatusListeningTo = this.FindControl<TextBlock>("PreviewStatusListeningTo")!;
            previewTime = this.FindControl<StackPanel>("PreviewTime")!;
            previewTimeStart = this.FindControl<TextBlock>("PreviewTimeStart")!;
            previewTimeEnd = this.FindControl<TextBlock>("PreviewTimeEnd")!;
            previewTimeProgress = this.FindControl<ProgressBar>("PreviewTimeProgress")!;
            previewPaused = this.FindControl<Grid>("PreviewPaused")!;

            httpClient.Timeout = TimeSpan.FromSeconds(2);
            DataContext = Config.Settings;

            PropertyChanged += (_, e) => {
                if (e.Property == WindowStateProperty && WindowState == WindowState.Minimized && !Config.Settings.CloseToTray) {
                    Hide();
                    Console.WriteLine("INFO: Minimized to Tray");
                }
            };

            ResetPresence();
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        public async void UpdateAccountIcon() {
            string iconSource = App.Account?.Thumb ?? "avares://PlexampRPC/Resources/PlexIcon.png";
            userIcon.Source = await LoadBitmap(iconSource);

            discordUsername.Text = App.DiscordClient.CurrentUser?.Username ?? "Discord";
            discordAvatar.Source = await LoadBitmap(App.DiscordClient.CurrentUser?.GetAvatarURL(User.AvatarFormat.PNG, User.AvatarSize.x128) ?? "https://cdn.discordapp.com/embed/avatars/0.png");
        }

        public void GetAccountInfo() {
            UpdateAccountIcon();
            statusTextBox.Text = App.Account?.Title ?? App.Account?.Username ?? "Name";

            if (string.IsNullOrEmpty(Config.Settings.PlexAddress)) {
                if (App.PlexResources != null)
                    userServerComboBox.ItemsSource = App.PlexResources;
            }
            else {
                userServerComboBox.ItemsSource = new List<PlexResourceData>() {
                    new() {
                        Name = Config.Settings.PlexAddress,
                        AccessToken = App.Token,
                        Uri = new UriBuilder(Config.Settings.PlexAddress).Uri,
                        LocalUri = new UriBuilder(Config.Settings.PlexAddress).Uri
                    }
                };
                userServerComboBox.IsEnabled = false;
            }
            userServerComboBox.SelectedIndex = App.PlexResources?.ToList().FindIndex(r => r.Name == Config.Settings.SelectedServer) ?? 0;
            if (userServerComboBox.SelectedIndex == -1)
                userServerComboBox.SelectedIndex = 0;

            userInfoPanel.IsVisible = true;
            userServerComboBox.IsVisible = true;
            loadingImage.IsVisible = false;
        }

        public async void StartPolling() {
            SessionData? lastSession = null;
            DateTime lastUpdated = DateTime.Now;

            while (true) {
                SessionData? currentSession = Config.Settings.LocalPlayer ? await GetLocalSession() : await GetServerSession();
                if (currentSession != null) {
                    if (JsonSerializer.Serialize(currentSession) != JsonSerializer.Serialize(lastSession)) {
                        await SetPresence(await BuildPresence(currentSession));
                        if (currentSession?.Key != lastSession?.Key)
                            Console.WriteLine("Title: {title}\nArtist: {artist}\nAlbum: {album}\nYear: {year}\nPlayer: {player}\nListen Count: {listens}\nCodec: {codec}\nContainer: {container}\nBitrate (Kbps): {bitrate}\nChannel Layout: {channel}\nBit Depth: {bitdepth}\nSamplerate (kHz): {samplerate}".ApplyPlaceholders(currentSession));

                        lastSession = currentSession;
                        lastUpdated = DateTime.Now;
                    }
                    else if (DateTime.Now - lastUpdated > TimeSpan.FromSeconds(Config.Settings.SessionTimeout)) {
                        ResetPresence();
                    }
                    await Task.Delay(TimeSpan.FromSeconds(Config.Settings.RefreshInterval));
                }
                else {
                    ResetPresence();
                    await Task.Delay(TimeSpan.FromSeconds(Config.Settings.RefreshInterval * 2));
                }
            }
        }

        private static async Task<SessionData?> GetLocalSession() {
            try {
                HttpRequestMessage requestMessage = new(HttpMethod.Get, "http://localhost:32500/player/timeline/poll?wait=0&includeMetadata=1&commandID=1");
                requestMessage.Headers.Add("Accept", "application/xml");

                HttpResponseMessage sendResponse = await httpClient.SendAsync(requestMessage);
                sendResponse.EnsureSuccessStatusCode();

                XmlDocument responseXml = new();
                responseXml.LoadXml(await sendResponse.Content.ReadAsStringAsync());

                XmlNode? timelineNode = responseXml.SelectSingleNode("/MediaContainer/Timeline[Track]");
                if (timelineNode is null)
                    return null;

                XmlNode? trackNode = timelineNode.SelectSingleNode("Track");
                if (trackNode is null)
                    return null;

                static string? GetAttr(XmlNode node, string name) => node.Attributes?[name]?.Value;

                SessionData sessionData = new() {
                    Title = GetAttr(trackNode, "title"),
                    Album = GetAttr(trackNode, "parentTitle"),
                    ArtPath = GetAttr(trackNode, "thumb"),
                    Type = GetAttr(trackNode, "type"),
                    Guid = GetAttr(trackNode, "guid"),
                    TrackArtist = GetAttr(trackNode, "originalTitle"),
                    AlbumArtist = GetAttr(trackNode, "grandparentTitle"),
                    Player = new SessionData.PlayerData { State = GetAttr(timelineNode, "state") },
                    ProgressOffset = int.TryParse(GetAttr(timelineNode, "time"), out int vo) ? vo : 0,
                    Duration = int.TryParse(GetAttr(trackNode, "duration") ?? GetAttr(timelineNode, "duration"), out int dur) ? dur : 0,
                    User = App.Account?.Username is not null ? new SessionData.UserData { Name = App.Account.Username } : null
                };

                return sessionData;
            }
            catch (Exception e) {
                Console.WriteLine($"WARN: Unable to get current session: local timeline poll\n{e.Message} {e.InnerException}");
                return null;
            }
        }

        private async Task<SessionData?> GetServerSession() {
            try {
                if (userServerComboBox.SelectedItem is null)
                    return null;
                if (!Uri.IsWellFormedUriString(SelectedAddress?.ToString(), UriKind.Absolute)) {
                    Console.WriteLine("WARN: No server selected or address is invalid");
                    return null;
                }
                HttpRequestMessage requestMessage = new(HttpMethod.Get, $"{SelectedAddress}status/sessions?X-Plex-Token={SelectedResource?.AccessToken}");
                requestMessage.Headers.Add("Accept", "application/json");

                HttpResponseMessage sendResponse = await httpClient.SendAsync(requestMessage);
                sendResponse.EnsureSuccessStatusCode();

                JsonDocument responseJson = JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync());

                if (!responseJson.RootElement.GetProperty("MediaContainer").TryGetProperty("Metadata", out _))
                    return null;

                SessionData[]? sessions = JsonSerializer.Deserialize<SessionData[]>(responseJson.RootElement.GetProperty("MediaContainer").GetProperty("Metadata"));

                return sessions?.FirstOrDefault(session => session.Type == "track" && session.User?.Name == App.Account?.Username);
            }
            catch (Exception e) {
                Console.WriteLine($"WARN: Unable to get current session: {SelectedAddress}status/sessions?X-Plex-Token={SelectedResource?.AccessToken?[..3]}...\n{e.Message} {e.InnerException}");
                return null;
            }
        }

        private async Task<PresenceData> BuildPresence(SessionData session) {
            string L1 = Config.Settings.TemplateL1.ApplyPlaceholders(session);
            string L2 = Config.Settings.TemplateL2.ApplyPlaceholders(session);
            string L3 = Config.Settings.TemplateL3.ApplyPlaceholders(session);

            return new PresenceData() {
                Line1 = L1,
                Line2 = L2,
                Line3 = L3,
                ArtLink = Config.Settings.LocalPlayer ? "https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexIcon.png" : await GetThumbnail(session.ArtPath, session.Album),
                State = session.Player?.State,
                TimeOffset = session.ProgressOffset,
                Duration = session.Duration,
                Url = session?.Guid?.StartsWith("plex://") == true ? $"https://listen.plex.tv/{session.Guid?[7..]}" : null
            };
        }

        private async Task SetPresence(PresenceData presence) {
            if (presence.State == "playing") {
                App.DiscordClient.SetPresence(new() {
                    Details = presence.Line1,
                    State = presence.Line2,
                    Timestamps = new(DateTime.UtcNow.AddMilliseconds(-(double)presence.TimeOffset), DateTime.UtcNow.AddMilliseconds((double)presence.Duration - (double)presence.TimeOffset)),
                    Type = ActivityType.Listening,
                    StatusDisplay = Enum.Parse<StatusDisplayType>(Config.Settings.StatusDisplayType),
                    Assets = new() {
                        LargeImageKey = presence.ArtLink,
                        LargeImageText = presence.Line3
                    }
                });

                previewTime.IsVisible = true;
                previewPaused.IsVisible = false;
            }
            else {
                App.DiscordClient.SetPresence(new RichPresence() {
                    Details = presence.Line1,
                    State = presence.Line2,
                    Timestamps = new(DateTime.UtcNow, DateTime.UtcNow),
                    Type = ActivityType.Listening,
                    StatusDisplay = Enum.Parse<StatusDisplayType>(Config.Settings.StatusDisplayType),
                    Assets = new() {
                        LargeImageKey = presence.ArtLink,
                        LargeImageText = presence.Line3,
                        SmallImageKey = "https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexPaused.png",
                        SmallImageText = "Paused",
                    }
                });

                previewTime.IsVisible = false;
                previewPaused.IsVisible = true;
            }

            previewArt.Source = await LoadBitmap(presence.ArtLink);
            previewL1.Text = presence.Line1;
            previewL2.Text = presence.Line2;
            previewL3.Text = presence.Line3;

            previewListeningTo.Text = $"Listening to {Config.Settings.DiscordListeningTo}";
            previewStatusListeningTo.Text = Config.Settings.StatusDisplayType switch {
                "State" => previewL2.Text,
                "Details" => previewL1.Text,
                _ => Config.Settings.DiscordListeningTo,
            };
            discordStatus.IsVisible = true;

            TimeSpan timeStart = TimeSpan.FromMilliseconds(presence.TimeOffset);
            previewTimeStart.Text = $"{string.Format("{0:D2}:{1:D2}", timeStart.Minutes, timeStart.Seconds)}";

            TimeSpan timeEnd = TimeSpan.FromMilliseconds(presence.Duration);
            previewTimeEnd.Text = $"{string.Format("{0:D2}:{1:D2}", timeEnd.Minutes, timeEnd.Seconds)}";

            previewTimeProgress.Value = presence.Duration > 0 ? 100d * presence.TimeOffset / presence.Duration : 0;
        }

        private void ResetPresence() {
            previewArt.Source = LoadAssetBitmap("Resources/PlexIcon.png");

            previewL1.Text = Config.Settings.TemplateL1.ApplyPlaceholders();
            previewL2.Text = Config.Settings.TemplateL2.ApplyPlaceholders();
            previewL3.Text = Config.Settings.TemplateL3.ApplyPlaceholders();

            previewListeningTo.Text = $"Listening to {Config.Settings.DiscordListeningTo}";
            previewStatusListeningTo.Text = Config.Settings.DiscordListeningTo;
            discordStatus.IsVisible = false;

            previewTime.IsVisible = false;
            previewPaused.IsVisible = false;

            App.DiscordClient.ClearPresence();
        }

        private async Task<string> GetThumbnail(string? path, string? album) {
            string cacheFile = Path.Combine(Path.GetDirectoryName(Config.FilePath)!, "cache.json");

            Dictionary<string, ThumbnailData> thumbnails;
            string thumbnailsJson = "";

            if (File.Exists(cacheFile)) {
                thumbnailsJson = File.ReadAllText(cacheFile);
                try { thumbnails = JsonSerializer.Deserialize<Dictionary<string, ThumbnailData>>(File.ReadAllText(cacheFile))!; }
                catch { thumbnails = []; }

            }
            else
                thumbnails = [];

            string thumbnailLink;
            if (path is not null && thumbnails.TryGetValue(path, out ThumbnailData? value)) {
                thumbnailLink = value.Art;
            }
            else {
                try { thumbnailLink = await UploadImage(path!); }
                catch (Exception e) {
                    Console.WriteLine($"WARN: Unable to upload thumbnail for current session, using Plex Icon as thumbnail instead\n{e.Message} {e.InnerException}");
                    return "https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexIcon.png";
                }
                thumbnails.Add(path!, new() { Name = album ?? "Unknown", Art = thumbnailLink });
            }

            string newThumbnailsJson = JsonSerializer.Serialize(thumbnails, serializerOptions);
            if (newThumbnailsJson != thumbnailsJson)
                File.WriteAllText(cacheFile, newThumbnailsJson);

            return thumbnailLink;
        }

        public async Task<PlexResourceData[]?> GetAccountResources() {
            try {
                HttpRequestMessage requestMessage = new(HttpMethod.Get, $"https://plex.tv/api/v2/resources?includeHttps=1&includeIPv6=1&X-Plex-Token={App.Token}&X-Plex-Client-Identifier=PlexampRPC");
                requestMessage.Headers.Add("Accept", "application/json");

                HttpResponseMessage sendResponse = await httpClient.SendAsync(requestMessage);
                sendResponse.EnsureSuccessStatusCode();

                JsonDocument responseJson = JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync());

                PlexResourceData[]? serverResources = JsonSerializer.Deserialize<PlexResourceData[]>(responseJson.RootElement)?.Where(r => r.Provides?.Split(",").Contains("server") == true).ToArray();

                if (serverResources is null || serverResources.Length == 0) {
                    Console.WriteLine("WARN: No servers found");
                    return null;
                }

                List<PlexResourceData>? finalResources = [];
                for (int i = 0; i < serverResources.Length; i++) {
                    PlexResourceData resource = serverResources[i];

                    if (Config.Settings.OwnedOnly && !resource.Owned)
                        continue;
                    if (serverResources.Length > 1)
                        statusTextBox.Text = $"Loading Servers...\n[{i}/{serverResources.Length}]";
                    else
                        statusTextBox.Text = "Loading Servers...";
                    await TestResource(resource);
                    if (resource is not null)
                        finalResources.Add(resource);
                }
                if (serverResources.Length > 1)
                    statusTextBox.Text = $"Loading Servers...\n[{serverResources.Length}/{serverResources.Length}]";
                await Task.Delay(200);

                return [.. finalResources];
            }
            catch (Exception e) {
                Console.WriteLine($"WARN: Unable to get resource: {e.Message} {e.InnerException}");
                return null;
            }
        }

        private static async Task TestResource(PlexResourceData resource) {
            foreach (PlexConnectionData connection in resource.Connections!) {
                Uri uri;
                if (connection.Local)
                    uri = new UriBuilder("http", connection.Address, connection.Port).Uri;
                else if (!Config.Settings.LocalAddress)
                    uri = new UriBuilder(connection.Uri!).Uri;
                else
                    continue;

                try {
                    Console.WriteLine($"INFO: Testing {(connection.Local ? "Local" : "Remote")} {uri}status/sessions?X-Plex-Token={resource.AccessToken?[..3]}...");
                    HttpRequestMessage requestMessage = new(HttpMethod.Get, $"{uri}status/sessions?X-Plex-Token={resource.AccessToken}");
                    requestMessage.Headers.Add("Accept", "application/json");

                    HttpResponseMessage sendResponse = await httpClient.SendAsync(requestMessage);
                    sendResponse.EnsureSuccessStatusCode();
                    Console.WriteLine($"INFO: Success {(connection.Local ? "Local" : "Remote")} {uri}status/sessions?X-Plex-Token={resource.AccessToken?[..3]}...");
                    if (connection.Local)
                        resource.LocalUri ??= uri;
                    else
                        resource.Uri ??= uri;
                    if (resource.LocalUri is not null && resource.Uri is not null) {
                        break;
                    }
                }
                catch (TaskCanceledException) {
                    Console.WriteLine($"WARN: Timeout {(connection.Local ? "Local" : "Remote")} {uri}status/sessions?X-Plex-Token={resource.AccessToken?[..3]}...");
                }
                catch (HttpRequestException e) {
                    Console.WriteLine($"WARN: Unable to access {uri}status/sessions?X-Plex-Token={resource.AccessToken?[..3]}: {e.Message}");
                }
                catch (Exception e) {
                    Console.WriteLine($"WARN: Unable to get resource: {e.Message} {e.InnerException}");
                }
            }
        }

        private async Task<string> UploadImage(string thumb) {
            HttpResponseMessage getResponse = await httpClient.GetAsync($"{SelectedAddress}photo/:/transcode?width={Config.Settings.ArtResolution}&height={Config.Settings.ArtResolution}&minSize=1&upscale=1&format=png&url={thumb}&X-Plex-Token={SelectedResource?.AccessToken}");

            string dataString = Uri.EscapeDataString(Convert.ToBase64String(await getResponse.Content.ReadAsByteArrayAsync()));
            HttpResponseMessage sendResponse = await httpClient.SendAsync(new() {
                Method = HttpMethod.Post,
                RequestUri = new("https://freeimage.host/api/1/upload"),
                Content = new StringContent($"image={dataString}&key=6d207e02198a847aa98d0a2a901485a5", Encoding.UTF8, "application/x-www-form-urlencoded")
            });
            sendResponse.EnsureSuccessStatusCode();
            return JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("image").GetProperty("url").GetString()!;
        }

        private void Template_LostFocus(object sender, RoutedEventArgs e) => Config.Save();

        private async void SettingsButton_Click(object sender, RoutedEventArgs e) {
            SettingsWindow settingsWindow = new() { WindowStartupLocation = WindowStartupLocation.CenterOwner };
            await settingsWindow.ShowDialog(this);
            Config.Save();
        }

        private void LogsButton_Click(object sender, RoutedEventArgs e) => new LogWindow(App.Log!).Show();

        protected override void OnClosing(WindowClosingEventArgs e) {
            base.OnClosing(e);
            if (!forceClose && Config.Settings.CloseToTray) {
                Hide();
                Console.WriteLine("INFO: Minimized to Tray");
                e.Cancel = true;
            }
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            App.DiscordClient.Dispose();

            Config.Settings.SelectedServer = SelectedResource?.Name ?? string.Empty;

            Config.Save();
        }

        protected override async void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            if (e.Key == Key.F12)
                OpenConfigFolder();

            if (e.Key == Key.F5)
                new LogWindow(App.Log!).Show();

            if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control && App.Log is not null)
                await App.Log.SaveAs(this);
        }

        public void CloseForShutdown() {
            forceClose = true;
            Close();
        }

        private static void OpenConfigFolder() {
            string folder = Path.GetDirectoryName(Config.FilePath)!;
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }

        private static async Task<Bitmap?> LoadBitmap(string source) {
            try {
                if (Uri.TryCreate(source, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) {
                    await using Stream remoteStream = await httpClient.GetStreamAsync(uri);
                    MemoryStream memoryStream = new();
                    await remoteStream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                    return new Bitmap(memoryStream);
                }

                if (Uri.TryCreate(source, UriKind.Absolute, out uri) && uri.Scheme == "avares")
                    return new Bitmap(AssetLoader.Open(uri));

                if (source.StartsWith('/'))
                    return LoadAssetBitmap(source.TrimStart('/'));

                if (File.Exists(source))
                    return new Bitmap(source);
            }
            catch (Exception e) {
                Console.WriteLine($"WARN: Unable to load image: {source}\n{e.Message}");
            }

            return LoadAssetBitmap("Resources/PlexIcon.png");
        }

        private static Bitmap LoadAssetBitmap(string path) {
            return new Bitmap(AssetLoader.Open(new Uri($"avares://PlexampRPC/{path}")));
        }
    }
}
