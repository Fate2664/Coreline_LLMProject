using UnityEngine;

namespace Coreline
{
    public class HouseSleep : MonoBehaviour, IInteractable
    {
        [SerializeField] private PlayerInteractionDetector playerInteractionDetector;

        private IndicatorManager indicatorManager;

        private void Awake()
        {
            indicatorManager = GetComponentInChildren<IndicatorManager>();
            playerInteractionDetector ??= FindFirstObjectByType<PlayerInteractionDetector>();
        }

        private void FixedUpdate()
        {
            if (indicatorManager == null || playerInteractionDetector == null)
            {
                return;
            }

            if (ReferenceEquals(playerInteractionDetector.CurrentTarget, this))
            {
                indicatorManager.ShowIndictor();
            }
            else
            {
                indicatorManager.HideIndictor();
            }
        }

        public void Interact(PlayerController interactor)
        {
            if (TimeManager.Instance == null)
            {
                Debug.LogWarning("Cannot sleep because no TimeManager exists in the scene.", this);
                return;
            }

            TimeManager.Instance.Sleep();
        }
    }
}
