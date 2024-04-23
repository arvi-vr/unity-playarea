namespace ARVI.PlayArea
{
    using System;
    using System.Text;
#if UNITY_2020_1_OR_NEWER
    using UnityEditor.Build;
#else
    using System.Reflection;
    using System.Linq;
#endif
    using UnityEditor;
    using UnityEngine;

    public class PlayAreaSetupWindow : EditorWindow
    {
        enum VRProvider
        {
            None = 0,
            OpenVR = 1,
            OpenXR = 2
        }

        private static PlayAreaSetupWindow window;

        private const string WINDOW_TITLE = "ARVI Play Area Setup";
        private const float WINDOW_WIDTH = 500f;
        private const float WINDOW_HEIGHT = 300f;

        private const string DEFINE_OPENVR = "ARVI_PROVIDER_OPENVR";
        private const string DEFINE_OPENXR = "ARVI_PROVIDER_OPENXR";

        private VRProvider currentProvider = VRProvider.None;
        private Color defaultGUIBackgroundColor;

        [MenuItem("ARVI/Play Area/Setup", false, 101)]
        public static void ShowWindow()
        {
            window = GetWindow<PlayAreaSetupWindow>(true, WINDOW_TITLE, true);
            window.position = new Rect(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT);
            CenterOnMainWindow(window);
        }

        public static void CenterOnMainWindow(EditorWindow window)
        {
            Rect mainWindow;
            if (!TryGetEditorMainWindow(out mainWindow))
                mainWindow = new Rect(0, 0, Screen.currentResolution.width, Screen.currentResolution.height);
            var position = window.position;
            position.center = mainWindow.center;
            window.position = position;
        }

        public static bool TryGetEditorMainWindow(out Rect window)
        {
    #if UNITY_2020_1_OR_NEWER
            window = EditorGUIUtility.GetMainWindowPosition();
            return true;
    #else
                window = Rect.zero;
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var containerWinType = assemblies.SelectMany(assembly => assembly.GetTypes()).FirstOrDefault(type => type.IsSubclassOf(typeof(ScriptableObject)) && type.Name == "ContainerWindow");
                if (containerWinType == null)
                    return false;
                var showModeField = containerWinType.GetField("m_ShowMode", BindingFlags.NonPublic | BindingFlags.Instance);
                if (showModeField == null)
                    return false;
                var positionProperty = containerWinType.GetProperty("position", BindingFlags.Public | BindingFlags.Instance);
                if (positionProperty == null)
                    return false;
                var windows = Resources.FindObjectsOfTypeAll(containerWinType);
                foreach (var win in windows)
                {
                    try
                    {
                        var showmode = (int)showModeField.GetValue(win);
                        if (showmode == 4) // main window
                        {
                            window = (Rect)positionProperty.GetValue(win, null);
                            return true;
                        }
                    }
                    catch { }
                }
                return false;
    #endif
        }

        protected virtual void OnEnable()
        {
            defaultGUIBackgroundColor = GUI.backgroundColor;

            if (HasDefine(DEFINE_OPENVR, EditorUserBuildSettings.selectedBuildTargetGroup))
                currentProvider = VRProvider.OpenVR;
            else if (HasDefine(DEFINE_OPENXR, EditorUserBuildSettings.selectedBuildTargetGroup))
                currentProvider = VRProvider.OpenXR;
            else
                currentProvider = VRProvider.None;
        }

        protected virtual void OnGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            DrawVRProviderArea();
            GUILayout.Space(20f);
            DrawPlayAreaLayerArea();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawVRProviderArea()
        {
#if UNITY_4_6 || UNITY_5_0
            GUILayout.Label("1. Select your VR provider: OpenVR or OpenXR");
#else
            GUILayout.Label(new GUIContent("1. Select your VR provider: OpenVR or OpenXR"));
#endif
            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = currentProvider == VRProvider.None ? Color.red : Color.green;
            var selectedProvider = (VRProvider)EditorGUILayout.EnumPopup(string.Empty, currentProvider, GUILayout.MaxWidth(100));
            GUI.backgroundColor = defaultGUIBackgroundColor;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

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
        }

        private void DrawPlayAreaLayerArea()
        {
#if UNITY_4_6 || UNITY_5_0
            GUILayout.Label("2. For the Play Area to work correctly, an additional layer is required.\nClick \"Setup Layer\" to configure it automatically.");
#else
            GUILayout.Label(new GUIContent("2. To disable rendering of the game world when leaving the playe area,\n" +
                "you need to configure an additional layer. Click \"Setup Layer\" to configure\n" +
                "it automatically or do nothing if you want to use your custom behavior\n" +
                "when leaving the play area."));
#endif
            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.5f, 0.5f, 1f);            
            if (GUILayout.Button("Setup Layer", GUILayout.Height(30), GUILayout.MaxWidth(150)))
                SetupLayer();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUI.backgroundColor = originalColor;
        }

        private static bool HasDefine(string define, BuildTargetGroup targetGroup)
        {
#if UNITY_2021_2_OR_NEWER
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            var currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
#else
			string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#endif
            return currentDefines.Contains(define);
        }

        private static void ModifyDefines(string[] definesToRemove, string[] definesToSet, BuildTargetGroup targetGroup)
        {
#if UNITY_2021_2_OR_NEWER
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            var currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
#else
			string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#endif
            for (var i = 0; i < definesToRemove.Length; ++i)
            {
                var define = definesToRemove[i];
                if (currentDefines.Contains(define))
                    currentDefines = currentDefines.Replace(define + ";", "").Replace(";" + define, "").Replace(define, "");
            }

            for (var i = 0; i < definesToSet.Length; ++i)
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

        private static void SetupLayer()
        {
            var log = new StringBuilder();

            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset != null && asset.Length > 0)
            {
                try
                {
                    var tagManager = asset[0];
                    var serializedObject = new SerializedObject(asset[0]);
                    var layers = serializedObject.FindProperty("layers");

                    var layersChanged = TryUpdateLayer(layers, PlayArea.PLAY_AREA_LAYER_NAME, log);

                    EditorUtility.DisplayDialog("Layer Report", log.ToString(), "OK");

                    if (layersChanged)
                    {
                        serializedObject.ApplyModifiedProperties();
                        serializedObject.Update();

                        EditorUtility.SetDirty(tagManager);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        Debug.Log("TagManager saved");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            else
            {
                Debug.LogWarning("Unable to load ProjectSettings/TagManager.asset");
            }
        }

        private static bool TryUpdateLayer(SerializedProperty layers, string layerName, StringBuilder log)
        {

            var layer = -1;

            for (var i = 6; i < layers.arraySize; ++i)
            {
                var layerN = layers.GetArrayElementAtIndex(i).stringValue;
                if (layerN == layerName)
                {
                    log.AppendLine(string.Format("Layer \"{0}\" already exists at slot {1}", layerName, 1));
                    return false;
                }
                if (layer == -1 && string.IsNullOrEmpty(layerN))
                    layer = i;
            }

            if (layer == -1)
            {
                log.AppendLine(string.Format("No layer space available for required layer \"{0}\". Make space and try again.", layerName));
                return false;
            }
            else
            {
                var property = layers.GetArrayElementAtIndex(layer);
                property.stringValue = layerName;
                log.AppendLine(string.Format("Layer \"{0}\" assigned to slot {1}", layerName, layer));
                return true;
            }
        }
    }
}