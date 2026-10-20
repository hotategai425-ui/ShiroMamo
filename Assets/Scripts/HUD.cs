using TMPro;
using UnityEngine;

// 画面表示(TextMeshPro)。割り当てなくても動くよう null チェック済み
public class HUD : MonoBehaviour
{
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text fortressText;
    [SerializeField] private TMP_Text playerText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private float messageDuration = 3f;

    private float messageHideTime;

    private void OnEnable() { GameLog.OnMessage += ShowMessage; }
    private void OnDisable() { GameLog.OnMessage -= ShowMessage; }

    private void Start()
    {
        if (messageText) messageText.text = "";
        if (resultText) resultText.text = "";
    }

    private void Update()
    {
        var wm = WaveManager.Instance;
        var fort = Fortress.Instance;
        var p = PlayerStats.Instance;

        if (wm != null)
        {
            if (waveText) waveText.text = $"WAVE {wm.CurrentWave}/{wm.TotalWaves}  [{PhaseLabel(wm.Phase)}]";
            if (timerText)
            {
                int t = Mathf.CeilToInt(wm.TimeRemaining);
                timerText.text = $"{t / 60:00}:{t % 60:00}";
            }
            if (resultText)
            {
                if (wm.Phase == GamePhase.Victory) resultText.text = "VICTORY!\nPress R to restart";
                else if (wm.Phase == GamePhase.Defeat) resultText.text = "GAME OVER\nPress R to restart";
            }
        }

        if (fort != null && fortressText)
            fortressText.text = $"FORTRESS HP {Mathf.CeilToInt(fort.CurrentHp)}/{Mathf.CeilToInt(fort.MaxHp)}";

        if (p != null && playerText)
        {
            string weapon = p.Equipment.Weapon != null ? p.Equipment.Weapon.displayName : "-";
            string armor = p.Equipment.Armor != null ? p.Equipment.Armor.displayName : "-";
            playerText.text =
                $"Lv {p.Level}  HP {Mathf.CeilToInt(p.CurrentHp)}/{Mathf.CeilToInt(p.MaxHp)}\n" +
                $"EXP {Mathf.FloorToInt(p.Exp)}/{Mathf.CeilToInt(p.ExpToNext)}\n" +
                $"ATK {p.Attack:0.#}  DEF {p.Defense:0.#}\n" +
                $"Weapon: {weapon}\nArmor: {armor}";
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
