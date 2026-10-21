using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;

// Relay 接続メニュー(協力タワーディフェンス用)。NetworkManager と同じオブジェクトに付ける。
// 元の NetworkStartUI からの変更点:
//  ・Unity Services の初期化と匿名ログインをここで行う
//  ・最大人数(既定2人)を超える参加を断る
//  ・接続後はロビー表示(ホストに「Start Game」ボタン)。ゲーム開始後はメニューを隠す
//  ・メニューが出ている間だけカーソルを解放(一人称視点のカーソル固定とぶつからないように)
//  ・エラーを画面に表示
public class NetworkStartUI : MonoBehaviour
{
    [SerializeField] int maxPlayers = 2;

    string joinCode = "";
    string shownJoinCode = "";
    string status = "";
    bool initialized = false;
    bool busy = false;

    static string RelayConnectionType
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return "wss";
#else
            return "dtls";
#endif
        }
    }

    static void ConfigureRelayTransport(UnityTransport transport, RelayServerData relayData)
    {
        transport.SetRelayServerData(relayData);
#if UNITY_WEBGL && !UNITY_EDITOR
        transport.UseWebSockets = true;
#else
        transport.UseWebSockets = false;
#endif
    }

    async void Start()
    {
        // ウィンドウが非アクティブでも通信を止めない(2つ起動して試すときに必須)
        Application.runInBackground = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            initialized = true;
        }
        catch (Exception e)
        {
            status = "Relay準備エラー: " + e.Message;
            Debug.LogException(e);
        }
    }

    void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        bool listening = nm != null && nm.IsListening;

        var wm = WaveManager.Instance;
        bool inLobby = wm == null || wm.Phase == GamePhase.Lobby;

        // ゲーム開始後はメニューを隠す(カーソル操作は CameraFollow に任せる)
        if (listening && !inLobby) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GUI.Box(new Rect(20, 20, 320, 300), "");

        GUILayout.BeginArea(new Rect(35, 35, 290, 270));

        GUILayout.Label("Relay Menu");

        if (nm == null)
        {
            GUILayout.Label("NetworkManager がシーンにありません");
        }
        else if (!initialized)
        {
            GUILayout.Label("Relay準備中...");
        }
        else if (busy)
        {
            GUILayout.Label("接続処理中...");
        }
        else if (listening)
        {
            DrawLobby(nm, wm);
        }
        else
        {
            if (GUILayout.Button("Host"))
            {
                StartHostRelay();
            }

            GUILayout.Space(10);

            GUILayout.Label("Join Code");
            joinCode = GUILayout.TextField(joinCode).ToUpper().Trim();

            if (GUILayout.Button("Join"))
            {
                StartClientRelay();
            }
        }

        if (!string.IsNullOrEmpty(status)) GUILayout.Label(status);

        GUILayout.EndArea();
    }

    void DrawLobby(NetworkManager nm, WaveManager wm)
    {
        if (nm.IsServer)
        {
            GUILayout.Label("ホスト  人数: " + nm.ConnectedClientsIds.Count + " / " + maxPlayers);

            if (!string.IsNullOrEmpty(shownJoinCode))
            {
                GUILayout.Label("Join Code(相手に伝える):");
                GUILayout.TextField(shownJoinCode);
            }

            string label = nm.ConnectedClientsIds.Count >= maxPlayers
                ? "Start Game"
                : "Start Game(相手を待たずに開始)";
            if (GUILayout.Button(label) && wm != null) wm.StartGame();
        }
        else if (nm.IsConnectedClient)
        {
            GUILayout.Label("接続しました。ホストの開始を待っています...");
        }
        else
        {
            GUILayout.Label("接続中...");
        }

        GUILayout.Space(10);
        if (GUILayout.Button("切断"))
        {
            nm.Shutdown();
            shownJoinCode = "";
            status = "切断しました";
        }
    }

    // 最大人数を超えた参加を断る
    void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        bool ok = NetworkManager.Singleton.ConnectedClientsIds.Count < maxPlayers;
        response.Approved = ok;
        response.CreatePlayerObject = ok;
        response.Pending = false;
        if (!ok) response.Reason = "満員です";
    }

    async void StartHostRelay()
    {
        busy = true;
        status = "";

        try
        {
            // maxPlayers - 1 = ホスト以外に入れる人数
            Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);

            shownJoinCode =
                await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var nm = NetworkManager.Singleton;
            UnityTransport transport = nm.GetComponent<UnityTransport>();

            ConfigureRelayTransport(
                transport,
                new RelayServerData(allocation, RelayConnectionType)
            );

            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = Approve;

            if (!nm.StartHost()) status = "ホストの開始に失敗しました";
        }
        catch (Exception e)
        {
            status = "Relayエラー: " + e.Message;
            Debug.LogException(e);
        }

        busy = false;
    }

    async void StartClientRelay()
    {
        busy = true;
        status = "";

        try
        {
            if (string.IsNullOrEmpty(joinCode))
            {
                status = "Join Code を入力してください";
                busy = false;
                return;
            }

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            var nm = NetworkManager.Singleton;
            UnityTransport transport = nm.GetComponent<UnityTransport>();

            ConfigureRelayTransport(
                transport,
                new RelayServerData(allocation, RelayConnectionType)
            );

            if (!nm.StartClient()) status = "接続の開始に失敗しました";
        }
        catch (Exception e)
        {
            status = "Relayエラー: " + e.Message;
            Debug.LogException(e);
        }

        busy = false;
    }
}
