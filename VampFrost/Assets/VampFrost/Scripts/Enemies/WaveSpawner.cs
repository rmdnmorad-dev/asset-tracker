using UnityEngine;

namespace VampFrost
{
    public class WaveSpawner : MonoBehaviour
    {
        public static WaveSpawner I;
        const int CAP = 240;

        float spawnT;
        int lastWave;
        bool finalBossOut, miniOut;

        public static WaveSpawner Create(Transform worldRoot)
        {
            var go = new GameObject("WaveSpawner");
            go.transform.SetParent(worldRoot, false);
            var s = go.AddComponent<WaveSpawner>();
            I = s;
            return s;
        }

        void OnDestroy() { if (I == this) I = null; }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.state != GameManager.State.Playing) return;
            var map = gm.Map;
            float dt = Time.deltaTime;
            int wave = gm.wave;

            if (wave != lastWave)
            {
                if (lastWave > 0) GameEvents.OnWaveEnd?.Invoke(lastWave);
                lastWave = wave;
                GameEvents.OnWaveStart?.Invoke(wave);

                if (wave > SaveSystem.Data.bestWave[map.id])
                { SaveSystem.Data.bestWave[map.id] = wave; SaveSystem.Save(); }

                if (wave % 5 == 0 && wave < GameManager.MaxWave)
                {
                    if (map.id == 4 && wave == 15 && !miniOut)
                    { miniOut = true; Boss.Spawn(EnemyDefs.Bosses[5], RingPos(), true); } // High Commando Maniac
                    else
                        Enemy.Spawn(EnemyDefs.Mobs[map.mobs[Random.Range(0, map.mobs.Length)]],
                                    RingPos(), wave, true);
                }

                if (wave >= GameManager.MaxWave && !finalBossOut)
                {
                    finalBossOut = true;
                    Boss.Spawn(EnemyDefs.Bosses[map.bossId], RingPos(), false);
                }
            }

            if (finalBossOut) return; // full attention on the boss

            spawnT -= dt;
            if (spawnT <= 0 && Enemy.Active.Count < Mathf.Min(CAP, 50 + wave * 10))
            {
                spawnT = Mathf.Max(.12f, 1.15f - wave * .05f);
                int batch = 1 + wave / 6;
                for (int i = 0; i < batch; i++)
                {
                    // heavier mobs get rarer weighting early
                    int idx = Random.value < .55f ? 0 : Random.value < .7f ? 1 : 2;
                    Enemy.Spawn(EnemyDefs.Mobs[map.mobs[idx]], RingPos(), wave, false);
                }
            }
        }

        static Vector2 RingPos()
        {
            Vector2 c = PlayerController.I != null ? (Vector2)PlayerController.I.transform.position : Vector2.zero;
            float a = Random.value * Mathf.PI * 2f;
            return c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(12f, 14f);
        }
    }
}
