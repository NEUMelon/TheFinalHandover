// 《最后一次交班》Demo α —— 鼠标拾取与快捷键
// 只拾取当前节点 / 特写能点到的交互物（NavigationSystem.CanInteract），墙和地面挡住射线，避免隔着车间点到东西。
// 嵌套的物件（柜子里的手套、抽屉里的磁带）按“碰撞盒更小的优先”选中。
using System.Linq;
using LSB.Core;
using UnityEngine;

namespace LSB.Unity
{
    public sealed class InteractionController : MonoBehaviour
    {
        public GameSession Session;
        public NodeCameraRig Rig;
        public WorkshopRefs World;
        public UiRoot Ui;
        public HudView Hud;
        public ViewHost Views;

        InteractableTag hovered;
        bool spaceHeld;
        Vector3 downPos;
        bool dragging;
        readonly RaycastHit[] hits = new RaycastHit[32];

        public bool InputBlocked => Session == null || Views == null || Views.AnyOpen || MemoryOverlay.Active || EndingOverlay.Cinematic || (Hud != null && Hud.TransitionShowing) || Session.State.Phase == Phase.Title;

        void Update()
        {
            if (Session == null || Rig == null) return;
            Keys();
            var mouse = Input.mousePosition;
            bool overUi = Ui != null && Ui.PointerOverUi(mouse);

            if (InputBlocked || Rig.Moving) { SetHover(null, mouse); return; }

            // 特写：按住左键拖动旋转
            if (Input.GetMouseButtonDown(0)) { downPos = mouse; dragging = false; }
            if (Input.GetMouseButton(0) && Rig.InCloseUp && !overUi)
            {
                var d = mouse - downPos;
                if (d.magnitude > 6f) dragging = true;
                if (dragging) Rig.Orbit(Input.GetAxis("Mouse X") * 3f, -Input.GetAxis("Mouse Y") * 2f);
            }

            var tag = overUi ? null : Pick(mouse);
            SetHover(tag, mouse);

            if (Input.GetMouseButtonUp(0) && !overUi && !dragging)
            {
                if (tag != null) Session.Interaction.Use(tag.Id);
                else Session.Playtest.EmptyClick(mouse.x / Mathf.Max(1, Screen.width), mouse.y / Mathf.Max(1, Screen.height));
            }
            if (Input.GetMouseButtonUp(1) && Rig.InCloseUp) Session.Nav.ExitCloseUp();
        }

        void Keys()
        {
            if (MemoryOverlay.Active) { SetSpace(false); return; }   // 记忆显影自己处理 Esc（跳过）
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Views != null && Views.CloseTop()) return;
                if (Session.State.CloseUp != null) Session.Nav.ExitCloseUp();
            }
            // Tab：交班本开 / 合（UI-DS §5.1）。交班本是主界面，在哪儿都能翻
            if (Input.GetKeyDown(KeyCode.Tab) && Views != null && Session.State.Phase > Phase.Title && Session.State.Phase < Phase.Done)
            {
                if (Views.IsOpen("ledger")) Views.CloseTop();
                else if (!InputBlocked) Session.OpenView("ledger");
                return;
            }
            if (InputBlocked) { SetSpace(false); return; }
            if (Input.GetKeyDown(KeyCode.Alpha1)) Session.Interaction.SelectTool(ToolId.Probe);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Session.Interaction.SelectTool(ToolId.Torch);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Session.Interaction.SelectTool(ToolId.Caliper);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Session.Interaction.SelectTool(ToolId.Driver);
            if (Input.GetKeyDown(KeyCode.L) && Session.State.Node == "N1" && Session.State.Phase >= Phase.Intro) Session.Interaction.Use("ledger");
            SetSpace(Input.GetKey(KeyCode.Space));
        }

        /// <summary>按住空格：当前位置所有能点的东西一起亮（可访问性 / 卡关时找物件）。</summary>
        void SetSpace(bool on)
        {
            if (on == spaceHeld || World == null) return;
            spaceHeld = on;
            foreach (var t in World.Interactables.Values)
                if (t != hovered) t.SetHighlight(on && Session.Nav.CanInteract(t.Id));
            if (on) Session.Playtest.Record("space_highlight");
        }

        InteractableTag Pick(Vector3 mouse)
        {
            var ray = Rig.Cam.ScreenPointToRay(mouse);
            int n = Physics.RaycastNonAlloc(ray, hits, 60f, ~0, QueryTriggerInteraction.Collide);
            float blocker = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (hits[i].collider.GetComponent<InteractableTag>() == null) blocker = Mathf.Min(blocker, hits[i].distance);
            InteractableTag best = null; float bestVol = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var t = hits[i].collider.GetComponent<InteractableTag>();
                if (t == null || hits[i].distance > blocker + 0.02f) continue;
                if (!Session.Nav.CanInteract(t.Id)) continue;
                var b = hits[i].collider.bounds.size;
                float vol = b.x * b.y * b.z;
                if (vol < bestVol) { bestVol = vol; best = t; }
            }
            return best;
        }

        void SetHover(InteractableTag t, Vector3 mouse)
        {
            if (hovered != t)
            {
                if (hovered != null) hovered.SetHighlight(spaceHeld && Session.Nav.CanInteract(hovered.Id));
                hovered = t;
                if (hovered != null) hovered.SetHighlight(true);
            }
            if (Hud == null) return;
            if (t == null) { Hud.Hover(null, false, mouse); return; }
            var tool = Session.State.Tool;
            bool act = Session.Interaction.Match(t.Id, tool) != null;
            string toolHint = tool == ToolId.None ? "" : "　〔" + InteractionSystem.ToolName(tool) + "〕";
            Hud.Hover(t.DisplayName + toolHint, act, mouse);
        }
    }
}
