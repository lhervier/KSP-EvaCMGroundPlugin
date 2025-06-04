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
            GameEvents.onEditorPartEvent.Add(OnEditorPartEvent);
            
            GameEvents.OnEVAConstructionMode.Add((bool mode) => {
                Log($"OnEVAConstructionMode: {mode}");
            });
            Log("Plugin started");
        }

        public void OnDestroy() {
            Log("Plugin stopped");
        }

        // ==============================================================================================

        bool IsGrounded(Collider collider) {
            if (collider is BoxCollider boxCollider) {
                return IsGrounded(boxCollider);
            }
            else if (collider is CapsuleCollider capsuleCollider) {
                return IsGrounded(capsuleCollider);
            }
            else if (collider is SphereCollider sphereCollider) {
                return IsGrounded(sphereCollider);
            }
            else {
                Log($"ERROR : Unsupported collider: {collider.name} (position: {collider.transform.position})");
                return false;
            }
        }

        bool IsGrounded(BoxCollider boxCollider) {
            int layerMask = ~((1 << boxCollider.gameObject.layer) |     // The collider layer
                             (1 << 11) |                                // UIDialog
                             (1 << 1) |                                 // TransparentFX
                             (1 << 5) |                                 // UI
                             (1 << 12) |                                // UIVectors
                             (1 << 13) |                                // UI_Mask
                             (1 << 14) |                                // Screens
                             (1 << 25) |                                // UIAdditional
                             (1 << 10));                                // Scaled Scenery
            
            Collider[] colliders = Physics.OverlapBox(
                boxCollider.bounds.center,
                boxCollider.bounds.extents,
                boxCollider.transform.rotation,
                layerMask
            );
            
            if (colliders.Length > 0) {
                foreach (Collider collider in colliders) {
                    Log($"=> Collision with : {collider.name} on layer {collider.gameObject.layer} ({LayerMask.LayerToName(collider.gameObject.layer)})");
                }
            }
            
            return colliders.Length > 0;
        }

        bool IsGrounded(CapsuleCollider capsuleCollider) {
            Log($"IsGrounded(CapsuleCollider): {capsuleCollider.name}");
            Vector3 center = capsuleCollider.transform.position;
            float radius = capsuleCollider.radius;
            float height = capsuleCollider.height;
            Vector3 direction = capsuleCollider.transform.up;
            
            int layerMask = ~(1 << capsuleCollider.gameObject.layer);
            
            return Physics.CheckCapsule(
                center - direction * (height * 0.5f),
                center + direction * (height * 0.5f),
                radius,
                layerMask
            );
        }

        bool IsGrounded(SphereCollider sphereCollider) {
            Log($"IsGrounded(SphereCollider): {sphereCollider.name}");
            int layerMask = ~(1 << sphereCollider.gameObject.layer);
            
            return Physics.CheckSphere(
                sphereCollider.transform.position,
                sphereCollider.radius,
                layerMask
            );
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
