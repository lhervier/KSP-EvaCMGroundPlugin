using System;
using System.IO;
using System.Reflection;
using com.github.lhervier.ksp.shared;

namespace com.github.lhervier.ksp.evacmgroundmod.settings
{
    /// <summary>
    /// The mod's own settings, per INSTALL rather than per save — they describe how the player wants the
    /// mod to behave, and nothing here belongs to a game.
    ///
    /// Stored in PluginData/ next to the DLL: GameDatabase skips that folder, so KSP never tries to read
    /// this file as a part config. This replaces the eva_cm_ground.cfg that used to sit beside the DLL and
    /// was parsed by hand.
    /// </summary>
    public class EvaCMGroundSettings
    {
        private static readonly ModLogger LOGGER = new ModLogger("Settings");

        private const string ConfigFile = "settings.cfg";
        private const string GeneralNode = "GENERAL";

        /// <summary>Smallest ground offset the UI offers (m).</summary>
        public const float GroundOffsetMin = 0f;

        /// <summary>Largest ground offset the UI offers (m).</summary>
        public const float GroundOffsetMax = 0.1f;

        /// <summary>Default ground offset (m), i.e. the value the mod shipped with.</summary>
        public const float GroundOffsetDefault = 0.01f;

        /// <summary>
        /// Default for <see cref="KeepAnchoredBaseGroundPosition"/>: on, like the placement fix. The bug
        /// it addresses has no upside, so a fresh install is protected without the player having to find
        /// the setting; unchecking it is the escape hatch, not the starting point.
        /// </summary>
        public const bool KeepAnchoredBaseGroundPositionDefault = true;

        private readonly string _path;

        /// <summary>How verbose the mod's logging is.</summary>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// How far down the collision test volume is dropped before looking for the ground, in meters.
        /// The bigger it is, the earlier a part is declared to be in the ground.
        /// </summary>
        public float GroundOffset { get; set; }

        /// <summary>
        /// Whether a landed base built on a ground anchor is kept at the altitude it was saved at, rather
        /// than being put back down on the terrain by KSP at every load.
        /// </summary>
        public bool KeepAnchoredBaseGroundPosition { get; set; }

        public EvaCMGroundSettings()
        {
            string dll = Assembly.GetExecutingAssembly().Location;
            _path = Path.Combine(Path.GetDirectoryName(dll), Path.Combine("PluginData", ConfigFile));
            Reset();
        }

        private void Reset()
        {
            // Same default as the sibling mods: what the player is meant to see in KSP.log, no more.
            LogLevel = LogLevel.Info;
            GroundOffset = GroundOffsetDefault;
            KeepAnchoredBaseGroundPosition = KeepAnchoredBaseGroundPositionDefault;
        }

        /// <summary>Reads the settings file, falling back on the defaults when it is missing or unreadable.</summary>
        public void Load()
        {
            Reset();
            if (!File.Exists(_path))
            {
                LOGGER.LogInfo("No settings file yet, using the defaults");
                return;
            }

            ConfigNode root = ConfigNode.Load(_path);
            ConfigNode general = root?.GetNode(GeneralNode);
            if (general == null)
            {
                LOGGER.LogWarning($"Unusable settings file '{_path}', using the defaults");
                return;
            }

            string logLevel = LogLevel.ToString();
            general.TryGetValue("logLevel", ref logLevel);
            // An unknown level (hand-edited file, or a level dropped from the enum) keeps the default
            // rather than silencing the log.
            LogLevel parsed;
            if (Enum.TryParse(logLevel, out parsed) && Enum.IsDefined(typeof(LogLevel), parsed))
            {
                LogLevel = parsed;
            }
            else
            {
                LOGGER.LogWarning($"Unknown log level '{logLevel}', keeping {LogLevel}");
            }

            float groundOffset = GroundOffset;
            general.TryGetValue("groundOffset", ref groundOffset);
            // Clamped rather than refused: a hand-edited file is worth honoring, but a negative offset
            // would lift the test volume off the ground and a huge one would refuse every position.
            GroundOffset = UnityEngine.Mathf.Clamp(groundOffset, GroundOffsetMin, GroundOffsetMax);

            bool keepAnchoredBase = KeepAnchoredBaseGroundPosition;
            general.TryGetValue("keepAnchoredBaseGroundPosition", ref keepAnchoredBase);
            KeepAnchoredBaseGroundPosition = keepAnchoredBase;

            LOGGER.LogInfo($"Settings loaded from {_path} (logLevel={LogLevel}, groundOffset={GroundOffset}"
                + $", keepAnchoredBaseGroundPosition={KeepAnchoredBaseGroundPosition})");
        }

        /// <summary>Writes the settings file, creating PluginData/ on first save.</summary>
        public void Save()
        {
            try
            {
                ConfigNode root = new ConfigNode();
                ConfigNode general = root.AddNode(GeneralNode);
                general.AddValue("logLevel", LogLevel.ToString());
                general.AddValue("groundOffset", GroundOffset);
                general.AddValue("keepAnchoredBaseGroundPosition", KeepAnchoredBaseGroundPosition);

                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                root.Save(_path);
            }
            catch (Exception e)
            {
                // A setting that fails to persist is not worth interrupting a flight for.
                LOGGER.LogError($"Could not save the settings to '{_path}': {e.Message}");
            }
        }
    }
}
