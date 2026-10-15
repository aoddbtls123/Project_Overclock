using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Overclock.Core;
using UnityEditor;
using UnityEngine;

namespace Overclock.Tests
{
    public class DataAssetTests
    {
        private static readonly Regex IdPattern = new Regex(@"\A[a-z][a-z0-9_]*\z");

        [Test]
        public void AllDataAssets_HaveValidUniqueIds()
        {
            List<string> partPaths = FindPaths<PartData>();
            List<string> modulePaths = FindPaths<ModuleData>();

            // 에셋을 하나도 못 찾으면 검사할 것이 없어 그냥 통과한다. 그걸 막는다
            Assert.GreaterOrEqual(partPaths.Count, 3, "부품 에셋을 찾지 못함");
            Assert.GreaterOrEqual(modulePaths.Count, 3, "모듈 에셋을 찾지 못함");

            var entries = new List<IdEntry>();
            foreach (string path in partPaths)
            {
                entries.Add(new IdEntry(path, AssetDatabase.LoadAssetAtPath<PartData>(path).Id));
            }

            foreach (string path in modulePaths)
            {
                entries.Add(new IdEntry(path, AssetDatabase.LoadAssetAtPath<ModuleData>(path).Id));
            }

            List<string> problems = FindIdProblems(entries);
            Assert.IsTrue(problems.Count == 0, "id 문제 " + problems.Count + "건\n" + string.Join("\n", problems));
        }

        [Test]
        public void AllParts_HaveTag()
        {
            List<string> partPaths = FindPaths<PartData>();
            Assert.GreaterOrEqual(partPaths.Count, 3, "부품 에셋을 찾지 못함");

            var problems = new List<string>();
            foreach (string path in partPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<PartData>(path).Tag == PartTag.None)
                {
                    problems.Add(path + ": 태그 없음 (None)");
                }
            }

            Assert.IsTrue(problems.Count == 0, "태그 문제 " + problems.Count + "건\n" + string.Join("\n", problems));
        }

        [Test]
        public void IdCheck_FindsEmptyMalformedAndDuplicateIds()
        {
            var entries = new List<IdEntry>
            {
                new IdEntry("A.asset", "bolt"),
                new IdEntry("B.asset", ""),
                new IdEntry("C.asset", null),
                new IdEntry("D.asset", "Bolt"),
                new IdEntry("E.asset", " gear"),
                new IdEntry("F.asset", "bolt"),
                new IdEntry("G.asset", "core_2"),
                new IdEntry("H.asset", "bolt\n"),
            };

            List<string> problems = FindIdProblems(entries);

            var flagged = new List<string>();
            foreach (string problem in problems)
            {
                flagged.Add(problem.Substring(0, problem.IndexOf(':')));
            }

            flagged.Sort(StringComparer.Ordinal);
            Assert.AreEqual("B.asset,C.asset,D.asset,E.asset,F.asset,H.asset", string.Join(",", flagged), string.Join("\n", problems));
        }

        // 세이브가 id 로 에셋을 찾으므로 부품·모듈 전체에서 하나뿐이어야 한다
        private static List<string> FindIdProblems(List<IdEntry> entries)
        {
            var problems = new List<string>();
            var firstPathById = new Dictionary<string, string>();
            foreach (IdEntry entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Id))
                {
                    problems.Add(entry.Path + ": id 비어 있음");
                    continue;
                }

                if (!IdPattern.IsMatch(entry.Id))
                {
                    problems.Add(entry.Path + ": id 형식 오류 \"" + entry.Id + "\" (영문 소문자로 시작 · 소문자·숫자·_ 만)");
                    continue;
                }

                if (firstPathById.TryGetValue(entry.Id, out string firstPath))
                {
                    problems.Add(entry.Path + ": id 중복 \"" + entry.Id + "\" (" + firstPath + " 와 같음)");
                    continue;
                }

                firstPathById.Add(entry.Id, entry.Path);
            }

            return problems;
        }

        private static List<string> FindPaths<T>() where T : ScriptableObject
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        private readonly struct IdEntry
        {
            public IdEntry(string path, string id)
            {
                Path = path;
                Id = id;
            }

            public string Path { get; }
            public string Id { get; }
        }
    }
}
