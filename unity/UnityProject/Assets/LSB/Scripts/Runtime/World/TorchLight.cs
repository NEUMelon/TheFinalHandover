// 《最后一次交班》Demo α —— 手电（工具 2）
// 选中手电时，相机上挂一盏窄角聚光灯，跟着鼠标指的方向照。谜题一找老测点、谜题三照凹痕都靠它（灰盒：一盏实时聚光灯，不做体积光）。
using LSB.Core;
using UnityEngine;

namespace LSB.Unity
{
    public sealed class TorchLight : MonoBehaviour
    {
        public GameSession Session;
        public NodeCameraRig Rig;
        Light spot;

        void Start()
        {
            var go = new GameObject("LSB_手电");
            go.transform.SetParent(Rig.transform, false);
            spot = go.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 22f;
            spot.innerSpotAngle = 10f;
            spot.range = 14f;
            spot.intensity = 6f;
            spot.color = new Color(1f, 0.93f, 0.8f);
            spot.shadows = LightShadows.Soft;
            spot.enabled = false;
        }

        void LateUpdate()
        {
            if (spot == null || Session == null) return;
            bool on = Session.State.Tool == ToolId.Torch && Session.State.Phase != Phase.Title;
            spot.enabled = on;
            if (!on) return;
            var ray = Rig.Cam.ScreenPointToRay(Input.mousePosition);
            var target = Quaternion.LookRotation(ray.direction);
            spot.transform.rotation = Quaternion.Slerp(spot.transform.rotation, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
        }
    }
}
