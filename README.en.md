# Translator Shortcut

[한국어](README.md) | **English**

A Windows tray app that toggles **Chrome's built-in page translation** with a single hotkey (default `Alt+T`). It drives Chrome's own translator, so the result is the same as translating from Chrome's menu.

> The app's interface is in Korean, and the default settings target Korean Chrome. For other Chrome languages, run [Calibrate] once (see below).

## Download and usage

1. Download `TranslatorShortcut.exe` from [Releases](https://github.com/jeremywrightkim/chrome_translator_shortcut/releases/latest) and run it. No installation is needed.
   - If SmartScreen warns you, click [More info] → [Run anyway]. (The exe is not code-signed.)
2. In Chrome, press `Alt+T` to translate the page. Press it again to return to the original.

- The target language follows your Chrome settings (`chrome://settings/languages`).
- The hotkey is active only while a Chrome window is in front, so other apps can still use the same key.
- Double-click the tray icon, or run the exe again, to open Settings (환경설정). You can change the hotkey and enable start with Windows there.

Requirements: Windows 10 or 11

### Choosing a hotkey

If you pick a key that Chrome or a web page uses, that key's original function stops working.

- **Recommended:** the default `Alt+T` (easy to press with the left hand, no conflict with Chrome)
- **Single keys:** `F4`, `F9`, `F8`, `F2` (unused by Chrome; note that `F2`/`F4` are used by Google Sheets and `F8`/`F9` by DevTools)
- **Avoid:** `Ctrl`+letter, right `Alt` (the Korean/English toggle on Korean keyboards), `F1`, `F3`, `F5`, `F6`, `F7`, `F10`, `F11`, `F12`

## If the hotkey doesn't work

The defaults target **Korean Chrome 154**. If your Chrome version or language differs, try these in Settings:

1. **Compatibility check (호환성 검사):** checks translation policies, the Google Translate setting, and Chrome's UI elements without touching Chrome.
2. **Calibrate (보정…):** open a foreign-language page and click the translate icon and translate-bubble buttons as guided. The app re-learns Chrome's UI for your setup.

If it still fails, open the tray menu → Open log folder (로그 폴더 열기) and attach `log.txt` to an [issue](https://github.com/jeremywrightkim/chrome_translator_shortcut/issues).

## Build

Builds with the C# compiler that ships with Windows; no SDK needed. Output: `bin\TranslatorShortcut.exe`.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

## Support

If you find this useful, you can support the project on the [support page](https://cgcg.review/support/), also available from the tray menu (후원하기…).
