using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace Eliza.ConstraintEditor
{
    [Serializable]
    public struct ElizaConstraintTemplate
    {
        public string TemplateName;
        public List<SerializedConstraint> Constraints;
    }
    [Serializable]
    public struct SerializedConstraint
    {
        public string ArmaturePath;
        public float Weight;
        public bool IsActive;
        public bool IsLocked;
        public bool FreezeX;
        public bool FreezeY;
        public bool FreezeZ;
        public Vector3 RotationAtRest;
        public Vector3 RotationOffset;
        public List<SerializedConstraintSource> Sources;
    }
    [Serializable]
    public struct SerializedConstraintSource
    {
        public string ArmaturePath;
        public float Weight;

        public static SerializedConstraintSource Generate(ConstraintSource source)
        {
            string path = source.sourceTransform.name;
            Transform parent = source.sourceTransform.parent;

            while (parent.name != "Armature")
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }

            return new SerializedConstraintSource()
            {
                ArmaturePath = path,
                Weight = source.weight
            };
        }
    }

    public static class ConstraintSerializer
    {
        public static void SaveConstraintsAsTemplate(string templateName, List<ConstraintMetaData> constraintData)
        {
            ElizaConstraintTemplate template = new ElizaConstraintTemplate()
            {
                TemplateName = templateName,
                Constraints = new List<SerializedConstraint>()
            };

            foreach (var cData in constraintData)
                template.Constraints.Add(GenerateSerializedConstraint(cData));

            string path = $"{Application.dataPath}/ElizaConstraintEditor/Templates/{templateName}.json";
            string templateJson = JsonUtility.ToJson(template);

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(templateJson);
                }
            }

            AssetDatabase.Refresh();
        }

        public static void LoadConstraintsFromTempalte(string templateName, Transform currentArmature)
        {
            string templateJson = string.Empty;
            string path = $"{Application.dataPath}/ElizaConstraintEditor/Templates/{templateName}.json";

            if (!File.Exists(path))
            {
                Debug.LogError($"No template exists at {path}!");
                return;
            }

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new StreamReader(stream))
                {
                    templateJson = reader.ReadToEnd();
                }
            }

            var template = (ElizaConstraintTemplate)JsonUtility.FromJson(templateJson, typeof(ElizaConstraintTemplate));
            foreach (var tData in template.Constraints)
            {
                if (EditorUtility.TryFindChild(currentArmature, tData.ArmaturePath, out Transform transform))
                {
                    RotationConstraint constraint;
                    if (!transform.gameObject.TryGetComponent<RotationConstraint>(out constraint))
                        constraint = transform.gameObject.AddComponent<RotationConstraint>();

                    //constraint.constraintActive = tData.IsActive;
                    constraint.weight = tData.Weight;
                    constraint.locked = tData.IsLocked;
                    constraint.rotationAxis = Axis.None;

                    if (tData.FreezeX)
                        constraint.rotationAxis |= Axis.X;
                    if (tData.FreezeY)
                        constraint.rotationAxis |= Axis.Y;
                    if (tData.FreezeZ)
                        constraint.rotationAxis |= Axis.Z;

                    constraint.rotationAtRest = tData.RotationAtRest;
                    constraint.rotationOffset = tData.RotationOffset;

                    foreach (var tSource in tData.Sources)
                    {
                        if (EditorUtility.TryFindChild(currentArmature, tSource.ArmaturePath, out Transform sourceTransform))
                        {
                            constraint.AddSource(new ConstraintSource()
                            {
                                sourceTransform = sourceTransform,
                                weight = tSource.Weight
                            });
                        }
                        else Debug.LogWarning($"Could not find source for {constraint.name}:\n{tSource.ArmaturePath}");
                    }

                    constraint.constraintActive = tData.IsActive;
                }
                else Debug.LogWarning($"Could not find constraint target:\n{tData.ArmaturePath}");
            }
        }

        public static SerializedConstraint GenerateSerializedConstraint(ConstraintMetaData cData)
        {
            var constraint = cData.Constraint;
            List<SerializedConstraintSource> serializedSources = new List<SerializedConstraintSource>(constraint.sourceCount);

            for (int i = 0; i < constraint.sourceCount; ++i)
                serializedSources.Add(SerializedConstraintSource.Generate(constraint.GetSource(i)));

            return new SerializedConstraint()
            {
                ArmaturePath = $"{cData.PathFromArmature}/{constraint.name}",
                Weight = constraint.weight,
                IsActive = constraint.constraintActive,
                IsLocked = constraint.locked,
                FreezeX = constraint.rotationAxis.HasFlag(Axis.X),
                FreezeY = constraint.rotationAxis.HasFlag(Axis.Y),
                FreezeZ = constraint.rotationAxis.HasFlag(Axis.Z),
                RotationAtRest = constraint.rotationAtRest,
                RotationOffset = constraint.rotationOffset,
                Sources = serializedSources
            };
        }
        public static SerializedConstraintSource GenerateSerializedSource(ConstraintSource source)
        {
            string path = source.sourceTransform.name;
            Transform parent = source.sourceTransform.parent;

            while (parent.name != "Armature")
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }

            return new SerializedConstraintSource()
            {
                ArmaturePath = path,
                Weight = source.weight
            };
        }

        public static void SaveSettings(ConstraintEditorSettings settings)
        {
            string path = $"{Application.dataPath}/ElizaConstraintEditor/settings.json";
            string templateJson = JsonUtility.ToJson(settings);

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(templateJson);
                }
            }
        }
        public static ConstraintEditorSettings LoadSettings()
        {
            string json = string.Empty;
            string path = $"{Application.dataPath}/ElizaConstraintEditor/settings.json";

            if (!File.Exists(path))
            {
                Debug.LogError($"No settings exist at {path}!");
                return new ConstraintEditorSettings();
            }

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new StreamReader(stream))
                {
                    json = reader.ReadToEnd();
                }
            }

            return (ConstraintEditorSettings)JsonUtility.FromJson(json, typeof(ConstraintEditorSettings));
        }
    }
}
