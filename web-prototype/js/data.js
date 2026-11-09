/* 《最后一次交班》UI Prototype —— 文案与谜题数据
   来源：02_母项目VS/01_四谜题事实链_v0.1.md 谜题二；03_听音诊断原型_v0.1.md §3–§4；
   口径：剧情时间压缩（谜题二约 17:30）、B-1～B-7、1983 只标定测点与听感 / 2–4kHz 为 2026 复测、
         “夹在工牌套中的工作照”、1965“第一次以师傅身份带徒”。
   用字：叙事文本年份用汉字，系统 UI / 日志 / 规则卡用阿拉伯数字（世界观圣经 §8）。 */
window.DATA = {
  shift: { date: '二〇二六年十月　日', shiftName: '中班', span: '16:00—24:00', now: '17:30' },

  /* 时频图假数据（03_听音诊断原型 §3.2） */
  spec: {
    duration: 6.0,
    fmax: 8000,
    band: [2000, 4000],
    gmf: 620,
    impacts: [0.12, 0.414, 0.708, 1.002, 1.296, 1.59, 1.884, 2.178, 2.472, 2.766, 3.06, 3.354, 3.648, 3.942, 4.236, 4.53, 4.824, 5.118, 5.412, 5.706],
    sidebandIndex: 0.18,
    sidebandThreshold: 0.35,
  },

  /* 证据（谜题二） */
  evidence: {
    E2_1: { name: 'L1 听音 · 时频图', short: 'L1 时频图', paper: '贴页 4', desc: '约 2–4kHz 带内，间隔均匀的竖向冲击条纹。' },
    E2_2: { name: 'R1 听音 · 啮合边频', short: 'R1 边频', paper: '贴页 5', desc: '啮合线 620Hz，边频指标 0.18，在正常区。' },
    E2_3: { name: '一九八三年设备记录', short: '1983 记录', paper: '贴页 3', desc: '老周批注：“咔哒在左，是轴承；在右，是齿轮。”' },
  },

  /* 三联板：每格 3 项（1 正确 + 2 干扰），需要的证据为空 = 一直可选 */
  triad: [
    {
      key: 'phen', no: '一联', title: '异常现象',
      options: [
        { id: 'p2', text: '嗡声变大了', needs: ['E2_2'], ok: false,
          fb: { log: 'TWIN > 按“嗡变大”建模仿真：没有咔哒。', sim: 'humLoud', cap: '〔仿真：只有嗡声，没有咔哒〕' } },
        { id: 'p1', text: '“嗡”里夹着“咔哒”', needs: ['E2_1'], ok: true },
        { id: 'p3', text: '主轴发热', needs: [], ok: false,
          fb: { log: 'MON > 温度 41°C，正常。无对应证据。', sim: null, cap: '' } },
      ],
    },
    {
      key: 'exp', no: '二联', title: '经验判断',
      options: [
        { id: 'e1', text: '咔哒在左，是轴承', needs: ['E2_3'], ok: true },
        { id: 'e2', text: '在右，是齿轮', needs: ['E2_3'], ok: false,
          fb: { log: 'TWIN > 按“更换齿轮”仿真：咔哒仍在。', sim: 'gearFixed', cap: '〔仿真：换过齿轮，咔哒还在〕' } },
        { id: 'e3', text: '老机器都这样响', needs: [], ok: false,
          fb: { log: 'TWIN > 按“忽略”仿真：咔哒随时间变密。', sim: 'denser', cap: '〔仿真：咔哒越来越密〕' } },
      ],
    },
    {
      key: 'data', no: '三联', title: '数据特征',
      options: [
        { id: 'd2', text: 'R1 啮合频率幅值升高 = 齿轮磨损', needs: ['E2_2'], ok: false,
          fb: { log: 'RULE > 规则冲突：R1 边频带指标在正常区。', sim: null, cap: '' } },
        { id: 'd3', text: '全频带能量升高 = 轴承故障', needs: [], ok: false,
          fb: { log: 'RULE > 规则冲突：该特征无法区分轴承与齿轮。', sim: null, cap: '' } },
        { id: 'd1', text: 'L1 测点、本机 2–4kHz 共振带内的周期冲击 = 后轴承缺陷；齿轮看啮合边频', needs: ['E2_1', 'E2_2'], ok: true },
      ],
    },
  ],

  /* 经验规则卡（UI-DS §5.4，字段与顺序不得增删改名） */
  rules: {
    ZHOU_LEFT: {
      id: 'ZHOU_LEFT', type: '监测位置',
      source: '周铁生，1983 年测点示意页；2026 年衡对比复核',
      point: 'LT01 主轴箱左端 L1',
      feature: '对主轴后轴承信噪比高于 R1',
      binds: ['LT01_SPDL_BRG_RR_VIB'],
    },
    ZHOU_CLICK: {
      id: 'ZHOU_CLICK', type: '诊断',
      source: '周铁生，1983 年设备记录批注；2026 年林小满复测',
      point: 'LT01 主轴箱左端 L1',
      feature: '本机约 2–4kHz 结构共振带内的周期冲击',
      binds: ['LT01_SPDL_BRG_RR_IMP_ALM', 'LT01_GB_GMF_SB_IDX'],
    },
    JIANGUO_GUARD: { id: 'JIANGUO_GUARD', type: '安全联锁' },
    SUN_HALFBEAT: { id: 'SUN_HALFBEAT', type: '控制时序' },
  },

  levels: ['学徒', '一级', '二级', '三级', '四级'],

  /* 三级提示（谜题二，周建国笔迹） */
  hints: [
    { at: 90, text: '咔哒在哪边最清楚？' },
    { at: 180, text: '打开时频图，找一下下竖着的亮条。' },
    { at: 300, text: '翻到一九八三年那页，老周写过。' },
  ],

  /* 衡日志：开局已有的历史（谜题一已完成） */
  logBoot: [
    ['16:00', 'SYS', '中班开始。班长 周建国，调试 林小满。'],
    ['16:04', 'MON', 'LT01 主轴后轴承：R1 信号以齿轮啮合为主，轴承特征不可读。'],
    ['16:21', 'RULE', '经验规则 ZHOU_LEFT 已确认。来源 周铁生。', 'ok'],
    ['16:21', 'SYS', '交接 1/4。等级：学徒 → 一级。'],
    ['17:31', 'DIAG', '通用模型判定：主轴变速齿轮磨损（置信度 0.71）。', 'warn'],
    ['17:31', 'DIAG', '建议停机更换齿轮。', 'warn'],
  ],

  /* 人物档案（UI-DS §5.6；禁写政治面貌、家庭成分、奖惩记录） */
  people: [
    {
      key: 'zhou', name: '周铁生', tab: '周铁生', state: '完整',
      rows: [
        ['性别', '男'], ['出生年月', '一九三八年'], ['进厂年月', '一九五六年'],
        ['工种', '车工'], ['技术等级', '八级'],
      ],
      records: [
        ['一九五六', '进厂当学徒，参加三号车间建线。'],
        ['一九六五', '第一次以师傅身份带徒，签师徒合同。'],
        ['一九八三', 'LT01 听音标定：定下 L1 测点，记下“咔哒在左”。'],
      ],
      rules: [['ZHOU_LEFT', true], ['ZHOU_CLICK', 'dyn']],
      note: '退休：一九九八年',
    },
    {
      key: 'jian', name: '周建国', tab: '周建国', state: '部分',
      rows: [
        ['性别', '男'], ['出生年月', '一九六六年五月'], ['进厂年月', '一九八四年'],
        ['工种', '车工（二〇〇八年起任班长）'], ['技术等级', null],
      ],
      records: [
        ['一九八四', '技校毕业进厂，当学徒。'],
        [null, null],
        ['二〇〇八', '任三号车间班长。'],
      ],
      rules: [['JIANGUO_GUARD', false]],
      note: '本班交班人',
    },
    {
      key: 'sun', name: '孙桂兰', tab: '孙桂兰', state: '未解锁',
      rows: [
        ['性别', '女'], ['出生年月', '一九五六年'], ['进厂年月', null],
        ['工种', '天车工（一九七六年起）'], ['技术等级', null],
      ],
      records: [[null, null], [null, null], [null, null]],
      rules: [['SUN_HALFBEAT', false]],
      note: '',
    },
  ],
};
