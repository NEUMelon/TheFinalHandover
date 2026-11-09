// 《最后一次交班》Demo α —— 场景里的标记组件
using System.Collections.Generic;
using LSB.Core;
using UnityEngine;

namespace LSB.Unity
{
    /// <summary>挂在可交互物上：ID 对应 ContentDB.Interactables。碰撞盒尺寸按白盒规格 §7。</summary>
    public sealed class InteractableTag : MonoBehaviour
    {
        public string Id;
        public string DisplayName;
        public int SpecNo;
        public bool PuzzleRelevant;
        /// <summary>高亮哪些渲染体（为空 = 自己和子物体）。大件设备的交互区只是一个碰撞盒，高亮的是设备本身的部件。</summary>
        public Renderer[] Targets;

        Renderer[] renderers;
        Material[][] original;
        Material[][] lit;
        bool highlighted;

        // 不在 Awake 里缓存：生成器 AddComponent 之后才会填 Targets

        void Cache()
        {
            var list = new List<Renderer>(GetComponentsInChildren<Renderer>());
            if (Targets != null) foreach (var r in Targets) if (r != null && !list.Contains(r)) list.Add(r);
            renderers = list.ToArray();
            original = new Material[renderers.Length][];
            lit = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                original[i] = renderers[i].sharedMaterials;
                lit[i] = new Material[original[i].Length];
                for (int k = 0; k < original[i].Length; k++) lit[i][k] = original[i][k] == null ? null : GrayboxMaterials.Highlight(original[i][k]);
            }
        }

        public void SetHighlight(bool on)
        {
            if (renderers == null) Cache();
            if (highlighted == on) return;
            highlighted = on;
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].sharedMaterials = on ? lit[i] : original[i];
        }
    }

    /// <summary>灰盒根节点上的引用表：表现层通过它找到会动的部件。</summary>
    public sealed class WorkshopRefs : MonoBehaviour
    {
        public readonly Dictionary<string, InteractableTag> Interactables = new Dictionary<string, InteractableTag>();
        public readonly List<Light>[] LampRows = new List<Light>[6];      // X = 2, 6, 10, 14, 18, 22（结局由 X22 → X2 熄灭）
        public readonly List<Renderer>[] LampShades = new List<Renderer>[6];
        public Renderer[] HandoverLamps = new Renderer[4];                 // 交接状态板 4 灯
        public Renderer[] MesScreens;
        public Transform CraneBridge, CraneTrolley, CraneRope, CraneHook, CraneMug, MugWater;
        public Transform GuardHinge;
        public Transform Spindle;
        public Transform MainSwitchHandle;
        public Transform LockerDoor, DrawerBox, CabinetDoor;
        public Light SunLight;
        public Light[] WindowShafts;
        public GameObject LabelsRoot;

        /// <summary>
        /// 场景存盘后 Dictionary / List 数组不会被序列化：运行时从子物体重新建索引。
        /// 灯排按名字找（“灯排_1_X2”“灯罩_X2_Z4.5”），名字由 WorkshopGraybox 生成。
        /// </summary>
        public void Reindex()
        {
            Interactables.Clear();
            foreach (var t in GetComponentsInChildren<InteractableTag>(true)) Register(t);
            float[] xs = { 2, 6, 10, 14, 18, 22 };
            for (int r = 0; r < 6; r++)
            {
                LampRows[r] = new List<Light>();
                LampShades[r] = new List<Renderer>();
            }
            foreach (var l in GetComponentsInChildren<Light>(true))
                for (int r = 0; r < 6; r++) if (l.name.StartsWith($"灯排_{r + 1}_")) LampRows[r].Add(l);
            foreach (var rd in GetComponentsInChildren<Renderer>(true))
                for (int r = 0; r < 6; r++) if (rd.name.StartsWith($"灯罩_X{xs[r]:0}_")) LampShades[r].Add(rd);
        }

        public void Register(InteractableTag t) { if (t != null && !string.IsNullOrEmpty(t.Id)) Interactables[t.Id] = t; }

        public static Vector3 V(V3 v) => new Vector3(v.x, v.y, v.z);
    }
}
