using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEditor;
using UnityEngine;

namespace Overclock.Tests
{
    public class ModuleFamilyTests
    {
        // 10/20 확정 (계열 구상 §1-1): 기준 = 등급 · 애매한 4종 = 범용
        private static readonly Dictionary<string, ModuleFamily> DesignedFamilies = new Dictionary<string, ModuleFamily>
        {
            { "base_reinforcement", ModuleFamily.Volume },
            { "sync_circuit", ModuleFamily.Volume },
            { "overload_circuit", ModuleFamily.Volume },
            { "auxiliary_power", ModuleFamily.Volume },
            { "grade_filter", ModuleFamily.Stable },
            { "compound_calibrator", ModuleFamily.Stable },
            { "rare_resonator", ModuleFamily.Stable },
            { "amplifier_coil", ModuleFamily.Stable },
            { "calibrator", ModuleFamily.Jackpot },
            { "fine_calibrator", ModuleFamily.Jackpot },
            { "selective_amplifier", ModuleFamily.Jackpot },
            { "electronic_amplifier", ModuleFamily.General },
            { "electronic_network", ModuleFamily.General },
            { "power_link", ModuleFamily.General },
            { "carry_over_unit", ModuleFamily.General }
        };

        [Test]
        public void ModuleFamily_KeepsSavedValues()
        {
            Assert.AreEqual(0, (int)ModuleFamily.General);
            Assert.AreEqual(1, (int)ModuleFamily.Volume);
            Assert.AreEqual(2, (int)ModuleFamily.Stable);
            Assert.AreEqual(3, (int)ModuleFamily.Jackpot);
        }

        [Test]
        public void NewModule_IsGeneral()
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            Assert.AreEqual(ModuleFamily.General, module.Family);
            Object.DestroyImmediate(module);
        }

        [Test]
        public void ModuleAssets_HaveDesignedFamilies()
        {
            // 표에 있는 15종만 검사한다. 새 모듈은 표 없이 추가할 수 있다 (코드 수정 없이 모듈 추가)
            var found = new HashSet<string>();
            var problems = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:ModuleData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ModuleData module = AssetDatabase.LoadAssetAtPath<ModuleData>(path);
                if (!DesignedFamilies.TryGetValue(module.Id, out ModuleFamily expected))
                {
                    continue;
                }

                found.Add(module.Id);
                if (module.Family != expected)
                {
                    problems.Add(path + ": " + module.Family + " (기대 " + expected + ")");
                }
            }

            foreach (string id in DesignedFamilies.Keys)
            {
                if (!found.Contains(id))
                {
                    problems.Add(id + ": 에셋 없음");
                }
            }

            problems.Sort(System.StringComparer.Ordinal);
            Assert.IsTrue(problems.Count == 0, "계열 문제 " + problems.Count + "건\n" + string.Join("\n", problems));
        }
    }
}
