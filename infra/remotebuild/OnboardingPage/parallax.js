(() => {
  function generateStars(root, count, seed) {
    if (!root) return;
    let s = seed;
    const rnd = () => (s = (s * 9301 + 49297) % 233280) / 233280;
    const sizes = ['1px', '1px', '1.5px', '2px'];
    let buf = '';
    for (let i = 0; i < count; i++) {
      const x = rnd() * 100;
      const y = rnd() * 100;
      const size = sizes[Math.floor(rnd() * sizes.length)];
      const op = (0.35 + rnd() * 0.65).toFixed(2);
      const blink = rnd() > 0.92 ? `animation: starBlink ${3 + rnd() * 3}s ${rnd() * 4}s ease-in-out infinite;` : '';
      buf += `<div style="position:absolute;left:${x}%;top:${y}%;width:${size};height:${size};background:#fff;border-radius:50%;opacity:${op};${blink}"></div>`;
    }
    root.innerHTML = buf;
  }

  generateStars(document.querySelector('.layer-stars'), 220, 1337);
  generateStars(document.querySelector('.bg-stars'), 180, 7919);

  const layers = [
    { el: document.querySelector('.layer-stars'),   speed: -0.02 },
    { el: document.querySelector('.layer-planets'), speed: -0.12 },
    { el: document.querySelector('.layer-beam'),    speed: -0.30 },
    { el: document.querySelector('.layer-beans'),   speed: -0.55 },
  ].filter(l => l.el);

  const beansLayer = document.querySelector('.layer-beans');
  const hero = document.querySelector('.hero');
  const heroText = document.querySelector('.hero-text');
  const heroSub = document.querySelector('.hero-sub');
  const heroCta = document.querySelector('.hero-cta');
  const scrollCue = document.querySelector('.scroll-cue');

  let ticking = false;
  function update() {
    const y = window.scrollY;
    const vh = window.innerHeight;
    const heroEnd = hero ? hero.offsetHeight - vh : 1000;

    for (const l of layers) {
      l.el.style.transform = `translate3d(0, ${y * l.speed}px, 0)`;
    }

    if (beansLayer) {
      beansLayer.style.opacity = Math.max(0, 1 - y / 400);
    }

    if (heroText) {
      const subProgress = Math.min(1, Math.max(0, (y - 200) / 300));
      if (heroSub) {
        heroSub.style.opacity = subProgress;
        heroSub.style.transform = `translateY(${(1 - subProgress) * 30}px)`;
      }

      const ctaProgress = Math.min(1, Math.max(0, (y - 350) / 300));
      if (heroCta) {
        heroCta.style.opacity = ctaProgress;
        heroCta.style.transform = `translateY(${(1 - ctaProgress) * 30}px)`;
      }

      heroText.style.opacity = 1;
    }

    if (scrollCue) {
      scrollCue.style.opacity = Math.max(0, 1 - y / 150);
    }

    ticking = false;
  }
  window.addEventListener('scroll', () => {
    if (!ticking) {
      requestAnimationFrame(update);
      ticking = true;
    }
  }, { passive: true });
  update();

  const io = new IntersectionObserver((entries) => {
    entries.forEach(e => {
      if (e.isIntersecting) {
        e.target.classList.add('in');
        io.unobserve(e.target);
      }
    });
  }, { threshold: 0.15 });
  document.querySelectorAll('.reveal').forEach(el => io.observe(el));

  const snapSections = [
    document.querySelector('.features'),
    document.querySelector('.team'),
    document.querySelector('footer')
  ].filter(Boolean);

  if (!snapSections.length) return;

  let isSnapping = false;

  window.addEventListener('wheel', (e) => {
    if (isSnapping) { e.preventDefault(); return; }

    const y = window.scrollY;
    const vh = window.innerHeight;
    const heroH = hero ? hero.offsetHeight : 0;
    const heroScrollEnd = heroH - vh;
    const firstTop = snapSections[0].offsetTop;

    if (y < heroScrollEnd - 60) return;

    const absDelta = Math.abs(e.deltaMode === 1 ? e.deltaY * 40 : e.deltaY);

    if (y < firstTop - 20) {
      if (e.deltaY > 0 && absDelta > 15) {
        e.preventDefault();
        snapTo(snapSections[0]);
      }
      return;
    }

    e.preventDefault();
    if (absDelta < 15) return;

    const idx = findCurrentSection(y);
    if (e.deltaY > 0) {
      if (idx < snapSections.length - 1) snapTo(snapSections[idx + 1]);
    } else {
      if (idx === 0) {
        snapTo(0);
      } else {
        snapTo(snapSections[idx - 1]);
      }
    }
  }, { passive: false });

  function findCurrentSection(scrollY) {
    for (let i = snapSections.length - 1; i >= 0; i--) {
      if (scrollY >= snapSections[i].offsetTop - 50) return i;
    }
    return 0;
  }

  function snapTo(target) {
    isSnapping = true;
    const targetEl = typeof target === 'number' ? null : target;
    const top = typeof target === 'number' ? target : target.offsetTop;
    window.scrollTo({ top, behavior: 'smooth' });
    setTimeout(() => {
      if (targetEl) {
        targetEl.querySelectorAll('.reveal').forEach(el => el.classList.add('in'));
      }
      setTimeout(() => { isSnapping = false; }, 700);
    }, 800);
  }

  // -------- features carousel --------
  const track = document.querySelector('.features-track');
  const slides = track ? track.querySelectorAll('.feature') : [];
  const prevBtn = document.querySelector('.carousel-prev');
  const nextBtn = document.querySelector('.carousel-next');
  const dots = document.querySelectorAll('.carousel-dot');

  if (track && slides.length) {
    let cur = 0;
    let autoId;

    function goTo(idx) {
      cur = ((idx % slides.length) + slides.length) % slides.length;
      track.style.transform = `translateX(-${cur * 100}%)`;
      dots.forEach((d, i) => d.classList.toggle('active', i === cur));
    }

    function startAuto() {
      clearInterval(autoId);
      autoId = setInterval(() => goTo(cur + 1), 4000);
    }

    prevBtn?.addEventListener('click', () => { goTo(cur - 1); startAuto(); });
    nextBtn?.addEventListener('click', () => { goTo(cur + 1); startAuto(); });
    dots.forEach((d, i) => d.addEventListener('click', () => { goTo(i); startAuto(); }));

    startAuto();
  }
})();
