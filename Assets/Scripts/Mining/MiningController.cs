using System;
using System.Collections;
using Coreline;
using UnityEngine;

public class MiningController : MonoBehaviour
{
    private const int MaxMiningHits = 8;

    [Header("References")] 
    [SerializeField] private PickaxeAnimation pickaxeAnimation;
    [SerializeField] private Transform pickaxeHitbox;

    [Header("Mining")] [SerializeField] private LayerMask mineableLayers = ~0;
    [SerializeField] private Vector3 pickaxeHitboxHalfExtents = new Vector3(0.15f, 0.15f, 0.15f);
    [SerializeField] private float miningInterval = 0.2f;
    [SerializeField] private float hitDelay = 0.05f;
    [SerializeField] private float damagePerHit = 1f;

    private Player player;
    private bool isPrimaryAttackHeld;
    private Coroutine miningRoutine;
    private readonly Collider[] miningHits = new Collider[MaxMiningHits];

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    private void Update()
    {
        OnPrimaryAttack(player.CharacterInput.PrimaryAttack);
    }


    public void OnPrimaryAttack(bool isPressed)
    {
        if (isPrimaryAttackHeld == isPressed)
            return;
        
        isPrimaryAttackHeld = isPressed;

        if (isPrimaryAttackHeld)
        {
            if (miningRoutine == null)
                miningRoutine = StartCoroutine(MiningLoop());
            return;
        }

        if (miningRoutine != null)
        {
            StopCoroutine(miningRoutine);
            miningRoutine = null;
        }

        pickaxeAnimation?.ReturnToRest();
    }

    private IEnumerator MiningLoop()
    {
        while (isPrimaryAttackHeld)
        {
            pickaxeAnimation?.PlaySwing(miningInterval);

            yield return new WaitForSeconds(hitDelay);
            TryMineTarget();

            float recoveryTime = Mathf.Max(0f, miningInterval - hitDelay);
            yield return new WaitForSeconds(recoveryTime);
        }

        miningRoutine = null;
    }

    private void TryMineTarget()
    {
        int hitCount = Physics.OverlapBoxNonAlloc(
            pickaxeHitbox.position,
            pickaxeHitboxHalfExtents,
            miningHits,
            pickaxeHitbox.rotation,
            mineableLayers,
            QueryTriggerInteraction.Ignore);

        if (hitCount == 0)
            return;

        IMineable mineable = null;
        Vector3 hitPoint = Vector3.zero;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = miningHits[i];
            if (collider == null)
                continue;

            IMineable candidateMineable = collider.GetComponentInParent<IMineable>();
            if (candidateMineable == null)
                continue;

            Vector3 candidateHitPoint = collider.ClosestPoint(pickaxeHitbox.position);
            float distanceSqr = (candidateHitPoint - pickaxeHitbox.position).sqrMagnitude;

            if (distanceSqr >= closestDistanceSqr)
                continue;

            closestDistanceSqr = distanceSqr;
            mineable = candidateMineable;
            hitPoint = candidateHitPoint;
        }

        if (mineable == null)
            return;

        Vector3 hitNormal = pickaxeHitbox.position - hitPoint;
        if (hitNormal.sqrMagnitude < 0.0001f)
            hitNormal = -pickaxeHitbox.forward;
        else
            hitNormal.Normalize();

        mineable.Mine(new MiningHit(pickaxeHitbox, hitPoint, hitNormal, damagePerHit));
    }
}
