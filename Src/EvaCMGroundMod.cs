using System.Collections.Generic;
using UnityEngine;
using com.github.lhervier.ksp.shared;
using com.github.lhervier.ksp.evacmgroundmod.settings;

namespace com.github.lhervier.ksp.evacmgroundmod {

    /// <summary>
    /// The fix itself: while EVA construction mode is on, a part that would end up in the ground is put
    /// back where it last was.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.PSystemSpawn, false)]
    public class EvaCMGroundMod : MonoBehaviour {

        private static readonly ModLogger LOGGER = new ModLogger("GroundFix");

        /// <summary>
        /// How far down the collision test volume is dropped before looking for the ground, in meters.
        ///
        /// Static, and read on every test rather than captured once: the settings window writes it live
        /// (through the view model), while this addon is rebuilt at every scene change.
        /// </summary>
        public static float GroundOffset { get; set; } = EvaCMGroundSettings.GroundOffsetDefault;

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
            (1 << 21) |     // Part Triggers
            (1 << 26)       // WheelCollidersIgnore
        );

        private Part previousPart;
        private Vector3 previousPosition;
        private Quaternion previousRotation;

        protected void Awake()
        {
            LOGGER.LogInfo("Awaked");
            DontDestroyOnLoad(this);
        }

        public void Start() {
            GameEvents.OnEVAConstructionMode.Add(OnEVAConstructionMode);
            LOGGER.LogInfo("Plugin started");
        }

        public void OnDestroy() {
            GameEvents.onEditorPartEvent.Remove(OnEditorPartEvent);
            GameEvents.OnEVAConstructionMode.Remove(OnEVAConstructionMode);
            LOGGER.LogInfo("Plugin stopped");
        }

        public void OnEVAConstructionMode(bool mode) {
            LOGGER.LogDebug($"OnEVAConstructionMode: {mode}");
            if( mode ) {
                LOGGER.LogDebug("Starting Fix");
                GameEvents.onEditorPartEvent.Add(OnEditorPartEvent);
            }
            else {
                LOGGER.LogDebug("Stopping Fix");
                GameEvents.onEditorPartEvent.Remove(OnEditorPartEvent);
            }
        }

        Vector3 GetScale(Collider collider) {
            Vector3 colliderScale = collider.transform.lossyScale;
            return new Vector3(
                Mathf.Abs(colliderScale.x),
                Mathf.Abs(colliderScale.y),
                Mathf.Abs(colliderScale.z)
            );
        }

        /// <summary>
        /// The vector a collider's test volume is dropped by, so that a part is declared to be in the
        /// ground slightly before it actually touches it.
        /// </summary>
        private static Vector3 GetGroundOffsetVector(Vector3 worldPosition) {
            // Down is toward the center of the body, not world -Y: the flight scene is never aligned on
            // the surface, so Vector3.up points away from the ground at a single spot on the planet.
            Vector3 up = FlightGlobals.getUpAxis(FlightGlobals.currentMainBody, worldPosition);
            return -up * GroundOffset;
        }

        /// <summary>
        /// Among the candidates the broad phase returned, those the collider really penetrates once
        /// placed at <paramref name="position"/>.
        /// </summary>
        private Collider[] GetPenetratingColliders(
            Collider collider,
            Vector3 position,
            Collider[] potentialColliders
        ) {
            // Filtering the colliders that have a real penetration
            List<Collider> penetratingColliders = new List<Collider>();
            foreach (Collider otherCollider in potentialColliders) {
                // Colliders for analyse arms are not considered as colliding
                if( otherCollider.name == "rangeTrigger" && otherCollider.gameObject.layer == 15) {     // Local Scenery
                    LOGGER.LogDebug($"Skipping rangeTrigger collider...");
                    continue;
                }
                if (Physics.ComputePenetration(
                    collider,
                    // The dropped position, not collider.transform.position: this test is the one that
                    // decides, so the ground offset has to reach it. Applied to the broad phase alone, it
                    // would change nothing at all.
                    position,
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

            LOGGER.LogDebug($"Collider {collider.name}/{collider.GetType().Name} colliding with {colliders.Length} colliders");
            foreach (Collider coll in colliders) {
                LOGGER.LogDebug($"- {coll.name} on layer {coll.gameObject.layer} ({LayerMask.LayerToName(coll.gameObject.layer)})");
            }
            LOGGER.LogDebug($"");
            LOGGER.LogDebug($"Collider game hierarchy :");
            {
                Transform currentTransform = collider.transform.parent;
                while (currentTransform != null) {
                    LOGGER.LogDebug($"  Parent: {currentTransform.name} ({currentTransform.GetType().Name})");
                    currentTransform = currentTransform.parent;
                }
            }
            LOGGER.LogDebug($"");
            LOGGER.LogDebug($"Colliding colliders hierarchy :");
            foreach (Collider coll in colliders) {
                LOGGER.LogDebug($"- Collider {coll.name}/{coll.GetType().Name} hierarchy :");
                Transform currentTransform = coll.transform.parent;
                while (currentTransform != null) {
                    LOGGER.LogDebug($"  Parent: {currentTransform.name} ({currentTransform.GetType().Name})");
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
                LOGGER.LogError($"Unsupported collider type : {collider.GetType().Name} (position: {collider.transform.position})");
                colliders = new Collider[0];
            }
            LogPenetratingColliders(collider, colliders);
            return colliders.Length > 0;
        }

        Collider[] GetBoxColliders(BoxCollider boxCollider) {
            Vector3 scale = GetScale(boxCollider);
            
            Vector3 center = boxCollider.transform.position + GetGroundOffsetVector(boxCollider.transform.position);
            Vector3 scaledSize = Vector3.Scale(boxCollider.size, scale);
            Quaternion rotation = boxCollider.transform.rotation;

            Collider[] potentialColliders = Physics.OverlapBox(
                center,
                scaledSize * 0.5f,
                rotation,
                LAYER_MASK
            );
            return GetPenetratingColliders(boxCollider, center, potentialColliders);
        }

        Collider[] GetCapsuleColliders(CapsuleCollider capsuleCollider) {
            Vector3 center = capsuleCollider.transform.position + GetGroundOffsetVector(capsuleCollider.transform.position);
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
            return GetPenetratingColliders(capsuleCollider, center, potentialColliders);
        }

        Collider[] GetSphereColliders(SphereCollider sphereCollider) {
            Vector3 scale = GetScale(sphereCollider);
            // For a sphere, we use the largest scale to maintain the spherical shape
            float maxScale = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            float scaledRadius = sphereCollider.radius * maxScale;

            Vector3 center = sphereCollider.transform.position + GetGroundOffsetVector(sphereCollider.transform.position);

            Collider[] potentialColliders = Physics.OverlapSphere(
                center,
                scaledRadius,
                LAYER_MASK
            );
            return GetPenetratingColliders(sphereCollider, center, potentialColliders);
        }

        Collider[] GetMeshColliders(MeshCollider meshCollider) {
            // Getting all the colliders in the zone
            Vector3 scale = GetScale(meshCollider);
            Vector3 scaledExtents = Vector3.Scale(
                meshCollider.bounds.extents,
                scale
            );

            // The broad phase is centered on the bounds and the penetration test on the transform, so the
            // drop is applied to both rather than to a single shared center.
            Vector3 offset = GetGroundOffsetVector(meshCollider.transform.position);

            Collider[] potentialColliders = Physics.OverlapBox(
                meshCollider.bounds.center + offset,
                scaledExtents,
                meshCollider.transform.rotation,
                LAYER_MASK
            );

            return GetPenetratingColliders(meshCollider, meshCollider.transform.position + offset, potentialColliders);
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
