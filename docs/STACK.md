# Stack

- Language: C# with nullable reference types and implicit usings.
- Runtime: .NET 10.
- UI: Avalonia UI desktop.
- Main packages: Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, DiscordRichPresence, Plex.Api, Microsoft.Extensions.Logging.Console.
- Persistence: JSON settings under the user's application data folder.
- External services: Plex OAuth/resources/session APIs, Discord Rich Presence, GitHub releases API, freeimage.host upload API.
- Assets: images, icons, and fonts under `Resources/` are embedded as Avalonia resources.
- Target OS: Avalonia UI is cross-platform; startup integration still creates a Windows Startup `.cmd` launcher when enabled.

## Considerations

- Tray icon uses Avalonia `TrayIcon`; balloon notifications from the previous WPF tray package are not available.
- WPF-only XAML, `System.Windows.*`, `Hardcodet.NotifyIcon.Wpf`, `SharpVectors.Wpf`, `WebBrowser`, and COM shortcut creation are not part of the Avalonia stack.
- Unit test projects are not present in this repository.
