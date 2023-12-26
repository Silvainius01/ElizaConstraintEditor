using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eliza.ConstraintEditor
{
    /// <summary>
    /// Storage for armature data. Allows me to have persistant behaviour across multiple armatures.
    /// </summary>
    public class ElizaArmaureData
    {
        public string templateName;
        public GameObject parentObject;
        public Transform armature;
        public List<ConstraintMetaData> constraintData = new List<ConstraintMetaData>();
    }
}