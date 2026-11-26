using System.Text;
using System.Linq;
using System.Globalization;
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
    private Vector2 skillScroll;
    private ulong skillDetailId;
    private ItemInstance[] inventoryItems;
    private PlayerEquipment inventoryOwner;
    private GameObject battleRoot;
    private TMP_Text leftLabel, rightLabel, hint, crosshair, guardianLabel;
    private RectTransform playerBar, fortressBar;
    private Font japaneseFont;
    private TMP_FontAsset japaneseTmpFont;
    private GUISkin japaneseInventorySkin;
    private ulong selectedInventoryId;
    private enum InventorySort { Rarity = 2, AttackMultiplier = 3, DamageReduction = 4, Hp = 5 }
    private InventorySort inventorySort = InventorySort.Rarity;

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
        if (guardianLabel)
        {
            guardianLabel.text = GuardianStatusLabel(p.GuardianStatus);
            guardianLabel.enabled = p.HasGuardian;
        }
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
            playerText.text = PlayerStatusLabel(p);
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
        if (player == null || !player.Equipment.IsInventoryOpen) return;
        if (japaneseInventorySkin == null)
        {
            japaneseInventorySkin = Instantiate(GUI.skin);
            if (japaneseFont) japaneseInventorySkin.font = japaneseFont;
            japaneseInventorySkin.label.fontSize = 20;
            japaneseInventorySkin.label.wordWrap = true;
            japaneseInventorySkin.label.fixedHeight = 0;
            japaneseInventorySkin.label.alignment = TextAnchor.MiddleLeft;
            japaneseInventorySkin.label.padding = new RectOffset(8, 8, 4, 4);
            japaneseInventorySkin.label.normal.textColor = Color.white;
            japaneseInventorySkin.button.fontSize = 20;
            japaneseInventorySkin.button.wordWrap = false;
        }
        var previousSkin = GUI.skin;
        var previousMatrix = GUI.matrix;
        var previousColor = GUI.color;
        int previousDepth = GUI.depth;
        try
        {
            GUI.skin = japaneseInventorySkin;
            GUI.color = Color.white;
            GUI.depth = -100;
            // 小さい表示でも列配置を維持し、画面内へ全体を縮小する。
            float scale = Mathf.Min(1f, Screen.width / 1000f, Screen.height / 650f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            DrawInventory(player.Equipment, Screen.width / scale, Screen.height / scale);
        }
        finally
        {
            GUI.skin = previousSkin;
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }
    }

    private void DrawInventory(PlayerEquipment equipment, float screenWidth, float screenHeight)
    {
        // 表示順だけを変更。同値ではレア度、その後は元の所持順を保つ。
        if (Event.current.type == EventType.Layout || inventoryItems == null || inventoryOwner != equipment)
            RefreshInventorySnapshot(equipment);
        float width = Mathf.Min(1120, screenWidth - 40);
        float height = Mathf.Min(960, screenHeight - 40);
        // 操作欄は固定し、狭い画面では詳細側を縮めて一覧2行分を確保。
        float detailHeight = SkillDetailHeight(height);
        float footerHeight = 164 + detailHeight;
        var window = new Rect((screenWidth - width) / 2, (screenHeight - height) / 2, width, height);
        FillRect(window, new Color(.075f, .085f, .105f, 1));
        GUI.BeginGroup(window);
        try
        {
            float innerWidth = width - 40;
            GUI.Label(new Rect(20, 12, innerWidth - 130, 46), $"持ち物  {inventoryItems.Length}個");
            if (GUI.Button(new Rect(width - 130, 14, 110, 42), "閉じる"))
            {
                equipment.CloseInventory();
                GUIUtility.ExitGUI();
            }
            DrawEquippedHand(equipment, true, new Rect(20, 66, innerWidth, 46));
            if (!equipment.UsesTwoHandedWeapon)
                DrawEquippedHand(equipment, false, new Rect(20, 116, innerWidth, 46));
            GUI.Label(new Rect(20, 170, innerWidth, 36), "見出しで降順 ／ 攻撃は本人×倍率・HPは補正 ／ 白枠：装備中・背景：選択中");

            float tableWidth = innerWidth - 20; // 縦スクロールバー用の幅を常に確保。
            DrawInventoryCells(new Rect(20, 210, tableWidth, 42), default, "装備", true);
            var viewport = new Rect(20, 254, innerWidth, height - footerHeight - 266);
            inventoryScroll = GUI.BeginScrollView(viewport, inventoryScroll,
                new Rect(0, 0, tableWidth, Mathf.Max(viewport.height, inventoryItems.Length * 64)), false, true);
            try
            {
                ulong leftId = equipment.GetWeaponInstance(true).InstanceId;
                ulong rightId = equipment.GetWeaponInstance(false).InstanceId;
                for (int i = 0; i < inventoryItems.Length; i++)
                {
                    var instance = inventoryItems[i];
                    var row = new Rect(0, i * 64, tableWidth, 60);
                    bool equipped = instance.InstanceId == leftId || instance.InstanceId == rightId;
                    FillRect(row, instance.InstanceId == selectedInventoryId
                        ? new Color(.22f, .30f, .40f) : new Color(.12f, .135f, .16f));
                    if (equipped) DrawWhiteBorder(row);
                    string hand = EquippedHandLabel(instance.InstanceId, leftId, rightId);
                    // 透明の行ボタン。数値・レア度は後から描き、押下色と独立させる。
                    if (GUI.Button(row, GUIContent.none, GUIStyle.none)) selectedInventoryId = instance.InstanceId;
                    DrawInventoryCells(row, instance, hand, false);
                }
                if (inventoryItems.Length == 0)
                    GUI.Label(new Rect(8, 12, tableWidth - 16, 80), "武器がありません。敵を倒して武器を拾いましょう。");
            }
            finally { GUI.EndScrollView(); }

            var selected = inventoryItems.FirstOrDefault(item => item.InstanceId == selectedInventoryId);
            float footerY = height - footerHeight;
            FillRect(new Rect(20, footerY, innerWidth, 2), new Color(.32f, .35f, .4f));
            GUI.Label(new Rect(20, footerY + 8, innerWidth, 44), selected.IsValid
                ? $"選択中：{ItemName(selected.Definition)} ［{RarityLabel(selected.Rarity)}］"
                : "一覧の武器をクリックして選択してください。");
            DrawSkillDetails(selected, new Rect(20, footerY + 52, innerWidth, detailHeight));
            bool previousEnabled = GUI.enabled;
            try
            {
                GUI.enabled = previousEnabled && selected.IsValid;
                bool twoHanded = selected.Definition != null && selected.Definition.UsesTwoHands;
                if (GUI.Button(new Rect(20, footerY + detailHeight + 58, twoHanded ? 412 : 200, 46), twoHanded ? "両手に装備" : "左手に装備"))
                {
                    equipment.EquipWeapon(selected.InstanceId, true);
                    GUIUtility.ExitGUI();
                }
                if (!twoHanded && GUI.Button(new Rect(232, footerY + detailHeight + 58, 200, 46), "右手に装備"))
                {
                    equipment.EquipWeapon(selected.InstanceId, false);
                    GUIUtility.ExitGUI();
                }
                if (GUI.Button(new Rect(width - 200, footerY + detailHeight + 58, 180, 46), "落とす"))
                {
                    equipment.DropWeapon(selected.InstanceId);
                    GUIUtility.ExitGUI();
                }
            }
            finally { GUI.enabled = previousEnabled; }
            GUI.Label(new Rect(20, height - 48, innerWidth - 220, 38), "開いている間も戦闘は進みます。");
            GUI.Label(new Rect(width - 240, height - 48, 220, 38), "I / Esc：閉じる");
        }
        finally { GUI.EndGroup(); }
    }

    private void DrawSkillDetails(ItemInstance selected, Rect area)
    {
        RefreshSkillDetailSelection(selected);
        var style = new GUIStyle(GUI.skin.label);
        SetLabelColor(style, Color.white);
        style.wordWrap = true;
        string text = selected.IsValid ? WeaponSkillDisplay.Build(selected.Skills)
            : "武器を選ぶと固有・共通スキルと説明を確認できます。";
        float contentWidth = area.width - 22;
        float contentHeight = Mathf.Max(area.height, style.CalcHeight(new GUIContent(text), contentWidth) + 12);
        FillRect(area, new Color(.10f, .11f, .13f));
        skillScroll = GUI.BeginScrollView(area, skillScroll, new Rect(0, 0, contentWidth, contentHeight), false, true);
        try { GUI.Label(new Rect(0, 0, contentWidth, contentHeight), text, style); }
        finally { GUI.EndScrollView(); }
    }

    private static float SkillDetailHeight(float windowHeight) => Mathf.Clamp(windowHeight - 698, 48, 140);

    private void RefreshSkillDetailSelection(ItemInstance selected)
    {
        ulong id = selected.IsValid ? selected.InstanceId : 0;
        if (skillDetailId != id) { skillDetailId = id; skillScroll = Vector2.zero; }
    }

    private void RefreshInventorySnapshot(PlayerEquipment equipment)
    {
        if (inventoryOwner != equipment)
        {
            selectedInventoryId = 0;
            inventoryScroll = Vector2.zero;
            skillDetailId = 0;
            skillScroll = Vector2.zero;
            inventorySort = InventorySort.Rarity;
        }
        inventoryOwner = equipment;
        inventoryItems = Enumerable.Range(0, equipment.WeaponCount)
            .Select(equipment.GetInventoryInstance).Where(item => item.IsValid && item.Definition != null)
            .OrderByDescending(item => InventorySortValue(item, inventorySort))
            .ThenByDescending(item => item.Rarity).ToArray();
        // 削除時は他の武器を勝手に選択しない。移動した同一個体は選択を維持する。
        if (!inventoryItems.Any(item => item.InstanceId == selectedInventoryId)) selectedInventoryId = 0;
    }

    private void DrawInventoryCells(Rect row, ItemInstance instance, string hand, bool header)
    {
        float[] widths = { 80, row.width - 480, 100, 100, 100, 100 };
        string[] values = InventoryCellValues(instance, hand, header);
        float x = row.x;
        for (int column = 0; column < widths.Length; column++)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = column >= 3 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            SetLabelColor(style, !header && (column == 1 || column == 2) ? RarityColor(instance.Rarity) : Color.white);
            var cell = new Rect(x, row.y, widths[column], row.height);
            if (header && column >= 2)
            {
                var buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.fontSize = 18;
                buttonStyle.padding = new RectOffset(2, 2, 4, 4);
                string title = values[column] + ((int)inventorySort == column ? " ↓" : "");
                if (GUI.Button(new Rect(cell.x + 2, cell.y, cell.width - 4, cell.height), title, buttonStyle))
                {
                    inventorySort = (InventorySort)column;
                    inventoryScroll = Vector2.zero;
                    // 次のLayoutで所持順から並べ直す。選択は個体IDのまま維持。
                    GUIUtility.ExitGUI();
                }
            }
            else GUI.Label(cell, values[column], style);
            x += widths[column];
        }
    }

    private static float InventorySortValue(ItemInstance item, InventorySort sort)
    {
        switch (sort)
        {
            case InventorySort.AttackMultiplier: return item.AttackMultiplier;
            case InventorySort.DamageReduction: return DisplayedReduction(item);
            case InventorySort.Hp: return item.MaxHpBonus;
            default: return (int)item.Rarity;
        }
    }

    private static string[] InventoryCellValues(ItemInstance instance, string hand, bool header) => header
        ? new[] { hand, "武器名", "レア度", "攻撃倍率", "軽減率", "HP" }
        : new[] { hand, ItemName(instance.Definition), RarityLabel(instance.Rarity),
            FormatMultiplier(instance.AttackMultiplier), FormatReduction(DisplayedReduction(instance)), $"+{instance.MaxHpBonus:0.#}" };

    private static string FormatMultiplier(float value) => "×" + value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string FormatReduction(float value) => (value * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
    private static float DisplayedReduction(ItemInstance instance) => instance.Definition != null && instance.Definition.IsShield
        ? instance.DamageReductionRate : 0f;

    private static string PlayerStatusLabel(PlayerStats player) =>
        $"レベル {player.Level}  HP {Mathf.CeilToInt(player.CurrentHp)}/{Mathf.CeilToInt(player.MaxHp)}" +
        (player.IsDead ? " （戦闘不能）" : "") +
        $"\n\n経験値 {Mathf.FloorToInt(player.Exp)}/{Mathf.CeilToInt(player.ExpToNext)}";

    private void DrawEquippedHand(PlayerEquipment equipment, bool leftHand, Rect area)
    {
        var instance = equipment.GetWeaponInstance(leftHand);
        var definition = instance.Definition;
        string hand = equipment.UsesTwoHandedWeapon ? "両手" : leftHand ? "左手" : "右手";
        var style = new GUIStyle(GUI.skin.label);
        SetLabelColor(style, WeaponColor(instance));
        GUI.Label(new Rect(area.x, area.y, area.width - 160, area.height),
            $"{hand}：{(definition != null ? ItemName(definition) + " ［" + RarityLabel(instance.Rarity) + "］" : equipment.UsesTwoHandedWeapon ? "装備情報を同期中……" : "装備なし")}", style);
        bool previousEnabled = GUI.enabled;
        try
        {
            GUI.enabled = previousEnabled && instance.IsValid;
            if (GUI.Button(new Rect(area.xMax - 150, area.y, 150, area.height), hand + "を外す"))
            {
                equipment.EquipWeapon(-1, leftHand);
                GUIUtility.ExitGUI();
            }
        }
        finally { GUI.enabled = previousEnabled; }
    }

    private static void SetLabelColor(GUIStyle style, Color color)
    {
        style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = color;
        style.onNormal.textColor = style.onHover.textColor = style.onActive.textColor = style.onFocused.textColor = color;
    }

    private static void FillRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static void DrawWhiteBorder(Rect rect)
    {
        FillRect(new Rect(rect.x, rect.y, rect.width, 2), Color.white);
        FillRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), Color.white);
        FillRect(new Rect(rect.x, rect.y, 2, rect.height), Color.white);
        FillRect(new Rect(rect.xMax - 2, rect.y, 2, rect.height), Color.white);
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
        guardianLabel = NewText("Guardian Status");
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
        Place(playerText, 0, 0, 32, 144, 720, 200, 26, TextAlignmentOptions.TopLeft);
        Place(guardianLabel, 0, 0, 32, 150, 600, 40, 24, TextAlignmentOptions.TopLeft);
        Place(leftLabel, 0, 0, 32, 24, 620, 100, 26, TextAlignmentOptions.TopLeft);
        Place(rightLabel, .5f, 0, 0, 24, 620, 100, 26, TextAlignmentOptions.TopLeft);
        Place(hint, 1, 0, -32, 24, 280, 44, 24, TextAlignmentOptions.Right);
        Place(messageText, .5f, 0, 0, 300, 1000, 100, 28, TextAlignmentOptions.Center);
        Place(resultText, .5f, .5f, 0, 0, 1000, 200, 44, TextAlignmentOptions.Center);
        Place(crosshair, .5f, .5f, 0, 0, 24, 24, 24, TextAlignmentOptions.Center);
        crosshair.text = "+";
        playerBar = MakeBar("Player HP", new Vector2(0, 0), new Vector2(32, 288), 400);
        fortressBar = MakeBar("Fortress HP", new Vector2(.5f, 1), new Vector2(0, -124), 480);
        battleRoot.SetActive(false);
    }

    private static string GuardianStatusLabel(GuardianActivationStatus status)
    {
        switch (status)
        {
            case GuardianActivationStatus.Unowned: return "";
            case GuardianActivationStatus.Active: return "守護者：発動中";
            case GuardianActivationStatus.OutOfRange: return "守護者：範囲外";
            default: return "守護者：待機中";
        }
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
        if (player.Equipment.UsesTwoHandedWeapon && !player.Equipment.IsAttackHandEnabled(leftHand))
            return "右クリック：叩きつけ（未実装）";
        var instance = player.Equipment.GetWeaponInstance(leftHand);
        var definition = instance.Definition;
        string input = leftHand ? "左クリック" : "右クリック";
        if (player.Equipment.UsesTwoHandedWeapon && (!instance.IsValid || definition == null))
            return $"{input}：両手装備の情報を同期中……";
        if (!instance.IsValid || definition == null)
            return $"{input}：素手\n攻撃力 {player.GetAttack(leftHand):0.##}";
        return $"{input}：{ItemName(definition)}{(player.Equipment.UsesTwoHandedWeapon ? "（両手）" : "")} ［{RarityLabel(instance.Rarity)}］\n" +
            (definition.IsShield ? $"防御力 {FormatReduction(DisplayedReduction(instance))}"
                : $"攻撃力 {player.GetAttack(leftHand):0.##}");
    }

    private static string ItemName(ItemData item) => LocalizeWords(item.displayName);

    private static string EquippedHandLabel(ulong instanceId, ulong leftId, ulong rightId) => instanceId == 0 ? ""
        : instanceId == leftId && instanceId == rightId ? "両手"
        : instanceId == leftId ? "左手" : instanceId == rightId ? "右手" : "";

    private static Color RarityColor(ItemRarity rarity) => rarity == ItemRarity.Common
        ? Color.white : ItemData.GetRarityColor(rarity);

    private static Color WeaponColor(ItemInstance instance) => instance.IsValid
        ? RarityColor(instance.Rarity) : Color.white;

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
        .Replace("Crystal Sword", "クリスタルソード").Replace("Magic Book", "魔法の本").Replace("Shield", "盾")
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
