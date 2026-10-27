using UnityEngine;
using UnityEngine.UIElements;

namespace Vellocet.Sdk.UI
{
    /// <summary>A world-space UI Toolkit label for authored map text and entity displays.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class WorldText : MonoBehaviour
    {
        private const float PixelsPerUnit = 100;
        [SerializeField, TextArea] private string content = "";
        [SerializeField] private Color foreground = Color.white;
        [SerializeField] private float pointSize = 36;
        [SerializeField] private float fontUnitScale = .1f;
        [SerializeField] private Vector2 size = new(100, 100);
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
        private IVisualElementScheduledItem _fit;
        private float _fitMinimum, _fitMaximum, _fitCandidate;
        private int _fitSteps;
        private UIDocument _document;
        private Label _label;
        private PanelSettings _settings;
        private static PanelSettings SharedSettings;
        private static int SharedUsers;
        private Pivot DocumentPivot => anchor.x < .25f ? (anchor.y < .25f ? Pivot.BottomLeft : anchor.y > .75f ? Pivot.TopLeft : Pivot.LeftCenter) :
            anchor.x > .75f ? (anchor.y < .25f ? Pivot.BottomRight : anchor.y > .75f ? Pivot.TopRight : Pivot.RightCenter) :
            anchor.y < .25f ? Pivot.BottomCenter : anchor.y > .75f ? Pivot.TopCenter : Pivot.Center;
        public string text { get => content; set { var next = value ?? ""; if (content == next) return; content = next; Apply(); } }
        public Color color { get => foreground; set { foreground = value; Apply(); } }
        public Font FontAsset { get => font; set { font = value; Apply(); } }
        public FontStyle FontStyle { get => fontStyle; set { fontStyle = value; Apply(); } }
        public float fontSize { get => pointSize; set { pointSize = value; Apply(); } }
        public TextAnchor alignment { get => textAlignment; set { textAlignment = value; Apply(); } }
        public void Configure(string value, Color tint, float points, Vector2 dimensions, Vector2 pivot, Font sourceFont, bool screenFont, TextAnchor align, FontStyle style)
        {
            content = value; foreground = tint; pointSize = points; size = dimensions; anchor = pivot;
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
            _document = GetComponent<UIDocument>();
            if (!_document) _document = gameObject.AddComponent<UIDocument>();
            _document.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            _document.panelSettings = _settings; _document.enabled = true;
            _document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            _document.worldSpaceSize = size * PixelsPerUnit; _document.pivot = DocumentPivot;
            if (TryGetComponent<UIRenderer>(out var renderer)) renderer.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            var root = _document.rootVisualElement; root.Clear(); root.pickingMode = PickingMode.Ignore;
            _label = new Label { pickingMode = PickingMode.Ignore }; root.Add(_label);
            _label.style.width = Length.Percent(100); _label.style.height = Length.Percent(100);
            _label.style.position = Position.Absolute;
            _label.style.flexShrink = 0;
            _fit = _label.schedule.Execute(FitText).Every(16); _fit.Pause();
            Apply();
#endif
        }
        private void Apply()
        {
            if (_label == null) return;
            _document.worldSpaceSize = size * PixelsPerUnit; _document.pivot = DocumentPivot;
            _label.style.width = StyleKeyword.Auto; _label.style.height = StyleKeyword.Auto;
            _label.style.left = margins.x * PixelsPerUnit; _label.style.top = margins.y * PixelsPerUnit;
            _label.style.right = margins.z * PixelsPerUnit; _label.style.bottom = margins.w * PixelsPerUnit;
            _label.style.whiteSpace = wordWrap ? WhiteSpace.Normal : WhiteSpace.NoWrap;
            _label.text = content; _label.enableRichText = richText; _label.style.color = foreground;
            _fitMinimum = Mathf.Max(.01f, minimumPointSize * fontUnitScale * PixelsPerUnit);
            _fitMaximum = Mathf.Max(_fitMinimum, maximumPointSize * fontUnitScale * PixelsPerUnit);
            _fitCandidate = autoSize ? _fitMaximum : Mathf.Max(.01f, pointSize * fontUnitScale * PixelsPerUnit);
            _label.style.fontSize = _fitCandidate;
            _label.style.unityTextAlign = textAlignment; _label.style.unityFontStyleAndWeight = fontStyle;
            if (font) _label.style.unityFontDefinition = FontDefinition.FromFont(font);
            else _label.style.unityFontDefinition = StyleKeyword.Null;
            _fitSteps = 0; if (autoSize) _fit?.Resume(); else _fit?.Pause();
        }
        private void FitText()
        {
            if (_label?.panel == null || _fitSteps++ >= 10) { _fit?.Pause(); return; }
            var area = _label.contentRect;
            if (area.width <= 0 || area.height <= 0) return;
            // Measure after the style pass so the candidate size has resolved.
            if (!Mathf.Approximately(_label.resolvedStyle.fontSize, _fitCandidate)) return;
            var measured = _label.MeasureTextSize(content, wordWrap ? area.width : 0,
                wordWrap ? VisualElement.MeasureMode.AtMost : VisualElement.MeasureMode.Undefined,
                0, VisualElement.MeasureMode.Undefined);
            var fits = measured.x <= area.width + .001f && measured.y <= area.height + .001f;
            if (fits) _fitMinimum = _fitCandidate; else _fitMaximum = _fitCandidate;
            if ((_fitMaximum - _fitMinimum) <= Mathf.Max(.0001f, maximumPointSize * fontUnitScale * PixelsPerUnit * .005f) || _fitSteps >= 9)
            { _label.style.fontSize = _fitMinimum; _fit.Pause(); return; }
            _fitCandidate = (_fitMinimum + _fitMaximum) * .5f;
            _label.style.fontSize = _fitCandidate;
        }
        private void OnValidate() => Apply();
#if UNITY_EDITOR
        // Scene authoring previews must not add transient components to prefab assets or exported maps.
        private void OnDrawGizmos()
        {
            if (Application.isPlaying || string.IsNullOrEmpty(content)) return;
            var style = new GUIStyle(UnityEditor.EditorStyles.label) { richText = richText, alignment = textAlignment, fontStyle = fontStyle };
            if (font) style.font = font;
            style.normal.textColor = foreground;
            UnityEditor.Handles.Label(transform.position, content, style);
        }
#endif
        private void OnDisable() { _fit?.Pause(); _fit = null; if (_document) _document.enabled = false; _label = null; }
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
