// 《最后一次交班》Demo α —— 场景表现：相机走位、会动的部件、灯光预设、交接灯、天车试吊回放、结局熄灯
// 只订阅逻辑层事件，不改逻辑状态。
using System.Collections;
using LSB.Core;
using UnityEngine;

namespace LSB.Unity
{
    public sealed class WorldPresenter : MonoBehaviour
    {
        public GameSession Session;
        public NodeCameraRig Rig;
        public WorkshopRefs World;
        public ProceduralAudio Audio;

        Coroutine crane;
        float spindleSpeed = 360f;   // 度/秒（灰盒示意）

        public void Bind(GameSession g, NodeCameraRig rig, WorkshopRefs world, ProceduralAudio audio)
        {
            Session = g; Rig = rig; World = world; Audio = audio;
            g.Bus.Raised += OnEvent;
            SyncAll(true);
        }

        void OnDestroy() { if (Session != null) Session.Bus.Raised -= OnEvent; }

        /// <summary>读档 / 跳关之后把场景摆成状态对应的样子。</summary>
        public void SyncAll(bool snap)
        {
            var s = Session.State;
            var n = Session.Content.Node(s.Node);
            if (n != null) { if (snap) Rig.SnapToNode(n); else Rig.MoveToNode(n, 0.6f); }
            if (s.CloseUp != null) Rig.MoveToCloseUp(Session.Content.CloseUp(s.CloseUp), snap ? 0.01f : 0.6f);
            SetDoor(World.LockerDoor, s.Flag("locker_open") ? 105f : 0f, true);
            SetDoor(World.CabinetDoor, s.Flag("cabinet_open") ? -100f : 0f, true);
            if (World.DrawerBox != null) World.DrawerBox.localPosition = new Vector3(26.55f + (s.Flag("drawer_open") ? 0.3f : 0f), 0.62f, 13.55f);
            for (int i = 0; i < 4; i++) SetLamp(i, s.Lamps[i]);
            ApplyLight(Session.Timeline.LightPreset);
            if (s.Phase < Phase.Ending || s.Ending < EndingStep.LightsOut) SetRows(true);
            else
            {
                // 读档时停在结局过场里：灯已经熄了；衡启动以后是冷白
                spindleSpeed = 0;
                SetRows(false);
                if (World.SunLight) World.SunLight.enabled = false;
                RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.03f);
                if (s.Ending >= EndingStep.HengBoot) HengBoot();
            }
        }

        void OnEvent(GameEvent e)
        {
            var s = Session.State;
            switch (e.Type)
            {
                case Ev.NodeChanged:
                    var to = Session.Content.Node(e.A); var from = Session.Content.Node(e.B);
                    if (to != null) Rig.MoveToNode(to, from == null ? 0.8f : NavigationSystem.MoveSeconds(from, to));
                    break;
                case Ev.CloseUpChanged:
                    if (e.B == "enter") Rig.MoveToCloseUp(Session.Content.CloseUp(e.A));
                    else if (s.CloseUp == null) Rig.MoveToNode(Session.Nav.Current, 0.6f);
                    break;
                case Ev.PhaseChanged:
                    if (e.B == "load" || e.B == "new") SyncAll(true);
                    break;
                case Ev.FlagChanged:
                    if (e.A == "locker_open") StartCoroutine(Swing(World.LockerDoor, e.B == "1" ? 105f : 0f));
                    if (e.A == "cabinet_open") StartCoroutine(Swing(World.CabinetDoor, e.B == "1" ? -100f : 0f));
                    if (e.A == "drawer_open" && World.DrawerBox != null) StartCoroutine(Slide(World.DrawerBox, new Vector3(26.85f, 0.62f, 13.55f)));
                    break;
                case Ev.ClockChanged: ApplyLight(e.B); break;
                case Ev.LampOff: SetLamp(e.N, false); break;
                case Ev.OpenView:
                    if (e.A == "switch") { var p = Session.Content.SwitchPose; Rig.MoveToPose(WorkshopRefs.V(p.Pos), p.Yaw, p.Pitch, p.FocalMm, 0.8f); }
                    break;
                case Ev.CraneTrial:
                    if (Session.Puzzles.LastTrial != null) { if (crane != null) StopCoroutine(crane); crane = StartCoroutine(PlayTrial(Session.Puzzles.LastTrial)); }
                    break;
                case Ev.EndingStepChanged:
                    switch ((EndingStep)e.N)
                    {
                        case EndingStep.LightsOut:
                            // 白盒规格 §8 结局第 3 步：拉完闸转回 270°，固定长镜头看逐排熄灯
                            Rig.MoveToNode(Session.Nav.Current, 0.8f);
                            StartCoroutine(SwitchOff());
                            break;
                        case EndingStep.HengBoot: HengBoot(); break;
                        case EndingStep.Credits: StartCoroutine(WalkOut()); break;
                    }
                    break;
            }
        }

        void Update()
        {
            if (World != null && World.Spindle != null && spindleSpeed > 0.1f) World.Spindle.Rotate(Vector3.up, spindleSpeed * Time.deltaTime, Space.Self);
        }

        // ---------------- 部件 ----------------
        static void SetDoor(Transform hinge, float yaw, bool snap)
        {
            if (hinge != null) hinge.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        static IEnumerator Swing(Transform hinge, float yaw)
        {
            if (hinge == null) yield break;
            var a = hinge.localRotation; var b = Quaternion.Euler(0, yaw, 0);
            for (float t = 0; t < 1; t += Time.deltaTime / 0.5f) { hinge.localRotation = Quaternion.Slerp(a, b, t * t * (3 - 2 * t)); yield return null; }
            hinge.localRotation = b;
        }

        static IEnumerator Slide(Transform tr, Vector3 to)
        {
            var a = tr.localPosition;
            for (float t = 0; t < 1; t += Time.deltaTime / 0.35f) { tr.localPosition = Vector3.Lerp(a, to, t); yield return null; }
            tr.localPosition = to;
        }

        void SetLamp(int i, bool lit)
        {
            if (World.HandoverLamps == null || i < 0 || i >= World.HandoverLamps.Length || World.HandoverLamps[i] == null) return;
            World.HandoverLamps[i].sharedMaterial = lit
                ? GrayboxMaterials.Get(GrayboxMaterials.LampOn, 0.5f, 0, GrayboxMaterials.LampOn * 1.5f)
                : GrayboxMaterials.Get(GrayboxMaterials.Dark, 0.5f);
        }

        // ---------------- 灯光预设（剧情时间） ----------------
        void ApplyLight(string preset)
        {
            var sun = World.SunLight;
            switch (preset)
            {
                case "day1600":
                    if (sun) { sun.enabled = true; sun.intensity = 1.6f; sun.color = new Color(1f, 0.82f, 0.58f); sun.transform.localRotation = Quaternion.Euler(18, 55, 0); }
                    RenderSettings.ambientLight = new Color(0.36f, 0.35f, 0.33f);
                    SetRowIntensity(0.6f);
                    break;
                case "dusk1730":
                    if (sun) { sun.enabled = true; sun.intensity = 0.8f; sun.color = new Color(1f, 0.62f, 0.38f); sun.transform.localRotation = Quaternion.Euler(6, 60, 0); }
                    RenderSettings.ambientLight = new Color(0.26f, 0.24f, 0.24f);
                    SetRowIntensity(1.0f);
                    break;
                case "night":
                    if (sun) sun.enabled = false;
                    RenderSettings.ambientLight = new Color(0.08f, 0.09f, 0.12f);
                    SetRowIntensity(1.5f);
                    break;
            }
        }

        void SetRowIntensity(float k)
        {
            foreach (var row in World.LampRows) if (row != null) foreach (var l in row) if (l != null) l.intensity = k;
        }

        void SetRows(bool on)
        {
            for (int r = 0; r < 6; r++) SetRow(r, on);
        }

        void SetRow(int r, bool on)
        {
            if (World.LampRows[r] != null) foreach (var l in World.LampRows[r]) if (l != null) l.enabled = on;
            if (World.LampShades[r] != null) foreach (var sh in World.LampShades[r])
                    if (sh != null) sh.sharedMaterial = on ? GrayboxMaterials.Get(GrayboxMaterials.Enamel, 0.6f, 0, new Color(1f, 0.85f, 0.6f) * 0.8f) : GrayboxMaterials.Get(GrayboxMaterials.Enamel, 0.6f);
        }

        // ---------------- 结局：主轴降速，灯由近及远（X 22 → X 2）每排 0.6 秒熄灭 ----------------
        IEnumerator SwitchOff()
        {
            for (float t = 0; t < 1.5f; t += Time.deltaTime) { spindleSpeed = Mathf.Lerp(360, 0, t / 1.5f); yield return null; }
            spindleSpeed = 0;
            foreach (var s in World.MesScreens ?? new Renderer[0]) if (s) s.sharedMaterial = GrayboxMaterials.Get(GrayboxMaterials.Dark, 0.8f);
            for (int r = 5; r >= 0; r--) { SetRow(r, false); Audio?.Play("lamp_off", 0.5f); yield return new WaitForSeconds(0.6f); }
            if (World.SunLight) World.SunLight.enabled = false;
            RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.03f);
        }

        void HengBoot()
        {
            RenderSettings.ambientLight = new Color(0.30f, 0.36f, 0.42f);   // 冷白
            foreach (var s in World.MesScreens ?? new Renderer[0]) if (s) s.sharedMaterial = GrayboxMaterials.Get(GrayboxMaterials.Screen, 0.8f, 0, GrayboxMaterials.Screen * 1.4f);
            for (int r = 0; r < 6; r++) if (World.LampRows[r] != null) foreach (var l in World.LampRows[r]) if (l) { l.enabled = true; l.color = new Color(0.85f, 0.93f, 1f); l.intensity = 0.7f; }
        }

        /// <summary>
        /// 献词前的远景：周建国的背影从西大门走出去（白盒规格 §8 结局第 4 步，镜头回到交班桌朝西看）。
        /// 灰盒：一个深色胶囊体，不做角色模型。
        /// </summary>
        IEnumerator WalkOut()
        {
            var n1 = Session.Content.Node("N1");
            if (n1 != null) Rig.MoveToNode(n1, 1.5f);
            var who = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            who.name = "灰盒_周建国背影";
            Destroy(who.GetComponent<Collider>());
            who.GetComponent<Renderer>().sharedMaterial = GrayboxMaterials.Get(GrayboxMaterials.Dark, 0.9f);
            who.transform.localScale = new Vector3(0.5f, 0.85f, 0.5f);
            var a = new Vector3(5.0f, 0.85f, 8.5f); var b = new Vector3(-1.8f, 0.85f, 8.5f);
            for (float t = 0; t < 1; t += Time.deltaTime / 3.2f) { who.transform.position = Vector3.Lerp(a, b, t); yield return null; }
            Destroy(who);
        }

        // ---------------- 天车试吊回放（2E 会配上曲线面板；这里先让场景里的吊钩真的按仿真结果摆） ----------------
        IEnumerator PlayTrial(CraneTrial tr)
        {
            if (World.CraneBridge == null || tr.Time == null || tr.Time.Length == 0) yield break;
            float xEnd = tr.TrolleyX[tr.TrolleyX.Length - 1];
            var water = World.MugWater;
            if (water) water.gameObject.SetActive(true);
            for (int i = 0; i < tr.Time.Length; i++)
            {
                float th = tr.Angle[i];
                World.CraneBridge.localPosition = new Vector3(6.0f + (tr.TrolleyX[i] - xEnd), 0, 0);
                World.CraneRope.localRotation = Quaternion.FromToRotation(Vector3.down, new Vector3(Mathf.Sin(th), -Mathf.Cos(th), 0));
                if (water) water.localRotation = Quaternion.Euler(0, 0, -th * Mathf.Rad2Deg * 6f);   // 水面倾角夸大显示
                if (tr.Outcome == CraneOutcome.Spill && water && i > 0 && tr.Time[i] > tr.StopTime + tr.Period * 0.25f) water.gameObject.SetActive(false);
                yield return new WaitForSeconds(tr.SampleDt);
            }
            World.CraneRope.localRotation = Quaternion.identity;
            if (water) { water.localRotation = Quaternion.identity; water.gameObject.SetActive(true); }
        }
    }
}
