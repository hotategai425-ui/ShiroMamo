using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExplosionPreparationVisual : NetworkBehaviour
{
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField, Min(0.01f)] private float startFrequencyHz = 1f;
    [SerializeField, Min(0.01f)] private float endFrequencyHz = 4f;
    [SerializeField, Range(0f, 1f)] private float flashFraction = 0.5f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly List<MaterialSlot> slots = new List<MaterialSlot>();
    private MaterialPropertyBlock flashBlock;
    private bool slotsInitialized;
    private bool flashing;

    // The caller supplies synchronized time. Identical inputs produce the same phase.
    public void SetPreparationVisual(bool preparing, float remainingSeconds, float durationSeconds)
    {
        if (!isActiveAndEnabled || !preparing ||
            !IsPositiveFinite(remainingSeconds) || !IsPositiveFinite(durationSeconds) ||
            !IsPositiveFinite(startFrequencyHz) || !IsPositiveFinite(endFrequencyHz))
        {
            SetFlashing(false);
            return;
        }

        double duration = durationSeconds;
        double elapsed = duration - Math.Min(remainingSeconds, durationSeconds);
        double endFrequency = Math.Max(endFrequencyHz, startFrequencyHz);
        // Integrate the linearly increasing frequency to get completed cycles.
        double cycles = elapsed * (startFrequencyHz +
            0.5 * (endFrequency - startFrequencyHz) * elapsed / duration);
        SetFlashing(cycles - Math.Floor(cycles) < Mathf.Clamp01(flashFraction));
    }

    public override void OnNetworkDespawn()
    {
        SetFlashing(false);
        base.OnNetworkDespawn();
    }

    private void OnDisable() => SetFlashing(false);

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);

    private void InitializeSlots()
    {
        foreach (Renderer target in GetComponentsInChildren<Renderer>(true))
        {
            if (!(target is MeshRenderer) && !(target is SkinnedMeshRenderer))
                continue;

            Material[] materials = target.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null)
                    continue;

                if (material.HasProperty(BaseColorId))
                    slots.Add(new MaterialSlot(target, index, BaseColorId));
                else if (material.HasProperty(ColorId))
                    slots.Add(new MaterialSlot(target, index, ColorId));
            }
        }

        flashBlock = new MaterialPropertyBlock();
        slotsInitialized = true;
    }

    private void SetFlashing(bool active)
    {
        if (flashing == active)
            return;

        if (!slotsInitialized)
            InitializeSlots();

        foreach (MaterialSlot slot in slots)
        {
            if (slot.Target == null)
                continue;

            if (active)
            {
                slot.Target.GetPropertyBlock(slot.Original, slot.Index);
                if (slot.Original.isEmpty)
                    slot.Target.GetPropertyBlock(flashBlock);
                else
                    slot.Target.GetPropertyBlock(flashBlock, slot.Index);

                flashBlock.SetColor(slot.ColorProperty, flashColor);
                slot.Target.SetPropertyBlock(flashBlock, slot.Index);
            }
            else
            {
                slot.Target.SetPropertyBlock(slot.Original.isEmpty ? null : slot.Original, slot.Index);
            }
        }

        flashing = active;
    }

    private sealed class MaterialSlot
    {
        public readonly Renderer Target;
        public readonly int Index;
        public readonly int ColorProperty;
        public readonly MaterialPropertyBlock Original = new MaterialPropertyBlock();

        public MaterialSlot(Renderer target, int index, int colorProperty)
        {
            Target = target;
            Index = index;
            ColorProperty = colorProperty;
        }
    }
}
