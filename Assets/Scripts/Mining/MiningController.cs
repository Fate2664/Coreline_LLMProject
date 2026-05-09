using System.Collections;
using UnityEngine;

public class MiningController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private PickaxeSwingView pickaxeSwingView;

    [Header("Mining")] [SerializeField] private LayerMask mineableLayers = ~0;
    [SerializeField] private float miningRange = 3f;
    [SerializeField] private float miningInterval = 0.35f;
    [SerializeField] private float hitDelay = 0.12f;
    [SerializeField] private float damagePerHit = 1f;

    private Transform miningOrigin;
    private bool isPrimaryAttackHeld;
    private Coroutine miningRoutine;

    private void Start()
    {
        miningOrigin = Camera.main.transform;
        gameInput.PrimaryAttack += OnPrimaryAttack;
    }

    private void OnDestroy()
    {
        gameInput.PrimaryAttack -= OnPrimaryAttack;
    }

    private void OnPrimaryAttack(bool isPressed)
    {
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

        pickaxeSwingView?.ReturnToRest();
    }

    private IEnumerator MiningLoop()
    {
        while (isPrimaryAttackHeld)
        {
            pickaxeSwingView?.PlaySwing(miningInterval);

            yield return new WaitForSeconds(hitDelay);
            TryMineTarget();

            float recoveryTime = Mathf.Max(0f, miningInterval - hitDelay);
            yield return new WaitForSeconds(recoveryTime);
        }

        miningRoutine = null;
    }

    private void TryMineTarget()
    {
        if (!Physics.Raycast(miningOrigin.position, miningOrigin.forward, out RaycastHit hit, miningRange,
                mineableLayers, QueryTriggerInteraction.Ignore))
            return;

        IMineable mineable = hit.collider.GetComponentInParent<IMineable>();
        if (mineable == null || !mineable.CanBeMined)
            return;

        mineable.Mine(new MiningHit(miningOrigin, hit.point, hit.normal, damagePerHit));
    }
}