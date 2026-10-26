using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEditor;

namespace Overclock.Tests
{
    public class IconAssetTests
    {
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
    }
}
