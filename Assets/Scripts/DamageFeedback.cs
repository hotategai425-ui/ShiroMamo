using System;
using UnityEngine;

// UIとの共通契約。敵本体が消えた後も使える、確定値とワールド位置だけを渡す。
public struct DamageFeedbackData
{
    public float Amount;
    public Vector3 WorldPosition;
    public bool IsCritical;
}

// ホストの1回の会心抽選結果。数値を調べ直して会心かどうかを推測しない。
public struct AttackDamageResult
{
    public float Amount;
    public bool IsCritical;
}

public static class DamageFeedback
{
    public static event Action<DamageFeedbackData> DamageConfirmed;

    internal static void Publish(DamageFeedbackData data)
    {
        if (!(data.Amount > 0f) || float.IsInfinity(data.Amount) || !IsFinite(data.WorldPosition)) return;
        DamageConfirmed?.Invoke(data);
    }

    internal static bool IsFinite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
        && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
}
