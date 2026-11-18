using UnityEngine;

// 各プレイヤーの選択だけを送る。停止・再開と能力反映はホストが決定する。
public class LevelUpChoiceUI : MonoBehaviour
{
    private PlayerStats player;
    private Font font;
    private GUISkin skin;
    private LevelUpOffer displayedOffer;
    private ulong sentTicket;

    private void Awake()
    {
        player = GetComponent<PlayerStats>();
        font = Resources.Load<Font>("Fonts/NotoSansJP");
    }

    private void OnGUI()
    {
        if (player == null || !player.IsSpawned || !player.IsOwner || !player.IsTeamLevelUpPaused) return;
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

        GUISkin previousSkin = GUI.skin;
        bool previousEnabled = GUI.enabled;
        int previousDepth = GUI.depth;
        try
        {
            GUI.skin = skin;
            GUI.depth = -100;
            float width = Mathf.Min(900f, Screen.width - 24f);
            float height = Mathf.Min(360f, Screen.height - 24f);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height), GUI.skin.box);
            GUILayout.Label($"レベルアップ！ チーム共有レベル {player.Level}");
            GUILayout.Label("全員が選び終わるまで、戦闘とウェーブ時間を停止しています。");
            if (!displayedOffer.IsValid)
                GUILayout.Label("自分の選択は完了しました。相棒の選択を待っています……");
            else
            {
                GUILayout.Label($"レベル {displayedOffer.Level} の強化を1つ選んでください（残り {displayedOffer.PendingCount} 回）");
                GUILayout.BeginHorizontal();
                for (int i = 0; i < 3; i++)
                {
                    UpgradeChoice choice = displayedOffer.GetChoice(i);
                    if (!choice.IsValid) continue;
                    string name = player.UpgradeCatalog != null ? player.UpgradeCatalog.Find(choice.DefinitionId)?.displayName : null;
                    GUI.enabled = previousEnabled && sentTicket != displayedOffer.Ticket;
                    if (GUILayout.Button($"{name ?? choice.Stat.ToString()}\n\n{Describe(choice)}", GUILayout.MinHeight(130f), GUILayout.ExpandWidth(true)))
                    {
                        sentTicket = displayedOffer.Ticket;
                        player.ChooseLevelUp(displayedOffer.Ticket, i);
                        GUIUtility.ExitGUI();
                    }
                }
                GUILayout.EndHorizontal();
                GUI.enabled = previousEnabled;
                if (sentTicket == displayedOffer.Ticket) GUILayout.Label("選択を送信しています……");
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
