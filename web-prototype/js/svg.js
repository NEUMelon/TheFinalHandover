/* 《最后一次交班》UI Prototype —— 内联 SVG 资产（单一来源）
   这些图形全部为程序绘制的占位 / 线框级插画，由 tools/export_svg.cjs 导出到 assets/svg/。
   最终交付时，凡标“需人工替换”的图形须由团队手绘或由 Unity 实机画面替换（见 README §5）。 */
window.SVG = (() => {
  const grain = (id, freq = 0.9, op = 0.18) => `
    <filter id="${id}" x="0" y="0" width="100%" height="100%">
      <feTurbulence type="fractalNoise" baseFrequency="${freq}" numOctaves="3" seed="7" result="n"/>
      <feColorMatrix in="n" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 ${op} 0" result="g"/>
      <feComposite in="g" in2="SourceGraphic" operator="in" result="gi"/>
      <feBlend in="SourceGraphic" in2="gi" mode="multiply"/>
    </filter>`;

  /* 厚涂笔触：确定性随机，叠在铸件正面（只是占位质感，最终以手绘贴图为准） */
  function strokes() {
    let seed = 42; const r = () => (seed = (seed * 16807) % 2147483647) / 2147483647;
    let out = '';
    for (let i = 0; i < 140; i++) {
      const x = 70 + r() * 530, y = 310 + r() * 600, len = 70 + r() * 160, ang = -0.08 + r() * 0.16;
      const light = r() > .55, op = (.025 + r() * .05).toFixed(3), w = (2 + r() * 6).toFixed(1);
      const x2 = x + Math.cos(ang) * len, y2 = y + Math.sin(ang) * len, cx = (x + x2) / 2 + (r() - .5) * 20, cy = (y + y2) / 2 + (r() - .5) * 10;
      out += `<path d="M${x.toFixed(0)} ${y.toFixed(0)} Q${cx.toFixed(0)} ${cy.toFixed(0)} ${x2.toFixed(0)} ${y2.toFixed(0)}" stroke="${light ? '#b9cdbd' : '#22302a'}" stroke-opacity="${op}" stroke-width="${w}" stroke-linecap="round" fill="none"/>`;
    }
    return out;
  }

  /* ---------- 工具箱：听针 / 手电 / 卡尺 / 螺丝刀（俯视，抽屉内） ---------- */
  const toolRod = `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 420 70" width="420" height="70">
  <defs>
    <linearGradient id="rodSteel" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#e9e6df"/><stop offset=".35" stop-color="#9a9890"/>
      <stop offset=".55" stop-color="#5d5c57"/><stop offset="1" stop-color="#2e2d2a"/>
    </linearGradient>
    <linearGradient id="rodWood" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#c9925a"/><stop offset=".45" stop-color="#8a5a2e"/><stop offset="1" stop-color="#4a2e15"/>
    </linearGradient>
    <linearGradient id="rodPolish" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#f3d9a8" stop-opacity="0"/><stop offset=".5" stop-color="#f6e2b8" stop-opacity=".55"/><stop offset="1" stop-color="#f3d9a8" stop-opacity="0"/>
    </linearGradient>
  </defs>
  <ellipse cx="215" cy="54" rx="200" ry="7" fill="#000" opacity=".35"/>
  <!-- 钢针：由粗到尖 -->
  <path d="M150 30 L404 33.2 Q412 35 404 36.8 L150 40 Z" fill="url(#rodSteel)"/>
  <path d="M150 31.2 L395 33.6" stroke="#fff" stroke-opacity=".5" stroke-width="1"/>
  <!-- 铜箍 -->
  <rect x="136" y="25" width="18" height="20" rx="3" fill="#b08a3e"/>
  <rect x="136" y="25" width="18" height="6" rx="3" fill="#e2c27a" opacity=".7"/>
  <!-- 木柄（左手常握处磨亮） -->
  <path d="M14 22 Q8 35 14 48 L138 46 L138 24 Z" fill="url(#rodWood)"/>
  <path d="M40 23 L110 24 L110 46 L40 47 Z" fill="url(#rodPolish)"/>
  <path d="M20 29 Q70 27 132 29" stroke="#2a1a0b" stroke-opacity=".35" stroke-width="1" fill="none"/>
  <path d="M22 41 Q80 43 132 41" stroke="#2a1a0b" stroke-opacity=".3" stroke-width="1" fill="none"/>
  <circle cx="22" cy="35" r="3" fill="#2a1a0b" opacity=".6"/>
</svg>`;

  const toolTorch = `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 300 96" width="300" height="96">
  <defs>
    <linearGradient id="tBody" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#d7d3c9"/><stop offset=".3" stop-color="#8e8b83"/><stop offset=".7" stop-color="#4b4a46"/><stop offset="1" stop-color="#2a2927"/>
    </linearGradient>
    <linearGradient id="tHead" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#f1efe8"/><stop offset=".4" stop-color="#a9a69d"/><stop offset="1" stop-color="#3b3a37"/>
    </linearGradient>
    <radialGradient id="tLens" cx=".4" cy=".4" r=".7">
      <stop offset="0" stop-color="#fff6dc"/><stop offset=".5" stop-color="#e8b45a" stop-opacity=".55"/><stop offset="1" stop-color="#6b5530"/>
    </radialGradient>
    <pattern id="knurl" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
      <rect width="6" height="6" fill="none"/><path d="M0 0 V6" stroke="#1a1a1a" stroke-opacity=".35" stroke-width="1.4"/>
    </pattern>
  </defs>
  <ellipse cx="150" cy="82" rx="140" ry="8" fill="#000" opacity=".35"/>
  <rect x="18" y="30" width="180" height="36" rx="6" fill="url(#tBody)"/>
  <rect x="40" y="30" width="110" height="36" fill="url(#knurl)"/>
  <rect x="10" y="32" width="12" height="32" rx="4" fill="#3a3936"/>
  <!-- 开关滑钮 -->
  <rect x="160" y="24" width="24" height="10" rx="3" fill="#1a1a1a"/><rect x="164" y="22" width="12" height="6" rx="2" fill="#5a5953"/>
  <!-- 灯头 -->
  <path d="M196 30 L238 14 L238 82 L196 66 Z" fill="url(#tHead)"/>
  <rect x="236" y="12" width="16" height="72" rx="5" fill="#b9b6ad"/>
  <ellipse cx="252" cy="48" rx="10" ry="34" fill="url(#tLens)"/>
  <path d="M26 36 H190" stroke="#fff" stroke-opacity=".35"/>
  <!-- 掉漆露铜 -->
  <circle cx="72" cy="60" r="3" fill="#b08a3e" opacity=".8"/>
</svg>`;

  const toolCaliper = (() => {
    let ticks = '';
    for (let i = 0; i <= 150; i++) {
      const x = 74 + i * 1.9;
      const h = i % 10 === 0 ? 10 : i % 5 === 0 ? 7 : 4;
      ticks += `<line x1="${x.toFixed(1)}" y1="38" x2="${x.toFixed(1)}" y2="${38 + h}" stroke="#2a2a28" stroke-width=".7"/>`;
      if (i % 10 === 0) ticks += `<text x="${x.toFixed(1)}" y="58" font-size="6.5" font-family="JetBrainsMono, monospace" text-anchor="middle" fill="#2a2a28">${i / 10}</text>`;
    }
    return `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 130" width="400" height="130">
  <defs>
    <linearGradient id="cSteel" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#f2f0ea"/><stop offset=".5" stop-color="#c3c0b7"/><stop offset="1" stop-color="#7d7b74"/>
    </linearGradient>
  </defs>
  <ellipse cx="200" cy="118" rx="190" ry="7" fill="#000" opacity=".3"/>
  <!-- 主尺 -->
  <rect x="40" y="30" width="350" height="34" rx="2" fill="url(#cSteel)"/>
  ${ticks}
  <!-- 固定量爪 -->
  <path d="M40 30 L66 30 L66 112 L56 112 L40 70 Z" fill="url(#cSteel)" stroke="#6d6b65" stroke-width=".8"/>
  <path d="M40 30 L60 30 L56 6 L46 6 Z" fill="url(#cSteel)" stroke="#6d6b65" stroke-width=".8"/>
  <!-- 游标 -->
  <rect x="120" y="26" width="84" height="44" rx="3" fill="#b5b2a9" stroke="#5f5d58" stroke-width=".8"/>
  <rect x="126" y="48" width="72" height="14" fill="#e8e5dc"/>
  <path d="M120 70 L120 112 L132 112 L146 70 Z" fill="url(#cSteel)" stroke="#6d6b65" stroke-width=".8"/>
  <path d="M120 26 L120 6 L128 6 L136 26 Z" fill="url(#cSteel)" stroke="#6d6b65" stroke-width=".8"/>
  <circle cx="186" cy="22" r="6" fill="#8a877f" stroke="#4a4844"/>
  <text x="300" y="25" font-size="8" font-family="JetBrainsMono, monospace" fill="#4a4844">0.02mm</text>
</svg>`;
  })();

  const toolDriver = `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 320 64" width="320" height="64">
  <defs>
    <linearGradient id="dWood" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#d9a066"/><stop offset=".5" stop-color="#9b6534"/><stop offset="1" stop-color="#55331a"/>
    </linearGradient>
    <linearGradient id="dSteel" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#eeeae2"/><stop offset=".5" stop-color="#8f8c84"/><stop offset="1" stop-color="#3b3a36"/>
    </linearGradient>
  </defs>
  <ellipse cx="160" cy="52" rx="150" ry="6" fill="#000" opacity=".3"/>
  <path d="M120 27 L292 28 L304 25 L306 39 L292 36 L120 37 Z" fill="url(#dSteel)"/>
  <rect x="108" y="22" width="16" height="20" rx="2" fill="#a8a59c"/>
  <path d="M12 18 Q4 32 12 46 L110 44 Q114 32 110 20 Z" fill="url(#dWood)"/>
  <path d="M24 22 Q60 20 104 23 M24 42 Q60 44 104 41" stroke="#3a210d" stroke-opacity=".35" fill="none"/>
  <!-- 红漆环（旧班组标记） -->
  <rect x="80" y="19" width="8" height="26" fill="#A8322D" opacity=".75"/>
</svg>`;

  /* ---------- 合格章（纸面，绿色） ---------- */
  const stampPass = (txt = '合格', sub = '三号车间 · 点检') => `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 160" width="160" height="160">
  <defs>
    <filter id="inkRough" x="-10%" y="-10%" width="120%" height="120%">
      <feTurbulence type="fractalNoise" baseFrequency="2.2" numOctaves="1" seed="3" result="t"/>
      <feColorMatrix in="t" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 -2.2 2.1" result="m"/>
      <feComposite in="SourceGraphic" in2="m" operator="in" result="c"/>
      <feTurbulence type="fractalNoise" baseFrequency=".05" numOctaves="2" seed="9" result="w"/>
      <feDisplacementMap in="c" in2="w" scale="2.5"/>
    </filter>
    <path id="ringPath" d="M80 80 m-56 0 a56 56 0 1 1 112 0 a56 56 0 1 1 -112 0"/>
  </defs>
  <g filter="url(#inkRough)" fill="none" stroke="#5E7D6A" opacity=".92">
    <circle cx="80" cy="80" r="72" stroke-width="5"/>
    <circle cx="80" cy="80" r="64" stroke-width="1.5"/>
    <text font-family="NotoSerifSC, serif" font-size="14" font-weight="700" fill="#5E7D6A" stroke="none" letter-spacing="2">
      <textPath href="#ringPath" startOffset="2%">${sub} · ${sub}</textPath>
    </text>
    <text x="80" y="96" text-anchor="middle" font-family="NotoSerifSC, serif" font-weight="900" font-size="44" fill="#5E7D6A" stroke="none">${txt}</text>
  </g>
</svg>`;

  /* ---------- LT01 主轴箱特写（CU-1，谜题二）需人工替换 ---------- */
  const lathe = `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1160 1000" width="1160" height="1000" id="latheSvg">
  <defs>
    <linearGradient id="oilFade" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#14140f" stop-opacity="0"/><stop offset="1" stop-color="#14140f" stop-opacity=".45"/></linearGradient>
    <linearGradient id="warmRim" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#E8B45A" stop-opacity=".38"/><stop offset=".6" stop-color="#E8B45A" stop-opacity=".08"/><stop offset="1" stop-color="#E8B45A" stop-opacity="0"/></linearGradient>
    <linearGradient id="floorG" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3a3a35"/><stop offset="1" stop-color="#1a1b1a"/></linearGradient>
    <radialGradient id="vign" cx=".42" cy=".42" r=".75"><stop offset=".55" stop-color="#000" stop-opacity="0"/><stop offset="1" stop-color="#05080b" stop-opacity=".6"/></radialGradient>
    <radialGradient id="warmFace" cx=".15" cy="0" r=".9"><stop offset="0" stop-color="#E8B45A" stop-opacity=".22"/><stop offset="1" stop-color="#E8B45A" stop-opacity="0"/></radialGradient>
    <clipPath id="hsClip"><path d="M70 330 Q70 310 96 310 L574 310 Q600 310 600 336 L600 880 Q600 904 574 904 L96 904 Q70 904 70 880 Z"/></clipPath>
    <radialGradient id="lampPool" cx=".42" cy=".08" r=".9">
      <stop offset="0" stop-color="#E8B45A" stop-opacity=".55"/>
      <stop offset=".35" stop-color="#8d6a3a" stop-opacity=".25"/>
      <stop offset="1" stop-color="#0f1824" stop-opacity="0"/>
    </radialGradient>
    <linearGradient id="bgWall" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#1b2836"/><stop offset=".55" stop-color="#22302f"/><stop offset="1" stop-color="#151c1f"/>
    </linearGradient>
    <linearGradient id="castFront" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#7d9a87"/><stop offset=".25" stop-color="#5E7D6A"/><stop offset=".8" stop-color="#3d5547"/><stop offset="1" stop-color="#2a3a31"/>
    </linearGradient>
    <linearGradient id="castTop" x1="0" y1="1" x2="0" y2="0">
      <stop offset="0" stop-color="#9fb5a3"/><stop offset="1" stop-color="#6f8a78"/>
    </linearGradient>
    <linearGradient id="castSide" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#3a5144"/><stop offset="1" stop-color="#22302a"/>
    </linearGradient>
    <linearGradient id="bedTop" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#cfd0c8"/><stop offset=".3" stop-color="#8d8f88"/><stop offset="1" stop-color="#4a4d48"/>
    </linearGradient>
    <linearGradient id="chuckG" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#e2e0d8"/><stop offset=".4" stop-color="#9c9a92"/><stop offset=".75" stop-color="#55544f"/><stop offset="1" stop-color="#2b2a27"/>
    </linearGradient>
    <radialGradient id="polish" cx=".5" cy=".5" r=".5">
      <stop offset="0" stop-color="#e9efe6" stop-opacity=".85"/><stop offset=".6" stop-color="#c3d0c4" stop-opacity=".35"/><stop offset="1" stop-color="#c3d0c4" stop-opacity="0"/>
    </radialGradient>
    <radialGradient id="knob" cx=".35" cy=".3" r=".7">
      <stop offset="0" stop-color="#6b6b68"/><stop offset=".4" stop-color="#1f1f1e"/><stop offset="1" stop-color="#050505"/>
    </radialGradient>
    <linearGradient id="brass" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#e3cf9a"/><stop offset=".5" stop-color="#b59a5a"/><stop offset="1" stop-color="#7a6334"/>
    </linearGradient>
  </defs>

  <!-- 背景：17:30 天窗光转暗，钨丝灯为主 -->
  <rect width="1160" height="1000" fill="url(#bgWall)"/>
  <rect y="0" width="1160" height="430" fill="#1F3A5F" opacity=".25"/>
  <!-- 远处排架柱与高窗 -->
  <rect x="890" y="0" width="70" height="720" fill="#141c22" opacity=".8"/>
  <rect x="60" y="40" width="300" height="120" fill="#3a5874" opacity=".22"/>
  <rect x="380" y="40" width="300" height="120" fill="#3a5874" opacity=".16"/>
  <path d="M60 100 H680 M210 40 V160 M530 40 V160" stroke="#0f1620" stroke-width="5" opacity=".5"/>
  <!-- 墙裙 1.2m -->
  <rect y="560" width="1160" height="200" fill="#2f4338" opacity=".7"/>
  <rect y="556" width="1160" height="6" fill="#141c18" opacity=".7"/>
  <rect width="1160" height="1000" fill="url(#lampPool)"/>

  <!-- 床身 -->
  <g>
    <path d="M520 690 L1160 690 L1160 760 L520 760 Z" fill="url(#bedTop)"/>
    <path d="M520 700 L1160 700" stroke="#f0eee6" stroke-opacity=".45" stroke-width="2"/>
    <path d="M520 742 L1160 742" stroke="#f0eee6" stroke-opacity=".25" stroke-width="2"/>
    <path d="M520 760 L1160 760 L1160 940 L520 940 Z" fill="url(#castFront)"/>
    <path d="M520 760 L1160 760 L1160 772 L520 772 Z" fill="#1a1a1a" opacity=".35"/>
    <!-- 丝杠、光杠 -->
    <rect x="520" y="830" width="640" height="10" rx="5" fill="#8f8d86"/>
    <rect x="520" y="860" width="640" height="8" rx="4" fill="#6f6d67"/>
    <path d="M520 832 H1160" stroke="#e6e3da" stroke-opacity=".4"/>
  </g>

  <!-- 床脚 -->
  <rect y="900" width="1160" height="100" fill="url(#floorG)"/>
  <ellipse cx="350" cy="910" rx="330" ry="22" fill="#000" opacity=".55"/>
  <path d="M0 960 H1160" stroke="#F2C200" stroke-width="10" opacity=".55"/>

  <!-- 主轴箱（铸件圆角放大 10–15%） -->
  <g id="headstock">
    <!-- 顶面 -->
    <path d="M70 330 Q72 300 110 296 L560 296 Q590 296 600 316 L660 270 Q640 252 612 252 L172 252 Q140 252 128 270 Z" fill="url(#castTop)"/>
    <!-- 正面 -->
    <path d="M70 330 Q70 310 96 310 L574 310 Q600 310 600 336 L600 880 Q600 904 574 904 L96 904 Q70 904 70 880 Z" fill="url(#castFront)"/>
    <!-- 右侧面 -->
    <path d="M600 336 L660 290 L660 846 L600 892 Z" fill="url(#castSide)"/>
    <!-- 铸造分模线、边缘高光 -->
    <path d="M86 322 L588 322" stroke="#c9d6c9" stroke-opacity=".55" stroke-width="2"/>
    <path d="M96 904 L574 904" stroke="#0e1410" stroke-opacity=".7" stroke-width="3"/>
    <!-- 掉漆露底（L1 原厂漆与铸铁） -->
    <path d="M78 600 q10 -8 18 2 q-4 12 -14 10 z" fill="#3a4a3c"/>
    <path d="M82 604 q4 -3 7 1 q-2 4 -6 3 z" fill="#555048"/>
    <path d="M588 360 q8 6 4 16 q-10 -2 -10 -10 z" fill="#3a4a3c"/>
    <path d="M300 892 q14 -6 22 4 q-8 8 -22 -4 z" fill="#4a4740"/>
    <path d="M140 860 q20 -6 30 2" stroke="#24302a" stroke-width="3" fill="none" opacity=".6"/>
    <!-- 油污暗化（下部） -->
    <rect x="70" y="640" width="530" height="264" fill="url(#oilFade)"/>
    <g clip-path="url(#hsClip)">${strokes()}</g>
    <path d="M70 330 Q72 300 110 296 L560 296 Q590 296 600 316 L660 270 Q640 252 612 252 L172 252 Q140 252 128 270 Z" fill="url(#warmRim)"/>
    <rect x="70" y="310" width="530" height="594" fill="url(#warmFace)"/>

    <!-- 检修盖板 -->
    <rect x="120" y="420" width="250" height="170" rx="14" fill="#557362" stroke="#2c3b32" stroke-width="3"/>
    <g fill="#2a3a31"><circle cx="138" cy="438" r="6"/><circle cx="352" cy="438" r="6"/><circle cx="138" cy="572" r="6"/><circle cx="352" cy="572" r="6"/></g>
    <g fill="#9fb4a4"><circle cx="136" cy="436" r="2"/><circle cx="350" cy="436" r="2"/><circle cx="136" cy="570" r="2"/><circle cx="350" cy="570" r="2"/></g>

    <!-- 铭牌（虚构型号） -->
    <rect x="140" y="346" width="210" height="52" rx="4" fill="url(#brass)"/>
    <rect x="146" y="352" width="198" height="40" rx="2" fill="none" stroke="#5a4823" stroke-width="1"/>
    <text x="245" y="372" text-anchor="middle" font-family="NotoSerifSC, serif" font-size="15" font-weight="700" fill="#3a2e14">HC-620 普通车床</text>
    <text x="245" y="388" text-anchor="middle" font-family="JetBrainsMono, monospace" font-size="11" fill="#3a2e14">No. LT01 · 1956</text>

    <!-- 变速手柄（R1 附近 = 变速齿轮组正上方） -->
    <g>
      <rect x="420" y="430" width="140" height="120" rx="12" fill="#4f6b5a" stroke="#2c3b32" stroke-width="2"/>
      <circle cx="455" cy="490" r="24" fill="#2d3d34"/><circle cx="525" cy="490" r="24" fill="#2d3d34"/>
      <path d="M455 490 L430 410" stroke="#bfbdb5" stroke-width="10" stroke-linecap="round"/>
      <path d="M525 490 L560 418" stroke="#bfbdb5" stroke-width="10" stroke-linecap="round"/>
      <circle cx="430" cy="404" r="20" fill="url(#knob)"/><circle cx="562" cy="412" r="20" fill="url(#knob)"/>
      <circle cx="424" cy="398" r="5" fill="#fff" opacity=".35"/><circle cx="556" cy="406" r="5" fill="#fff" opacity=".35"/>
      <!-- 磨亮的手柄杆 -->
      <path d="M452 480 L436 430" stroke="#f3f1ea" stroke-width="3" stroke-linecap="round" opacity=".55"/>
    </g>

    <!-- 1983 / L3 测点：顶面左端，后轴承座正上方 -->
    <g id="l1mark">
      <ellipse cx="190" cy="276" rx="66" ry="16" fill="url(#polish)"/>
      <ellipse cx="190" cy="276" rx="30" ry="9" fill="none" stroke="#ece4b8" stroke-opacity=".55" stroke-width="5" stroke-dasharray="40 6 22 9"/>
      <ellipse cx="190" cy="276" rx="4" ry="2.4" fill="#1b231e"/>
      <ellipse cx="189" cy="275" rx="1.6" ry="1" fill="#eef3ee" opacity=".7"/>
    </g>

    <!-- 2025 / L7 衡加装：R1 临时传感器 + 新线槽 -->
    <g id="r1sensor">
      <rect x="690" y="196" width="470" height="30" fill="#c9cdcf"/><rect x="690" y="196" width="470" height="6" fill="#eef1f2"/><rect x="690" y="222" width="470" height="4" fill="#7d8285"/>
      <path d="M516 346 C 516 318, 560 306, 610 296 S 680 236, 700 222" stroke="#9aa0a3" stroke-width="7" fill="none"/>
      <path d="M516 346 C 516 318, 560 306, 610 296 S 680 236, 700 222" stroke="#d4d9dc" stroke-width="2" fill="none" opacity=".6"/>
      <rect x="600" y="290" width="16" height="12" rx="2" fill="#141414" transform="rotate(-20 608 296)"/>
      <rect x="494" y="350" width="44" height="40" rx="6" fill="#1b1c1d"/>
      <rect x="500" y="344" width="32" height="10" rx="3" fill="#34383b"/>
      <rect x="497" y="354" width="38" height="4" fill="#5b6064"/>
      <!-- 二维码标签 -->
      <rect x="548" y="342" width="34" height="34" fill="#f2f0ea"/>
      <g fill="#1a1a1a">
        <rect x="551" y="345" width="9" height="9"/><rect x="570" y="345" width="9" height="9"/><rect x="551" y="364" width="9" height="9"/>
        <rect x="563" y="357" width="4" height="4"/><rect x="569" y="365" width="4" height="4"/><rect x="574" y="370" width="5" height="3"/><rect x="563" y="368" width="3" height="5"/>
      </g>
      <circle cx="530" cy="384" r="2.5" fill="#E6F0F5"/>
    </g>
  </g>

  <!-- 卡盘（前轴承端） -->
  <g>
    <ellipse cx="700" cy="590" rx="58" ry="168" fill="#2a2a27"/>
    <rect x="660" y="430" width="120" height="320" fill="url(#chuckG)"/>
    <ellipse cx="780" cy="590" rx="58" ry="160" fill="#8d8b84"/>
    <ellipse cx="780" cy="590" rx="46" ry="128" fill="#6e6c66"/>
    <path d="M780 470 L792 530 L768 530 Z M780 710 L792 650 L768 650 Z" fill="#c8c5bc"/>
    <ellipse cx="780" cy="590" rx="16" ry="44" fill="#1e1e1c"/>
    <path d="M664 440 H776" stroke="#fff" stroke-opacity=".4" stroke-width="2"/>
  </g>
  <!-- 工件 -->
  <rect x="790" y="568" width="370" height="44" fill="#b9b7af"/>
  <rect x="790" y="568" width="370" height="10" fill="#e9e7df" opacity=".7"/>
  <rect x="790" y="604" width="370" height="8" fill="#4a4945" opacity=".6"/>

  <!-- 护罩（1987 凹痕位置，谜题三用；此处只露边） -->
  <path d="M652 452 C 660 372, 760 330, 852 380 L 864 404 C 790 362, 690 386, 668 462 Z" fill="#6f8a78"/>
  <path d="M652 452 C 660 372, 760 330, 852 380" stroke="#b9cdbd" stroke-opacity=".6" stroke-width="2" fill="none"/>
  <path d="M668 462 C 690 386, 790 362, 864 404" stroke="#1e2a24" stroke-opacity=".7" stroke-width="3" fill="none"/>
  <circle cx="660" cy="440" r="7" fill="#3a4a40"/><circle cx="660" cy="440" r="3" fill="#9fb4a4"/>
  <path d="M770 360 q12 10 26 4" stroke="#1e2a24" stroke-width="3" fill="none" opacity=".75"/>
  <path d="M772 357 q12 6 22 2" stroke="#d6e2d8" stroke-width="1.5" fill="none" opacity=".5"/>

  <rect width="1160" height="1000" fill="url(#vign)"/>
</svg>`;

  /* ---------- 夹在工牌套中的工作照（1983，网点照片）需人工替换 ---------- */
  const photoScene = `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 300 220" width="300" height="220">
  <defs>
    <linearGradient id="phBg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#bdbdbd"/><stop offset="1" stop-color="#5a5a5a"/></linearGradient>
    <radialGradient id="phLight" cx=".25" cy=".1" r=".8"><stop offset="0" stop-color="#fff" stop-opacity=".8"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>
  </defs>
  <rect width="300" height="220" fill="url(#phBg)"/>
  <rect width="300" height="220" fill="url(#phLight)"/>
  <!-- 高窗 -->
  <rect x="20" y="10" width="90" height="46" fill="#f2f2f2"/><rect x="128" y="10" width="90" height="46" fill="#e0e0e0"/>
  <!-- 车床主轴箱（画面右下） -->
  <path d="M120 120 L250 120 L262 110 L134 110 Z" fill="#9a9a9a"/>
  <rect x="120" y="120" width="130" height="80" fill="#4a4a4a"/>
  <rect x="250" y="130" width="50" height="40" fill="#777"/>
  <rect x="140" y="200" width="160" height="20" fill="#2a2a2a"/>
  <!-- 老周（侧身，工作帽；左手伸向主轴箱左端，持听针；右手扶刀架手柄） -->
  <path d="M40 220 L46 140 Q50 120 70 116 L96 116 Q112 120 114 140 L118 220 Z" fill="#262626"/>
  <ellipse cx="82" cy="98" rx="17" ry="19" fill="#8a8a8a"/>
  <path d="M63 90 Q64 72 84 72 Q104 74 102 92 L98 86 L66 88 Z" fill="#303030"/>
  <path d="M106 132 L136 114" stroke="#262626" stroke-width="11" stroke-linecap="round"/>
  <path d="M134 114 L148 112" stroke="#d9d9d9" stroke-width="2.5"/>
  <path d="M60 150 L74 190 L110 196" stroke="#262626" stroke-width="10" stroke-linecap="round" fill="none"/>
  <rect x="104" y="188" width="18" height="10" fill="#1a1a1a"/>
</svg>`;

  /* ---------- 1983 测点示意页：主轴箱剖面简图（B-1）晒图风 ---------- */
  const sectionSketch = `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 420 220" width="420" height="220">
  <g fill="none" stroke="#1F3A5F" stroke-width="1.6" stroke-linecap="round">
    <rect x="30" y="50" width="300" height="130" rx="10"/>
    <line x1="10" y1="115" x2="400" y2="115" stroke-dasharray="14 4 3 4"/>
    <!-- 后轴承座（左） -->
    <rect x="58" y="92" width="34" height="46"/><line x1="58" y1="92" x2="92" y2="138"/><line x1="92" y1="92" x2="58" y2="138"/>
    <!-- 前轴承座（右） -->
    <rect x="270" y="88" width="40" height="54"/><line x1="270" y1="88" x2="310" y2="142"/><line x1="310" y1="88" x2="270" y2="142"/>
    <!-- 齿轮组 -->
    <circle cx="170" cy="115" r="30"/><circle cx="215" cy="140" r="18"/><circle cx="170" cy="115" r="6"/>
    <rect x="330" y="96" width="60" height="38"/>
  </g>
  <!-- L1 箭头 -->
  <g stroke="#1F3A5F" fill="#1F3A5F">
    <line x1="75" y1="18" x2="75" y2="86" stroke-width="1.8"/>
    <path d="M69 78 L75 90 L81 78 Z"/>
    <circle cx="75" cy="50" r="3"/>
  </g>
  <text x="84" y="26" font-family="ZhiMangXing, cursive" font-size="22" fill="#1F3A5F">L1</text>
  <text x="112" y="40" font-family="ZhiMangXing, cursive" font-size="20" fill="#1F3A5F">L1 听后轴承</text>
  <text x="34" y="206" font-family="ZhiMangXing, cursive" font-size="18" fill="#1F3A5F">后轴承座</text>
  <text x="256" y="206" font-family="ZhiMangXing, cursive" font-size="18" fill="#1F3A5F">前轴承</text>
  <text x="150" y="168" font-family="ZhiMangXing, cursive" font-size="16" fill="#1F3A5F">变速齿轮</text>
  <!-- R1 位置（2025 补标，铅笔） -->
  <text x="196" y="40" font-family="LongCang, cursive" font-size="16" fill="#555" opacity=".75">R1</text>
  <line x1="205" y1="44" x2="205" y2="100" stroke="#555" stroke-opacity=".6" stroke-dasharray="3 3"/>
</svg>`;

  /* ---------- 衡：等级证封面徽记（直角、细线，不拟人） ---------- */
  const hengMark = `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">
  <g fill="none" stroke="#E6F0F5" stroke-width="1.5">
    <rect x="4" y="4" width="56" height="56"/>
    <line x1="4" y1="32" x2="60" y2="32"/>
    <line x1="32" y1="12" x2="32" y2="52"/>
    <line x1="18" y1="22" x2="46" y2="22"/>
  </g>
  <circle cx="32" cy="32" r="3" fill="#E6F0F5"/>
</svg>`;

  return { toolRod, toolTorch, toolCaliper, toolDriver, stampPass, lathe, photoScene, sectionSketch, hengMark };
})();
