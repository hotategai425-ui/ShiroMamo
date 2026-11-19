using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 確定ダメージのローカルイベントだけを購読。ネットワーク通信やダメージ計算はしない。
public sealed class DamageNumberPresenter : MonoBehaviour
{
    [SerializeField] private DamageNumberSettings settings;
    [SerializeField] private Camera targetCamera;
    private readonly List<DamageNumberView> views = new List<DamageNumberView>();
    private RectTransform canvasRect;
    private TMP_FontAsset font;
    private bool ownsSettings;
    private int nextSlot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBootstrap()
    {
        // Domain Reloadを無効にしたEditorでも二重登録しない。
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<DamageNumberPresenter>() != null) return;
        new GameObject("Damage Number Presenter").AddComponent<DamageNumberPresenter>();
    }

    private void Awake()
    {
        if (!settings) settings = Resources.Load<DamageNumberSettings>("DamageNumberSettings");
        if (!settings)
        {
            settings = ScriptableObject.CreateInstance<DamageNumberSettings>();
            ownsSettings = true;
        }
        var sourceFont = Resources.Load<Font>("Fonts/NotoSansJP");
        if (sourceFont) font = TMP_FontAsset.CreateFontAsset(sourceFont);
        if (!font)
        {
            Debug.LogError("DamageNumberPresenter: 日本語フォントを読み込めません。", this);
            enabled = false;
            return;
        }
        font.isMultiAtlasTexturesEnabled = true;
        var root = new GameObject("Damage Numbers", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 既存の戦闘HUDの下。OnGUIの持ち物/強化選択は従来通り上に描く。
        canvas.sortingOrder = -10;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)root.transform;
    }

    private void OnEnable() => DamageFeedback.DamageConfirmed += OnDamageConfirmed;
    private void OnDisable()
    {
        DamageFeedback.DamageConfirmed -= OnDamageConfirmed;
        foreach (var view in views) if (view) view.Hide();
    }

    private void OnDamageConfirmed(DamageFeedbackData data)
    {
        if (!canvasRect || !font || !(data.Amount > 0) || float.IsInfinity(data.Amount)
            || !Finite(data.WorldPosition.x) || !Finite(data.WorldPosition.y) || !Finite(data.WorldPosition.z)) return;
        DamageNumberView available = null;
        foreach (var view in views)
            if (!view.IsAlive) { available = view; break; }
        if (!available && views.Count < Mathf.Clamp(settings.maxVisibleNumbers, 1, 256))
        {
            var item = new GameObject("Damage Number", typeof(RectTransform));
            item.transform.SetParent(canvasRect, false);
            available = item.AddComponent<DamageNumberView>();
            available.Initialize(font);
            views.Add(available);
        }
        if (!available)
        {
            available = views[0];
        }
        // 再利用時も最も古い表示から置き換える。
        views.Remove(available);
        views.Add(available);
        available.Show(data.Amount, data.WorldPosition, data.IsCritical, nextSlot, settings);
        nextSlot = (nextSlot + 1) % Mathf.Max(1, settings.horizontalSlots);
    }

    private void LateUpdate()
    {
        Camera camera = targetCamera ? targetCamera : Camera.main;
        foreach (var view in views)
        {
            view.Advance(Time.deltaTime);
            view.RenderForCamera(camera, canvasRect);
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private void OnDestroy()
    {
        if (font)
        {
            foreach (var atlas in font.atlasTextures) if (atlas) Destroy(atlas);
            if (font.material) Destroy(font.material);
            Destroy(font);
        }
        if (ownsSettings && settings) Destroy(settings);
    }
}
