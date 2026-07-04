using System;
using System.IO;
using UnityEngine;

namespace VampFrost
{
    [Serializable]
    public class SaveData
    {
        // audio settings (persisted per audio spec)
        public float master = 0.8f, music = 0.75f, sfx = 0.9f, ui = 0.8f;
        public bool muteMaster, muteMusic, muteSfx, muteUi;
        // progression
        public int gold;
        public int[] bestWave = new int[6];
        public int runsPlayed;
    }

    public static class SaveSystem
    {
        public static SaveData Data { get; private set; } = new SaveData();
        static string Path => Application.persistentDataPath + "/vampfrost_save.json";

        public static void Load()
        {
            try
            {
                if (File.Exists(Path))
                {
                    Data = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path)) ?? new SaveData();
                    if (Data.bestWave == null || Data.bestWave.Length < 6) Data.bestWave = new int[6];
                }
            }
            catch (Exception e) { Debug.LogWarning("[VampFrost] Save load failed: " + e.Message); Data = new SaveData(); }
        }

        public static void Save()
        {
            try { File.WriteAllText(Path, JsonUtility.ToJson(Data, true)); }
            catch (Exception e) { Debug.LogWarning("[VampFrost] Save failed: " + e.Message); }
        }
    }
}
