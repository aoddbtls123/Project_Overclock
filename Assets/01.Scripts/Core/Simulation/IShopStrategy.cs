namespace Overclock.Core
{
    public interface IShopStrategy
    {
        string Name { get; }

        // 열린 상점에서 산다. 나가기 (LeaveShop) 는 하네스가 한다.
        // 선택에 난수가 필요하면 choiceRandom 만 쓴다 (게임 난수와 분리된 런별 선택 난수원)
        void Visit(RunSession session, RandomService choiceRandom);
    }
}
