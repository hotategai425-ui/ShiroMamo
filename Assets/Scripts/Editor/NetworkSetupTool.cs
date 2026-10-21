using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

// メニュー Tools > TD > Create Network Prefabs で、ネットワーク対応のプレハブを
// Assets/Prefabs に自動作成する(Player / Monster / ExpOrb / ItemPickup)。
// このファイルは必ず「Editor」という名前のフォルダ(例: Assets/Scripts/Editor)に置くこと。
public static class NetworkSetupTool
{
    private const string Dir = "Assets/Prefabs";

    [MenuItem("Tools/TD/Create Network Prefabs")]
    public static void CreatePrefabs()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Prefabs");

        CreatePlayer();
        CreateMonster();
        CreateExpOrb();
        CreateItemPickup();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Network prefabs created in " + Dir);
    }

    private static void CreatePlayer()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Player";
        go.AddComponent<Rigidbody>();
        go.AddComponent<NetworkObject>();
        go.AddComponent<ClientNetworkTransform>();
        go.AddComponent<PlayerStats>();        // PlayerEquipment も自動で付く
        go.AddComponent<PlayerController3D>();
        go.AddComponent<PlayerAttack>();
        Save(go, "Player");
    }

    private static void CreateMonster()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Monster";
        go.AddComponent<Rigidbody>();
        go.AddComponent<NetworkObject>();
        go.AddComponent<NetworkTransform>();
        go.AddComponent<Monster>();
        Save(go, "Monster");
    }

    private static void CreateExpOrb()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ExpOrb";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.localScale = Vector3.one * 0.4f;
        go.AddComponent<NetworkObject>();
        go.AddComponent<NetworkTransform>();
        go.AddComponent<ExpOrb>();
        Save(go, "ExpOrb");
    }

    private static void CreateItemPickup()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "ItemPickup";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
        go.AddComponent<NetworkObject>();
        go.AddComponent<NetworkTransform>();
        go.AddComponent<ItemPickup>();
        Save(go, "ItemPickup");
    }

    private static void Save(GameObject go, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(go, Dir + "/" + name + ".prefab");
        Object.DestroyImmediate(go);
    }
}
