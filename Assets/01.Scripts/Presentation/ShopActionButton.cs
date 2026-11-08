using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 잠긴 동안 시작한 누름은 잠금이 풀린 뒤 떼어도 거래하지 않는다.
    public sealed class ShopActionButton : MonoBehaviour, IPointerDownHandler
    {
        private Func<bool> allowed;
        private Action action;
        private bool blockedPress;

        public void Bind(Func<bool> allowed, Action action)
        {
            this.allowed = allowed;
            this.action = action;
            GetComponent<Button>().onClick.RemoveListener(Invoke);
            GetComponent<Button>().onClick.AddListener(Invoke);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            blockedPress = allowed == null || !allowed();
        }

        private void Invoke()
        {
            if (!blockedPress && allowed != null && allowed()) action?.Invoke();
            blockedPress = false;
        }
    }
}
