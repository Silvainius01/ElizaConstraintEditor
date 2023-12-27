using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations;

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
    public enum EditorState { Editor, Settings }
    public enum ConstraintRemovalState { NotRemoving, UserConfirm, Removing }
    public enum EditorErrorCode { NoError, ArmatureNull, ParentNull, TransformNull, ConstraintNull }

    public class ConstraintEditor : EditorWindow
    {
        const string ArmatureName = "Armature";
        public static ConstraintEditorSettings Settings
        {
            get
            {
                if (m_settings is null)
                    m_settings = ConstraintSerializer.LoadSettings();
                return m_settings;
            }
        }
        private static ConstraintEditorSettings m_settings;

        bool UserAddingSource = false;
        string templateName = "ShortStack";
        Vector2 scrollPos = Vector2.zero;

        int currentArmatureId;
        ArmatureData currentArmature => trackedArmatures.ContainsKey(currentArmatureId)
            ? trackedArmatures[currentArmatureId]
            : null;
        Dictionary<int, ArmatureData> trackedArmatures = new Dictionary<int, ArmatureData>();


        EditorState state = EditorState.Editor;

        #region GUI Styles
        GUIStyle BoldText = new GUIStyle("boldLabel");
        #endregion

        [MenuItem("Window/Constraint Editor")]
        static void ShowWindow()
        {
            var window = GetWindow<ConstraintEditor>();
            //string iconPath = "Assets/ElizaConstraintEditor/ElizaCrystal_icon_16x16.png";
            //Texture icon = AssetDatabase.LoadAssetAtPath(iconPath, typeof(Texture)) as Texture;
            window.titleContent = new GUIContent("Constraint Editor" /*, icon*/);

            window.antiAlias = 1; // Fixes getting spammed by an error.
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

            if (armature is not null)
            {
                int id = armature.gameObject.GetInstanceID();
                if (!trackedArmatures.ContainsKey(id))
                    trackedArmatures.Add(id, new ArmatureData(armature));
                currentArmatureId = id;
            }

            Repaint();
        }

        private void OnGUI()
        {
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUIStyle.none);

            EditorUtility.DrawTitle("Eliza Constraint Editor");
            EditorGUILayout.Space(10);

            if (Settings.EnableDebugMode && EditorUtility.ToggleButton(
                EditorUtility.GetCenteredControlRect(250),
                ref EnableDebugMode,
                trueLable: "Hide Debug",
                falseLable: "Show Debug"))
            {
                using (new GUILayout.VerticalScope("helpbox"))
                {
                    DrawDebugInterface();
                }
            }

            EditorState prevState = state;
            if (state != EditorState.Settings && EditorUtility.CenteredButton("Editor Settings", 250))
                state = EditorState.Settings;
            else if (state != EditorState.Editor && EditorUtility.CenteredButton("Show Constraints", 250))
                state = EditorState.Editor;

            if (prevState != state && prevState == EditorState.Settings)
                ConstraintSerializer.SaveSettings(Settings);

            switch (state)
            {
                case EditorState.Editor:
                    DrawStateStandard(); break;
                case EditorState.Settings:
                    DrawStateSettings(); break;
            }

            GUILayout.Space(40);
            GUILayout.EndScrollView();
        }

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

            //DebugCenteredRectGroup();
        }
        void DebugCenteredRectGroup()
        {
            using (new GUILayout.HorizontalScope())
            {
                Rect yesButtonRect = EditorGUILayout.GetControlRect();
                Rect noButtonRect = EditorGUILayout.GetControlRect();
                float center = yesButtonRect.max.x + (noButtonRect.min.x - yesButtonRect.max.x) / 2;

                yesButtonRect.xMin = center - 125;
                noButtonRect.xMax = center + 125;

                GUI.Button(yesButtonRect, "Yes");
                GUI.Button(noButtonRect, "No");
            }

            var rGroup = EditorUtility.GetCenteredRectGroupHorizontal(xMod, WidthMod, 5);
            for (int i = 0; i < rGroup.Length; ++i)
                GUI.Button(rGroup[i], i.ToString());
        }
        #endregion

        private bool IsValidArmature(ArmatureData aramature, out ConstraintEditorError error)
        {
            error = new ConstraintEditorError();

            if (aramature is null)
            {
                error.message = "Armature data is null!";
                error.code = EditorErrorCode.ArmatureNull;
                return false;
            }
            if (aramature.parentObject is null)
            {
                error.message = "Armature parent object is missing!";
                error.code = EditorErrorCode.ParentNull;
                return false;
            }
            if (aramature.armatureTransform is null)
            {
                error.message = "Armature transorm is missing!";
                error.code = EditorErrorCode.TransformNull;
                return false;
            }

            foreach (var cData in aramature.allConstraintData)
                if (cData.Constraint == null) // Why wont the pattern match `is null` work?
                {
                    error.message = $"Transform {cData.PathFromArmature} is missing a constraint!";
                    error.code = EditorErrorCode.ConstraintNull;
                    return false;
                }

            error.message = "Armature is valid";
            error.code = EditorErrorCode.NoError;
            return true;
        }

        #region Standard State
        private void DrawStateStandard()
        {
            EditorGUILayout.Space(10);

            if (!IsValidArmature(currentArmature, out var errorData))
            {
                EditorUtility.DrawTitle("Select an object with an attached armature");

                switch (errorData.code)
                {
                    // Should only be thrown when nothing is selected, an expected case.
                    case EditorErrorCode.ArmatureNull:
                        if (Settings.VerboseConsoleErrors)
                            Debug.LogError(errorData.message);
                        return;
                    case EditorErrorCode.ParentNull:
                    case EditorErrorCode.TransformNull:
                    case EditorErrorCode.ConstraintNull:
                        Debug.LogError(errorData.message);
                        return;
                    default:
                        Debug.LogError($"Uncaught editor error: {errorData.code}\nMessage: {errorData.message}");
                        return;
                }
            }

            using (new GUILayout.VerticalScope())
            {
                EditorUtility.DrawTitle($"{currentArmature.parentObject.name}/Armature");
                EditorGUILayout.Space(10);

                EditorGUILayout.TextField("Template Name", templateName);
                if (GUILayout.Button($"Save to {templateName}.json"))
                    currentArmature.SaveToTemplate(templateName);
                if (GUILayout.Button($"Load {templateName}.json"))
                {
                    if (Settings.DestroyConstraintsOnLoad)
                        currentArmature.RemoveAllConstraints();
                    currentArmature.LoadFromTemplate(templateName);
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Current Constraints:");
                EditorGUI.indentLevel += 1;

                var allConstraintData = currentArmature.allConstraintData;
                for (int i = 0; i < allConstraintData.Count; ++i)
                {
                    // If this entry is being removed, kill it and move on.
                    if (allConstraintData[i].RemovingState == ConstraintRemovalState.Removing)
                        currentArmature.RemoveConstraint(i--);
                    else DrawConstraintFields(allConstraintData[i]);
                }

                if (!UserAddingSource)
                    UserAddingSource = GUILayout.Button("Add Constraint");
                else
                {
                    GameObject obj = EditorGUILayout.ObjectField("Target Object", null, typeof(GameObject), true) as GameObject;

                    if (EditorUtility.CenteredButton("Cancel", 250))
                        UserAddingSource = false;
                    else if (obj is not null)
                    {
                        RotationConstraint constraint = null;
                        if (!obj.TryGetComponent<RotationConstraint>(out constraint))
                            constraint = obj.AddComponent<RotationConstraint>();

                        currentArmature.AddConstraint(constraint);
                        UserAddingSource = false;
                    }
                }
            }
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

                    if (cData.RemovingState == ConstraintRemovalState.UserConfirm)
                    {
                        // Extra spaces bc easier than figuring what BS the control rects are doing.
                        EditorGUILayout.LabelField("ARE YOU SURE?      ", EditorUtility.DefaultTitleStyle);

                        var rGroup = EditorUtility.GetCenteredRectGroupHorizontal(2, 250, 5);
                        if (GUI.Button(rGroup[0], "Yes"))
                            cData.RemovingState = ConstraintRemovalState.Removing;
                        if (GUI.Button(rGroup[1], "No"))
                            cData.RemovingState = ConstraintRemovalState.NotRemoving;
                    }
                    else
                    {
                        cData.RemovingState = EditorUtility.CenteredButton("Remove Constraint", 250)
                            ? ConstraintRemovalState.UserConfirm
                            : ConstraintRemovalState.NotRemoving;
                    }

                    string activeButtonLabel = cData.Constraint.constraintActive
                        ? "Constraint ON"
                        : "Constraint OFF";
                    if (EditorUtility.CenteredButton(activeButtonLabel, 250))
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
        private void DrawStateSettings()
        {
            EditorGUILayout.Space(10);
            EditorUtility.DrawTitle("Editor Settings");

            var settings = Settings;
            using (new EditorGUILayout.VerticalScope())
            {
                DrawToggleSetting(ref settings.EnableDebugMode,
                    "Debug Mode",
                    "Reveals internal developer settings. It will do nothing for you. It's for me.");
                DrawToggleSetting(ref settings.DestroyConstraintsOnLoad,
                    "Destroy Constraints On Load",
                    "If enabled, loading a template will destroy any existing constraints.");
                DrawToggleSetting(ref settings.LoadRotationData,
                    "Load Rotations",
                    "If enabled, constraint rotation data will be loaded from templates. HIGHLY RECCOMENDED to leave off.");

                if (Settings.EnableDebugMode)
                {
                    EditorGUILayout.Space(10);
                    EditorUtility.DrawTitle("Developer Settings");

                    DrawToggleSetting(ref settings.VerboseConsoleErrors,
                        "Enable All Console Errors",
                        "Turning this on will print all errors caught by the editor. Even ones that dont matter.");
                }
            }
        }
        private void DrawToggleSetting(ref bool value, string label, string tooltip)
        {
            value = EditorGUILayout.Toggle(new GUIContent(label, tooltip), value, GUILayout.ExpandWidth(true));
        }
        #endregion
    }

    public class ConstraintMetaData
    {
        public bool IsExpanded;
        public string FullPath;
        public string[] SplitPath;
        public RotationConstraint Constraint;
        public ConstraintRemovalState RemovingState = ConstraintRemovalState.NotRemoving;

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

    public class ArmatureData
    {
        public GameObject parentObject;
        public Transform armatureTransform;
        public List<ConstraintMetaData> allConstraintData = new List<ConstraintMetaData>();

        public ArmatureData(Transform armatureTransform)
        {
            parentObject = armatureTransform.parent.gameObject;
            this.armatureTransform = armatureTransform;
            RefreshConstraintData();
        }

        public void RefreshConstraintData()
        {
            var rotationConstraints = armatureTransform.GetComponentsInChildren<RotationConstraint>();

            allConstraintData.Clear();
            allConstraintData.Capacity = rotationConstraints.Length;
            foreach (var constraint in armatureTransform.GetComponentsInChildren<RotationConstraint>())
                allConstraintData.Add(ConstraintMetaData.Generate(constraint));
        }

        public void AddConstraint(RotationConstraint constraint)
        {
            ConstraintMetaData cData = ConstraintMetaData.Generate(constraint);

            cData.IsExpanded = true;
            allConstraintData.Add(cData);
        }

        public void RemoveConstraint(int index)
        {
            ConstraintMetaData cData = allConstraintData[index];
            GameObject.DestroyImmediate(cData.Constraint);
            allConstraintData.RemoveAt(index);
        }
        public void RemoveAllConstraints()
        {
            foreach (var cData in allConstraintData)
                UnityEngine.Object.DestroyImmediate(cData.Constraint);
            allConstraintData.Clear();
        }

        public void SaveToTemplate(string template)
        {
            ConstraintSerializer.SaveConstraintsAsTemplate(template, allConstraintData);
        }
        public void LoadFromTemplate(string template)
        {
            ConstraintSerializer.LoadConstraintsFromTempalte(template, armatureTransform);
            RefreshConstraintData();
        }
    }
}
