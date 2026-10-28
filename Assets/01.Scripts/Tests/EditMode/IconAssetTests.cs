using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEditor;
using UnityEngine;

namespace Overclock.Tests
{
    public class IconAssetTests
    {
        private const string IconFolder = "Assets/04.Art/Icons";

        [Test]
        public void AllDataAssets_HaveIcon()
        {
            var problems = new List<string>();
            int partCount = 0;
            int moduleCount = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:PartData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                partCount++;
                if (AssetDatabase.LoadAssetAtPath<PartData>(path).Icon == null)
                {
                    problems.Add(path);
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ModuleData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                moduleCount++;
                if (AssetDatabase.LoadAssetAtPath<ModuleData>(path).Icon == null)
                {
                    problems.Add(path);
                }
            }

            // 에셋을 하나도 못 찾으면 검사할 것이 없어 그냥 통과한다. 그걸 막는다
            Assert.GreaterOrEqual(partCount, 3, "부품 에셋을 찾지 못함");
            Assert.GreaterOrEqual(moduleCount, 3, "모듈 에셋을 찾지 못함");
            problems.Sort(StringComparer.Ordinal);
            Assert.IsTrue(problems.Count == 0, "아이콘 없음 " + problems.Count + "건\n" + string.Join("\n", problems));
        }

        // 아이콘 연결 메뉴(Overclock/Assign Icons)는 파일 이름 = id 로 찾는다. id 만 바꾸면 다음 연결 때 아이콘이 빠진다
        [Test]
        public void AllIcons_FileNameMatchesId()
        {
            var problems = new List<string>();
            int checkedCount = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:PartData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                PartData part = AssetDatabase.LoadAssetAtPath<PartData>(path);
                checkedCount += CheckIconName(path, part.Id, part.Icon, problems);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ModuleData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ModuleData module = AssetDatabase.LoadAssetAtPath<ModuleData>(path);
                checkedCount += CheckIconName(path, module.Id, module.Icon, problems);
            }

            Assert.GreaterOrEqual(checkedCount, 6, "아이콘이 있는 에셋을 찾지 못함");
            problems.Sort(StringComparer.Ordinal);
            Assert.IsTrue(problems.Count == 0, "아이콘 파일 이름 ≠ id " + problems.Count + "건\n" + string.Join("\n", problems));
        }

        // 아이콘이 없는 경우는 AllDataAssets_HaveIcon 이 잡는다
        private static int CheckIconName(string assetPath, string id, Sprite icon, List<string> problems)
        {
            if (icon == null)
            {
                return 0;
            }

            string expected = IconFolder + "/" + id + ".png";
            string actual = AssetDatabase.GetAssetPath(icon);
            if (actual != expected)
            {
                problems.Add(assetPath + ": id " + id + ", 아이콘 " + actual + " (기대 " + expected + ")");
            }

            return 1;
        }
    }
}
