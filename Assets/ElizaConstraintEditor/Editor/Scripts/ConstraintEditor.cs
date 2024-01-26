using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations;
using System.Linq;

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

/*
 *  TODO:
 *      - Add button to auto select specific constraints. I cant replace everything the inspector has.
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
                else if (!obj.transform.TryFindChild(ArmatureName, out armature))
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

                // Always refresh data when selection changes.
                currentArmature.RefreshConstraintData();
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
                EditorGUILayout.Space(5);
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
        Transform targetTransform;
        List<Transform> sources = new List<Transform>();

        void DrawDebugInterface()
        {
            EditorGUILayout.LabelField("DEBUG:");
            WidthMod = EditorGUILayout.IntField("WidthMod", WidthMod);
            xMod = EditorGUILayout.IntField("xMod", xMod);
            StringMod = EditorGUILayout.TextField("StringMod", StringMod);
            EditorGUILayout.Space(5);

            //DebugCenteredRectGroup();
            DebugRotationConstraintOffset();

            if (EditorUtility.CenteredButton("Refresh Armature Data", 250))
            {
                if (currentArmature is not null)
                    currentArmature.RefreshConstraintData();
            }
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
        void DebugRotationConstraintOffset()
        {
            targetTransform = EditorGUILayout.ObjectField("Target", targetTransform, typeof(Transform), true) as Transform;

            EditorGUILayout.LabelField("Additional Sources:");

            EditorGUI.indentLevel += 1;
            Transform t = EditorGUILayout.ObjectField("Add: ", null, typeof(Transform), true) as Transform;
            if (t != null) sources.Add(t);

            for (int i = 0; i < sources.Count; ++i)
            {
                using (new GUILayout.HorizontalScope())
                {
                    sources[i] = EditorGUILayout.ObjectField(sources[i], typeof(Transform), true) as Transform;
                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                        sources.RemoveAt(i--);
                }
            }

            EditorGUI.indentLevel -= 1;

            if (targetTransform is not null && sources.Any())
            {
                Quaternion offset = targetTransform.rotation;

                //EditorGUILayout.Vector3Field("Offset Angles", offset.eulerAngles);

                for (int i = 0; i < sources.Count; ++i)
                    offset = Quaternion.Inverse(Quaternion.Inverse(offset) * sources[i].localRotation);
                //offset = Quaternion.Inverse(offset);

                EditorGUILayout.Vector4Field("Offset Quaternion", offset.ToVector4());

                if (currentArmature is not null && currentArmature.allConstraintData.Any())
                {
                    var constraintOffset = currentArmature.allConstraintData[0].Constraint.rotationOffset;
                    Quaternion fromConstraint = Quaternion.Euler(constraintOffset);
                    //EditorGUILayout.Vector3Field("Constraint Angles", constraintOffset);
                    EditorGUILayout.Vector4Field("Constraint Quaternion", fromConstraint.ToVector4());
                }
            }
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
                    error.message = $"Transform {cData.PathFromArmature}/{cData.Constraint.name} is missing a constraint!";
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

                templateName = EditorGUILayout.TextField("Template Name", templateName);
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
                    else allConstraintData[i].DrawFields();
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
                DrawToggleSetting(ref settings.AdvancedMode,
                    "Advanced Mode",
                    "If enabled, the editor removes some protections and allows you to edit constraint data more freely.");

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
                allConstraintData.Add(new ConstraintMetaData(constraint));
        }

        public void AddConstraint(RotationConstraint constraint)
        {
            ConstraintMetaData cData = new ConstraintMetaData(constraint);

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
