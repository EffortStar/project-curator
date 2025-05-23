﻿using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Ogxd.ProjectCurator
{
    public class ProjectCuratorWindow : EditorWindow, IHasCustomMenu
    {

        [MenuItem("Window/Project Curator")]
        static void Init()
        {
            GetWindow<ProjectCuratorWindow>("Project Curator");
        }

        public ProjectCuratorWindow()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            Repaint();
        }

        private Vector2 scroll;

        private bool dependenciesOpen = true;
        private bool referencesOpen = true;

        private static GUIStyle titleStyle;
        private static GUIStyle TitleStyle => titleStyle ?? (titleStyle = new GUIStyle(EditorStyles.label) { fontSize = 13 });

        private static GUIStyle itemStyle;
        private static GUIStyle ItemStyle => itemStyle ?? (itemStyle = new GUIStyle(EditorStyles.label) { margin = new RectOffset(32, 0, 0, 0) });

        private void OnGUI()
        {
	        if (EditorApplication.isCompiling)
	        {
		        EditorGUILayout.HelpBox("Currently recompiling..", MessageType.Info);
		        return;
	        }
	        
	        if (EditorApplication.isUpdating)
	        {
		        EditorGUILayout.HelpBox("Currently refreshing the asset database...", MessageType.Info);
		        return;
	        }
	        
            string selectedPath = AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
            if (string.IsNullOrEmpty(selectedPath))
                return;

            // Standard spacing to mimic Unity's Inspector header
            GUILayout.Space(2);

            Rect rect;

            GUILayout.BeginHorizontal("In BigTitle");
            GUILayout.Label(AssetDatabase.GetCachedIcon(selectedPath), GUILayout.Width(36), GUILayout.Height(36));
            GUILayout.BeginVertical();
            GUILayout.Label(Path.GetFileName(selectedPath), TitleStyle);
            // Display directory (without "Assets/" prefix)
            GUILayout.Label(Regex.Match(Path.GetDirectoryName(selectedPath), "(\\\\.*)$").Value);
            rect = GUILayoutUtility.GetLastRect();
            GUILayout.EndVertical();
            GUILayout.Space(44);
            GUILayout.EndHorizontal();

            if (Directory.Exists(selectedPath))
                return;

            GUID guid = AssetDatabase.GUIDFromAssetPath(selectedPath);
            AssetInfo selectedAssetInfo = ProjectCurator.GetAsset(guid);
            if (selectedAssetInfo == null) {
                if (selectedPath.StartsWith("Assets"))
                {
                    bool rebuildClicked = HelpBoxWithButton(new GUIContent("You must rebuild database to obtain information on this asset", EditorGUIUtility.IconContent("console.warnicon").image), new GUIContent("Rebuild Database"));
                    if (rebuildClicked)
                    {
                        ProjectCurator.RebuildDatabase();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Project Curator ignores assets that are not in the Asset folder.", MessageType.Warning);
                }

                return;
            }

            var content = new GUIContent(selectedAssetInfo.IsIncludedInBuild ? ProjectIcons.LinkBlue : ProjectIcons.LinkBlack, selectedAssetInfo.IncludedStatus.ToString());
            GUI.Label(new Rect(position.width - 20, rect.y + 1, 16, 16), content);

            scroll = GUILayout.BeginScrollView(scroll);

            dependenciesOpen = EditorGUILayout.Foldout(dependenciesOpen, $"Dependencies ({selectedAssetInfo.Dependencies.Count})");
            if (dependenciesOpen) {
                foreach (var dependency in selectedAssetInfo.Dependencies) {
                    RenderOtherAsset(dependency);
                }
            }

            GUILayout.Space(6);

            referencesOpen = EditorGUILayout.Foldout(referencesOpen, $"Referencers ({selectedAssetInfo.Referencers.Count})");
            if (referencesOpen) {
                foreach (var referencer in selectedAssetInfo.Referencers) {
                    RenderOtherAsset(referencer);
                }
            }

            GUILayout.Space(5);

            GUILayout.EndScrollView();

            if (!selectedAssetInfo.IsIncludedInBuild) {
                bool deleteClicked = HelpBoxWithButton(new GUIContent("This asset is not referenced and never used. Would you like to delete it ?", EditorGUIUtility.IconContent("console.warnicon").image), new GUIContent("Delete Asset"));
                if (deleteClicked) {
                    File.Delete(selectedPath);
                    AssetDatabase.Refresh();
                    GUID guidDeleted = AssetDatabase.GUIDFromAssetPath(selectedPath);
                    ProjectCurator.RemoveAssetFromDatabase(guidDeleted);
                }
            }
        }

        void RenderOtherAsset(GUID guid) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (GUILayout.Button(new GUIContent(Path.GetFileName(path), path), ItemStyle)) {
                UnityEditor.Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            }
            var rect = GUILayoutUtility.GetLastRect();
            GUI.DrawTexture(new Rect(rect.x - 16, rect.y, rect.height, rect.height), AssetDatabase.GetCachedIcon(path));
            AssetInfo assetInfo = ProjectCurator.GetAsset(guid);
            var content = new GUIContent(assetInfo.IsIncludedInBuild ? ProjectIcons.LinkBlue : ProjectIcons.LinkBlack, assetInfo.IncludedStatus.ToString());
            GUI.Label(new Rect(rect.width + rect.x - 20, rect.y + 1, 16, 16), content);
        }

        void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Rebuild Database"), false, ProjectCurator.RebuildDatabase);
            menu.AddItem(new GUIContent("Clear Database"), false, ProjectCurator.ClearDatabase);
            menu.AddItem(new GUIContent("Project Overlay"), ProjectWindowOverlay.Enabled, () => { ProjectWindowOverlay.Enabled = !ProjectWindowOverlay.Enabled; });
        }

        public bool HelpBoxWithButton(GUIContent messageContent, GUIContent buttonContent)
        {
            float buttonWidth = buttonContent.text.Length * 8;
            const float buttonSpacing = 5f;
            const float buttonHeight = 18f;

            // Reserve size of wrapped text
            Rect contentRect = GUILayoutUtility.GetRect(messageContent, EditorStyles.helpBox);
            // Reserve size of button
            GUILayoutUtility.GetRect(1, buttonHeight + buttonSpacing);

            // Render background box with text at full height
            contentRect.height += buttonHeight + buttonSpacing;
            GUI.Label(contentRect, messageContent, EditorStyles.helpBox);

            // Button (align lower right)
            Rect buttonRect = new Rect(contentRect.xMax - buttonWidth - 4f, contentRect.yMax - buttonHeight - 4f, buttonWidth, buttonHeight);
            return GUI.Button(buttonRect, buttonContent);
        }
    }
}