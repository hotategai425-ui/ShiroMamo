using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 各プレイヤーの選択だけを送る。停止・再開と能力反映はホストが決定する。
public class LevelUpChoiceUI : MonoBehaviour
{
    private PlayerStats player;
    private Font font;
    private GUISkin skin;
    private LevelUpOffer displayedOffer;
    private ulong sentTicket;
    [Tooltip("クリック・決定キーを離してから選択可能になるまでの実時間（秒）。連打中は待ち直す。")]
    [Min(0.1f), SerializeField] private float inputQuietSeconds = 0.35f;
    private ulong gatedTicket;
    private ulong gestureTicket;
    private double releasedAt = -1;
    private bool inputReady;
    private int armedFrame = -1;
    private bool clearGuiInput;

    private void Awake()
    {
        player = GetComponent<PlayerStats>();
        font = Resources.Load<Font>("Fonts/NotoSansJP");
    }

    private void OnEnable() { displayedOffer = default; ResetInputGate(); }
    private void OnDisable() => ResetInputGate();

    private void Update() => RefreshInputGate();

    private void RefreshInputGate()
    {
        bool visible = player != null && player.IsSpawned && player.IsOwner && player.IsLevelUpChoicePaused;
        ulong ticket = visible && player.CurrentLevelUpOffer.IsValid ? player.CurrentLevelUpOffer.Ticket : 0;
        AdvanceInputGate(ticket, ChoiceInputHeld(), Application.isFocused, Time.unscaledTimeAsDouble, Time.frameCount);
    }

    private static bool ChoiceInputHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        return (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
            || (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed || keyboard.spaceKey.isPressed));
#else
        return Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetKey(KeyCode.Return)
            || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space);
#endif
    }

    private void ResetInputGate()
    {
        gatedTicket = gestureTicket = 0;
        releasedAt = -1;
        inputReady = false;
        armedFrame = -1;
        clearGuiInput = true;
    }

    private void AdvanceInputGate(ulong ticket, bool held, bool focused, double now, int frame)
    {
        if (ticket != gatedTicket || !focused || ticket == 0)
        {
            ResetInputGate();
            gatedTicket = ticket;
        }
        if (ticket == 0 || !focused || sentTicket == ticket || inputReady) return;
        if (held) { releasedAt = -1; return; }
        if (releasedAt < 0) releasedAt = now;
        if (now - releasedAt < Mathf.Max(0.1f, inputQuietSeconds)) return;
        inputReady = true;
        armedFrame = frame;
    }

    private void ObserveSelectionEvent(EventType type, int button, KeyCode key, int frame)
    {
        bool newPress = (type == EventType.MouseDown && (button == 0 || button == 1))
            || (type == EventType.KeyDown && (key == KeyCode.Return || key == KeyCode.KeypadEnter || key == KeyCode.Space));
        if (!newPress) return;
        if (!inputReady || frame <= armedFrame)
        {
            releasedAt = -1;
            gestureTicket = 0;
            inputReady = false;
            return;
        }
        if (type == EventType.KeyDown || button == 0) gestureTicket = gatedTicket;
    }

    private bool TrySendChoice(ulong ticket, int index)
    {
        if (!inputReady || ticket == 0 || gatedTicket != ticket || gestureTicket != ticket || sentTicket == ticket
            || player == null || player.CurrentLevelUpOffer.Ticket != ticket || index < 0 || index > 2) return false;
        sentTicket = ticket;
        gestureTicket = 0;
        inputReady = false;
        player.ChooseLevelUp(ticket, index);
        return true;
    }

    private void OnGUI()
    {
        if (player == null || !player.IsSpawned || !player.IsOwner || !player.IsLevelUpChoicePaused) return;
        if (skin == null)
        {
            skin = Instantiate(GUI.skin);
            if (font != null) skin.font = font;
            skin.label.fontSize = 18;
            skin.label.wordWrap = true;
            skin.label.padding = new RectOffset(10, 10, 7, 7);
            skin.button.fontSize = 20;
            skin.button.wordWrap = true;
            skin.button.padding = new RectOffset(12, 12, 12, 12);
        }
        if (Event.current.type == EventType.Layout || !displayedOffer.IsValid)
            displayedOffer = player.CurrentLevelUpOffer;
        RefreshInputGate();
        if (clearGuiInput)
        {
            // 前の画面の押下中コントロールや決定キーのフォーカスを引き継がない。
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
            clearGuiInput = false;
        }
        ObserveSelectionEvent(Event.current.type, Event.current.button, Event.current.keyCode, Time.frameCount);

        GUISkin previousSkin = GUI.skin;
        bool previousEnabled = GUI.enabled;
        int previousDepth = GUI.depth;
        try
        {
            GUI.skin = skin;
            GUI.depth = -100;
            float width = Mathf.Min(900f, Screen.width - 24f);
            float height = Mathf.Min(440f, Screen.height - 24f);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height), GUI.skin.box);
            GUILayout.Label($"レベルアップ！ チーム共有レベル {player.Level}");
            GUILayout.Label("全員が選び終わるまで、戦闘とウェーブ時間を停止しています。");
            if (!displayedOffer.IsValid)
                GUILayout.Label("自分の選択は完了しました。相棒の選択を待っています……");
            else
            {
                GUILayout.Label($"レベル {displayedOffer.Level} の強化を1つ選んでください（残り {displayedOffer.PendingCount} 回）");
                GUILayout.Label("カーソルを下の選択肢へ移動して選んでください。");
                // FPSのロック解除直後の中央カーソルが、選択ボタンに重ならない配置にする。
                float choicesTop = height * 0.5f + 24f;
                GUILayout.BeginArea(new Rect(10f, choicesTop, width - 20f, height - choicesTop - 10f));
                GUILayout.BeginHorizontal();
                for (int i = 0; i < 3; i++)
                {
                    UpgradeChoice choice = displayedOffer.GetChoice(i);
                    if (!choice.IsValid) continue;
                    string name = player.UpgradeCatalog != null ? player.UpgradeCatalog.Find(choice.DefinitionId)?.displayName : null;
                    GUI.enabled = previousEnabled && inputReady && Time.frameCount > armedFrame
                        && gatedTicket == displayedOffer.Ticket && player.CurrentLevelUpOffer.Ticket == displayedOffer.Ticket
                        && sentTicket != displayedOffer.Ticket;
                    if (GUILayout.Button($"{name ?? choice.Stat.ToString()}\n\n{Describe(choice)}", GUILayout.MinHeight(Mathf.Min(130f, height - choicesTop - 64f)), GUILayout.ExpandWidth(true)))
                    {
                        if (TrySendChoice(displayedOffer.Ticket, i)) GUIUtility.ExitGUI();
                    }
                }
                GUILayout.EndHorizontal();
                GUI.enabled = previousEnabled;
                if (sentTicket == displayedOffer.Ticket) GUILayout.Label("選択を送信しています……");
                else if (!inputReady) GUILayout.Label("誤選択防止のため、クリック・決定キーを離して少し待ってください。");
                GUILayout.EndArea();
            }
            GUILayout.EndArea();
        }
        finally
        {
            GUI.skin = previousSkin;
            GUI.enabled = previousEnabled;
            GUI.depth = previousDepth;
        }
    }

    private static string Describe(UpgradeChoice choice)
    {
        switch (choice.Stat)
        {
            case UpgradeStat.CriticalChance: return $"会心率 +{choice.Amount * 100f:0.#}%（最大100%）";
            case UpgradeStat.CriticalDamage: return $"会心時の倍率 +{choice.Amount:0.##}倍";
            case UpgradeStat.AttackSpeed: return $"攻撃速度 +{choice.Amount * 100f:0.#}%";
            case UpgradeStat.MoveSpeed: return $"移動速度 +{choice.Amount:0.##}m/s";
            case UpgradeStat.JumpHeight: return $"ジャンプの高さ +{choice.Amount:0.##}m";
            default: return $"+{choice.Amount:0.##}";
        }
    }

    private void OnDestroy() { if (skin != null) Destroy(skin); }
}
