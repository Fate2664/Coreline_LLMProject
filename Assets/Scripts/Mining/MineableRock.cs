using UnityEngine;
using UnityEngine.Events;

public class MineableRock : MonoBehaviour, IMineable
{
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private bool destroyOnDepleted = true;
    [SerializeField] private ParticleSystem rockBreakParticles;
    
    [Header("Events")]
    [SerializeField] private UnityEvent onHit;
    [SerializeField] private UnityEvent onDepleted;

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
        onHit.Invoke();

        if (currentHealth > 0f)
            return;

        currentHealth = 0f;
        onDepleted.Invoke();
        
        ParticleSystem rockParticles = Instantiate(rockBreakParticles, transform.position, transform.rotation);
        rockParticles.Play();
        Destroy(rockParticles, rockBreakParticles.main.duration + rockBreakParticles.main.startLifetime.constantMax);
        if (destroyOnDepleted)
            Destroy(gameObject);
    }
}
