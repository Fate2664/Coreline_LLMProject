using UnityEngine;

namespace Coreline
{
    public class Ore : Entity
    {
        [SerializeField] private OreData oreData;
        
        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        public void Throw(Vector3 direction, float force)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(direction * force, ForceMode.Impulse);
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerInventory playerInventory = other.GetComponentInParent<PlayerInventory>();
            if (playerInventory == null && !other.CompareTag("Player"))
            {
                return;
            }

            playerInventory ??=
                FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);

            if (playerInventory == null ||
                oreData == null ||
                oreData.inventoryItemData == null)
            {
                return;
            }

            if (!playerInventory.TryAddItem(
                    oreData.inventoryItemData,
                    oreData.pickupAmount))
            {
                return;
            }
            
            Destroy(gameObject);
        }
    }
}
