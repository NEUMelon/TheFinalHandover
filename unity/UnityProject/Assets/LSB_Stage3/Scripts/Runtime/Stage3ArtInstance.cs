// 《最后一次交班》Stage 3 · 场景里每个 3D 资产实例的接线数据
// 由菜单 LSB/Stage3/摆放 3D 资产 挂到实例外壳上，内容来自资产目录里的 unity_manifest.json（20_场景地编/stage3/**/unity_manifest.json，同步时拷进 Assets/LSB_Art/Env/<资产>/）。
// 运行时 Stage3ArtBinder 读它：隐藏被替换的灰盒渲染、让模型部件跟着灰盒会动的部件走、发光部件跟着灰盒灯罩亮灭。
// 由 Claude 编写（AI 辅助代码，记入 AI 使用日志）。
using System;
using UnityEngine;

namespace LSB.Stage3
{
    public sealed class Stage3ArtInstance : MonoBehaviour
    {
        public string Asset;
        public string InstanceName;
        [TextArea(3, 20)] public string ManifestJson;

        public Stage3Manifest Parse()
        {
            if (string.IsNullOrEmpty(ManifestJson)) return null;
            try { return JsonUtility.FromJson<Stage3Manifest>(ManifestJson); }
            catch (Exception e) { Debug.LogWarning($"[LSB Stage3] {name} 的 manifest 解析失败：{e.Message}"); return null; }
        }
    }

    [Serializable]
    public sealed class Stage3Manifest
    {
        public string asset;
        public string fbx;
        public string[] textures;
        public Stage3ManifestInstance[] instances;
        public string[] hide_graybox;
        public Stage3ManifestFollow[] follow;
        public Stage3ManifestEmission[] mirror_emission;
        public Stage3ManifestHighlight[] highlight;   // 交互物 ID → 模型部件：鼠标指到时高亮模型
        public string notes;
    }

    [Serializable]
    public sealed class Stage3ManifestInstance
    {
        public string name;
        public float[] pos;
        public float yaw;
    }

    [Serializable]
    public sealed class Stage3ManifestFollow
    {
        public string part;
        public string gray;
        public string mode;   // rigid / rot
    }

    [Serializable]
    public sealed class Stage3ManifestHighlight
    {
        public string id;
        public string part;
        public string instance;
    }

    [Serializable]
    public sealed class Stage3ManifestEmission
    {
        public string instance;
        public string part;
        public string gray;
    }
}
