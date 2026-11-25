using UnityEngine;

// 本番HUDと独立した、ホスト用の仮検証画面。
[RequireComponent(typeof(CreatorMode))]
public class CreatorModeUI : MonoBehaviour
{
    private CreatorMode mode;
    private Font font;
    private GUISkin skin;
    private Vector2 scroll;
    private string levelText = "1";
    private string feedback = "";
    private bool wasOpen;
    private int rarityChoice;
    private readonly string[] rarityChoices = { "通常抽選", "コモン", "レア", "エピック" };

    private void Awake()
    {
        mode = GetComponent<CreatorMode>();
        font = Resources.Load<Font>("Fonts/NotoSansJP");
    }

    private void Update()
    {
        bool open = mode.CanUse && mode.IsMenuOpen;
        if (open && !wasOpen)
        {
            levelText = mode.Player.Level.ToString();
            feedback = "";
        }
        wasOpen = open;
    }

    private void OnGUI()
    {
        if (!mode.CanUse) return;
        if (skin == null)
        {
            skin = Instantiate(GUI.skin);
            if (font != null) skin.font = font;
            skin.label.fontSize = 16;
            skin.label.wordWrap = true;
            skin.label.padding = new RectOffset(6, 6, 4, 4);
            skin.button.fontSize = 16;
            skin.button.padding = new RectOffset(8, 8, 6, 6);
            skin.textField.fontSize = 16;
            skin.textField.padding = new RectOffset(6, 6, 6, 6);
        }
        GUISkin previous = GUI.skin;
        bool previousEnabled = GUI.enabled;
        try
        {
            GUI.skin = skin;
            if (!mode.IsMenuOpen)
            {
                if (mode.MonstersPaused)
                    GUI.Label(new Rect(12f, Screen.height - 40f, 360f, 30f), "制作者モード：敵停止中（F2で操作）");
                return;
            }
            float width = Mathf.Min(560f, Screen.width - 20f);
            float height = Mathf.Min(620f, Screen.height - 20f);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("制作者モード（ホスト専用）");
            if (GUILayout.Button("閉じる", GUILayout.Width(80f)))
            {
                mode.SetMenuOpen(false);
                GUIUtility.ExitGUI();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("F2 / Esc：閉じる。画面を開いている本人の操作だけ停止します。");
            GUILayout.Space(8f);
            GUILayout.Label(mode.MonstersPaused ? "敵：停止中" : "敵：稼働中");
            if (GUILayout.Button(mode.MonstersPaused ? "モンスターを再開" : "モンスターを停止"))
                mode.SetMonstersPaused(!mode.MonstersPaused);
            GUILayout.Label("停止対象：移動・攻撃・追加出現。プレイヤーの攻撃は可能です。ウェーブ時間は進みます。");
            GUILayout.Space(8f);
            GUILayout.Label($"チームの共有レベル：{mode.Player.Level}（上げた分の強化選択が出ます）");
            GUI.enabled = !mode.Player.IsDead;
            GUILayout.BeginHorizontal();
            levelText = GUILayout.TextField(levelText, 10, GUILayout.Width(120f));
            if (GUILayout.Button("レベルを設定（経験値0・HP全回復）"))
            {
                feedback = int.TryParse(levelText, out int requested) && mode.TrySetLevel(requested)
                    ? "レベルを変更しました。" : "1以上で、経験値を計算できる範囲の整数を入力してください。";
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            if (GUILayout.Button("自爆ゴブリンを1体生成（戦闘中・砦の前方）"))
                feedback = mode.TrySpawnSuicideGoblin() ? "自爆ゴブリンを生成しました。"
                    : "生成できません。戦闘中か、砦の前方に空きがあるか確認してください。";
            GUILayout.Label("武器を持ち物に追加（空いている手には自動装備）");
            rarityChoice = GUILayout.Toolbar(rarityChoice, rarityChoices);
            GUILayout.Label("指定したレア度の設定で性能を抽選します。通常抽選ではレア度も抽選します。");
            scroll = GUILayout.BeginScrollView(scroll);
            var database = ItemDatabase.Instance;
            if (database != null)
            {
                for (int i = 0; i < database.Count; i++)
                {
                    ItemData weapon = ItemDatabase.Get(i);
                    if (weapon == null || weapon.slot != ItemSlot.Weapon) continue;
                    if (GUILayout.Button(weapon.displayName + " を追加"))
                    {
                        ItemRarity? rarity = rarityChoice == 0 ? (ItemRarity?)null : (ItemRarity)(rarityChoice - 1);
                        feedback = mode.TryGiveWeapon(i, rarity) ? "武器を持ち物に追加しました。"
                            : "追加できませんでした。指定したレア度の設定があるか確認してください。";
                    }
                }
            }
            GUILayout.EndScrollView();
            GUI.enabled = true;
            if (mode.Player.IsDead) GUILayout.Label("戦闘不能中はレベル変更・武器追加ができません。");
            GUILayout.Label(feedback);
            GUILayout.EndArea();
        }
        finally
        {
            GUI.enabled = previousEnabled;
            GUI.skin = previous;
        }
    }

    private void OnDestroy()
    {
        if (skin != null) Destroy(skin);
    }
}
