using UnityEngine;
using UnityEngine.UI;
using TMPro;
using com.github.lhervier.ksp.shared;
using com.github.lhervier.ksp.shared.ugui;
using com.github.lhervier.ksp.shared.ugui.checkbox;
using com.github.lhervier.ksp.shared.ugui.combo;
using com.github.lhervier.ksp.shared.ugui.slider;
using com.github.lhervier.ksp.evacmgroundmod.settings;
using com.github.lhervier.ksp.evacmgroundmod.ui.styles;

namespace com.github.lhervier.ksp.evacmgroundmod.ui.ugui
{
    /// <summary>
    /// Popup content (everything below the shared title bar): the log level combo, then the ground offset
    /// field and the sentence that explains it, then the anchored base checkbox and its own sentence.
    /// Mounted and stretched to fill the content host by the PopupBuilder, so it only fills what it is
    /// given.
    /// </summary>
    public class ContentBuilder : IUGUIBuilder<ContentController>
    {
        private EvaCMGroundViewModel _viewModel;
        public ContentBuilder WithViewModel(EvaCMGroundViewModel viewModel)
        {
            this._viewModel = viewModel;
            return this;
        }

        public ContentController Build()
        {
            var rootGo = new GameObject("EvaCMGround.Content", typeof(RectTransform));

            var layout = rootGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(
                Mathf.RoundToInt(EvaCMGroundPalette.ContentPaddingH),
                Mathf.RoundToInt(EvaCMGroundPalette.ContentPaddingH),
                Mathf.RoundToInt(EvaCMGroundPalette.ContentPaddingV),
                Mathf.RoundToInt(EvaCMGroundPalette.ContentPaddingV));
            layout.spacing = EvaCMGroundPalette.ContentSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Parented by the builder itself: the combo hands its floating dropdown and its click trap
            // that same parent, and only reparents them onto the canvas while open.
            ComboController logLevelCombo = new ComboBuilder()
                .WithParent(rootGo.transform)
                .WithLabel(ModLocalization.GetString("settingsLogLevel"))
                .WithLabelFor(LogLevels.LabelFor)
                .Build();

            SliderController groundOffsetSlider = BuildGroundOffsetField(
                rootGo.transform,
                out TextMeshProUGUI groundOffsetValue);

            BuildHint(rootGo.transform, "settingsGroundOffsetHint", EvaCMGroundPalette.HintHeight);

            // Greedy: the whole row toggles, the way a settings line is expected to behave.
            CheckboxController keepAnchoredBaseCheckbox = new CheckboxBuilder()
                .WithLabel(ModLocalization.GetString("settingsKeepAnchoredBase"))
                .WithGreedyState(true)
                .Build();
            keepAnchoredBaseCheckbox.transform.SetParent(rootGo.transform, false);

            BuildHint(rootGo.transform, "settingsKeepAnchoredBaseHint", EvaCMGroundPalette.AnchorHintHeight);

            return rootGo
                .AddComponent<ContentController>()
                .WithViewModel(_viewModel)
                .WithControls(logLevelCombo, groundOffsetSlider, groundOffsetValue, keepAnchoredBaseCheckbox);
        }

        // ----------------------------------------------------------------

        /// <summary>
        /// Builds the ground offset field: a header row (label + current value) and the slider below it.
        /// Returns the slider, and hands back the value label the controller keeps in sync.
        /// </summary>
        private static SliderController BuildGroundOffsetField(Transform parent, out TextMeshProUGUI valueLabel)
        {
            var fieldGo = new GameObject("GroundOffsetField", typeof(RectTransform));
            fieldGo.transform.SetParent(parent, false);
            var fieldLayout = fieldGo.AddComponent<VerticalLayoutGroup>();
            fieldLayout.padding = new RectOffset(0, 0, 0, 0);
            fieldLayout.spacing = EvaCMGroundPalette.FieldSpacing;
            fieldLayout.childAlignment = TextAnchor.UpperLeft;
            fieldLayout.childControlWidth = true;
            fieldLayout.childControlHeight = true;
            fieldLayout.childForceExpandWidth = true;
            fieldLayout.childForceExpandHeight = false;

            var headerGo = new GameObject("Header", typeof(RectTransform));
            headerGo.transform.SetParent(fieldGo.transform, false);
            var headerLayout = headerGo.AddComponent<HorizontalLayoutGroup>();
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(headerGo.transform, false);
            // Greedy on width so it eats the leftover space and pushes the value against the right edge.
            var labelLe = labelGo.AddComponent<LayoutElement>();
            labelLe.flexibleWidth = 1f;
            var label = UGUILabels.AddLabel(labelGo);
            label.text = ModLocalization.GetString("settingsGroundOffset");
            label.fontSize = EvaCMGroundPalette.FieldLabelFontSize;
            label.color = EvaCMGroundPalette.FieldLabelColor;
            label.alignment = TextAlignmentOptions.Left;

            var valueGo = new GameObject("Value", typeof(RectTransform));
            valueGo.transform.SetParent(headerGo.transform, false);
            valueLabel = UGUILabels.AddLabel(valueGo);
            valueLabel.fontSize = EvaCMGroundPalette.FieldValueFontSize;
            valueLabel.color = EvaCMGroundPalette.FieldValueColor;
            valueLabel.alignment = TextAlignmentOptions.Right;

            // The slider stays continuous: the view model snaps what it is given to the millimeter, so the
            // whole-numbers mode would only duplicate that rounding in a second place.
            return new SliderBuilder()
                .WithParent(fieldGo.transform)
                .WithMin(EvaCMGroundSettings.GroundOffsetMin)
                .WithMax(EvaCMGroundSettings.GroundOffsetMax)
                .Build();
        }

        /// <summary>
        /// Builds an explanatory sentence, wrapped to the window width, reserving <paramref name="height"/>
        /// pixels for it.
        /// </summary>
        private static void BuildHint(Transform parent, string key, float height)
        {
            var go = new GameObject("Hint", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            // Height reserved rather than fitted: a ContentSizeFitter inside a layout group measures the
            // text before the group has given it its width, so a wrapped paragraph would settle on the
            // wrong number of lines. The reserved height is what the sentence needs at this size.
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
            var label = UGUILabels.AddLabel(go);
            label.text = ModLocalization.GetString(key);
            label.fontSize = EvaCMGroundPalette.HintFontSize;
            label.color = EvaCMGroundPalette.HintColor;
            label.alignment = TextAlignmentOptions.TopLeft;
            // Labels come with wrapping off (they are meant for single-line rows); a paragraph is the
            // one case that wants it back, and its height then has to follow the wrapped text.
            label.enableWordWrapping = true;
        }
    }
}
