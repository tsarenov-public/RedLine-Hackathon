using UnityEngine;
using UnityEditor;

namespace TND.Upscaling.Framework.IUFW
{
    [CustomEditor(typeof(UpscalerAdapterOptions))]
    public class UpscalerAdapterOptionsEditor: UpscalerControllerEditor
    {
        protected override bool SaveInParentAsset => true;

        private SerializedProperty _enableReactiveMaskProperty;
        private SerializedProperty _reactiveMaskUseComponentMaxProperty;
        private SerializedProperty _reactiveMaskScaleProperty;
        private SerializedProperty _reactiveMaskApplyThresholdProperty;
        private SerializedProperty _reactiveMaskThresholdProperty;
        private SerializedProperty _reactiveMaskBinaryValueProperty;
        
        protected override void OnEnable()
        {
            base.OnEnable();

            _enableReactiveMaskProperty = serializedObject.FindProperty("m_EnableReactiveMaskPass");
            _reactiveMaskUseComponentMaxProperty = serializedObject.FindProperty("m_ReactiveMaskUseComponentMax");
            _reactiveMaskScaleProperty = serializedObject.FindProperty("m_ReactiveMaskScale");
            _reactiveMaskApplyThresholdProperty = serializedObject.FindProperty("m_ApplyReactiveMaskThreshold");
            _reactiveMaskThresholdProperty = serializedObject.FindProperty("m_ReactiveMaskThreshold");
            _reactiveMaskBinaryValueProperty = serializedObject.FindProperty("m_ReactiveMaskBinaryValue");
        }

        public override void OnInspectorGUI()
        {
            // We manually apply the current indentation to a box containing the entire TND Upscaling UI.
            // Within that box, all indentation is temporarily reset back to 0.
            int indentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            EditorGUILayout.BeginVertical(new GUIStyle { margin = new RectOffset(indentLevel * 15, 0, 0, 0) });
            EditorGUILayout.BeginVertical("box");
            
            base.OnInspectorGUI();
            
            GUILayout.EndVertical();
            GUILayout.EndVertical();

            EditorGUI.indentLevel = indentLevel;
        }

        protected override void DrawReactiveMaskSettings()
        {
            EditorGUILayout.PropertyField(_enableReactiveMaskProperty);
            if (_enableReactiveMaskProperty.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_reactiveMaskUseComponentMaxProperty);
                EditorGUILayout.PropertyField(_reactiveMaskScaleProperty);
                EditorGUILayout.PropertyField(_reactiveMaskApplyThresholdProperty);
                if (_reactiveMaskApplyThresholdProperty.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Slider(_reactiveMaskThresholdProperty, 0.0f, 1.0f);
                    EditorGUILayout.Slider(_reactiveMaskBinaryValueProperty, 0.0f, 1.0f);
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }
        }
    }
}
