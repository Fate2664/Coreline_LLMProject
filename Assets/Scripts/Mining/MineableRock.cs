using UnityEngine;
using UnityEngine.Events;
using UnityEditor;

public class MineableRock : MonoBehaviour, IMineable
{
    [SerializeField] private float maxHealth = 3f;
    
    [Header("VFX")] [SerializeField] private ParticleSystem sparkParticles;
    [SerializeField] private ParticleSystem rockParticles;
    [SerializeField] private ParticleSystem rockBreakParticles;
    
    public event UnityAction OnDepleted = delegate { };

    private float currentHealth;

    public bool CanBeMined => currentHealth > 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public bool Mine(MiningHit hit)
    {
        if (!CanBeMined)
            return false;
        Vector3 hitNormal = hit.Source.position - hit.Source.position;
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
}
