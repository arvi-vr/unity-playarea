namespace ARVI.PlayArea
{
    using System;
#if !UNITY_2020_1_OR_NEWER
    using System.Linq;
    using System.Reflection;
#endif
    using UnityEditor;
    using UnityEngine;

    public class PlayAreaUpdateWindow : EditorWindow
    {
        private static GUIStyle largeStyle;
        private static GUIStyle normalStyle;

        private Version version;
        private string description;
        private DateTime date;
        private string versionURL;
        private bool isNewVersion;

        private const string WINDOW_TITLE = "ARVI Play Area Update Checker";
        private const float WINDOW_WIDTH = 500f;
        private const float WINDOW_HEIGHT = 300f;

        public static PlayAreaUpdateWindow Show(Version version, string description, DateTime date, string versionURL, bool isNewVersion)
        {
            var window = GetWindow<PlayAreaUpdateWindow>(true, WINDOW_TITLE, true);

            window.position = new Rect(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT);
            CenterOnMainWindow(window);
            window.version = version;
            window.description = description;
            window.date = date;
            window.versionURL = versionURL;
            window.isNewVersion = isNewVersion;

            return window;
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

        protected virtual void OnGUI()
        {
            if (version == null)
                return;

            InitializeStyles();

            GUILayout.Label(isNewVersion ? "New version available!" : "Already up to date", largeStyle);
#if UNITY_4_6 || UNITY_5_0
            GUILayout.Label(string.Format("Installed Version: <b>{0}</b>\nLatest Version: <b>{1}</b> (published on {2})\n\n{3}", PlayArea.Version, version, date.ToLocalTime(), description), normalStyle);
#else
            GUILayout.Label(new GUIContent(string.Format("Installed Version: <b>{0}</b>\nLatest Version: <b>{1}</b> (published on {2})\n\n{3}", PlayArea.Version, version, date.ToLocalTime(), description)), normalStyle);
#endif
            GUILayout.FlexibleSpace();

            if (isNewVersion)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical();
                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.5f, 0.5f, 1f);
                if (GUILayout.Button("Download", GUILayout.Height(30), GUILayout.MaxWidth(150)))
                    Application.OpenURL(versionURL);
                GUI.backgroundColor = originalColor;
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.FlexibleSpace();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Skip this version", GUILayout.Height(20), GUILayout.MaxWidth(120)))
                {
                    EditorPrefs.SetString(PlayAreaUpdateChecker.SKIPPED_VERSION_KEY, version.ToString());
                    Close();
                }

                GUILayout.FlexibleSpace();
                using (var changeCheckScope = new EditorGUI.ChangeCheckScope())
                {
                    var toggleValue = GUILayout.Toggle(PlayAreaUpdateChecker.ShouldCheckForUpdates, "Automatically check for updates");
                    if (changeCheckScope.changed)
                        PlayAreaUpdateChecker.ShouldCheckForUpdates = toggleValue;
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                using (var changeCheckScope = new EditorGUI.ChangeCheckScope())
                {
                    var toggleValue = GUILayout.Toggle(PlayAreaUpdateChecker.ShouldCheckForUpdates, "Automatically check for updates");
                    if (changeCheckScope.changed)
                        PlayAreaUpdateChecker.ShouldCheckForUpdates = toggleValue;
                }
                GUILayout.EndHorizontal();
            }
        }

        private static void InitializeStyles()
        {
            if (largeStyle == null)
            {
                largeStyle = new GUIStyle(EditorStyles.largeLabel)
                {
                    fontSize = 28,
                    alignment = TextAnchor.UpperCenter,
                    richText = true
                };
            }

            if (normalStyle == null)
            {
                normalStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    richText = true
                };
            }
        }
    }
}