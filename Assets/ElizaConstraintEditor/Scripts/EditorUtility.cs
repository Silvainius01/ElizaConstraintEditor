using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Eliza.ConstraintEditor
{
    public static class EditorUtility
    {
        public static readonly GUIStyle DefaultTitleStyle = new GUIStyle("boldLabel") { alignment = TextAnchor.MiddleCenter };

        // I straight up stole this title layout from Dreadrith
        // https://github.com/Dreadrith/Unity-Animation-Hierarchy-Editor/blob/master/AnimationHierarchyEditor.cs#L260-L265
        public static void DrawTitle(string title) => DrawTitle(title, DefaultTitleStyle);
        public static void DrawTitle(string title, GUIStyle labelStyle)
        {
            using (new GUILayout.HorizontalScope("in bigtitle"))
                GUILayout.Label(title, labelStyle);
        }

        public static bool TryFindChild(this Transform t, string name, out Transform result)
        {
            result = t.Find(name);
            return result != null;
        }

        public static Rect GetCenteredControlRect(float width)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            float centerX = rect.center.x;
            rect.xMin = centerX - (width / 2);
            rect.xMax = centerX + (width / 2);
            return rect;
        }

        /// <summary>
        /// Returns a group of evenly spaced, centered horizontal control rects.
        /// </summary>
        /// <param name="width">The width the rects collectively fill.</param>
        /// <param name="spacing"> Space between rects </param>
        public static Rect[] GetCenteredRectGroupHorizontal(int numRects, float width, float spacing)
        {
            int last = numRects - 1;
            Rect[] group = new Rect[numRects];

            using (new GUILayout.HorizontalScope())
            {
                Rect cRect = EditorGUILayout.GetControlRect();
                for (int i = 0; i < numRects; ++i)
                    group[i] = new Rect(cRect);
            }

            spacing /= 2;
            width /= numRects;
            float start =
                  (group[0].min.x + (group[last].max.x - group[0].min.x) / 2)   // Calculate center
                - (width * (numRects / 2)                                       // Subtract half the total span
                + (numRects % 2 == 1 ? width / 2 : 0));                         // If an odd number, add half a width to account.

            for (int i = 0; i < numRects; ++i)
            {
                float xMin = start + width * i;
                group[i].xMin = xMin + spacing;
                group[i].xMax = xMin + width - spacing;
            }

            // Dont need spacing at the ends :)
            group[0].xMin = start;
            group[last].xMax += spacing;
            return group;
        }

        public static Vector3 DrawCustomVector3(string label, Vector3 value)
        {
            string[] labels = new string[3] { "X", "Y", "Z" };
            Rect controlRect = EditorGUILayout.GetControlRect();

            EditorGUI.LabelField(controlRect, label);
            controlRect.xMin += 45;

            for (int i = 0; i < 3; ++i)
            {
                controlRect.xMin += 80;
                controlRect.width = 40;
                EditorGUI.LabelField(controlRect, labels[i]);

                controlRect.xMin += 15;
                controlRect.width = 100;
                value.y = EditorGUI.FloatField(controlRect, value.y);
            }

            return value;
        }
    }
}