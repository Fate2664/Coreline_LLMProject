using UnityEngine;

namespace Coreline
{
    public class MoveCamera : MonoBehaviour
    {
        [SerializeField] private Transform cameraPosition;

        private void LateUpdate()
        {
            if (cameraPosition == null)
            {
                return;
            }

            transform.position = cameraPosition.position;
        }
    }
}
