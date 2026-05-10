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
            if (!other.CompareTag("Player"))
                return;
            //Add ore to inventory
            UIManager uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null) return;
            uiManager.AddItemToInventory(oreData.inventoryItemData, oreData.pickupAmount);
            
            Destroy(gameObject);
        }
    }
}
