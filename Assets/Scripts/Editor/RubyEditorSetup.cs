using UnityEngine;
using UnityEditor;

namespace RubyEditor.Setup
{
    [InitializeOnLoad]
    public static class RubyEditorSetup
    {
        static RubyEditorSetup()
        {
            // Add missing tags
            AddTag("ZoneObject");
            AddTag("EditorObject");
            AddTag("TerrainObject");
        }
        
        static void AddTag(string tag)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            
            // Check if tag already exists
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
                if (t.stringValue.Equals(tag))
                {
                    return; // Tag already exists
                }
            }
            
            // Add new tag
            tagsProp.InsertArrayElementAtIndex(0);
            SerializedProperty newTag = tagsProp.GetArrayElementAtIndex(0);
            newTag.stringValue = tag;
            tagManager.ApplyModifiedProperties();
            
            Debug.Log($"Added tag: {tag}");
        }
    }
}