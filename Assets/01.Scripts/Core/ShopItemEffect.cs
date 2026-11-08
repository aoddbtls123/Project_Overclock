namespace Overclock.Core
{
    // 값을 바꾸지 말 것: 에셋 · 세이브가 숫자로 저장한다
    public enum ShopItemEffect
    {
        // 오버클럭 배수 증가분 +1 → +2 (부품 · 1번만)
        Supercharger = 0,
        // 다음 제안 카드 4장 (서비스 · 1개 = 제안 1번 · 모아 둘 수 있음)
        SpareCard = 1,
        // 장착 모듈 1개를 빼고 환급 → 바로 대체 제안 3장에서 1개 장착 (서비스 · 방문당 1번)
        Decompose = 2,
    }
}
