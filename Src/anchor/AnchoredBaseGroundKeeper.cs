using UnityEngine;
using com.github.lhervier.ksp.shared;

namespace com.github.lhervier.ksp.evacmgroundmod.anchor
{
    /// <summary>
    /// Keeps a landed base built on a ground anchor at the altitude it was saved at, instead of letting
    /// KSP put it back down on the terrain every time it loads — a pass that lifts the anchor's spikes
    /// out of the ground, welds them there, and so raises the base a little more at each cycle.
    ///
    /// Touches no vessel other than an anchored base saved with uninitialized PQS levels, and nothing at
    /// all while <see cref="Enabled"/> is off.
    ///
    /// Independent of the placement fix on purpose — its own addon, its own setting, no shared state —
    /// so it can be lifted into a mod of its own without untangling anything.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.PSystemSpawn, true)]
    public class AnchoredBaseGroundKeeper : MonoBehaviour
    {
        private static readonly ModLogger LOGGER = new ModLogger("AnchorKeeper");

        // The field ModuleGroundPart persists once the part has been riveted to the ground.
        private const string DeployedField = "deployedOnGround";

        /// <summary>
        /// Whether loaded vessels are protected. Owned by the settings, which apply the stored value at
        /// startup — before any vessel can load — and the shipped default is on.
        /// </summary>
        public static bool Enabled = true;

        private static AnchoredBaseGroundKeeper _instance;

        private void Start()
        {
            // KSPAddon(..., once: true) builds this object a single time, but nothing promises it survives
            // a scene change — and the subscription below has to outlive every scene, since a vessel load
            // is exactly what it listens for. Hence DontDestroyOnLoad, and a guard in case a second
            // instance is ever built: only the first one holds the subscription.
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            GameEvents.onProtoVesselLoad.Add(OnProtoVesselLoad);
            LOGGER.LogInfo("Watching vessel loads");
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }
            GameEvents.onProtoVesselLoad.Remove(OnProtoVesselLoad);
            _instance = null;
        }

        // ==========================================================================
        // The fix
        // ==========================================================================

        // An instance method even though it only touches statics: EventData.Add builds an EvtDelegate
        // whose constructor dereferences the delegate's target, so a static handler throws inside Add.
        private void OnProtoVesselLoad(GameEvents.FromToAction<ProtoVessel, ConfigNode> action)
        {
            if (!Enabled)
            {
                return;
            }

            // The same event is fired from the ProtoVessel(ConfigNode, Game) constructor, before a single
            // value has been parsed; that firing carries the config node. The one worth acting on, at the
            // very top of ProtoVessel.Load, carries null — the proto vessel is then fully parsed and no
            // Vessel exists yet, which is what makes this immune to load-order surprises.
            if (action.to != null)
            {
                return;
            }

            ProtoVessel proto = action.from;
            if (proto == null || !NeedsProtection(proto))
            {
                return;
            }

            CelestialBody body = ReferenceBodyOf(proto);
            PQS pqs = body != null ? body.pqsController : null;
            if (pqs == null)
            {
                return;
            }

            // Vessel.GoOffRails skips its ground positioning pass for a landed vessel whose stored PQS
            // levels match the live controller. Writing them is not a workaround: it is the very state a
            // healthy vessel is in, and CheckGroundCollision's own first line is to return on that test.
            proto.PQSminLevel = pqs.minLevel;
            proto.PQSmaxLevel = pqs.maxLevel;

            // A one-part vessel is positioned whatever its PQS levels say, unless this flag states it was
            // dropped where it stands on purpose. A lone anchor is exactly that case.
            if (proto.protoPartSnapshots.Count == 1)
            {
                proto.skipGroundPositioningForDroppedPart = true;
            }

            LOGGER.LogInfo(
                $"'{proto.vesselName}' is an anchored base saved without PQS levels: keeping it at "
                + $"alt={proto.altitude:0.000} m rather than letting KSP put it back on the terrain "
                + $"(levels set to {pqs.minLevel}/{pqs.maxLevel})");
        }

        /// <summary>
        /// Whether this vessel is a landed, anchored base that KSP would put back down on the terrain at
        /// load, for the single reason this fix addresses.
        /// </summary>
        private static bool NeedsProtection(ProtoVessel proto)
        {
            if (!proto.landed || proto.splashed)
            {
                return false;
            }

            // A vessel on the pad is landed too, and there the positioning pass is stock behaviour the
            // launch relies on.
            if (proto.situation == Vessel.Situations.PRELAUNCH)
            {
                return false;
            }

            // KSP positions a spawning vessel whatever its levels say, and for a ground part that has just
            // been dropped that pass IS how it gets seated. The first placement is left alone.
            if (proto.vesselSpawning)
            {
                return false;
            }

            // Deliberately narrow: only levels that were never initialized at all, which is the EVA drop
            // path writing PQSMin = PQSMax = 0 into the vessel it creates (EVAConstructionModeEditor
            // .GetProtoVesselNode). Levels that are merely stale mean the terrain detail setting really
            // did change, and there KSP's pass has a reason to exist.
            if (proto.PQSminLevel != 0 && proto.PQSmaxLevel != 0)
            {
                return false;
            }

            return IsRivetedToTheGround(proto);
        }

        /// <summary>Whether any part of the vessel is a ground part that has been riveted down.</summary>
        private static bool IsRivetedToTheGround(ProtoVessel proto)
        {
            for (int i = 0; i < proto.protoPartSnapshots.Count; i++)
            {
                ProtoPartSnapshot part = proto.protoPartSnapshots[i];
                if (part == null || part.modules == null)
                {
                    continue;
                }
                for (int j = 0; j < part.modules.Count; j++)
                {
                    ConfigNode values = part.modules[j].moduleValues;
                    if (values == null)
                    {
                        continue;
                    }
                    // Looked up by field name rather than by module name: ModuleGroundPart persists it, and
                    // so does anything deriving from it, without this having to know the class names. It
                    // also answers from the save alone, with nothing instantiated.
                    bool deployed = false;
                    if (values.TryGetValue(DeployedField, ref deployed) && deployed)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>The body the vessel is landed on, or null when it cannot be resolved.</summary>
        private static CelestialBody ReferenceBodyOf(ProtoVessel proto)
        {
            OrbitSnapshot orbit = proto.orbitSnapShot;
            if (orbit == null || FlightGlobals.Bodies == null)
            {
                return null;
            }
            int index = orbit.ReferenceBodyIndex;
            if (index < 0 || index >= FlightGlobals.Bodies.Count)
            {
                return null;
            }
            return FlightGlobals.Bodies[index];
        }
    }
}
