using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations;
using Unity.VisualScripting;
using System.Security.Cryptography.X509Certificates;

/*
 *  Made for Eliza (and whomever she shares this with) by Silvainius
 *  
 *  I heavily referenced code and design from Dreadrith's fork of s-m-k's Animation Heirarchy:
 *  https://github.com/Dreadrith/Unity-Animation-Hierarchy-Editor
 *  
 *  If you're wondering what "helpbox", "in bigtitle", and other magic strings are, well so was I! Here is what I found on them:
 *      - Maybe Checkout UnityEngine.GUISKin.BuildStyleCache(), it has a few built-in engine styles.
 *          - Engine "skins" also have their own styles (and presumably style overrides), so that its at best VERY incomplete.
 *      - https://stackoverflow.com/a/43730992 Just a list of random ones. No sources for them provided.
 *      - https://discussions.unity.com/t/what-are-the-editor-resources-by-name-for-editorguiutility-load-method/116914/2 Found them in decompiled code?
 *  
 *  Some resources on custom editor icons (in case I forget):
 *      - https://forum.unity.com/threads/how-to-add-the-icon-in-editorwindow-tab.29075/#post-2442771
 *  
 */

namespace Eliza.ConstraintEditor
{
    public class ConstraintEditor : EditorWindow
    {
        enum EditorState { Standard, Settings }
        
        const string ArmatureName = "Armature";

        bool UserAddingSource = false;
        string templateName = "ShortStack";
        Vector2 scrollPos = Vector2.zero;
        GameObject currentObject;
        Transform currentArmature;
        List<ConstraintMetaData> constraints = new List<ConstraintMetaData>();

        EditorState state = EditorState.Standard;

        #region GUI Styles
        GUIStyle BoldText = new GUIStyle("boldLabel");
        #endregion

        #region Debug
        bool EnableDebugMode = true;
        int WidthMod = 80;
        int xMod = 0;
        string StringMod = string.Empty;

        void DrawDebugInterface()
        {
            EditorGUILayout.LabelField("DEBUG:");
            WidthMod = EditorGUILayout.IntField("WidthMod", WidthMod);
            xMod = EditorGUILayout.IntField("xMod", xMod);
            StringMod = EditorGUILayout.TextField("StringMod", StringMod);
            EditorGUILayout.Space(10);

            //using (new GUILayout.HorizontalScope())
            //{
            //    Rect yesButtonRect = EditorGUILayout.GetControlRect();
            //    Rect noButtonRect = EditorGUILayout.GetControlRect();
            //    float center = yesButtonRect.max.x + (noButtonRect.min.x - yesButtonRect.max.x) / 2;

            //    yesButtonRect.xMin = center - 125;
            //    noButtonRect.xMax = center + 125;

            //    GUI.Button(yesButtonRect, "Yes");
            //    GUI.Button(noButtonRect, "No");
            //}

            //var rGroup = EditorUtility.GetCenteredRectGroupHorizontal(xMod, WidthMod, 5);
            //for (int i = 0; i < rGroup.Length; ++i)
            //    GUI.Button(rGroup[i], i.ToString());

            EditorGUILayout.Space(10);
        }
        #endregion

        [MenuItem("Window/Constraint Editor")]
        static void ShowWindow()
        {
            var window = GetWindow<ConstraintEditor>();
            //string iconPath = "Assets/ElizaConstraintEditor/ElizaCrystal_icon_16x16.png";
            //Texture icon = AssetDatabase.LoadAssetAtPath(iconPath, typeof(Texture)) as Texture;
            window.titleContent = new GUIContent("Constraint Editor" /*, icon*/);
        }

        private void OnSelectionChange()
        {
            GameObject obj = Selection.activeGameObject;
            Transform armature = null;

            if (obj is not null)
            {
                // If we selected an armature, we're done!
                if (obj.name == ArmatureName)
                {
                    armature = obj.transform;
                    obj = armature.parent.gameObject;
                }
                // If we dont find an armature below us
                else if (!EditorUtility.TryFindChild(obj.transform, ArmatureName, out armature))
                {
                    // Look for an armature above us
                    Transform p = obj.transform.parent;
                    while (p is not null && armature is null)
                    {
                        if (p.name != ArmatureName)
                            p = p.parent;
                        else
                        {
                            armature = p;
                            obj = p.parent.gameObject;
                        }
                    }
                }
            }

            // Dont reconstruct data if we are just moving around the armature.
            //if (obj == currentObject && currentArmature == armature)
            //    return;

            currentObject = null;
            constraints.Clear();

            if (armature is not null)
            {
                currentObject = obj;
                currentArmature = armature;
                constraints.Clear();
                var components = armature.GetComponentsInChildren<RotationConstraint>();

                foreach (var constraint in components)
                    constraints.Add(ConstraintMetaData.Generate(constraint));
            }

            Repaint();
        }

        private void OnGUI()
        {
            if (currentObject == null)
            {
                EditorUtility.DrawTitle("Select an object with an attached armature");
                return;
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos, GUIStyle.none);

            if (EnableDebugMode)
                using (new GUILayout.VerticalScope())
                    DrawDebugInterface();

            switch(state)
            {
                case EditorState.Standard:
                    state = DrawStateStandard(); break;
                case EditorState.Settings:
                    break;
            }

            GUILayout.Space(40);
            GUILayout.EndScrollView();
        }

        #region Standard State
        private EditorState DrawStateStandard()
        {
            using (new GUILayout.VerticalScope())
            {
                EditorUtility.DrawTitle($"{currentObject.name}/Armature");

                //using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.TextField("Template Name", templateName);

                    if (GUILayout.Button($"Save to {templateName}.json"))
                        ConstraintSerializer.SaveConstraintsAsTemplate(templateName, constraints);

                    if (GUILayout.Button($"Load {templateName}.json"))
                        ConstraintSerializer.LoadConstraintsFromTempalte(templateName, currentArmature);
                }

                EditorGUILayout.LabelField("Current Constraints:");
                EditorGUI.indentLevel += 1;
                for (int i = 0; i < constraints.Count; ++i)
                {
                    // If this entry is being removed, kill it and move on.
                    if (constraints[i].RemovingState > 1)
                    {
                        DestroyImmediate(constraints[i].Constraint);
                        constraints.RemoveAt(i--);
                    }
                    else DrawConstraintFields(constraints[i]);
                }

                if (!UserAddingSource)
                    UserAddingSource = GUILayout.Button("Add Constraint");
                else
                {
                    GameObject obj = EditorGUILayout.ObjectField("Target Object", null, typeof(GameObject), true) as GameObject;
                    if (obj is not null)
                    {
                        RotationConstraint constraint = null;
                        if (!obj.TryGetComponent<RotationConstraint>(out constraint))
                            constraint = obj.AddComponent<RotationConstraint>();

                        var cData = ConstraintMetaData.Generate(constraint);
                        cData.IsExpanded = true;
                        constraints.Add(cData);
                        UserAddingSource = false;
                    }
                }
            }
            return EditorState.Standard;
        }
        private void DrawConstraintFields(ConstraintMetaData cData)
        {
            GUIStyle textStyle = new GUIStyle()
            {
                fontStyle = FontStyle.Bold,
            };

            using (new GUILayout.HorizontalScope())
            {
                Rect rect = EditorGUILayout.GetControlRect();
                Rect rect2 = EditorGUILayout.GetControlRect();
                Rect combined = new Rect(rect);

                combined.xMax = rect2.xMax;

                if (GUI.Button(combined, string.Empty))
                    cData.IsExpanded = !cData.IsExpanded;

                EditorGUI.LabelField(rect, $"{cData.PathFromArmature}/");
                EditorGUI.LabelField(rect2, cData.Constraint.name, new GUIStyle("boldLabel"));
            }

            if (cData.IsExpanded)
            {
                EditorGUI.indentLevel += 1;
                using (new GUILayout.VerticalScope())
                {
                    EditorGUILayout.Space(10);

                    if (cData.RemovingState == 1)
                    {
                        // Extra spaces bc easier than figuring what BS the control rects are doing.
                        EditorGUILayout.LabelField("ARE YOU SURE?      ", EditorUtility.DefaultTitleStyle);

                        var rGroup = EditorUtility.GetCenteredRectGroupHorizontal(2, 250, 5);
                        if (GUI.Button(rGroup[0], "Yes"))
                            cData.RemovingState = 2;
                        if (GUI.Button(rGroup[1], "No"))
                            cData.RemovingState = 0;
                    }
                    else
                    {
                        Rect deleteButtonRect = EditorUtility.GetCenteredControlRect(250);
                        cData.RemovingState = GUI.Button(deleteButtonRect, "Remove Constraint") ? 1 : 0;
                    }

                    string activeButtonLabel = cData.Constraint.constraintActive
                        ? "Constraint ON"
                        : "Constraint OFF";
                    Rect activeButtonRect = EditorUtility.GetCenteredControlRect(250);
                    if (GUI.Button(activeButtonRect, activeButtonLabel))
                        cData.Constraint.constraintActive = !cData.Constraint.constraintActive;


                    cData.Constraint.weight = EditorGUILayout.Slider("Weight", cData.Constraint.weight, 0.0f, 1.0f);
                    cData.Constraint.locked = EditorGUILayout.Toggle("Lock", cData.Constraint.locked);

                    if (cData.Constraint.locked)
                        cData.Constraint.rotationAxis = (Axis)EditorGUILayout.EnumFlagsField("Freeze Axis", cData.Constraint.rotationAxis);
                    else
                    {
                        cData.Constraint.rotationAtRest = EditorUtility.DrawCustomVector3("Rotation at Rest", cData.Constraint.rotationAtRest);
                        cData.Constraint.rotationOffset = EditorUtility.DrawCustomVector3("Rotation Offset", cData.Constraint.rotationOffset);
                    }

                    EditorGUILayout.LabelField("Sources:");
                    EditorGUI.indentLevel += 1;
                    using (new GUILayout.VerticalScope("helpbox"))
                    {
                        List<ConstraintSource> sources = new List<ConstraintSource>(cData.Constraint.sourceCount);
                        cData.Constraint.GetSources(sources);

                        float weightFieldMinX = 0;
                        Rect targetsLabelRect;
                        Rect weightsLabelRect;
                        Rect targetFieldRect;
                        Rect weightFieldRect;

                        // Get the control rects now, since they wont allign with the sources.
                        using (new GUILayout.HorizontalScope())
                        {
                            targetsLabelRect = targetFieldRect = EditorGUILayout.GetControlRect();
                            weightsLabelRect = weightFieldRect = EditorGUILayout.GetControlRect();
                        }

                        for (int i = 0; i < sources.Count; i++)
                        {
                            ConstraintSource source = sources[i];
                            using (new GUILayout.HorizontalScope())
                            {
                                // Save rects so we can align things later.
                                targetFieldRect = EditorGUILayout.GetControlRect();
                                weightFieldRect = EditorGUILayout.GetControlRect(GUILayout.Width(100));

                                if (GUILayout.Button("Remove", GUILayout.Width(65)))
                                    cData.Constraint.RemoveSource(i);

                                float newWeight = EditorGUI.FloatField(weightFieldRect, source.weight);
                                Transform newSource = EditorGUI.ObjectField(targetFieldRect, source.sourceTransform, typeof(Transform), true) as Transform;

                                if (newWeight != source.weight || newSource != source.sourceTransform)
                                    cData.Constraint.SetSource(i, new ConstraintSource() { sourceTransform = newSource, weight = newWeight });

                                weightFieldMinX = weightFieldRect.xMin;
                            }
                        }

                        if (sources.Count > 0)
                            weightsLabelRect.xMin = weightFieldMinX;

                        EditorGUI.LabelField(targetsLabelRect, "Targets");
                        EditorGUI.LabelField(weightsLabelRect, "Weights");

                        Rect sourceButtonRect = EditorGUILayout.GetControlRect();
                        sourceButtonRect.max = new Vector2(weightFieldRect.max.x, sourceButtonRect.max.y);
                        if (GUI.Button(EditorGUI.IndentedRect(sourceButtonRect), "Add Source"))
                            cData.Constraint.AddSource(new ConstraintSource() { sourceTransform = null, weight = 1.0f });
                    }
                    EditorGUI.indentLevel -= 1;
                }
                EditorGUI.indentLevel -= 1;

                EditorGUILayout.Space(5);
            }
        }
        #endregion

        #region Settings State
        private EditorState DrawSettingsState()
        {

            return EditorState.Settings;
        }
        #endregion
    }

    public class ConstraintMetaData
    {
        public bool IsExpanded;
        public int RemovingState = 0;
        public string FullPath;
        public string[] SplitPath;
        public RotationConstraint Constraint;

        public string PathFromArmature
        {
            get
            {
                if (pathFromArmature is not null && pathFromArmature.Length > 0)
                    return pathFromArmature;

                string path = SplitPath[2];
                for (int i = 3; i < SplitPath.Length - 1; ++i)
                    path = $"{path}/{SplitPath[i]}";
                pathFromArmature = path;
                return pathFromArmature;
            }
        }

        private string pathFromArmature;

        public static ConstraintMetaData Generate(RotationConstraint constraint)
        {
            Transform parent = constraint.transform.parent;
            string fullPath = constraint.name;

            while (parent is not null)
            {
                fullPath = $"{parent.name}/{fullPath}";
                parent = parent.parent;
            }

            return new ConstraintMetaData()
            {
                IsExpanded = false,
                FullPath = fullPath,
                SplitPath = fullPath.Split('/'),
                Constraint = constraint
            };
        }
    }

    public class ConstraintEditorSettings
    {
        public bool EnableDebugMode = false;
        public bool DestroyConstraintsOnLoad = false;
    }
}
