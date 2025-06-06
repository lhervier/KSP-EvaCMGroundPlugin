using System;
using System.Collections.Generic;
using System.IO;
using Expansions.Missions.Editor;
using UnityEngine;

namespace com.github.lhervier.ksp {
	
	[KSPAddon(KSPAddon.Startup.PSystemSpawn, false)]
    public class EvaCMGroundMod : MonoBehaviour {
        
        private static bool DEBUG = false;
        private static float GROUND_OFFSET = 0.01f;
        private static readonly string CONFIG_FILE = "eva_cm_ground.cfg";

        /// <summary>
        /// Layer mask for the colliders that we want to check.
        //  Layer 0: Default
        //  Layer 1: TransparentFX
        //  Layer 2: Ignore Raycast
        //  Layer 4: Water
        //  Layer 5: UI
        //  Layer 8: PartsList_Icons
        //  Layer 9: Atmosphere
        //  Layer 10: Scaled Scenery
        //  Layer 11: UIDialog
        //  Layer 12: UIVectors
        //  Layer 13: UI_Mask
        //  Layer 14: Screens
        //  Layer 15: Local Scenery
        //  Layer 16: kerbals
        //  Layer 17: EVA
        //  Layer 18: SkySphere
        //  Layer 19: PhysicalObjects
        //  Layer 20: Internal Space
        //  Layer 21: Part Triggers
        //  Layer 22: KerbalInstructors
        //  Layer 23: AeroFXIgnore
        //  Layer 24: MapFX
        //  Layer 25: UIAdditional
        //  Layer 26: WheelCollidersIgnore
        //  Layer 27: WheelColliders
        //  Layer 28: TerrainColliders
        //  Layer 29: DragRender
        //  Layer 30: SurfaceFX
        //  Layer 31: Vectors
        /// </summary>
        private static readonly int LAYER_MASK = 
        ~(
            (1 << 0 ) |     // Default
            (1 << 11) |     // UIDialog
            (1 << 1 ) |     // TransparentFX
            (1 << 5 ) |     // UI
            (1 << 12) |     // UIVectors
            (1 << 13) |     // UI_Mask
            (1 << 14) |     // Screens
            (1 << 25) |     // UIAdditional
            (1 << 10) |     // Scaled Scenery
            (1 << 21)       // Part Triggers
        );

        private Part previousPart;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        
        private static void LogInternal(string level, string message) {
            Debug.Log($"[EvaCMGroundMod][{level}] {message}");
        }

        private static void LogInfo(string message) {
            LogInternal("INFO", message);
        }

        private static void LogDebug(string message) {
            if( !DEBUG ) {
                return;
            }
            LogInternal("DEBUG", message);
        }

        private static void LogError(string message) {
            LogInternal("ERROR", message);
        }

        private static void InitDebugMode() {
            try {
                // Get the directory where the mod DLL is located
                string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string modDirectory = Path.GetDirectoryName(dllPath);
                string configPath = Path.Combine(modDirectory, CONFIG_FILE);

                if (File.Exists(configPath)) {
                    string[] lines = File.ReadAllLines(configPath);
                    foreach (string line in lines) {
                        string trimmedLine = line.Trim();
                        if( trimmedLine.StartsWith("#") ) {
                            continue;
                        }
                        if (trimmedLine.StartsWith("debug=")) {
                            string value = trimmedLine.Substring(6).Trim().ToLower();
                            DEBUG = (value == "true" || value == "1" || value == "yes");
                            break;
                        }
                        if (trimmedLine.StartsWith("ground_offset=")) {
                            string value = trimmedLine.Substring(14).Trim();
                            GROUND_OFFSET = float.Parse(value);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex) {
                Debug.LogError($"[EvaCMGroundMod] Error reading config file: {ex.Message}");
            }
        }

        protected void Awake() 
        {
            InitDebugMode();
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

        Vector3 GetScale(Collider collider) {
            Vector3 colliderScale = collider.transform.lossyScale;
            return new Vector3(
                Mathf.Abs(colliderScale.x),
                Mathf.Abs(colliderScale.y),
                Mathf.Abs(colliderScale.z)
            );
        }

        private Collider[] GetPenetratingColliders(
            Collider collider,
            Collider[] potentialColliders
        ) {
            // Filtering the colliders that have a real penetration
            List<Collider> penetratingColliders = new List<Collider>();
            foreach (Collider otherCollider in potentialColliders) {
                // Colliders for analyse arms are not considered as colliding
                if( otherCollider.name == "rangeTrigger" && otherCollider.gameObject.layer == 15) {     // Local Scenery
                    LogDebug($"Skipping rangeTrigger collider...");
                    continue;
                }
                if (Physics.ComputePenetration(
                    collider, 
                    collider.transform.position, 
                    collider.transform.rotation,
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

        private void LogPenetratingColliders(Collider collider, Collider[] colliders) {
            if( colliders.Length == 0 ) return;
            if( !DEBUG ) return;

            LogDebug($"Collider {collider.name}/{collider.GetType().Name} colliding with {colliders.Length} colliders");
            foreach (Collider coll in colliders) {
                LogDebug($"- {coll.name} on layer {coll.gameObject.layer} ({LayerMask.LayerToName(coll.gameObject.layer)})");
            }
            LogDebug($"");
            LogDebug($"Collider game hierarchy :");
            {
                Transform currentTransform = collider.transform.parent;
                while (currentTransform != null) {
                    LogDebug($"  Parent: {currentTransform.name} ({currentTransform.GetType().Name})");
                    currentTransform = currentTransform.parent;
                }
            }
            LogDebug($"");
            LogDebug($"Colliding colliders hierarchy :");
            foreach (Collider coll in colliders) {
                LogDebug($"- Collider {coll.name}/{coll.GetType().Name} hierarchy :");
                Transform currentTransform = coll.transform.parent;
                while (currentTransform != null) {
                    LogDebug($"  Parent: {currentTransform.name} ({currentTransform.GetType().Name})");
                    currentTransform = currentTransform.parent;
                }
            }
        }
        
        bool IsCollidingWithGround(Collider collider) {
            Collider[] colliders;
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
            LogPenetratingColliders(collider, colliders);
            return colliders.Length > 0;
        }

        Collider[] GetBoxColliders(BoxCollider boxCollider) {
            Vector3 scale = GetScale(boxCollider);
            
            Vector3 center = boxCollider.transform.position - Vector3.up * GROUND_OFFSET;  // Adding 0.01 unit up to avoid collision with the ground
            Vector3 scaledSize = Vector3.Scale(boxCollider.size, scale);
            Quaternion rotation = boxCollider.transform.rotation;

            Collider[] potentialColliders = Physics.OverlapBox(
                center,
                scaledSize * 0.5f,
                rotation,
                LAYER_MASK
            );
            return GetPenetratingColliders(boxCollider, potentialColliders);
        }

        Collider[] GetCapsuleColliders(CapsuleCollider capsuleCollider) {
            Vector3 center = capsuleCollider.transform.position - Vector3.up * GROUND_OFFSET;  // Adding 0.01 unit up to avoid collision with the ground
            Vector3 scale = GetScale(capsuleCollider);
            
            // Calculate radius using the maximum scale of the two perpendicular axes
            float radiusScale;
            float heightScale;
            switch (capsuleCollider.direction) {
                case 0: // X-axis
                    radiusScale = Mathf.Max(scale.y, scale.z);
                    heightScale = scale.x;
                    break;
                case 1: // Y-axis
                    radiusScale = Mathf.Max(scale.x, scale.z);
                    heightScale = scale.y;
                    break;
                default: // Z-axis
                    radiusScale = Mathf.Max(scale.x, scale.y);
                    heightScale = scale.z;
                    break;
            }
            
            float scaledRadius = capsuleCollider.radius * radiusScale;
            float scaledHeight = capsuleCollider.height * heightScale;
            float height = scaledHeight - (2 * scaledRadius);
            Quaternion rotation = capsuleCollider.transform.rotation;
            int direction = capsuleCollider.direction;
            
            // Calculating the direction of the capsule.
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
            
            // Calculating the two points that define the capsule.
            Vector3 point1 = center - directionVector * (height * 0.5f);
            Vector3 point2 = center + directionVector * (height * 0.5f);
            
            // Returning the colliders that intersect the capsule.
            Collider[] potentialColliders = Physics.OverlapCapsule(
                point1,
                point2,
                scaledRadius,
                LAYER_MASK
            );
            return GetPenetratingColliders(capsuleCollider, potentialColliders);
        }

        Collider[] GetSphereColliders(SphereCollider sphereCollider) {
            Vector3 scale = GetScale(sphereCollider);
            // For a sphere, we use the largest scale to maintain the spherical shape
            float maxScale = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            float scaledRadius = sphereCollider.radius * maxScale;
            
            Collider[] potentialColliders = Physics.OverlapSphere(
                sphereCollider.transform.position - Vector3.up * GROUND_OFFSET,  // Adding 0.01 unit up to avoid collision with the ground
                scaledRadius,
                LAYER_MASK
            );
            return GetPenetratingColliders(sphereCollider, potentialColliders);
        }

        Collider[] GetMeshColliders(MeshCollider meshCollider) {
            // Getting all the colliders in the zone
            Vector3 scale = GetScale(meshCollider);
            Vector3 scaledExtents = Vector3.Scale(
                meshCollider.bounds.extents,
                scale
            );

            Collider[] potentialColliders = Physics.OverlapBox(
                meshCollider.bounds.center,
                scaledExtents,
                meshCollider.transform.rotation,
                LAYER_MASK
            );

            return GetPenetratingColliders(meshCollider, potentialColliders);
        }

        private void OnEditorPartEvent(ConstructionEventType eventType, Part part) {
            if( 
                part == this.previousPart && 
                part.transform.position == this.previousPosition && 
                part.transform.rotation == this.previousRotation 
            ) {
                return;
            }

            if( part != this.previousPart ) {
                // Seems to stabilize the parts when changing.
                if( previousPart != null ) {
                    this.previousPart.transform.position = this.previousPosition;
                    this.previousPart.transform.rotation = this.previousRotation;
                }
                
                this.previousPart = part;
                this.previousPosition = part.transform.position;
                this.previousRotation = part.transform.rotation;
            }

            // Checking altitude of the part center to see if it's below the ground
            double partCenterLatitude = FlightGlobals.currentMainBody.GetLatitude(part.transform.position);
            double partCenterLongitude = FlightGlobals.currentMainBody.GetLongitude(part.transform.position);
            double partCenterAltitude = FlightGlobals.currentMainBody.GetAltitude(part.transform.position);

            double terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(partCenterLatitude, partCenterLongitude, true);
            double heightAboveTerrain = partCenterAltitude - terrainAltitude;
            
            bool inGround = false;
            if (heightAboveTerrain < 0) {
                inGround = true;
            }
            else {
                Collider[] colliders = part.GetComponentsInChildren<Collider>();
                foreach (Collider collider in colliders) {
                    if (IsCollidingWithGround(collider)) {
                        inGround = true;
                        break;
                    }
                }
            }

            if( inGround ) {
                part.transform.position = this.previousPosition;
                part.transform.rotation = this.previousRotation;
            } else {
                this.previousPosition = part.transform.position;
                this.previousRotation = part.transform.rotation;
            }
        }
    }
}
