using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.WSA;

namespace Eliza.ConstraintEditor
{
    public static class EditorLabels
    {
        public static readonly GUIContent ConstraintActive = new GUIContent(
            "Active",
            "If enabled, the constraint is being actively evaluated.");
        public static readonly GUIContent ConstraintLocked = new GUIContent(
            "Locked",
            "If enabled, the constraint will not allow the rest position or offset to be changed. It also prevents you from changing the local rotation in the Inspector.");

    }
}