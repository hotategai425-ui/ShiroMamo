using System.Globalization;
using TMPro;
using UnityEngine;

// 敵のTransformを保持しないローカル表示。Canvasの親も敵から独立している。
public sealed class DamageNumberView : MonoBehaviour
{
    private TMP_Text label;
    private DamageNumberSettings settings;
    private Vector3 origin;
    private float age;
    private float horizontalOffset;
    private Color color;
    private bool alive;

    public bool IsAlive => alive;

    public void Hide()
    {
        alive = false;
        gameObject.SetActive(false);
    }

    public void Initialize(TMP_FontAsset font)
    {
        label = gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        label.richText = false;
        label.outlineWidth = 0.15f;
        label.outlineColor = Color.black;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    public void Show(float amount, Vector3 worldPosition, bool critical, int slot, DamageNumberSettings values)
    {
        settings = values;
        origin = worldPosition + settings.worldOffset;
        age = 0;
        alive = true;
        int slots = Mathf.Max(1, settings.horizontalSlots);
        horizontalOffset = (slot % slots - (slots - 1) * 0.5f) * settings.horizontalSpacing;
        color = critical ? settings.criticalColor : settings.normalColor;
        label.text = amount.ToString("0.#", CultureInfo.InvariantCulture);
        label.fontSize = settings.fontSize * (critical ? settings.criticalScale : 1);
        label.color = color;
        label.rectTransform.sizeDelta = settings.textArea;
        label.enabled = false;
        gameObject.SetActive(true);
    }

    public void Advance(float scaledDeltaTime)
    {
        if (!alive) return;
        age += Mathf.Max(0, scaledDeltaTime);
        if (age >= Mathf.Max(0.05f, settings.lifetime))
        {
            Hide();
        }
    }

    public void RenderForCamera(Camera camera, RectTransform canvasRect)
    {
        if (!alive) return;
        label.enabled = false;
        if (camera == null || !camera.isActiveAndEnabled) return;
        float progress = Mathf.Clamp01(age / Mathf.Max(0.05f, settings.lifetime));
        Vector3 world = origin + Vector3.up * (settings.riseDistance * progress);
        Vector3 viewport = camera.WorldToViewportPoint(world);
        if (viewport.z <= camera.nearClipPlane || viewport.z >= camera.farClipPlane
            || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return;
        Vector3 screen = camera.WorldToScreenPoint(world);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 point)) return;
        point.x += horizontalOffset;
        // 横ずらし後も画面端へ貼り付けず、文字領域全体が外れる場合は隠す。
        Rect bounds = canvasRect.rect;
        Vector2 half = label.rectTransform.sizeDelta * 0.5f;
        if (point.x - half.x < bounds.xMin || point.x + half.x > bounds.xMax
            || point.y - half.y < bounds.yMin || point.y + half.y > bounds.yMax) return;
        if (IsOccluded(camera, world)) return;
        // 横ずらしされた実表示位置にも壁がないことを確認する。
        Vector2 shifted = RectTransformUtility.WorldToScreenPoint(null, canvasRect.TransformPoint(point));
        Vector3 shiftedWorld = camera.ScreenToWorldPoint(new Vector3(shifted.x, shifted.y, screen.z));
        if (IsOccluded(camera, shiftedWorld)) return;
        label.rectTransform.anchoredPosition = point;
        float fade = Mathf.InverseLerp(settings.fadeStartRatio, 1, progress);
        label.color = new Color(color.r, color.g, color.b, color.a * (1 - fade));
        label.enabled = true;
    }

    private bool IsOccluded(Camera camera, Vector3 target)
    {
        Vector3 start = camera.transform.position;
        Vector3 delta = target - start;
        float distance = delta.magnitude;
        if (distance <= 0.001f) return false;
        // カメラが壁の内側にあるとRaycastの始点が検出されないため、始点も確認。
        foreach (var collider in Physics.OverlapSphere(start, 0.01f,
            settings.environmentMask, QueryTriggerInteraction.Ignore))
            if (IsEnvironment(collider)) return true;
        // RaycastAllで動的キャラクターを除外しても、その奥の壁を見落とさない。
        foreach (var hit in Physics.RaycastAll(start, delta / distance,
            Mathf.Max(0, distance - settings.occlusionPadding), settings.environmentMask, QueryTriggerInteraction.Ignore))
        {
            if (IsEnvironment(hit.collider)) return true;
        }
        return false;
    }

    private static bool IsEnvironment(Collider collider) => !collider.GetComponentInParent<Monster>()
        && !collider.GetComponentInParent<PlayerStats>() && !collider.GetComponentInParent<Pickup>()
        && !collider.GetComponentInParent<MagicProjectile>();
}
