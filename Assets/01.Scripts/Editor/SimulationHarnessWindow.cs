using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Overclock.Core;
using UnityEditor;
using UnityEngine;

namespace Overclock.EditorTools
{
    public sealed class SimulationHarnessWindow : EditorWindow
    {
        private const int MaxRunCount = 100000;
        private const int ReportRoundCount = 8;
        private const double AnalyticR4PassRate = 0.1336;

        private int runCount = 10000;
        private int firstSeed = 1;
        private PartData[] parts = new PartData[0];
        private ModuleData[] modules = new ModuleData[0];
        private List<StrategyResult> results;
        private string report = "";
        private Vector2 scroll;

        private sealed class StrategyResult
        {
            public StrategyResult(IModuleStrategy strategy, List<RunRecord> records)
            {
                Strategy = strategy;
                Records = records;
                Summary = new SimulationSummary(records);
            }

            public IModuleStrategy Strategy { get; }
            public List<RunRecord> Records { get; }
            public SimulationSummary Summary { get; }
        }

        [MenuItem("Overclock/Simulation Harness")]
        private static void Open()
        {
            GetWindow<SimulationHarnessWindow>("Simulation Harness");
        }

        private void OnEnable()
        {
            LoadAssets();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("부품 " + parts.Length + "종 · 모듈 " + modules.Length + "종 (프로젝트의 모든 에셋, 경로 순)");
            EditorGUILayout.LabelField("부품: " + JoinNames(parts));
            EditorGUILayout.LabelField("모듈: " + JoinNames(modules));
            if (GUILayout.Button("에셋 다시 찾기"))
            {
                LoadAssets();
            }

            runCount = Mathf.Clamp(EditorGUILayout.IntField("런 수 (전략마다)", runCount), 1, MaxRunCount);
            firstSeed = Mathf.Clamp(EditorGUILayout.IntField("시작 시드", firstSeed), 1, int.MaxValue - MaxRunCount);

            if (GUILayout.Button("실행"))
            {
                Run();
            }

            GUI.enabled = results != null;
            if (GUILayout.Button("CSV 저장"))
            {
                SaveCsv();
            }

            GUI.enabled = true;

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report);
            EditorGUILayout.EndScrollView();
        }

        private void LoadAssets()
        {
            parts = LoadAll<PartData>();
            modules = LoadAll<ModuleData>();
        }

        // 순서가 바뀌면 같은 시드라도 고르는 부품·카드가 달라지므로 경로 순으로 고정함
        private static T[] LoadAll<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            var paths = new List<string>();
            foreach (string guid in guids)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            paths.Sort(StringComparer.Ordinal);

            var assets = new List<T>();
            foreach (string path in paths)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets.ToArray();
        }

        private void Run()
        {
            uint seed = (uint)firstSeed;
            var strategies = new IModuleStrategy[]
            {
                new NoModuleStrategy(),
                new RandomModuleStrategy(),
                new GreedyModuleStrategy(parts),
            };

            var stopwatch = Stopwatch.StartNew();
            try
            {
                results = new List<StrategyResult>();
                foreach (IModuleStrategy strategy in strategies)
                {
                    results.Add(new StrategyResult(strategy, RunSimulator.SimulateMany(seed, runCount, parts, modules, strategy)));
                }
            }
            catch (ArgumentException exception)
            {
                results = null;
                report = "실행할 수 없습니다: " + exception.Message;
                return;
            }

            report = BuildReport(stopwatch.Elapsed.TotalSeconds);
        }

        private string BuildReport(double seconds)
        {
            SimulationSummary none = results[0].Summary;
            SimulationSummary random = results[1].Summary;
            SimulationSummary greedy = results[2].Summary;
            double choiceImpact = greedy.MeanFinalRound - random.MeanFinalRound;

            var text = new StringBuilder();
            text.AppendLine("런 " + runCount + " × 전략 3 · 시드 " + firstSeed + " ~ " + (firstSeed + runCount - 1)
                + " · 부품 " + parts.Length + " · 모듈 " + modules.Length + " · " + seconds.ToString("F1") + "초");
            text.AppendLine("도달 라운드 = 런이 끝난 라운드 (R8 실패와 클리어 모두 8)");
            text.AppendLine();

            text.AppendLine("① 모듈 없음 R4 통과율 (R4 도달 런 중)  " + Percent(none.PassRate(4))
                + "  (" + none.PassedCount(4) + " / " + none.ReachedCount(4) + ")  목표 15% 이하 · 해석값 " + Percent(AnalyticR4PassRate)
                + "  " + Mark(none.PassRate(4) <= 0.15));
            text.AppendLine("② 무작위 평균 도달 라운드  " + random.MeanFinalRound.ToString("F2")
                + "  (중앙값 " + random.MedianFinalRound + ")  목표 5~6  "
                + Mark(random.MeanFinalRound >= 5.0 && random.MeanFinalRound <= 6.0));
            text.AppendLine("③ 기대 생산량 최대 전략  평균 도달 라운드 " + greedy.MeanFinalRound.ToString("F2")
                + " (중앙값 " + greedy.MedianFinalRound + ") · 클리어율 " + Percent(greedy.ClearRate) + "  목표 8 (판정 기준 미정)");
            text.AppendLine("④ ③ − ② 선택의 영향력  " + choiceImpact.ToString("F2") + "  목표 1 이상  " + Mark(choiceImpact >= 1.0));
            text.AppendLine("⑤ 운의 영향력 (상위 10% 평균 − 하위 10% 평균)  무작위 " + random.LuckSpread.ToString("F2")
                + " · 기대 생산량 최대 " + greedy.LuckSpread.ToString("F2") + "  목표 ④ 보다 작을 것  무작위 "
                + Mark(choiceImpact > random.LuckSpread) + " · 기대 생산량 최대 " + Mark(choiceImpact > greedy.LuckSpread));
            text.AppendLine("   ③ 은 다음 라운드 기대 생산량만 보는 전략이라 실제 최적보다 낮을 수 있다");
            text.AppendLine("   무작위의 ⑤ 에는 뽑기 운과 선택의 무작위성이 섞여 있다");
            text.AppendLine();

            text.Append("종료 라운드 분포 (%)  ");
            for (int round = 1; round <= ReportRoundCount; round++)
            {
                text.Append("R" + round + (round == ReportRoundCount ? " 실패" : "") + "  ");
            }

            text.AppendLine("클리어");
            foreach (StrategyResult result in results)
            {
                text.Append(result.Strategy.Name + "  ");
                for (int round = 1; round <= ReportRoundCount; round++)
                {
                    text.Append(Share(result.Summary.EndedAtCount(round, false), result.Summary.RunCount) + "  ");
                }

                text.AppendLine(Share(result.Summary.EndedAtCount(ReportRoundCount, true), result.Summary.RunCount));
            }

            return text.ToString();
        }

        private void SaveCsv()
        {
            string path = EditorUtility.SaveFilePanel("시뮬레이션 결과 저장", "", "harness_" + DateTime.Now.ToString("MMdd_HHmm") + ".csv", "csv");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var csv = new StringBuilder();
            csv.AppendLine("strategy,seed,final_round,cleared,modules");
            foreach (StrategyResult result in results)
            {
                foreach (RunRecord record in result.Records)
                {
                    var names = new List<string>();
                    foreach (ModuleData module in record.EquippedModules)
                    {
                        names.Add(module.DisplayName);
                    }

                    csv.AppendLine(Quote(result.Strategy.Name) + "," + record.Seed + "," + record.FinalRound + ","
                        + (record.Cleared ? 1 : 0) + "," + Quote(string.Join(" | ", names)));
                }
            }

            // Excel 이 한글을 깨뜨리지 않게 BOM 을 붙임
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
            File.WriteAllText(Path.ChangeExtension(path, ".txt"), report, new UTF8Encoding(true));
            report += "\n저장: " + path;
        }

        private static string JoinNames(PartData[] items)
        {
            var names = new List<string>();
            foreach (PartData item in items)
            {
                names.Add(item.DisplayName);
            }

            return string.Join(", ", names);
        }

        private static string JoinNames(ModuleData[] items)
        {
            var names = new List<string>();
            foreach (ModuleData item in items)
            {
                names.Add(item.DisplayName);
            }

            return string.Join(", ", names);
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Percent(double rate)
        {
            return (rate * 100.0).ToString("F2") + "%";
        }

        private static string Share(int count, int total)
        {
            return (100.0 * count / total).ToString("F1");
        }

        private static string Mark(bool passed)
        {
            return passed ? "[통과]" : "[미달]";
        }
    }
}
