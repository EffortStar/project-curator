using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;

namespace Ogxd.ProjectCurator
{
    public static class ProjectCurator
    {
        [NonSerialized]
        private static Dictionary<GUID, AssetInfo> guidToAssetInfo;

        static ProjectCurator()
        {
            guidToAssetInfo = new Dictionary<GUID, AssetInfo>();
            try {
                var assetInfos = ProjectCuratorData.AssetInfos;
                for (int i = 0; i < assetInfos.Length; i++) {
                    guidToAssetInfo.Add(assetInfos[i].Guid, assetInfos[i]);
                }
            } catch (Exception e) {
                Debug.LogError($"An error occurred while loading ProjectCurator database: {e}");
            }
        }

        public static AssetInfo GetAsset(GUID guid)
        {
            guidToAssetInfo.TryGetValue(guid, out AssetInfo assetInfo);
            return assetInfo;
        }

        public static AssetInfo AddAssetToDatabase(GUID guid, HashSet<GUID> referencers = null)
        {
            AssertGuidValid(guid);

            if (!guidToAssetInfo.TryGetValue(guid, out var assetInfo)) {
                guidToAssetInfo.Add(guid, assetInfo = new(guid));
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            var dependencyPaths = AssetDatabase.GetDependencies(path, recursive: false);

            foreach (string dependencyPath in dependencyPaths) {
                var dependencyGuid = AssetDatabase.GUIDFromAssetPath(dependencyPath);
                if (
                    dependencyGuid != assetInfo.Guid &&
                    guidToAssetInfo.TryGetValue(dependencyGuid, out AssetInfo depInfo)
                ) {
                    assetInfo.Dependencies.Add(dependencyGuid);
                    depInfo.Referencers.Add(assetInfo.Guid);
                    // Included status may have changed and need to be recomputed
                    depInfo.ClearIncludedStatus();
                }
            }

            if (referencers != null)
                assetInfo.Referencers = referencers;

            return assetInfo;
        }

        public static AssetInfo RemoveAssetFromDatabase(GUID guid)
        {
            AssertGuidValid(guid);

            if (guidToAssetInfo.TryGetValue(guid, out AssetInfo assetInfo)) {
	            // Go through everything known to reference this asset,
	            // and remove it as a dependency.
                foreach (GUID referencer in assetInfo.Referencers) {
                    if (guidToAssetInfo.TryGetValue(referencer, out AssetInfo referencerAssetInfo)) {
                        if (referencerAssetInfo.Dependencies.Remove(guid)) {
                            referencerAssetInfo.ClearIncludedStatus();
                        } else {
                            // Non-Reciprocity Error
                            if ((ProjectCuratorPreferences.instance.WarningVisibility & Warnings.NonReciprocity) != 0)
								Warn($"Asset '{FormatGuid(referencer)}' that depends on '{FormatGuid(guid)}' doesn't have it as a dependency");
                        }
                    } else {
	                    if ((ProjectCuratorPreferences.instance.WarningVisibility & Warnings.NotPresentInDatabase) != 0)
							Warn($"Asset '{FormatGuid(referencer)}' that depends on '{FormatGuid(guid)}' is not present in the database");
                    }
                }
                // Go through everything known to be referenced by this asset,
                // and remove this asset as a referencer.
                foreach (GUID dependency in assetInfo.Dependencies) {
                    if (guidToAssetInfo.TryGetValue(dependency, out AssetInfo dependencyAssetInfo)) {
                        if (dependencyAssetInfo.Referencers.Remove(guid)) {
                            dependencyAssetInfo.ClearIncludedStatus();
                        } else {
                            // Non-Reciprocity Error
                            if ((ProjectCuratorPreferences.instance.WarningVisibility & Warnings.NonReciprocity) != 0)
								Warn($"Asset '{FormatGuid(dependency)}' that is referenced by '{FormatGuid(guid)}' doesn't have it as a referencer");
                        }
                    } else {
	                    if ((ProjectCuratorPreferences.instance.WarningVisibility & Warnings.NotPresentInDatabase) != 0)
							Warn($"Asset '{FormatGuid(dependency)}' that is referenced by '{FormatGuid(guid)}' is not present in the database");
                    }
                }
                guidToAssetInfo.Remove(guid);
            } else {
	            if ((ProjectCuratorPreferences.instance.WarningVisibility & Warnings.NotPresentInDatabase) != 0)
					Warn($"Asset '{FormatGuid(guid)}' is not present in the database");
            }

            return assetInfo;
        }

        public static void ClearDatabase()
        {
            guidToAssetInfo.Clear();
        }

        public static void RebuildDatabase()
        {
            guidToAssetInfo = new Dictionary<GUID, AssetInfo>();

            var allAssetPaths = AssetDatabase.GetAllAssetPaths();

            // Ignore non-assets (package folder for instance) and directories
            allAssetPaths = allAssetPaths
                .Where(path => path.StartsWith("Assets/") && !Directory.Exists(path))
                .ToArray();

            EditorUtility.DisplayProgressBar("Building Dependency Database", "Gathering All Assets...", 0f);

            // Gather all assets
            for (int p = 0; p < allAssetPaths.Length; p++) {
                string path = allAssetPaths[p];
                GUID guid = AssetDatabase.GUIDFromAssetPath(path);
                AssetInfo assetInfo = new AssetInfo(guid);
                guidToAssetInfo.Add(assetInfo.Guid, assetInfo);
            }

            // Find links between assets
            for (int p = 0; p < allAssetPaths.Length; p++) {
                var path = allAssetPaths[p];
                if (p % 10 == 0) {
                    var cancel = EditorUtility.DisplayCancelableProgressBar("Building Dependency Database", path, (float)p / allAssetPaths.Length);
                    if (cancel) {
                        guidToAssetInfo = null;
                        break;
                    }
                }
                GUID guid = AssetDatabase.GUIDFromAssetPath(path);
                AddAssetToDatabase(guid);
            }

            EditorUtility.ClearProgressBar();

            ProjectCuratorData.IsUpToDate = true;

            SaveDatabase();
        }

        public static void SaveDatabase()
        {
            if (guidToAssetInfo == null)
                return;
            var assetInfos = new AssetInfo[guidToAssetInfo.Count];
            int i = 0;
            foreach (var pair in guidToAssetInfo) {
                assetInfos[i] = pair.Value;
                i++;
            }
            ProjectCuratorData.AssetInfos = assetInfos;
            ProjectCuratorData.Save();
        }

        static void Warn(string message)
        {
            Debug.LogWarning("ProjectCurator: " + message);
        }

        static string FormatGuid(GUID guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path)
                ? $"(Missing asset with GUID={guid})"
                : path;
        }

        static void AssertGuidValid(GUID guid)
        {
            if (guid.Empty()) {
                throw new ArgumentException("GUID is empty", nameof(guid));
            }
        }
    }
}