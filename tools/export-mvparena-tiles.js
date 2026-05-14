/*
 * MVPArena tile export
 * ─────────────────────
 * Loads docs/30_전장_레벨디자인/10_세부설계/assets/mvparena-map-concepts/export.html
 * in a headless Chromium and screenshots each Variant A tile as PNG.
 *
 * Output: unity/Assets/_Project/Art/MVPArena/{T_FLR.png, T_WAL.png, ...}
 *
 * Run: cd tools && npm install && npm run export-tiles
 */

const path = require('path');
const fs = require('fs');
const puppeteer = require('puppeteer');

const PROJECT_ROOT = path.resolve(__dirname, '..');
const EXPORT_HTML = path.join(
  PROJECT_ROOT,
  'docs', '30_전장_레벨디자인', '10_세부설계',
  'assets', 'mvparena-map-concepts', 'export.html'
);
const OUT_DIR = path.join(
  PROJECT_ROOT,
  'unity', 'Assets', '_Project', 'Art', 'MVPArena'
);

// v11 hash M1SDYjJ1: 34 tile variants + 2 BG + 4 Goal = 40 PNG
const SUFFIX9 = ['TL','TC','TR','ML','MC','MR','BL','BC','BR'];
const TILE_IDS = [
  // T_FLR ×3 (1D: L/M/R), 2×1 unit
  'T_FLR_L', 'T_FLR_M', 'T_FLR_R',
  // T_WAL ×9 (9-slice), 1×2 unit
  ...SUFFIX9.map(s => `T_WAL_${s}`),
  // T_STP ×9 (9-slice), 1×0.5 unit
  ...SUFFIX9.map(s => `T_STP_${s}`),
  // T_RFL ×9 (9-slice), 0.5×1 unit
  ...SUFFIX9.map(s => `T_RFL_${s}`),
  // singletons
  'P_BOX', 'P_PIP', 'T_RMP', 'T_RMP_LEFT',
  // backgrounds
  'BG_MVPARENA', 'BG_TILE',
  // goal portals
  'G_S1_START', 'G_ADVANCE_MID', 'G_S7_END', 'G_GENERIC',
];

(async () => {
  if (!fs.existsSync(EXPORT_HTML)) {
    console.error(`[export-tiles] FATAL: export.html not found at ${EXPORT_HTML}`);
    process.exit(1);
  }
  if (!fs.existsSync(OUT_DIR)) {
    fs.mkdirSync(OUT_DIR, { recursive: true });
    console.log(`[export-tiles] created ${path.relative(PROJECT_ROOT, OUT_DIR)}/`);
  }

  const fileUrl = 'file:///' + EXPORT_HTML.replace(/\\/g, '/');
  console.log(`[export-tiles] loading ${fileUrl}`);

  const browser = await puppeteer.launch({
    headless: 'new',
    args: [
      '--allow-file-access-from-files',
      '--disable-web-security',
      '--no-sandbox',
    ],
  });

  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 1600, height: 1200, deviceScaleFactor: 1 });

    // surface console errors for debugging
    page.on('console', msg => {
      const type = msg.type();
      if (type === 'error' || type === 'warning') {
        console.log(`[browser:${type}] ${msg.text()}`);
      }
    });
    page.on('pageerror', err => console.error(`[browser:pageerror] ${err.message}`));

    await page.goto(fileUrl, { waitUntil: 'networkidle0', timeout: 60000 });

    // wait until React + Babel finish + at least one tile is rendered
    await page.waitForSelector('[data-tile]', { timeout: 30000 });
    // allow SVG filter (turbulence/glow) to paint
    await new Promise(r => setTimeout(r, 1500));

    const renderedIds = await page.$$eval('[data-tile]', els => els.map(e => e.dataset.tile));
    console.log(`[export-tiles] rendered tiles: ${renderedIds.join(', ')}`);

    let ok = 0, skipped = 0;
    for (const tileId of TILE_IDS) {
      const selector = `[data-tile="${tileId}"] svg`;
      const elem = await page.$(selector);
      if (!elem) {
        console.warn(`  SKIP ${tileId}: <svg> not found inside [data-tile="${tileId}"]`);
        skipped++;
        continue;
      }
      const outPath = path.join(OUT_DIR, `${tileId}.png`);
      await elem.screenshot({ path: outPath, omitBackground: true });
      const stat = fs.statSync(outPath);
      console.log(`  OK   ${tileId} → ${path.relative(PROJECT_ROOT, outPath)} (${(stat.size / 1024).toFixed(1)} KB)`);
      ok++;
    }

    console.log(`\n[export-tiles] done · ok=${ok} skipped=${skipped} target=${TILE_IDS.length}`);
    if (skipped > 0) process.exitCode = 2;
  } finally {
    await browser.close();
  }
})().catch(err => {
  console.error(`[export-tiles] FATAL: ${err.stack || err.message}`);
  process.exit(1);
});
