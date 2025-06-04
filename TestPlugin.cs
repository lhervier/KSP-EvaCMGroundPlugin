using System.Collections.Generic;
using Expansions.Missions.Editor;
using UnityEngine;

namespace com.github.lhervier.ksp {
	
	[KSPAddon(KSPAddon.Startup.PSystemSpawn, false)]
    public class TestPlugin : MonoBehaviour {
        
        private Part previousPart;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        
        private static void LogInternal(string level, string message) {
            Debug.Log($"[TestPlugin][{level}] {message}");
        }

        private static void LogInfo(string message) {
            LogInternal("INFO", message);
        }

        private static void LogDebug(string message) {
            LogInternal("DEBUG", message);
        }

        private static void LogError(string message) {
            LogInternal("ERROR", message);
        }

        protected void Awake() 
        {
            LogInfo("Awaked");
            DontDestroyOnLoad(this);
        }

        public void Start() {
            GameEvents.OnEVAConstructionMode.Add(OnEVAConstructionMode);
            LogInfo("Plugin started");
        }

        public void OnDestroy() {
            GameEvents.onEditorPartEvent.Remove(OnEditorPartEvent);
            GameEvents.OnEVAConstructionMode.Remove(OnEVAConstructionMode);
            LogInfo("Plugin stopped");
        }

        public void OnEVAConstructionMode(bool mode) {
            LogDebug($"OnEVAConstructionMode: {mode}");
            if( mode ) {
                LogDebug("Starting Fix");
                GameEvents.onEditorPartEvent.Add(OnEditorPartEvent);
            }
            else {
                LogDebug("Stopping Fix");
                GameEvents.onEditorPartEvent.Remove(OnEditorPartEvent);
            }
        }

        // ==============================================================================================

        private int GetLayerMask(Collider collider) {
            return ~((1 << collider.gameObject.layer) |     // The collider layer
                             (1 << 11) |                                // UIDialog
                             (1 << 1) |                                 // TransparentFX
                             (1 << 5) |                                 // UI
                             (1 << 12) |                                // UIVectors
                             (1 << 13) |                                // UI_Mask
                             (1 << 14) |                                // Screens
                             (1 << 25) |                                // UIAdditional
                             (1 << 10));                                // Scaled Scenery
        }

        private void LogColliders(Collider[] colliders) {
            foreach (Collider collider in colliders) {
                LogDebug($"=> Collision with : {collider.name} on layer {collider.gameObject.layer} ({LayerMask.LayerToName(collider.gameObject.layer)})");
            }
        }

        bool IsGrounded(Collider collider) {
            Collider[] colliders = null;
            if (collider is BoxCollider boxCollider) {
                colliders = GetBoxColliders(boxCollider);
            }
            else if (collider is CapsuleCollider capsuleCollider) {
                colliders = GetCapsuleColliders(capsuleCollider);
            }
            else if (collider is SphereCollider sphereCollider) {
                colliders = GetSphereColliders(sphereCollider);
            }
            else if (collider is MeshCollider meshCollider) {
                colliders = GetMeshColliders(meshCollider);
            }
            else {
                LogError($"Unsupported collider type : {collider.GetType().Name} (position: {collider.transform.position})");
                colliders = new Collider[0];
            }
            LogColliders(colliders);
            return colliders.Length > 0;
        }

        Collider[] GetBoxColliders(BoxCollider boxCollider) {
            return Physics.OverlapBox(
                boxCollider.transform.position - Vector3.up * 0.01f,  // Décalage de 0.01 unités vers le haut
                boxCollider.size * 0.5f,
                boxCollider.transform.rotation,
                GetLayerMask(boxCollider)
            );
        }

        Collider[] GetCapsuleColliders(CapsuleCollider capsuleCollider) {
            // On a le centre de la capsule, son rayon, sa hauteur et sa rotation.
            Vector3 center = capsuleCollider.transform.position - Vector3.up * 0.01f;  // Décalage de 0.01 unités vers le haut
            float radius = capsuleCollider.radius;
            float height = capsuleCollider.height - (2 * radius);
            Quaternion rotation = capsuleCollider.transform.rotation;
            int direction = capsuleCollider.direction;
            
            // On calcule la direction de la capsule.
            Vector3 directionVector;
            switch (direction)
            {
                case 0: // X-axis
                    directionVector = rotation * Vector3.right;  // (1, 0, 0)
                    break;
                case 1: // Y-axis
                    directionVector = rotation * Vector3.up;     // (0, 1, 0)
                    break;
                case 2: // Z-axis
                    directionVector = rotation * Vector3.forward; // (0, 0, 1)
                    break;
                default:
                    directionVector = rotation * Vector3.up;
                    break;
            }
            
            // On calcul les deux points qui définissent la capsule.
            Vector3 point1 = center - directionVector * (height * 0.5f);
            Vector3 point2 = center + directionVector * (height * 0.5f);
            
            // On retourne les colliders qui intersectent la capsule.
            return Physics.OverlapCapsule(
                point1,
                point2,
                radius,
                GetLayerMask(capsuleCollider)
            );
        }

        Collider[] GetSphereColliders(SphereCollider sphereCollider) {
            return Physics.OverlapSphere(
                sphereCollider.transform.position - Vector3.up * 0.01f,  // Décalage de 0.01 unités vers le haut
                sphereCollider.radius,
                GetLayerMask(sphereCollider)
            );
        }

        Collider[] GetMeshColliders(MeshCollider meshCollider) {
            // On récupère tous les colliders dans la zone
            Collider[] potentialColliders = Physics.OverlapBox(
                meshCollider.bounds.center,
                meshCollider.bounds.extents,
                meshCollider.transform.rotation,
                GetLayerMask(meshCollider)
            );

            // On filtre les colliders qui ont une pénétration réelle
            List<Collider> penetratingColliders = new List<Collider>();
            foreach (Collider otherCollider in potentialColliders) {
                if (Physics.ComputePenetration(
                    meshCollider, 
                    meshCollider.transform.position, 
                    meshCollider.transform.rotation,
                    otherCollider, 
                    otherCollider.transform.position, 
                    otherCollider.transform.rotation,
                    out Vector3 direction, 
                    out float distance
                )) {
                    penetratingColliders.Add(otherCollider);
                }
            }

            return penetratingColliders.ToArray();
        }

        private void OnEditorPartEvent(ConstructionEventType eventType, Part part) {
            if( 
                part == this.previousPart && 
                part.transform.position == this.previousPosition && 
                part.transform.rotation == this.previousRotation 
            ) {
                return;
            }

            LogDebug($"--------------------------------");
            LogDebug($"onEditorPartEvent: {eventType} / {part.name} / {part.persistentId} / {part.transform.position} / {part.transform.rotation}");
            if( part != this.previousPart ) {
                // Seems to stabilize the parts when changing.
                if( previousPart != null ) {
                    this.previousPart.transform.position = this.previousPosition;
                    this.previousPart.transform.rotation = this.previousRotation;
                }
                
                this.previousPart = part;
                this.previousPosition = part.transform.position;
                this.previousRotation = part.transform.rotation;
                LogDebug($"=> New part. Using current position as previous position");
            }
            else {
                LogDebug($"=> Using stored previous position: {this.previousPosition} / {this.previousRotation}");
            }

            Collider[] colliders = part.GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders) {
                if (IsGrounded(collider)) {
                    LogDebug($"=> Collider {collider.GetType().Name} is Grounded ! Restoring previous position and rotation to {this.previousPosition} / {this.previousRotation}");
                    part.transform.position = this.previousPosition;
                    part.transform.rotation = this.previousRotation;
                    break;
                }
                else {
                    LogDebug($"=> Collider {collider.GetType().Name} is not grounded...");
                }
            }
            
            this.previousPosition = part.transform.position;
            this.previousRotation = part.transform.rotation;
        }
    }
}
