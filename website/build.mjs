// Builds the static site: node website/build.mjs  ->  website/dist
// No dependencies. Pages are HTML fragments in src/pages with a small header
// comment; <code-block lang="..."> elements are escaped and highlighted here,
// so the published pages need no client-side highlighter.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.dirname(fileURLToPath(import.meta.url));
const src = path.join(root, 'src');
const dist = path.join(root, 'dist');

const VERSION = '4.9.1';
const REPO = 'https://github.com/ModernLibs/FileHelpers';
const NUGET = 'https://www.nuget.org/packages/ModernLibs.FileHelpers';

const NAV = [
  ['quick-start', 'Quick start'],
  ['examples', 'Examples'],
  ['must-read', 'Must read'],
  ['migration', 'Migration'],
  ['benchmarks', 'Benchmarks'],
];

const GUIDE = [
  ['index', 'Introduction'],
  ['install', 'Install'],
  ['quick-start', 'Quick start'],
  ['must-read', 'Must read'],
  ['examples', 'Examples'],
  ['diagnostics', 'Diagnostics'],
  ['migration', 'Migration from 3.x'],
  ['benchmarks', 'Benchmarks'],
  ['community', 'Credits & contributing'],
];

const esc = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

// ---------- highlighting ----------

const CS_KEYWORDS = new Set(`abstract as async await base bool break byte case catch char class const continue
decimal default delegate do double else enum event explicit false finally float for foreach get if in init int
interface internal is long namespace new null object out override params private protected public readonly record
ref required return sealed set static string struct switch this throw true try typeof using var void while yield
when where is not and or`.split(/\s+/));

function highlightCSharp(code) {
  const re = /(\/\/[^\n]*)|(\$?@?"(?:[^"\\\n]|\\.)*")|('(?:[^'\\\n]|\\.)')|(\b\d[\d_]*(?:\.\d+)?[mMfFdDlL]?\b)|(\b[A-Za-z_]\w*\b)|([\s\S])/g;
  let out = '';
  let prev = '';
  for (const m of code.matchAll(re)) {
    const [tok, comment, str, chr, num, ident] = m;
    if (comment) out += `<span class="t-c">${esc(tok)}</span>`;
    else if (str || chr) out += `<span class="t-s">${esc(tok)}</span>`;
    else if (num) out += `<span class="t-n">${esc(tok)}</span>`;
    else if (ident) {
      if (CS_KEYWORDS.has(tok)) out += `<span class="t-k">${tok}</span>`;
      else if (/^[A-Z]/.test(tok) && prev !== '.') out += `<span class="t-t">${tok}</span>`;
      else out += tok;
    } else out += esc(tok);
    if (!/^\s+$/.test(tok)) prev = tok;
  }
  return out;
}

function highlightDiff(code) {
  return code
    .split('\n')
    .map((l) => {
      const cls = l.startsWith('+') ? 'd-add' : l.startsWith('-') ? 'd-del' : '';
      return cls ? `<span class="${cls}">${esc(l)}</span>` : esc(l);
    })
    .join('\n');
}

function highlightShell(code) {
  return code
    .split('\n')
    .map((l) => (l.trimStart().startsWith('#') ? `<span class="t-c">${esc(l)}</span>` : esc(l)))
    .join('\n');
}

function highlightXml(code) {
  return esc(code)
    .replace(/(&lt;!--[\s\S]*?--&gt;)/g, '<span class="t-c">$1</span>')
    .replace(/(&lt;\/?)([\w.]+)/g, '$1<span class="t-k">$2</span>')
    .replace(/("[^"]*")/g, '<span class="t-s">$1</span>');
}

function dedent(code) {
  const lines = code.replace(/^\n+|\s+$/g, '').split('\n');
  const indent = Math.min(...lines.filter((l) => l.trim()).map((l) => l.match(/^ */)[0].length));
  return lines.map((l) => l.slice(indent)).join('\n');
}

const LANG_LABEL = { csharp: 'C#', diff: 'diff', shell: 'shell', xml: 'XML', text: 'text', data: 'data' };

function renderCodeBlocks(html) {
  return html.replace(/<code-block([^>]*)>([\s\S]*?)<\/code-block>/g, (_, attrs, raw) => {
    const lang = (attrs.match(/lang="([^"]+)"/) || [, 'text'])[1];
    const title = (attrs.match(/title="([^"]+)"/) || [])[1];
    const code = dedent(raw);
    const body =
      lang === 'csharp' ? highlightCSharp(code)
      : lang === 'diff' ? highlightDiff(code)
      : lang === 'shell' ? highlightShell(code)
      : lang === 'xml' ? highlightXml(code)
      : esc(code);
    const label = title || LANG_LABEL[lang] || lang;
    return `<figure class="code code--${lang}"><figcaption><span>${esc(label)}</span><button type="button" class="copy" data-copy hidden>Copy</button></figcaption><pre tabindex="0"><code>${body}</code></pre></figure>`;
  });
}

// ---------- layout ----------

function parsePage(file) {
  const text = fs.readFileSync(file, 'utf8');
  const head = text.match(/^<!--\s*([\s\S]*?)-->/);
  if (!head) throw new Error(`${file}: missing header comment`);
  const meta = Object.fromEntries(
    head[1].trim().split('\n').map((l) => {
      const i = l.indexOf(':');
      return [l.slice(0, i).trim(), l.slice(i + 1).trim()];
    }),
  );
  return { meta, body: text.slice(head[0].length) };
}

const href = (slug) => (slug === 'index' ? './' : `${slug}.html`);

function tocOf(body) {
  return [...body.matchAll(/<h2 id="([^"]+)"[^>]*>([\s\S]*?)<\/h2>/g)].map((m) => [m[1], m[2].replace(/<[^>]+>/g, '')]);
}

function header(slug) {
  const links = NAV.map(
    ([s, label]) => `<a href="${href(s)}"${s === slug ? ' aria-current="page"' : ''}>${label}</a>`,
  ).join('');
  return `<a class="skip" href="#main">Skip to content</a>
<header class="site-header">
  <a class="brand" href="./" aria-label="ModernLibs.FileHelpers, home">
    <img src="assets/logo.svg" alt="" width="28" height="28">
    <span class="brand__name"><span class="brand__org">ModernLibs.</span>FileHelpers</span>
  </a>
  <nav class="site-nav" id="site-nav" aria-label="Main">${links}</nav>
  <div class="site-tools">
    <a class="pill" href="${NUGET}" title="Latest release on NuGet">v${VERSION}</a>
    <a class="tool" href="${REPO}">GitHub</a>
    <button type="button" class="tool theme-toggle" data-theme-toggle hidden aria-label="Switch colour theme">Theme</button>
    <button type="button" class="tool menu-toggle" data-menu-toggle hidden aria-expanded="false" aria-controls="site-nav">Menu</button>
  </div>
</header>`;
}

function footer() {
  const guide = GUIDE.map(([s, label]) => `<li><a href="${href(s)}">${label}</a></li>`).join('');
  return `<footer class="site-footer">
  <div class="site-footer__grid">
    <div class="site-footer__about">
      <p class="label">Colophon</p>
      <p>ModernLibs.FileHelpers is an independent reimplementation modelled on the API of
      <a href="https://github.com/MarcosMeli/FileHelpers">FileHelpers 3.x</a> by Marcos Meli and contributors.
      It is not the original project: report problems with this library
      <a href="${REPO}/issues">in its own issue tracker</a>, not upstream.</p>
      <p>MIT licensed. No trackers, no cookies, no third-party requests.</p>
    </div>
    <nav aria-label="Guide"><p class="label">Guide</p><ul>${guide}</ul></nav>
    <nav aria-label="Project"><p class="label">Project</p><ul>
      <li><a href="${REPO}">Source code</a></li>
      <li><a href="${NUGET}">NuGet package</a></li>
      <li><a href="${REPO}/blob/main/CHANGELOG.md">Changelog</a></li>
      <li><a href="${REPO}/issues">Issues &amp; feedback</a></li>
      <li><a href="${REPO}/blob/main/LICENSE">License</a></li>
    </ul></nav>
  </div>
  <p class="wordmark" aria-hidden="true">FileHelpers</p>
</footer>`;
}

function layout({ slug, meta, body }) {
  const isHome = meta.layout === 'home';
  const title = isHome ? 'ModernLibs.FileHelpers: typed fixed-width and delimited files for .NET' : `${meta.title} · ModernLibs.FileHelpers`;
  let main;
  if (isHome) {
    main = `<main id="main" class="home">${body}</main>`;
  } else {
    const toc = tocOf(body);
    const idx = GUIDE.findIndex(([s]) => s === slug);
    const prev = idx > 0 ? GUIDE[idx - 1] : null;
    const next = idx >= 0 && idx < GUIDE.length - 1 ? GUIDE[idx + 1] : null;
    const rail = toc.length
      ? `<aside class="rail" aria-label="On this page"><p class="label">On this page</p><ol>${toc
          .map(([id, text]) => `<li><a href="#${id}">${text}</a></li>`)
          .join('')}</ol></aside>`
      : '';
    const pager = `<nav class="pager" aria-label="Guide pages">${
      prev ? `<a href="${href(prev[0])}" rel="prev"><span class="label">Previous</span>${prev[1]}</a>` : '<span></span>'
    }${next ? `<a href="${href(next[0])}" rel="next"><span class="label">Next</span>${next[1]}</a>` : '<span></span>'}</nav>`;
    main = `<main id="main" class="doc">
  <header class="doc__head">
    <p class="label">${String(idx + 1).padStart(2, '0')} / ${String(GUIDE.length).padStart(2, '0')} · ${meta.section || 'Guide'}</p>
    <h1>${meta.title}</h1>
    <p class="lede">${meta.lede || meta.description}</p>
  </header>
  <div class="doc__body">${rail}<article class="prose">${body}${pager}</article></div>
</main>`;
  }
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${title}</title>
<meta name="description" content="${esc(meta.description)}">
<meta name="color-scheme" content="light dark">
<meta property="og:title" content="${esc(title)}">
<meta property="og:description" content="${esc(meta.description)}">
<meta property="og:type" content="website">
<link rel="icon" href="assets/logo.svg" type="image/svg+xml">
<link rel="preload" href="assets/fonts/inter-latin-wght-normal.woff2" as="font" type="font/woff2" crossorigin>
<link rel="preload" href="assets/fonts/jetbrains-mono-latin-wght-normal.woff2" as="font" type="font/woff2" crossorigin>
<link rel="stylesheet" href="assets/site.css">
<script>try{var t=localStorage.getItem('theme');if(t)document.documentElement.dataset.theme=t}catch(e){}document.documentElement.classList.add('js')</script>
</head>
<body>
${header(slug)}
${main}
${footer()}
${isHome ? '<script src="assets/vendor/gsap.min.js" defer></script>\n<script src="assets/vendor/ScrollTrigger.min.js" defer></script>\n' : ''}<script src="assets/site.js" defer></script>
</body>
</html>
`;
}

// ---------- build ----------

fs.rmSync(dist, { recursive: true, force: true });
fs.mkdirSync(dist, { recursive: true });
fs.cpSync(path.join(src, 'assets'), path.join(dist, 'assets'), { recursive: true });
fs.copyFileSync(path.join(root, '..', 'assets', 'logo.svg'), path.join(dist, 'assets', 'logo.svg'));
fs.writeFileSync(path.join(dist, '.nojekyll'), '');

const pagesDir = path.join(src, 'pages');
const slugs = [];
for (const file of fs.readdirSync(pagesDir).filter((f) => f.endsWith('.html')).sort()) {
  const slug = file.replace(/\.html$/, '');
  const { meta, body } = parsePage(path.join(pagesDir, file));
  const html = layout({ slug, meta, body: renderCodeBlocks(body).replaceAll('{{VERSION}}', VERSION).replaceAll('{{REPO}}', REPO).replaceAll('{{NUGET}}', NUGET) });
  fs.writeFileSync(path.join(dist, file), html);
  slugs.push(slug);
}

// Fail the build on links to pages or anchors that do not exist.
let broken = 0;
for (const slug of slugs) {
  const html = fs.readFileSync(path.join(dist, `${slug}.html`), 'utf8');
  for (const [, target] of html.matchAll(/href="([^"]+)"/g)) {
    if (/^(https?:|mailto:|assets\/)/.test(target)) continue;
    const [file, anchor] = target.split('#');
    const page = file === './' || file === '' ? (file === '' ? slug : 'index') : file.replace(/\.html$/, '');
    if (!slugs.includes(page)) { console.error(`${slug}: broken link ${target}`); broken++; continue; }
    if (anchor && !fs.readFileSync(path.join(dist, `${page}.html`), 'utf8').includes(`id="${anchor}"`)) {
      console.error(`${slug}: missing anchor ${target}`); broken++;
    }
  }
}
if (broken) process.exit(1);
console.log(`Built ${slugs.length} pages -> ${path.relative(process.cwd(), dist)}`);
