using UnityEngine;

// DAMAGE-01の表示用仮値。ダメージ計算や通信設定は持たない。
[CreateAssetMenu(menuName = "ShiroMamo/Damage Number Settings")]
public sealed class DamageNumberSettings : ScriptableObject
{
    [Header("位置・演出（ゲーム全体の停止に従う）")]
    public Vector3 worldOffset = new Vector3(0, 0.15f, 0);
    [Min(0.05f)] public float lifetime = 0.9f;
    [Min(0)] public float riseDistance = 0.65f;
    [Range(0, 0.95f)] public float fadeStartRatio = 0.3f;
    [Header("Full HD基準の文字サイズ・色")]
    [Min(8)] public float fontSize = 36;
    [Min(1)] public float criticalScale = 1.25f;
    public Color normalColor = Color.white;
    public Color criticalColor = new Color(1, 1, 0, 1);
    public Vector2 textArea = new Vector2(240, 90);
    [Header("連打時の横ずらし・同時表示数")]
    [Min(0)] public float horizontalSpacing = 40;
    [Range(1, 9)] public int horizontalSlots = 5;
    [Range(1, 256)] public int maxVisibleNumbers = 96;
    [Header("遮蔽（敵・プレイヤー・拾得物・魔法弾は除外）")]
    public LayerMask environmentMask = ~0;
    [Min(0)] public float occlusionPadding = 0.02f;
}
