# Contributing to ReInstala

Thanks for taking the time to contribute! This project is intentionally small and dependency-free,
so a few conventions keep it that way.

## Before you start

- Open an issue to discuss larger changes before writing code.
- For bugs, use the bug report template and include your Windows version, Winget version
  (`winget --version`) and the steps to reproduce.

## Building

Requirements: Windows 10/11 with .NET Framework 4.x (included in Windows). No Visual Studio, SDK or
package manager is needed.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1
```

- `dist\ReInstala.exe` is the portable app.
- `dist\ReInstala-Setup.exe` is the per-user installer (it embeds the app and the logo).
- Build output (`dist/`, `obj/`) is ignored by Git.

Run the diagnostic report before and after your change:

```powershell
.\dist\ReInstala.exe --selftest
```

## Code guidelines

- Target **.NET Framework 4.8** and **C# 5** (`/langversion:5`). Avoid newer syntax such as string
  interpolation, expression-bodied members or `nameof`.
- Do not add NuGet packages, external executables or an SDK-based project. The app must keep
  compiling with the built-in `csc.exe`.
- Keep the UI in Brazilian Portuguese, with clear and short labels.
- Follow the existing style: 4-space indentation, `private` fields prefixed with `_`, explicit
  types instead of `var` in most places, and comments only where the reason is not obvious.
- Preserve accessibility: keyboard focus, readable contrast in both themes, and no text clipping
  at the minimum window size.
- The app must never require administrator rights to open. Elevation is only requested when
  installing a batch.

## Testing checklist

- `.\dist\ReInstala.exe --selftest` returns `RESULT=OK`.
- Light and dark themes render correctly (switch the theme and reopen the app).
- The list scrolls with the mouse wheel from any control, and the scroll bar stays in sync.
- A backup is created in Downloads and can be restored in the same session.
- At the minimum window size, the Settings tab shows every option without clipping.
- No new entries appear in `%LOCALAPPDATA%\ReInstala\error.log`.

## Commits and pull requests

- Write short, imperative commit messages (for example `Fix horizontal scroll bar in backup list`).
- One topic per pull request; describe what changed and how you tested it.
- Update `CHANGELOG.md` for user-visible changes.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
