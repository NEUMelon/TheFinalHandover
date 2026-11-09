// 把 js/svg.js 里的内联 SVG 导出成独立 .svg 文件到 assets/svg/，便于交给美术做替换参考：node tools/export_svg.cjs
// 注意：这些图形都是原型占位（由 AI 编写的矢量代码绘制），正式交付不能直接使用，见 README“必须人工替换”。
const fs = require('fs');
const path = require('path');
const vm = require('vm');
const root = path.resolve(__dirname, '..');
const out = path.join(root, 'assets', 'svg');
fs.mkdirSync(out, { recursive: true });
const ctx = { window: {} };
vm.runInNewContext(fs.readFileSync(path.join(root, 'js', 'svg.js'), 'utf8'), ctx);
const S = ctx.window.SVG;
const files = {
  'lathe_LT01主轴箱_场景插画.svg': S.lathe,
  'tool_1_听针.svg': S.toolRod,
  'tool_2_手电.svg': S.toolTorch,
  'tool_3_卡尺.svg': S.toolCaliper,
  'tool_4_螺丝刀.svg': S.toolDriver,
  'sketch_1983测点示意.svg': S.sectionSketch,
  'stamp_合格_点检.svg': S.stampPass('合格', '三号车间 · 点检'),
  'stamp_合格_经验规则.svg': S.stampPass('合格', '经验规则 · 已确认'),
  'stamp_合格_技术等级考核.svg': S.stampPass('合格', '技术等级考核'),
};
for (const [name, svg] of Object.entries(files)) {
  fs.writeFileSync(path.join(out, name), '<?xml version="1.0" encoding="UTF-8"?>\n' + svg.trim() + '\n', 'utf8');
  console.log('ok', name);
}
