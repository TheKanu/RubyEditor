// Erstelle diese Datei: Assets/RubyEditor/Scripts/Core/RubyEditorBootstrap.cs
using UnityEngine;

namespace RubyEditor.Core
{
    public class RubyEditorBootstrap : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject editorManagerPrefab;

        void Awake()
        {
            Debug.Log("🚀 Ruby GameEditor wird gestartet...");
            InitializeEditor();
        }

        void InitializeEditor()
        {
            // Editor Manager erstellen
            if (editorManagerPrefab != null)
            {
                Instantiate(editorManagerPrefab);
            }
            else
            {
                // Fallback: Erstelle Manager zur Laufzeit
                GameObject managerGO = new GameObject("EditorManager");
                managerGO.AddComponent<EditorManager>();
                managerGO.AddComponent<GridSystem>();
                managerGO.AddComponent<SelectionSystem>();
            }

            Debug.Log("✅ Ruby GameEditor erfolgreich initialisiert!");
        }
    }
}