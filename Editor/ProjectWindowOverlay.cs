﻿using UnityEditor;
using UnityEngine;

namespace Ogxd.ProjectCurator
{
    [InitializeOnLoad]
    public static partial class ProjectWindowOverlay
    {
        static ProjectWindowOverlay()
        {
            enabled = Enabled;
            EditorApplication.projectWindowItemOnGUI += ProjectWindowItemOnGUI;
        }

        private static void ProjectWindowItemOnGUI(string guidStr, Rect rect)
        {
            if (enabled)
            {
                GUID.TryParse(guidStr, out GUID guid);
                AssetInfo assetInfo = ProjectCurator.GetAsset(guid);
                if (assetInfo != null) {
                    var content = new GUIContent(assetInfo.IsIncludedInBuild ? ProjectIcons.LinkBlue : ProjectIcons.LinkBlack, assetInfo.IncludedStatus.ToString());
                    GUI.Label(new Rect(rect.width + rect.x - 20, rect.y + 1, 16, 16), content);
                }
            }
        }

        private static bool enabled;

        public static bool Enabled {
            get {
                return enabled = EditorPrefs.GetBool("ProjectCurator_PWO");
            }
            set {
                EditorPrefs.SetBool("ProjectCurator_PWO", enabled = value);
            }
        }
    }
}