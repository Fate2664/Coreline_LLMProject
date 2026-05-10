using System;
using Unity.VisualScripting;
using UnityEngine;

namespace Coreline
{
    public abstract class EdgeSocket : MonoBehaviour
    {
        [SerializeField] protected Vector3 snapLocalPosition;
        [SerializeField] protected Vector3 snapLocalRotation;
        [SerializeField] protected float triggerRadius = 0.3f;
        [SerializeField] protected string socketLayerName = "Socket";

        private BuildPart occupant;
        
        public BuildPart Occupant => occupant;
        public bool IsOccupied => occupant != null;

        public virtual Pose GetSnapPose() => new(GetSnapWorldPosition(), GetSnapWorldRotation());
        protected Vector3 GetSnapWorldPosition() => transform.position + transform.TransformDirection(snapLocalPosition); 
        protected Quaternion GetSnapWorldRotation() => transform.rotation * Quaternion.Euler(snapLocalRotation);

        public void SetOccupant(BuildPart occupant) => this.occupant = occupant;
        public virtual bool CanAcceptPart(BuildPieceType pieceType) => false;

        private void Awake() => ApplyLayerAndCollider();
        private void Reset() => ApplyLayerAndCollider();
        private void OnValidate() => ApplyLayerAndCollider();

        protected void ApplyLayerAndCollider()
        {
            var idx = LayerMask.NameToLayer(socketLayerName);
            if (idx >= 0) gameObject.layer = idx;
            
            var sc = gameObject.GetOrAddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = triggerRadius;
            sc.center = Vector3.zero;
        }


    }
}
