/* 《最后一次交班》UI Prototype —— 程序合成的占位音频（WebAudio）
   对应 03_听音诊断原型 §3.1 的四段 WAV：L1_normal / R1_normal / L1_wrong_gearfix / R1_gearwear。
   全部为程序生成的示意声音，不是实地录音；最终版须换成团队实地录音与声音设计（声音圣经）。 */
window.AUDIO = (() => {
  let ctx = null, master = null, loopSrc = null, loopGain = null, loopStart = 0, loopName = null;
  const buffers = {};
  let volume = 0.8;
  const D = () => window.DATA.spec;

  function init() {
    if (ctx) return true;
    try {
      ctx = new (window.AudioContext || window.webkitAudioContext)();
      master = ctx.createGain(); master.gain.value = volume; master.connect(ctx.destination);
      build();
      return true;
    } catch (e) { ctx = null; return false; }
  }

  function makeBuf(seconds, fill) {
    const sr = ctx.sampleRate, n = Math.floor(seconds * sr);
    const b = ctx.createBuffer(1, n, sr), d = b.getChannelData(0);
    fill(d, sr);
    let peak = 0; for (let i = 0; i < n; i++) peak = Math.max(peak, Math.abs(d[i]));
    if (peak > 0.95) for (let i = 0; i < n; i++) d[i] *= 0.95 / peak;
    return b;
  }
  function hum(d, sr, amp, f0 = 620, wobble = 0) {
    for (let i = 0; i < d.length; i++) {
      const t = i / sr, f = f0 * (1 + wobble * Math.sin(2 * Math.PI * 15 * t));
      d[i] += amp * (Math.sin(2 * Math.PI * f * t) + .45 * Math.sin(2 * Math.PI * 2 * f * t + 1) + .2 * Math.sin(2 * Math.PI * 3 * f * t + 2)
        + .25 * Math.sin(2 * Math.PI * 48 / 60 * t * 31));
      d[i] += amp * .08 * (Math.random() * 2 - 1);
    }
  }
  function click(d, sr, at, amp) {
    const s = Math.floor(at * sr), len = Math.floor(0.03 * sr);
    const fr = [2550, 3150, 3720], ph = fr.map(() => Math.random() * 6.28);
    for (let k = 0; k < len && s + k < d.length; k++) {
      const t = k / sr, env = Math.exp(-t / 0.005);
      let v = 0; fr.forEach((f, j) => v += Math.sin(2 * Math.PI * f * t + ph[j]));
      d[s + k] += amp * env * (v / 3 + .25 * (Math.random() * 2 - 1));
    }
  }
  function build() {
    const dur = D().duration, imp = D().impacts;
    buffers.L1 = makeBuf(dur, (d, sr) => { hum(d, sr, .07); imp.forEach(t => click(d, sr, t, .55)); });
    buffers.R1 = makeBuf(dur, (d, sr) => { hum(d, sr, .3); imp.forEach(t => click(d, sr, t, .025)); });
    buffers.humLoud = makeBuf(dur, (d, sr) => hum(d, sr, .42));
    buffers.gearFixed = makeBuf(dur, (d, sr) => { hum(d, sr, .06, 655); imp.forEach(t => click(d, sr, t, .55)); });
    buffers.denser = makeBuf(dur, (d, sr) => { hum(d, sr, .07); let t = .1, gap = .3; while (t < dur) { click(d, sr, t, .55); gap = Math.max(.07, gap * .9); t += gap; } });
    buffers.gearWear = makeBuf(dur, (d, sr) => hum(d, sr, .3, 620, .03));
    // 打卡机“咔—哒”
    buffers.punch = makeBuf(1.1, (d, sr) => {
      const hit = (at, f, a, dec) => { const s = Math.floor(at * sr); for (let k = 0; k < .25 * sr && s + k < d.length; k++) { const t = k / sr; d[s + k] += a * Math.exp(-t / dec) * (Math.sin(2 * Math.PI * f * t) * .6 + (Math.random() * 2 - 1) * .7); } };
      hit(0, 180, .9, .03); hit(.02, 1200, .3, .01); hit(.46, 260, 1, .025); hit(.47, 2400, .35, .008);
    });
    buffers.flip = makeBuf(.5, (d, sr) => { let y = 0; for (let i = 0; i < d.length; i++) { const t = i / sr; const env = Math.sin(Math.PI * Math.min(1, t / .45)) ** 2; y = .82 * y + .18 * (Math.random() * 2 - 1); d[i] = env * y * .9; } });
    buffers.stamp = makeBuf(.4, (d, sr) => { for (let i = 0; i < d.length; i++) { const t = i / sr; d[i] = Math.exp(-t / .03) * (Math.sin(2 * Math.PI * 90 * t) * .9 + (Math.random() * 2 - 1) * .5); } });
    buffers.drawer = makeBuf(.7, (d, sr) => { let y = 0; for (let i = 0; i < d.length; i++) { const t = i / sr; y = .6 * y + .4 * (Math.random() * 2 - 1); d[i] = Math.sin(Math.PI * t / .7) * (y * .35 + .15 * Math.sin(2 * Math.PI * (900 + 300 * Math.sin(t * 30)) * t)); } });
    buffers.relay = makeBuf(.3, (d, sr) => { click(d, sr, 0, .8); click(d, sr, .012, .4); for (let i = 0; i < d.length; i++) d[i] += Math.exp(-i / sr / .02) * Math.sin(2 * Math.PI * 140 * i / sr) * .4; });
    buffers.beep = makeBuf(.5, (d, sr) => { for (let i = 0; i < d.length; i++) { const t = i / sr; const on = (t < .12 || (t > .2 && t < .32)) ? 1 : 0; d[i] = on * .25 * Math.sin(2 * Math.PI * 1760 * t); } });
    buffers.tick = makeBuf(.05, (d, sr) => click(d, sr, 0, .25));
  }

  function play(name, gain = 1) {
    if (!init() || !buffers[name]) return;
    if (ctx.state === 'suspended') ctx.resume();
    const s = ctx.createBufferSource(), g = ctx.createGain();
    s.buffer = buffers[name]; g.gain.value = gain; s.connect(g); g.connect(master); s.start();
  }
  function loop(name) {
    if (!init()) { loopStart = performance.now() / 1000; loopName = name; return; }
    if (ctx.state === 'suspended') ctx.resume();
    stopLoop();
    loopSrc = ctx.createBufferSource(); loopGain = ctx.createGain();
    loopSrc.buffer = buffers[name]; loopSrc.loop = true;
    loopSrc.connect(loopGain); loopGain.connect(master);
    loopStart = ctx.currentTime; loopName = name; loopSrc.start();
  }
  function stopLoop() { if (loopSrc) { try { loopSrc.stop(); } catch (e) { } loopSrc = null; } loopName = null; }
  /* 播放头位置（秒，0–duration）。没有音频时用时钟推进，保证画面同步可演示。 */
  function position() {
    const now = ctx ? ctx.currentTime : performance.now() / 1000;
    return ((now - loopStart) % D().duration + D().duration) % D().duration;
  }
  function playing() { return !!loopName; }
  function setVolume(v) { volume = v; if (master) master.gain.value = v; }
  return { init, play, loop, stopLoop, position, playing, setVolume, get current() { return loopName; } };
})();
