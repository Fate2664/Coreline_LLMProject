using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class MineableRock : MonoBehaviour, IMineable
{
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    [SerializeField] private float maxHealth = 3f;

    [Header("Mining Restrictions")]
    [SerializeField] private bool miningRobotOnly;
    [SerializeField] private Transform invalidMineVisual;
    [SerializeField, Min(0.1f)] private float invalidMineFlashDuration = 2f;
    [SerializeField, Min(0.1f)] private float invalidMineFlashesPerSecond = 3f;
    
    [Header("VFX")] [SerializeField] private ParticleSystem sparkParticles;
    [SerializeField] private ParticleSystem rockParticles;
    [SerializeField] private ParticleSystem rockBreakParticles;
    
    public event UnityAction OnDepleted = delegate { };

    private float currentHealth;
    private Renderer[] invalidMineRenderers;
    private Color[] invalidMineColors;
    private MaterialPropertyBlock invalidMinePropertyBlock;
    private Coroutine invalidMineFlashRoutine;
    private bool invalidMineVisualInitiallyActive;

    public bool CanBeMined => currentHealth > 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
        InitializeInvalidMineVisual();
    }

    private void OnDisable()
    {
        if (invalidMineFlashRoutine != null)
        {
            StopCoroutine(invalidMineFlashRoutine);
            invalidMineFlashRoutine = null;
        }

        SetInvalidMineVisualAlpha(0f);
        RestoreInvalidMineVisualActiveState();
    }

    public bool Mine(MiningHit hit)
    {
        if (!CanBeMined)
            return false;

        if (miningRobotOnly && hit.SourceType != MiningSourceType.MiningRobot)
        {
            if (hit.SourceType == MiningSourceType.Player)
                PlayInvalidMineFlash();

            return false;
        }

        Vector3 hitNormal = hit.Normal;
        if (hitNormal.sqrMagnitude < 0.0001f)
            hitNormal = -hit.Source.forward;
        else
            hitNormal.Normalize();
        
        Vector3 spawnPosition = hit.Source.position + hitNormal * 0.2f;
        Quaternion rotation = Quaternion.LookRotation(hitNormal);
        ParticleSystem sparkVFX = Instantiate(sparkParticles, spawnPosition, rotation);
        ParticleSystem rockVFX = Instantiate(rockParticles, spawnPosition, rotation);
        sparkVFX.Play();
        rockVFX.Play();
        Destroy(rockVFX.gameObject, rockVFX.main.duration + rockVFX.main.startLifetime.constantMax);
        Destroy(sparkVFX.gameObject, sparkVFX.main.duration + sparkVFX.main.startLifetime.constantMax);

        currentHealth -= hit.Damage;

        if (currentHealth > 0f)
            return false;

        currentHealth = 0f;
        OnDepleted.Invoke();
        ParticleSystem rockBreakVFX = Instantiate(rockBreakParticles, spawnPosition, rotation);
        rockBreakVFX.Play();
        Destroy(rockBreakVFX.gameObject, rockBreakVFX.main.duration + rockBreakVFX.main.startLifetime.constantMax);
        Destroy(gameObject);
        return true;
    }

    private void InitializeInvalidMineVisual()
    {
        if (!miningRobotOnly)
            return;

        if (invalidMineVisual == null)
            invalidMineVisual = FindChildByName(transform, "RedVisual");

        if (invalidMineVisual == null)
        {
            Debug.LogWarning(
                $"{name} is restricted to mining robots but has no RedVisual child assigned.",
                this);
            return;
        }

        invalidMineVisualInitiallyActive = invalidMineVisual.gameObject.activeSelf;
        invalidMineRenderers = invalidMineVisual.GetComponentsInChildren<Renderer>(true);
        invalidMineColors = new Color[invalidMineRenderers.Length];
        invalidMinePropertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < invalidMineRenderers.Length; i++)
        {
            Material material = invalidMineRenderers[i].sharedMaterial;
            Color color = Color.red;

            if (material != null && material.HasProperty(BaseColorProperty))
                color = material.GetColor(BaseColorProperty);
            else if (material != null && material.HasProperty(ColorProperty))
                color = material.GetColor(ColorProperty);

            invalidMineColors[i] = color;
        }

        SetInvalidMineVisualAlpha(0f);
    }

    private void PlayInvalidMineFlash()
    {
        if (invalidMineVisual == null || invalidMineRenderers == null || invalidMineRenderers.Length == 0)
            return;

        if (invalidMineFlashRoutine == null)
            invalidMineFlashRoutine = StartCoroutine(FlashInvalidMineVisual());
    }

    private IEnumerator FlashInvalidMineVisual()
    {
        invalidMineVisual.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < invalidMineFlashDuration)
        {
            float alpha = Mathf.PingPong(elapsed * invalidMineFlashesPerSecond * 2f, 1f);
            SetInvalidMineVisualAlpha(alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetInvalidMineVisualAlpha(0f);
        RestoreInvalidMineVisualActiveState();
        invalidMineFlashRoutine = null;
    }

    private void SetInvalidMineVisualAlpha(float alpha)
    {
        if (invalidMineRenderers == null || invalidMinePropertyBlock == null)
            return;

        alpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < invalidMineRenderers.Length; i++)
        {
            Renderer targetRenderer = invalidMineRenderers[i];
            if (targetRenderer == null)
                continue;

            Color color = invalidMineColors[i];
            color.a = alpha;

            invalidMinePropertyBlock.Clear();
            targetRenderer.GetPropertyBlock(invalidMinePropertyBlock);
            invalidMinePropertyBlock.SetColor(BaseColorProperty, color);
            invalidMinePropertyBlock.SetColor(ColorProperty, color);
            targetRenderer.SetPropertyBlock(invalidMinePropertyBlock);
        }
    }

    private void RestoreInvalidMineVisualActiveState()
    {
        if (invalidMineVisual != null &&
            invalidMineVisual.gameObject.activeSelf != invalidMineVisualInitiallyActive)
        {
            invalidMineVisual.gameObject.SetActive(invalidMineVisualInitiallyActive);
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (Transform child in root)
        {
            if (child.name == childName)
                return child;

            Transform match = FindChildByName(child, childName);
            if (match != null)
                return match;
        }

        return null;
    }
}
