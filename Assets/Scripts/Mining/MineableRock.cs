using UnityEngine;
using UnityEngine.Events;
using UnityEditor;

public class MineableRock : MonoBehaviour, IMineable
{
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private bool destroyOnDepleted = true;
    [SerializeField] private ParticleSystem rockBreakParticles;
    
    public event UnityAction OnDepleted = delegate { };

    private float currentHealth;

    public bool CanBeMined => currentHealth > 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void Mine(MiningHit hit)
    {
        if (!CanBeMined)
            return;

        currentHealth -= hit.Damage;
        
        if (currentHealth > 0f)
            return;

        currentHealth = 0f;
        OnDepleted.Invoke();
        
        ParticleSystem rockParticles = Instantiate(rockBreakParticles, transform.position, transform.rotation);
        rockParticles.Play();
        Destroy(rockParticles, rockBreakParticles.main.duration + rockBreakParticles.main.startLifetime.constantMax);
        if (destroyOnDepleted)
        {
            Destroy(gameObject);
        }
    }
}
