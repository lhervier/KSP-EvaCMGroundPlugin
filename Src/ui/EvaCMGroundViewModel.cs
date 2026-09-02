using UnityEngine;
using com.github.lhervier.ksp.shared;
using com.github.lhervier.ksp.evacmgroundmod.settings;

namespace com.github.lhervier.ksp.evacmgroundmod.ui
{
    /// <summary>
    /// State of the settings window: plain properties that fire a KSP EventVoid on change, which the uGUI
    /// controllers subscribe to. Same shape as the sibling mods' view models.
    ///
    /// It is also the single place where a setting, its persisted copy and the fix's static switch are kept
    /// in step — the fix's switch has to be static (its addon is rebuilt at every scene change) while the
    /// setting outlives the session, so something has to own that pairing.
    /// </summary>
    public class EvaCMGroundViewModel : MonoBehaviour
    {
        private EvaCMGroundSettings _settings;

        /// <summary>Fired whenever <see cref="LogLevel"/> changes.</summary>
        public readonly EventVoid OnLogLevelChanged =
            new EventVoid("EvaCMGroundViewModel.OnLogLevelChanged");

        /// <summary>Fired whenever <see cref="GroundOffset"/> changes.</summary>
        public readonly EventVoid OnGroundOffsetChanged =
            new EventVoid("EvaCMGroundViewModel.OnGroundOffsetChanged");

        /// <summary>Binds the view model to the settings and applies them. Call once, right after AddComponent.</summary>
        public EvaCMGroundViewModel WithSettings(EvaCMGroundSettings settings)
        {
            _settings = settings;
            ModLogger.SetLogLevel(settings.LogLevel);
            EvaCMGroundMod.GroundOffset = settings.GroundOffset;
            return this;
        }

        /// <summary>How verbose the mod's logging is.</summary>
        public LogLevel LogLevel
        {
            get { return _settings != null ? _settings.LogLevel : LogLevel.Info; }
            set
            {
                if (_settings == null || _settings.LogLevel == value)
                {
                    return;
                }
                _settings.LogLevel = value;
                _settings.Save();
                ModLogger.SetLogLevel(value);
                OnLogLevelChanged.Fire();
            }
        }

        /// <summary>
        /// How far down the collision test volume is dropped before looking for the ground, in meters.
        /// Set values are snapped to the millimeter and clamped to what the settings allow, so a slider
        /// dragged anywhere in its track never persists a value the file would refuse on reload.
        /// </summary>
        public float GroundOffset
        {
            get { return _settings != null ? _settings.GroundOffset : EvaCMGroundSettings.GroundOffsetDefault; }
            set
            {
                if (_settings == null)
                {
                    return;
                }
                float snapped = Mathf.Round(
                    Mathf.Clamp(value, EvaCMGroundSettings.GroundOffsetMin, EvaCMGroundSettings.GroundOffsetMax) * 1000f) / 1000f;
                if (Mathf.Approximately(_settings.GroundOffset, snapped))
                {
                    return;
                }
                _settings.GroundOffset = snapped;
                _settings.Save();
                EvaCMGroundMod.GroundOffset = snapped;
                OnGroundOffsetChanged.Fire();
            }
        }
    }
}
