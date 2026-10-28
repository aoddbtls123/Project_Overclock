using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Overclock.Core;
using UnityEditor;
using UnityEngine;

namespace Overclock.Tests
{
    public class SceneDataOrderTests
    {
        private const string ScenePath = "Assets/Scenes/Run.unity";
        private const string ScreenScriptPath = "Assets/01.Scripts/Presentation/GrayBoxRunScreen.cs";
        private const string EmptySlot = "(빈 칸)";

        // 하네스는 에셋을 경로 순으로 모은다. 씬 배열이 그 순서와 다르면 같은 시드라도 게임과 하네스의 판이 갈린다
        [Test]
        public void RunScene_PartsFollowAssetPathOrder()
        {
            AssertSceneArrayMatches("parts", FindAssetPaths<PartData>());
        }

        [Test]
        public void RunScene_ModulesFollowAssetPathOrder()
        {
            AssertSceneArrayMatches("modules", FindAssetPaths<ModuleData>());
        }

        [Test]
        public void OrderCheck_FindsMissingExtraDuplicateAndSwapped()
        {
            var expected = new List<string> { "a", "b", "c" };

            AssertProblems(expected, new List<string> { "a", "b", "c" }, "");
            AssertProblems(expected, new List<string> { "a", "b" }, "빠짐: c");
            AssertProblems(expected, new List<string> { "a", "b", "c", "d" }, "목록에 없음: d");
            AssertProblems(expected, new List<string> { "a", "b", "c", EmptySlot }, "목록에 없음: " + EmptySlot);
            AssertProblems(expected, new List<string> { "a", "b", "c", "c" }, "중복: c");
            AssertProblems(expected, new List<string> { "a", "c", "b" }, "순서 다름: Element 1 기대 b, 실제 c");
            AssertProblems(expected, new List<string> { "a", "c", "d" }, "빠짐: b\n목록에 없음: d");
        }

        private static void AssertSceneArrayMatches(string fieldName, List<string> expected)
        {
            Assert.GreaterOrEqual(expected.Count, 3, "에셋을 찾지 못함");

            List<string> actual = ReadSceneArray(fieldName);
            List<string> problems = FindOrderProblems(expected, actual);
            Assert.IsTrue(problems.Count == 0,
                ScenePath + " RunScreen." + fieldName + " 문제 " + problems.Count + "건\n" + string.Join("\n", problems));
        }

        private static void AssertProblems(List<string> expected, List<string> actual, string expectedProblems)
        {
            Assert.AreEqual(expectedProblems, string.Join("\n", FindOrderProblems(expected, actual)));
        }

        private static List<string> FindOrderProblems(List<string> expected, List<string> actual)
        {
            var problems = new List<string>();

            foreach (string item in expected)
            {
                if (!actual.Contains(item))
                {
                    problems.Add("빠짐: " + item);
                }
            }

            var seen = new HashSet<string>();
            foreach (string item in actual)
            {
                if (!expected.Contains(item))
                {
                    problems.Add("목록에 없음: " + item);
                }
                else if (!seen.Add(item))
                {
                    problems.Add("중복: " + item);
                }
            }

            if (problems.Count > 0)
            {
                return problems;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                if (expected[i] != actual[i])
                {
                    problems.Add("순서 다름: Element " + i + " 기대 " + expected[i] + ", 실제 " + actual[i]);
                    break;
                }
            }

            return problems;
        }

        // 디스크의 씬 파일을 읽는다 (커밋되는 내용). 씬을 열지 않으므로 에디터 상태를 바꾸지 않는다
        private static List<string> ReadSceneArray(string fieldName)
        {
            string screenGuid = AssetDatabase.AssetPathToGUID(ScreenScriptPath);
            Assert.IsFalse(string.IsNullOrEmpty(screenGuid), ScreenScriptPath + " 를 찾지 못함");

            string scriptLine = "  m_Script: {fileID: 11500000, guid: " + screenGuid + ", type: 3}";
            string fieldLine = "  " + fieldName + ":";
            string[] lines = File.ReadAllText(ScenePath).Replace("\r", "").Split('\n');

            var result = new List<string>();
            int screenCount = 0;
            bool inScreen = false;
            bool found = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("--- ", StringComparison.Ordinal))
                {
                    inScreen = false;
                }
                else if (line == scriptLine)
                {
                    inScreen = true;
                    screenCount++;
                }
                else if (inScreen && line == fieldLine)
                {
                    found = true;
                    for (int j = i + 1; j < lines.Length && lines[j].StartsWith("  - ", StringComparison.Ordinal); j++)
                    {
                        result.Add(ToAssetPath(lines[j]));
                    }
                }
                else if (inScreen && line == fieldLine + " []")
                {
                    found = true;
                }
            }

            Assert.AreEqual(1, screenCount, ScenePath + " 의 GrayBoxRunScreen 개수");
            Assert.IsTrue(found, ScenePath + " RunScreen 에 " + fieldName + " 칸이 없음");
            return result;
        }

        private static string ToAssetPath(string elementLine)
        {
            const string GuidKey = "guid: ";
            int start = elementLine.IndexOf(GuidKey, StringComparison.Ordinal);
            if (start < 0)
            {
                return EmptySlot;
            }

            start += GuidKey.Length;
            int end = elementLine.IndexOf(',', start);
            string guid = end < 0 ? elementLine.Substring(start) : elementLine.Substring(start, end - start);
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? "(없는 에셋 guid " + guid + ")" : path;
        }

        // 하네스(SimulationHarnessWindow.LoadAll)와 같은 방법: 타입으로 찾고 경로를 Ordinal 정렬
        private static List<string> FindAssetPaths<T>() where T : ScriptableObject
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }
    }
}
