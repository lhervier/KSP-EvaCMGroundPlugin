using UnityEngine;
using com.github.lhervier.ksp.shared;
using com.github.lhervier.ksp.evacmgroundmod.settings;

namespace com.github.lhervier.ksp.evacmgroundmod
{
    /// <summary>
    /// One-shot startup addon: applies the persisted settings (log level, ground offset), once for the
    /// whole KSP session.
    ///
    /// It exists because both have to be in place before the fix runs, while the settings window — where
    /// the player changes them — only reaches the scenes the toolbar button lives in. Until it is opened,
    /// nothing else would have read the file.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.PSystemSpawn, true)]
    public class Mod : MonoBehaviour
    {
        private static readonly ModLogger LOGGER = new ModLogger("Mod");

        private void Start()
        {
            EvaCMGroundSettings settings = new EvaCMGroundSettings();
            settings.Load();
            ModLogger.SetLogLevel(settings.LogLevel);
            EvaCMGroundMod.GroundOffset = settings.GroundOffset;
            LOGGER.LogInfo($"Log level set to {settings.LogLevel}, ground offset set to {settings.GroundOffset} m");
        }
    }
}
