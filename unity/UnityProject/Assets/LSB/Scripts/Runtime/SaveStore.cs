// 《最后一次交班》Demo α —— 存档 / 试玩记录落盘
// 存档：Application.persistentDataPath/lsb_demo_alpha.save（纯文本，SaveCodec）。
// 试玩记录：persistentDataPath/playtest/<会话>.csv 与 _summary.csv。只记录游戏内自动可得的客观行为，不含任何主观评价。
using System;
using System.IO;
using LSB.Core;
using UnityEngine;

namespace LSB.Unity
{
    public static class SaveStore
    {
        static string SavePath => Path.Combine(Application.persistentDataPath, "lsb_demo_alpha.save");
        public static string PlaytestDir => Path.Combine(Application.persistentDataPath, "playtest");

        public static bool HasSave => File.Exists(SavePath);

        public static void Save(GameSession g)
        {
            try { g.Save(t => File.WriteAllText(SavePath, t)); }
            catch (Exception e) { Debug.LogWarning("[LSB] 存档失败：" + e.Message); }
        }

        public static bool TryLoad(GameSession g)
        {
            try
            {
                if (!HasSave) return false;
                var s = SaveCodec.Decode(File.ReadAllText(SavePath));
                if (s == null) return false;
                g.ReplaceState(s);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[LSB] 读档失败：" + e.Message); return false; }
        }

        public static string ExportPlaytest(GameSession g)
        {
            try
            {
                Directory.CreateDirectory(PlaytestDir);
                var id = string.IsNullOrEmpty(g.Playtest.SessionId) ? "session" : g.Playtest.SessionId;
                var log = Path.Combine(PlaytestDir, id + ".csv");
                File.WriteAllText(log, g.Playtest.ToCsv());
                File.WriteAllText(Path.Combine(PlaytestDir, id + "_summary.csv"), g.Playtest.SummaryCsv());
                return log;
            }
            catch (Exception e) { Debug.LogWarning("[LSB] 导出试玩记录失败：" + e.Message); return null; }
        }
    }
}
