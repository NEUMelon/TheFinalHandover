/* 《最后一次交班》UI Prototype —— 记忆层网点（AM 网点，45° 网角）
   美术圣经 §3：记忆层 = 版画网点，只用于旧照片与旧印刷品，不做全局滤镜。
   场景内容是几何剪影占位（不画五官），最终照片须由团队手绘 / 实拍后再做网点处理。 */
window.HALFTONE = (() => {
  const scenes = {
    /* 夹在工牌套中的工作照：老周背侧身站在 LT01 前，左手持听针伸向主轴箱左端，右手扶刀架手柄 */
    workPhoto(g, w, h) {
      const lg = g.createLinearGradient(0, 0, 0, h); lg.addColorStop(0, '#9a9a9a'); lg.addColorStop(1, '#3c3c3c');
      g.fillStyle = lg; g.fillRect(0, 0, w, h);
      g.fillStyle = '#f4f4f4'; g.fillRect(14, 8, 80, 40); g.fillRect(110, 8, 80, 40); g.fillRect(206, 8, 80, 40);
      g.fillStyle = '#6a6a6a'; g.fillRect(52, 8, 4, 40); g.fillRect(148, 8, 4, 40); g.fillRect(244, 8, 4, 40);
      const rg = g.createRadialGradient(60, 30, 5, 60, 60, 200); rg.addColorStop(0, 'rgba(255,255,255,.55)'); rg.addColorStop(1, 'rgba(255,255,255,0)');
      g.fillStyle = rg; g.fillRect(0, 0, w, h);
      // 床身、卡盘、主轴箱
      g.fillStyle = '#7a7a7a'; g.fillRect(140, 146, 160, 26);
      g.fillStyle = '#4a4a4a'; g.fillRect(140, 172, 160, 48);
      g.fillStyle = '#8c8c8c'; g.beginPath(); g.ellipse(160, 132, 12, 30, 0, 0, 7); g.fill();
      g.fillStyle = '#c4c4c4'; g.beginPath(); g.moveTo(22, 104); g.lineTo(150, 104); g.lineTo(160, 94); g.lineTo(34, 94); g.fill();
      g.fillStyle = '#545454'; g.fillRect(22, 104, 128, 96);
      g.fillStyle = '#2a2a2a'; g.fillRect(14, 200, 150, 20);
      g.fillStyle = '#666'; g.fillRect(244, 126, 34, 22);
      // 人（背侧身，工作帽）
      g.fillStyle = '#1e1e1e';
      g.fill(new Path2D('M150 220 L156 132 Q160 112 182 108 L206 108 Q224 112 226 132 L232 220 Z'));
      g.fillStyle = '#5c5c5c'; g.beginPath(); g.ellipse(193, 90, 15, 17, 0, 0, 7); g.fill();
      g.fillStyle = '#262626'; g.fill(new Path2D('M176 86 Q176 66 194 66 Q212 66 211 86 L206 80 L180 82 Z'));
      g.strokeStyle = '#1e1e1e'; g.lineCap = 'round';
      g.lineWidth = 11; g.beginPath(); g.moveTo(164, 120); g.quadraticCurveTo(110, 104, 74, 102); g.stroke();
      g.lineWidth = 10; g.beginPath(); g.moveTo(222, 124); g.quadraticCurveTo(250, 138, 258, 140); g.stroke();
      g.strokeStyle = '#e6e6e6'; g.lineWidth = 2.5; g.beginPath(); g.moveTo(74, 100); g.lineTo(50, 99); g.stroke();
    },
    portraitZhou(g, w, h) { portrait(g, w, h, true, '#8a8a8a'); },
    portraitJian(g, w, h) { portrait(g, w, h, false, '#9a9a9a'); },
  };
  function portrait(g, w, h, cap, bg) {
    g.fillStyle = bg; g.fillRect(0, 0, w, h);
    const rg = g.createRadialGradient(w * .3, h * .2, 4, w * .5, h * .5, w); rg.addColorStop(0, 'rgba(255,255,255,.5)'); rg.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = rg; g.fillRect(0, 0, w, h);
    g.fillStyle = '#222'; g.fill(new Path2D(`M${w * .08} ${h} Q${w * .12} ${h * .7} ${w * .36} ${h * .64} L${w * .64} ${h * .64} Q${w * .88} ${h * .7} ${w * .92} ${h} Z`));
    g.fillStyle = '#d0d0d0'; g.fill(new Path2D(`M${w * .42} ${h * .64} L${w * .5} ${h * .78} L${w * .58} ${h * .64} Z`));
    g.fillStyle = '#6e6e6e'; g.fillRect(w * .43, h * .5, w * .14, h * .16);
    g.fillStyle = '#7d7d7d'; g.beginPath(); g.ellipse(w * .5, h * .4, w * .2, h * .17, 0, 0, 7); g.fill();
    if (cap) { g.fillStyle = '#2a2a2a'; g.fill(new Path2D(`M${w * .28} ${h * .34} Q${w * .3} ${h * .16} ${w * .5} ${h * .16} Q${w * .72} ${h * .16} ${w * .74} ${h * .34} Z`)); g.fillRect(w * .26, h * .32, w * .5, h * .04); }
    else { g.fillStyle = '#1e1e1e'; g.fill(new Path2D(`M${w * .29} ${h * .38} Q${w * .3} ${h * .2} ${w * .5} ${h * .21} Q${w * .71} ${h * .2} ${w * .71} ${h * .38} Q${w * .6} ${h * .27} ${w * .29} ${h * .38} Z`)); }
  }

  /* 把灰度场景转换为网点：每个网格点按亮度画圆，半径 ∝ √(1−L) */
  function render(canvas, sceneName, { cell = 3.4, angle = 45, ink = '#2b2620', paper = '#e7dec8', scale = 2 } = {}) {
    const w = canvas.width, h = canvas.height;
    const src = document.createElement('canvas'); src.width = w; src.height = h;
    const sg = src.getContext('2d'); scenes[sceneName](sg, w, h);
    const data = sg.getImageData(0, 0, w, h).data;
    const g = canvas.getContext('2d');
    g.fillStyle = paper; g.fillRect(0, 0, w, h); g.fillStyle = ink;
    const a = angle * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a), R = Math.hypot(w, h);
    for (let u = -R; u < R; u += cell) for (let v = -R; v < R; v += cell) {
      const x = u * ca - v * sa + w / 2, y = u * sa + v * ca + h / 2;
      if (x < -cell || y < -cell || x > w + cell || y > h + cell) continue;
      const xi = Math.min(w - 1, Math.max(0, x | 0)), yi = Math.min(h - 1, Math.max(0, y | 0));
      const L = data[(yi * w + xi) * 4] / 255;
      const r = cell * .62 * Math.sqrt(1 - L);
      if (r < .25) continue;
      g.beginPath(); g.arc(x, y, r, 0, 6.2832); g.fill();
    }
    return canvas;
  }
  return { render };
})();
