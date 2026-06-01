# LiveGardenTVPlus – IPTV Player for Windows

[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-blue)](https://github.com/dotnet/wpf)
[![WebView2](https://img.shields.io/badge/WebView2-hls.js-green)](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

**LiveGardenTVPlus** is a desktop IPTV player for Windows (WPF / .NET 10) that plays HLS streams (m3u8) using **WebView2** and **hls.js**.  
It loads local or online M3U playlists, organizes channels by groups, and provides a modern, themeable interface.

<!-- ![Screenshot placeholder](screenshot.png) -->

## Screenshots

<table align="center">
  <tr>
    <td align="center">
      <img src="Screenshots/preview1.jpg?sanitize=true&raw=true" title="preview1" width="400"/><br/>
      <b>Preview 1</b>
    </td>
    <td align="center">
      <img src="Screenshots/preview2.jpg?sanitize=true&raw=true" title="preview2" width="400"/><br/>
      <b>Preview 2</b>
    </td>
  </tr>

  <tr>
    <td align="center">
      <img src="Screenshots/preview3.jpg?sanitize=true&raw=true" title="preview3" width="400"/><br/>
      <b>Preview 3</b>
    </td>
    <td align="center">
      <img src="Screenshots/preview4.jpg?sanitize=true&raw=true" title="preview4" width="400"/><br/>
      <b>Preview 4</b>
    </td>
  </tr>

  <tr>
    <td align="center">
      <img src="Screenshots/preview5.jpg?sanitize=true&raw=true" title="preview5" width="400"/><br/>
      <b>Preview 5</b>
    </td>
    <td align="center">
      <img src="Screenshots/preview6.jpg?sanitize=true&raw=true" title="preview6" width="400"/><br/>
      <b>Preview 6</b>
    </td>
  </tr>
</table>

---
### Changelog

## ✨ Features (Version 1.0)

- **Playlist support** – Load M3U/M3U8 files from your PC or from a remote URL.
- **GitHub playlist browser** – Automatically fetches all `.m3u` files from the [TivuStreamList](https://github.com/OwnerPlugins/TivuStreamList/tree/list/ios) repository (root + `local/` subfolder). Includes a fallback static list.
- **Channel grouping** – TreeView with group drill‑down, search, favorites.
- **Themes** – 16 predefined color themes + Light/Dark mode, changeable at runtime.
- **Player controls** – Play/Pause, speed (0.5×, 1×, 2×), buffer slider (1–10 seconds), Picture‑in‑Picture (PIP).
- **Fullscreen mode** – Hides all UI; press ESC to exit.
- **Sidebar toggle** – Collapse the channel list to focus on video.
- **Settings window** – Change buffer size, select online playlist (GitHub refresh), and choose UI language (see note below).
- **Persistent preferences** – Saves last playlist URL, buffer, theme, and language (language not yet fully applied).

---

## ⚠️ Known limitation – Language translation

The application includes over 90 language files (`.lng`) and a language selector in `SettingsWindow`, but **the UI does not actually translate** at this moment. This feature is under active development and will be fixed in a future release.

---

## 🚀 Getting started

### Prerequisites

- Windows 10 / 11 (x64 or x86)
- [.NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (or SDK for development)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (usually pre‑installed on Windows 11)

### Download & run

1. Go to the [Releases](https://github.com/YOUR_USERNAME/LiveGardenTVPlus/releases) page (or clone the repository).
2. Download `LiveGardenTVPlus.exe` (standalone) or the installer.
3. Run the application – no additional configuration required.

### Build from source

```bash
git clone https://github.com/YOUR_USERNAME/LiveGardenTVPlus.git
cd LiveGardenTVPlus
dotnet build -c Release
```

The executable will be in `bin/Release/net10.0-windows/`.

---

## 📂 Project structure (main)

```
LiveGardenTVPlus/
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── Views/
│   ├── SettingsWindow.xaml / .cs
│   └── ColorPickerWindow.xaml / .cs
├── Services/
│   ├── M3uParser.cs
│   ├── FavoritesManager.cs
│   ├── UserPreferences.cs
│   ├── ThemeManager.cs
│   ├── LanguageManager.cs (translation not yet functional)
│   ├── TranslationHelper.cs (currently ineffective)
│   └── GitHubPlaylistFetcher.cs
├── Models/
│   ├── Channel.cs
│   └── ChannelGroup.cs
├── Languages/              (92+ .lng files)
├── Themes/                 (16 .xaml theme files)
└── PlayerHost/player.html  (hls.js wrapper)
```

---

## 🛠️ Usage

1. **Load a playlist**  
   - Click `Load M3U` (local file) or `Online M3U` (enter raw URL).  
   - Or go to `Settings` → `Refresh from GitHub` → select a playlist → press `LOAD` or `SAVE`.

2. **Play a channel**  
   - Click any channel in the tree view. The video starts automatically.

3. **Manage groups**  
   - Click a group name to see only its channels.  
   - Click `← Back to all groups` to return.

4. **Favorites**  
   - Right‑click a channel (or use the star icon) to add/remove favorites.  
   - Toggle the `⭐ Favorites only` checkbox.

5. **Search**  
   - Type in the search box to filter channels (flat result list).

6. **Theme & UI**  
   - Use the palette icon to choose a color theme.  
   - `Hide List` collapses the sidebar.  
   - `Fullscreen` hides all UI (press ESC to exit).  
   - Speed buttons change playback speed.  
   - Drag & drop a `.m3u` file onto the window.

---

## 🙏 Credits

- **Development**: Lululla (original author) & community contributions.
- **Playlist repository**: [OwnerPlugins/TivuStreamList](https://github.com/OwnerPlugins/TivuStreamList) – massive collection of Italian and international M3U streams.
- **HLS playback**: [hls.js](https://github.com/video-dev/hls.js) (MIT license)
- **UI components**: [MaterialDesignThemes.Wpf](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit)
- **WebView2**: Microsoft Edge WebView2 (Microsoft)
- **Inspiration and testing**: Corvo Boys community ([corvoboys.org](https://www.corvoboys.org))

---

## 📄 License

This project is released under the **MIT License** – see [LICENSE](LICENSE) file for details.

---

## 🤝 Contributing

Bug reports and pull requests are welcome. Please open an issue first to discuss major changes.  
For language translation fixes (the current limitation), any help is highly appreciated!

---

## 📬 Contact

For questions or suggestions, visit the [GitHub repository](https://github.com/OwnerPlugins/LiveGardenTVPlus) or the official website [corvoboys.org](https://www.corvoboys.org).

---

*Happy streaming!* 🎥
