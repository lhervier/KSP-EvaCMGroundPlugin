using System.Collections.Generic;
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
            else {
                LogError($"Unsupported collider type : {collider.GetType().Name} (position: {collider.transform.position})");
                colliders = new Collider[0];
            }
            LogColliders(colliders);
            return colliders.Length > 0;
        }

        Collider[] GetBoxColliders(BoxCollider boxCollider) {
            return Physics.OverlapBox(
                boxCollider.bounds.center,
                boxCollider.bounds.extents,
                boxCollider.transform.rotation,
                GetLayerMask(boxCollider)
            );
        }

        Collider[] GetCapsuleColliders(CapsuleCollider capsuleCollider) {
            return Physics.OverlapBox(
                capsuleCollider.bounds.center,
                capsuleCollider.bounds.extents,
                capsuleCollider.transform.rotation,
                GetLayerMask(capsuleCollider)
            );
        }

        Collider[] GetSphereColliders(SphereCollider sphereCollider) {
            return Physics.OverlapBox(
                sphereCollider.bounds.center,
                sphereCollider.bounds.extents,
                sphereCollider.transform.rotation,
                GetLayerMask(sphereCollider)
            );
        }

        private void OnEditorPartEvent(ConstructionEventType eventType, Part part) {
            if( 
                part == this.previousPart && 
                part.transform.position == this.previousPosition && 
                part.transform.rotation == this.previousRotation 
            ) {
                LogDebug($"=> No change to part, position and rotation. Skipping...");
                return;
            }

            LogDebug($"--------------------------------");
            LogDebug($"onEditorPartEvent: {eventType} / {part.name} / {part.persistentId} / {part.transform.position} / {part.transform.rotation}");
            if( part != this.previousPart ) {
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
                    LogDebug($"=> Grounded ! Restoring previous position and rotation to {this.previousPosition} / {this.previousRotation}");
                    part.transform.position = this.previousPosition;
                    part.transform.rotation = this.previousRotation;
                    break;
                }
                else {
                    LogDebug($"=> Not grounded...");
                }
            }
            
            this.previousPosition = part.transform.position;
            this.previousRotation = part.transform.rotation;
        }
    }
}
