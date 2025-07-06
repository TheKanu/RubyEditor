using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
// Add the correct namespace for EditorManager if it exists, for example:
using RubyEditor.Core;

namespace RubyEditor.UI
{
    public class EditorUIManager : MonoBehaviour
    {
        [Header("UI Documents")]
        [SerializeField] private UIDocument mainUIDocument;
        [SerializeField] private VisualTreeAsset toolPaletteTemplate;
        [SerializeField] private VisualTreeAsset inspectorTemplate;

        [Header("Panel References")]
        private VisualElement root;
        private VisualElement toolPalette;
        private VisualElement inspector;
        private VisualElement statusBar;
        private Label coordinatesLabel;
        private Label modeLabel;

        // Tool buttons
        private Dictionary<string, Button> toolButtons = new Dictionary<string, Button>();

        public static EditorUIManager Instance { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void OnEnable()
        {
            // Warte bis UIDocument bereit ist
            if (mainUIDocument != null)
            {
                root = mainUIDocument.rootVisualElement;
                SetupUI();
            }
        }

        void SetupUI()
        {
            // Tool Palette
            toolPalette = root.Q<VisualElement>("tool-palette");
            if (toolPalette != null)
            {
                SetupToolPalette();
            }

            // Inspector
            inspector = root.Q<VisualElement>("inspector");
            if (inspector != null)
            {
                SetupInspector();
            }

            // Status Bar
            statusBar = root.Q<VisualElement>("status-bar");
            if (statusBar != null)
            {
                SetupStatusBar();
            }
        }

        void SetupToolPalette()
        {
            // Clear existing content
            toolPalette.Clear();

            // Create tool categories
            CreateToolCategory("Selection", new string[] { "Select", "Move", "Rotate", "Scale" });
            CreateToolCategory("Terrain", new string[] { "Raise", "Lower", "Smooth", "Paint" });
            CreateToolCategory("Objects", new string[] { "Place", "Delete", "Duplicate", "Align" });
            CreateToolCategory("Navigation", new string[] { "Waypoint", "Path", "Area" });
        }

        void CreateToolCategory(string categoryName, string[] tools)
        {
            // Category container
            var category = new VisualElement();
            category.AddToClassList("tool-category");

            // Category header
            var header = new Label(categoryName);
            header.AddToClassList("category-header");
            category.Add(header);

            // Tool buttons container
            var buttonContainer = new VisualElement();
            buttonContainer.AddToClassList("tool-buttons");
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.flexWrap = Wrap.Wrap;

            // Create buttons for each tool
            foreach (string toolName in tools)
            {
                var button = new Button(() => SelectTool(toolName));
                button.text = toolName;
                button.AddToClassList("tool-button");
                button.style.width = 60;
                button.style.height = 60;

                toolButtons[toolName] = button;
                buttonContainer.Add(button);
            }

            category.Add(buttonContainer);
            toolPalette.Add(category);
        }

        void SetupInspector()
        {
            inspector.Clear();

            // Inspector header
            var header = new Label("Properties");
            header.AddToClassList("inspector-header");
            inspector.Add(header);

            // Transform section
            var transformFoldout = new Foldout();
            transformFoldout.text = "Transform";
            transformFoldout.value = true;

            // Position fields
            var positionContainer = CreateVector3Field("Position", Vector3.zero);
            transformFoldout.Add(positionContainer);

            // Rotation fields
            var rotationContainer = CreateVector3Field("Rotation", Vector3.zero);
            transformFoldout.Add(rotationContainer);

            // Scale fields
            var scaleContainer = CreateVector3Field("Scale", Vector3.one);
            transformFoldout.Add(scaleContainer);

            inspector.Add(transformFoldout);
        }

        VisualElement CreateVector3Field(string label, Vector3 defaultValue)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.marginBottom = 5;

            var fieldLabel = new Label(label);
            fieldLabel.style.width = 60;
            container.Add(fieldLabel);

            var xField = new FloatField() { value = defaultValue.x };
            xField.style.width = 50;
            container.Add(xField);

            var yField = new FloatField() { value = defaultValue.y };
            yField.style.width = 50;
            container.Add(yField);

            var zField = new FloatField() { value = defaultValue.z };
            zField.style.width = 50;
            container.Add(zField);

            return container;
        }

        void SetupStatusBar()
        {
            statusBar.Clear();
            statusBar.style.flexDirection = FlexDirection.Row;
            statusBar.style.justifyContent = Justify.SpaceBetween;

            // Left side - coordinates
            coordinatesLabel = new Label("X: 0.0 Y: 0.0 Z: 0.0");
            coordinatesLabel.AddToClassList("status-text");
            statusBar.Add(coordinatesLabel);

            // Center - current mode
            modeLabel = new Label("Mode: Select");
            modeLabel.AddToClassList("status-text");
            statusBar.Add(modeLabel);

            // Right side - grid snap
            var snapToggle = new Toggle("Grid Snap");
            snapToggle.value = true;
            statusBar.Add(snapToggle);
        }

        void SelectTool(string toolName)
        {
            // Update UI
            foreach (var kvp in toolButtons)
            {
                kvp.Value.RemoveFromClassList("selected");
            }

            if (toolButtons.ContainsKey(toolName))
            {
                toolButtons[toolName].AddToClassList("selected");
            }

            // Update mode label
            if (modeLabel != null)
            {
                modeLabel.text = $"Mode: {toolName}";
            }

            // Notify EditorManager
            if (EditorManager.Instance != null)
            {
                // EditorManager.Instance.SetTool(toolName);
            }

            Debug.Log($"Selected tool: {toolName}");
        }

        public void UpdateCoordinates(Vector3 position)
        {
            if (coordinatesLabel != null)
            {
                coordinatesLabel.text = $"X: {position.x:F1} Y: {position.y:F1} Z: {position.z:F1}";
            }
        }

        public void UpdateInspector(GameObject selectedObject)
        {
            if (selectedObject == null)
            {
                // Clear inspector
                return;
            }

            // Update transform values
            // Implementation folgt
        }
    }
}