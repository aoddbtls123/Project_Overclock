// [임시] 11/05 출력 창 등급 테두리 — 씬 연결 메뉴. 실행 · Ctrl+S 후 이 파일을 지운다 (커밋 안 함)
using Overclock.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.EditorTools
{
    public static class MachineFrameSceneBuilder
    {
        [MenuItem("Overclock/[임시] Build Machine Output Frame")]
        private static void Build()
        {
            GachaMachineView machine = Object.FindFirstObjectByType<GachaMachineView>(FindObjectsInactive.Include);
            if (machine == null)
            {
                Debug.LogWarning("[임시] GachaMachineView 가 없습니다 — Run 씬을 열고 다시 실행하세요");
                return;
            }

            Transform window = FindChild(machine.transform, "OutputWindow");
            Image windowImage = window != null ? window.GetComponent<Image>() : null;
            if (windowImage == null)
            {
                Debug.LogWarning("[임시] 기계 아래에 Image 가 있는 OutputWindow 가 없습니다");
                return;
            }

            Outline outline = window.GetComponent<Outline>();
            if (outline == null)
            {
                outline = Undo.AddComponent<Outline>(window.gameObject);
            }

            // 평소에는 꺼 둔다. 공개 순간 GachaMachineView 가 등급 색 · 두께로 켠다
            Undo.RecordObject(outline, "Output frame");
            outline.effectColor = new Color(0.6f, 0.63f, 0.65f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            outline.enabled = false;

            SerializedObject machineObject = new SerializedObject(machine);
            machineObject.FindProperty("outputFrame").objectReferenceValue = outline;
            machineObject.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(machine.gameObject.scene);
            Debug.Log("[임시] 출력 창 테두리 연결 완료: " + window.name + " — Ctrl+S 로 씬 저장 후 이 메뉴 스크립트를 지우세요");
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform found = FindChild(child, childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
