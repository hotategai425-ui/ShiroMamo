using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GamePhase { Combat, Collect, Victory, Defeat }

// ウェーブ進行: 戦闘2分 → 回収1分 を3ウェーブ繰り返す
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Serializable]
    public class WaveSetting
    {
        [Tooltip("モンスターの出現間隔(秒)")] public float spawnInterval = 3f;
        public float hpMultiplier = 1f;
        public float damageMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float expMultiplier = 1f;
        public float dropRateMultiplier = 1f;
    }

    [SerializeField] private float combatDuration = 120f;
    [SerializeField] private float collectDuration = 60f;

    // 要素数 = ウェーブ数。ウェーブごとの強化倍率
    [SerializeField] private WaveSetting[] waves =
    {
        new WaveSetting { spawnInterval = 3.0f, hpMultiplier = 1.0f, damageMultiplier = 1.0f, speedMultiplier = 1.0f, expMultiplier = 1.0f, dropRateMultiplier = 1.0f },
        new WaveSetting { spawnInterval = 2.5f, hpMultiplier = 1.6f, damageMultiplier = 1.4f, speedMultiplier = 1.1f, expMultiplier = 1.3f, dropRateMultiplier = 1.2f },
        new WaveSetting { spawnInterval = 2.0f, hpMultiplier = 2.5f, damageMultiplier = 2.0f, speedMultiplier = 1.2f, expMultiplier = 1.6f, dropRateMultiplier = 1.4f },
    };

    public int CurrentWave { get; private set; } = 1;
    public GamePhase Phase { get; private set; }
    public float TimeRemaining { get; private set; }
    public int TotalWaves => waves.Length;
    public bool IsFinished => Phase == GamePhase.Victory || Phase == GamePhase.Defeat;
    public WaveSetting CurrentSetting => waves[Mathf.Clamp(CurrentWave - 1, 0, waves.Length - 1)];

    public event Action<int, GamePhase> OnPhaseChanged;

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        if (waves == null || waves.Length == 0)
            Debug.LogError("WaveManager: waves が空です");
    }

    private void Start()
    {
        CurrentWave = 1;
        SetPhase(GamePhase.Combat);
    }

    private void Update()
    {
        if (IsFinished)
        {
            if (GameInput.RestartPressed)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining <= 0f) NextPhase();
    }

    private void NextPhase()
    {
        if (Phase == GamePhase.Combat)
        {
            SetPhase(GamePhase.Collect);
        }
        else if (Phase == GamePhase.Collect)
        {
            if (CurrentWave >= TotalWaves)
            {
                SetPhase(GamePhase.Victory);
            }
            else
            {
                CurrentWave++;
                SetPhase(GamePhase.Combat);
            }
        }
    }

    public void Defeat()
    {
        if (IsFinished) return;
        SetPhase(GamePhase.Defeat);
    }

    private void SetPhase(GamePhase next)
    {
        Phase = next;
        switch (next)
        {
            case GamePhase.Combat:
                TimeRemaining = combatDuration;
                Pickup.ClearAll(); // 前ウェーブの拾い忘れを消す
                GameLog.Post($"WAVE {CurrentWave} START!");
                break;
            case GamePhase.Collect:
                TimeRemaining = collectDuration;
                Monster.DespawnAll(); // 残ったモンスターは撤退(ドロップなし)
                GameLog.Post("COLLECT TIME! Pick up drops");
                break;
            case GamePhase.Victory:
            case GamePhase.Defeat:
                TimeRemaining = 0f;
                Time.timeScale = 0f;
                break;
        }
        OnPhaseChanged?.Invoke(CurrentWave, next);
    }
}
