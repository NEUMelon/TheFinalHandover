/* 《最后一次交班》UI Prototype —— 状态机与交互
   主线（可点击走通）：打卡 → 打开交班本 → 谜题二“咔哒在左” → 听针贴 L1 / R1 → 时频图框选 / 边频判断
   → 翻到一九八三年贴页 → 三联板选三项 → 确认 → ZHOU_CLICK 经验规则卡 → 交接状态灯熄灭 → 衡等级提升。
   开局状态：谜题一已完成（ZHOU_LEFT 已确认，第一盏灯已熄，衡为一级）。 */
(() => {
  'use strict';
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => [...r.querySelectorAll(s)];
  const D = window.DATA, A = window.AUDIO, SP = window.SPECTRO, SV = window.SVG;
  const stage = $('#stage');
  const wait = ms => new Promise(r => setTimeout(r, ms));
  const SAVE_KEY = 'lsb_ui_proto_v1';

  /* ---------------- 舞台缩放 ---------------- */
  let scale = 1;
  function fit() {
    const g = innerWidth < 700 ? 16 : 0; // 窄屏（手机竖屏）左右各留 16px
    scale = Math.min((innerWidth - 2 * g) / 1920, innerHeight / 1080);
    stage.style.transformOrigin = '50% 50%';
    stage.style.transform = `translate(-50%, -50%) scale(${scale})`;
  }
  addEventListener('resize', fit); fit();
  function rectOf(el) {
    const r = el.getBoundingClientRect(), s = stage.getBoundingClientRect();
    return { x: (r.left - s.left) / scale, y: (r.top - s.top) / scale, w: r.width / scale, h: r.height / scale };
  }
  function stagePt(e) { const s = stage.getBoundingClientRect(); return { x: (e.clientX - s.left) / scale, y: (e.clientY - s.top) / scale }; }

  /* ---------------- 状态 ---------------- */
  const fresh = () => ({
    screen: 'title', ledgerOpen: false, spread: 0,
    ev: { E2_1: false, E2_2: false, E2_3: false },
    rules: { ZHOU_LEFT: 'CONFIRMED', ZHOU_CLICK: null },
    level: 1, lamps: [false, true, true, true],
    pick: { phen: null, exp: null, data: null }, conflictCols: [],
    point: null, tool: null, boxOk: null, sbDone: false, devCount: 0,
    hintLevel: 0, puzzleDone: false, logged: {},
    hintsOn: true, hintFast: false, captions: true, vol: .8,
  });
  let S = fresh();
  let LOG = D.logBoot.map((l, i) => ({ id: i, t: l[0], m: l[1], c: l[2], k: l[3] || '' }));
  let logId = LOG.length, lastAct = performance.now(), deskStart = 0, busy = false;

  function save() { try { localStorage.setItem(SAVE_KEY, JSON.stringify({ S, LOG })); } catch (e) { } }
  function loadSave() { try { const j = JSON.parse(localStorage.getItem(SAVE_KEY)); if (j && j.S) return j; } catch (e) { } return null; }
  function act() { lastAct = performance.now(); }

  /* ---------------- 剧情内时钟（时间压缩：谜题二约 17:30） ---------------- */
  function clock() {
    if (S.screen === 'ending') return '23:58';
    const m = 17 * 60 + 32 + Math.min(26, Math.floor((performance.now() - (deskStart || performance.now())) / 9000));
    return `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`;
  }

  const CN = '〇一二三四五六七八九';
  const cnNum = n => n < 10 ? CN[n] : (n < 20 ? '' : CN[Math.floor(n / 10)]) + '十' + (n % 10 ? CN[n % 10] : '');
  const cnTime = t => { const [h, m] = t.split(':').map(Number); return cnNum(h) + '点' + (m ? cnNum(m) + '分' : '整'); };

  /* ---------------- 衡日志 ---------------- */
  function log(m, c, k = '', t = clock()) { LOG.push({ id: logId++, t, m, c, k }); refreshLogs(); }
  function logOnce(key, m, c, k) { if (S.logged[key]) return; S.logged[key] = 1; log(m, c, k); }
  function liHTML(l) {
    const c = l.c.replace(/(ZHOU_[A-Z]+|JIANGUO_GUARD|SUN_HALFBEAT)/g, '<span class="c">$1</span>');
    return `<span class="t">[${l.t}]</span> <span class="m">${l.m.padEnd(4, ' ')} &gt;</span> ${c}`;
  }
  function renderLog(ul, n) {
    if (!ul) return;
    const lines = LOG.slice(-n), last = +(ul.dataset.last || -1);
    ul.innerHTML = '';
    lines.forEach(l => {
      const li = document.createElement('li');
      if (l.k) li.className = l.k;
      li.innerHTML = liHTML(l);
      ul.appendChild(li);
      if (l.id > last && ul.offsetParent) typewrite(li);
    });
    ul.dataset.last = LOG.length ? LOG[LOG.length - 1].id : -1;
  }
  /* 逐字打出（UI-DS §5.5：每字 20ms，匀速） */
  function typewrite(li) {
    const full = li.innerHTML, text = li.textContent; let i = 0;
    li.textContent = '';
    const step = () => {
      i += 1;
      if (i >= text.length) { li.innerHTML = full; return; }
      li.textContent = text.slice(0, i);
      setTimeout(step, 20);
    };
    step();
  }
  function refreshLogs() {
    renderLog($('#miniLog'), 7);
    renderLog($('#tabletLog'), 6);
    renderLog($('#triadLog'), 3);
    renderLog($('#mesLog'), 26);
    renderLog($('#ruleLog'), 4);
  }

  /* ---------------- 字幕 ---------------- */
  let capTimer = 0;
  function caption(text, who = '', ms = 2600) {
    if (!S.captions) return;
    const el = $('#caption');
    el.innerHTML = `<span>${who ? `<b class="who">${who}</b>` : ''}${text}</span>`;
    el.classList.add('on'); clearTimeout(capTimer);
    if (ms) capTimer = setTimeout(() => el.classList.remove('on'), ms);
  }
  function walkie(text, ms = 2600) {
    const w = $('#walkie'); w.classList.add('talk'); A.play('tick', .6);
    caption(text, '对讲机 · 周建国', ms);
    setTimeout(() => w.classList.remove('talk'), ms);
  }

  /* ---------------- 屏幕切换 ---------------- */
  function go(name) {
    $$('.screen').forEach(s => s.classList.toggle('on', s.id === 'scr-' + name));
    S.screen = name;
    $('#toolbox').classList.toggle('show', name === 'desk' || name === 'lathe');
    closeToolbox();
    if (name !== 'lathe') A.stopLoop();
    if (name === 'desk') enterDesk();
    if (name === 'lathe') enterLathe();
    if (name === 'mes') renderMES();
    if (name === 'ending') enterEnding();
    if (name === 'title') enterTitle();
    renderTools();
    refreshLogs();
  }
  function overlay(id, on = true) { $('#ov-' + id).classList.toggle('on', on); }
  function closeOverlays(except = []) { $$('.overlay.on').forEach(o => { if (!except.includes(o.id)) o.classList.remove('on'); }); }

  /* =====================================================================
     1 主菜单：打卡机
     ===================================================================== */
  const scribble = (seed) => {
    let s = seed; const r = () => (s = (s * 16807) % 2147483647) / 2147483647;
    let d = 'M4 20'; for (let i = 0; i < 6; i++) d += ` q${(6 + r() * 8).toFixed(0)} ${(-14 + r() * 20).toFixed(0)} ${(12 + r() * 10).toFixed(0)} ${(-4 + r() * 8).toFixed(0)}`;
    return `<svg width="104" height="30" viewBox="0 0 130 30"><path d="${d}" fill="none" stroke="#1F3A5F" stroke-width="2" stroke-linecap="round" opacity=".7"/></svg>`;
  };
  function buildRack() {
    const has = !!loadSave();
    const cells = [
      `<button class="tcard menu-card" id="cardStart" data-act="punch"><span class="menu">开 始</span><span class="who">林小满　0347</span><span class="stamp-time">16:00</span></button>`,
      `<div class="tcard filler"><span class="who" style="font-size:26px;opacity:.85">周建国</span><span class="mono" style="font-size:14px;color:#6b6455">0112</span></div>`,
      has ? `<button class="tcard menu-card" data-act="continue"><span class="menu">继 续</span><span class="who">接着上次的班</span></button>`
        : `<button class="tcard menu-card" data-act="noop" disabled><span class="menu" style="color:#b9b09c">继 续</span><span class="who" style="color:#a69f8e;font-size:18px">（空白卡，无存档）</span></button>`,
      `<div class="tcard filler">${scribble(3)}</div>`,
      `<button class="tcard menu-card" data-act="settings"><span class="menu">设 置</span><span class="who">提示 · 字幕</span></button>`,
      `<div class="tcard filler">${scribble(9)}</div>`,
      `<button class="tcard menu-card" data-act="quit"><span class="menu">退 出</span><span class="who">下班</span></button>`,
      `<div class="tcard filler">${scribble(14)}</div>`,
      `<div class="tcard filler">${scribble(21)}</div>`,
      `<div class="tcard filler">${scribble(27)}</div>`,
    ];
    $('#rackGrid').innerHTML = cells.map((c, i) => `<div class="pocket">${c}<div class="lip" data-no="${String(i + 1).padStart(2, '0')}"></div></div>`).join('');
  }
  function buildDial() {
    let t = '';
    for (let i = 0; i < 60; i++) {
      const a = i * 6 * Math.PI / 180, big = i % 5 === 0;
      const r1 = big ? 112 : 120, r2 = 127;
      t += `<line x1="${150 + r1 * Math.sin(a)}" y1="${150 - r1 * Math.cos(a)}" x2="${150 + r2 * Math.sin(a)}" y2="${150 - r2 * Math.cos(a)}" stroke="#1A1A1A" stroke-width="${big ? 3 : 1}"/>`;
      if (big) { const n = i / 5 || 12, rr = 96; t += `<text x="${150 + rr * Math.sin(a)}" y="${150 - rr * Math.cos(a) + 7}" text-anchor="middle" font-family="NotoSerifSC" font-weight="700" font-size="20" fill="#1A1A1A">${n}</text>`; }
    }
    $('#dialTicks').innerHTML = t;
  }
  let dialTime = { h: 15, m: 58, s: 10 }, dialTimer = 0;
  function setHands(h, m, s) {
    $('#hHour').setAttribute('transform', `rotate(${(h % 12) * 30 + m * .5} 150 150)`);
    $('#hMin').setAttribute('transform', `rotate(${m * 6 + s * .1} 150 150)`);
    $('#hSec').setAttribute('transform', `rotate(${s * 6} 150 150)`);
  }
  function enterTitle() {
    buildRack(); buildDial();
    dialTime = { h: 15, m: 58, s: 10 }; $('#slotTime').textContent = '15:58';
    setHands(15, 58, 10);
    clearInterval(dialTimer);
    dialTimer = setInterval(() => { if (S.screen !== 'title' || busy) return; dialTime.s = (dialTime.s + 1) % 60; setHands(dialTime.h, dialTime.m, dialTime.s); }, 1000);
    $('.notice-form dd.blank, .notice-form dd.signed') && ($('.notice-form dd:last-of-type').className = 'blank', $('.notice-form dd:last-of-type').innerHTML = '');
    focusIdx = 0; markFocus();
  }
  let focusIdx = 0;
  function menuCards() { return $$('#rackGrid button.tcard:not([disabled])'); }
  function markFocus() { menuCards().forEach((c, i) => c.classList.toggle('focus', i === focusIdx)); }

  async function punch() {
    if (busy) return; busy = true;
    A.init(); A.setVolume(S.vol);
    const card = $('#cardStart'), r = rectOf(card), mouth = rectOf($('.slot-mouth'));
    const fly = card.cloneNode(true); fly.removeAttribute('id'); fly.classList.remove('focus');
    Object.assign(fly.style, { left: r.x + 'px', top: r.y + 'px', width: r.w + 'px', height: r.h + 'px', margin: 0, zIndex: 860, position: 'absolute' });
    fly.classList.add('fly'); stage.appendChild(fly); card.style.visibility = 'hidden';
    const tx = mouth.x + mouth.w / 2 - r.w / 2, ty = mouth.y - r.h + 10;
    await fly.animate([{ left: r.x + 'px', top: r.y + 'px', transform: 'rotate(0deg)' }, { left: (r.x + tx) / 2 + 'px', top: Math.min(r.y, ty) - 80 + 'px', transform: 'rotate(-6deg)' }, { left: tx + 'px', top: ty + 'px', transform: 'rotate(0deg)' }],
      { duration: 620, easing: 'cubic-bezier(.215,.61,.355,1)', fill: 'forwards' }).finished;
    // 插入：卡片下沉，插槽以下部分被遮住
    await fly.animate([{ top: ty + 'px', clipPath: 'inset(0 0 0 0)' }, { top: ty + 70 + 'px', clipPath: 'inset(0 0 70px 0)' }], { duration: 200, easing: 'ease-in', fill: 'forwards' }).finished;
    A.play('punch');
    // 咔—哒：时间跳到 16:00
    await wait(460);
    $('#slotTime').textContent = '16:00'; setHands(16, 0, 0);
    fly.classList.add('punched');
    await fly.animate([{ top: ty + 70 + 'px', clipPath: 'inset(0 0 70px 0)' }, { top: ty + 10 + 'px', clipPath: 'inset(0 0 10px 0)' }], { duration: 480, easing: 'cubic-bezier(.215,.61,.355,1)', fill: 'forwards' }).finished;
    const dd = $('.notice-form dd:last-of-type'); dd.className = 'signed'; dd.innerHTML = '<span class="hand-lin write" style="font-size:32px;--dur:700ms">林小满</span>';
    await wait(1100);
    fly.remove(); card.style.visibility = '';
    // 新游戏
    const keep = { hintsOn: S.hintsOn, hintFast: S.hintFast, captions: S.captions, vol: S.vol };
    S = Object.assign(fresh(), keep);
    LOG = D.logBoot.map((l, i) => ({ id: i, t: l[0], m: l[1], c: l[2], k: l[3] || '' })); logId = LOG.length;
    busy = false;
    go('intro');
    runIntro();
  }
  let introTimer = [];
  function runIntro() {
    const line = $('#introLine'); line.classList.remove('write'); void line.offsetWidth; line.style.setProperty('--dur', '2400ms'); line.classList.add('write');
    introTimer.forEach(clearTimeout);
    introTimer = [setTimeout(() => caption('本子在桌上，按单子走。', '对讲机 · 周建国', 2400), 2900), setTimeout(endIntro, 5600)];
  }
  function endIntro() { if (S.screen !== 'intro') return; introTimer.forEach(clearTimeout); go('desk'); }

  /* =====================================================================
     2 交班本
     ===================================================================== */
  function enterDesk() {
    if (!deskStart) deskStart = performance.now();
    renderHandover($('#handoverDesk'));
    const L = $('#ledger');
    L.classList.toggle('closed', !S.ledgerOpen); L.classList.toggle('open', S.ledgerOpen);
    $('#cover').classList.toggle('gone', S.ledgerOpen);
    if (S.ledgerOpen) renderSpread();
    else setTimeout(() => S.screen === 'desk' && !S.ledgerOpen && caption('点击交班本，翻开。', '', 2400), 700);
  }
  async function openLedger() {
    if (S.ledgerOpen || busy) return; busy = true; act();
    renderSpread();
    const L = $('#ledger');
    L.classList.remove('closed'); A.play('flip', .8);
    await wait(60); L.classList.add('open');
    await wait(950); $('#cover').classList.add('gone');
    S.ledgerOpen = true; busy = false; save();
    caption('十七点三十分　点检第二项：LT01 主轴异响', '', 3000);
  }

  const spreads = [
    { L: pageLastShift, R: pageThisShift },
    { L: page1983, R: pageSketch },
  ];
  function renderSpread() {
    const sp = spreads[S.spread];
    $('#pageL').innerHTML = sp.L(); $('#pageR').innerHTML = sp.R();
    hydrate($('#ledger'));
  }
  function hydrate(root) {
    $$('canvas[data-ht]', root).forEach(c => window.HALFTONE.render(c, c.dataset.ht, { cell: +(c.dataset.cell || 3.2) }));
    $$('[data-stamp]', root).forEach(el => { el.innerHTML = SV.stampPass(...el.dataset.stamp.split('|')); });
  }
  async function flip(dir) {
    const to = S.spread + dir;
    if (busy || !S.ledgerOpen || to < 0 || to >= spreads.length) return;
    busy = true; act(); A.play('flip', .9);
    const pages = $('.pages', $('#ledger')), cur = spreads[S.spread], nxt = spreads[to];
    const leaf = document.createElement('div'); leaf.className = 'leaf';
    const fwd = dir > 0;
    leaf.innerHTML = `<div class="face front"><div class="page right" style="left:20px">${fwd ? cur.R() : nxt.R()}</div></div><div class="face back"><div class="page left" style="left:0">${fwd ? nxt.L() : cur.L()}</div></div>`;
    hydrate(leaf);
    if (fwd) { $('#pageR').innerHTML = nxt.R(); } else { $('#pageL').innerHTML = nxt.L(); }
    hydrate(pages);
    pages.appendChild(leaf);
    const dur = parseInt(getComputedStyle(document.documentElement).getPropertyValue('--D-Flip')) || 520;
    await leaf.animate(fwd ? [{ transform: 'rotateY(0deg)' }, { transform: 'rotateY(-180deg)' }] : [{ transform: 'rotateY(-180deg)' }, { transform: 'rotateY(0deg)' }],
      { duration: dur, easing: 'cubic-bezier(.215,.61,.355,1)', fill: 'forwards' }).finished;
    S.spread = to; renderSpread(); leaf.remove(); busy = false;
  }

  function pageLastShift() {
    return `
      <div class="pg-head"><h2>上一班</h2><span>交班记录</span></div>
      <div class="pg-meta">日期 <span class="hand-jian">十月　日</span>　班次 <span class="hand-jian">中班</span>　交班人 <span class="hand-jian">建国</span></div>
      <div class="notes">
        <p>小林：</p>
        <p>LT01 照常开。</p>
        <p>衡报齿轮磨损，要停车。我看不用停。</p>
        <p>护罩，开车前看一眼。</p>
        <p class="sig">建国　十六点</p>
      </div>
      <button class="paste sleeve" data-act="read" data-item="photo" style="left:70px;top:520px;--rot:rotate(-4deg);transform:var(--rot)">
        <span class="clip"></span>
        <span class="photo"><canvas data-ht="workPhoto" width="300" height="220"></canvas></span>
        <span class="cap">老周　一九八三</span>
      </button>
      <div class="paste mealticket" style="left:400px;top:610px;transform:rotate(6deg)"><b>三号车间食堂</b><strong>菜票 贰角</strong><small>No.0471</small></div>
      <div class="tape" style="left:420px;top:596px;transform:rotate(-18deg);width:60px"></div>
      <div class="pg-foot"><span>— 1 —</span><span>16:00</span></div>`;
  }

  function pageThisShift() {
    const done = S.rules.ZHOU_CLICK === 'CONFIRMED';
    const evs = [['E2_3', '贴页 3'], ['E2_1', '贴页 4'], ['E2_2', '贴页 5']];
    const all = S.ev.E2_1 && S.ev.E2_2 && S.ev.E2_3;
    return `
      <div class="pg-head"><h2>本班</h2><span>点检记录　中班 16:00—24:00</span></div>
      <div class="pg-meta">接班人 <span class="hand-lin" style="font-size:24px">林小满</span><span style="margin-left:auto" class="hand-lin">十七点三十分</span></div>
      ${!S.hintsOn ? `<button class="hint-btn" data-act="hintNow">需要提示</button>` : ''}
      <div class="chk">
        <div class="h">点检项</div><div class="h">异常</div><div class="h">处理</div><div class="h">签名</div>

        <div class="c item"><b>1</b>LT01 主轴后轴承测点</div>
        <div class="c hand-lin">右端 R1 只听见一片“嗡”</div>
        <div class="c hand-lin">改用左端 L1（老周的点）<br><span class="rid">ZHOU_LEFT</span></div>
        <div class="c sig-cell"><span class="hand-lin" style="font-size:28px">林</span><span class="mini-stamp" data-stamp="合格|三号车间 · 点检"></span></div>

        <div class="c item ${done ? '' : 'row-cur'}"><b>2</b>LT01 主轴异响</div>
        <div class="c hand-lin ${done ? '' : 'row-cur'}"><span class="${done ? 'strike' : ''}">衡报：变速齿轮磨损，建议停车换齿轮</span></div>
        <div class="c hand-lin ${done ? '' : 'row-cur'}">${done ? '后轴承早期缺陷，列入计划检修，不停车<br><span class="rid">ZHOU_CLICK</span>' : `<span class="pencil" style="font-size:20px">待查</span><br><button class="go-link" id="goLathe" data-act="lathe">→ 去 LT01 听</button>`}</div>
        <div class="c sig-cell ${done ? '' : 'row-cur'}">${done ? '<span class="hand-lin" style="font-size:28px">林</span><span class="mini-stamp" data-stamp="合格|三号车间 · 点检"></span>' : ''}</div>

        <div class="c item row-dim"><b>3</b>LT01 护罩 / 启动条件</div><div class="c"></div><div class="c"></div><div class="c"></div>
        <div class="c item row-dim"><b>4</b>CR01 天车试吊</div><div class="c"></div><div class="c"></div><div class="c"></div>
      </div>
      <div class="ev-row">本项贴页 ${evs.map(([k, n]) => `<span class="ev-clip ${S.ev[k] ? 'got' : ''}">${n}</span>`).join('')}</div>
      ${done ? '' : `<button class="triad-tab ${all ? 'ready' : ''}" data-act="triad">三联板</button>`}
      ${hintHTML()}
      <button class="corner-flip" data-act="flipNext">${S.ev.E2_3 ? '' : '<i class="newmark"></i>'}<span>翻页 D</span></button>
      <div class="pg-foot"><span>— 2 —</span><span>${clock()}</span></div>`;
  }

  /* 三级提示（UI-DS §5.8）：页边铅笔 → 钢笔 + 箭头 → 钢笔 + 圈出 */
  function hintHTML() {
    if (S.puzzleDone || !S.hintLevel) return '';
    const h = D.hints;
    let out = `<div class="margin-hint l1 write" style="left:0;top:704px;--dur:800ms">${h[0].text}</div>`;
    if (S.hintLevel >= 2) out += `<div class="margin-hint l2 write" style="left:0;top:740px;--dur:800ms">${h[1].text}
        <svg width="60" height="60" style="left:230px;top:-290px"><path d="M10 50 Q 30 10 52 6" stroke="#1F3A5F" stroke-width="2" fill="none"/><path d="M44 2 L54 6 L46 14" stroke="#1F3A5F" stroke-width="2" fill="none"/></svg></div>`;
    if (S.hintLevel >= 3) out += `<div class="margin-hint l3 write" style="left:0;top:776px;--dur:800ms">${h[2].text}
        <svg width="150" height="120" style="left:430px;top:-40px"><ellipse cx="75" cy="60" rx="68" ry="46" stroke="#1F3A5F" stroke-width="2.2" fill="none" transform="rotate(-8 75 60)"/></svg></div>`;
    return out;
  }

  function page1983() {
    return `
      <button class="paste oldsheet" data-act="read" data-item="note1983" style="left:46px;top:70px;--rot:rotate(-1.6deg);transform:var(--rot)">
        <div class="of-head"><span>设备记录</span><small>LT01　一九八三年五月</small></div>
        <div class="of-body">
          <p>主轴箱 听音 定点</p>
          <p>左端 L1：样冲眼，刷漆圈</p>
          <p class="big">咔哒在左，是轴承；</p>
          <p class="big">在右，是齿轮。</p>
          <p>声尖、脆，像表针。</p>
        </div>
        <div class="of-sig">周铁生</div>
        ${S.ev.E2_3 ? '<span class="key-icon"><i></i><i></i><i></i></span>' : '<i class="newmark"></i>'}
      </button>
      <div class="tape" style="left:20px;top:84px;transform:rotate(-40deg)"></div>
      <div class="tape" style="left:540px;top:60px;transform:rotate(32deg)"></div>
      <div class="tape" style="left:528px;top:664px;transform:rotate(-30deg)"></div>
      <button class="corner-flip" data-act="flipPrev"><span>A 翻回</span></button>
      <div class="pg-foot"><span>— 3 —</span><span>贴页 3</span></div>`;
  }
  function pageSketch() {
    return `
      <div class="pg-head"><h2>测点</h2><span>贴页（剪贴）</span></div>
      <div class="paste sketchsheet" style="left:60px;top:140px;transform:rotate(1.2deg)">
        <h4>LT01 主轴箱　测点示意　一九八三</h4>${SV.sectionSketch}
      </div>
      <div class="tape" style="left:40px;top:150px;transform:rotate(-36deg)"></div>
      <div class="paste printout" style="left:100px;top:520px;transform:rotate(-.8deg)">
        <h4>LT01 主轴箱振动复测（打印单）</h4>
        <table>
          <tr><td>测点</td><td>L1 主轴箱左端 / R1 右端</td></tr>
          <tr><td>复测</td><td class="mono">2026　林小满</td></tr>
          <tr><td>本机结构共振带</td><td class="mono">约 2–4 kHz</td></tr>
          <tr><td>说明</td><td>本机实测值，不作通用规律</td></tr>
        </table>
      </div>
      <div class="hand-lin" style="position:absolute;left:150px;top:770px;font-size:22px;transform:rotate(-2deg)">老周记的是位置和声音，频带是今年复测的。</div>
      <div class="pg-foot"><span>— 4 —</span><span>贴页</span></div>`;
  }

  /* 交接状态板（UI-DS §6；灯亮 = 旧方式承担，熄 = 衡接手） */
  const lampNames = ['测点', '诊断', '联锁', '天车'];
  function renderHandover(el) {
    el.innerHTML = `<div class="hv-plate"><b>交接状态板</b><small>HANDOVER · 2025</small></div>
      ${lampNames.map((n, i) => `<div class="hv-row"><span class="lamp ${S.lamps[i] ? '' : 'off'}" data-lamp="${i}"><i></i></span><span class="hv-tape">${i + 1}　${n}</span></div>`).join('')}
      <div class="hv-foot">亮：旧方式　熄：衡</div>`;
  }

  /* 证据阅读视图 */
  function openReader(item) {
    act();
    let html = '', cap = '';
    if (item === 'photo') {
      html = `<div class="paste sleeve" style="position:relative;transform:scale(2.1) rotate(-2deg)"><span class="clip"></span><span class="photo"><canvas data-ht="workPhoto" width="300" height="220"></canvas></span><span class="cap">老周　一九八三</span></div>`;
      cap = '<b>夹在工牌套中的工作照</b>老周左手持听针，伸向主轴箱左端。';
    } else if (item === 'note1983') {
      html = `<div class="paste oldsheet" style="position:relative;transform:scale(.88) rotate(-1deg)">${$('#pageL .oldsheet') ? $('#pageL .oldsheet').innerHTML : ''}</div>`;
      cap = S.ev.E2_3 ? '<b>贴页 3</b>一九八三年设备记录（已记入）' : '<b>贴页 3 已记入</b>“咔哒在左，是轴承；在右，是齿轮。”';
    }
    $('#readerRoot').innerHTML = `<div class="reader" data-act="closeOv">${html}<div class="reader-cap">${cap}</div></div>`;
    hydrate($('#readerRoot'));
    overlay('reader');
    A.play('flip', .5);
    if (item === 'note1983' && !S.ev.E2_3) setTimeout(() => acquire('E2_3', $('#readerRoot .oldsheet')), 700);
  }

  /* 证据飞入交班本（UI-DS §5.2：D-Slow，落下 2px 回弹） */
  async function acquire(id, fromEl) {
    if (S.ev[id]) return;
    S.ev[id] = true; act(); save();
    const e = D.evidence[id];
    const fr = fromEl ? rectOf(fromEl) : { x: 900, y: 500, w: 10, h: 10 };
    const target = S.screen === 'lathe' ? $('#lcTabs') : ($('.ev-row') || $('#ledger'));
    const tr = rectOf(target);
    const slip = document.createElement('div'); slip.className = 'fly paper';
    slip.style.cssText = 'width:200px;padding:8px 12px;font:400 16px/22px var(--F-Serif);color:#1a1a1a;left:0;top:0';
    slip.innerHTML = `<b style="font-family:var(--F-Hand-Jian);font-weight:400;font-size:22px;color:#1F3A5F">${e.paper}</b>　${e.short}`;
    $('#flyLayer').appendChild(slip);
    const sx = fr.x + fr.w / 2 - 100, sy = fr.y + fr.h / 2 - 20, tx = tr.x + tr.w / 2 - 100, ty = tr.y + tr.h / 2 - 20;
    await slip.animate([
      { transform: `translate(${sx}px,${sy}px) scale(1) rotate(0deg)` },
      { transform: `translate(${(sx + tx) / 2}px,${Math.min(sy, ty) - 120}px) scale(.9) rotate(-8deg)`, offset: .5 },
      { transform: `translate(${tx}px,${ty + 2}px) scale(.62) rotate(-3deg)`, offset: .9 },
      { transform: `translate(${tx}px,${ty}px) scale(.62) rotate(-3deg)` },
    ], { duration: 480, easing: 'cubic-bezier(.215,.61,.355,1)', fill: 'forwards' }).finished;
    slip.remove();
    A.play('flip', .4);
    log('MON', `记录：${e.name}。`);
    if (S.ev.E2_1 && S.ev.E2_2) logOnce('learn1', 'DIAG', '齿轮磨损判定与 R1 边频不符。冲击来源待定。', 'warn');
    if (S.ev.E2_1 && S.ev.E2_2 && S.ev.E2_3) logOnce('learn2', 'RULE', '证据齐备。可建立规则草案。');
    if (S.screen === 'desk' && S.ledgerOpen) renderSpread();
    if (S.screen === 'lathe') renderLedgerCorner(id);
    if (S.ev.E2_1 && S.ev.E2_2 && S.ev.E2_3) caption('三份贴页都齐了。回交班本，打开三联板。', '', 3200);
  }

  /* =====================================================================
     4 听音诊断
     ===================================================================== */
  const PT = { L1: { x: 180, y: 346, rot: 32 }, R1: { x: 506, y: 440, rot: 22 } };
  let latheBuilt = false, raf = 0, lastK = -1, drag = null;
  function enterLathe() {
    if (!latheBuilt) {
      $('#latheArt').innerHTML = SV.lathe;
      $('#probe').innerHTML = SV.toolRod;
      Object.assign($('#hotL1').style, { left: PT.L1.x + 'px', top: PT.L1.y + 'px' });
      Object.assign($('#hotR1').style, { left: PT.R1.x + 'px', top: PT.R1.y + 'px' });
      Object.assign($('#tagL1').style, { left: '52px', top: '186px' });
      Object.assign($('#tagR1').style, { left: '420px', top: '244px' });
      $('#leadL1').setAttribute('d', 'M150 242 L176 318');
      $('#leadR1').setAttribute('d', 'M500 300 L506 410');
      latheBuilt = true;
    }
    renderTablet(); renderLedgerCorner();
    updatePoint();
    logOnce('enterLathe', 'DIAG', '通用模型维持判定：变速齿轮磨损。', 'warn');
    if (!S.tool) setTimeout(() => S.screen === 'lathe' && !S.tool && caption('先从工具箱取听针（按 1），贴在 L1 或 R1 上听。', '', 3600), 500);
    cancelAnimationFrame(raf); raf = requestAnimationFrame(tick);
  }
  function renderLedgerCorner(newId) {
    const map = [['E2_3', '贴页3', -4], ['E2_1', '贴页4', 3], ['E2_2', '贴页5', -2]];
    $('#lcTabs').innerHTML = map.filter(([k]) => S.ev[k]).map(([k, n, r]) => `<i class="${k === newId ? 'new' : ''}" style="--r:${r}deg">${n}</i>`).join('');
    $('#lcHint').textContent = S.hintLevel && !S.puzzleDone ? '页边有批注' : '';
  }
  function renderTablet() {
    const p = S.point;
    const yt = [8, 6, 4, 2, 0].map(k => `<span style="top:${(1 - k / 8) * 100}%">${k}</span>`).join('');
    const xt = [0, 1, 2, 3, 4, 5, 6].map(k => `<span style="left:${k / 6 * 100}%">${k}</span>`).join('');
    const bandTop = (1 - 4000 / 8000) * SPEC_H, bandH = 2000 / 8000 * SPEC_H, gmfY = (1 - 620 / 8000) * SPEC_H;
    const ticks = D.spec.impacts.map((t, i) => `<i data-k="${i}" style="left:${t / 6 * 100}%"></i>`).join('');
    $('#tablet').innerHTML = `
      <div class="t-screen sys">
        <div class="t-head"><b>衡 · DIAG</b><span>LT01 主轴箱 · 听音诊断</span><span id="tClock">${clock()}</span></div>
        <div class="t-row"><span class="lbl">测点</span>
          <button class="sys-btn ${p === 'L1' ? 'on' : ''}" data-act="point" data-pt="L1">L1 左端</button>
          <button class="sys-btn ${p === 'R1' ? 'on' : ''}" data-act="point" data-pt="R1">R1 右端</button>
          <button class="sys-btn" data-act="playPause" ${p ? '' : 'disabled'}>${A.playing() ? '❚❚ 暂停' : '▶ 播放'}</button>
          <span class="src">${p ? `音频占位 ${p}_normal.wav · 循环 6s` : '等待听针'}</span></div>
        <div class="spec-box ${p === 'R1' ? 'r1' : ''}" id="specBox">
          <span class="yunit">kHz</span><div class="yax">${yt}</div>
          <canvas id="specBase" width="760" height="340"></canvas><canvas id="specFx" width="760" height="340"></canvas>
          ${p === 'L1' ? `<div class="band" style="top:${bandTop}px;height:${bandH}px"><em>本机共振带（2026 复测）</em></div>` : ''}
          ${p ? `<div class="gmf" style="top:${gmfY}px"><em>GMF 620Hz${p === 'R1' ? ' · 齿轮啮合' : ''}</em></div>` : ''}
          <div class="selbox" id="selBox"></div>
          ${p ? (p === 'L1' && !S.boxOk ? '' : '') : '<div class="spec-hint">用听针贴住测点后显示时频图<br>横轴 时间（s）　纵轴 频率（kHz）　亮度 = 能量</div>'}
          <div class="xax">${xt}</div>
        </div>
        <div class="clickstrip" id="clickStrip"><span class="cs-lbl">冲击</span>${p === 'L1' ? ticks : ''}<span class="cs-word" id="csWord">咔哒</span></div>
        <div class="t-row" style="margin-top:12px"><span class="lbl">标记</span>
          <span style="color:var(--C-Sys-Dim)">${p === 'L1' ? (S.boxOk ? `已标记 ${S.boxOk.n} 次周期冲击 · 间隔 0.29s` : '在时频图上拖动鼠标，框出竖向冲击条纹') : p === 'R1' ? 'R1 看下方啮合边频指示' : '—'}</span></div>
        <div class="sb-panel ${p === 'R1' ? '' : 'off'}">
          <div><div class="sb-title">R1 · GMF 局部放大 ±40Hz</div><div class="sb-plot">${sbPlot()}</div></div>
          <div>
            <div class="sb-title">边频指标 LT01_GB_GMF_SB_IDX</div>
            <div class="gauge"><div class="norm" style="width:${.35 / .6 * 100}%"></div><div class="bad" style="left:${.35 / .6 * 100}%"></div><div class="needle" style="left:${.18 / .6 * 100}%"></div></div>
            <div class="gauge-lbl"><span>0</span><span>阈值 0.35</span><span>0.60</span></div>
            <div class="gauge-lbl"><span>当前 0.18</span><span>${p === 'R1' ? '正常区' : ''}</span></div>
            <div class="sb-ask">齿轮状态
              <button class="sys-btn ${S.sbDone ? 'on' : ''}" data-act="sb" data-v="ok" ${p === 'R1' ? '' : 'disabled'}>正常</button>
              <button class="sys-btn" data-act="sb" data-v="bad" ${p === 'R1' ? '' : 'disabled'}>异常</button></div>
          </div>
        </div>
        <div class="t-log"><ul class="log" id="tabletLog"></ul></div>
      </div>`;
    if (p) SP.drawBase($('#specBase'), p);
    if (S.boxOk && p === 'L1') showBox(S.boxOk, true);
    bindSpec();
    refreshLogs();
  }
  function sbPlot() {
    // 中心啮合峰 + 两侧低幅边频（±15Hz）
    const W = 330, H = 120, x = f => (f + 40) / 80 * W, y = a => H - 14 - a * (H - 30);
    const peak = (f, a) => { let d = ''; for (let i = -6; i <= 6; i++) { const ff = f + i * .5, aa = a * Math.exp(-(i * i) / 6); d += `${i === -6 ? 'M' : 'L'}${x(ff).toFixed(1)} ${y(aa).toFixed(1)} `; } return d; };
    return `<svg width="${W}" height="${H}"><line x1="0" y1="${H - 14}" x2="${W}" y2="${H - 14}" stroke="rgba(230,240,245,.45)"/>
      <line x1="0" y1="${y(.35)}" x2="${W}" y2="${y(.35)}" stroke="rgba(242,194,0,.6)" stroke-dasharray="3 3"/>
      <path d="${peak(0, 1)}" stroke="#E6F0F5" fill="none" stroke-width="1.4"/>
      <path d="${peak(-15, .18)}" stroke="#E6F0F5" fill="none"/><path d="${peak(15, .16)}" stroke="#E6F0F5" fill="none"/>
      <text x="${x(0)}" y="${H - 1}" fill="rgba(230,240,245,.6)" font-size="14" font-family="JetBrainsMono" text-anchor="middle">620</text>
      <text x="${x(-15)}" y="${H - 1}" fill="rgba(230,240,245,.6)" font-size="14" font-family="JetBrainsMono" text-anchor="middle">-15</text>
      <text x="${x(15)}" y="${H - 1}" fill="rgba(230,240,245,.6)" font-size="14" font-family="JetBrainsMono" text-anchor="middle">+15</text>
      <text x="${W - 4}" y="${y(.35) - 4}" fill="rgba(242,194,0,.8)" font-size="14" font-family="JetBrainsMono" text-anchor="end">阈值</text></svg>`;
  }
  function selectPoint(pt) {
    act();
    if (S.tool !== 'rod') { caption('先从工具箱取听针（按 1）。', '', 2400); openToolbox(); return; }
    S.point = pt; A.init(); A.setVolume(S.vol); A.loop(pt); lastK = -1;
    if (pt === 'L1') logOnce('sampleL1', 'MON', 'L1 采样：后轴承成分清楚，齿轮成分低。');
    else logOnce('sampleR1', 'MON', 'R1 采样：以齿轮啮合为主。');
    updatePoint(); renderTablet(); save();
  }
  function updatePoint() {
    ['L1', 'R1'].forEach(p => { $('#hot' + p).classList.toggle('on', S.point === p); $('#tag' + p).classList.toggle('on', S.point === p); });
    const pr = $('#probe');
    if (S.point) { const P = PT[S.point]; Object.assign(pr.style, { left: P.x - 410 + 'px', top: P.y - 35 + 'px', transform: `rotate(${P.rot}deg)` }); pr.classList.add('on'); }
    else pr.classList.remove('on');
  }
  function tick() {
    if (S.screen !== 'lathe') return;
    const fx = $('#specFx');
    if (fx && S.point && A.playing()) {
      const pos = A.position();
      SP.drawFx(fx, S.point, pos, !!S.boxOk, S.boxOk);
      const k = SP.lastImpact(pos);
      if (S.point === 'L1' && k !== lastK) {
        lastK = k;
        const ti = $(`#clickStrip i[data-k="${k}"]`);
        $$('#clickStrip i.hit').forEach(i => i.classList.remove('hit'));
        if (ti) ti.classList.add('hit');
        const w = $('#csWord'); w.classList.add('on'); setTimeout(() => w.classList.remove('on'), 110);
        $('#hotL1').animate([{ transform: 'scale(1.15)' }, { transform: 'scale(1)' }], { duration: 160 });
        if (S.captions && !$('#caption').classList.contains('on')) caption('〔L1 听针：嗡……咔哒、咔哒、咔哒〕', '', 1600);
      }
      if (S.point === 'R1' && S.captions && !$('#caption').classList.contains('on')) caption('〔R1 听针：一片均匀的嗡〕', '', 1600);
    } else if (fx) fx.getContext('2d').clearRect(0, 0, 760, 340);
    const tc = $('#tClock'); if (tc) tc.textContent = clock();
    raf = requestAnimationFrame(tick);
  }
  const SPEC_W = 680, SPEC_H = 280; // 时频图显示尺寸（与 screens.css .spec-box 一致）
  function specLocal(e) {
    const r = $('#specBox').getBoundingClientRect();
    return { x: Math.max(0, Math.min(SPEC_W, (e.clientX - r.left) / scale)), y: Math.max(0, Math.min(SPEC_H, (e.clientY - r.top) / scale)) };
  }
  function bindSpec() {
    const box = $('#specBox'); if (!box) return;
    box.onpointerdown = e => {
      if (S.point !== 'L1') { if (S.point === 'R1') caption('R1 不用框选，看下方的啮合边频指示。', '', 2200); return; }
      if (S.boxOk) return;
      e.preventDefault(); try { box.setPointerCapture(e.pointerId); } catch (_) {}
      drag = specLocal(e); const sb = $('#selBox'); sb.style.display = 'block'; sb.classList.remove('ok');
      Object.assign(sb.style, { left: drag.x + 'px', top: drag.y + 'px', width: 0, height: 0 });
    };
  }
  // 用 pointer 事件，鼠标与触屏都能框选
  addEventListener('pointermove', e => {
    if (drag) {
      const p = specLocal(e), sb = $('#selBox');
      Object.assign(sb.style, { left: Math.min(p.x, drag.x) + 'px', top: Math.min(p.y, drag.y) + 'px', width: Math.abs(p.x - drag.x) + 'px', height: Math.abs(p.y - drag.y) + 'px' });
    }
    torchAt(e);
  });
  addEventListener('pointerdown', torchAt); // 触屏点一下也能移动手电光圈
  function torchAt(e) { if (S.screen === 'lathe' && S.tool === 'torch') { const p = stagePt(e); $('#torchMask').style.setProperty('--mx', p.x + 'px'); $('#torchMask').style.setProperty('--my', p.y + 'px'); } }
  addEventListener('pointercancel', () => { if (drag) { drag = null; $('#selBox').style.display = 'none'; } });
  addEventListener('pointerup', e => {
    if (!drag) return;
    const p = specLocal(e), a = drag; drag = null;
    if (Math.abs(p.x - a.x) < 6 || Math.abs(p.y - a.y) < 6) { $('#selBox').style.display = 'none'; return; }
    act();
    const kx = SP.W / SPEC_W, ky = SP.H / SPEC_H; // 画布内部像素 / 显示像素
    const x0 = Math.min(a.x, p.x) * kx, x1 = Math.max(a.x, p.x) * kx, y0 = Math.min(a.y, p.y) * ky, y1 = Math.max(a.y, p.y) * ky;
    const box = { t0: SP.x2t(x0), t1: SP.x2t(x1), f0: SP.y2f(y1), f1: SP.y2f(y0), css: { x: Math.min(a.x, p.x), y: Math.min(a.y, p.y), w: Math.abs(p.x - a.x), h: Math.abs(p.y - a.y) } };
    const j = SP.judge(box);
    log('DIAG', j.msg, j.ok ? 'ok' : '');
    if (j.ok) {
      S.boxOk = Object.assign(box, { n: j.n }); showBox(S.boxOk, true);
      acquire('E2_1', $('#selBox'));
      setTimeout(() => S.screen === 'lathe' && renderTablet(), 520);
    } else {
      S.devCount++;
      if (S.devCount >= 3 && S.hintLevel < 1) { S.hintLevel = 1; caption('交班本页边多了一行铅笔字。', '', 2400); renderLedgerCorner(); }
    }
  });
  function showBox(b, ok) { const sb = $('#selBox'); if (!sb) return; Object.assign(sb.style, { display: 'block', left: b.css.x + 'px', top: b.css.y + 'px', width: b.css.w + 'px', height: b.css.h + 'px' }); sb.classList.toggle('ok', ok); }
  async function sideband(v) {
    act();
    if (S.point !== 'R1') return;
    if (v === 'ok') {
      if (S.sbDone) return;
      S.sbDone = true; log('DIAG', 'R1 边频指标 0.18，低于阈值 0.35。齿轮状态正常。', 'ok');
      acquire('E2_2', $('.sb-panel .gauge'));
      setTimeout(() => S.screen === 'lathe' && renderTablet(), 520);
    } else {
      log('DIAG', '当前边频指标 0.18，低于阈值 0.35。');
      caption('〔A/B 对比：齿轮磨损仿真 → 当前 R1〕', '', 3200);
      A.stopLoop(); A.play('gearWear', .9); await wait(3000); if (S.point === 'R1' && S.screen === 'lathe') A.loop('R1');
    }
  }

  /* =====================================================================
     9 工具箱
     ===================================================================== */
  const TOOLS = [['rod', '听针', 'toolRod'], ['torch', '手电', 'toolTorch'], ['caliper', '卡尺', 'toolCaliper'], ['driver', '螺丝刀', 'toolDriver']];
  function toolAvail(k) { return S.screen === 'lathe' && (k === 'rod' || k === 'torch'); }
  function renderTools() {
    $('#tbDrawer').innerHTML = `<div class="tb-inner">${TOOLS.map(([k, n, svg], i) => `
      <button class="tslot ${S.tool === k ? 'sel' : ''} ${toolAvail(k) ? '' : 'na'}" data-act="tool" data-tool="${k}">
        <span class="no">${i + 1}</span>${SV[svg]}<span class="nm">${n}</span>${toolAvail(k) ? '' : '<span class="na-tip">此处用不上</span>'}</button>`).join('')}</div>`;
    const sl = $('#scr-lathe');
    sl.classList.toggle('torch', S.tool === 'torch' && S.screen === 'lathe');
    sl.style.cursor = S.tool === 'rod' ? cursorRod : S.tool === 'torch' ? cursorTorch : '';
  }
  const cur = svg => `url("data:image/svg+xml,${encodeURIComponent(svg)}") 4 28, crosshair`;
  const cursorRod = cur('<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32"><path d="M4 28 L22 10" stroke="#e8e5dc" stroke-width="2.5"/><path d="M20 12 L29 3" stroke="#8a5a2e" stroke-width="5" stroke-linecap="round"/><circle cx="4" cy="28" r="2" fill="#E8B45A"/></svg>');
  const cursorTorch = cur('<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32"><rect x="12" y="10" width="16" height="7" rx="2" fill="#9a978e" transform="rotate(-45 20 13)"/><path d="M4 28 L12 18 L16 22 Z" fill="#E8B45A" opacity=".85"/></svg>');
  function openToolbox() { $('#toolbox').classList.add('open'); A.play('drawer', .7); }
  function closeToolbox() { $('#toolbox').classList.remove('open'); }
  function toggleToolbox() { if ($('#toolbox').classList.contains('open')) closeToolbox(); else openToolbox(); }
  function pickTool(k) {
    if (!toolAvail(k)) { caption(S.screen === 'desk' ? '交班桌上用不上工具。去 LT01 再拿。' : '这里用不上。', '', 2000); return; }
    act(); S.tool = S.tool === k ? null : k; renderTools();
    A.play('tick', .5);
    if (S.tool === 'rod') caption('听针：点 L1 或 R1，贴上去听。', '', 2600);
    if (S.tool === 'torch') caption('手电：照一照主轴箱左端。', '', 2400);
    setTimeout(closeToolbox, 360);
  }

  /* =====================================================================
     3 三联板
     ===================================================================== */
  function optLocked(o) { return o.needs.some(n => !S.ev[n]); }
  function renderTriad() {
    const evs = [['E2_3', '贴页 3　1983 记录'], ['E2_1', '贴页 4　L1 时频图'], ['E2_2', '贴页 5　R1 边频']];
    const filled = D.triad.every(c => S.pick[c.key]);
    $('#triadRoot').innerHTML = `
      <div class="triad paper">
        <button class="triad-close" data-act="closeTriad">收 起</button>
        <div class="triad-head"><div><h2 style="display:inline">三联对应单</h2><span class="sub">点检第二项　LT01 主轴异响</span></div>
          <div class="triad-ev">贴页　${evs.map(([k, n]) => `<span class="evi ${S.ev[k] ? 'got' : ''}">${S.ev[k] ? n : n + '（未取）'}</span>`).join('')}</div></div>
        <div class="triad-body">
          ${D.triad.map(c => {
            const sel = c.options.find(o => o.id === S.pick[c.key]);
            const anyAvail = c.options.some(o => !optLocked(o));
            const cf = S.conflictCols.includes(c.key);
            const ok = S.rules.ZHOU_CLICK && S.rules.ZHOU_CLICK !== null;
            return `<div class="tcol" data-col="${c.key}">
              <div class="tcol-head"><b>${c.no}</b><span>${c.title}</span></div>
              <div class="tslot2 ${sel ? 'filled' : anyAvail ? 'avail' : ''} ${cf ? 'conflict' : ''} ${ok ? 'ok' : ''}" id="slot-${c.key}">
                ${sel ? `<span class="fill">${sel.text}</span>` : `<span class="empty">${anyAvail ? '待　填' : '空'}</span>`}
                ${cf ? '<span class="xmark">规则冲突</span>' : ''}
                <span class="pass" data-stamp="合格|三号车间 · 点检"></span>
              </div>
              <div class="opts">${c.options.map(o => {
                const lk = optLocked(o);
                const ref = o.needs.map(n => D.evidence[n].paper).join(' + ');
                return `<button class="opt ${lk ? 'locked' : ''} ${S.pick[c.key] === o.id ? 'sel' : ''}" data-act="${lk ? 'lockedOpt' : 'pick'}" data-col="${c.key}" data-opt="${o.id}">
                  ${o.text}${ref ? `<span class="evref">${ref}</span>` : ''}${lk ? '<span class="lk">待取证</span>' : ''}</button>`;
              }).join('')}</div></div>`;
          }).join('')}
          <div class="tcol-stamp">
            <button class="confirm" id="confirmBtn" data-act="confirm" ${filled && !S.rules.ZHOU_CLICK ? '' : 'disabled'}><span class="handle"></span><span class="base"></span><span class="face">确认</span></button>
            <div class="stamp-note">${filled ? '三格已满<br>盖章确认' : '三格填满后<br>才能盖章'}</div>
          </div>
        </div>
      </div>
      <div class="triad-sys sys"><ul class="log" id="triadLog"></ul></div>`;
    hydrate($('#triadRoot'));
    refreshLogs();
  }
  function openTriad() { act(); renderTriad(); overlay('triad'); A.play('flip', .6); }
  function pick(col, id) {
    act(); S.pick[col] = S.pick[col] === id ? null : id;
    S.conflictCols = S.conflictCols.filter(c => c !== col);
    A.play('tick', .4); renderTriad(); save();
  }
  async function confirmTriad() {
    if (busy) return;
    const filled = D.triad.every(c => S.pick[c.key]); if (!filled) return;
    busy = true; act();
    const btn = $('#confirmBtn'); btn.classList.add('press'); A.play('stamp');
    await wait(220); btn.classList.remove('press');
    const wrong = D.triad.filter(c => !c.options.find(o => o.id === S.pick[c.key]).ok);
    if (!wrong.length) {
      S.rules.ZHOU_CLICK = 'DRAFT';
      for (const c of D.triad) { $('#slot-' + c.key).classList.add('ok'); A.play('stamp', .5); await wait(160); }
      log('RULE', '三联一致。生成规则草案 ZHOU_CLICK。');
      await wait(900);
      busy = false; overlay('triad', false); generateRule();
      return;
    }
    S.conflictCols = wrong.map(c => c.key);
    renderTriad();
    S.conflictCols.forEach(k => $('#slot-' + k).classList.add('shake'));
    const fb = wrong[0].options.find(o => o.id === S.pick[wrong[0].key]).fb;
    if (!/规则冲突/.test(fb.log)) log('RULE', '规则冲突。', 'conflict');
    const [m, ...rest] = fb.log.split(' > ');
    log(m, rest.join(' > '), /规则冲突/.test(fb.log) ? 'conflict' : 'warn');
    if (fb.sim) { A.play(fb.sim, .9); caption(fb.cap, '', 3000); }
    else A.play('beep', .6);
    busy = false; save();
  }

  /* =====================================================================
     5 经验规则卡（记住与告别同帧）
     ===================================================================== */
  function rcFields(r, mode, status, opts = {}) {
    const st = {
      paper: { DRAFT: '<span class="st-pencil">草拟</span>', PENDING: '<span class="st-pending">待验证</span>', CONFIRMED: '<span class="st-pending" style="border-color:var(--C-Green);color:var(--C-Green)">已确认</span>', UNNAMED: '<span class="st-pencil">待命名</span>' },
      scr: { DRAFT: '<span class="st-sys">DRAFT</span>', PENDING: '<span class="st-sys pending"><i class="dot"></i>PENDING</span>', CONFIRMED: '<span class="st-sys confirmed">CONFIRMED</span>', UNNAMED: '<span class="st-sys unnamed">UNNAMED</span>' },
    };
    const rows = [
      ['Rule ID', '规则编号', `<span class="v id" ${opts.idAttr || ''}>${opts.idHTML ?? r.id}</span>`],
      ['Source', '来源', `<span class="v src" ${opts.srcAttr || ''}>${r.source}</span>`],
      ['Measurement Point', '测点', `<span class="v">${r.point}</span>`],
      ['Feature', '特征', `<span class="v">${r.feature}</span>`],
      ['Binds', '绑定变量', `<span class="v binds">${r.binds.join('<br>')}</span>`],
      ['Status', '状态', `<span class="v" data-status>${st[mode][status]}</span>`],
    ];
    return rows.map(([en, zh, v], i) => `<div class="rf" ${opts.hide && i > 0 ? 'style="opacity:0" data-reveal' : ''}><span class="k"><span class="caps">${en}</span><span class="label">${zh}</span></span>${v}</div>`).join('');
  }
  async function generateRule() {
    busy = true;
    const r = D.rules.ZHOU_CLICK;
    $('#ruleRoot').innerHTML = `
      <div class="rc-wrap">
        <div class="rc-col"><div class="rc-cap">经验规则卡 · 交班本</div>
          <div class="rcard paper" id="rcPaper">${rcFields(r, 'paper', 'DRAFT', { srcAttr: 'id="rcSrc"' })}</div></div>
        <div class="rc-col"><div class="rc-cap sysc">RULE CARD · 衡 / 知识库</div>
          <div class="rcard scr sys" id="rcScr">${rcFields(r, 'scr', 'DRAFT', { idHTML: '<span id="rcId"></span><span class="blink">_</span>', hide: true })}</div></div>
      </div>
      <div class="rc-bottom">
        <div><div class="hv-inset" id="hvInset">${lampNames.map((n, i) => `<div class="cell"><span class="lamp ${S.lamps[i] ? '' : 'off'}" data-lamp="${i}"><i></i></span><span class="hv-tape">${i + 1} ${n}</span></div>`).join('')}</div>
          <div class="hv-inset-cap">控制间 · 交接状态板</div></div>
        <div class="rc-sysline sys"><ul class="log" id="ruleLog"></ul></div>
      </div>
      <button class="rc-next" id="rcNext" data-act="closeRule">收起 ▸</button>`;
    overlay('rule'); refreshLogs();
    A.play('flip', .6);
    await wait(500);
    // 0–600ms 手写 Source 拆成点阵；600–1400ms 点阵重组为等宽 Rule ID（逐字 80ms）
    const src = rectOf($('#rcSrc')), dst = rectOf($('#rcId').parentElement);
    $('#rcSrc').animate([{ opacity: 1 }, { opacity: .35 }, { opacity: 1 }], { duration: 1600 });
    dots(src, dst);
    await wait(600);
    const id = r.id; const idEl = $('#rcId');
    for (let i = 1; i <= id.length; i++) { idEl.textContent = id.slice(0, i); A.play('tick', .25); await wait(80); }
    const cur = $('#rcScr .blink'); if (cur) cur.remove();
    $$('#rcScr [data-reveal]').forEach((el, i) => setTimeout(() => el.style.opacity = 1, i * 40));
    // 待验证 0.8s（仿真验证）
    S.rules.ZHOU_CLICK = 'PENDING';
    setStatus('PENDING'); log('TWIN', '仿真验证 ZHOU_CLICK：L1 冲击报警 / R1 边频。');
    await wait(800);
    // 同一帧：盖章“已确认”+ CONFIRMED + 交接状态灯熄灭
    S.rules.ZHOU_CLICK = 'CONFIRMED'; S.lamps[1] = false; S.clickAt = clock();
    setStatus('CONFIRMED');
    const stamp = document.createElement('span'); stamp.className = 'st-stamp'; stamp.innerHTML = SV.stampPass('合格', '经验规则 · 已确认'); $('#rcPaper').appendChild(stamp);
    $('#rcScr').classList.add('confirmed');
    A.play('stamp'); A.play('relay', .8);
    lampOff($('#hvInset [data-lamp="1"]')); lampOff($('#handoverDesk [data-lamp="1"]'));
    log('RULE', '经验规则 ZHOU_CLICK 已确认。来源 周铁生。', 'ok');
    log('DIAG', '更正：主轴后轴承早期缺陷，列入计划检修。');
    log('DIAG', '撤回“停机更换齿轮”建议。');
    log('SYS', '交接 2/4。');
    save();
    await wait(1000);
    busy = false;
    promote();
  }
  function setStatus(st) {
    const p = $('#rcPaper [data-status]'), s = $('#rcScr [data-status]');
    const P = { PENDING: '<span class="st-pending">待验证</span>', CONFIRMED: '<span class="st-pending" style="border-color:var(--C-Green);color:var(--C-Green)">已确认</span>' };
    const Q = { PENDING: '<span class="st-sys pending"><i class="dot"></i>PENDING</span>', CONFIRMED: '<span class="st-sys confirmed">CONFIRMED</span>' };
    p.innerHTML = P[st]; s.innerHTML = Q[st];
  }
  function lampOff(el) { if (!el) return; el.classList.add('dying'); setTimeout(() => { el.classList.remove('dying'); el.classList.add('off'); }, 600); }
  function dots(src, dst) {
    const cv = $('#dots'), g = cv.getContext('2d');
    const N = 260, P = [];
    for (let i = 0; i < N; i++) {
      const sx = src.x + Math.random() * src.w, sy = src.y + Math.random() * src.h;
      P.push({ sx, sy, mx: sx + (Math.random() - .3) * 120, my: sy - 30 - Math.random() * 80, tx: dst.x + Math.random() * Math.min(dst.w, 260), ty: dst.y + 8 + Math.random() * (dst.h - 16) });
    }
    const t0 = performance.now();
    const step = now => {
      const t = now - t0; g.clearRect(0, 0, 1920, 1080);
      if (t > 1600) return;
      P.forEach(p => {
        let x, y, a = 1, col;
        if (t < 600) { const k = 1 - Math.pow(1 - t / 600, 3); x = p.sx + (p.mx - p.sx) * k; y = p.sy + (p.my - p.sy) * k; col = '31,58,95'; }
        else { const k = Math.min(1, (t - 600) / 800); x = p.mx + (p.tx - p.mx) * k; y = p.my + (p.ty - p.my) * k; col = '230,240,245'; a = t > 1400 ? 1 - (t - 1400) / 200 : 1; }
        g.fillStyle = `rgba(${col},${a})`; g.fillRect(x, y, 2, 2);
      });
      requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  }

  /* =====================================================================
     8 等级提升：技术等级考核合格通知
     ===================================================================== */
  async function promote() {
    const from = D.levels[S.level], to = D.levels[S.level + 1];
    S.level += 1; save();
    $('#promoRoot').innerHTML = `
      <div class="promo paper print">
        <div class="org">衡川第三机械厂　三号车间</div>
        <h2>技术等级考核合格通知</h2>
        <div class="no">No. HC3-HENG-000${S.level}</div>
        <div class="rule"></div>
        <dl style="margin:0">
          <div class="pf"><dt>持证者</dt><dd><span class="mono" style="color:var(--C-Ink);font-weight:700">衡</span></dd></div>
          <div class="pf"><dt>工　种</dt><dd>设备诊断（学艺）</dd></div>
          <div class="pf"><dt>等　级</dt><dd>${from}<span class="arrow">→</span><b style="font-size:26px">${to}</b></dd></div>
          <div class="pf"><dt>依　据</dt><dd><span class="mono">ZHOU_CLICK</span></dd></div>
          <div class="pf"><dt>评　定</dt><dd><span class="hand-lin" style="font-size:30px">林小满</span></dd></div>
        </dl>
        <div class="ladder">${D.levels.map((l, i) => `<div class="${i < S.level ? 'done' : i === S.level ? 'now' : 'later'}"><small>${i}</small>${l}</div>`).join('')}</div>
        <div class="verdict">技术等级考核合格</div>
        <div class="sig"><span>三号车间　技术考核组</span><span class="hand-lin" style="font-size:24px">${cnTime(S.clickAt || clock())}</span></div>
        <span class="bigstamp" id="bigStamp"></span>
        <div class="promo-close" id="promoClose">点击任意处收起</div>
      </div>
      <div class="promo-sys sys" id="promoSys"><ul class="log"><li><span class="t">[${clock()}]</span> <span class="m">SYS  &gt;</span> 等级：${from} → ${to}</li></ul></div>`;
    $('#bigStamp').innerHTML = SV.stampPass('合格', '技术等级考核');
    overlay('promo');
    A.play('flip', .7);
    await wait(520);
    $('#bigStamp').classList.add('on'); A.play('stamp');
    $('#promoSys').classList.add('on');
    log('SYS', `等级：${from} → ${to}。`);
    await wait(2500);
    $('#promoClose').classList.add('on'); promoReady = true;
  }
  let promoReady = false;
  function closePromo() {
    if (!promoReady) return; promoReady = false;
    closeOverlays(); S.puzzleDone = true; S.hintLevel = 0; S.spread = 0; S.ledgerOpen = true; save();
    go('desk');
    setTimeout(() => walkie('嗯。', 2200), 700);
    setTimeout(() => caption('点检第二项完成。交接状态板第二盏灯已熄。', '', 3200), 3200);
  }

  /* 技术等级证（交班桌上的小本） */
  function openCert() {
    act();
    const rows = D.levels.map((l, i) => {
      const done = i <= S.level;
      const basis = i === 1 ? 'ZHOU_LEFT' : i === 2 && S.rules.ZHOU_CLICK === 'CONFIRMED' ? 'ZHOU_CLICK' : '';
      const t = i === 1 ? '16:21' : i === 2 && basis ? (S.clickAt || '17:41') : '';
      return `<tr class="${done ? '' : 'later'}"><td class="lv">${l}</td><td>${i === 0 ? '<span style="font-size:15px">二〇二五年 入库</span>' : t ? `<span class="mono">${t}</span>` : ''}</td><td>${basis ? `<span class="mono">${basis}</span>` : ''}</td><td>${i > 0 && basis ? '<span class="hand-lin" style="font-size:26px">林小满</span>' : ''}</td></tr>`;
    }).join('');
    $('#certRoot').innerHTML = `
      <div class="certbook">
        <div class="cp l paper"><h3>技术等级证</h3><div class="org">衡川第三机械厂　三号车间</div>
          <div class="photo-box">照　片<br><small style="font-size:14px">（此栏不适用）</small></div>
          <dl style="margin:0 40px">
            <div class="pf"><dt>持证者</dt><dd class="mono" style="font-size:24px;font-weight:700">衡</dd></div>
            <div class="pf"><dt>工　种</dt><dd>设备诊断（学艺）</dd></div>
            <div class="pf"><dt>证　号</dt><dd class="mono" style="font-size:20px">HC3-HENG-000${S.level}</dd></div>
            <div class="pf"><dt>现等级</dt><dd><b style="font-size:26px">${D.levels[S.level]}</b></dd></div>
          </dl></div>
        <div class="cp r paper"><h3 style="font-size:32px">等级记录</h3>
          <table class="lt"><tr><th>等级</th><th>考核</th><th>依据</th><th>评定</th></tr>${rows}</table>
          <p style="font:400 15px/24px var(--F-Serif);color:#6b6455;margin-top:16px">每确认一条经验规则，晋升一级。本切片上限四级。</p></div>
      </div>`;
    overlay('cert'); A.play('flip', .6);
  }

  /* =====================================================================
     7 人物档案
     ===================================================================== */
  let archSel = 'zhou';
  function openArchive(k = archSel) {
    act(); archSel = k;
    const p = D.people.find(x => x.key === k), locked = p.state === '未解锁';
    const ruleState = ([id, st]) => {
      if (locked) return '<span class="no lockd">——</span>';
      const ok = st === true || (st === 'dyn' && S.rules[id] === 'CONFIRMED');
      return `<span class="${ok ? 'ok' : 'no'}">${id}</span>`;
    };
    $('#archiveRoot').innerHTML = `
      <div class="arch">
        <div class="arch-folder"></div>
        <div class="arch-tabs">${D.people.map(x => `<button class="arch-tab ${x.key === k ? 'on' : ''} ${x.state === '未解锁' ? 'locked' : ''}" data-act="archTab" data-k="${x.key}"><b>${x.name}</b><small>${x.state === '未解锁' ? '仅姓名' : x.rows[3][1]}</small><span class="st">${x.state}</span></button>`).join('')}</div>
        <div class="arch-note">职工档案卡（摘录复印件）。<br>只录工作经历与技术记录。</div>
        <div class="acard paper" key="${k}">
          <div class="ah"><h3>职工档案卡</h3><span>三号车间　摘录</span></div>
          <div class="grid">
            <dl style="margin:0">
              <div class="af"><dt>姓　名</dt><dd class="name">${p.name}</dd></div>
              ${p.rows.map(([a, b]) => `<div class="af"><dt>${a}</dt>${locked ? '<dd class="lockd"></dd>' : `<dd class="${b ? '' : 'none'}">${b || ''}</dd>`}</div>`).join('')}
            </dl>
            <div>${p.key === 'sun' ? '<div class="aphoto empty">照　片</div>' : `<div class="aphoto"><i class="corner c-tl"></i><i class="corner c-br"></i><canvas data-ht="${p.key === 'zhou' ? 'portraitZhou' : 'portraitJian'}" data-cell="3" width="196" height="266"></canvas></div>`}</div>
          </div>
          <div class="asec"><h4>主要记录</h4>${p.records.map(([y, t]) => y ? `<div class="arec"><span>${y}</span><span>${t}</span></div>` : '<div class="arec locked"><span></span><span></span></div>').join('')}</div>
          <div class="asec"><h4>经验规则</h4><div class="arules">${p.rules.map(ruleState).join('')}</div></div>
          <div class="afoot">${locked ? '档案未调阅：完成相关点检后解锁' : p.note}</div>
          <div class="astate">${p.state}</div>
        </div>
      </div>`;
    hydrate($('#archiveRoot'));
    overlay('archive'); A.play('flip', .5);
  }

  /* =====================================================================
     6 衡 MES
     ===================================================================== */
  let mesTimer = 0, mesRule = 'ZHOU_CLICK';
  function renderMES() {
    const R = S.rules, conf = k => R[k] === 'CONFIRMED';
    const st = k => R[k] === 'CONFIRMED' ? '<span class="st-sys confirmed">CONFIRMED</span>' : R[k] ? `<span class="st-sys">${R[k]}</span>` : '<span class="st-sys" style="opacity:.35">——</span>';
    const owner = ['周铁生', '周铁生', '周建国', '孙桂兰'];
    const rk = ['ZHOU_LEFT', 'ZHOU_CLICK', 'JIANGUO_GUARD', 'SUN_HALFBEAT'];
    const nOff = S.lamps.filter(l => !l).length;
    const mr = D.rules[mesRule];
    const conflicts = LOG.filter(l => l.k === 'conflict').length;
    $('#mesRoot').innerHTML = `
      <div class="mes">
        <div class="mes-top"><b>衡</b><span>MES / PdM　·　衡川第三机械厂 三号车间</span><span>中班　<span id="mesClock">${clock()}</span></span></div>
        <div class="mes-col">
          <div class="mes-sec"><h3><span>设备 ASSET · 三号车间</span><span>中班</span></h3>
            <div class="as-row"><span class="id">LT01</span><span>普通车床 · 主轴箱</span><span>${conf('ZHOU_CLICK') ? '运行 · 后轴承列入计划检修' : '<span class="a">运行 · 判定：变速齿轮磨损</span>'}</span></div>
            <div class="as-row"><span class="id">LT01</span><span>护罩 / 启动条件</span><span class="dimt">旧回路</span></div>
            <div class="as-row"><span class="id">CR01</span><span>天车</span><span class="dimt">待试吊</span></div></div>
          <div class="mes-sec mes-log"><h3><span>日志 LOG</span><span>${LOG.length} 条</span></h3><ul class="log" id="mesLog"></ul></div>
        </div>
        <div class="mes-col">
          <div class="mes-sec"><h3><span>知识库 RULE BASE</span><span>${rk.filter(conf).length}/4</span></h3>
            ${rk.map((k, i) => `<div class="mes-rule ${R[k] ? '' : 'none'}" data-act="mesRule" data-k="${k}"><b>${k}</b><small>${D.rules[k].type} · 来源 ${owner[i]}</small><span class="st">${st(k)}</span></div>`).join('')}</div>
          <div class="mes-sec"><h3><span>诊断 DIAG · LT01 主轴</span><span></span></h3>
            <div class="diag-t">
              <span class="tt">17:31</span><span class="${conf('ZHOU_CLICK') ? 'x' : 'a'}">通用模型：变速齿轮磨损（0.71）→ 停机换齿轮</span>
              <span class="tt">L1</span><span>${S.ev.E2_1 ? `周期冲击 ×${S.boxOk ? S.boxOk.n : '—'}，间隔 0.29s，本机共振带内` : '未标记'}</span>
              <span class="tt">R1</span><span>${S.ev.E2_2 ? '边频指标 0.18 &lt; 0.35，齿轮正常' : '未判断'}</span>
              <span class="tt">冲突</span><span>${conflicts ? `规则冲突 ×${conflicts}` : '无'}</span>
              <span class="tt">结论</span><span>${conf('ZHOU_CLICK') ? '主轴后轴承早期缺陷，列入计划检修' : '待经验规则'}</span>
            </div></div>
          ${R[mesRule] ? `<div class="mes-sec"><h3><span>RULE CARD</span><span>${mesRule}</span></h3><div class="mes-card"><div class="rcard scr ${conf(mesRule) ? 'confirmed' : ''}">${rcFields(mr, 'scr', R[mesRule])}</div></div></div>` : ''}
        </div>
        <div class="mes-col">
          <div class="mes-sec"><h3><span>交接 HANDOVER</span><span>${nOff}/4</span></h3>
            ${lampNames.map((n, i) => `<div class="ho-row"><span class="sq ${S.lamps[i] ? '' : 'on'}"></span><span>${i + 1} ${n}</span><span class="who">${S.lamps[i] ? (i === 2 ? '旧回路' : '人工') : '衡'}</span></div>`).join('')}</div>
          <div class="mes-sec"><h3><span>等级 LEVEL</span><span>${D.levels[S.level]}</span></h3>
            <div class="lv-bar">${D.levels.map((l, i) => `<i class="${i <= S.level ? 'on' : ''}"></i>`).join('')}</div>
            <div class="lv-names">${D.levels.map((l, i) => `<span class="${i === S.level ? 'on' : ''}">${l}</span>`).join('')}</div></div>
          <div class="mes-sec"><h3><span>点检 CHECKLIST</span><span></span></h3>
            ${['LT01 主轴后轴承测点', 'LT01 主轴异响', 'LT01 护罩 / 启动条件', 'CR01 天车试吊'].map((n, i) => `<div class="ho-row"><span class="sq ${!S.lamps[i] ? 'on' : ''}"></span><span>${n}</span><span class="who">${!S.lamps[i] ? 'DONE' : i === 1 ? 'OPEN' : '—'}</span></div>`).join('')}</div>
        </div>
        <div class="mes-foot"><button class="mes-back" data-act="desk">[Esc] 返回交班桌</button><span>衡不提示答案，只报冲突。</span></div>
      </div>`;
    refreshLogs();
    clearInterval(mesTimer);
    mesTimer = setInterval(() => { if (S.screen !== 'mes') return clearInterval(mesTimer); const c = $('#mesClock'); if (c) c.textContent = clock(); }, 1000);
  }

  /* =====================================================================
     10 结局签名
     ===================================================================== */
  let sigDrawn = false, ridVal = '';
  async function enterEnding() {
    sigDrawn = false; ridVal = '';
    const rk = ['ZHOU_LEFT', 'ZHOU_CLICK', 'JIANGUO_GUARD', 'SUN_HALFBEAT'];
    const unnamed = { id: '', source: '林小满，2026 年交接班记录', point: 'LT01 主轴尾端', feature: '（待描述）未登记微弱信号', binds: ['——'] };
    $('#endRoot').innerHTML = `
      <div class="end-bg"></div>
      <div class="ledger open end-ledger"><div class="ledger-cloth"></div><div class="pages">
        <div class="page left">
          <div class="pg-head"><h2>交班</h2><span>最后一页</span></div>
          <div class="notes" style="margin-top:20px">
            ${rk.map((k, i) => `<p class="hand-lin" style="font-size:26px">${i + 1}　${lampNames[i]}　<span class="mono" style="font-size:18px">${k}</span></p>`).join('')}
          </div>
          <div class="end-sig">
            <div class="ln"><span>交班人</span><span class="hand-jian big write" style="--dur:1600ms;--delay:600ms">周建国</span></div>
            <div class="t" style="text-align:right;margin-top:8px">二十三点五十分</div>
          </div>
          <div class="pg-foot"><span>— 末页 —</span><span>23:50</span></div>
        </div>
        <div class="page right">
          <div class="pg-head"><h2>接班</h2><span>接班人签名</span></div>
          <div class="end-sig" style="margin-top:196px">
            <div class="ln"><span>接班人</span><div class="sigpad" id="sigPad"><span class="ph" id="sigPh">在此签名（按住鼠标书写）</span><span class="auto" id="sigAuto"></span><canvas id="sigCanvas" width="896" height="300"></canvas></div></div>
          </div>
          <div class="sig-actions"><button data-act="sigAuto">代签（演示）</button><button data-act="sigClear">清除</button><button data-act="sigDone">确认签名</button></div>
          <div class="dual" id="dual"></div>
          <div class="pg-foot"><span>— 末页 —</span><span id="endClock">23:58</span></div>
        </div>
        <div class="gutter"></div></div></div>
      <div class="end-sys sys">
        <div class="end-log"><ul class="log" id="endLog"></ul></div>
        <div class="rcard scr" id="endCard">${rcFields(unnamed, 'scr', 'UNNAMED', { idHTML: '<input class="rid" id="ridInput" maxlength="20" placeholder="ID: ________" spellcheck="false">' })}</div>
      </div>
      <div class="end-black" id="endBlack"></div>
      <div class="end-final" id="endFinal">谨以此作献给所有把手艺交出去的人。</div>`;
    bindSig();
    const ri = $('#ridInput');
    ri.addEventListener('input', () => { ri.value = ri.value.toUpperCase().replace(/[^A-Z0-9_]/g, ''); ridVal = ri.value; });
    ri.addEventListener('keydown', e => { if (e.key === 'Enter' && ridVal) { ri.blur(); named(); } e.stopPropagation(); });
    setTimeout(() => caption('左边那个咔哒，你听见了吗？', '周建国', 3600), 400);
    const ul = $('#endLog'); ul.innerHTML = '';
    const seq = [
      ['23:50', 'SYS', '原班长签字确认。按交接单停机切换。', '', 900],
      ['23:51', 'SYS', '总闸断开。旧回路停止。', '', 1200],
      ['23:56', 'SYS', '衡启动。交接 4/4。', '', 900],
      ['23:56', 'RULE', 'ZHOU_LEFT', 'ok', 500], ['23:56', 'RULE', 'ZHOU_CLICK', 'ok', 500],
      ['23:56', 'RULE', 'JIANGUO_GUARD', 'ok', 500], ['23:56', 'RULE', 'SUN_HALFBEAT', 'ok', 900],
      ['23:57', 'MON', '检测到未登记微弱信号（主轴尾端）。', 'warn', 900],
      ['23:57', 'RULE', '无法按 ZHOU_CLICK 规则归类。申请命名。', '', 800],
      ['23:57', 'SYS', '等级：四级 → 待师傅评定。', '', 0],
    ];
    endSeq = seq.slice();
    await wait(2600);
    while (endSeq.length) {
      if (S.screen !== 'ending') return;
      const [t, m, c, k, d] = endSeq.shift();
      endPush(t, m, c, k);
      await wait(d + c.length * 20);
    }
  }
  // 结局日志：玩家提前签名 / 命名时，先把剩余的切换日志一次补齐，保证时间顺序
  let endSeq = [];
  function endPush(t, m, c, k = '', type = true) {
    const ul = $('#endLog'); const li = document.createElement('li'); if (k) li.className = k; li.innerHTML = liHTML({ t, m, c }); ul.appendChild(li);
    if (type) typewrite(li);
    while (ul.children.length > 7) ul.firstChild.remove();
  }
  function endFlush() { while (endSeq.length) { const [t, m, c, k] = endSeq.shift(); endPush(t, m, c, k, false); } }
  function bindSig() {
    const cv = $('#sigCanvas'), g = cv.getContext('2d'); let down = false, last = null;
    const pt = e => { const r = cv.getBoundingClientRect(); return { x: (e.clientX - r.left) / r.width * cv.width, y: (e.clientY - r.top) / r.height * cv.height }; };
    g.strokeStyle = '#1F3A5F'; g.lineWidth = 5; g.lineCap = 'round'; g.lineJoin = 'round';
    cv.onpointerdown = e => { e.preventDefault(); try { cv.setPointerCapture(e.pointerId); } catch (_) {} down = true; last = pt(e); $('#sigPh').style.display = 'none'; sigDrawn = true; };
    cv.onpointermove = e => { if (!down) return; const p = pt(e); g.beginPath(); g.moveTo(last.x, last.y); g.lineTo(p.x, p.y); g.stroke(); last = p; };
    addEventListener('pointerup', () => down = false); addEventListener('pointercancel', () => down = false);
  }
  function sigAuto() { const a = $('#sigAuto'); a.textContent = '林小满'; a.classList.remove('write'); void a.offsetWidth; a.style.setProperty('--dur', '1400ms'); a.classList.add('write'); $('#sigPh').style.display = 'none'; sigDrawn = true; }
  function sigClear() { const cv = $('#sigCanvas'); cv.getContext('2d').clearRect(0, 0, cv.width, cv.height); $('#sigAuto').textContent = ''; $('#sigPh').style.display = ''; sigDrawn = false; $('#dual').innerHTML = ''; }
  function sigDone() {
    if (!sigDrawn) { caption('先在接班人栏签名。', '', 2000); return; }
    $('#dual').innerHTML = `<span class="h">林小满</span><span class="m">LIN_XIAOMAN · 23:58</span>`;
    A.play('stamp', .5);
    endFlush(); endPush('23:58', 'SYS', '接班人 林小满 签名已记录。');
    if (!ridVal) setTimeout(() => { if (!ridVal) { caption('衡在等这条新规则的名字。', '', 2600); $('#ridInput').focus(); } }, 900);
    else finale();
  }
  function named() {
    const s = $('#endCard [data-status]'); s.innerHTML = '<span class="st-sys pending"><i class="dot"></i>PENDING</span>';
    endFlush(); endPush('23:58', 'RULE', `新规则命名：${ridVal}。待师傅评定。`);
    if ($('#dual').innerHTML) finale(); else caption('在接班人栏签下名字。', '', 2600);
  }
  async function finale() { await wait(2400); $('#endBlack').classList.add('on'); await wait(1200); $('#endFinal').classList.add('on'); }

  /* =====================================================================
     设置 / 暂停 / 演示目录
     ===================================================================== */
  function openSettings() {
    const o = (k, on, label) => `<button class="chkopt ${on ? 'on' : ''}" data-act="set" data-k="${k}"><i></i>${label}</button>`;
    $('#settingsRoot').innerHTML = `
      <div class="setform paper">
        <h2>设 置 单</h2>
        <div class="srow"><span>页边提示<small>三级批注 90 / 180 / 300 秒</small></span><div class="chkopts">${o('hintsOn', S.hintsOn, '开')}${o('hintsOff', !S.hintsOn, '关（显示“需要提示”）')}</div></div>
        <div class="srow"><span>提示节奏<small>演示加速 = 10 倍</small></span><div class="chkopts">${o('hintStd', !S.hintFast, '标准')}${o('hintFast', S.hintFast, '演示加速')}</div></div>
        <div class="srow"><span>字幕<small>含环境关键声音标注</small></span><div class="chkopts">${o('capOn', S.captions, '开')}${o('capOff', !S.captions, '关')}</div></div>
        <div class="srow"><span>音量<small>占位音频为程序合成</small></span><input type="range" min="0" max="1" step=".05" value="${S.vol}" id="volRange"></div>
        <div class="srow"><span>色觉辅助<small>正确 = 圆章，冲突 = 斜纹（默认）</small></span><div class="chkopts">${o('noop', true, '形状区分')}</div></div>
        <button class="set-back" data-act="closeOv">返 回</button>
      </div>`;
    $('#volRange').oninput = e => { S.vol = +e.target.value; A.setVolume(S.vol); save(); };
    overlay('settings');
  }
  function setOpt(k) {
    if (k === 'hintsOn') S.hintsOn = true; if (k === 'hintsOff') S.hintsOn = false;
    if (k === 'hintStd') S.hintFast = false; if (k === 'hintFast') S.hintFast = true;
    if (k === 'capOn') S.captions = true; if (k === 'capOff') { S.captions = false; $('#caption').classList.remove('on'); }
    save(); openSettings(); if (S.screen === 'desk' && S.ledgerOpen) renderSpread();
  }
  function openPause() {
    const j = (k, n, key = '') => `<button class="pbtn" data-act="jump" data-k="${k}">${n}${key ? `<small>${key}</small>` : ''}</button>`;
    $('#pauseRoot').innerHTML = `
      <div class="pause">
        <h2>暂 停</h2><p>非叙事界面。演示目录可直接跳到任一界面，便于截图；跳转会自动补齐所需状态。</p>
        <button class="pbtn" data-act="closeOv">继续<small>Esc</small></button>
        <button class="pbtn" data-act="settings">设置</button>
        <div class="grp">演示目录</div>
        ${j('title', '1　主菜单 · 打卡机')}${j('desk', '2　交班本主界面')}${j('desk1983', '2′ 交班本 · 一九八三年贴页')}
        ${j('triad', '3　三联板（证据齐）')}${j('triadWrong', '3′ 三联板 · 规则冲突')}${j('lathe', '4　听音诊断 · L1', 'Tab')}
        ${j('rule', '5　经验规则卡 ZHOU_CLICK 生成')}${j('mes', '6　衡 MES', 'M')}${j('archive', '7　人物档案')}
        ${j('promo', '8　等级提升通知')}${j('cert', '8′ 技术等级证')}${j('toolbox', '9　工具箱', 'T')}${j('ending', '10 结局签名')}
        <div class="grp">进度</div>
        <button class="pbtn" data-act="jump" data-k="reset">清除存档并回到打卡机</button>
      </div>`;
    overlay('pause');
  }
  function ensureGame() {
    if (!deskStart) deskStart = performance.now();
    S.ledgerOpen = true;
  }
  async function jump(k) {
    closeOverlays(); A.stopLoop(); busy = false;
    const allEv = () => { S.ev = { E2_1: true, E2_2: true, E2_3: true }; S.boxOk = S.boxOk || { t0: 1.4, t1: 3.3, f0: 2100, f1: 3900, n: 7, css: { x: 158, y: 86, w: 214, h: 68 } }; S.sbDone = true; };
    // 演示目录直达“谜题二已完成”：补齐一次真实通关会产生的状态与衡日志（误判 → 学习 → 规则确认）
    const solved = () => {
      allEv();
      if (S.rules.ZHOU_CLICK === 'CONFIRMED') return;
      const E = D.evidence;
      [['17:32', 'DIAG', '通用模型维持判定：变速齿轮磨损。', 'warn'], ['17:33', 'MON', 'L1 采样：后轴承成分清楚，齿轮成分低。'],
       ['17:34', 'DIAG', '已标记：周期冲击 ×7，间隔 0.29s。', 'ok'], ['17:34', 'MON', `记录：${E.E2_1.name}。`],
       ['17:35', 'MON', 'R1 采样：以齿轮啮合为主。'], ['17:36', 'DIAG', 'R1 边频指标 0.18，低于阈值 0.35。齿轮状态正常。', 'ok'],
       ['17:36', 'MON', `记录：${E.E2_2.name}。`], ['17:36', 'DIAG', '齿轮磨损判定与 R1 边频不符。冲击来源待定。', 'warn'],
       ['17:37', 'MON', `记录：${E.E2_3.name}。`], ['17:37', 'RULE', '证据齐备。可建立规则草案。'],
       ['17:38', 'RULE', '规则冲突。', 'conflict'], ['17:38', 'TWIN', '按“更换齿轮”仿真：咔哒仍在。', 'warn'],
       ['17:40', 'RULE', '三联一致。生成规则草案 ZHOU_CLICK。'], ['17:40', 'TWIN', '仿真验证 ZHOU_CLICK：L1 冲击报警 / R1 边频。'],
       ['17:41', 'RULE', '经验规则 ZHOU_CLICK 已确认。来源 周铁生。', 'ok'], ['17:41', 'DIAG', '更正：主轴后轴承早期缺陷，列入计划检修。'],
       ['17:41', 'DIAG', '撤回“停机更换齿轮”建议。'], ['17:41', 'SYS', '交接 2/4。'], ['17:41', 'SYS', `等级：${D.levels[1]} → ${D.levels[2]}。`],
      ].forEach(([t, m, c, k]) => LOG.push({ id: logId++, t, m, c, k: k || '' }));
      ['enterLathe', 'sampleL1', 'sampleR1', 'learn1', 'learn2'].forEach(x => S.logged[x] = 1);
      Object.assign(S, { lamps: [false, false, true, true], level: 2, puzzleDone: true, clickAt: '17:41', hintLevel: 0 });
      S.rules.ZHOU_CLICK = 'CONFIRMED';
    };
    const unsolve = () => { S.rules.ZHOU_CLICK = null; S.lamps = [false, true, true, true]; S.level = 1; S.puzzleDone = false; };
    switch (k) {
      case 'title': go('title'); break;
      case 'reset': try { localStorage.removeItem(SAVE_KEY); } catch (e) { } S = fresh(); LOG = D.logBoot.map((l, i) => ({ id: i, t: l[0], m: l[1], c: l[2], k: l[3] || '' })); logId = LOG.length; go('title'); break;
      case 'desk': ensureGame(); S.spread = 0; go('desk'); break;
      case 'desk1983': ensureGame(); S.spread = 1; go('desk'); break;
      case 'lathe': ensureGame(); S.tool = 'rod'; go('lathe'); selectPoint('L1'); break;
      case 'triad': ensureGame(); allEv(); unsolve(); S.pick = { phen: null, exp: null, data: null }; S.conflictCols = []; S.spread = 0; go('desk'); openTriad(); break;
      case 'triadWrong': ensureGame(); allEv(); unsolve(); S.pick = { phen: 'p1', exp: 'e2', data: 'd1' }; S.conflictCols = []; S.spread = 0; go('desk'); openTriad(); await wait(300); confirmTriad(); break;
      case 'rule': ensureGame(); allEv(); unsolve(); S.pick = { phen: 'p1', exp: 'e1', data: 'd1' }; S.spread = 0; go('desk'); log('RULE', '三联一致。生成规则草案 ZHOU_CLICK。'); S.rules.ZHOU_CLICK = 'DRAFT'; generateRule(); break;
      case 'promo': ensureGame(); allEv(); S.rules.ZHOU_CLICK = 'CONFIRMED'; S.lamps = [false, false, true, true]; S.level = 1; go('desk'); promote(); break;
      case 'mes': ensureGame(); solved(); go('mes'); break;
      case 'archive': ensureGame(); solved(); go('desk'); openArchive('zhou'); break;
      case 'cert': ensureGame(); solved(); go('desk'); openCert(); break;
      case 'toolbox': ensureGame(); go('lathe'); await wait(200); openToolbox(); break;
      case 'ending': go('ending'); break;
    }
  }

  /* =====================================================================
     提示计时（自上一次有效操作起算）
     ===================================================================== */
  setInterval(() => {
    if (!S.hintsOn || S.puzzleDone || !(S.screen === 'desk' || S.screen === 'lathe') || !S.ledgerOpen) return;
    if ($('.overlay.on')) return;
    const sec = (performance.now() - lastAct) / 1000 * (S.hintFast ? 10 : 1);
    const lv = sec >= 300 ? 3 : sec >= 180 ? 2 : sec >= 90 ? 1 : 0;
    if (lv > S.hintLevel) {
      S.hintLevel = lv; A.play('tick', .3);
      if (S.screen === 'desk') renderSpread(); else { renderLedgerCorner(); caption('交班本页边多了一行批注。', '', 2400); }
    }
  }, 1000);

  /* =====================================================================
     事件分发
     ===================================================================== */
  stage.addEventListener('click', e => {
    const t = e.target.closest('[data-act]'); if (!t || t.disabled) return;
    const a = t.dataset.act;
    switch (a) {
      case 'punch': punch(); break;
      case 'continue': { const j = loadSave(); if (j) { S = Object.assign(fresh(), j.S); LOG = j.LOG; logId = LOG.length ? LOG[LOG.length - 1].id + 1 : 0; ensureGame(); A.init(); go('desk'); } break; }
      case 'settings': closeOverlays(); openSettings(); break;
      case 'quit': caption('今天这一班还没交。（浏览器原型无法退出程序）', '', 2600); break;
      case 'openLedger': openLedger(); break;
      case 'flipNext': flip(1); break;
      case 'flipPrev': flip(-1); break;
      case 'read': openReader(t.dataset.item); break;
      case 'lathe': act(); go('lathe'); break;
      case 'desk': act(); go('desk'); break;
      case 'triad': openTriad(); break;
      case 'closeTriad': overlay('triad', false); if (S.screen === 'desk') renderSpread(); break;
      case 'pick': pick(t.dataset.col, t.dataset.opt); break;
      case 'lockedOpt': caption('这一项还没有对应的贴页。', '', 2000); t.classList.add('shake'); setTimeout(() => t.classList.remove('shake'), 300); break;
      case 'confirm': confirmTriad(); break;
      case 'closeRule': overlay('rule', false); break;
      case 'closePromo': closePromo(); break;
      case 'point': selectPoint(t.dataset.pt); break;
      case 'playPause': if (A.playing()) A.stopLoop(); else if (S.point) A.loop(S.point); renderTablet(); break;
      case 'sb': sideband(t.dataset.v); break;
      case 'toolbox': toggleToolbox(); break;
      case 'tool': pickTool(t.dataset.tool); break;
      case 'archive': openArchive(); break;
      case 'archTab': openArchive(t.dataset.k); break;
      case 'cert': openCert(); break;
      case 'mes': act(); go('mes'); break;
      case 'mesRule': mesRule = t.dataset.k; renderMES(); break;
      case 'closeOv': if (t.closest('#ov-promo')) { closePromo(); break; } closeOverlays(['ov-rule', 'ov-promo']); if (S.screen === 'desk' && S.ledgerOpen) renderSpread(); break;
      case 'set': setOpt(t.dataset.k); break;
      case 'jump': jump(t.dataset.k); break;
      case 'hintNow': S.hintLevel = Math.min(3, S.hintLevel + 1); renderSpread(); break;
      case 'sigAuto': sigAuto(); break;
      case 'sigClear': sigClear(); break;
      case 'sigDone': sigDone(); break;
    }
  });
  $('#ov-promo').addEventListener('click', () => closePromo());
  $('#scr-intro').addEventListener('click', endIntro);
  $('#scr-lathe').addEventListener('click', e => { if (S.tool === 'torch' && !e.target.closest('[data-act]')) { act(); const p = stagePt(e); if (Math.hypot(p.x - PT.L1.x, p.y - PT.L1.y) < 90) caption('手电下：磨亮的样冲眼，一圈褪色的漆。', '', 2600); } });

  // Esc 与屏幕右上角“目录”按钮共用：先关浮层，再退出 MES，否则打开暂停 / 演示目录
  function back() { if ($('#ov-promo').classList.contains('on')) return closePromo(); if ($('.overlay.on:not(#ov-rule)')) { closeOverlays(['ov-rule']); if (S.screen === 'desk' && S.ledgerOpen) renderSpread(); } else if (S.screen === 'mes') go('desk'); else openPause(); }
  $('#menuFab').addEventListener('click', () => { if ($('#ov-pause').classList.contains('on')) { closeOverlays(['ov-rule']); if (S.screen === 'desk' && S.ledgerOpen) renderSpread(); } else { closeOverlays(['ov-rule', 'ov-promo']); openPause(); } });

  addEventListener('keydown', e => {
    const k = e.key;
    if (e.target.tagName === 'INPUT') return;
    if (k === 'Escape') return back();
    if (S.screen === 'title') {
      const cards = menuCards();
      if (k === 'ArrowDown') { focusIdx = (focusIdx + 1) % cards.length; markFocus(); }
      if (k === 'ArrowUp') { focusIdx = (focusIdx + cards.length - 1) % cards.length; markFocus(); }
      if (k === 'Enter') cards[focusIdx] && cards[focusIdx].click();
      return;
    }
    if ($('.overlay.on')) return;
    if (k === 'Tab') { e.preventDefault(); if (S.screen === 'desk' && S.ledgerOpen) go('lathe'); else if (S.screen === 'lathe') go('desk'); else if (S.screen === 'desk') openLedger(); }
    if (S.screen === 'desk' && (k === 'd' || k === 'D' || k === 'ArrowRight')) flip(1);
    if (S.screen === 'desk' && (k === 'a' || k === 'A' || k === 'ArrowLeft')) flip(-1);
    if ((k === 't' || k === 'T') && (S.screen === 'desk' || S.screen === 'lathe')) toggleToolbox();
    if (['1', '2', '3', '4'].includes(k) && (S.screen === 'desk' || S.screen === 'lathe')) pickTool(TOOLS[+k - 1][0]);
    if ((k === 'm' || k === 'M') && S.screen === 'desk') go('mes');
  });

  /* ---------------- 启动 ---------------- */
  // 字体就绪后再显示，避免回退字体闪烁；URL 参数 ?jump=xxx 用于截图脚本直达
  document.fonts.ready.then(() => {
    const q = new URLSearchParams(location.search).get('jump');
    if (q) jump(q); else go('title');
    document.body.dataset.ready = '1';
  });
  window.__proto = { jump, get state() { return S; }, go, selectPoint, pick, confirmTriad, acquire, openTriad, flip, openLedger };
})();
