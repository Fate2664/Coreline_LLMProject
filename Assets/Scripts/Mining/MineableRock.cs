using UnityEngine;
using UnityEngine.Events;

public class MineableRock : MonoBehaviour, IMineable
{
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private bool destroyOnDepleted = true;

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

        if (destroyOnDepleted)
            Destroy(gameObject);
    }
}
