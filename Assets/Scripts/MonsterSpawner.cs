using Unity.Netcode;
using UnityEngine;

// 戦闘フェーズ中、スポーン地点からモンスターを出現させる(ホストだけが動く)
public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private MonsterData[] monsterTypes;
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("MonsterData の Prefab が空のときに使う、ネットワーク対応のモンスターのプレハブ(Monster.prefab)")]
    [SerializeField] private Monster defaultMonsterPrefab;
    [Tooltip("同時に存在できる最大数")]
    [SerializeField] private int maxAlive = 40;
    [SerializeField] private float spawnScatter = 1.5f;

    private float timer;
    private bool warned;

    private void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

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

        Monster prefab = data.prefab != null ? data.prefab : defaultMonsterPrefab;
        if (prefab == null)
        {
            if (!warned)
            {
                Debug.LogError("MonsterSpawner: Default Monster Prefab が未設定です(Monster.prefab を入れてください)");
                warned = true;
            }
            return;
        }

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Vector2 scatter = Random.insideUnitCircle * spawnScatter;
        Vector3 pos = point.position + new Vector3(scatter.x, 1f, scatter.y);

        Monster monster = Instantiate(prefab, pos, Quaternion.identity);
        monster.ServerInit(data, setting);
        monster.GetComponent<NetworkObject>().Spawn(true);
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
