using System;
using System.Collections.Generic;
using Nova;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Coreline
{
    public class BuildPlacer : MonoBehaviour
    {
        #region Class Variables

        [Header("Prefabs & Tools")]
        [SerializeField] public BuildPieceType placementPieceType = BuildPieceType.Floor;
        [SerializeField] private GameObject floorTilePrefab;
        [SerializeField] private GameObject wallTilePrefab;
        [SerializeField] private GameObject doorTilePrefab;

        [Space(10)] 
        [Header("GroundPlacement")] 
        [SerializeField] private LayerMask placementMask;
        [SerializeField] private LayerMask floorOverlapMask;
        [SerializeField] private float gridSize = 2.5f;
        [SerializeField] private bool snapFloorToGrid = true;
        
        [Space(10)]
        [Header("Socket Raycast")]
        [SerializeField] private bool useSocketSnapping = true;
        [SerializeField] private LayerMask socketRaycastMask;
        [SerializeField] private float socketRaycastDistance = 500f;
        [SerializeField] private float groundRaycastDistance = 500f;
        [SerializeField] private int socketHitBufferSize = 16;
        
        [Space(10)]
        [Header("Socket Occupancy")]
        [SerializeField] private bool skipSocketOccupied = true;

        [Space(10)] [Header("Materials")] 
        [SerializeField] private Material previewMaterialValid;
        [SerializeField] private Material previewMaterialInvalid;
        [SerializeField] private float previewLiftY = 0.02f;

        private GameObject previewInstance;
        private bool destroyMode;
        private BuildPart hoveredBuild;
        private readonly List<Renderer> hoveredRenderers = new ();
        private readonly List<Material[]> hoveredMaterials = new ();
        private RaycastHit[] hitsBuffer;
        private readonly List<EdgeSocket> availableSockets = new(8);
        private Camera mainCamera;
        private GameObject placementPrefabOverride;
        private Action<GameObject> placementCompleted;
        private GameObject previewPrefabSource;
        private bool waitForPlacementInputRelease;

        #endregion

        private bool IsInventoryPrefabPlacement => placementPrefabOverride != null;

        private GameObject ActivePrefab => placementPrefabOverride != null ? placementPrefabOverride : placementPieceType switch
        {
            BuildPieceType.Floor => floorTilePrefab,
            BuildPieceType.Wall => wallTilePrefab,
            BuildPieceType.Door => doorTilePrefab,
            _ => null
        };

        public void BeginPrefabPlacement(GameObject prefab, Action<GameObject> onPlaced = null)
        {
            SetDestroyMode(false);
            placementPrefabOverride = prefab;
            placementCompleted = onPlaced;
            previewPrefabSource = null;
            waitForPlacementInputRelease = Mouse.current != null && Mouse.current.leftButton.isPressed;
            ClearPreview();
            enabled = true;
        }

        public void CancelPrefabPlacement()
        {
            if (!IsInventoryPrefabPlacement)
            {
                return;
            }

            ClearPrefabPlacementState();
            ClearPreview();
            enabled = false;
        }

        public void SetDestroyMode(bool value)
        {
            if (destroyMode == value) return;
            
            destroyMode = value;
            ClearPreview();
            ClearHoveredBuild();
        }

        private void Awake()
        {
            mainCamera = Camera.main;
            hitsBuffer = new RaycastHit[Mathf.Max(1, socketHitBufferSize)];
            if (socketRaycastMask.value == 0) socketRaycastMask = LayerMask.GetMask("Socket");
            if (placementMask.value == 0)
            {
                var sl = LayerMask.NameToLayer("Socket");
                placementMask = sl >= 0 ? ~(1 << sl) : ~0;
            }

            if (floorOverlapMask.value == 0)
            {
                var sl = LayerMask.NameToLayer("Socket");
                floorOverlapMask = sl >= 0 ? ~(1 << sl) : ~0;
            }
        }

        private void Update()
        {
            if (mainCamera == null || Mouse.current == null) return;
            var mouse = Mouse.current;

            if (IsInventoryPrefabPlacement &&
                ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                 mouse.rightButton.wasPressedThisFrame))
            {
                CancelPrefabPlacement();
                return;
            }

            if (destroyMode)
            {
                HandleDestroyMode(mouse);
                return;
            }

            GameObject activePrefab = ActivePrefab;
            if (activePrefab != previewPrefabSource)
            {
                ClearPreview();
                previewPrefabSource = activePrefab;
            }

            if (!TryGetActivePreviewPose(mouse.position.ReadValue(), out var pose, out var placementValid, out var socketSnap))
            {
                if (previewInstance != null) previewInstance.SetActive(false);
                return;
            }
            
            UpdatePreview(pose, placementValid);

            if (waitForPlacementInputRelease)
            {
                waitForPlacementInputRelease = mouse.leftButton.isPressed;
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && placementValid) Place(activePrefab, pose, socketSnap);
        }

        private void UpdatePreview(Pose pose, bool valid)
        {
            if (previewInstance == null)
            {
                var p = ActivePrefab;
                if (p == null) return;
                previewInstance = Instantiate(p);
                previewInstance.hideFlags = HideFlags.DontSave;
                PreparePreviewInstance(previewInstance);
            }
            
            var pos = pose.position;
            pos.y += previewLiftY;
            previewInstance.transform.SetPositionAndRotation(pos, pose.rotation);
            
            var mat = valid ?  previewMaterialValid : previewMaterialInvalid;
            if (mat == null) return;
            foreach (var r in previewInstance.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        }

        private static void PreparePreviewInstance(GameObject instance)
        {
            foreach (var c in instance.GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }

            foreach (var behaviour in instance.GetComponentsInChildren<Behaviour>())
            {
                behaviour.enabled = false;
            }
        }
        
        private void Place(GameObject prefab, Pose pose, EdgeSocket socketSnap)
        {
            if (prefab == null || (!IsInventoryPrefabPlacement && placementPieceType == BuildPieceType.Floor && IsFloorOccupied(pose.position))) return;
            var go = Instantiate(prefab, pose.position, pose.rotation);
            if (socketSnap != null)
            {
                var part = go.GetComponent<BuildPart>();
                if (part != null) socketSnap.SetOccupant(part);
            }

            if (!IsInventoryPrefabPlacement)
            {
                return;
            }

            Action<GameObject> onPlaced = placementCompleted;
            ClearPrefabPlacementState();
            ClearPreview();
            enabled = false;
            onPlaced?.Invoke(go);
        }

        private bool IsFloorOccupied(Vector3 position)
        {
            var halExtent = new Vector3(gridSize * .45f, 0.25f, gridSize * .45f);
            var center = position + Vector3.up * .1f;
            
            var overlaps = Physics.OverlapBox(center, halExtent, Quaternion.identity, floorOverlapMask, QueryTriggerInteraction.Collide);

            foreach (var overlap in overlaps)
            {
                if (overlap.GetComponent<EdgeSocket>() != null) continue;
                var part =  overlap.GetComponentInParent<BuildPart>();
                if (part != null && part.Type == BuildPieceType.Floor)
                    return true;
            }

            return false;
        }

        private void SnapGrid(ref Vector3 position)
        {
            if (gridSize <= 0f) return;
            position.x = Mathf.Round(position.x / gridSize) * gridSize;
            position.z = Mathf.Round(position.z / gridSize) * gridSize;
        }
        
        private void HandleDestroyMode(Mouse mouse)
        {
            if (!TryGetHoveredBuild(mouse.position.ReadValue(), out var build))
            {
                SetHoveredBuild(null);
                return;
            }

            SetHoveredBuild(build);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                DestroyBuild(build);
            }
        }
        
        private void SetHoveredBuild(BuildPart build)
        {
            if (hoveredBuild == build) return;

            ClearHoveredBuild();
            hoveredBuild = build;

            if (hoveredBuild == null) return;
            foreach (var renderer in hoveredBuild.GetComponentsInChildren<Renderer>())
            {
                hoveredRenderers.Add(renderer);
                hoveredMaterials.Add(renderer.sharedMaterials);

                var invalidMaterials = new Material[renderer.sharedMaterials.Length];
                for (var i = 0; i < invalidMaterials.Length; i++)
                {
                    invalidMaterials[i] = previewMaterialInvalid;
                }

                renderer.sharedMaterials = invalidMaterials;
            }
        }

        private void DestroyBuild(BuildPart build)
        {
            if (build == null) return;
            
            ClearHoveredBuild();
            Destroy(build.gameObject);
        }

        #region Try Methods

        private bool TryGetPreviewPose(Vector2 screenPos, out Pose pose, out bool placementValid, out EdgeSocket socketFromSnap)
        {
            pose = default;
            placementValid = false;
            socketFromSnap = null;
            
            Ray ray = mainCamera.ScreenPointToRay(screenPos);
            
            //Socket Snapping
            if (useSocketSnapping && socketRaycastMask.value != 0 && TrySnapSocket(ray, out pose, out socketFromSnap))
            {
                placementValid = placementPieceType != BuildPieceType.Floor || !IsFloorOccupied(pose.position);
                return true;
            }
            
            //Hitting ground
            if (Physics.Raycast(ray, out var hit, groundRaycastDistance, placementMask, QueryTriggerInteraction.Ignore))
            {
                var position = hit.point;

                if (placementPieceType == BuildPieceType.Floor)
                {
                    if (snapFloorToGrid) SnapGrid(ref position);
                    placementValid = !IsFloorOccupied(position);
                }
                
                pose = new Pose(position, Quaternion.identity);
                return true;
            }
            
            //Horizontal Plane
            if (TryHorizontalPlane(ray, out var point))
            {
                if (placementPieceType == BuildPieceType.Floor)
                {
                    if (snapFloorToGrid) SnapGrid(ref point);
                    placementValid = !IsFloorOccupied(point);
                }
                
                pose = new Pose(point, Quaternion.identity);
                return true;
            }
            
            return false;
        }

        private bool TryGetActivePreviewPose(Vector2 screenPos, out Pose pose, out bool placementValid, out EdgeSocket socketFromSnap)
        {
            return IsInventoryPrefabPlacement
                ? TryGetPrefabPlacementPose(screenPos, out pose, out placementValid, out socketFromSnap)
                : TryGetPreviewPose(screenPos, out pose, out placementValid, out socketFromSnap);
        }

        private bool TryGetPrefabPlacementPose(Vector2 screenPos, out Pose pose, out bool placementValid, out EdgeSocket socketFromSnap)
        {
            pose = default;
            placementValid = false;
            socketFromSnap = null;

            Ray ray = mainCamera.ScreenPointToRay(screenPos);
            Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            if (Physics.Raycast(ray, out var hit, groundRaycastDistance, placementMask, QueryTriggerInteraction.Ignore))
            {
                pose = new Pose(hit.point, rotation);
                placementValid = true;
                return true;
            }

            if (TryHorizontalPlane(ray, out var point))
            {
                pose = new Pose(point, rotation);
                placementValid = true;
                return true;
            }

            return false;
        }

        private bool TrySnapSocket(Ray ray, out Pose bestPose, out EdgeSocket socket)
        {
            bestPose = default;
            socket = null;
            var n = Physics.RaycastNonAlloc(ray, hitsBuffer, socketRaycastDistance, socketRaycastMask,  QueryTriggerInteraction.Collide);
            if (n <= 0) return false;

            var best = float.MaxValue;
            var found = false;

            for (var i = 0; i < n; i++)
            {
                var h = hitsBuffer[i];
                if (h.collider == null) continue;
                
                availableSockets.Clear();
                h.collider.GetComponents(availableSockets);
                EdgeSocket edge = null;

                for (var j = 0; j < availableSockets.Count; j++)
                {
                    var s = availableSockets[j];
                    if (s.CanAcceptPart(placementPieceType))
                    {
                        edge = s;
                        break;
                    }
                }

                if (edge == null) continue;
                if (skipSocketOccupied && edge.IsOccupied) continue;

                var candidate = edge.GetSnapPose();

                if (h.distance < best)
                {
                    best = h.distance;
                    bestPose = candidate;
                    socket = edge;
                    found = true;
                }
            }
            return found;
        }

        private static bool TryHorizontalPlane(Ray ray, out Vector3 hit)
        {
            hit = default;
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            var t = -ray.origin.y /  ray.direction.y;
            if (t < 0f) return false;
            hit = ray.origin + ray.direction * t;
            hit.y = 0f;
            return true;
        }

        private bool TryGetHoveredBuild(Vector2 screenPos, out BuildPart build)
        {
            build = null;
            
            var ray = mainCamera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, groundRaycastDistance, floorOverlapMask, QueryTriggerInteraction.Collide)) return false;
            
            build = hit.collider.GetComponent<BuildPart>();
            return build != null;
        }

        #endregion

        #region Clear Methods

        private void ClearPreview()
        {
            if (previewInstance == null) return;

            Destroy(previewInstance);
            previewInstance = null;
        }

        private void ClearPrefabPlacementState()
        {
            placementPrefabOverride = null;
            placementCompleted = null;
            previewPrefabSource = null;
            waitForPlacementInputRelease = false;
        }

        private void ClearHoveredBuild()
        {
            for (var i = 0; i < hoveredRenderers.Count; i++)
            {
                if (hoveredRenderers[i] != null)
                {
                    hoveredRenderers[i].sharedMaterials = hoveredMaterials[i];
                }
            }

            hoveredBuild = null;
            hoveredRenderers.Clear();
            hoveredMaterials.Clear();
        }
        
        private void OnDisable()
        {
            ClearPreview();
            ClearHoveredBuild();
            ClearPrefabPlacementState();
        }

        private void OnDestroy()
        {
            ClearPreview();
            ClearHoveredBuild();
        }

        #endregion
    }
}
