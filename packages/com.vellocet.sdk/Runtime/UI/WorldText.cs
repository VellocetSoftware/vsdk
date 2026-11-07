using System;
using UnityEngine;
#if !UNITY_SERVER
using Vellocet.Localization.UI;
#endif
using UnityEngine.UIElements;

namespace Vellocet.Sdk.UI
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class WorldText : MonoBehaviour
    {
        private const float PixelsPerUnit = 100;
        [SerializeField, TextArea] private string content = "";
        [SerializeField] private Color foreground = Color.white;
        [SerializeField] private float pointSize = 36;
        [SerializeField] private float fontUnitScale = .1f;
        [SerializeField, Tooltip("World units. Set both to zero to fit the text.")]
        private Vector2 size;
        [SerializeField] private Vector2 anchor = new(.5f, .5f);
        [SerializeField] private TextAnchor textAlignment = TextAnchor.MiddleCenter;
        [SerializeField] private Font font;
        [SerializeField] private FontStyle fontStyle;
        [SerializeField] private bool richText = true;
        [SerializeField] private bool wordWrap = true;
        [SerializeField] private Vector4 margins;
        [SerializeField] private bool autoSize;
        [SerializeField] private float minimumPointSize = 18;
        [SerializeField] private float maximumPointSize = 72;
        private PanelRenderer _document;
        private VisualElement _root;
        private int _uiVersion;
        private IDisposable _localization;
        private Label _label;
        private PanelSettings _settings;
        private static PanelSettings SharedSettings;
        private static int SharedUsers;
        private Pivot DocumentPivot => anchor.x < .25f ? (anchor.y < .25f ? Pivot.BottomLeft : anchor.y > .75f ? Pivot.TopLeft : Pivot.LeftCenter) :
            anchor.x > .75f ? (anchor.y < .25f ? Pivot.BottomRight : anchor.y > .75f ? Pivot.TopRight : Pivot.RightCenter) :
            anchor.y < .25f ? Pivot.BottomCenter : anchor.y > .75f ? Pivot.TopCenter : Pivot.Center;

        public string text
        {
            get => content;
            set { var next = value ?? ""; if (content == next) return; content = next; if (_label != null) _label.text = next; }
        }
        public Color color
        {
            get => foreground;
            set { if (foreground == value) return; foreground = value; if (_label != null) _label.style.color = value; }
        }
        public Font FontAsset
        {
            get => font;
            set { if (font == value) return; font = value; if (_label != null) ApplyFont(); }
        }
        public FontStyle FontStyle
        {
            get => fontStyle;
            set { if (fontStyle == value) return; fontStyle = value; if (_label != null) _label.style.unityFontStyleAndWeight = value; }
        }
        public float fontSize
        {
            get => pointSize;
            set { if (pointSize == value) return; pointSize = value; if (_label != null) _label.style.fontSize = Mathf.Max(.01f, value * fontUnitScale * PixelsPerUnit); }
        }
        public TextAnchor alignment
        {
            get => textAlignment;
            set { if (textAlignment == value) return; textAlignment = value; if (_label != null) _label.style.unityTextAlign = value; }
        }
        public void Configure(string value, Color tint, float points, Vector2 dimensions, Vector2 pivot, Font sourceFont, bool screenFont, TextAnchor align, FontStyle style)
        {
            content = value ?? ""; foreground = tint; pointSize = points; size = dimensions; anchor = pivot;
            font = sourceFont; fontUnitScale = screenFont ? 1 : .1f; textAlignment = align; fontStyle = style; Apply();
        }
        private void OnEnable()
        {
#if !UNITY_SERVER
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif
            if (!_settings)
            {
                if (!SharedSettings)
                {
                    SharedUsers = 0;
                    var template = Resources.Load<PanelSettings>("UI/WorldTextPanelSettings");
                    if (!template) { Debug.LogError("World text panel settings are missing.", this); return; }
                    SharedSettings = Instantiate(template); SharedSettings.hideFlags = HideFlags.HideAndDontSave;
                    SharedSettings.name = "World text"; SharedSettings.renderMode = PanelRenderMode.WorldSpace;
                    SharedSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/DesktopTheme");
                    SharedSettings.textSettings = Resources.Load<PanelTextSettings>("UI/DesktopTextSettings");
                }
                _settings = SharedSettings; SharedUsers++;
            }
            _document = GetComponent<PanelRenderer>();
            if (!_document) _document = gameObject.AddComponent<PanelRenderer>();
            _document.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            _document.panelSettings = _settings;
            _localization = LocalizationPanels.Register(_settings);
            _document.enabled = true;
            _document.RegisterUIReloadCallback(OnUIReload);
#endif
        }
        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version && ReferenceEquals(_root, root)) return;
            _label?.RemoveFromHierarchy();
            _uiVersion = version;
            _root = root;
            root.pickingMode = PickingMode.Ignore;
            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.style.flexShrink = 0;
            root.Add(_label);
            Apply();
        }

        private void Apply()
        {
            if (_label == null) return;
            var bounded = size.x > 0 && size.y > 0;
            _document.worldSpaceSizeMode = bounded ? WorldSpaceSizeMode.Fixed : WorldSpaceSizeMode.Dynamic;
            if (bounded) _document.worldSpaceSize = size * PixelsPerUnit;
            _document.pivot = DocumentPivot;
            var root = _root;
            root.style.paddingLeft = margins.x * PixelsPerUnit; root.style.paddingTop = margins.y * PixelsPerUnit;
            root.style.paddingRight = margins.z * PixelsPerUnit; root.style.paddingBottom = margins.w * PixelsPerUnit;
            _label.style.width = bounded ? Length.Percent(100) : StyleKeyword.Auto;
            _label.style.height = bounded ? Length.Percent(100) : StyleKeyword.Auto;
            _label.style.alignSelf = Align.FlexStart;
            _label.style.whiteSpace = bounded && wordWrap ? WhiteSpace.Normal : WhiteSpace.NoWrap;
            _label.text = content; _label.enableRichText = richText; _label.style.color = foreground;
            _label.style.fontSize = Mathf.Max(.01f, pointSize * fontUnitScale * PixelsPerUnit);
            _label.style.unityTextAlign = textAlignment; _label.style.unityFontStyleAndWeight = fontStyle;
            // Keep Best Fit in a fixed box to avoid layout feedback.
            var fit = autoSize && bounded;
            _label.style.unityTextGenerator = fit ? TextGeneratorType.Advanced : TextGeneratorType.Standard;
            var minimum = Mathf.Max(.01f, minimumPointSize * fontUnitScale * PixelsPerUnit);
            var maximum = Mathf.Max(minimum, maximumPointSize * fontUnitScale * PixelsPerUnit);
            _label.style.unityTextAutoSize = fit ? new TextAutoSize(TextAutoSizeMode.BestFit, minimum, maximum) : TextAutoSize.None();
            ApplyFont();
        }
        private void ApplyFont()
        {
            if (font) _label.style.unityFontDefinition = FontDefinition.FromFont(font);
            else _label.style.unityFontDefinition = StyleKeyword.Null;
        }
        private void OnValidate() => Apply();
#if UNITY_EDITOR
        // Keep preview components out of saved prefabs and exported maps.
        private void OnDrawGizmos()
        {
            if (Application.isPlaying || string.IsNullOrEmpty(content)) return;
            var style = new GUIStyle(UnityEditor.EditorStyles.label) { richText = richText, alignment = textAlignment, fontStyle = fontStyle };
            if (font) style.font = font;
            style.normal.textColor = foreground;
            UnityEditor.Handles.Label(transform.position, content, style);
        }
#endif
        private void OnDisable()
        {
            if (_document)
            {
                _document.UnregisterUIReloadCallback(OnUIReload);
                _document.enabled = false;
            }
            _localization?.Dispose();
            _localization = null;
            _label?.RemoveFromHierarchy();
            _label = null;
            _root = null;
            _uiVersion = 0;
        }
        private void OnDestroy()
        {
            if (_document)
            {
                if (Application.isPlaying) Destroy(_document); else DestroyImmediate(_document);
            }
            if (_settings && --SharedUsers <= 0)
            {
                if (Application.isPlaying) Destroy(_settings); else DestroyImmediate(_settings);
                SharedSettings = null; SharedUsers = 0;
            }
            _settings = null;
        }
    }
}
