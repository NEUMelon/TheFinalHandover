/* 《最后一次交班》UI Prototype —— 时频图（预计算假数据，非实时 FFT）
   按 03_听音诊断原型 §3.2 的注释数据合成图像：共振带内周期冲击（L1）、啮合线 620Hz（R1）。
   颜色只用衡的冷白单色映射（UI-DS §5.11），不用彩虹色谱。 */
window.SPECTRO = (() => {
  const W = 760, H = 340;
  const cache = {};
  const S = () => window.DATA.spec;
  const f2y = f => H - (f / S().fmax) * H;
  const t2x = t => (t / S().duration) * W;
  const y2f = y => (1 - y / H) * S().fmax;
  const x2t = x => (x / W) * S().duration;

  function rand(seed) { let s = seed; return () => (s = (s * 16807) % 2147483647) / 2147483647; }

  function build(point) {
    if (cache[point]) return cache[point];
    const c = document.createElement('canvas'); c.width = W; c.height = H;
    const g = c.getContext('2d'), img = g.createImageData(W, H), d = img.data;
    const r = rand(point === 'L1' ? 11 : 23), sp = S();
    const L1 = point === 'L1';
    const lines = L1 ? [[620, .38], [1240, .2], [1860, .1]] : [[620, 1], [1240, .62], [1860, .36], [2480, .16]];
    const impAmp = L1 ? 1 : .07;
    for (let x = 0; x < W; x++) {
      const t = x2t(x);
      // 最近一次冲击后的衰减
      let imp = 0;
      for (const ti of sp.impacts) {
        const dt = t - ti;
        if (dt >= -0.006 && dt < 0.08) imp = Math.max(imp, dt < 0 ? 1 + dt / 0.006 : Math.exp(-dt / 0.016));
      }
      for (let y = 0; y < H; y++) {
        const f = y2f(y);
        let v = 0.05 + 0.07 * r() + 0.08 * Math.exp(-f / 1500);
        for (const [lf, la] of lines) v += la * Math.exp(-((f - lf) ** 2) / (2 * 38 ** 2));
        // 共振带内冲击（带边缘柔化）
        const inBand = f >= sp.band[0] && f <= sp.band[1] ? 1 : Math.exp(-((f < sp.band[0] ? sp.band[0] - f : f - sp.band[1]) ** 2) / (2 * 260 ** 2));
        const tex = .75 + .25 * Math.sin(f / 90 + x * .7) * r();
        v += impAmp * imp * (inBand * .95 + .1) * tex;
        v = Math.min(1, v);
        const k = Math.pow(v, 1.25);
        const i = (y * W + x) * 4;
        // C-Sys-BG #0E1620 → C-Sys #E6F0F5
        d[i] = 14 + (230 - 14) * k; d[i + 1] = 22 + (240 - 22) * k; d[i + 2] = 32 + (245 - 32) * k; d[i + 3] = 255;
      }
    }
    g.putImageData(img, 0, 0);
    // 扫描线（2px 间隔，8%）
    g.fillStyle = 'rgba(14,22,32,.16)';
    for (let y = 0; y < H; y += 2) g.fillRect(0, y, W, 1);
    return (cache[point] = c);
  }

  function drawBase(canvas, point) {
    const g = canvas.getContext('2d');
    g.clearRect(0, 0, W, H);
    g.drawImage(build(point), 0, 0);
  }

  /* 每帧：播放头、冲击条纹同步闪亮、已标记条纹描边 */
  function drawFx(canvas, point, pos, marked, box) {
    const g = canvas.getContext('2d'), sp = S();
    g.clearRect(0, 0, W, H);
    const yb0 = f2y(sp.band[1]), yb1 = f2y(sp.band[0]);
    if (point === 'L1') {
      for (const ti of sp.impacts) {
        const x = t2x(ti), dt = pos - ti;
        const inBox = marked && box && ti >= box.t0 && ti <= box.t1;
        if (dt >= 0 && dt < 0.18) {
          const a = 1 - dt / 0.18;
          g.fillStyle = `rgba(230,240,245,${0.32 * a})`;
          g.fillRect(x - 5, yb0, 12, yb1 - yb0);
          g.strokeStyle = `rgba(230,240,245,${a})`; g.lineWidth = 1;
          g.strokeRect(x - 5.5, yb0 - 6.5, 13, yb1 - yb0 + 13);
        }
        if (inBox) {
          g.strokeStyle = 'rgba(230,240,245,.9)'; g.lineWidth = 1;
          g.beginPath(); g.moveTo(x - 3.5, yb0 - 12); g.lineTo(x - 3.5, yb0 - 4); g.moveTo(x - 3.5, yb1 + 4); g.lineTo(x - 3.5, yb1 + 12); g.stroke();
        }
      }
    }
    const px = Math.round(t2x(pos)) + .5;
    g.strokeStyle = 'rgba(230,240,245,.95)'; g.lineWidth = 1;
    g.beginPath(); g.moveTo(px, 0); g.lineTo(px, H); g.stroke();
    g.fillStyle = '#E6F0F5';
    g.beginPath(); g.moveTo(px - 5, 0); g.lineTo(px + 5, 0); g.lineTo(px, 7); g.fill();
  }

  /* 框选判定（03_听音诊断原型 §4.1） */
  function judge(box) {
    const sp = S();
    const n = sp.impacts.filter(t => t >= box.t0 && t <= box.t1).length;
    const ov = Math.max(0, Math.min(box.f1, sp.band[1]) - Math.max(box.f0, sp.band[0]));
    const ratio = ov / Math.max(1, box.f1 - box.f0);
    if (box.f0 <= sp.gmf && box.f1 >= sp.gmf) return { ok: false, msg: '框内混入齿轮啮合线。请缩小频率范围。' };
    if (ratio < 0.6) return { ok: false, msg: '框选区域不在冲击频带内。' };
    if (n < 3) return { ok: false, msg: '冲击样本不足。请框得更长一些。' };
    return { ok: true, n, msg: `已标记：周期冲击 ×${n}，间隔 0.29s。` };
  }

  /* 最近一次越过播放头的冲击序号（用于“咔哒”同步） */
  function lastImpact(pos) {
    const imp = S().impacts; let k = -1;
    for (let i = 0; i < imp.length; i++) if (imp[i] <= pos) k = i;
    return k;
  }

  return { W, H, f2y, t2x, y2f, x2t, drawBase, drawFx, judge, lastImpact };
})();
