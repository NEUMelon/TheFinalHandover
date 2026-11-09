// 自动点一遍谜题二完整流程，并逐步截图：node tools/flow_test.cjs [输出目录] [要测的 HTML，默认 index.html]
// 第二个参数可以指向 dist/ 下的单文件打包版，确认打包后整条流程照样走得通。
// 打卡 → 交班本 → 翻到 1983 贴页取证 → 去 LT01 → 取听针 → L1 时频图框选取证 → R1 边频取证
// → 回交班本 → 三联板（先选错一次看“规则冲突”，再选对）→ 确认 → ZHOU_CLICK 规则卡 → 灯熄 → 等级提升
// 之后单独验证结局：从暂停 / 演示目录进入 → 代签 → 确认签名 → 给新规则命名。
// 最后用手机横屏 + 触屏再测一遍：“目录”按钮、时频图手指框选、手指签名。
// 依赖 playwright（同 screenshots.cjs）。全部通过时退出码为 0。
const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');
const { pathToFileURL } = require('url');
const root = path.resolve(__dirname, '..');
const out = path.resolve(root, process.argv[2] || 'screenshots/flow');
fs.mkdirSync(out, { recursive: true });
const url = pathToFileURL(path.resolve(process.argv[3] || path.join(root, 'index.html'))).href;

let n = 0, fails = 0;
const ok = (cond, msg) => { console.log((cond ? 'PASS ' : 'FAIL ') + msg); if (!cond) fails++; };

(async () => {
  const browser = await chromium.launch();
  const p = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
  const errors = [];
  p.on('pageerror', e => errors.push(e.message));
  p.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  const shot = async name => { n++; await p.screenshot({ path: path.join(out, `${String(n).padStart(2, '0')}_${name}.png`) }); };
  const S = () => p.evaluate(() => JSON.parse(JSON.stringify(window.__proto.state)));
  const click = async sel => { await p.locator(sel).first().click(); };

  await p.goto(url);
  await p.evaluate(() => localStorage.clear());
  await p.goto(url);
  await p.waitForFunction(() => document.body.dataset.ready === '1');
  await p.waitForTimeout(600);
  await shot('主菜单');

  // 1 打卡
  await click('#cardStart');
  await p.waitForTimeout(3200);
  await shot('打卡后开场');
  await click('#scr-intro');
  await p.waitForTimeout(800);
  ok((await S()).screen === 'desk', '打卡 → 交班桌');

  // 2 翻开交班本
  await click('#cover');
  await p.waitForTimeout(1300);
  ok((await S()).ledgerOpen, '交班本已翻开');
  await shot('交班本_本班');

  // 3 翻到 1983 贴页，读取记录 → 贴页 3
  await click('#pageR .corner-flip');
  await p.waitForTimeout(900);
  ok((await S()).spread === 1, '翻页到 1983 贴页');
  await click('#pageL .oldsheet');
  await p.waitForTimeout(1500);
  await shot('阅读_1983记录');
  ok((await S()).ev.E2_3, '取得贴页 3（1983 记录）');
  await click('#ov-reader .reader');
  await p.waitForTimeout(400);
  await click('#pageL .corner-flip');
  await p.waitForTimeout(900);

  // 4 去 LT01 听
  await click('#goLathe');
  await p.waitForTimeout(800);
  ok((await S()).screen === 'lathe', '交班本 → LT01 听音诊断');
  await p.keyboard.press('1');
  await p.waitForTimeout(300);
  ok((await S()).tool === 'rod', '从工具箱取听针');
  await click('#hotL1');
  await p.waitForTimeout(1600);
  await shot('L1_时频图');

  // 5 框选 L1 竖向冲击条纹（约 1.4–3.3s，2.1–3.9kHz）
  const b = await p.locator('#specBox').boundingBox();
  const fx = t => b.x + t / 6 * 680, fy = f => b.y + (1 - f / 8000) * 280;
  await p.mouse.move(fx(1.4), fy(3900));
  await p.mouse.down();
  await p.mouse.move(fx(2.4), fy(3000), { steps: 6 });
  await p.mouse.move(fx(3.3), fy(2100), { steps: 6 });
  await p.mouse.up();
  await p.waitForTimeout(1200);
  ok((await S()).ev.E2_1, '框选冲击条纹 → 取得贴页 4（L1 时频图）');
  await shot('L1_框选取证');

  // 6 R1 边频
  await click('#hotR1');
  await p.waitForTimeout(900);
  await click('[data-act="sb"][data-v="ok"]');
  await p.waitForTimeout(1200);
  ok((await S()).ev.E2_2, 'R1 边频判正常 → 取得贴页 5');
  await shot('R1_边频取证');

  // 7 回交班本，打开三联板
  await click('.ledger-corner');
  await p.waitForTimeout(800);
  await click('[data-act="triad"]');
  await p.waitForTimeout(600);
  const ids = await p.evaluate(() => window.DATA.triad.map(c => ({ key: c.key, ok: c.options.find(o => o.ok).id })));
  // 先在“经验判断”选错一项
  const wrong = await p.evaluate(() => window.DATA.triad.find(c => c.key === 'exp').options.find(o => !o.ok).id);
  for (const c of ids) await click(`.opt[data-col="${c.key}"][data-opt="${c.key === 'exp' ? wrong : c.ok}"]`);
  await click('#confirmBtn');
  await p.waitForTimeout(1500);
  ok((await S()).conflictCols.includes('exp'), '选错 → 经验判断格显示规则冲突');
  await shot('三联板_规则冲突');

  // 8 改选正确的三项并确认
  await click(`.opt[data-col="exp"][data-opt="${ids.find(c => c.key === 'exp').ok}"]`);
  await p.waitForTimeout(300);
  await shot('三联板_三格正确');
  await click('#confirmBtn');
  await p.waitForTimeout(2600);
  await shot('规则卡_生成中');
  await p.waitForTimeout(2000);
  await shot('规则卡_ZHOU_CLICK');
  let st = await S();
  ok(st.rules.ZHOU_CLICK === 'CONFIRMED', '规则卡 ZHOU_CLICK 已确认');
  ok(st.lamps[1] === false, '交接状态板第二盏灯熄灭');

  // 9 等级提升
  await p.waitForTimeout(1600);
  await shot('等级提升通知');
  st = await S();
  ok(st.level === 2, '衡等级：一级 → 二级');
  await p.waitForTimeout(2600);
  await p.mouse.click(200, 540);
  await p.waitForTimeout(1200);
  ok((await S()).puzzleDone, '收起通知，回到交班桌，谜题二完成');
  await shot('完成后_交班桌');

  // 10 衡 MES 回看
  await click('.tablet-mini');
  await p.waitForTimeout(1800);
  await shot('衡MES_回看');
  await p.keyboard.press('Escape');
  await p.waitForTimeout(500);

  // 11 结局签名（从暂停 / 演示目录进入：网页版点右上角“目录”，本地桌面版按 Esc）
  if (await p.locator('#menuFab').isVisible()) await click('#menuFab'); else await p.keyboard.press('Escape');
  await p.waitForTimeout(500);
  ok(await p.locator('#ov-pause').evaluate(e => e.classList.contains('on')), '打开暂停 / 演示目录');
  await click('[data-act="jump"][data-k="ending"]');
  await p.waitForTimeout(800);
  await click('[data-act="sigAuto"]');
  await p.waitForTimeout(1600);
  await click('[data-act="sigDone"]');
  await p.waitForTimeout(1200);
  await p.locator('#ridInput').fill('');
  await p.locator('#ridInput').type('lin_tail');
  await p.keyboard.press('Enter');
  await p.waitForTimeout(2200);
  await shot('结局_签名与命名');
  const named = await p.evaluate(() => document.querySelector('#endCard').textContent);
  ok(/LIN_TAIL/.test(await p.locator('#ridInput').inputValue().catch(() => '')) || /LIN_TAIL/.test(named), '新规则命名为 LIN_TAIL（自动转大写）');
  await p.waitForTimeout(6000);
  await shot('结局_收尾');

  // 12 手机横屏触屏：用 CDP 发真实触摸事件（Playwright 的 touchscreen 只有 tap，不能拖）
  const ctx = await browser.newContext({ viewport: { width: 844, height: 390 }, hasTouch: true, isMobile: true, deviceScaleFactor: 2 });
  const m = await ctx.newPage();
  m.on('pageerror', e => errors.push('[触屏] ' + e.message));
  const cdp = await ctx.newCDPSession(m);
  const touchDrag = async (pts) => {
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: pts[0][0], y: pts[0][1] }] });
    for (const [x, y] of pts.slice(1)) { await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x, y }] }); await m.waitForTimeout(30); }
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  };
  const tap = async sel => { await m.locator(sel).first().tap(); };
  await m.goto(url);
  await m.evaluate(() => { try { localStorage.clear(); } catch (e) { } });
  await m.goto(url);
  await m.waitForFunction(() => document.body.dataset.ready === '1');
  await m.waitForTimeout(600);
  ok(await m.locator('#menuFab').isVisible(), '[触屏] 右上角显示“目录”按钮');
  await m.screenshot({ path: path.join(out, `${String(++n).padStart(2, '0')}_触屏_主菜单.png`) });
  await tap('#menuFab'); await m.waitForTimeout(400);
  await tap('[data-act="jump"][data-k="lathe"]'); await m.waitForTimeout(1600);
  const mb = await m.locator('#specBox').boundingBox();
  const mx = t => mb.x + t / 6 * mb.width, my = f => mb.y + (1 - f / 8000) * mb.height;
  await touchDrag([[mx(1.4), my(3900)], [mx(1.9), my(3500)], [mx(2.4), my(3000)], [mx(2.9), my(2500)], [mx(3.3), my(2100)]]);
  await m.waitForTimeout(1200);
  ok((await m.evaluate(() => window.__proto.state.ev.E2_1)), '[触屏] 手指框选冲击条纹 → 取得贴页 4');
  await m.screenshot({ path: path.join(out, `${String(++n).padStart(2, '0')}_触屏_框选取证.png`) });
  await tap('#menuFab'); await m.waitForTimeout(400);
  await tap('[data-act="jump"][data-k="ending"]'); await m.waitForTimeout(900);
  const sb = await m.locator('#sigCanvas').boundingBox();
  await touchDrag([0, .2, .4, .6, .8].map((k, i) => [sb.x + sb.width * (.15 + k * .7), sb.y + sb.height * (i % 2 ? .35 : .65)]));
  await m.waitForTimeout(300);
  ok(await m.locator('#sigPh').evaluate(e => e.style.display === 'none'), '[触屏] 手指在接班人栏签名');
  await m.screenshot({ path: path.join(out, `${String(++n).padStart(2, '0')}_触屏_手写签名.png`) });
  await ctx.close();

  ok(errors.length === 0, '页面无脚本报错' + (errors.length ? '：' + errors.join(' | ') : ''));
  await browser.close();
  console.log(fails ? `${fails} 项未通过` : '全部通过');
  process.exit(fails ? 1 : 0);
})();
