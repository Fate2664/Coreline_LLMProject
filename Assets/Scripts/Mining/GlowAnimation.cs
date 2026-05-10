using System;
using DG.Tweening;
using UnityEngine;

public class GlowAnimation : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    [SerializeField] private Material glowMAT;
    [SerializeField, ColorUsage(true, true)] private Color emissionColor = Color.white;
    [SerializeField] private float glowIntensity = 3f;
    [SerializeField] private float duration = 0.75f;

    private Color baseEmissionColor;
    private Color originalEmissionColor;
    private bool originalEmissionKeywordState;
    private float startIntensity;
    private float currentIntensity;
    private Tween glowTween;

    private void Start()
    {
        if (glowMAT == null)
        {
            return;
        }

        originalEmissionKeywordState = glowMAT.IsKeywordEnabled("_EMISSION");
        originalEmissionColor = glowMAT.GetColor(EmissionColorId);
        glowMAT.EnableKeyword("_EMISSION");

        Color emission = originalEmissionColor;

        // If the material already has emission, use that as the low end.
        if (emission.maxColorComponent > 0.0001f)
        {
            startIntensity = emission.maxColorComponent;
            baseEmissionColor = emission / startIntensity;
        }
        else
        {
            startIntensity = 1f;
            baseEmissionColor = Color.white;
        }

        currentIntensity = startIntensity;
        ApplyEmission(currentIntensity);

        glowTween = DOTween.To(() => currentIntensity, x =>
            {
                currentIntensity = x;
                ApplyEmission(currentIntensity);
            }, glowIntensity, duration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private void ApplyEmission(float intensity)
    {
        glowMAT.SetColor(EmissionColorId, baseEmissionColor * intensity);
    }

    private void RestoreEmission()
    {
        if (glowMAT == null)
        {
            return;
        }

        glowMAT.SetColor(EmissionColorId, originalEmissionColor);

        if (originalEmissionKeywordState)
        {
            glowMAT.EnableKeyword("_EMISSION");
        }
        else
        {
            glowMAT.DisableKeyword("_EMISSION");
        }
    }

    private void OnDisable()
    {
        glowTween?.Kill();
        glowTween = null;
        RestoreEmission();
    }

    private void OnDestroy()
    {
        glowTween?.Kill();
        glowTween = null;
        RestoreEmission();
    }
}
