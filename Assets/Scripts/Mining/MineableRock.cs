using UnityEngine;
using UnityEngine.Events;
using UnityEditor;

public class MineableRock : MonoBehaviour, IMineable
{
    [SerializeField] private float maxHealth = 3f;

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

        currentHealth -= hit.Damage;

        if (currentHealth > 0f)
            return false;

        currentHealth = 0f;
        OnDepleted.Invoke();

        Destroy(gameObject);
        return true;
    }
}
