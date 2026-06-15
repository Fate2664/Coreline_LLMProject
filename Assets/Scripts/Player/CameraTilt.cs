using UnityEngine;

namespace Coreline
{
    public class CameraTilt : MonoBehaviour
    {
        [SerializeField] private float attackDamping = 0.5f;
        [SerializeField] private float decayDamping = 0.3f;
        [SerializeField] private float walkStrength = 0.075f;
        [SerializeField] private float slideStrength = 0.25f;
        [SerializeField] private float strengthResponse = 0.25f;
        
        private Vector3 dampedAcceleration;
        private Vector3 dampedAccelerationVelocity;
        private float smoothStrength;
        
        public void Initialize()
        {
            smoothStrength = walkStrength;
        }

        public void UpdateTilt(float deltaTime, bool sliding, Vector3 acceleration, Vector3 up)
        {
            var planarAcceleration = Vector3.ProjectOnPlane(acceleration, up);
            var damping = planarAcceleration.magnitude > dampedAcceleration.magnitude ? attackDamping : decayDamping;
            
            dampedAcceleration = Vector3.SmoothDamp
            (
                current: dampedAcceleration,
                target: planarAcceleration,
                currentVelocity: ref dampedAccelerationVelocity,
                smoothTime: damping,
                maxSpeed: float.PositiveInfinity,
                deltaTime: deltaTime
                );
            
            //Get rotation axis based on the acceleration vector
            var tiltAxis = Vector3.Cross(dampedAcceleration.normalized, up).normalized;
            //Reset the rotation to that of its parent
            transform.localRotation = Quaternion.identity;
            //Rotate around tilt axis
            var targetStrength = sliding ? slideStrength : walkStrength;
            
            smoothStrength = Mathf.Lerp(smoothStrength, targetStrength, 1f-Mathf.Exp(-strengthResponse * deltaTime));
            transform.rotation = Quaternion.AngleAxis(-dampedAcceleration.magnitude * smoothStrength, tiltAxis) * transform.rotation;
            

        }
    }
}
