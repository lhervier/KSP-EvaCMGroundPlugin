using System;
using HarmonyLib;
using UnityEngine;
using com.github.lhervier.ksp.shared;

namespace com.github.lhervier.ksp.evacmgroundmod.terrain
{
    /// <summary>
    /// Builds the terrain's collision mesh where the terrain actually is.
    ///
    /// KSP computes the height of every PQS vertex in double, exactly and reproducibly, then places the
    /// mesh through Unity Transforms in float — on vectors 600 km long, where a float's quantization step
    /// is 62.5 mm. The rounding depends on the world frame's azimuth, which differs at every load (it
    /// survives a scene change, and even a restart of the game), so the same quad is built up to 22 cm
    /// higher or lower each time. That is what makes an anchored base look planted one time and buried
    /// the next, and what makes a large base explode on its first load.
    ///
    /// This redoes the same two placements in double, subtracting before converting to float rather than
    /// after: what reaches a float is then a distance within the quad — 1.7 km at most instead of 600 km —
    /// which is a five-hundred-fold gain in resolution. It is not a workaround, it is the same formula
    /// evaluated in an order that does not cancel out its own significant digits.
    ///
    /// Off by default: it patches stock terrain generation, so a mistake here means holes in the ground.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.PSystemSpawn, true)]
    public class PqsQuadPrecisionFix : MonoBehaviour
    {
        private static readonly ModLogger LOGGER = new ModLogger("PqsFix");

        private const string HarmonyId = "com.github.lhervier.ksp.evacmgroundmod.pqs";

        /// <summary>
        /// Whether the patches do anything. They are installed either way and return the stock behaviour
        /// when this is off, so the setting can be read after they are in place, and flipped at any time:
        /// quads are built in flight, long after startup.
        /// </summary>
        public static bool Enabled = false;

        /// <summary>
        /// Largest correction the fix will apply, in meters. The rounding it removes is a few centimeters;
        /// anything beyond a meter means the double frame this relies on (PQS.transformPosition and
        /// transformRotation) does not describe the transform hierarchy the quads actually hang from, and
        /// the safe answer is then to leave KSP alone rather than to move the ground somewhere else.
        /// </summary>
        private const double MaxCorrection = 1.0;

        // Reading the same private fields the stock method reads. Bound once: this runs for every vertex
        // of every quad, so a Traverse lookup per call would be paid hundreds of times per frame.
        private static AccessTools.FieldRef<PQS, PQ> _buildQuad;
        private static AccessTools.FieldRef<PQS, int> _vertexIndex;
        private static AccessTools.FieldRef<PQ, Vector3d> _precisePosition;

        // Logged once rather than per vertex: a guard that trips does so for every vertex of every quad.
        private static bool _guardReported;

        private void Start()
        {
            try
            {
                _buildQuad = AccessTools.FieldRefAccess<PQS, PQ>("buildQuad");
                _vertexIndex = AccessTools.FieldRefAccess<PQS, int>("vertexIndex");
                _precisePosition = AccessTools.FieldRefAccess<PQ, Vector3d>("PrecisePosition");

                Harmony harmony = new Harmony(HarmonyId);
                harmony.PatchAll(typeof(PqsQuadPrecisionFix).Assembly);
                LOGGER.LogInfo($"PQS quad precision patches installed (enabled: {Enabled})");
            }
            catch (Exception e)
            {
                // A mod that cannot patch must not take the game down with it: the terrain then behaves
                // exactly as it does without this mod.
                Enabled = false;
                LOGGER.LogError($"Could not install the PQS precision patches, leaving KSP alone: {e}");
            }
        }

        // The body a sphere belongs to, remembered between calls: quads of one sphere are built in bursts,
        // so a single slot spares a scan of FlightGlobals.Bodies per vertex.
        private static PQS _lastSphere;
        private static CelestialBody _lastBody;

        /// <summary>Whether the fix should act on this sphere right now.</summary>
        private static bool AppliesTo(PQS sphere)
        {
            // surfaceRelativeQuads picks which of the two vertex placements KSP uses; only the surface
            // relative one goes through the Transforms this corrects.
            return Enabled && sphere != null && sphere.surfaceRelativeQuads;
        }

        /// <summary>The body whose terrain this sphere draws, or null when it cannot be resolved.</summary>
        private static CelestialBody BodyOf(PQS sphere)
        {
            if (ReferenceEquals(sphere, _lastSphere))
            {
                return _lastBody;
            }
            if (FlightGlobals.Bodies == null)
            {
                return null;
            }
            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                CelestialBody candidate = FlightGlobals.Bodies[i];
                if (candidate != null && ReferenceEquals(candidate.pqsController, sphere))
                {
                    _lastSphere = sphere;
                    _lastBody = candidate;
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>
        /// The world position of a point given in the sphere's frame, in double, or false when this cannot
        /// be done without losing more precision than it saves.
        ///
        /// Not PQS.GetWorldPosition: measured, that one misses by 750 km on the body being flown over. It
        /// reads transformRotation, which CelestialBody.updateRotation only maintains for bodies that are
        /// NOT the dominant one — the dominant body's transform is left still and the world frame turns
        /// around it instead, so its stored rotation goes stale while the quads keep following the
        /// transform. The quads hang off bodyTransform, so that is the frame to work in.
        /// </summary>
        private static bool TryWorldPosition(CelestialBody body, Vector3d sphereRelative, out Vector3d world)
        {
            world = Vector3d.zero;
            Transform bodyTransform = body.bodyTransform;
            if (bodyTransform == null)
            {
                return false;
            }

            // body.rotation is bodyTransform.rotation in double — measured, they agree to 3e-8 per
            // component, which is a float's own quantization step. Using the double one matters: applied
            // to a 600 km vector, the float quaternion is worth 36 mm of error on its own, the very size
            // of the defect being corrected. Same story for body.position against bodyTransform.position,
            // quantized to 62.5 mm at this distance — and body.position is the double the whole game
            // positions vessels from, so this puts the terrain in the frame of what stands on it.
            world = body.rotation * sphereRelative + body.position;
            return true;
        }

        /// <summary>
        /// Whether <paramref name="corrected"/> is close enough to where KSP put the quad to be a rounding
        /// correction rather than a different place altogether. Reports the first refusal and then stays
        /// quiet.
        /// </summary>
        private static bool IsSaneCorrection(Vector3d corrected, Vector3 current, string what)
        {
            double distance = (corrected - (Vector3d)current).magnitude;
            if (distance <= MaxCorrection)
            {
                return true;
            }
            if (!_guardReported)
            {
                _guardReported = true;
                LOGGER.LogError($"PQS precision fix disengaged on {what}: correction of {distance:0.000} m"
                    + " is too large to be a rounding error — the double frame does not match the quad"
                    + " hierarchy. Leaving the terrain as KSP builds it.");
            }
            return false;
        }

        // ==========================================================================
        // Where a vertex goes inside its quad
        // ==========================================================================

        [HarmonyPatch(typeof(PQS), "BuildVertexSurfaceRelative")]
        private static class BuildVertexSurfaceRelativePatch
        {
            private static bool Prefix(PQS __instance, PQS.VertexBuildData data)
            {
                if (!AppliesTo(__instance))
                {
                    return true;
                }

                PQ quad = _buildQuad(__instance);
                if (quad == null || quad.verts == null || PQS.verts == null)
                {
                    return true;
                }

                int index = _vertexIndex(__instance);
                if (index < 0 || index >= quad.verts.Length || index >= PQS.verts.Length)
                {
                    return true;
                }

                // Same admissibility test as the quad placement below, and for the same reason: the
                // offset computed here is expressed in the sphere's frame, which is only the quad's frame
                // when the body transform does not rotate it.
                CelestialBody body = BodyOf(__instance);
                Vector3d worldOrigin;
                if (body == null || !TryWorldPosition(body, quad.positionPlanet, out worldOrigin)
                    || !IsSaneCorrection(worldOrigin, quad.transform.position, "a quad's vertices"))
                {
                    return true;
                }

                Vector3d vertRel = data.directionFromCenter * data.vertHeight;

                // The sphere relative vertex, which the normals are rebuilt from, is what stock stores
                // here too — it is already a double and needs nothing.
                PQS.verts[index] = vertRel;

                // The whole point, and it never leaves the sphere's frame: both the vertex and its quad's
                // origin are doubles there, and their difference — at most the width of a quad, 1.7 km —
                // is the only thing a float ever sees. Stock instead converts each of them to float at
                // 600 km, where the quantization step is 62.5 mm, and subtracts afterwards.
                Vector3d offsetInQuad = vertRel - quad.positionPlanet;

                // Into world orientation in double, then into the quad's own frame. The float quaternion
                // of the quad's transform is only used on a vector at most a quad wide, where its
                // quantization is worth a tenth of a millimeter — unlike the 600 km vectors stock feeds
                // to the very same kind of conversion.
                Vector3d worldOffset = body.rotation * offsetInQuad;
                quad.verts[index] = Quaternion.Inverse(quad.transform.rotation) * (Vector3)worldOffset;
                return false;
            }
        }

        // ==========================================================================
        // Where the quad itself goes
        // ==========================================================================

        /// <summary>
        /// Places a quad at the world position its double origin says, rather than at the one a 600 km
        /// float local position lands on. Runs after both stock placements, which set the same field.
        /// </summary>
        private static void PlaceQuad(PQ quad)
        {
            PQS sphere = quad != null ? quad.sphereRoot : null;
            if (!AppliesTo(sphere) || quad.transform == null)
            {
                return;
            }

            CelestialBody body = BodyOf(sphere);
            Vector3d world;
            if (body == null || !TryWorldPosition(body, quad.positionPlanet, out world)
                || !IsSaneCorrection(world, quad.transform.position, "a quad's origin"))
            {
                return;
            }

            quad.transform.position = (Vector3)world;
            _precisePosition(quad) = world;
        }

        [HarmonyPatch(typeof(PQ), "SetupQuad")]
        private static class SetupQuadPatch
        {
            private static void Postfix(PQ __instance)
            {
                PlaceQuad(__instance);
            }
        }

        [HarmonyPatch(typeof(PQ), "PreciseUpdateSubQuadsPosition")]
        private static class PreciseUpdateSubQuadsPositionPatch
        {
            private static void Postfix(PQ __instance)
            {
                PlaceQuad(__instance);
            }
        }
    }
}
