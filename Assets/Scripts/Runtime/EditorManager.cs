using UnityEngine;

namespace RubyEditor.Core
{
    public class EditorManager : MonoBehaviour
    {
        public static EditorManager Instance { get; private set; }

        [Header("Systems")]
        public GridSystem gridSystem;
        public SelectionSystem selectionSystem;

        [Header("Current State")]
        public EditorMode currentMode = EditorMode.Select;
        public Tool currentTool;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            InitializeSystems();
        }

        void InitializeSystems()
        {
            // Grid System initialisieren
            if (gridSystem == null)
                gridSystem = GetComponent<GridSystem>();

            // Selection System initialisieren
            if (selectionSystem == null)
                selectionSystem = GetComponent<SelectionSystem>();
        }
    }

    public enum EditorMode
    {
        Select,
        Place,
        Paint,
        Terrain,
        Navigation
    }

    // Define a basic Tool class or replace with your actual Tool implementation
    public class Tool
    {
        // Add properties and methods as needed
    }
}