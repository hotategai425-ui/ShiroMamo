using UnityEngine;

// ドロップ品の生成。プレハブ未設定なら仮のプリミティブ(球/立方体/円柱)で生成
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
        Vector3 p = Scatter(pos);
        ExpOrb orb;

        if (expOrbPrefab != null)
        {
            orb = Instantiate(expOrbPrefab, p, Quaternion.identity);
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ExpOrb";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = p;
            go.transform.localScale = Vector3.one * 0.4f;
            go.GetComponent<Renderer>().material.color = new Color(0.3f, 0.9f, 1f);
            orb = go.AddComponent<ExpOrb>();
        }
        orb.Init(amount);
    }

    public void SpawnItem(Vector3 pos, ItemData item)
    {
        Vector3 p = Scatter(pos);
        ItemPickup pickup;

        if (itemPickupPrefab != null)
        {
            pickup = Instantiate(itemPickupPrefab, p, Quaternion.identity);
        }
        else
        {
            bool weapon = item.slot == ItemSlot.Weapon;
            var go = GameObject.CreatePrimitive(weapon ? PrimitiveType.Cube : PrimitiveType.Cylinder);
            go.name = "Item_" + item.displayName;
            Destroy(go.GetComponent<Collider>());
            go.transform.position = p;
            go.transform.localScale = weapon ? new Vector3(0.25f, 0.9f, 0.25f) : new Vector3(0.6f, 0.2f, 0.6f);
            go.GetComponent<Renderer>().material.color = item.RarityColor;
            pickup = go.AddComponent<ItemPickup>();
        }
        pickup.Init(item);
    }

    private static Vector3 Scatter(Vector3 pos)
    {
        Vector2 o = Random.insideUnitCircle * 0.6f;
        return new Vector3(pos.x + o.x, 0.5f, pos.z + o.y);
    }
}
