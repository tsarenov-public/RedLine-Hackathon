using UnityEditor;
using UnityEngine;

namespace Arcade.Vehicle
{
    [CustomEditor(typeof(NFSCameraController)), CanEditMultipleObjects]
    public sealed class NFSCameraControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == nameof(NFSCameraController.lookAtOffset))
                {
                    EditorGUILayout.LabelField(new GUIContent(property.displayName, property.tooltip));
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.Slider(property.FindPropertyRelative("x"), -1f, 1f,
                            new GUIContent("Lateral (m)", "Aim left or right of the chassis center."));
                        EditorGUILayout.Slider(property.FindPropertyRelative("y"), 0f, 2f,
                            new GUIContent("Height (m)", "Aim above the chassis origin."));
                        EditorGUILayout.Slider(property.FindPropertyRelative("z"), 0f, 8f,
                            new GUIContent("Ahead (m)", "Aim farther down the track."));
                    }
                    continue;
                }

                using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                    EditorGUILayout.PropertyField(property, true);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
