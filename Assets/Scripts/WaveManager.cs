using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GamePhase { Lobby, Combat, Collect, Victory, Defeat }

// ウェーブ進行(ホストが管理し、全員に同期)
// ロビー → [戦闘2分 → 回収1分] × 3ウェーブ → クリア
// GameSystems オブジェクトに NetworkObject と一緒に付ける
public class WaveManager : NetworkBehaviour
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

    [SerializeField] private WaveSetting[] waves =
    {
        new WaveSetting { spawnInterval = 3.0f, hpMultiplier = 1.0f, damageMultiplier = 1.0f, speedMultiplier = 1.0f, expMultiplier = 1.0f, dropRateMultiplier = 1.0f },
        new WaveSetting { spawnInterval = 2.5f, hpMultiplier = 1.6f, damageMultiplier = 1.4f, speedMultiplier = 1.1f, expMultiplier = 1.3f, dropRateMultiplier = 1.2f },
        new WaveSetting { spawnInterval = 2.0f, hpMultiplier = 2.5f, damageMultiplier = 2.0f, speedMultiplier = 1.2f, expMultiplier = 1.6f, dropRateMultiplier = 1.4f },
    };

    // ---- 同期される値(書き込めるのはホストだけ) ----
    private NetworkVariable<GamePhase> phase = new NetworkVariable<GamePhase>(GamePhase.Lobby);
    private NetworkVariable<int> wave = new NetworkVariable<int>(1);
    // フェーズ終了時刻(サーバー時刻)。毎フレーム同期せず、各自が残り時間を計算する
    private NetworkVariable<double> phaseEndTime = new NetworkVariable<double>(0.0);

    public int CurrentWave => wave.Value;
    public GamePhase Phase => phase.Value;
    public int TotalWaves => waves.Length;
    public bool IsFinished => Phase == GamePhase.Victory || Phase == GamePhase.Defeat;
    public WaveSetting CurrentSetting => waves[Mathf.Clamp(CurrentWave - 1, 0, waves.Length - 1)];

    public float TimeRemaining
    {
        get
        {
            if (!IsSpawned) return 0f;
            if (Phase != GamePhase.Combat && Phase != GamePhase.Collect) return 0f;
            return Mathf.Max(0f, (float)(phaseEndTime.Value - NetworkManager.ServerTime.Time));
        }
    }

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        if (waves == null || waves.Length == 0)
            Debug.LogError("WaveManager: waves が空です");
    }

    // ホストがロビーで「Start Game」を押したときに呼ばれる
    public void StartGame()
    {
        if (!IsServer || Phase != GamePhase.Lobby) return;
        wave.Value = 1;
        SetPhase(GamePhase.Combat);
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned) return;

        if (Phase == GamePhase.Lobby) return;

        if (IsFinished)
        {
            // ホストが R でリスタート(全員のシーンを読み込み直す)
            if (GameInput.RestartPressed)
            {
                NetworkManager.SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
            }
            return;
        }

        if (NetworkManager.ServerTime.Time >= phaseEndTime.Value) NextPhase();
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
                wave.Value = CurrentWave + 1;
                SetPhase(GamePhase.Combat);
            }
        }
    }

    // 砦が壊れたとき Fortress から呼ばれる(ホストのみ)
    public void Defeat()
    {
        if (!IsServer || IsFinished) return;
        SetPhase(GamePhase.Defeat);
    }

    private void SetPhase(GamePhase next)
    {
        phase.Value = next;
        double now = NetworkManager.ServerTime.Time;

        switch (next)
        {
            case GamePhase.Combat:
                phaseEndTime.Value = now + combatDuration;
                Pickup.ClearAll(); // 前ウェーブの拾い忘れを消す
                AnnounceRpc($"WAVE {wave.Value} START!");
                break;
            case GamePhase.Collect:
                phaseEndTime.Value = now + collectDuration;
                Monster.DespawnAll(); // 残ったモンスターは撤退(ドロップなし)
                AnnounceRpc("COLLECT TIME! Pick up drops");
                break;
            case GamePhase.Victory:
                phaseEndTime.Value = now;
                Monster.DespawnAll();
                break;
            case GamePhase.Defeat:
                phaseEndTime.Value = now;
                Monster.DespawnAll();
                break;
        }
    }

    // 全員の画面にメッセージを出す
    [Rpc(SendTo.Everyone)]
    private void AnnounceRpc(string message)
    {
        GameLog.Post(message);
    }
}
