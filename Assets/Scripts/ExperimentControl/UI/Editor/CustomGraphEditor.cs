using UnityEditor;
using UnityEngine;

namespace ExperimentControl.UI.Editor
{
    [CustomEditor(typeof(CustomGraph))]
    public class CustomGraphEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            CustomGraph myScript = (CustomGraph)target;
            if(GUILayout.Button("Redraw Graph"))
            {
                myScript.ResetScaling();
            }
            
            DrawDefaultInspector();
        }
    }
}