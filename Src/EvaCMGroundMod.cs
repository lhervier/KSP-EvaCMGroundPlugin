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
        /// Layer mask for the colliders that we want to check. Layers alone do not say what is solid:
        /// every query below also passes QueryTriggerInteraction.Ignore, because a trigger is a volume a
        /// module watches, not something a part can rest on, and nothing keeps one on a solid layer --
        /// ModuleRobotArmScanner puts a 4 m trigger sphere on Local Scenery.
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

        /// <summary>
        /// Shortest move worth sweeping, in meters.
        /// </summary>
        private const float MIN_SWEEP_DISTANCE = 1e-4f;

        /// <summary>
        /// How many times the search for the ground is halved once it has been bracketed.
        /// </summary>
        private const int BISECTION_STEPS = 12;

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

            // Both ways: the pose we remember only makes sense inside a single construction
            // session. Entering the mode again and grabbing the same part would otherwise match
            // "part == previousPart" and keep a stale reference pose, which the sweep would use as
            // its origin (and, on a blocked move, restore the part to).
            this.previousPart = null;

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
                LAYER_MASK,
                QueryTriggerInteraction.Ignore
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
                LAYER_MASK,
                QueryTriggerInteraction.Ignore
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
                LAYER_MASK,
                QueryTriggerInteraction.Ignore
            );
            return GetPenetratingColliders(sphereCollider, transformWorldPosition, potentialColliders);
        }

        Collider[] GetMeshColliders(MeshCollider meshCollider) {
            // The broad phase is centered on the bounds and the penetration test on the transform, so the
            // drop is applied to both rather than to a single shared center.
            Vector3 offset = GetGroundOffsetVector(meshCollider.transform.position);

            // Collider.bounds is already a world space, axis aligned bounding box : its extents are world
            // units (no lossyScale to apply) and its axes are the world ones. Hence the identity rotation :
            // rotating that box with the transform would describe a volume the collider does not occupy.
            Collider[] potentialColliders = Physics.OverlapBox(
                meshCollider.bounds.center + offset,
                meshCollider.bounds.extents,
                Quaternion.identity,
                LAYER_MASK,
                QueryTriggerInteraction.Ignore
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
        /// How far <paramref name="collider"/> can travel along <paramref name="direction"/>, from the pose
        /// the physics scene currently holds, before it reaches the ground. Capped at
        /// <paramref name="distance"/>, which is also what comes back when nothing stands in the way.
        /// </summary>
        private float GetCastDistance(Collider collider, Vector3 direction, float distance) {
            Vector3 scale = GetScale(collider);

            // A cast reports whatever its shape already overlaps as a hit at distance zero, and tells
            // nothing of what lies further on. Starting it a shape's length behind gives it clear room to
            // begin in, and the same length is taken off the answer.
            float backoff = GetProjectedExtent(collider.bounds, direction) + GroundOffset;
            Vector3 origin = -direction * backoff;
            float castDistance = distance + backoff;

            RaycastHit[] hits;
            if (collider is BoxCollider boxCollider) {
                hits = Physics.BoxCastAll(
                    boxCollider.transform.TransformPoint(boxCollider.center) + origin,
                    Vector3.Scale(boxCollider.size, scale) * 0.5f,
                    direction,
                    boxCollider.transform.rotation,
                    castDistance,
                    LAYER_MASK,
                    QueryTriggerInteraction.Ignore
                );
            }
            else if (collider is SphereCollider sphereCollider) {
                hits = Physics.SphereCastAll(
                    sphereCollider.transform.TransformPoint(sphereCollider.center) + origin,
                    sphereCollider.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)),
                    direction,
                    castDistance,
                    LAYER_MASK,
                    QueryTriggerInteraction.Ignore
                );
            }
            else if (collider is CapsuleCollider capsuleCollider) {
                float radiusScale;
                float heightScale;
                switch (capsuleCollider.direction) {
                    case 0:     // X-axis
                        radiusScale = Mathf.Max(scale.y, scale.z);
                        heightScale = scale.x;
                        break;
                    case 1:     // Y-axis
                        radiusScale = Mathf.Max(scale.x, scale.z);
                        heightScale = scale.y;
                        break;
                    default:    // Z-axis
                        radiusScale = Mathf.Max(scale.x, scale.y);
                        heightScale = scale.z;
                        break;
                }
                float scaledRadius = capsuleCollider.radius * radiusScale;
                float height = (capsuleCollider.height * heightScale) - (2 * scaledRadius);
                Quaternion rotation = capsuleCollider.transform.rotation;
                Vector3 directionVector;
                switch (capsuleCollider.direction) {
                    case 0:     // X-axis
                        directionVector = rotation * Vector3.right;
                        break;
                    case 2:     // Z-axis
                        directionVector = rotation * Vector3.forward;
                        break;
                    default:    // Y-axis
                        directionVector = rotation * Vector3.up;
                        break;
                }
                Vector3 worldCenter = capsuleCollider.transform.TransformPoint(capsuleCollider.center) + origin;
                hits = Physics.CapsuleCastAll(
                    worldCenter - directionVector * (height * 0.5f),
                    worldCenter + directionVector * (height * 0.5f),
                    scaledRadius,
                    direction,
                    castDistance,
                    LAYER_MASK,
                    QueryTriggerInteraction.Ignore
                );
            }
            else {
                // Unity sweeps boxes, spheres and capsules, never an arbitrary mesh. The collider's world
                // bounding box stands in for it. Reporting contact early is harmless: this only says that
                // something is on the way, the stopping point is settled afterwards on the real shape.
                Bounds bounds = collider.bounds;
                hits = Physics.BoxCastAll(
                    bounds.center + origin,
                    bounds.extents,
                    direction,
                    Quaternion.identity,
                    castDistance,
                    LAYER_MASK,
                    QueryTriggerInteraction.Ignore
                );
            }

            float reachable = distance;
            foreach (RaycastHit hit in hits) {
                reachable = Mathf.Min(reachable, Mathf.Max(0f, hit.distance - backoff));
            }
            return reachable;
        }

        /// <summary>
        /// How far the world bounding box of <paramref name="bounds"/> reaches along
        /// <paramref name="direction"/>, from its centre.
        /// </summary>
        private static float GetProjectedExtent(Bounds bounds, Vector3 direction) {
            Vector3 extents = bounds.extents;
            return Mathf.Abs(direction.x) * extents.x
                 + Mathf.Abs(direction.y) * extents.y
                 + Mathf.Abs(direction.z) * extents.z;
        }

        /// <summary>
        /// Whether <paramref name="part"/> is in the ground in the given pose. It is left in that pose,
        /// the physics scene included.
        /// </summary>
        private bool IsPoseInGroundAt(
            Part part,
            Collider[] colliders,
            Vector3 position,
            Quaternion rotation
        ) {
            part.transform.position = position;
            part.transform.rotation = rotation;
            // The tests read the physics scene, which does not necessarily follow a transform written from
            // a script.
            Physics.SyncTransforms();
            return IsPoseInGround(part, colliders);
        }

        /// <summary>
        /// Whether <paramref name="part"/> is in the ground once moved <paramref name="travel"/> meters
        /// along <paramref name="direction"/> from <paramref name="fromPosition"/>. Its rotation is left
        /// untouched.
        /// </summary>
        private bool IsInGroundAt(
            Part part,
            Collider[] colliders,
            Vector3 fromPosition,
            Vector3 direction,
            float travel
        ) {
            return IsPoseInGroundAt(
                part,
                colliders,
                fromPosition + direction * travel,
                part.transform.rotation
            );
        }

        /// <summary>
        /// Where <paramref name="part"/> ends up when moved from <paramref name="fromPosition"/> toward
        /// <paramref name="toPosition"/>: the target itself when the way is clear, otherwise the furthest
        /// point along the way that still keeps its colliders <see cref="GroundOffset"/> clear of the ground.
        ///
        /// <paramref name="fromPosition"/> has to be a pose that is out of the ground: the answer is
        /// bracketed between it and the first pose found in the ground, so a start already in the ground
        /// would be handed back as the only reachable point. The caller rules that case out.
        /// </summary>
        private Vector3 GetReachablePosition(
            Part part,
            Collider[] colliders,
            Vector3 fromPosition,
            Vector3 toPosition
        ) {
            Vector3 move = toPosition - fromPosition;
            float distance = move.magnitude;
            if (distance < MIN_SWEEP_DISTANCE) {
                return toPosition;
            }
            Vector3 direction = move / distance;

            part.transform.position = fromPosition;
            Physics.SyncTransforms();

            // A cast only has to answer whether anything stands on the way, not where the part stops: for a
            // mesh it sweeps a bounding box, so it reports contact somewhat early, and that is harmless
            // here. What it buys is the common case, an open move settled in one query.
            float firstTouch = distance;
            float window = 0f;
            foreach (Collider collider in colliders) {
                firstTouch = Mathf.Min(firstTouch, GetCastDistance(collider, direction, distance));
                window = Mathf.Max(window, 2f * GetProjectedExtent(collider.bounds, direction));
            }
            if (firstTouch >= distance) {
                LOGGER.LogDebug("Nothing on the way, the move is granted whole");
                return toPosition;
            }

            // Where the colliders themselves touch is found on ComputePenetration, which knows their real
            // shape. It is looked for from the box's contact onwards, over a window of twice the box: the
            // real contact cannot be further than the box is wide, and neither can the stretch over which
            // the part is still crossing the surface. That window is a property of the part, not of how far
            // the player dragged, so the search stays bounded however long the move is.
            // Once something has been found on the way, the move is granted at most one window at a time.
            // The window is what the geometry lets us vouch for: the colliders cannot touch further than
            // their own box from where its own contact was reported, nor stay in contact for longer than
            // that again. Past it nothing has been looked at -- another slope, another building -- so the
            // rest of the move is left for the next event rather than granted on faith.
            float searchEnd = Mathf.Min(distance, firstTouch + window);
            // The resolution is a fraction of the window, not of any collider dimension. How thick a part
            // is says nothing about how far it travels while it still overlaps the ground: measured on the
            // Oscar-B, a collider 0.35 m tall crosses in under 0.19 m, and a step drawn from its bounding
            // box (0.31 m) stepped clean over it.
            float step = Mathf.Max(MIN_SWEEP_DISTANCE, window / MAX_SWEEP_STEPS);

            // The probes are spread over the window rather than counted off from its start, so that the far
            // end is always one of them. Stepping from the start would let a move shorter than one step be
            // judged on its starting pose alone, which is known to be clear anyway.
            int probes = Mathf.Clamp(Mathf.CeilToInt((searchEnd - firstTouch) / step), 1, MAX_SWEEP_STEPS);
            float free = 0f;
            float blocked = -1f;
            for (int probe = 0; probe <= probes; probe++) {
                float travel = Mathf.Lerp(firstTouch, searchEnd, (float)probe / probes);
                if (IsInGroundAt(part, colliders, fromPosition, direction, travel)) {
                    blocked = travel;
                    break;
                }
                free = travel;
            }
            if (blocked < 0f) {
                return fromPosition + direction * searchEnd;
            }

            // Both bounds are tested for real, so the answer is squeezed between a pose known to be clear
            // and a pose known to be in the ground: the part can neither be stopped short of the ground nor
            // slip past it.
            for (int i = 0; i < BISECTION_STEPS; i++) {
                float middle = 0.5f * (free + blocked);
                if (IsInGroundAt(part, colliders, fromPosition, direction, middle)) {
                    blocked = middle;
                } else {
                    free = middle;
                }
            }

            LOGGER.LogDebug($"Ground reached {free:F3} m away, stopping {GroundOffset:F3} m short of it");
            return fromPosition + direction * Mathf.Max(0f, free - GroundOffset);
        }

        /// <summary>
        /// The colliders of <paramref name="part"/> that take part in collisions, the only ones this fix
        /// has any reason to keep out of the ground.
        /// </summary>
        private static Collider[] GetSolidColliders(Part part) {
            List<Collider> solidColliders = new List<Collider>();
            foreach (Collider collider in part.GetComponentsInChildren<Collider>()) {
                // A trigger has no solidity: it is a volume a module watches for something entering it, and
                // it is usually far larger than the part. ModuleRobotArmScanner hangs a 4 m sphere off the
                // arm that way, which would stop the part 4 m above the ground.
                // A disabled collider is out of the physics scene entirely, so nothing can rest on it
                // either, but GetComponentsInChildren still hands it over.
                if (!collider.enabled || collider.isTrigger) {
                    continue;
                }
                solidColliders.Add(collider);
            }
            return solidColliders.ToArray();
        }

        /// <summary>
        /// Whether <paramref name="part"/> is in the ground in the pose it currently holds.
        /// </summary>
        private bool IsPoseInGround(Part part, Collider[] colliders) {
            foreach (Collider collider in colliders) {
                if (IsCollidingWithGround(collider)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether <paramref name="eventType"/> is one of the gizmo moves this fix truncates.
        /// </summary>
        private static bool IsGizmoMove(ConstructionEventType eventType) {
            return eventType == ConstructionEventType.PartOffsetting
                || eventType == ConstructionEventType.PartOffset
                || eventType == ConstructionEventType.PartRotating
                || eventType == ConstructionEventType.PartRotated;
        }

        private void OnEditorPartEvent(ConstructionEventType eventType, Part part) {
            // onEditorPartEvent carries every construction event, not only the gizmo moves. Picking a part
            // up, dropping it, attaching or detaching it are placements KSP has already decided on, and
            // they are not a move away from the pose we are following: an attach has just snapped the part
            // onto a node, so truncating it would leave the part off the node its attachment data says it
            // sits on. Some do not even carry the part being followed -- PartDetached fires on the hovered
            // part.
            // Whatever KSP does to a part outside of a gizmo move, the pose it leaves it in is the one the
            // next move has to start from, so the part simply stops being followed rather than being
            // tracked across the event. The next gizmo move picks it up again where it really is.
            if( !IsGizmoMove(eventType) ) {
                if( this.previousPart != null ) {
                    LOGGER.LogDebug($"[{eventType}] Not a gizmo move: no longer following {this.previousPart.partInfo.name}");
                    this.previousPart = null;
                }
                return;
            }

            if( 
                part == this.previousPart && 
                part.transform.position == this.previousPosition && 
                part.transform.rotation == this.previousRotation 
            ) {
                return;
            }

            if( part != this.previousPart ) {
                LOGGER.LogDebug(
                    $"Now following {part.partInfo.name}, from {part.transform.position.ToString("F3")}"
                );

                // Seems to stabilize the parts when changing.
                if( previousPart != null ) {
                    this.previousPart.transform.position = this.previousPosition;
                    this.previousPart.transform.rotation = this.previousRotation;
                }
                
                this.previousPart = part;
                this.previousPosition = part.transform.position;
                this.previousRotation = part.transform.rotation;
            }

            Collider[] colliders = GetSolidColliders(part);
            Vector3 targetPosition = part.transform.position;
            Quaternion targetRotation = part.transform.rotation;

            // The move is cut short at the ground rather than refused. Sweeping the real colliders answers
            // in a single query whatever the distance, so crossing the ground stops being something the test
            // has to catch in time and becomes something the part cannot do: however long the player keeps
            // dragging, the part is only ever put down on the near side of what stands in its way.
            LOGGER.LogDebug(
                $"[{eventType}] {part.partInfo.name}: {Vector3.Distance(this.previousPosition, targetPosition):F3} m" +
                $" and {Quaternion.Angle(this.previousRotation, targetRotation):F1} deg asked for"
            );

            // Both stages below measure a move away from a pose they take for clear, and a part can start
            // from inside the ground all the same: attached on a node, terrain detail changed under it,
            // pose followed from before a scene change. Held to that start, neither stage has an answer.
            // The truncation would bracket the stopping point between a start it believes clear and the
            // first pose found in the ground, that is to say between the start and the start: the part
            // would be pinned where it stands, in every direction, digging itself out included. The
            // rotation walk would read its very first pose as a hit and put the part back on the buried
            // pose it came from. So the move is granted whole and the part stays draggable; whatever pose
            // it is dropped on becomes the next start, and truncation resumes as soon as that one is clear.
            if( IsPoseInGroundAt(part, colliders, this.previousPosition, this.previousRotation) ) {
                LOGGER.LogDebug($"Starting pose is already in the ground, the move is granted whole");
                this.previousPosition = targetPosition;
                this.previousRotation = targetRotation;
                part.transform.position = targetPosition;
                part.transform.rotation = targetRotation;
                Physics.SyncTransforms();
                return;
            }
            // The test above left the part on the starting pose. What follows measures the translation on
            // the rotation the move asks for, the one KSP had written before the event, so it is put back.
            part.transform.position = targetPosition;
            part.transform.rotation = targetRotation;

            targetPosition = GetReachablePosition(part, colliders, this.previousPosition, targetPosition);
            part.transform.position = targetPosition;

            // The rotation is still walked pose by pose: a sweep travels in a straight line and cannot
            // express it, while a collider away from the part origin travels an arc that steps over a
            // surface just like a translation does. That travel is bounded by the angle, so it cannot grow
            // the way a held drag does. Terrain and buildings are non convex MeshColliders, that is to say
            // surfaces with no thickness at all: a collider landing entirely below one penetrates nothing.
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

            if (inGround) {
                LOGGER.LogDebug($"Rotation walked over {steps} pose(s) hits the ground, keeping the previous pose");
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
