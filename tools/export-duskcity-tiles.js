/*
 * DuskCity tile export
 * ─────────────────────
 * Loads .tmp/design-fetch-v13/beans/project/tile-export-duskcity.html
 * in headless Chromium and screenshots each tile as PNG.
 *
 * Output:
 *   33 tiles → unity/Assets/_Project/Art/DuskCity/Tiles/{id}.png
 *   1 BG     → unity/Assets/_Project/Art/DuskCity/BG_DUSKCITY.png  (overwrite)
 *
 * Run: cd tools && node export-duskcity-tiles.js
 *
 * NOTE: export-mvparena-tiles.js is NOT modified. This is a separate script.
 */

const path = require('path');
const fs   = require('fs');
const puppeteer = require('puppeteer');

const PROJECT_ROOT = path.resolve(__dirname, '..');

const EXPORT_HTML = path.join(
  PROJECT_ROOT,
  '.tmp', 'design-fetch-v13', 'beans', 'project',
  'tile-export-duskcity.html'
);

const TILES_OUT_DIR = path.join(
  PROJECT_ROOT,
  'unity', 'Assets', '_Project', 'Art', 'DuskCity', 'Tiles'
);

const BG_OUT_PATH = path.join(
  PROJECT_ROOT,
  'unity', 'Assets', '_Project', 'Art', 'DuskCity', 'BG_DUSKCITY.png'
);

const SUFFIX9 = ['TL','TC','TR','ML','MC','MR','BL','BC','BR'];

// 33 tile IDs (DuskCity set — same names as MVPArena, different art)
const TILE_IDS = [
  'T_FLR_L', 'T_FLR_M', 'T_FLR_R',
  ...SUFFIX9.map(s => `T_WAL_${s}`),
  ...SUFFIX9.map(s => `T_STP_${s}`),
  ...SUFFIX9.map(s => `T_RFL_${s}`),
  'T_RMP', 'T_RMP_LEFT',
  'P_BOX',
];

// dimensions (pixels at scale=1, matching tile-sprites.jsx)
const TILE_DIMS = {
  T_FLR_L: {w:200, h:100}, T_FLR_M: {w:200, h:100}, T_FLR_R: {w:200, h:100},
  ...Object.fromEntries(SUFFIX9.map(s => [`T_WAL_${s}`, {w:100, h:200}])),
  ...Object.fromEntries(SUFFIX9.map(s => [`T_STP_${s}`, {w:100, h:50}])),
  ...Object.fromEntries(SUFFIX9.map(s => [`T_RFL_${s}`, {w:50,  h:100}])),
  T_RMP:      {w:100, h:100},
  T_RMP_LEFT: {w:100, h:100},
  P_BOX:      {w:100, h:100},
};

(async () => {
  if (!fs.existsSync(EXPORT_HTML)) {
    console.error(`[export-duskcity-tiles] FATAL: HTML not found:\n  ${EXPORT_HTML}`);
    process.exit(1);
  }

  // ensure output dirs
  if (!fs.existsSync(TILES_OUT_DIR)) {
    fs.mkdirSync(TILES_OUT_DIR, { recursive: true });
    console.log(`[export-duskcity-tiles] created ${path.relative(PROJECT_ROOT, TILES_OUT_DIR)}/`);
  }

  const fileUrl = 'file:///' + EXPORT_HTML.replace(/\\/g, '/');
  console.log(`[export-duskcity-tiles] loading ${fileUrl}`);

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

    // viewport large enough for BG (7280×1300) — scale=4 means we render at native
    // but screenshot each element individually, so viewport just needs to be big enough
    // for the largest single tile (T_FLR 200×100). BG is rendered via element screenshot.
    await page.setViewport({ width: 7400, height: 1400, deviceScaleFactor: 1 });

    page.on('console', msg => {
      const type = msg.type();
      if (type === 'error' || type === 'warning') {
        console.log(`[browser:${type}] ${msg.text()}`);
      }
    });
    page.on('pageerror', err => console.error(`[browser:pageerror] ${err.message}`));

    await page.goto(fileUrl, { waitUntil: 'networkidle0', timeout: 90000 });

    // wait for React render — first tile container must be visible
    await page.waitForSelector('[data-asset-id]', { timeout: 30000 });
    // allow SVG filters (glow/blur) to finish painting
    await new Promise(r => setTimeout(r, 2000));

    const renderedIds = await page.$$eval('[data-asset-id]', els => els.map(e => e.dataset.assetId));
    console.log(`[export-duskcity-tiles] rendered asset elements: ${renderedIds.length}`);

    let okTiles = 0, skippedTiles = 0;

    // ── 33 tiles ──────────────────────────────────────────────
    console.log('\n── tiles ──');
    for (const tileId of TILE_IDS) {
      const selector = `#export-${tileId}`;
      const elem = await page.$(selector);
      if (!elem) {
        console.warn(`  SKIP ${tileId}: element #export-${tileId} not found`);
        skippedTiles++;
        continue;
      }

      const outPath = path.join(TILES_OUT_DIR, `${tileId}.png`);
      await elem.screenshot({ path: outPath, omitBackground: true });
      const stat = fs.statSync(outPath);
      const dims = TILE_DIMS[tileId] || {};
      console.log(`  OK   ${tileId} → Tiles/${tileId}.png  (${(stat.size/1024).toFixed(1)} KB, native ${dims.w||'?'}×${dims.h||'?'})`);
      okTiles++;
    }

    // ── BG_DUSKCITY ───────────────────────────────────────────
    console.log('\n── background ──');
    const bgSelector = `#export-BG_DUSKCITY`;
    const bgElem = await page.$(bgSelector);
    if (!bgElem) {
      console.warn(`  SKIP BG_DUSKCITY: element not found`);
    } else {
      await bgElem.screenshot({ path: BG_OUT_PATH, omitBackground: true });
      const stat = fs.statSync(BG_OUT_PATH);
      console.log(`  OK   BG_DUSKCITY → Art/DuskCity/BG_DUSKCITY.png  (${(stat.size/1024).toFixed(1)} KB, native 7280×1300)`);
    }

    console.log(`\n[export-duskcity-tiles] done · tiles ok=${okTiles} skipped=${skippedTiles}/${TILE_IDS.length}`);
    if (skippedTiles > 0) process.exitCode = 2;

  } finally {
    await browser.close();
  }
})().catch(err => {
  console.error(`[export-duskcity-tiles] FATAL: ${err.stack || err.message}`);
  process.exit(1);
});
