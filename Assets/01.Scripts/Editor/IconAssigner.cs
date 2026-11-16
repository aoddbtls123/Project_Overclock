using System.Collections.Generic;
using Overclock.Core;
using UnityEditor;
using UnityEngine;

namespace Overclock.EditorTools
{
    public static class IconAssigner
    {
        private const string IconFolder = "Assets/04.Art/Icons";

        // 아이콘 파일 이름 = 부품·모듈 id. 에셋마다 손으로 연결하지 않고 이름으로 찾아 넣음
        [MenuItem("Overclock/Assign Icons")]
        private static void AssignIcons()
        {
            ImportAsSprites();

            var missing = new List<string>();
            int assigned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:PartData"))
            {
                PartData part = AssetDatabase.LoadAssetAtPath<PartData>(AssetDatabase.GUIDToAssetPath(guid));
                if (TryAssign(part, part.Id))
                {
                    assigned++;
                }
                else
                {
                    missing.Add(part.Id);
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ModuleData"))
            {
                ModuleData module = AssetDatabase.LoadAssetAtPath<ModuleData>(AssetDatabase.GUIDToAssetPath(guid));
                if (TryAssign(module, module.Id))
                {
                    assigned++;
                }
                else
                {
                    missing.Add(module.Id);
                }
            }

            AssetDatabase.SaveAssets();

            if (missing.Count == 0)
            {
                Debug.Log("아이콘 연결 " + assigned + "개");
            }
            else
            {
                Debug.LogWarning("아이콘 연결 " + assigned + "개 · 파일 없음 " + missing.Count + "개: " + string.Join(", ", missing));
            }
        }

        private static void ImportAsSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        private static bool TryAssign(Object asset, string id)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/" + id + ".png");
            if (sprite == null)
            {
                return false;
            }

            var serialized = new SerializedObject(asset);
            serialized.FindProperty("icon").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return true;
        }
    }
}
