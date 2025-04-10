using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ogxd.ProjectCurator
{
    [Serializable]
    public class ProjectCuratorData
    {
        private const string JSON_PATH = "UserSettings/ProjectCuratorData.json";

        [SerializeField]
        bool isUpToDate = false;

        public static bool IsUpToDate {
            get => Instance.isUpToDate;
            set => Instance.isUpToDate = value;
        }

        [SerializeField]
        AssetInfo[] _assetInfos;

        public static AssetInfo[] AssetInfos {
            get => Instance._assetInfos ?? (Instance._assetInfos = Array.Empty<AssetInfo>());
            set => Instance._assetInfos = value;
        }

        private static ProjectCuratorData instance;
        public static ProjectCuratorData Instance {
            get {
                if (instance == null) {
                    if (File.Exists(JSON_PATH)) {
                        var json = File.ReadAllText(JSON_PATH);
                        instance = new ProjectCuratorData();
                        EditorJsonUtility.FromJsonOverwrite(json, instance);
                    } else {
                        instance = new ProjectCuratorData();
                    }
                }
                return instance;
            }
        }

        public static void Save()
        {
            var json = EditorJsonUtility.ToJson(Instance, false);
            File.WriteAllText(JSON_PATH, json);
        }
    }
}