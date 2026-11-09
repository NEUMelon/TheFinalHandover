// 《最后一次交班》Demo α —— UI Toolkit 根节点
// 选 UI Toolkit（不是 uGUI）：UI-DS §8 规定“USS 变量与本文件 Token 同名”，原型的 CSS Token 可以一一搬成 USS 变量。
// 分层：世界 HUD → 面板（交班本、三联板、衡屏幕…）→ 覆盖层（过场卡、记忆显影、结局）。开发菜单是独立的 IMGUI，不在这里。
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class UiRoot : MonoBehaviour
    {
        public const string PanelResource = "LSB/UI/LSB_Panel";
        public const string ThemeResource = "LSB/UI/LSB_Theme";

        public UIDocument Doc { get; private set; }
        public VisualElement Root { get; private set; }
        public VisualElement HudLayer { get; private set; }
        public VisualElement ViewLayer { get; private set; }
        public VisualElement OverlayLayer { get; private set; }

        public static UiRoot Create(Transform parent)
        {
            // 先关着物体再挂 UIDocument：面板设置要在 UIDocument.OnEnable 之前给好，否则根节点可能还没接到面板上
            var go = new GameObject("LSB_UI");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = LoadPanel();
            var ui = go.AddComponent<UiRoot>();
            go.SetActive(true);
            ui.Init();
            return ui;
        }

        static PanelSettings LoadPanel()
        {
            var ps = Resources.Load<PanelSettings>(PanelResource);
            if (ps != null) return ps;
            // 编辑器菜单 “LSB/生成 UI 面板设置” 会把它存成资源；没生成时运行时临时建一个，效果相同
            ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "LSB_Panel (runtime)";
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);   // UI-DS 字号按 1080p 像素定义
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemeResource);
            return ps;
        }

        void Init()
        {
            Doc = GetComponent<UIDocument>();
            Root = Doc.rootVisualElement;
            Root.Clear();
            foreach (var n in new[] { "LSB/UI/lsb_tokens", "LSB/UI/lsb_hud", "LSB/UI/lsb_views" })
            {
                var ss = Resources.Load<StyleSheet>(n);
                if (ss != null) Root.styleSheets.Add(ss);
            }
            var app = new VisualElement { name = "lsb-root", pickingMode = PickingMode.Ignore };
            app.AddToClassList("lsb-root");
            Root.Add(app);
            HudLayer = Layer(app, "hud");
            ViewLayer = Layer(app, "views");
            OverlayLayer = Layer(app, "overlay");
        }

        static VisualElement Layer(VisualElement p, string name)
        {
            var v = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            v.AddToClassList("layer");
            p.Add(v);
            return v;
        }

        /// <summary>鼠标是否点在 UI 上（是的话场景里不做拾取）。</summary>
        public bool PointerOverUi(Vector2 screenPos)
        {
            var panel = Root?.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var picked = panel.Pick(p);
            return picked != null && picked != Root;
        }

        // ---------------- 小工具 ----------------
        public static Label L(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        public static VisualElement Box(params string[] classes)
        {
            var v = new VisualElement();
            foreach (var c in classes) v.AddToClassList(c);
            return v;
        }

        public static Button B(string text, System.Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text };
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        public static void Show(VisualElement v, bool on) => v.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
