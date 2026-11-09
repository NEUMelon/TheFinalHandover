#!/usr/bin/env python3
"""《最后一次交班》Demo α —— 把多名试玩者的游戏自动记录汇总成两张表（2F 可用性测试用）

用法：
    python3 tools/merge_playtest.py <放试玩记录的文件夹> [输出文件夹]

输入：游戏在每局结束（或退出）时导出的 <编号>_<时间>.csv（逐条事件）。
      Windows 上在 %USERPROFILE%\\AppData\\LocalLow\\<公司名>\\<产品名>\\playtest\\，本局完成卡上也写着路径。
输出（UTF-8 带 BOM，Excel 直接打开）：
    汇总_自动采集.csv   每人一行：是否通关、四题和结局耗时、提示次数、三联填错、空点、停得最久的地方
    卡点_自动采集.csv   每人每个卡点一行：停留时长、到第几级提示、空点次数、在哪个位置

只汇总程序客观记下的行为。观察、问卷、访谈由主持人填模板，这个脚本不碰、也不估填。
只用 Python 标准库。
"""
import csv
import glob
import os
import sys
from collections import Counter, defaultdict

PHASES = ["Intro", "P1", "P2", "P3", "P4", "Ending"]
PHASE_ZH = {"Intro": "开场", "P1": "谜题一", "P2": "谜题二", "P3": "谜题三", "P4": "谜题四", "Ending": "结局"}
# 玩家主动做的操作：两次之间的空档 = “不知道做什么”的候选
ACTIONS = {
    "interact", "interact_unreachable", "move", "tool", "closeup", "view_open", "page", "ledger_spread",
    "spectro_box", "deviation", "crane_trial", "triad_ok", "triad_wrong", "triad_locked", "deduction_check",
    "twin", "switch_pulled", "signed", "rule_named", "memory_skip", "space_highlight", "empty_click",
}


def read_events(path):
    with open(path, encoding="utf-8-sig", newline="") as f:
        rows = list(csv.DictReader(f))
    for r in rows:
        r["t"] = float(r.get("t_seconds") or 0)
    return rows


def tester_of(session):
    return session.split("_", 1)[0] if "_" in session else session


def phase_bounds(ev):
    """每一段的开始、结束时间（按 phase_start / phase_end）。"""
    start, end = {}, {}
    for e in ev:
        if e["type"] == "phase_start" and e["a"] not in start:
            start[e["a"]] = e["t"]
        if e["type"] == "phase_end" and e["a"] not in end:
            end[e["a"]] = e["t"]
    last = ev[-1]["t"] if ev else 0
    out = {}
    for p in PHASES:
        if p in start:
            out[p] = (start[p], end.get(p, last))
    return out


def stage_spans(ev):
    """卡点（stage 记录）→ 停留区间。"""
    spans, cur, t0 = [], None, 0.0
    for e in ev:
        if e["type"] == "stage":
            if cur is not None:
                spans.append((cur, t0, e["t"]))
            cur, t0 = e["a"], e["t"]
        elif e["type"] == "game_finished" and cur is not None:
            spans.append((cur, t0, e["t"]))
            cur = None
    if cur is not None and ev:
        spans.append((cur, t0, ev[-1]["t"]))
    return spans


def longest_gap(ev, phase):
    acts = [e for e in ev if e["phase"] == phase and e["type"] in ACTIONS]
    best = (0.0, "", "")
    for a, b in zip(acts, acts[1:]):
        gap = b["t"] - a["t"]
        if gap > best[0]:
            best = (gap, a["node"], f'{a["type"]}:{a["a"]}')
    return best


def summarize(path):
    ev = read_events(path)
    if not ev:
        return None, []
    session = ev[0]["session"]
    bounds = phase_bounds(ev)
    finished = any(e["type"] == "game_finished" for e in ev)
    dev = any(e["type"] == "dev_jump" for e in ev)
    row = {
        "编号": tester_of(session), "会话": session, "文件": os.path.basename(path),
        "通关": "是" if finished else "否", "用过开发菜单跳关": "是" if dev else "否",
        "总用时_秒": round(ev[-1]["t"]),
    }
    for p in PHASES:
        zh = PHASE_ZH[p]
        es = [e for e in ev if e["phase"] == p]
        hints = [e for e in es if e["type"] == "hint"]
        row[f"{zh}_用时_秒"] = round(bounds[p][1] - bounds[p][0]) if p in bounds else ""
        row[f"{zh}_提示L1"] = sum(1 for h in hints if h["b"].startswith("L1"))
        row[f"{zh}_提示L2"] = sum(1 for h in hints if h["b"].startswith("L2"))
        row[f"{zh}_提示L3"] = sum(1 for h in hints if h["b"].startswith("L3"))
        row[f"{zh}_手动要提示"] = sum(1 for h in hints if h["b"].endswith("manual"))
        row[f"{zh}_三联填错"] = sum(1 for e in es if e["type"] == "triad_wrong")
        row[f"{zh}_空点"] = sum(1 for e in es if e["type"] == "empty_click")
        row[f"{zh}_点不到"] = sum(1 for e in es if e["type"] == "interact_unreachable")
        gap, node, after = longest_gap(ev, p)
        row[f"{zh}_最长空档_秒"] = round(gap)
        row[f"{zh}_最长空档位置"] = node
        row[f"{zh}_最长空档前一步"] = after
    empties = Counter(e["node"] for e in ev if e["type"] == "empty_click")
    row["空点最多的位置"] = "；".join(f"{n}×{c}" for n, c in empties.most_common(3))
    row["跳过记忆显影"] = sum(1 for e in ev if e["type"] == "memory_skip")
    row["新规则命名"] = next((e["a"] for e in ev if e["type"] == "rule_named"), "")

    stages = []
    for st, t0, t1 in stage_spans(ev):
        within = [e for e in ev if t0 <= e["t"] < t1]
        lv = max((int(e["b"][1]) for e in within if e["type"] == "hint" and e["b"][:1] == "L"), default=0)
        nodes = Counter(e["node"] for e in within if e["type"] in ACTIONS)
        stages.append({
            "编号": row["编号"], "卡点": st, "停留_秒": round(t1 - t0, 1), "到第几级提示": lv,
            "空点": sum(1 for e in within if e["type"] == "empty_click"),
            "点不到": sum(1 for e in within if e["type"] == "interact_unreachable"),
            "主要位置": "；".join(f"{n}×{c}" for n, c in nodes.most_common(2)),
        })
    return row, stages


def write(path, rows):
    if not rows:
        return
    keys = list(rows[0].keys())
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, fieldnames=keys)
        w.writeheader()
        w.writerows(rows)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    src = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else src
    files = sorted(f for f in glob.glob(os.path.join(src, "*.csv"))
                   if not f.endswith("_summary.csv") and "自动采集" not in os.path.basename(f))
    rows, stages = [], []
    for f in files:
        r, s = summarize(f)
        if r:
            rows.append(r)
            stages.extend(s)
    os.makedirs(out, exist_ok=True)
    write(os.path.join(out, "汇总_自动采集.csv"), rows)
    write(os.path.join(out, "卡点_自动采集.csv"), stages)
    testers = defaultdict(int)
    for r in rows:
        testers[r["编号"]] += 1
    print(f"读了 {len(files)} 份记录，{len(testers)} 个编号：{'、'.join(sorted(testers))}")
    dup = [t for t, c in testers.items() if c > 1]
    if dup:
        print("注意：这些编号有不止一份记录（中途重开？），汇总里各占一行：" + "、".join(dup))
    print("输出：" + os.path.join(out, "汇总_自动采集.csv") + "、卡点_自动采集.csv")


if __name__ == "__main__":
    main()
