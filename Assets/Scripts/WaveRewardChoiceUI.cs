using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 各プレイヤーの選択だけを送る。停止・再開と能力反映はホストが決定する。
public class WaveRewardChoiceUI : MonoBehaviour
{
    private PlayerStats player;
    private Font font;
    private GUISkin skin;
    private WaveRewardOffer displayedOffer;
    private ulong sentTicket;
    [Tooltip("クリック・決定キーを離してから選択可能になるまでの実時間（秒）。連打中は待ち直す。")]
    [Min(0.1f), SerializeField] private float inputQuietSeconds = 0.35f;
    private ulong gatedTicket;
    private ulong gestureTicket;
    private double releasedAt = -1;
    private bool inputReady;
    private int armedFrame = -1;
    private bool clearGuiInput;
    private Vector2 resultsScroll;

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
        bool visible = player != null && player.IsSpawned && player.IsOwner && player.IsWaveRewardChoicePaused;
        ulong ticket = visible && player.CurrentWaveRewardOffer.IsValid ? player.CurrentWaveRewardOffer.Ticket : 0;
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
            || player == null || !player.IsWaveRewardChoicePaused || player.CurrentWaveRewardOffer.Ticket != ticket
            || index < 0 || index > 2 || player.CurrentWaveRewardOffer.GetChoice(index) <= 0) return false;
        sentTicket = ticket;
        gestureTicket = 0;
        inputReady = false;
        player.ChooseWaveReward(ticket, index);
        return true;
    }

    private string RewardName(int id)
    {
        string name = player.RewardCatalog != null ? player.RewardCatalog.Find(id)?.displayName : null;
        return string.IsNullOrWhiteSpace(name) ? $"候補 ID {id}（名前未設定）" : name;
    }

    private string BuildResultsText()
    {
        var results = player.WaveRewardResults;
        if (results == null || results.Count == 0) return "この試合の取得：まだありません。";
        var text = new System.Text.StringBuilder();
        var latest = results[results.Count - 1];
        text.Append("直近の取得（ウェーブ ").Append(latest.Wave).Append("）：").AppendLine(RewardName(latest.DefinitionId));
        var counts = new System.Collections.Generic.Dictionary<int, int>();
        var order = new System.Collections.Generic.List<int>();
        foreach (var result in results)
        {
            if (!counts.ContainsKey(result.DefinitionId)) { counts[result.DefinitionId] = 0; order.Add(result.DefinitionId); }
            counts[result.DefinitionId]++;
        }
        text.Append("この試合の取得：");
        foreach (int id in order)
        {
            if (id != order[0]) text.Append(" ／ ");
            text.Append(RewardName(id)).Append(" ×").Append(counts[id]);
        }
        return text.ToString();
    }

    private void OnGUI()
    {
        if (player == null || !player.IsSpawned || !player.IsOwner || !player.IsWaveRewardChoicePaused) return;
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
            displayedOffer = player.CurrentWaveRewardOffer;
        RefreshInputGate();
        if (clearGuiInput)
        {
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
            float width = Mathf.Min(1000f, Screen.width - 24f);
            float height = Mathf.Min(580f, Screen.height - 24f);
            float choicesTop = height * 0.5f + 24f;
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height), skin.box);
            GUILayout.Label(displayedOffer.IsValid ? $"ウェーブ {displayedOffer.Wave} 終了：報酬を1つ選択" : "ウェーブ報酬：選択待ち");
            GUILayout.Label("全員が選び終わるまで、ゲームと時計を停止しています。完了後に回収を開始します。");
            GUILayout.Label("候補ごとの効果を確認して選んでください。効果なしの試験用候補も含まれます。");
            resultsScroll = GUILayout.BeginScrollView(resultsScroll, GUILayout.Height(Mathf.Max(40f, choicesTop - 180f)));
            GUILayout.Label(BuildResultsText());
            GUILayout.EndScrollView();
            GUILayout.BeginArea(new Rect(10f, choicesTop, width - 20f, height - choicesTop - 10f));
            if (!displayedOffer.IsValid)
                GUILayout.Label("自分の選択は完了しました。相棒の選択を待っています……");
            else
            {
                GUILayout.Label("カーソルを下の選択肢へ移動して選んでください。");
                GUILayout.BeginHorizontal();
                for (int i = 0; i < 3; i++)
                {
                    int id = displayedOffer.GetChoice(i);
                    if (id <= 0) continue;
                    string description = player.GetWaveRewardDescription(id);
                    if (string.IsNullOrWhiteSpace(description)) description = "候補の説明が見つかりません。";
                    GUI.enabled = previousEnabled && inputReady && Time.frameCount > armedFrame
                        && gatedTicket == displayedOffer.Ticket && player.CurrentWaveRewardOffer.Ticket == displayedOffer.Ticket
                        && sentTicket != displayedOffer.Ticket;
                    if (GUILayout.Button($"{RewardName(id)}\n\n{description}", GUILayout.MinHeight(Mathf.Clamp(height - choicesTop - 100f, 50f, 130f)), GUILayout.ExpandWidth(true)))
                        if (TrySendChoice(displayedOffer.Ticket, i)) GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();
                GUI.enabled = previousEnabled;
                if (sentTicket == displayedOffer.Ticket) GUILayout.Label("選択を送信しています……");
                else if (!inputReady) GUILayout.Label("誤選択防止のため、クリック・決定キーを離して少し待ってください。");
            }
            GUILayout.EndArea();
            GUILayout.EndArea();
        }
        finally
        {
            GUI.skin = previousSkin;
            GUI.enabled = previousEnabled;
            GUI.depth = previousDepth;
        }
    }

    private void OnDestroy() { if (skin != null) Destroy(skin); }
}

