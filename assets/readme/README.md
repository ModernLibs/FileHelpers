# README images

`hero.png` and `benchmarks.png` are the images at the top of the repository README (and of the
NuGet package page, which loads them from `raw.githubusercontent.com`). Each is rendered from the
HTML file of the same name at twice its CSS size:

```powershell
msedge --headless=new --hide-scrollbars --force-device-scale-factor=2 `
  --window-size=1000,490 --virtual-time-budget=8000 --screenshot=hero.png file:///.../hero.html
msedge --headless=new --hide-scrollbars --force-device-scale-factor=2 `
  --window-size=1000,740 --virtual-time-budget=8000 --screenshot=benchmarks.png file:///.../benchmarks.html
```

Render from a folder that also holds `logo.svg` one level up (`hero.html` loads `../logo.svg`). The
fonts (Archivo, JetBrains Mono) load from Google Fonts. The numbers in `benchmarks.html` come from
[docs/benchmarks/v4.9.0.md](../../docs/benchmarks/v4.9.0.md); update both when the results change.
Colours: slate `#466C89` and copper `#B8734F` from the logo, ground `#EEF2F5`, ink `#1D2B36`.
