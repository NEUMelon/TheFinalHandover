// 单页截图：node tools/shot.cjs <html 相对路径> <输出 png> [宽] [高] [full]
// 需要 playwright：本地 npm i playwright，或 NODE_PATH 指向全局 node_modules。
const { chromium } = require('playwright');
const path = require('path');
const { pathToFileURL } = require('url');
const root = path.resolve(__dirname, '..');
const [, , page = 'index.html', out = 'screenshots/shot.png', w = '1920', h = '1080', full = ''] = process.argv;
(async () => {
  const browser = await chromium.launch();
  const p = await browser.newPage({ viewport: { width: +w, height: +h } });
  p.on('console', m => { if (m.type() === 'error') console.log('console:', m.text()); });
  p.on('pageerror', e => console.log('pageerror:', e.message));
  await p.goto(pathToFileURL(path.join(root, page)).href);
  await p.waitForTimeout(1500);
  await p.screenshot({ path: path.resolve(root, out), fullPage: !!full });
  await browser.close();
  console.log('ok', out);
})();
