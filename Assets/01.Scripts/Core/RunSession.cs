using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class RunSession
    {
        private const int PityThreshold = 50;
        // 일반모드 라운드 수. 최고 기록 검사(BestRecords)도 이 값을 쓴다
        public const int NormalRoundCount = 8;
        // 무한모드에는 마지막 라운드가 없다. 클리어 판정(현재 라운드 == 라운드 수)에 닿지 않는 값을 넘긴다
        private const int EndlessRoundCount = int.MaxValue;
        private const int DrawsPerRound = 10;
        private const double BaseN = 0.72;
        private const double BaseR = 0.25;
        private const double BaseSR = 0.03;

        private readonly RandomService randomService;
        private List<ModuleData> openOffer;
        private bool offerRerolled;

        private RunSession(uint seed, RunMode mode, RandomService randomService, GachaEngine gachaEngine, RunManager runManager, ModuleOffer moduleOffer)
        {
            Seed = seed;
            Mode = mode;
            this.randomService = randomService;
            GachaEngine = gachaEngine;
            RunManager = runManager;
            ModuleOffer = moduleOffer;
        }

        public uint Seed { get; }
        public RunMode Mode { get; }
        public GachaEngine GachaEngine { get; }
        public RunManager RunManager { get; }
        public ModuleOffer ModuleOffer { get; }

        /// <summary>열려 있는 제안 카드. 없으면 빈 목록.</summary>
        public IReadOnlyList<ModuleData> CurrentOffer => openOffer ?? (IReadOnlyList<ModuleData>)Array.Empty<ModuleData>();

        /// <summary>열린 제안을 다시 받을 수 있는가. 돌파마다 1번 · 대가 없음 (11/06).</summary>
        public bool CanRerollOffer => openOffer != null && !offerRerolled;

        /// <summary>열린 제안을 이미 다시 받았는가. 모듈을 고르면 false 로 돌아간다.</summary>
        public bool OfferRerolled => offerRerolled;

        public static RunSession Create(uint seed, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules)
        {
            return Create(seed, parts, modules, RunMode.Normal);
        }

        public static RunSession Create(uint seed, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules, RunMode mode)
        {
            if (mode != RunMode.Normal && mode != RunMode.Endless)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            int roundCount = mode == RunMode.Endless ? EndlessRoundCount : NormalRoundCount;

            // 보증 카운터가 런 전체에서 이어져야 하므로 GachaEngine 은 런마다 한 번만 만든다
            // 같은 시드로 같은 판을 재현하려면 모든 난수 소비가 이 RandomService 하나를 거쳐야 한다
            var randomService = new RandomService(seed);
            var gachaEngine = new GachaEngine(randomService, PityThreshold, BaseN, BaseR, BaseSR);
            var partCatalog = new PartCatalog(parts, randomService);
            var runManager = new RunManager(gachaEngine, partCatalog, roundCount, DrawsPerRound);
            var moduleOffer = new ModuleOffer(modules, randomService);
            return new RunSession(seed, mode, randomService, gachaEngine, runManager, moduleOffer);
        }

        /// <summary>
        /// 돌파했을 때 제안 카드를 연다. 이미 열려 있으면 같은 카드를 돌려준다 (난수를 더 쓰지 않는다).
        /// </summary>
        public IReadOnlyList<ModuleData> OpenOffer()
        {
            if (RunManager.State != RunState.RoundCleared)
            {
                throw new InvalidOperationException("돌파 상태가 아닙니다: " + RunManager.State);
            }

            // 다시 뽑으면 공유 난수를 더 소비해 같은 시드의 판이 달라진다. 세이브 후 다시 열어도 카드가 바뀌면 안 된다
            if (openOffer == null)
            {
                openOffer = ModuleOffer.CreateOffer();
            }

            return openOffer;
        }

        /// <summary>
        /// 열린 제안을 버리고 새 카드를 받는다. 돌파마다 1번. 규칙은 처음 제안과 같다 (축이 겹치지 않음 · 같은 조합이 다시 나올 수 있음).
        /// 공유 난수를 쓰므로 부른 뒤에는 저장해야 한다 — 저장하지 않고 껐다 켜면 처음 카드로 돌아가 다시 받을 수 있게 된다.
        /// </summary>
        public IReadOnlyList<ModuleData> RerollOffer()
        {
            if (openOffer == null)
            {
                throw new InvalidOperationException("열린 제안이 없습니다.");
            }

            if (offerRerolled)
            {
                throw new InvalidOperationException("이번 돌파에서 이미 다시 받았습니다.");
            }

            openOffer = ModuleOffer.CreateOffer();
            offerRerolled = true;
            return openOffer;
        }

        /// <summary>
        /// 열린 제안에서 고른 모듈을 장착하고 다음 라운드로 넘어간다.
        /// </summary>
        public void SelectModule(ModuleData module)
        {
            if (openOffer == null)
            {
                throw new InvalidOperationException("열린 제안이 없습니다.");
            }

            if (!openOffer.Contains(module))
            {
                throw new ArgumentException("제안에 없는 모듈입니다.", nameof(module));
            }

            // 장착이 라운드 진행보다 먼저. 새 라운드의 뽑기 횟수는 시작할 때 장착된 모듈로 정해진다
            RunManager.EquipModule(module);
            RunManager.AdvanceRound();
            openOffer = null;
            offerRerolled = false;
        }

        public RunSaveData CaptureSaveData()
        {
            return new RunSaveData
            {
                version = RunSaveData.CurrentVersion,
                mode = Mode,
                seed = Seed,
                randomState = randomService.State,
                drawsWithoutSuperRare = GachaEngine.DrawsWithoutSuperRare,
                round = RunManager.CurrentRound,
                production = RunManager.Production,
                drawsRemaining = RunManager.DrawsRemaining,
                state = RunManager.State,
                streakGrade = RunManager.StreakGrade,
                streakCount = RunManager.StreakCount,
                overclockUsed = RunManager.OverclockUsed,
                overclockDrawsLeft = RunManager.OverclockDrawsLeft,
                roundPartsRecorded = RunManager.RoundPartsComplete,
                roundPartIds = RunManager.RoundPartsComplete ? ToIds(RunManager.RoundParts) : new string[0],
                equippedModuleIds = ToIds(RunManager.EquippedModules),
                offeredModuleIds = ToIds(CurrentOffer),
                offerRerolled = offerRerolled,
            };
        }

        /// <summary>
        /// 세이브 데이터로 런을 되살린다. 부품·모듈 목록은 저장할 때와 같은 에셋이어야 한다 (모듈은 id 로 찾는다).
        /// </summary>
        public static RunSession Restore(RunSaveData data, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.version != RunSaveData.CurrentVersion)
            {
                throw new ArgumentException("지원하지 않는 세이브 버전입니다: " + data.version, nameof(data));
            }

            Dictionary<string, ModuleData> modulesById = IndexById(modules);

            // 새로 만들 때는 난수를 쓰지 않는다. 난수 상태는 맨 마지막에 덮어쓴다
            RunSession session = Create(data.seed, parts, modules, data.mode);
            RunManager runManager = session.RunManager;

            // 확률표·뽑기 횟수는 저장하지 않고 장착 모듈로 다시 계산한다. 계산 규칙이 한 곳에만 있게 하기 위해서다
            foreach (ModuleData module in FindAll(data.equippedModuleIds, modulesById))
            {
                runManager.EquipModule(module);
            }

            runManager.RestoreProgress(data.round, data.production, data.drawsRemaining, data.state, data.streakGrade, data.streakCount);
            runManager.RestoreOverclock(data.overclockUsed, data.overclockDrawsLeft);
            session.GachaEngine.RestorePityCounter(data.drawsWithoutSuperRare);
            List<PartData> roundParts = FindParts(data.roundPartIds, parts);
            if (data.roundPartsRecorded)
            {
                runManager.RestoreRoundParts(roundParts);
            }
            else if (roundParts.Count > 0)
            {
                throw new ArgumentException("부품 목록을 기록하지 않았다는 세이브에 부품이 있습니다.", nameof(data));
            }
            else
            {
                runManager.MarkRoundPartsUnknown();
            }
            session.randomService.RestoreState(data.randomState);

            List<ModuleData> offer = FindAll(data.offeredModuleIds, modulesById);
            if (offer.Count > 0)
            {
                if (data.state != RunState.RoundCleared)
                {
                    throw new ArgumentException("돌파 상태가 아닌데 제안 카드가 저장되어 있습니다: " + data.state, nameof(data));
                }

                if (!session.ModuleOffer.IsPossibleOffer(offer))
                {
                    throw new ArgumentException("제안 카드 조합이 규칙에 맞지 않습니다: " + string.Join(",", data.offeredModuleIds), nameof(data));
                }

                session.openOffer = offer;
                session.offerRerolled = data.offerRerolled;
            }
            else if (data.offerRerolled)
            {
                throw new ArgumentException("제안 카드가 없는데 다시 받았다고 저장되어 있습니다.", nameof(data));
            }

            return session;
        }

        private static string[] ToIds(IReadOnlyList<ModuleData> modules)
        {
            var ids = new string[modules.Count];
            for (int i = 0; i < modules.Count; i++)
            {
                if (string.IsNullOrEmpty(modules[i].Id))
                {
                    throw new InvalidOperationException("id 가 비어 있는 모듈은 저장할 수 없습니다: " + modules[i].DisplayName);
                }

                ids[i] = modules[i].Id;
            }

            return ids;
        }

        private static string[] ToIds(IReadOnlyList<PartData> parts)
        {
            var ids = new string[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                if (string.IsNullOrEmpty(parts[i].Id))
                {
                    throw new InvalidOperationException("id 가 비어 있는 부품은 저장할 수 없습니다: " + parts[i].DisplayName);
                }

                ids[i] = parts[i].Id;
            }

            return ids;
        }

        private static List<PartData> FindParts(string[] ids, IReadOnlyList<PartData> parts)
        {
            var found = new List<PartData>();
            if (ids == null || ids.Length == 0)
            {
                return found;
            }

            var partsById = new Dictionary<string, PartData>();
            foreach (PartData part in parts)
            {
                if (part == null || string.IsNullOrEmpty(part.Id))
                {
                    continue;
                }

                if (partsById.ContainsKey(part.Id))
                {
                    throw new ArgumentException("부품 id 가 중복됩니다: " + part.Id, nameof(parts));
                }

                partsById.Add(part.Id, part);
            }

            foreach (string id in ids)
            {
                if (id == null || !partsById.TryGetValue(id, out PartData part))
                {
                    throw new ArgumentException("부품을 찾을 수 없습니다: " + id);
                }

                found.Add(part);
            }

            return found;
        }

        private static Dictionary<string, ModuleData> IndexById(IReadOnlyList<ModuleData> modules)
        {
            if (modules == null)
            {
                throw new ArgumentNullException(nameof(modules));
            }

            var modulesById = new Dictionary<string, ModuleData>();
            foreach (ModuleData module in modules)
            {
                if (module == null || string.IsNullOrEmpty(module.Id))
                {
                    continue;
                }

                if (modulesById.ContainsKey(module.Id))
                {
                    throw new ArgumentException("모듈 id 가 중복됩니다: " + module.Id, nameof(modules));
                }

                modulesById.Add(module.Id, module);
            }

            return modulesById;
        }

        private static List<ModuleData> FindAll(string[] ids, Dictionary<string, ModuleData> modulesById)
        {
            var found = new List<ModuleData>();
            if (ids == null)
            {
                return found;
            }

            foreach (string id in ids)
            {
                if (id == null || !modulesById.TryGetValue(id, out ModuleData module))
                {
                    throw new ArgumentException("모듈을 찾을 수 없습니다: " + id);
                }

                found.Add(module);
            }

            return found;
        }
    }
}
