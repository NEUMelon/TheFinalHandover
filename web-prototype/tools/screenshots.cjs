// 批量导出关键界面截图（1920×1080，1:1）：node tools/screenshots.cjs [输出目录] [文件名过滤]
// 展板印刷用高分图：SCALE=2 node tools/screenshots.cjs screenshots/2x  → 3840×2160
// 依赖 playwright（npm i playwright，或 NODE_PATH 指向已装好的全局 node_modules）。
// 每张图通过 index.html?jump=<界面> 直达，状态由原型自动补齐。
const { chromium } = require('playwright');
const path = require('path');
const { pathToFileURL } = require('url');
const root = path.resolve(__dirname, '..');
const out = path.resolve(root, process.argv[2] || 'screenshots');
const SHOTS = [
  ['01_主菜单_打卡机', 'title', 1200],
  ['02_交班本_本班', 'desk', 1500],
  ['02b_交班本_1983贴页', 'desk1983', 1500],
  ['03_听音诊断_L1时频图', 'lathe', 2300],
  ['04_三联板_证据齐', 'triad', 1000],
  ['04b_三联板_规则冲突', 'triadWrong', 1600],
  ['05_经验规则卡_ZHOU_CLICK', 'rule', 3300],
  ['06_等级提升通知', 'promo', 1600],
  ['06b_技术等级证', 'cert', 900],
  ['07_衡MES', 'mes', 2200],
  ['08_人物档案_周铁生', 'archive', 1200],
  ['09_工具箱', 'toolbox', 1200],
  ['10_结局签名', 'ending', 9000],
];
(async () => {
  const browser = await chromium.launch();
  for (const [name, jump, ms] of SHOTS) {
    if (process.argv[3] && !name.includes(process.argv[3])) continue;
    const p = await browser.newPage({ viewport: { width: 1920, height: 1080 }, deviceScaleFactor: Number(process.env.SCALE) || 1 });
    p.on('pageerror', e => console.log(name, 'pageerror:', e.message));
    p.on('console', m => { if (m.type() === 'error') console.log(name, 'console:', m.text()); });
    await p.goto(pathToFileURL(path.join(root, 'index.html')).href + '?jump=' + jump);
    await p.waitForFunction(() => document.body.dataset.ready === '1');
    await p.waitForTimeout(ms);
    await p.screenshot({ path: path.join(out, name + '.png') });
    await p.close();
    console.log('ok', name);
  }
  await browser.close();
})();
