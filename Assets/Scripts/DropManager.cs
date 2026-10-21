using Unity.Netcode;
using UnityEngine;

// ドロップ品の生成(ホストだけが呼ぶ)。
// ネットワーク対応のプレハブ(ExpOrb.prefab / ItemPickup.prefab)が必要。
public class DropManager : MonoBehaviour
{
    public static DropManager Instance { get; private set; }

    [SerializeField] private ExpOrb expOrbPrefab;
    [SerializeField] private ItemPickup itemPickupPrefab;

    private void Awake()
    {
        Instance = this;
    }

    public void SpawnExp(Vector3 pos, float amount)
    {
        if (expOrbPrefab == null)
        {
            Debug.LogError("DropManager: Exp Orb Prefab が未設定です");
            return;
        }

        ExpOrb orb = Instantiate(expOrbPrefab, Scatter(pos), Quaternion.identity);
        orb.Init(amount);
        orb.GetComponent<NetworkObject>().Spawn(true);
    }

    public void SpawnItem(Vector3 pos, ItemData item)
    {
        if (itemPickupPrefab == null)
        {
            Debug.LogError("DropManager: Item Pickup Prefab が未設定です");
            return;
        }

        int index = ItemDatabase.IndexOf(item);
        if (index < 0)
        {
            Debug.LogError($"DropManager: {item.displayName} が ItemDatabase に登録されていません");
            return;
        }

        ItemPickup pickup = Instantiate(itemPickupPrefab, Scatter(pos), Quaternion.identity);
        pickup.Init(index);
        pickup.GetComponent<NetworkObject>().Spawn(true);
    }

    private static Vector3 Scatter(Vector3 pos)
    {
        Vector2 o = Random.insideUnitCircle * 0.6f;
        return new Vector3(pos.x + o.x, 0.5f, pos.z + o.y);
    }
}
