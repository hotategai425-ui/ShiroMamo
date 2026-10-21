using UnityEngine;

// 一人称カメラ(オンライン版)。Main Camera に付ける。
// 自分が操作するプレイヤーを自動で見つけて、その目の位置に追従する。
// Target の設定は不要。Main Camera はシーン直下に置き、プレイヤーの子にはしないこと。
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private float eyeHeight = 0.6f;     // カプセル中心からの高さ
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private bool hideOwnModel = true;

    private float yaw;
    private float pitch;
    private PlayerStats current;
    private bool cursorLocked;

    // 左右の向き(PlayerController3D が移動方向に使う)
    public float Yaw => yaw;

    private void Update()
    {
        var local = PlayerStats.Local;

        // 自分のプレイヤーが出現した(または変わった)とき
        if (local != current)
        {
            current = local;
            if (local != null)
            {
                yaw = local.transform.eulerAngles.y;
                pitch = 0f;
                if (hideOwnModel)
                {
                    // 自分のモデルが視界に映らないよう非表示(影だけ残す)
                    foreach (var r in local.GetComponentsInChildren<Renderer>())
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }
        }

        // 戦闘/回収中だけ視点操作+カーソル固定。ロビーやクリア後はカーソルを出す
        var wm = WaveManager.Instance;
        bool active = local != null && wm != null &&
                      (wm.Phase == GamePhase.Combat || wm.Phase == GamePhase.Collect);
        SetCursorLocked(active);
        if (!active) return;

        Vector2 look = GameInput.Look * sensitivity;
        yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        if (current == null) return;

        transform.position = current.transform.position + Vector3.up * eyeHeight;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void SetCursorLocked(bool locked)
    {
        if (locked == cursorLocked) return;
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
