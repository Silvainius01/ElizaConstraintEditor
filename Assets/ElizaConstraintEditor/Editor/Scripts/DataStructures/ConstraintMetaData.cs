using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations;

namespace Eliza.ConstraintEditor
{
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

        public ConstraintMetaData(RotationConstraint constraint)
        {
            Transform parent = constraint.transform.parent;
            string fullPath = constraint.name;

            while (parent is not null)
            {
                fullPath = $"{parent.name}/{fullPath}";
                parent = parent.parent;
            }

            IsExpanded = false;
            FullPath = fullPath;
            SplitPath = fullPath.Split('/');
            Constraint = constraint;
        }

        public void DrawFields()
        {
            ConstraintEditorSettings settings = ConstraintEditor.Settings;

            // Expand/Collapse Button, and constraint name
            using (new GUILayout.HorizontalScope())
            {
                Rect rect = EditorGUILayout.GetControlRect();
                Rect rect2 = EditorGUILayout.GetControlRect();
                Rect combined = new Rect(rect);

                combined.xMax = rect2.xMax;

                if (GUI.Button(combined, string.Empty))
                    IsExpanded = !IsExpanded;

                EditorGUI.LabelField(rect, $"{PathFromArmature}/");
                EditorGUI.LabelField(rect2, Constraint.name, new GUIStyle("boldLabel"));
            }

            if (IsExpanded)
            {
                EditorGUI.indentLevel += 1;
                using (new GUILayout.VerticalScope())
                {
                    EditorGUILayout.Space(10);

                    if (RemovingState == ConstraintRemovalState.UserConfirm)
                    {
                        // Extra spaces bc easier than figuring what BS the control rects are doing.
                        EditorGUILayout.LabelField("ARE YOU SURE?      ", EditorUtility.DefaultTitleStyle);

                        var rGroup = EditorUtility.GetCenteredRectGroupHorizontal(2, 250, 5);
                        if (GUI.Button(rGroup[0], "Yes"))
                            RemovingState = ConstraintRemovalState.Removing;
                        if (GUI.Button(rGroup[1], "No"))
                            RemovingState = ConstraintRemovalState.NotRemoving;
                    }
                    else
                    {
                        RemovingState = EditorUtility.CenteredButton("Remove Constraint", 250)
                            ? ConstraintRemovalState.UserConfirm
                            : ConstraintRemovalState.NotRemoving;
                    }

                    DrawDataFields();

                    DrawSourceFields();
                }
                EditorGUI.indentLevel -= 1;

                EditorGUILayout.Space(5);
            }
        }
        private void DrawDataFields()
        {
            var settings = ConstraintEditor.Settings;

            if (settings.AdvancedMode)
            {
                bool showButton = !(Constraint.locked && Constraint.constraintActive);
                if (showButton && EditorUtility.CenteredButton("Activate and Lock", 250))
                    Constraint.ActivateAndPreserveOffset();

                EditorGUILayout.Space(5);
                Constraint.constraintActive = EditorGUILayout.Toggle("Active", Constraint.constraintActive);
                Constraint.locked = EditorGUILayout.Toggle("Locked", Constraint.locked);
            }
            else
            {
                if (Constraint.constraintActive)
                {
                    if (EditorUtility.CenteredButton("Constraint ON", 250))
                    {
                        Constraint.locked = false;
                        Constraint.constraintActive = false;
                    }
                }
                else if (Constraint.locked && EditorUtility.CenteredButton($"Constraint LOCKED", 250))
                    Constraint.locked = false;
                else if (EditorUtility.CenteredButton($"Constraint OFF", 250))
                    Constraint.ActivateAndPreserveOffset();
            }

            EditorGUILayout.Space(5);
            Constraint.weight = EditorGUILayout.Slider("Weight", Constraint.weight, 0.0f, 1.0f);
            EditorGUILayout.Space(5);


            if (settings.AdvancedMode)
            {
                GUI.enabled = !Constraint.locked;
                //Constraint.transform.localEulerAngles = EditorUtility.DrawCustomVector3("Object Rotation", Constraint.transform.localEulerAngles); ;
                Constraint.rotationAtRest = EditorUtility.DrawCustomVector3("Rotation at Rest", Constraint.rotationAtRest);
                Constraint.rotationOffset = EditorUtility.DrawCustomVector3("Rotation Offset", Constraint.rotationOffset);
                GUI.enabled = true;


                Constraint.rotationAxis = EditorUtility.DrawCustomAxis("Freeze Rotation", Constraint.rotationAxis);

                EditorGUILayout.Space(5);
                if (EditorUtility.CenteredButton("Recalculate Offset", 250))
                    Constraint.RecalculateOffset();
            }
            else
            {
                GUI.enabled = !Constraint.locked;
                Vector3 rotation = EditorUtility.DrawCustomVector3("Rotation at Rest", Constraint.rotationAtRest);

                Constraint.rotationAtRest = rotation;
                Constraint.transform.localEulerAngles = rotation;
                Constraint.rotationAxis = EditorUtility.DrawCustomAxis("Freeze Rotation", Constraint.rotationAxis);
                GUI.enabled = true;
            }
        }
        private void DrawSourceFields()
        {
            ConstraintEditorSettings settings = ConstraintEditor.Settings;

            EditorGUILayout.LabelField("Sources:");
            EditorGUI.indentLevel += 1;
            using (new GUILayout.VerticalScope("helpbox"))
            {
                List<ConstraintSource> sources = new List<ConstraintSource>(Constraint.sourceCount);
                Constraint.GetSources(sources);

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
                            Constraint.RemoveSource(i);

                        float newWeight = EditorGUI.FloatField(weightFieldRect, source.weight);
                        Transform newSource = EditorGUI.ObjectField(targetFieldRect, source.sourceTransform, typeof(Transform), true) as Transform;

                        if (newWeight != source.weight || newSource != source.sourceTransform)
                        {
                            Constraint.SetSource(i, new ConstraintSource() { sourceTransform = newSource, weight = newWeight });
                            Constraint.RecalculateOffset();
                        }

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
                {
                    Constraint.AddSource(new ConstraintSource() { sourceTransform = null, weight = 1.0f });
                    Constraint.RecalculateOffset();
                }
            }
            EditorGUI.indentLevel -= 1;
        }
    }
}
