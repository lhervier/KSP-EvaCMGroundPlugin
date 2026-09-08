using UnityEngine;
using com.github.lhervier.ksp.shared.ugui.styles;

namespace com.github.lhervier.ksp.evacmgroundmod.ui.styles
{
    /// <summary>
    /// Sizes and colors proper to the settings window. Anything already shared (paddings, label color,
    /// combo and slider metrics) comes from the KSP-Shared palettes instead: no builder of this mod
    /// hard-codes a dimension.
    /// </summary>
    public static class EvaCMGroundPalette
    {
        // ==============================================================
        // Main window
        // ==============================================================

        // Sized to its content rather than to a mockup: the log level combo, the ground offset field and
        // the sentence that explains it, then the anchored base checkbox and its own sentence. The width
        // is set by those sentences (three lines each at this size), the height by the rows stacked.
        public const float WindowWidth = 340f;
        public const float WindowHeight = 290f;

        // ==============================================================
        // Content
        // ==============================================================
        public const float ContentPaddingH = 10f;
        public const float ContentPaddingV = 10f;
        public const float ContentSpacing = 10f;

        // ==============================================================
        // Fields
        // ==============================================================
        public const int FieldLabelFontSize = 12;
        public static readonly Color FieldLabelColor = DefaultPalette.LabelColor;

        // The current value of a slider, one notch brighter than its label: it is the number the player
        // is dragging for.
        public const int FieldValueFontSize = 12;
        public static readonly Color FieldValueColor = DefaultPalette.AccentColor;

        // Explanatory sentence under a field. Smaller and dimmer than the labels: it is read once.
        public const int HintFontSize = 11;
        public static readonly Color HintColor = Utils.Rgb(136, 136, 136);

        // What the ground offset sentence needs at HintFontSize, wrapped to the window width: three
        // lines plus the leading. Reserved, not fitted (see ContentBuilder.BuildHint).
        public const float HintHeight = 48f;

        // Same, for the anchored base sentence: one line more, it has a consequence to spell out.
        public const float AnchorHintHeight = 64f;

        // Gap between a field's header row and its slider. Wider than the plain layout spacing on
        // purpose: the handle is as tall as the row above it, and without that air the two read as one
        // crowded block.
        public const float FieldSpacing = 8f;
    }
}
