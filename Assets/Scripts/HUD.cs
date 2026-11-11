using System.Text;
using TMPro;
using UnityEngine;

// 画面表示(TextMeshPro)。割り当てなくても動くよう null チェック済み。
// teamText(相棒の状態)は新しく追加した欄。使わなければ空のままでOK
public class HUD : MonoBehaviour
{
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text fortressText;
    [SerializeField] private TMP_Text playerText;
    [SerializeField] private TMP_Text teamText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private float messageDuration = 3f;

    private float messageHideTime;
    private readonly StringBuilder sb = new StringBuilder();
    private Vector2 inventoryScroll;
    private ItemData[] inventoryItems;
    private PlayerEquipment inventoryOwner;

    private void OnEnable() { GameLog.OnMessage += ShowMessage; }
    private void OnDisable() { GameLog.OnMessage -= ShowMessage; }

    private void Start()
    {
        if (messageText) messageText.text = "";
        if (resultText) resultText.text = "";
        if (teamText) teamText.text = "";
    }

    private void Update()
    {
        var wm = WaveManager.Instance;
        var fort = Fortress.Instance;
        var p = PlayerStats.Local;

        if (wm != null)
        {
            if (waveText)
            {
                waveText.text = wm.Phase == GamePhase.Lobby
                    ? "LOBBY"
                    : $"WAVE {wm.CurrentWave}/{wm.TotalWaves}  [{PhaseLabel(wm.Phase)}]";
            }
            if (timerText)
            {
                int t = Mathf.CeilToInt(wm.TimeRemaining);
                timerText.text = wm.Phase == GamePhase.Lobby ? "" : $"{t / 60:00}:{t % 60:00}";
            }
            if (resultText)
            {
                if (wm.Phase == GamePhase.Victory) resultText.text = "VICTORY!\nHost presses R to restart";
                else if (wm.Phase == GamePhase.Defeat) resultText.text = "GAME OVER\nHost presses R to restart";
                else resultText.text = "";
            }
        }

        if (fort != null && fortressText)
            fortressText.text = $"FORTRESS HP {Mathf.CeilToInt(fort.CurrentHp)}/{Mathf.CeilToInt(fort.MaxHp)}";

        if (p != null && playerText)
        {
            string left = p.Equipment.LeftWeapon != null ? p.Equipment.LeftWeapon.displayName : "-";
            string right = p.Equipment.RightWeapon != null ? p.Equipment.RightWeapon.displayName : "Unarmed";
            string leftAttack = p.Equipment.LeftWeapon != null ? $"{p.GetAttack(leftHand: true):0.#}" : "-";
            string armor = p.Equipment.Armor != null ? p.Equipment.Armor.displayName : "-";
            playerText.text =
                $"Lv {p.Level}  HP {Mathf.CeilToInt(p.CurrentHp)}/{Mathf.CeilToInt(p.MaxHp)}\n" +
                $"EXP {Mathf.FloorToInt(p.Exp)}/{Mathf.CeilToInt(p.ExpToNext)}\n" +
                $"DEF {p.Defense:0.#}  Armor: {armor}\n" +
                $"LMB: {left}  ATK {leftAttack}\n" +
                $"RMB: {right}  ATK {p.GetAttack(leftHand: false):0.#}";
        }

        // 相棒(自分以外のプレイヤー)の状態
        if (teamText)
        {
            sb.Clear();
            foreach (var other in PlayerStats.All)
            {
                if (other == null || other == p) continue;
                sb.Append($"Player{other.OwnerClientId + 1}  Lv {other.Level}  HP {Mathf.CeilToInt(other.CurrentHp)}/{Mathf.CeilToInt(other.MaxHp)}");
                if (other.IsDead) sb.Append("  (DOWN)");
                sb.Append('\n');
            }
            teamText.text = sb.ToString();
        }

        if (messageText && messageText.text != "" && Time.unscaledTime > messageHideTime)
            messageText.text = "";
    }

    private void ShowMessage(string msg)
    {
        if (!messageText) return;
        messageText.text = msg;
        messageHideTime = Time.unscaledTime + messageDuration;
    }

    private void OnGUI()
    {
        var player = PlayerStats.Local;
        if (player == null) return;

        var equipment = player.Equipment;
        if (!equipment.IsInventoryOpen)
        {
            GUI.Label(new Rect(20f, Screen.height - 32f, 240f, 28f), $"I: Weapons ({equipment.WeaponCount})");
            return;
        }

        // Layout と Repaint の間に拾得が起きても、同じ行数で描画する。
        if (Event.current.type == EventType.Layout || inventoryItems == null || inventoryOwner != equipment)
        {
            inventoryOwner = equipment;
            inventoryItems = new ItemData[equipment.WeaponCount];
            for (int i = 0; i < inventoryItems.Length; i++)
                inventoryItems[i] = equipment.GetInventoryWeapon(i);
        }

        float width = Mathf.Min(680f, Screen.width - 20f);
        float height = Mathf.Min(560f, Screen.height - 20f);
        var window = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(window, GUIContent.none);
        GUILayout.BeginArea(new Rect(window.x + 12f, window.y + 12f, width - 24f, height - 24f));
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Weapons ({equipment.WeaponCount})     I / Esc: Close");
        if (GUILayout.Button("Close", GUILayout.Width(70f)))
        {
            equipment.CloseInventory();
            GUIUtility.ExitGUI();
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("Choose a weapon for each hand. Combat continues while this window is open.");

        DrawEquippedHand(equipment, leftHand: true);
        DrawEquippedHand(equipment, leftHand: false);
        GUILayout.Space(10f);
        if (inventoryItems.Length == 0) GUILayout.Label("No weapons yet. Defeat monsters and collect their drops.");

        inventoryScroll = GUILayout.BeginScrollView(inventoryScroll);
        for (int i = 0; i < inventoryItems.Length; i++)
        {
            ItemData item = inventoryItems[i];
            if (item == null) continue;

            string hand = "";
            if (equipment.GetEquippedWeaponIndex(true) == i) hand = " [Left]";
            if (equipment.GetEquippedWeaponIndex(false) == i) hand = " [Right]";
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"#{i + 1}  {item.displayName}  ({item.rarity}){hand}");
            GUILayout.Label($"ATK +{item.attackBonus:0.#}    DEF +{item.defenseBonus:0.#}    Max HP +{item.maxHpBonus:0.#}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Equip Left (LMB)"))
            {
                equipment.EquipWeapon(i, leftHand: true);
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("Equip Right (RMB)"))
            {
                equipment.EquipWeapon(i, leftHand: false);
                GUIUtility.ExitGUI();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static void DrawEquippedHand(PlayerEquipment equipment, bool leftHand)
    {
        ItemData item = equipment.GetWeapon(leftHand);
        string hand = leftHand ? "Left (LMB)" : "Right (RMB)";
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{hand}: {(item != null ? item.displayName : "Empty")}");
        if (item != null && GUILayout.Button("Unequip", GUILayout.Width(80f)))
        {
            equipment.EquipWeapon(-1, leftHand);
            GUIUtility.ExitGUI();
        }
        GUILayout.EndHorizontal();
    }

    private static string PhaseLabel(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Combat: return "BATTLE";
            case GamePhase.Collect: return "COLLECT";
            case GamePhase.Victory: return "CLEAR";
            default: return "FAILED";
        }
    }
}
