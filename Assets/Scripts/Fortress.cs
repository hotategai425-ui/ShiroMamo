using UnityEngine;

// 中心の砦。HPが0になるとゲームオーバー
public class Fortress : MonoBehaviour
{
    public static Fortress Instance { get; private set; }

    [SerializeField] private float maxHp = 1000f;
    [Tooltip("砦の半径。モンスターはこの外周で攻撃を始める")]
    [SerializeField] private float radius = 2.5f;

    public float MaxHp => maxHp;
    public float CurrentHp { get; private set; }
    public float Radius => radius;

    private void Awake()
    {
        Instance = this;
        CurrentHp = maxHp;
    }

    public void TakeDamage(float amount)
    {
        if (CurrentHp <= 0f) return;

        CurrentHp = Mathf.Max(0f, CurrentHp - amount);
        if (CurrentHp <= 0f && WaveManager.Instance != null)
            WaveManager.Instance.Defeat();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
