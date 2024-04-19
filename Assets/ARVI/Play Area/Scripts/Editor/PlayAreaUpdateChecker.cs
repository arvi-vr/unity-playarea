namespace ARVI.PlayArea
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;
#if UNITY_2017_4_OR_NEWER
    using UnityEngine.Networking;
#endif

    [InitializeOnLoad]
    public static class PlayAreaUpdateChecker
    {
        [Serializable]
        private sealed class VersionInfo
        {
            [NonSerialized]
            public System.Version version;
            [NonSerialized]
            public DateTime publishedDateTime;
            [NonSerialized]
            public string changes;

#pragma warning disable 649
            public string html_url;
            public string tag_name;
            public string name;
            public string published_at;
            public string body;
#pragma warning restore 649

            public static VersionInfo FromJSON(string json)
            {
                VersionInfo versionInfo = JsonUtility.FromJson<VersionInfo>(json);
                versionInfo.version = new System.Version(versionInfo.tag_name);
                versionInfo.publishedDateTime = DateTime.Parse(versionInfo.published_at);
                versionInfo.changes = versionInfo.body;
                versionInfo.body = null;

                versionInfo.changes = Regex.Replace(versionInfo.changes, @"(?<!(\r\n){2}) \*.*\*{2}(.*)\*{2}", "\n<size=13>$2</size>");
                versionInfo.changes = Regex.Replace(versionInfo.changes, @"(\r\n){2} \*.*\*{2}(.*)\*{2}", "\n\n<size=13>$2</size>");
                versionInfo.changes = new Regex(@"(#+)\s?(.*)\b").Replace(
                    versionInfo.changes,
                    match => string.Format(
                        "<size={0}>{1}</size>",
                        Math.Max(8, 24 - match.Groups[1].Value.Length * 6),
                        match.Groups[2].Value
                    )
                );
                versionInfo.changes = Regex.Replace(versionInfo.changes, @"(\*\s+)\b", "  • ");

                return versionInfo;
            }
        }

        private const double checkUpdateHours = 4f;
        private const string latestReleaseGitHubURL = "https://api.github.com/repos/arvi-vr/unity-playarea/releases/latest";

        private static DateTime lastCheckTime;
        private static bool lastCheckTimeCached;

        private static bool shouldCheckForUpdates;
        private static bool shouldCheckForUpdatesCached;

        private static bool isManualCheck = false;

        private const string LAST_UPDATE_CHECK_KEY = "ARVI.PlayArea.LastUpdateCheck";
        public const string SKIPPED_VERSION_KEY = "ARVI.PlayArea.SkippedVersion";
        public const string SHOULD_CHECK_UPDATES_KEY = "ARVI.PlayArea.CheckUpdates";

#if UNITY_2017_4_OR_NEWER
        static UnityWebRequest versionInfoRequest;
#else
        static WWW versionInfoRequest;
#endif

        public static DateTime LastCheckTime
        {
            get
            {
                try
                {
                    if (lastCheckTimeCached)
                        return lastCheckTime;

                    lastCheckTime = DateTime.Parse(EditorPrefs.GetString(LAST_UPDATE_CHECK_KEY, "1/1/1971 00:00:01"), CultureInfo.InvariantCulture);
                    lastCheckTimeCached = true;
                }
                catch (FormatException)
                {
                    LastCheckTime = DateTime.UtcNow;
                }
                return lastCheckTime;
            }
            private set
            {
                lastCheckTime = value;
                EditorPrefs.SetString(LAST_UPDATE_CHECK_KEY, lastCheckTime.ToString(CultureInfo.InvariantCulture));
            }
        }

        public static bool ShouldCheckForUpdates
        {
            get
            {
                try
                {
                    if (shouldCheckForUpdatesCached)
                        return shouldCheckForUpdates;

                    shouldCheckForUpdates = EditorPrefs.GetBool(SHOULD_CHECK_UPDATES_KEY, true);
                    shouldCheckForUpdatesCached = true;
                }
                catch (FormatException)
                {
                    LastCheckTime = DateTime.UtcNow;
                }
                return shouldCheckForUpdates;
            }
            set
            {
                shouldCheckForUpdates = value;
                EditorPrefs.SetBool(SHOULD_CHECK_UPDATES_KEY, shouldCheckForUpdates);
            }
        }

        static PlayAreaUpdateChecker()
        {
            EditorApplication.update += UpdateCheckLoop;
        }

        static void UpdateCheckLoop()
        {
            if (!ShouldCheckForUpdates || !CheckForUpdates())
                EditorApplication.update -= UpdateCheckLoop;
        }

        static bool CheckForUpdates()
        {
            if (versionInfoRequest != null && versionInfoRequest.isDone)
            {
                if (!string.IsNullOrEmpty(versionInfoRequest.error))
                {
                    Debug.LogWarning("There was an error checking for updates to the ARVI Play Area: " + versionInfoRequest.error);
                    versionInfoRequest = null;
                    return false;
                }
#if UNITY_2017_4_OR_NEWER
                HandleVersionInfoResponse(versionInfoRequest.downloadHandler.text);
                versionInfoRequest.Dispose();
#else
                HandleVersionInfoResponse(versionInfoRequest.text);
#endif
                versionInfoRequest = null;
            }

            var minutesUntilUpdate = LastCheckTime.AddHours(checkUpdateHours).Subtract(DateTime.UtcNow).TotalMinutes;
            if (minutesUntilUpdate < 0)
            {
#if UNITY_2017_4_OR_NEWER
                versionInfoRequest = UnityWebRequest.Get(latestReleaseGitHubURL);
                versionInfoRequest.SendWebRequest();
#else
                versionInfoRequest = new WWW(latestReleaseGitHubURL);
#endif
                LastCheckTime = DateTime.UtcNow;
            }

            return versionInfoRequest != null;
        }

        static void HandleVersionInfoResponse(string data)
        {
            var versionInfo = VersionInfo.FromJSON(data);
            var latestVersion = versionInfo.version;
            var currentVersion = new System.Version(PlayArea.Version);
            var skippedVersion = new System.Version(EditorPrefs.GetString(SKIPPED_VERSION_KEY, PlayArea.Version));
            var isNewVersion = latestVersion > currentVersion;
            if (isManualCheck || (isNewVersion && (latestVersion != skippedVersion)))
            {
                if (!isManualCheck)
                    EditorPrefs.DeleteKey(SKIPPED_VERSION_KEY);
                PlayAreaUpdateWindow.Show(versionInfo.version, versionInfo.changes, versionInfo.publishedDateTime, versionInfo.html_url, isNewVersion);
                isManualCheck = false;
            }
        }

        [MenuItem("ARVI/Play Area/Check For Update", false, 102)]
        static void ManualUpdateCheck()
        {
            if (versionInfoRequest == null)
            {
                isManualCheck = true;
                LastCheckTime = DateTime.UtcNow.AddHours(-24f);
                EditorApplication.update -= UpdateCheckLoop;
                EditorApplication.update += UpdateCheckLoop;
            }
        }
    }
}