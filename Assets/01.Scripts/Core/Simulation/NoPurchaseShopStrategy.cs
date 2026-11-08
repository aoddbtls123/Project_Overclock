namespace Overclock.Core
{
    public sealed class NoPurchaseShopStrategy : IShopStrategy
    {
        public string Name => "안 삼";

        public void Visit(RunSession session, RandomService choiceRandom)
        {
        }
    }
}
