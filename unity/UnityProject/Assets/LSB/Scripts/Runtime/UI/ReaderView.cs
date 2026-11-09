// 《最后一次交班》Demo α —— 细看（阅读器，UI-DS §5.3：点击贴页放大到 900×600）
// 工作照、建线合影、点检单、衡的比对单（2D）。照片是灰盒示意（几何块拼的人和机床），正式版换手绘 / 做旧照片，不用 AI 生成的成品图。
using LSB.Core;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class ReaderView : ModalView
    {
        public override string Id => "reader";
        public ReaderView(GameSession g, UiRoot ui, ViewHost host) : base(g, ui, host, "reader") { }

        protected override void Build(string arg)
        {
            Panel.Clear();
            Panel.RemoveFromClassList("sheet"); Panel.RemoveFromClassList("photo-mode"); Panel.RemoveFromClassList("print");
            g.Content.ReaderText.TryGetValue(arg ?? "", out var lines);
            lines = lines ?? new[] { arg ?? "" };
            switch (arg)
            {
                case "photo": Photo(); break;
                case "photo1956": Group(); break;
                default: Panel.AddToClassList("sheet"); break;
                case "compare": Panel.AddToClassList("sheet"); Panel.AddToClassList("print"); break;
            }
            if (arg == "photo" || arg == "photo1956") Panel.AddToClassList("photo-mode");
            var cap = Box("reader-cap");
            cap.Add(L(lines[0], "f-serif-b", "reader-title"));
            for (int i = 1; i < lines.Length; i++)
            {
                var kv = lines[i].Split('|');
                if (kv.Length == 2)
                {
                    // “键|值”画成表格行（衡的比对单）
                    var r = Box("kv-row");
                    r.Add(L(kv[0], "kv-k")); r.Add(L(kv[1], "kv-v", arg == "compare" ? "f-sans" : "f-serif"));
                    r.EnableInClassList("warn", kv[1].Contains("缺"));
                    cap.Add(r);
                }
                else cap.Add(L(lines[i], arg == "checklist" && i == lines.Length - 1 ? "f-hand-zhou" : "f-serif", "reader-line"));
            }
            Panel.Add(cap);
            var foot = Box("reader-foot");
            if (arg == "photo" || arg == "photo1956") foot.Add(L("灰盒示意图　正式版换手绘", "placeholder-tag"));
            foot.Add(B("放下（Esc）", CloseSelf, "btn"));
            Panel.Add(foot);
        }

        static VisualElement At(VisualElement e, float x, float y, float w, float h)
        {
            e.style.position = Position.Absolute; e.style.left = x; e.style.top = y; e.style.width = w; e.style.height = h;
            return e;
        }

        /// <summary>夹在工牌套中的工作照：老周站在 LT01 前，右手扶刀架手柄，左手拿听针伸向主轴箱左端（事实链 B-1 / 谜题一三联 e2 的反证）。</summary>
        void Photo()
        {
            var ph = Box("photo-frame");
            var img = Box("photo-img");
            img.Add(At(Box("ph-machine"), 30, 150, 520, 150));          // 车床床身
            img.Add(At(Box("ph-headstock"), 30, 70, 170, 110));         // 主轴箱（左）
            img.Add(At(Box("ph-mark"), 44, 112, 14, 14));               // 左端测点
            img.Add(At(Box("ph-carriage"), 330, 110, 80, 60));          // 刀架
            img.Add(At(Box("ph-head"), 250, 20, 46, 50));               // 老周
            img.Add(At(Box("ph-body"), 236, 70, 76, 150));
            var armL = At(Box("ph-arm"), 120, 96, 130, 14); Rot(armL, 8); img.Add(armL);    // 左手 → 主轴箱左端
            var probe = At(Box("ph-probe"), 56, 112, 72, 4); Rot(probe, 4); img.Add(probe); // 听针
            var armR = At(Box("ph-arm"), 300, 100, 60, 14); Rot(armR, 20); img.Add(armR);   // 右手扶刀架手柄
            ph.Add(img);
            Panel.Add(ph);
        }

        void Group()
        {
            var ph = Box("photo-frame", "old");
            var img = Box("photo-img");
            for (int i = 0; i < 9; i++)
            {
                float x = 40 + i * 56, y = i % 2 == 0 ? 70 : 90;
                img.Add(At(Box("ph-head"), x + 8, y, 30, 34));
                img.Add(At(Box("ph-body"), x, y + 34, 46, 110));
            }
            img.Add(At(Box("ph-banner"), 60, 16, 440, 30));
            ph.Add(img);
            Panel.Add(ph);
        }
    }
}
