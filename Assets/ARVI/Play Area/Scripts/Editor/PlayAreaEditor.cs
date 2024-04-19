using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ARVI.PlayArea
{
    [CustomEditor(typeof(PlayArea))]
    public class PlayAreaEditor : Editor
    {
        enum VRProvider
        {
            None = 0,
            OpenVR = 1,
            OpenXR = 2
        }

        private VRProvider currentProvider = VRProvider.None;

        private const string DEFINE_OPENVR = "ARVI_PROVIDER_OPENVR";
        private const string DEFINE_OPENXR = "ARVI_PROVIDER_OPENXR";

        private Color defaultGUIBackgroundColor;

        private void OnEnable()
        {
            defaultGUIBackgroundColor = GUI.backgroundColor;

            if (HasDefine(DEFINE_OPENVR, EditorUserBuildSettings.selectedBuildTargetGroup))
                currentProvider = VRProvider.OpenVR;
            else if (HasDefine(DEFINE_OPENXR, EditorUserBuildSettings.selectedBuildTargetGroup))
                currentProvider = VRProvider.OpenXR;
            else
                currentProvider = VRProvider.None;
        }

        public override void OnInspectorGUI()
        {
            GUI.backgroundColor = currentProvider == VRProvider.None ? Color.red : Color.green;
            var selectedProvider = (VRProvider)EditorGUILayout.EnumPopup("Provider", currentProvider);
            GUI.backgroundColor = defaultGUIBackgroundColor;

            if (currentProvider != selectedProvider)
            {
                currentProvider = selectedProvider;

                switch (selectedProvider)
                {
                    case VRProvider.OpenVR:
                        ModifyDefines(new string[] { DEFINE_OPENXR }, new string[] { DEFINE_OPENVR }, EditorUserBuildSettings.selectedBuildTargetGroup);
                        break;
                    case VRProvider.OpenXR:
                        ModifyDefines(new string[] { DEFINE_OPENVR }, new string[] { DEFINE_OPENXR }, EditorUserBuildSettings.selectedBuildTargetGroup);
                        break;
                    default:
                        ModifyDefines(new string[] { DEFINE_OPENVR, DEFINE_OPENXR }, new string[] { }, EditorUserBuildSettings.selectedBuildTargetGroup);
                        break;
                }
            }

            DrawDefaultInspector();
        }

        private bool HasDefine(string define, BuildTargetGroup targetGroup)
        {
#if UNITY_2021_2_OR_NEWER
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
#else
			string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#endif
            return currentDefines.Contains(define);
        }

        private void ModifyDefines(string[] definesToRemove, string[] definesToSet, BuildTargetGroup targetGroup)
        {
#if UNITY_2021_2_OR_NEWER
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
#else
			string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#endif
            for (int i = 0; i < definesToRemove.Length; ++i)
            {
                var define = definesToRemove[i];
                if (currentDefines.Contains(define))
                    currentDefines = currentDefines.Replace(define + ";", "").Replace(";" + define, "").Replace(define, "");
            }

            for (int i = 0; i < definesToSet.Length; ++i)
            {
                var define = definesToSet[i];
                if (!currentDefines.Contains(define))
                {
                    if (string.IsNullOrEmpty(currentDefines))
                    {
                        currentDefines = define;
                    }
                    else
                    {
                        if (!currentDefines[currentDefines.Length - 1].Equals(';'))
                            currentDefines += ';';
                        currentDefines += define;
                    }
                }
            }

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, currentDefines);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, currentDefines);
#endif
        }
    }
}