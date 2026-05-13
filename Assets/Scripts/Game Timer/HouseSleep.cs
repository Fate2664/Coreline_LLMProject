using System;
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
        }

        private void FixedUpdate()
        {
            //Showing indicator
            if (indicatorManager == null) return;

            if (playerInteractionDetector.CurrentTarget != null &&
                playerInteractionDetector.CurrentIteractableObject != null &&
                playerInteractionDetector.CurrentIteractableObject.CompareTag("House"))
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
            TimeManager.Instance.Sleep();
        }
    }
}