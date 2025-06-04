using System.Collections.Generic;
using UnityEngine;

namespace com.github.lhervier.ksp {
	
	[KSPAddon(KSPAddon.Startup.PSystemSpawn, false)]
    public class TestPlugin : MonoBehaviour {
        
        private Part previousPart;
        private Vector3 previousPosition;
        private Quaternion previousRotation;

        private void Log(string message) {
            Debug.Log("[TestPlugin] " + message);
        }

        protected void Awake() 
        {
            Log("Awaked");
            DontDestroyOnLoad(this);
        }

        public void Start() {
            GameEvents.OnEVAConstructionMode.Add(OnEVAConstructionMode);
            Log("Plugin started");
        }

        public void OnDestroy() {
            GameEvents.onEditorPartEvent.Remove(OnEditorPartEvent);
            GameEvents.OnEVAConstructionMode.Remove(OnEVAConstructionMode);
            Log("Plugin stopped");
        }

        public void OnEVAConstructionMode(bool mode) {
            Log($"OnEVAConstructionMode: {mode}");
            if( mode ) {
                Log("Starting Fix");
                GameEvents.onEditorPartEvent.Add(OnEditorPartEvent);
            }
            else {
                Log("Stopping Fix");
                GameEvents.onEditorPartEvent.Remove(OnEditorPartEvent);
            }
        }

        // ==============================================================================================

        bool IsGrounded(Collider collider) {
            if (collider is BoxCollider boxCollider) {
                return IsBoxGrounded(boxCollider);
            }
            else if (collider is CapsuleCollider capsuleCollider) {
                return IsCapsuleGrounded(capsuleCollider);
            }
            else if (collider is SphereCollider sphereCollider) {
                return IsSphereGrounded(sphereCollider);
            }
            else {
                Log($"ERROR : Unsupported collider: {collider.name} (position: {collider.transform.position})");
                return false;
            }
        }

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
                Log($"=> Collision with : {collider.name} on layer {collider.gameObject.layer} ({LayerMask.LayerToName(collider.gameObject.layer)})");
            }
        }

        bool IsBoxGrounded(BoxCollider boxCollider) {
            Log($"IsBoxGrounded: {boxCollider.name}");
            Collider[] colliders = Physics.OverlapBox(
                boxCollider.bounds.center,
                boxCollider.bounds.extents,
                boxCollider.transform.rotation,
                GetLayerMask(boxCollider)
            );
            LogColliders(colliders);
            return colliders.Length > 0;
        }

        bool IsCapsuleGrounded(CapsuleCollider capsuleCollider) {
            Log($"IsCapsuleGrounded: {capsuleCollider.name}");
            Vector3 center = capsuleCollider.transform.position;
            float radius = capsuleCollider.radius;
            float height = capsuleCollider.height;
            Vector3 direction = capsuleCollider.transform.up;
            
            Collider[] colliders = Physics.OverlapCapsule(
                center - direction * (height * 0.5f),
                center + direction * (height * 0.5f),
                radius,
                GetLayerMask(capsuleCollider)
            );
            LogColliders(colliders);
            return colliders.Length > 0;
        }

        bool IsSphereGrounded(SphereCollider sphereCollider) {
            Log($"IsSphereGrounded: {sphereCollider.name}");
            
            Collider[] colliders = Physics.OverlapSphere(
                sphereCollider.transform.position,
                sphereCollider.radius,
                GetLayerMask(sphereCollider)
            );
            LogColliders(colliders);
            return colliders.Length > 0;
        }

        private void OnEditorPartEvent(ConstructionEventType eventType, Part part) {
            if( 
                part == this.previousPart && 
                part.transform.position == this.previousPosition && 
                part.transform.rotation == this.previousRotation 
            ) {
                Log($"=> No change to part, position and rotation. Skipping...");
                return;
            }

            Log($"--------------------------------");
            Log($"onEditorPartEvent: {eventType} / {part.name} / {part.persistentId} / {part.transform.position} / {part.transform.rotation}");
            if( part != this.previousPart ) {
                this.previousPart = part;
                this.previousPosition = part.transform.position;
                this.previousRotation = part.transform.rotation;
                Log($"=> New part. Using current position as previous position");
            }
            else {
                Log($"=> Using stored previous position: {this.previousPosition} / {this.previousRotation}");
            }

            Collider[] colliders = part.GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders) {
                if (IsGrounded(collider)) {
                    Log($"=> Grounded ! Restoring previous position and rotation to {this.previousPosition} / {this.previousRotation}");
                    part.transform.position = this.previousPosition;
                    part.transform.rotation = this.previousRotation;
                    break;
                }
                else {
                    Log($"=> Not grounded...");
                }
            }
            
            this.previousPosition = part.transform.position;
            this.previousRotation = part.transform.rotation;
        }
    }
}
