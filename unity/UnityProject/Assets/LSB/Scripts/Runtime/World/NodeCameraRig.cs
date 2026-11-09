// 《最后一次交班》Demo α —— 节点式固定机位（不是第一人称自由移动）
// 节点：位置 / 朝向 / 焦距全部取自白盒规格 §5；节点间平滑推移，耗时 = 距离 / 3m/s（0.8–2.5s），无手持晃动。
// 特写：围绕被看物体在限定范围内旋转（规格 §6），按住鼠标左键拖动；Esc 或右键退出。
using LSB.Core;
using UnityEngine;

namespace LSB.Unity
{
    [RequireComponent(typeof(Camera))]
    public sealed class NodeCameraRig : MonoBehaviour
    {
        Camera cam;
        /// <summary>
        /// 不在 Awake 里取：GameBootstrap（执行顺序 -100）的 Awake 会先于本组件的 Awake 调用 SnapToNode，
        /// 那时 Awake 里赋的值还是空的（2A 包在 Unity 2022.3 里报的 NullReferenceException 就是这个）。
        /// </summary>
        public Camera Cam { get { if (cam == null) { cam = GetComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.farClipPlane = 120f; } return cam; } }

        Vector3 fromPos, toPos;
        Quaternion fromRot, toRot;
        float fromFov, toFov;
        float t = 1f, dur = 1f;

        // 特写旋转
        CloseUpDef cu;
        float orbitYaw, orbitPitch;
        public bool InCloseUp => cu != null;
        public bool Moving => t < 1f;

        /// <summary>等效焦距（36mm 画幅）→ 垂直视场角，按当前画面宽高比换算。</summary>
        public static float VerticalFov(float focalMm, float aspect)
        {
            float hHalf = Mathf.Atan(18f / Mathf.Max(1f, focalMm));
            float vHalf = Mathf.Atan(Mathf.Tan(hHalf) / Mathf.Max(0.1f, aspect));
            return vHalf * 2f * Mathf.Rad2Deg;
        }

        /// <summary>规格 Yaw（0°=北，顺时针）/ Pitch（正=俯）→ Unity 旋转。规格 Z 北 = Unity +z，所以直接对应。</summary>
        public static Quaternion Rot(float yaw, float pitch) => Quaternion.Euler(pitch, yaw, 0f);

        void Awake() { _ = Cam; }

        public void SnapToNode(NodeDef n)
        {
            cu = null;
            transform.position = WorkshopRefs.V(n.Pos);
            transform.rotation = Rot(n.Yaw, n.Pitch);
            Cam.fieldOfView = VerticalFov(n.FocalMm, Cam.aspect);
            t = 1f;
        }

        public void MoveToNode(NodeDef n, float seconds)
        {
            cu = null;
            Begin(WorkshopRefs.V(n.Pos), Rot(n.Yaw, n.Pitch), VerticalFov(n.FocalMm, Cam.aspect), seconds);
        }

        public void MoveToCloseUp(CloseUpDef c, float seconds = 0.6f)
        {
            cu = c;
            orbitYaw = 0; orbitPitch = 0;
            CloseUpPose(out var p, out var r);
            Begin(p, r, VerticalFov(c.FocalMm, Cam.aspect), seconds);
        }

        /// <summary>任意机位（如拉总闸时朝向总闸）。</summary>
        public void MoveToPose(Vector3 pos, float yaw, float pitch, float focal, float seconds)
        {
            cu = null;
            Begin(pos, Rot(yaw, pitch), VerticalFov(focal, Cam.aspect), seconds);
        }

        void Begin(Vector3 p, Quaternion r, float fov, float seconds)
        {
            fromPos = transform.position; fromRot = transform.rotation; fromFov = Cam.fieldOfView;
            toPos = p; toRot = r; toFov = fov;
            dur = Mathf.Max(0.01f, seconds);
            t = 0f;
        }

        void CloseUpPose(out Vector3 pos, out Quaternion rot)
        {
            float yaw = cu.BaseYaw + orbitYaw;
            float pitch = Mathf.Clamp(cu.BasePitch + orbitPitch, cu.PitchMin, cu.PitchMax);
            rot = Rot(yaw, pitch);
            pos = WorkshopRefs.V(cu.Pivot) - rot * Vector3.forward * cu.Distance;
        }

        /// <summary>特写里拖动鼠标旋转（度）。</summary>
        public void Orbit(float dYaw, float dPitch)
        {
            if (cu == null || Moving) return;
            orbitYaw = Mathf.Clamp(orbitYaw + dYaw, -cu.YawRange, cu.YawRange);
            float basePitch = cu.BasePitch;
            orbitPitch = Mathf.Clamp(orbitPitch + dPitch, cu.PitchMin - basePitch, cu.PitchMax - basePitch);
            CloseUpPose(out var p, out var r);
            transform.position = p; transform.rotation = r;
        }

        void LateUpdate()
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / dur);
            float k = t * t * (3f - 2f * t);   // smoothstep：平滑推移，起止不突兀
            transform.position = Vector3.Lerp(fromPos, toPos, k);
            transform.rotation = Quaternion.Slerp(fromRot, toRot, k);
            Cam.fieldOfView = Mathf.Lerp(fromFov, toFov, k);
        }
    }
}
