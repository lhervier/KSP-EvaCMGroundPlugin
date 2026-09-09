using System.Collections;
using UnityEngine;
using com.github.lhervier.ksp.shared;

namespace com.github.lhervier.ksp.evacmgroundmod.anchor
{
    /// <summary>
    /// Diagnostic probe: records where the ground is, under every loaded anchored vessel, measured two
    /// independent ways — the PQS analytic height, and the collision surface a raycast actually hits.
    /// Comparing the two across several loads of the same save says whether a ground level that differs
    /// from one load to the next comes from the collision mesh or from the terrain as a whole.
    ///
    /// It samples three points of the same quad rather than one, so that a mesh translated as a whole can
    /// be told apart from a mesh deformed vertex by vertex, and it records the state the build of that
    /// mesh depends on: the quad's own origin, the triangle hit, and the azimuth of the world frame.
    ///
    /// Reads the world and writes to the log, nothing else: it moves no vessel and touches no setting.
    /// Runs at the Trace log level only, so the shipped default (Info) leaves it silent.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class GroundLevelProbe : MonoBehaviour
    {
        private static readonly ModLogger LOGGER = new ModLogger("GroundProbe");

        // Local Scenery, the layer the terrain collider lives on and the one Vessel.GetHeightFromTerrain
        // queries. Buildings and scatters share it; the hit object is named in the log so a hit on
        // something other than a terrain quad is recognizable.
        private const int LocalSceneryMask = 1 << 15;

        // The ray starts above the vessel rather than at it: a vessel sitting below the surface would
        // otherwise report no ground at all, which is precisely one of the states worth seeing.
        private const float RayStartHeight = 20f;
        private const float RayLength = 220f;

        // Distance of the two extra sample points, along two tangents. Small enough to stay on the same
        // quad — the smallest ones are about 1.2 km across — and large enough to span several of its
        // triangles, which are roughly 80 to 170 m wide.
        private const float TangentOffset = 100f;

        // Two samples are enough: measurement has shown the ground level is settled by the time the scene
        // starts and never moves afterwards — same value at t+1s, +5s, +15s, +30s and 27 minutes later.
        private static readonly float[] SampleTimes = { 0f, 5f };

        /// <summary>
        /// Environment variable that turns the probe on whatever the log level is. The log level lives in
        /// PluginData/settings.cfg, which a reinstall can lose; an environment variable belongs to the
        /// machine, so a measurement campaign survives redeploying the mod between two runs.
        /// </summary>
        private const string EnableVariable = "EVACM_GROUND_PROBE";

        private void Start()
        {
            if (!IsRequested())
            {
                Destroy(this);
                return;
            }
            LOGGER.LogInfo($"Ground probe active (Trace={LOGGER.IsTraceEnabled},"
                + $" {EnableVariable}={System.Environment.GetEnvironmentVariable(EnableVariable) ?? "<unset>"})");
            GameEvents.onVesselGoOffRails.Add(OnVesselGoOffRails);
            StartCoroutine(SampleOverTime());
        }

        /// <summary>Whether the probe should run: Trace log level, or the environment variable set.</summary>
        private static bool IsRequested()
        {
            if (LOGGER.IsTraceEnabled)
            {
                return true;
            }
            string requested = System.Environment.GetEnvironmentVariable(EnableVariable);
            return !string.IsNullOrEmpty(requested)
                && (requested == "1" || requested.ToLowerInvariant() == "true");
        }

        private void OnDestroy()
        {
            GameEvents.onVesselGoOffRails.Remove(OnVesselGoOffRails);
        }

        // An instance method even though it only touches statics: EventData.Add dereferences the
        // delegate's target, so a static handler throws inside Add.
        private void OnVesselGoOffRails(Vessel vessel)
        {
            if (IsAnchored(vessel))
            {
                // The one sample that is not on the clock: it is taken in the very frame the vessel is
                // unpacked, before a single physics step has run, so it shows the ground the vessel is
                // handed rather than the one it ends up on.
                Probe(vessel, "off-rails");
            }
        }

        private IEnumerator SampleOverTime()
        {
            float start = Time.realtimeSinceStartup;
            for (int i = 0; i < SampleTimes.Length; i++)
            {
                while (Time.realtimeSinceStartup - start < SampleTimes[i])
                {
                    yield return null;
                }
                ProbeAll($"t+{SampleTimes[i]:0}s");
            }
        }

        private static void ProbeAll(string label)
        {
            if (FlightGlobals.VesselsLoaded == null)
            {
                return;
            }
            for (int i = 0; i < FlightGlobals.VesselsLoaded.Count; i++)
            {
                Vessel vessel = FlightGlobals.VesselsLoaded[i];
                if (IsAnchored(vessel))
                {
                    Probe(vessel, label);
                }
            }
        }

        /// <summary>Whether this vessel is a loaded, landed vessel carrying a ground anchor.</summary>
        private static bool IsAnchored(Vessel vessel)
        {
            return vessel != null
                && vessel.loaded
                && vessel.LandedOrSplashed
                && vessel.FindPartModuleImplementing<ModuleGroundPart>() != null;
        }

        /// <summary>
        /// Writes the log lines describing the ground under <paramref name="vessel"/>: one header line
        /// carrying the state the quad's mesh was built from, then one line per sample point — the
        /// vessel's own position and two points a hundred meters away along each tangent.
        ///
        /// Every height is an altitude above sea level, never a world coordinate: the floating origin
        /// moves between loads, altitudes do not, so they compare from one session to the next.
        /// </summary>
        private static void Probe(Vessel vessel, string label)
        {
            CelestialBody body = vessel.mainBody;
            PQS pqs = body != null ? body.pqsController : null;
            if (pqs == null)
            {
                return;
            }

            // The azimuth of the world frame, which the float rounding in PQS.BuildVertexSurfaceRelative
            // depends on, and which survives a scene change through the Planetarium singleton.
            LOGGER.LogInfo($"[{label}] '{vessel.vesselName}' surfaceRelativeQuads={pqs.surfaceRelativeQuads}"
                + $" lvl={pqs.minLevel}/{pqs.maxLevel} (tgtSpeed={pqs.maxLevelAtCurrentTgtSpeed})"
                + $" | invRot={Planetarium.InverseRotAngle:0.000000} rotAngle={body.rotationAngle:0.000000}"
                + $" directRot={body.directRotAngle:0.000000}");

            Vector3 up = FlightGlobals.getUpAxis(body, vessel.transform.position);
            Vector3 polar = body.bodyTransform != null ? body.bodyTransform.up : Vector3.up;
            Vector3 east = Vector3.Cross(polar, up);
            if (east.sqrMagnitude < 1e-6f)
            {
                east = Vector3.Cross(Vector3.right, up);
            }
            east.Normalize();
            Vector3 north = Vector3.Cross(up, east).normalized;

            Vector3 center = vessel.transform.position;
            ProbePoint(body, pqs, label, vessel.vesselName, "center  ", center, up, vessel);
            ProbePoint(body, pqs, label, vessel.vesselName, "east+100", center + east * TangentOffset, up, null);
            ProbePoint(body, pqs, label, vessel.vesselName, "north100", center + north * TangentOffset, up, null);
        }

        /// <summary>
        /// Samples the ground at one point: the analytic height, the collision surface, the triangle and
        /// quad hit, and — the measurement that tells a translated mesh from a deformed one — the quad's
        /// own origin, compared between the double value it is computed from and the float position its
        /// transform ended up at.
        ///
        /// <paramref name="vessel"/> is only used to report how far the vessel sits above the surface,
        /// and may be null for the points that are not under it.
        /// </summary>
        private static void ProbePoint(CelestialBody body, PQS pqs, string label, string vesselName,
            string role, Vector3 point, Vector3 up, Vessel vessel)
        {
            // Through latitude/longitude, which looks like a detour but is the only correct route:
            // PQS.GetRelativePosition returns a vector in the sphere's own frame, which the planet's
            // rotation separates from the body frame GetSurfaceHeight samples. Feeding it that vector
            // reads the terrain at a different longitude — measured, it was off by about a kilometer.
            double altPqs = body.TerrainAltitude(body.GetLatitude(point), body.GetLongitude(point),
                allowNegative: true);

            RaycastHit hit;
            if (!Physics.Raycast(point + up * RayStartHeight, -up, out hit,
                    RayLength, LocalSceneryMask, QueryTriggerInteraction.Ignore))
            {
                LOGGER.LogInfo($"[{label}] '{vesselName}' {role} pqs={altPqs:0.0000}"
                    + " collider=<nothing under this point>");
                return;
            }

            double altCollider = body.GetAltitude(hit.point);
            string vesselPart = vessel != null
                ? $" | vessel-collider={(body.GetAltitude(vessel.transform.position) - altCollider) * 1000.0:+0.00;-0.00} mm"
                : "";

            PQ quad = hit.collider.GetComponentInParent<PQ>();
            string quadPart;
            if (quad != null)
            {
                // The quad's origin, the two ways it exists: positionPlanet is the double it is computed
                // from — analytic, so it should never move — while the transform holds what survived the
                // float conversion. A gap that changes between loads is the mesh being placed elsewhere
                // as a whole; a stable gap with a moving surface is the mesh itself being deformed.
                double originAnalytic = quad.positionPlanet.magnitude - pqs.radius;
                double originActual = body.GetAltitude(quad.transform.position);

                // What the precision fix would place this quad at, computed exactly the way it does: in
                // the body transform's frame, whose rotation is identity for the body being flown over,
                // and from body.position — the double KSP positions vessels from — rather than from its
                // float shadow. Reported whether the fix is on or off, so "would move" says in advance
                // how far the correction moves the quad; a few centimeters means the frame is right, and
                // hundreds of kilometers means it is not.
                Vector3d worldFromDouble = body.rotation * quad.positionPlanet + body.position;
                double originFixed = body.GetAltitude(worldFromDouble);
                double moved = (worldFromDouble - (Vector3d)quad.transform.position).magnitude;

                quadPart = $" | quad='{quad.name}' subdiv={quad.subdivision} tri={hit.triangleIndex}"
                    + $" origin: analytic={originAnalytic:0.0000} actual={originActual:0.0000}"
                    + $" (diff={(originActual - originAnalytic) * 1000.0:+0.00;-0.00} mm)"
                    + $" fixed={originFixed:0.0000}"
                    + $" (vs analytic={(originFixed - originAnalytic) * 1000.0:+0.00;-0.00} mm,"
                    + $" would move {moved * 1000.0:0.00} mm)";

                // Only under the vessel: which frame the quads actually hang from is a property of the
                // sphere, not of the sample point, and the answer is three lines long.
                if (vessel != null)
                {
                    ProbeFrames(pqs, body, quad, label, vesselName);
                }
            }
            else
            {
                quadPart = $" | object='{hit.collider.gameObject.name}' tri={hit.triangleIndex}";
            }

            LOGGER.LogInfo($"[{label}] '{vesselName}' {role} pqs={altPqs:0.0000} collider={altCollider:0.0000}"
                + $" | collider-pqs={(altCollider - altPqs) * 1000.0:+0.00;-0.00} mm{vesselPart}{quadPart}");
        }

        /// <summary>
        /// Reports which frame the quads are actually placed in, by pitting every candidate transform
        /// against the one measurable truth — where the quad's transform really is. Whichever candidate
        /// lands within centimeters is the frame a precision fix has to work in; a wrong one misses by
        /// hundreds of kilometers, which is how the first attempt was caught before it was ever enabled.
        /// </summary>
        private static void ProbeFrames(PQS pqs, CelestialBody body, PQ quad, string label, string vesselName)
        {
            Transform quadTransform = quad.transform;
            Vector3d truth = quadTransform.position;
            Transform parent = quadTransform.parent;
            Vector3d planetPos = quad.positionPlanet;

            // The one number that says whether the quad's local frame is still the frame positionPlanet is
            // expressed in: a few centimeters means yes (that is the float rounding being hunted), while
            // anything larger means reparenting moved the goalposts.
            double localGap = (planetPos - (Vector3d)quadTransform.localPosition).magnitude;

            LOGGER.LogInfo($"[{label}] '{vesselName}' frames: parent='{(parent != null ? parent.name : "<none>")}'"
                + $" | positionPlanet vs localPosition={localGap * 1000.0:0.00} mm"
                + $" | quad.position={Fmt(truth)}"
                + $" | sphere.position={Fmt(pqs.transform.position)}"
                + $" pqs.transformPosition={Fmt(pqs.transformPosition)}"
                + $" body.position={Fmt(body.position)}");

            // The rotation everything hinges on, component by component: if it turns out to be an exact
            // quarter turn — KSP swizzles between its own Z-up frame and Unity's Y-up one — it can be
            // rebuilt in double and applied to a 600 km vector for free. A rotation only known as a float
            // quaternion cannot: its own quantization already costs centimeters at that distance.
            Transform bodyTransform = body.bodyTransform;
            if (bodyTransform != null)
            {
                Quaternion rotation = bodyTransform.rotation;
                LOGGER.LogInfo($"[{label}] '{vesselName}' bodyTransform.rotation="
                    + $"({rotation.x:0.00000000},{rotation.y:0.00000000},{rotation.z:0.00000000},{rotation.w:0.00000000})"
                    + $" | sphere.rotation="
                    + $"({pqs.transform.rotation.x:0.00000000},{pqs.transform.rotation.y:0.00000000}"
                    + $",{pqs.transform.rotation.z:0.00000000},{pqs.transform.rotation.w:0.00000000})"
                    + $" | positionPlanet={Fmt(planetPos)}");

                LogFrameCandidate(label, vesselName, "rot*planet+body.pos  ",
                    (Vector3d)(rotation * (Vector3)planetPos) + body.position, truth);

                // The float rotation above is only known to about 6e-8 per component, which is already
                // 40 mm once applied to a 600 km vector — the very size of the error being chased, so it
                // cannot be the fix. These are the double rotations KSP keeps: whichever one matches the
                // float quaternion component for component IS that rotation, known exactly, and can
                // replace it. That is a comparison of rotations, not of positions, so the quad's own
                // rounding does not muddy it.
                QuaternionD planetarium = Planetarium.Rotation;
                QuaternionD bodyRotation = body.rotation;
                QuaternionD frame = body.BodyFrame.Rotation;
                LOGGER.LogInfo($"[{label}] '{vesselName}' double rotations:"
                    + $" Planetarium.Rotation=({planetarium.x:0.00000000},{planetarium.y:0.00000000}"
                    + $",{planetarium.z:0.00000000},{planetarium.w:0.00000000})"
                    + $" | body.rotation=({bodyRotation.x:0.00000000},{bodyRotation.y:0.00000000}"
                    + $",{bodyRotation.z:0.00000000},{bodyRotation.w:0.00000000})"
                    + $" | BodyFrame=({frame.x:0.00000000},{frame.y:0.00000000}"
                    + $",{frame.z:0.00000000},{frame.w:0.00000000})");

                LogFrameCandidate(label, vesselName, "Planetarium.Rotation ",
                    planetarium * planetPos + body.position, truth);
                LogFrameCandidate(label, vesselName, "Inverse(Planetarium) ",
                    QuaternionD.Inverse(planetarium) * planetPos + body.position, truth);
                LogFrameCandidate(label, vesselName, "body.rotation        ",
                    bodyRotation * planetPos + body.position, truth);
            }

            LogFrameCandidate(label, vesselName, "pqs.GetWorldPosition ",
                pqs.GetWorldPosition(planetPos), truth);
            LogFrameCandidate(label, vesselName, "sphere.TransformPoint",
                pqs.transform.TransformPoint((Vector3)planetPos), truth);
            if (parent != null)
            {
                LogFrameCandidate(label, vesselName, "parent.TransformPoint",
                    parent.TransformPoint((Vector3)planetPos), truth);
            }
            if (body.bodyTransform != null)
            {
                LogFrameCandidate(label, vesselName, "body.TransformPoint  ",
                    body.bodyTransform.TransformPoint((Vector3)planetPos), truth);
            }
        }

        private static void LogFrameCandidate(string label, string vesselName, string name,
            Vector3d candidate, Vector3d truth)
        {
            double off = (candidate - truth).magnitude;
            LOGGER.LogInfo($"[{label}] '{vesselName}' frame {name}: off by {off * 1000.0:0.00} mm"
                + $" ({off / 1000.0:0.000} km)");
        }

        private static string Fmt(Vector3d v)
        {
            return $"({v.x:0.000},{v.y:0.000},{v.z:0.000})";
        }
    }
}
