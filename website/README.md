# Website

The project site: introduction, install, quick start, must read, examples, diagnostics, migration,
benchmarks and credits. It covers the same ground as the original FileHelpers site
(filehelpers.net), rewritten for ModernLibs.FileHelpers 4.x.

## Build

Node 20 or later, no packages to install:

```bash
node website/build.mjs                         # writes website/dist
python3 -m http.server 4173 -d website/dist    # preview at http://localhost:4173
```

Preview through a local server rather than by opening the files: browsers block the fonts on
`file://` pages.

The build fails on a link to a page or anchor that does not exist.

## Layout

| Path | Contents |
|---|---|
| `build.mjs` | The build: page shell, navigation, code highlighting, link check |
| `src/pages/*.html` | One HTML fragment per page, with a header comment for title and description |
| `src/assets/site.css`, `site.js` | Styles and progressive enhancement (theme, copy buttons, home page animation) |
| `src/assets/fonts/` | Inter, JetBrains Mono, Newsreader (SIL Open Font License), latin subsets |
| `src/assets/vendor/` | GSAP 3.13 and ScrollTrigger (GSAP standard license), used on the home page only |

Code samples are written as `<code-block lang="csharp">` elements with the raw code inside; the build
escapes and highlights them, so there is no client-side highlighter.

## Editing

- The version shown on the site is `VERSION` in `build.mjs`.
- The benchmark figures on the home and benchmarks pages are copied from the README and
  `docs/benchmarks/`. Update them together.
- Every page works without JavaScript and with reduced motion; keep the markup as the finished state
  and let `site.js` animate towards it.

## Publishing

`.github/workflows/website.yml` builds the site and deploys it to GitHub Pages. It runs only when
started by hand until Pages is enabled for the repository (Settings, Pages, Source: GitHub Actions).
