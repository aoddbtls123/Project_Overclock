using UnityEngine;

namespace Overclock.Presentation
{
    [CreateAssetMenu(menuName = "Overclock/Shop Motion Settings")]
    public sealed class ShopMotionSettings : ScriptableObject
    {
        [Min(0f)] public float entranceSeconds = 0.28f;
        [Min(0f)] public float purchaseSeconds = 0.22f;
        [Min(0f)] public float decomposeSeconds = 0.35f;
        [Min(0f)] public float entranceDistance = 24f;
        [Min(0f)] public float fasteningDistance = 5f;
        [Min(0f)] public float cardTravel = 46f;
        [Min(0.15f)] public float repeatGuardSeconds = 0.3f;
    }
}
