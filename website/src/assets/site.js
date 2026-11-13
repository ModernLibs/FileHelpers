// Progressive enhancement only: every page reads fully without this file.
(() => {
  const root = document.documentElement;
  const $$ = (sel, el = document) => [...el.querySelectorAll(sel)];
  const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;

  // Theme: follows the system until the visitor picks one.
  const themeBtn = document.querySelector('[data-theme-toggle]');
  if (themeBtn) {
    const current = () => root.dataset.theme || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    const paint = () => { themeBtn.textContent = current() === 'dark' ? 'Light' : 'Dark'; };
    themeBtn.hidden = false;
    paint();
    themeBtn.addEventListener('click', () => {
      const next = current() === 'dark' ? 'light' : 'dark';
      root.dataset.theme = next;
      try { localStorage.setItem('theme', next); } catch { /* private mode */ }
      paint();
    });
  }

  // Mobile menu.
  const menuBtn = document.querySelector('[data-menu-toggle]');
  const nav = document.getElementById('site-nav');
  if (menuBtn && nav) {
    menuBtn.hidden = false;
    const set = (open) => { nav.classList.toggle('is-open', open); menuBtn.setAttribute('aria-expanded', String(open)); };
    menuBtn.addEventListener('click', () => set(!nav.classList.contains('is-open')));
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape') set(false); });
  }

  // Copy buttons.
  if (navigator.clipboard) {
    for (const btn of $$('[data-copy]')) {
      btn.hidden = false;
      btn.addEventListener('click', async () => {
        const sel = btn.dataset.copy;
        const source = sel ? document.querySelector(sel) : btn.closest('figure').querySelector('pre');
        try {
          await navigator.clipboard.writeText(source.textContent.trim());
          btn.textContent = 'Copied';
        } catch { btn.textContent = 'Failed'; }
        setTimeout(() => { btn.textContent = 'Copy'; }, 1600);
      });
    }
  }

  // Guide pages: mark the section being read in the rail.
  const railLinks = $$('.rail a');
  if (railLinks.length && 'IntersectionObserver' in window) {
    const byId = new Map(railLinks.map((a) => [a.getAttribute('href').slice(1), a]));
    const io = new IntersectionObserver((entries) => {
      for (const e of entries) {
        if (!e.isIntersecting) continue;
        railLinks.forEach((a) => a.classList.remove('is-active'));
        byId.get(e.target.id)?.classList.add('is-active');
      }
    }, { rootMargin: '-15% 0px -70% 0px' });
    $$('.prose h2[id]').forEach((h) => io.observe(h));
  }

  if (reduced || !('IntersectionObserver' in window)) return;
  root.classList.add('motion');

  // One-shot entrances. Content is visible by default; the hidden state is
  // only applied here, to elements that are still below the fold.
  const targets = $$('.band__head, .band__body > *, .grid li, .band__more, .chart');
  const io = new IntersectionObserver((entries) => {
    for (const e of entries) {
      if (!e.isIntersecting) continue;
      e.target.classList.add('is-in');
      io.unobserve(e.target);
    }
  }, { rootMargin: '0px 0px -8% 0px' });
  for (const el of targets) {
    if (el.getBoundingClientRect().top > innerHeight * 0.92) {
      if (!el.classList.contains('chart')) el.classList.add('reveal');
      const i = el.parentElement.classList.contains('grid') ? [...el.parentElement.children].indexOf(el) % 3 : 0;
      el.style.transitionDelay = `${i * 70}ms`;
    } else {
      el.classList.add('is-in');
    }
    io.observe(el);
  }

  // The record: a scroll-scrubbed parse. The markup is the finished, parsed
  // state; the timeline plays towards it from the raw line.
  const parse = document.querySelector('[data-parse]');
  const tall = matchMedia('(min-height: 620px)').matches;
  if (!parse || !window.gsap || !window.ScrollTrigger || !tall) { root.classList.add('no-scrub'); parse && (parse.style.height = 'auto'); parse && (parse.querySelector('.parse__stage').style.cssText = 'position:static;height:auto'); return; }

  gsap.registerPlugin(ScrollTrigger);
  const line = parse.querySelector('.record__line');
  const stepLabel = parse.querySelector('[data-step-label]');
  const steps = ['Raw line', 'Id · 6 chars', 'Name · 20 chars', 'Salary · 10 chars', 'Parsed'];
  const setStep = (i) => { stepLabel.textContent = steps[i]; };
  const hl = (name) => () => $$('.hl', parse).forEach((el) => el.classList.toggle('is-on', el.dataset.hl === name));
  const finalGap = parseFloat(getComputedStyle(line).getPropertyValue('--g')) || 0.8;

  const tl = gsap.timeline({
    defaults: { ease: 'none' },
    scrollTrigger: { trigger: parse, start: 'top top', end: 'bottom bottom', scrub: 0.4 },
  });

  gsap.set(line, { '--g': 0, '--caret': 0 });
  gsap.set($$('.field__meta, .pad, [data-out]', parse), { opacity: 0 });
  gsap.set($$('.field__meta', parse), { y: 8 });
  gsap.set(line, { x: () => (line.parentElement.clientWidth - line.offsetWidth) / 2 });
  setStep(0);

  tl.to({}, { duration: 0.35 })
    .to(line, { '--g': finalGap, x: 0, duration: 1 }, 'split')
    .to(line, { '--caret': 1, duration: 0.4 }, 'split+=.15')
    .to($$('.pad', parse), { opacity: 1, duration: 0.6 }, 'split+=.3');

  ['id', 'name', 'salary'].forEach((name, i) => {
    const field = parse.querySelector(`[data-field="${name}"]`);
    const at = `f${i}`;
    tl.addLabel(at)
      .call(hl(name), null, at).call(() => setStep(i + 1), null, at)
      .to(field.querySelector('.field__meta'), { opacity: 1, y: 0, duration: 0.5 }, at)
      .to(parse.querySelector(`[data-out="${name}"]`), { opacity: 1, duration: 0.5 }, `${at}+=.35`)
      .to({}, { duration: 0.45 });
  });

  tl.call(hl(null)).call(() => setStep(4)).to({}, { duration: 0.5 });

  // Scrubbing backwards has to restore the labels too.
  tl.eventCallback('onUpdate', () => {
    const t = tl.time();
    const marks = ['f0', 'f1', 'f2'].map((l) => tl.labels[l]);
    const i = t < marks[0] ? 0 : t < marks[1] ? 1 : t < marks[2] ? 2 : t < tl.duration() - 0.5 ? 3 : 4;
    setStep(i);
    hl([null, 'id', 'name', 'salary', null][i])();
  });
})();
