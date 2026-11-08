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

        private const int CoolerOverclockUses = 2;

        private readonly RandomService randomService;
        private readonly ShopSettings shop;
        private List<ModuleData> openOffer;
        private bool offerRerolled;
        private bool shopOpen;
        private bool hasCooler;
        private int reserveRerolls;
        private bool decomposedThisVisit;

        private RunSession(uint seed, RunMode mode, RandomService randomService, GachaEngine gachaEngine, RunManager runManager, ModuleOffer moduleOffer, ShopSettings shop)
        {
            Seed = seed;
            Mode = mode;
            this.randomService = randomService;
            GachaEngine = gachaEngine;
            RunManager = runManager;
            ModuleOffer = moduleOffer;
            this.shop = shop;
            Wallet = new CreditWallet();

            // 돌파마다 기본급 + 초과분 1:1 (11/08). 상점이 없는 세션은 지급도 없다 (하네스 · 옛 테스트가 그대로)
            if (shop != null)
            {
                runManager.BreakthroughReached = overflow => Wallet.Earn((int)Math.Min((long)shop.BaseWage + overflow, int.MaxValue));
            }
        }

        public uint Seed { get; }
        public RunMode Mode { get; }
        public GachaEngine GachaEngine { get; }
        public RunManager RunManager { get; }
        public ModuleOffer ModuleOffer { get; }
        public CreditWallet Wallet { get; }

        /// <summary>열려 있는 제안 카드. 없으면 빈 목록.</summary>
        public IReadOnlyList<ModuleData> CurrentOffer => openOffer ?? (IReadOnlyList<ModuleData>)Array.Empty<ModuleData>();

        /// <summary>열린 제안을 다시 받을 수 있는가. 돌파마다 무료 1번 (11/06) + 예비 다시 받기 (상점) 보유 수만큼.</summary>
        public bool CanRerollOffer => openOffer != null && (!offerRerolled || reserveRerolls > 0);

        /// <summary>열린 제안에서 무료 다시 받기를 이미 썼는가. 모듈을 고르면 false 로 돌아간다.</summary>
        public bool OfferRerolled => offerRerolled;

        /// <summary>이 세션에 상점이 있는가 (ShopSettings 를 넘겨 만들었는가).</summary>
        public bool HasShop => shop != null;

        /// <summary>상점이 열려 있는가. 짝수 라운드에서 모듈을 고른 뒤 열리고 LeaveShop 으로 닫힌다 (그때 다음 라운드).</summary>
        public bool IsShopOpen => shopOpen;

        public IReadOnlyList<ShopItemData> ShopItems => shop != null ? shop.Items : Array.Empty<ShopItemData>();
        public bool HasCooler => hasCooler;
        public int ReserveRerolls => reserveRerolls;
        public bool DecomposedThisVisit => decomposedThisVisit;

        public static RunSession Create(uint seed, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules)
        {
            return Create(seed, parts, modules, RunMode.Normal);
        }

        public static RunSession Create(uint seed, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules, RunMode mode)
        {
            return Create(seed, parts, modules, mode, null);
        }

        /// <summary>shop 이 null 이면 상점 · 크레딧 지급 없음 (상점 전과 같은 판).</summary>
        public static RunSession Create(uint seed, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules, RunMode mode, ShopSettings shop)
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
            shop?.Validate();
            return new RunSession(seed, mode, randomService, gachaEngine, runManager, moduleOffer, shop);
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

            // 상점 동안은 이미 고른 뒤다. 여기서 새로 열면 카드를 다시 뽑아 난수를 쓴다
            if (shopOpen)
            {
                throw new InvalidOperationException("상점이 열려 있습니다. 모듈은 이미 골랐습니다.");
            }

            // 다시 뽑으면 공유 난수를 더 소비해 같은 시드의 판이 달라진다. 세이브 후 다시 열어도 카드가 바뀌면 안 된다
            if (openOffer == null)
            {
                openOffer = ModuleOffer.CreateOffer();
            }

            return openOffer;
        }

        /// <summary>
        /// 열린 제안을 버리고 새 카드를 받는다. 무료 1번을 먼저 쓰고, 그다음부터 예비 다시 받기를 1개씩 쓴다. 규칙은 처음 제안과 같다 (축이 겹치지 않음 · 같은 조합이 다시 나올 수 있음).
        /// 공유 난수를 쓰므로 부른 뒤에는 저장해야 한다 — 저장하지 않고 껐다 켜면 처음 카드로 돌아가 다시 받을 수 있게 된다.
        /// </summary>
        public IReadOnlyList<ModuleData> RerollOffer()
        {
            if (openOffer == null)
            {
                throw new InvalidOperationException("열린 제안이 없습니다.");
            }

            if (offerRerolled && reserveRerolls == 0)
            {
                throw new InvalidOperationException("이번 돌파에서 이미 다시 받았습니다.");
            }

            openOffer = ModuleOffer.CreateOffer();
            if (!offerRerolled)
            {
                offerRerolled = true;
            }
            else
            {
                reserveRerolls--;
            }

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
            CloseOfferAndContinue();
        }

        // 하네스 전용: 열린 제안에서 아무것도 고르지 않고 넘어간다 (화면에는 이 길이 없다). 짝수 라운드면 상점이 열린다
        internal void DeclineOffer()
        {
            if (openOffer == null)
            {
                throw new InvalidOperationException("열린 제안이 없습니다.");
            }

            CloseOfferAndContinue();
        }

        // 짝수 라운드 돌파 (일반 R2 · R4 · R6 · 무한은 계속) 면 다음 라운드 전에 상점을 연다. 상점에서 바꾼 것 (분해 · 냉각기) 이 다음 라운드부터 바로 적용된다
        private void CloseOfferAndContinue()
        {
            openOffer = null;
            offerRerolled = false;
            if (shop != null && RunManager.CurrentRound % 2 == 0)
            {
                shopOpen = true;
                return;
            }

            RunManager.AdvanceRound();
        }

        /// <summary>상점을 나가 다음 라운드로 넘어간다. 안 사고 나가도 된다.</summary>
        public void LeaveShop()
        {
            RequireShopOpen();
            // 닫힌 상점의 분해 기록이 남으면 그 뒤 세이브를 Restore 가 거부한다
            shopOpen = false;
            decomposedThisVisit = false;
            RunManager.AdvanceRound();
        }

        public ShopItemStatus GetItemStatus(ShopItemData item)
        {
            RequireShopItem(item);
            switch (item.Effect)
            {
                case ShopItemEffect.Cooler:
                    if (hasCooler)
                    {
                        return ShopItemStatus.SoldOut;
                    }

                    break;
                case ShopItemEffect.Decompose:
                    if (decomposedThisVisit)
                    {
                        return ShopItemStatus.SoldOut;
                    }

                    if (RunManager.EquippedModules.Count == 0)
                    {
                        return ShopItemStatus.Unavailable;
                    }

                    break;
            }

            return Wallet.CanAfford(item.Price) ? ShopItemStatus.Available : ShopItemStatus.NotEnoughCredits;
        }

        /// <summary>냉각기 · 예비 다시 받기를 산다 (모듈 분해는 DecomposeModule). 난수를 쓰지 않는다. 산 뒤에는 저장해야 한다.</summary>
        public void BuyItem(ShopItemData item)
        {
            RequireShopOpen();
            RequireShopItem(item);
            if (item.Effect == ShopItemEffect.Decompose)
            {
                throw new InvalidOperationException("모듈 분해는 DecomposeModule 로 한다.");
            }

            RequireAvailable(item);
            Wallet.TrySpend(item.Price);
            if (item.Effect == ShopItemEffect.Cooler)
            {
                hasCooler = true;
                RunManager.SetOverclockUsesPerRound(CoolerOverclockUses);
            }
            else
            {
                reserveRerolls++;
            }
        }

        /// <summary>장착 모듈 하나를 빼고 환급받는다 (가격을 내고 환급액을 받음). 방문마다 1번. 난수를 쓰지 않는다. 산 뒤에는 저장해야 한다.</summary>
        public ModuleData DecomposeModule(int equippedIndex)
        {
            RequireShopOpen();
            ShopItemData item = FindShopItem(ShopItemEffect.Decompose);
            if (item == null)
            {
                throw new InvalidOperationException("이 상점에는 모듈 분해가 없습니다.");
            }

            RequireAvailable(item);
            if (equippedIndex < 0 || equippedIndex >= RunManager.EquippedModules.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(equippedIndex));
            }

            Wallet.TrySpend(item.Price);
            ModuleData removed = RunManager.RemoveModule(equippedIndex);
            Wallet.Earn(item.Refund);
            decomposedThisVisit = true;
            return removed;
        }

        public ShopItemData FindShopItem(ShopItemEffect effect)
        {
            foreach (ShopItemData item in ShopItems)
            {
                if (item.Effect == effect)
                {
                    return item;
                }
            }

            return null;
        }

        private void RequireShopOpen()
        {
            if (!shopOpen)
            {
                throw new InvalidOperationException("상점이 열려 있지 않습니다.");
            }
        }

        private void RequireShopItem(ShopItemData item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            bool found = false;
            foreach (ShopItemData listed in ShopItems)
            {
                if (listed == item)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                throw new ArgumentException("이 상점에 없는 품목입니다: " + item.Id, nameof(item));
            }
        }

        private void RequireAvailable(ShopItemData item)
        {
            ShopItemStatus status = GetItemStatus(item);
            if (status != ShopItemStatus.Available)
            {
                throw new InvalidOperationException("살 수 없습니다: " + item.Id + " " + status);
            }
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
                overclockUseCount = RunManager.OverclockUseCount,
                overclockDrawsLeft = RunManager.OverclockDrawsLeft,
                roundPartsRecorded = RunManager.RoundPartsComplete,
                roundPartIds = RunManager.RoundPartsComplete ? ToIds(RunManager.RoundParts) : new string[0],
                equippedModuleIds = ToIds(RunManager.EquippedModules),
                offeredModuleIds = ToIds(CurrentOffer),
                offerRerolled = offerRerolled,
                credits = Wallet.Balance,
                shopOpen = shopOpen,
                hasCooler = hasCooler,
                reserveRerolls = reserveRerolls,
                decomposedThisVisit = decomposedThisVisit,
            };
        }

        /// <summary>
        /// 세이브 데이터로 런을 되살린다. 부품·모듈 목록은 저장할 때와 같은 에셋이어야 한다 (모듈은 id 로 찾는다).
        /// </summary>
        public static RunSession Restore(RunSaveData data, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules)
        {
            return Restore(data, parts, modules, null);
        }

        /// <summary>상점 세션의 세이브는 같은 ShopSettings 로 되살린다. shop 이 null 이면 상점 값이 있는 세이브를 거부한다.</summary>
        public static RunSession Restore(RunSaveData data, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules, ShopSettings shop)
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
            RunSession session = Create(data.seed, parts, modules, data.mode, shop);
            RunManager runManager = session.RunManager;

            // 확률표·뽑기 횟수는 저장하지 않고 장착 모듈로 다시 계산한다. 계산 규칙이 한 곳에만 있게 하기 위해서다
            foreach (ModuleData module in FindAll(data.equippedModuleIds, modulesById))
            {
                runManager.EquipModule(module);
            }

            runManager.RestoreProgress(data.round, data.production, data.drawsRemaining, data.state, data.streakGrade, data.streakCount);
            RestoreShop(session, data);
            runManager.RestoreOverclock(ReadOverclockUseCount(data), data.overclockDrawsLeft);
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

            if (data.shopOpen && offer.Count > 0)
            {
                throw new ArgumentException("상점이 열렸는데 제안 카드가 저장되어 있습니다.", nameof(data));
            }

            return session;
        }

        // 횟수 필드가 없는 옛 세이브 (0) 에서 썼음 = 1회 (11/08 C)
        private static int ReadOverclockUseCount(RunSaveData data)
        {
            if (data.overclockUseCount < 0)
            {
                throw new ArgumentException("오버클럭 사용 횟수가 음수입니다: " + data.overclockUseCount, nameof(data));
            }

            if (data.overclockUseCount > 0 && !data.overclockUsed)
            {
                throw new ArgumentException("오버클럭을 안 썼다는데 사용 횟수가 있습니다: " + data.overclockUseCount, nameof(data));
            }

            return data.overclockUseCount == 0 && data.overclockUsed ? 1 : data.overclockUseCount;
        }

        // RestoreProgress 다음 · RestoreOverclock 전에 부른다 (냉각기가 오버클럭 한도를 정한다). 게임이 만들 수 있는 값만 받는다
        private static void RestoreShop(RunSession session, RunSaveData data)
        {
            if (data.credits < 0 || data.reserveRerolls < 0)
            {
                throw new ArgumentException("크레딧 · 예비 다시 받기가 음수입니다: " + data.credits + " / " + data.reserveRerolls, nameof(data));
            }

            if (session.shop == null)
            {
                if (data.credits != 0 || data.shopOpen || data.hasCooler || data.reserveRerolls != 0 || data.decomposedThisVisit)
                {
                    throw new ArgumentException("상점이 없는 세션에 상점 값이 저장되어 있습니다.", nameof(data));
                }

                return;
            }

            if (data.hasCooler && session.FindShopItem(ShopItemEffect.Cooler) == null)
            {
                throw new ArgumentException("냉각기를 파는 상점이 아닌데 냉각기가 있습니다.", nameof(data));
            }

            RunManager runManager = session.RunManager;
            if (data.shopOpen && (runManager.State != RunState.RoundCleared || runManager.CurrentRound % 2 != 0))
            {
                throw new ArgumentException("짝수 라운드 돌파가 아닌데 상점이 열려 있습니다: R" + runManager.CurrentRound + " " + runManager.State, nameof(data));
            }

            if (data.decomposedThisVisit && !data.shopOpen)
            {
                throw new ArgumentException("상점이 닫혔는데 이번 방문 분해 기록이 있습니다.", nameof(data));
            }

            session.Wallet.Restore(data.credits);
            session.shopOpen = data.shopOpen;
            session.decomposedThisVisit = data.decomposedThisVisit;
            session.reserveRerolls = data.reserveRerolls;
            session.hasCooler = data.hasCooler;
            if (data.hasCooler)
            {
                runManager.SetOverclockUsesPerRound(CoolerOverclockUses);
            }
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
