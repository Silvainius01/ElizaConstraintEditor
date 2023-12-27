using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eliza.ConstraintEditor
{
    [Serializable]
    public class ConstraintEditorSettings
    {
        public bool EnableDebugMode = false;
        public bool DestroyConstraintsOnLoad = true;
        public bool LoadRotationData = false;

        #region Developer Settings
        public bool VerboseConsoleErrors = false;
        #endregion
    }

    public class ConstraintEditorError
    {
        public EditorErrorCode code;
        public string message;
    }
}
