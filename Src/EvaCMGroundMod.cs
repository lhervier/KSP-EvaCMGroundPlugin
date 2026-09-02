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

        /// <summary>
        /// Largest number of intermediate poses a single move is cut into.
        /// </summary>
        private const int MAX_SWEEP_STEPS = 32;

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
            
            // The broad phase is centered on the collider's own volume (transform + local center) and the
            // penetration test on the transform, so the drop is applied to both rather than to a single
            // shared center.
            Vector3 offset = GetGroundOffsetVector(boxCollider.transform.position);
            Vector3 volumeWorldCenter = boxCollider.transform.TransformPoint(boxCollider.center) + offset;
            Vector3 transformWorldPosition = boxCollider.transform.position + offset;

            Vector3 scaledSize = Vector3.Scale(boxCollider.size, scale);
            Quaternion rotation = boxCollider.transform.rotation;

            Collider[] potentialColliders = Physics.OverlapBox(
                volumeWorldCenter,
                scaledSize * 0.5f,
                rotation,
                LAYER_MASK
            );
            return GetPenetratingColliders(boxCollider, transformWorldPosition, potentialColliders);
        }

        Collider[] GetCapsuleColliders(CapsuleCollider capsuleCollider) {
            // The broad phase is centered on the collider's own volume (transform + local center) and the
            // penetration test on the transform, so the drop is applied to both rather than to a single
            // shared center.
            Vector3 offset = GetGroundOffsetVector(capsuleCollider.transform.position);
            Vector3 volumeWorldCenter = capsuleCollider.transform.TransformPoint(capsuleCollider.center) + offset;
            Vector3 transformWorldPosition = capsuleCollider.transform.position + offset;

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
            Vector3 point1 = volumeWorldCenter - directionVector * (height * 0.5f);
            Vector3 point2 = volumeWorldCenter + directionVector * (height * 0.5f);
            
            // Returning the colliders that intersect the capsule.
            Collider[] potentialColliders = Physics.OverlapCapsule(
                point1,
                point2,
                scaledRadius,
                LAYER_MASK
            );
            return GetPenetratingColliders(capsuleCollider, transformWorldPosition, potentialColliders);
        }

        Collider[] GetSphereColliders(SphereCollider sphereCollider) {
            Vector3 scale = GetScale(sphereCollider);
            // For a sphere, we use the largest scale to maintain the spherical shape
            float maxScale = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            float scaledRadius = sphereCollider.radius * maxScale;

            // The broad phase is centered on the collider's own volume (transform + local center) and the
            // penetration test on the transform, so the drop is applied to both rather than to a single
            // shared center.
            Vector3 offset = GetGroundOffsetVector(sphereCollider.transform.position);
            Vector3 volumeWorldCenter = sphereCollider.transform.TransformPoint(sphereCollider.center) + offset;
            Vector3 transformWorldPosition = sphereCollider.transform.position + offset;

            Collider[] potentialColliders = Physics.OverlapSphere(
                volumeWorldCenter,
                scaledRadius,
                LAYER_MASK
            );
            return GetPenetratingColliders(sphereCollider, transformWorldPosition, potentialColliders);
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

        /// <summary>
        /// The smallest distance across <paramref name="collider"/>, in world units.
        /// </summary>
        private float GetSmallestThickness(Collider collider) {
            Vector3 scale = GetScale(collider);

            if (collider is BoxCollider boxCollider) {
                Vector3 scaledSize = Vector3.Scale(boxCollider.size, scale);
                return Mathf.Min(scaledSize.x, Mathf.Min(scaledSize.y, scaledSize.z));
            }
            if (collider is SphereCollider sphereCollider) {
                return 2f * sphereCollider.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            }
            if (collider is CapsuleCollider capsuleCollider) {
                float radiusScale;
                switch (capsuleCollider.direction) {
                    case 0:     // X-axis
                        radiusScale = Mathf.Max(scale.y, scale.z);
                        break;
                    case 1:     // Y-axis
                        radiusScale = Mathf.Max(scale.x, scale.z);
                        break;
                    default:    // Z-axis
                        radiusScale = Mathf.Max(scale.x, scale.y);
                        break;
                }
                // Across the capsule, never along it: it is nowhere thinner than its diameter.
                return 2f * capsuleCollider.radius * radiusScale;
            }

            Vector3 boundsSize = collider.bounds.size;
            return Mathf.Min(boundsSize.x, Mathf.Min(boundsSize.y, boundsSize.z));
        }

        /// <summary>
        /// How many intermediate poses the move of <paramref name="part"/> from
        /// <paramref name="fromPosition"/>/<paramref name="fromRotation"/> to the pose it currently holds
        /// has to be cut into, so that the test walks over everything standing in the way.
        /// </summary>
        private int GetSweepStepCount(
            Part part,
            Collider[] colliders,
            Vector3 fromPosition,
            Quaternion fromRotation
        ) {
            // The thinnest collider sets the step: what has to be ruled out is a move long enough to take
            // a collider from one side of a surface to the other without ever overlapping it.
            float thinnest = float.MaxValue;
            // How far from the part origin the colliders reach, to turn the rotation into a length.
            float radius = 0f;
            foreach (Collider collider in colliders) {
                thinnest = Mathf.Min(thinnest, GetSmallestThickness(collider));

                Bounds bounds = collider.bounds;
                radius = Mathf.Max(
                    radius,
                    Vector3.Distance(bounds.center, part.transform.position) + bounds.extents.magnitude
                );
            }
            if (thinnest <= 0f) {
                // A collider with no thickness cannot be walked over safely at any resolution.
                return MAX_SWEEP_STEPS;
            }

            // Rotation is part of the move: a collider away from the part origin travels an arc, and that
            // arc steps over a surface just like a translation does.
            float travelled =
                Vector3.Distance(fromPosition, part.transform.position)
                + Quaternion.Angle(fromRotation, part.transform.rotation) * Mathf.Deg2Rad * radius;

            // Half the thinnest collider, so that at least one pose lands inside whatever is crossed.
            int steps = Mathf.CeilToInt(travelled / (thinnest * 0.5f));
            return Mathf.Clamp(steps, 1, MAX_SWEEP_STEPS);
        }

        /// <summary>
        /// Whether <paramref name="part"/> is in the ground in the pose it currently holds.
        /// </summary>
        private bool IsPoseInGround(Part part, Collider[] colliders) {
            // Checking altitude of the part center to see if it's below the ground
            double partCenterLatitude = FlightGlobals.currentMainBody.GetLatitude(part.transform.position);
            double partCenterLongitude = FlightGlobals.currentMainBody.GetLongitude(part.transform.position);
            double partCenterAltitude = FlightGlobals.currentMainBody.GetAltitude(part.transform.position);

            double terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(partCenterLatitude, partCenterLongitude, true);
            double heightAboveTerrain = partCenterAltitude - terrainAltitude;

            if (heightAboveTerrain < 0) {
                return true;
            }

            foreach (Collider collider in colliders) {
                if (IsCollidingWithGround(collider)) {
                    return true;
                }
            }
            return false;
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

            Collider[] colliders = part.GetComponentsInChildren<Collider>();
            Vector3 targetPosition = part.transform.position;
            Quaternion targetRotation = part.transform.rotation;

            // The whole move is walked pose by pose, not just tested where it ends. The editor never moves
            // its gizmo back when this fix puts the part back, so the two drift apart for as long as the
            // player keeps dragging, and a single event ends up carrying the part a long way. Terrain and
            // buildings are non convex MeshColliders, that is to say surfaces with no thickness at all: a
            // collider landing entirely below one penetrates nothing, and would be let through.
            bool inGround = false;
            int steps = GetSweepStepCount(part, colliders, this.previousPosition, this.previousRotation);
            for (int step = 1; step <= steps; step++) {
                float ratio = (float)step / steps;
                part.transform.position = Vector3.Lerp(this.previousPosition, targetPosition, ratio);
                part.transform.rotation = Quaternion.Slerp(this.previousRotation, targetRotation, ratio);

                // The broad phase reads the physics scene, which does not necessarily follow a transform
                // written from a script.
                Physics.SyncTransforms();

                if (IsPoseInGround(part, colliders)) {
                    inGround = true;
                    break;
                }
            }

            if( inGround ) {
                part.transform.position = this.previousPosition;
                part.transform.rotation = this.previousRotation;
            } else {
                this.previousPosition = targetPosition;
                this.previousRotation = targetRotation;
                part.transform.position = targetPosition;
                part.transform.rotation = targetRotation;
            }

            // Leaving the physics scene on one of the poses walked through above would describe the part
            // somewhere it is not.
            Physics.SyncTransforms();
        }
    }
}
