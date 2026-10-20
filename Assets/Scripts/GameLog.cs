using System;

// 画面に出すメッセージ用の簡易イベント
public static class GameLog
{
    public static event Action<string> OnMessage;

    public static void Post(string message)
    {
        OnMessage?.Invoke(message);
    }
}
