using UnityEngine;

// 一人称カメラ。Main Camera に付ける(CameraFollow の代わり)。
// マウスで見回し、プレイヤーの目の位置に追従する。
// カメラ自身が回転を持つので、プレイヤーの子にはしないこと。
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;           // Player
    [SerializeField] private float eyeHeight = 0.6f;     // カプセル中心からの高さ(中心が高さ1なら目は1.6)
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private bool hideTargetRenderers = true;

    private float yaw;
    private float pitch;

    // 左右の向き(PlayerController3D が体の向きと移動方向に使う)
    public float Yaw => yaw;

    private void Start()
    {
        if (target != null)
        {
            yaw = target.eulerAngles.y;

            // 自分のモデルが視界に映らないよう非表示(影だけ残す)
            if (hideTargetRenderers)
            {
                foreach (var r in target.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }

        SetCursorLocked(true);
    }

    private void Update()
    {
        var wm = WaveManager.Instance;
        bool finished = wm != null && wm.IsFinished;

        // クリア/ゲームオーバー中はカーソルを解放して視点操作を止める
        SetCursorLocked(!finished);
        if (finished) return;

        Vector2 look = GameInput.Look * sensitivity;
        yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        transform.position = target.position + Vector3.up * eyeHeight;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}