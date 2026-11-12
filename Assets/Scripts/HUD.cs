using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 戦闘HUDは既存のTMP参照を使い、Full HD基準の仮配置を実行時に構築する。
// 持ち物画面は従来のOnGUI。装備・戦闘・同期の処理は既存APIに任せる。
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
    private ItemInstance[] inventoryItems;
    private PlayerEquipment inventoryOwner;
    private GameObject battleRoot;
    private TMP_Text leftLabel, rightLabel, hint, crosshair;
    private RectTransform playerBar, fortressBar;
    private Font japaneseFont;
    private TMP_FontAsset japaneseTmpFont;
    private GUISkin japaneseInventorySkin;
    private GUIStyle[] rarityLabelStyles;

    private void OnEnable() { GameLog.OnMessage += ShowMessage; }
    private void OnDisable() { GameLog.OnMessage -= ShowMessage; }

    private void Start()
    {
        BuildLayout();
        if (messageText) messageText.text = "";
        if (resultText) resultText.text = "";
        if (teamText) teamText.text = "";
    }

    private void Update()
    {
        var wm = WaveManager.Instance;
        var fort = Fortress.Instance;
        var p = PlayerStats.Local;
        bool visible = p != null && wm != null && wm.Phase != GamePhase.Lobby;
        if (battleRoot) battleRoot.SetActive(visible);
        if (!visible) return;
        if (crosshair) crosshair.enabled = !p.IsDead && !p.Equipment.IsInventoryOpen &&
            (wm.Phase == GamePhase.Combat || wm.Phase == GamePhase.Collect);

        if (wm != null)
        {
            if (waveText)
            {
                waveText.text = wm.Phase == GamePhase.Lobby
                    ? "ロビー"
                    : $"ウェーブ {wm.CurrentWave}/{wm.TotalWaves}  ［{PhaseLabel(wm.Phase)}］";
            }
            if (timerText)
            {
                int t = Mathf.Max(0, Mathf.CeilToInt(wm.TimeRemaining));
                timerText.text = wm.Phase == GamePhase.Lobby ? "" : $"残り {t / 60:00}:{t % 60:00}";
            }
            if (resultText)
            {
                string restart = p.IsServer ? "Rキーで再戦" : "ホストの再戦操作を待っています";
                if (wm.Phase == GamePhase.Victory) resultText.text = $"防衛成功！\n{restart}";
                else if (wm.Phase == GamePhase.Defeat) resultText.text = $"防衛失敗\n{restart}";
                else resultText.text = "";
            }
        }

        if (fort != null && fortressText)
            fortressText.text = $"砦のHP {Mathf.CeilToInt(fort.CurrentHp)}/{Mathf.CeilToInt(fort.MaxHp)}";

        if (p != null && playerText)
        {
            playerText.text =
                $"レベル {p.Level}  HP {Mathf.CeilToInt(p.CurrentHp)}/{Mathf.CeilToInt(p.MaxHp)}" +
                (p.IsDead ? " （戦闘不能）" : "") +
                $"\n\n経験値 {Mathf.FloorToInt(p.Exp)}/{Mathf.CeilToInt(p.ExpToNext)}  防御力 {p.Defense:0.#}";
            if (leftLabel) leftLabel.text = WeaponLabel(p, true);
            if (rightLabel) rightLabel.text = WeaponLabel(p, false);
            if (leftLabel) leftLabel.color = WeaponColor(p.Equipment.GetWeaponInstance(true));
            if (rightLabel) rightLabel.color = WeaponColor(p.Equipment.GetWeaponInstance(false));
            if (hint) hint.text = $"I：持ち物（{p.Equipment.WeaponCount}）";
            SetBar(playerBar, p.CurrentHp, p.MaxHp);
        }
        if (fort != null) SetBar(fortressBar, fort.CurrentHp, fort.MaxHp);

        // 相棒(自分以外のプレイヤー)の状態
        if (teamText)
        {
            sb.Clear();
            foreach (var other in PlayerStats.All)
            {
                if (other == null || other == p) continue;
                sb.Append($"相棒  レベル {other.Level}\nHP {Mathf.CeilToInt(other.CurrentHp)}/{Mathf.CeilToInt(other.MaxHp)}");
                if (other.IsDead) sb.Append("  （戦闘不能）");
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
        messageText.text = LocalizeMessage(msg);
        messageText.color = Color.white;
        if (msg != null && (msg.StartsWith("Collected: ") || msg.StartsWith("Equipped (Right): ") || msg.StartsWith("Equipped (Left): ")))
        {
            foreach (ItemRarity rarity in System.Enum.GetValues(typeof(ItemRarity)))
                if (msg.Contains($": {rarity} ")) messageText.color = RarityColor(rarity);
        }
        messageHideTime = Time.unscaledTime + messageDuration;
    }

    private void OnGUI()
    {
        var player = PlayerStats.Local;
        if (player == null) return;

        var equipment = player.Equipment;
        if (!equipment.IsInventoryOpen)
        {
            return;
        }
        // GUI.skinをこの描画中だけ差し替え、接続メニュー等へ影響させない。
        if (japaneseFont != null && japaneseInventorySkin == null)
        {
            japaneseInventorySkin = Instantiate(GUI.skin);
            japaneseInventorySkin.font = japaneseFont;
        }
        var previousSkin = GUI.skin;
        try
        {
            if (japaneseInventorySkin != null) GUI.skin = japaneseInventorySkin;
            DrawInventory(equipment);
        }
        finally { GUI.skin = previousSkin; }
    }

    private void DrawInventory(PlayerEquipment equipment)
    {

        // Layout と Repaint の間に拾得が起きても、同じ行数で描画する。
        if (Event.current.type == EventType.Layout || inventoryItems == null || inventoryOwner != equipment)
        {
            inventoryOwner = equipment;
            inventoryItems = new ItemInstance[equipment.WeaponCount];
            for (int i = 0; i < inventoryItems.Length; i++)
                inventoryItems[i] = equipment.GetInventoryInstance(i);
        }

        float width = Mathf.Min(680f, Screen.width - 20f);
        float height = Mathf.Min(560f, Screen.height - 20f);
        var window = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(window, GUIContent.none);
        GUILayout.BeginArea(new Rect(window.x + 12f, window.y + 12f, width - 24f, height - 24f));
        GUILayout.BeginHorizontal();
        GUILayout.Label($"持ち物（{equipment.WeaponCount}）     I / Esc：閉じる");
        if (GUILayout.Button("閉じる", GUILayout.Width(70f)))
        {
            equipment.CloseInventory();
            GUIUtility.ExitGUI();
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("左右の手に装備する武器を選んでください。開いている間も戦闘は進みます。");

        DrawEquippedHand(equipment, leftHand: true);
        DrawEquippedHand(equipment, leftHand: false);
        GUILayout.Space(10f);
        if (inventoryItems.Length == 0) GUILayout.Label("武器がありません。敵を倒して落とした武器を拾いましょう。");

        inventoryScroll = GUILayout.BeginScrollView(inventoryScroll);
        for (int i = 0; i < inventoryItems.Length; i++)
        {
            ItemInstance instance = inventoryItems[i];
            ItemData item = instance.Definition;
            if (!instance.IsValid || item == null) continue;

            string hand = "";
            if (equipment.GetEquippedWeaponIndex(true) == i) hand = " ［左手］";
            if (equipment.GetEquippedWeaponIndex(false) == i) hand = " ［右手］";
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"#{i + 1}  {ItemName(item)}  （{RarityLabel(instance.Rarity)}）{hand}", RarityStyle(instance.Rarity));
            GUILayout.Label($"攻撃力補正 +{instance.AttackBonus:0.#}    防御力補正 +{instance.DefenseBonus:0.#}    最大HP補正 +{instance.MaxHpBonus:0.#}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("左手に装備（左クリック）"))
            {
                equipment.EquipWeapon(i, leftHand: true);
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("右手に装備（右クリック）"))
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

    private void DrawEquippedHand(PlayerEquipment equipment, bool leftHand)
    {
        ItemInstance instance = equipment.GetWeaponInstance(leftHand);
        ItemData item = instance.Definition;
        string hand = leftHand ? "左手（左クリック）" : "右手（右クリック）";
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{hand}：{(item != null ? ItemName(item) : "装備なし")}", RarityStyle(instance.Rarity));
        if (item != null && GUILayout.Button("外す", GUILayout.Width(80f)))
        {
            equipment.EquipWeapon(-1, leftHand);
            GUIUtility.ExitGUI();
        }
        GUILayout.EndHorizontal();
        if (instance.IsValid && item != null)
            GUILayout.Label($"{RarityLabel(instance.Rarity)}  攻撃力補正 +{instance.AttackBonus:0.#}  防御力補正 +{instance.DefenseBonus:0.#}  最大HP補正 +{instance.MaxHpBonus:0.#}", RarityStyle(instance.Rarity));
    }

    // 仮配置は実行時のみ生成し、シーン保存値・ゲーム設定には触れない。
    private void BuildLayout()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null || playerText == null) return;
        japaneseFont = Resources.Load<Font>("Fonts/NotoSansJP");
        if (japaneseFont != null)
        {
            japaneseTmpFont = TMP_FontAsset.CreateFontAsset(japaneseFont);
            if (japaneseTmpFont != null) japaneseTmpFont.isMultiAtlasTexturesEnabled = true;
            else Debug.LogError("日本語UI用のTMPフォントを生成できませんでした。", this);
        }
        else Debug.LogError("日本語UI用フォント Fonts/NotoSansJP が見つかりません。", this);
        canvas.transform.localScale = Vector3.one;
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
        }
        battleRoot = new GameObject("Battle HUD", typeof(RectTransform));
        var root = (RectTransform)battleRoot.transform;
        root.SetParent(canvas.transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        if (!teamText) teamText = NewText("Team");
        leftLabel = NewText("Left Weapon");
        rightLabel = NewText("Right Weapon");
        hint = NewText("Inventory Hint");
        crosshair = NewText("Crosshair");
        Place(waveText, .5f, 1, 0, -24, 600, 40, 28, TextAlignmentOptions.Center);
        Place(fortressText, .5f, 1, 0, -72, 600, 40, 26, TextAlignmentOptions.Center);
        // 日本語フォントの行高を確保。旧44pxでは行全体が省略される場合がある。
        if (!timerText) timerText = NewText("Timer");
        Place(timerText, 1, 1, -32, -24, 360, 80, 34, TextAlignmentOptions.TopRight);
        timerText.textWrappingMode = TextWrappingModes.NoWrap;
        timerText.overflowMode = TextOverflowModes.Overflow;
        timerText.enabled = true;
        timerText.gameObject.SetActive(true);
        Place(teamText, 0, 1, 32, -24, 480, 96, 24, TextAlignmentOptions.TopLeft);
        Place(playerText, 0, 0, 32, 156, 500, 116, 28, TextAlignmentOptions.TopLeft);
        Place(leftLabel, 0, 0, 32, 24, 620, 100, 26, TextAlignmentOptions.TopLeft);
        Place(rightLabel, .5f, 0, 0, 24, 620, 100, 26, TextAlignmentOptions.TopLeft);
        Place(hint, 1, 0, -32, 24, 280, 44, 24, TextAlignmentOptions.Right);
        Place(messageText, .5f, 0, 0, 300, 1000, 100, 28, TextAlignmentOptions.Center);
        Place(resultText, .5f, .5f, 0, 0, 1000, 200, 44, TextAlignmentOptions.Center);
        Place(crosshair, .5f, .5f, 0, 0, 24, 24, 24, TextAlignmentOptions.Center);
        crosshair.text = "+";
        playerBar = MakeBar("Player HP", new Vector2(0, 0), new Vector2(32, 216), 400);
        fortressBar = MakeBar("Fortress HP", new Vector2(.5f, 1), new Vector2(0, -124), 480);
        battleRoot.SetActive(false);
    }

    private TMP_Text NewText(string label)
    {
        var text = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        text.font = playerText.font;
        return text;
    }

    private void Place(TMP_Text text, float ax, float ay, float x, float y, float w, float h,
        float size, TextAlignmentOptions alignment)
    {
        if (!text) return;
        var rect = text.rectTransform;
        rect.SetParent(battleRoot.transform, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(ax, ay);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        text.fontSize = size;
        if (japaneseTmpFont != null) text.font = japaneseTmpFont;
        text.enableAutoSizing = false;
        text.alignment = alignment;
        text.color = Color.white;
        text.fontStyle = FontStyles.Normal;
        text.margin = Vector4.zero;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.text = "";
    }

    private RectTransform MakeBar(string label, Vector2 anchor, Vector2 position, float width)
    {
        var background = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)background.transform;
        rect.SetParent(battleRoot.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, 12);
        background.GetComponent<Image>().color = new Color(0, 0, 0, .65f);
        background.GetComponent<Image>().raycastTarget = false;
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var fillRect = (RectTransform)fill.transform;
        fillRect.SetParent(rect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = Color.white;
        fill.GetComponent<Image>().raycastTarget = false;
        return fillRect;
    }

    private static void SetBar(RectTransform fill, float hp, float maxHp)
    {
        if (fill) fill.anchorMax = new Vector2(maxHp > 0 ? Mathf.Clamp01(hp / maxHp) : 0, 1);
    }

    private static string WeaponLabel(PlayerStats player, bool leftHand)
    {
        var instance = player.Equipment.GetWeaponInstance(leftHand);
        var definition = instance.Definition;
        string input = leftHand ? "左クリック" : "右クリック";
        if (!instance.IsValid || definition == null)
            return leftHand ? $"{input}：装備なし\n攻撃力 ―" : $"{input}：素手\n攻撃力 {player.GetAttack(false):0.#}";
        return $"{input}：{ItemName(definition)}\n{RarityLabel(instance.Rarity)} / 攻撃力 {player.GetAttack(leftHand):0.#}";
    }

    private static string ItemName(ItemData item) => LocalizeWords(item.displayName);

    private static Color RarityColor(ItemRarity rarity) => rarity == ItemRarity.Common
        ? Color.white : ItemData.GetRarityColor(rarity);

    private static Color WeaponColor(ItemInstance instance) => instance.IsValid
        ? RarityColor(instance.Rarity) : Color.white;

    private GUIStyle RarityStyle(ItemRarity rarity)
    {
        if (rarityLabelStyles == null)
        {
            rarityLabelStyles = new GUIStyle[3];
            for (int i = 0; i < rarityLabelStyles.Length; i++)
            {
                var style = new GUIStyle(GUI.skin.label);
                style.normal.textColor = RarityColor((ItemRarity)i);
                rarityLabelStyles[i] = style;
            }
        }
        return rarityLabelStyles[Mathf.Clamp((int)rarity, 0, rarityLabelStyles.Length - 1)];
    }

    private static string RarityLabel(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Rare: return "レア";
            case ItemRarity.Epic: return "エピック";
            default: return "コモン";
        }
    }

    // 表示だけを翻訳し、基礎作り担当の通知・RPC契約は変更しない。
    private static string LocalizeWords(string value) => (value ?? "")
        .Replace("Crystal Sword", "クリスタルソード").Replace("Magic Book", "魔法の本")
        .Replace("Common", "コモン").Replace("Rare", "レア").Replace("Epic", "エピック");

    private static string LocalizeMessage(string message)
    {
        string value = LocalizeWords(message);
        if (value.StartsWith("WAVE ") && value.EndsWith(" START!"))
            return "ウェーブ " + value.Substring(5, value.Length - 12) + " 開始！";
        return value.Replace("COLLECT TIME! Pick up drops", "回収時間です。落ちたアイテムを拾いましょう")
            .Replace("Collected: ", "拾得：").Replace(" (I: Inventory)", "（I：持ち物）")
            .Replace("LEVEL UP! Lv ", "レベルアップ！ レベル ")
            .Replace("You were defeated... respawning", "戦闘不能になりました。復活を待っています")
            .Replace("Equipped (Armor): ", "防具を装備：").Replace("Salvaged ", "経験値に変換：")
            .Replace("Equipped (Right): ", "右手に装備：").Replace("Equipped (Left): ", "左手に装備：")
            .Replace(" EXP)", " 経験値）").Replace(" (+", "（+");
    }

    private void OnDestroy()
    {
        if (japaneseInventorySkin) Destroy(japaneseInventorySkin);
        if (japaneseTmpFont)
        {
            foreach (var atlas in japaneseTmpFont.atlasTextures)
                if (atlas) Destroy(atlas);
            if (japaneseTmpFont.material) Destroy(japaneseTmpFont.material);
            Destroy(japaneseTmpFont);
        }
    }

    private static string PhaseLabel(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Combat: return "戦闘";
            case GamePhase.Collect: return "回収";
            case GamePhase.Victory: return "防衛成功";
            default: return "防衛失敗";
        }
    }
}
