using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DyviniaUtils {

    class Downloader {
        public static async Task Download(string downloadUrl, string destinationFilePath, IProgress<double> progress) {
            using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(30) };
            using HttpResponseMessage response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();
            long totalBytes = response.Content.Headers.ContentLength ?? 0L;

            await using Stream contentStream = await response.Content.ReadAsStreamAsync();
            long totalBytesRead = 0L;
            long readCount = 0L;
            byte[] buffer = new byte[4096];
            bool isMoreToRead = true;

            await using FileStream fileStream = new(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

            do {
                int bytesRead = await contentStream.ReadAsync(buffer);
                if (bytesRead == 0) {
                    isMoreToRead = false;
                    progress.Report(totalBytes > 0 ? (double)totalBytesRead / totalBytes : 1);
                    continue;
                }

                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));

                totalBytesRead += bytesRead;
                readCount++;

                if (readCount % 100 == 0) {
                    progress.Report(totalBytes > 0 ? (double)totalBytesRead / totalBytes : 0);
                }
            }
            while (isMoreToRead);
        }

        public static async Task DownloadWithWindow(string downloadUrl, string destinationFilePath) {
            DownloadWindow downloadWindow = new();
            downloadWindow.Show();
            await Download(downloadUrl, destinationFilePath, downloadWindow.Progress);
            await Task.Delay(100);
            downloadWindow.Close();
        }

        private class DownloadWindow : Window {
            public IProgress<double> Progress { get; }

            public DownloadWindow() {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                CanResize = false;
                WindowDecorations = WindowDecorations.None;
                Title = "Downloading";
                Height = 100;
                Width = 400;
                Background = Brushes.Transparent;

                Grid innerGrid = new() {
                    Background = SolidColorBrush.Parse("#FF141414"),
                    Margin = new Avalonia.Thickness(5)
                };

                TextBlock labelText = new() {
                    Text = Title,
                    FontWeight = FontWeight.Bold,
                    FontSize = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                ProgressBar progressBar = new() {
                    Height = 5,
                    Maximum = 1,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = Brushes.Transparent,
                    Foreground = Brushes.White,
                };
                Progress = new Progress<double>(p => progressBar.Value = p);

                innerGrid.Children.Add(labelText);
                innerGrid.Children.Add(progressBar);

                Grid rootGrid = new() {
                    Background = SolidColorBrush.Parse("#FF2D2D2D")
                };
                rootGrid.Children.Add(innerGrid);

                Content = rootGrid;
            }
        }
    }
}
