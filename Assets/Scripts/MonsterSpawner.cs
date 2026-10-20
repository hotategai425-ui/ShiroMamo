using UnityEngine;

// 戦闘フェーズ中、スポーン地点からモンスターを出現させる
public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private MonsterData[] monsterTypes;
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("同時に存在できる最大数")]
    [SerializeField] private int maxAlive = 40;
    [SerializeField] private float spawnScatter = 1.5f;

    private float timer;

    private void Update()
    {
        var wm = WaveManager.Instance;
        if (wm == null || wm.Phase != GamePhase.Combat) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = wm.CurrentSetting.spawnInterval;
        if (Monster.All.Count < maxAlive)
            Spawn(wm.CurrentWave, wm.CurrentSetting);
    }

    private void Spawn(int wave, WaveManager.WaveSetting setting)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        MonsterData data = PickMonster(wave);
        if (data == null) return;

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Vector2 scatter = Random.insideUnitCircle * spawnScatter;
        Vector3 pos = point.position + new Vector3(scatter.x, 1f, scatter.y);

        Monster monster;
        if (data.prefab != null)
        {
            monster = Instantiate(data.prefab, pos, Quaternion.identity);
        }
        else
        {
            // プレハブ未設定なら仮のカプセルを生成
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = data.displayName;
            go.transform.position = pos;
            monster = go.AddComponent<Monster>();
        }
        monster.Init(data, setting);
    }

    // spawnWeight による重み付き抽選(firstWave 以降のみ)
    private MonsterData PickMonster(int wave)
    {
        float total = 0f;
        foreach (var m in monsterTypes)
            if (m != null && m.firstWave <= wave) total += m.spawnWeight;
        if (total <= 0f) return null;

        float r = Random.value * total;
        MonsterData last = null;
        foreach (var m in monsterTypes)
        {
            if (m == null || m.firstWave > wave) continue;
            last = m;
            r -= m.spawnWeight;
            if (r <= 0f) return m;
        }
        return last;
    }

    private void OnDrawGizmos()
    {
        if (spawnPoints == null) return;
        Gizmos.color = Color.red;
        foreach (var p in spawnPoints)
            if (p != null) Gizmos.DrawWireSphere(p.position, 1f);
    }
}
