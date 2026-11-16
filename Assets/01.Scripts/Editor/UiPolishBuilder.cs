// [임시] 도트 폰트 적용 · 카드 자세히 버튼 배치 메뉴 (11/16). 실행하면 씬 · 에셋까지 저장한다. 실행 뒤 이 파일과 .meta 를 삭제한다. 커밋에 넣지 않는다
using System.Collections.Generic;
using Overclock.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Overclock.EditorTools
{
    public static class UiPolishBuilder
    {
        private const string PixelFontPath = "Assets/03.Fonts/Galmuri11.ttf";
        private const string PixelFontAssetPath = "Assets/03.Fonts/Galmuri11 SDF.asset";
        private const string NotoFontAssetPath = "Assets/03.Fonts/NotoSansKR-Regular SDF.asset";

        [MenuItem("Overclock/[임시] 도트 폰트 적용")]
        private static void ApplyPixelFont()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(PixelFontPath);
            if (font == null)
            {
                Debug.LogWarning("[임시] 폰트 파일 없음: " + PixelFontPath);
                return;
            }

            TMP_FontAsset pixelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixelFontAssetPath);
            if (pixelFont == null)
            {
                // 갈무리11 은 12 픽셀 격자 (em 1200). 48 로 샘플링하면 그림 1 픽셀 = 아틀라스 4 픽셀이라 SDF 로 키워도 모서리가 선다
                pixelFont = TMP_FontAsset.CreateFontAsset(font, 48, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                pixelFont.name = "Galmuri11 SDF";
                AssetDatabase.CreateAsset(pixelFont, PixelFontAssetPath);
                pixelFont.atlasTextures[0].name = "Galmuri11 SDF Atlas";
                AssetDatabase.AddObjectToAsset(pixelFont.atlasTextures[0], pixelFont);
                pixelFont.material.name = "Galmuri11 SDF Material";
                AssetDatabase.AddObjectToAsset(pixelFont.material, pixelFont);
            }

            // 갈무리에 없는 글자는 기존 노토로
            TMP_FontAsset noto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NotoFontAssetPath);
            if (noto != null)
            {
                pixelFont.fallbackFontAssetTable = new List<TMP_FontAsset> { noto };
            }

            EditorUtility.SetDirty(pixelFont);

            int changed = 0;
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (EditorUtility.IsPersistent(text) || !text.gameObject.scene.IsValid())
                {
                    continue;
                }

                text.font = pixelFont;
                text.fontSharedMaterial = pixelFont.material;
                EditorUtility.SetDirty(text);
                changed++;
            }

            // 코드가 새로 만드는 글자도 같은 폰트로
            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null)
            {
                SerializedObject settingsData = new SerializedObject(settings);
                settingsData.FindProperty("m_defaultFontAsset").objectReferenceValue = pixelFont;
                settingsData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
            Scene();
            Debug.Log("[임시] 도트 폰트 적용 · 글자 " + changed + "개 · 저장 완료");
        }

        [MenuItem("Overclock/[임시] 카드 자세히 버튼 배치")]
        private static void AddCardDetailToggle()
        {
            GameObject panel = GameObject.Find("Canvas/ModuleSelectPanel");
            GrayBoxRunScreen screen = Object.FindFirstObjectByType<GrayBoxRunScreen>(FindObjectsInactive.Include);
            if (panel == null || screen == null)
            {
                Debug.LogWarning("[임시] Run 씬을 열고 실행하세요 (ModuleSelectPanel · GrayBoxRunScreen)");
                return;
            }

            if (panel.transform.Find("CardDetailToggle") != null)
            {
                Debug.LogWarning("[임시] 이미 배치되어 있습니다");
                return;
            }

            // 다시 받기 버튼을 복제해 오른쪽에 둔다 (같은 모양 · 글자). 클릭 연결은 코드가 한다
            Transform reroll = panel.transform.Find("RerollButton");
            GameObject toggle = Object.Instantiate(reroll.gameObject, panel.transform);
            toggle.name = "CardDetailToggle";
            RectTransform rect = (RectTransform)toggle.transform;
            rect.anchoredPosition = new Vector2(400f, -320f);
            rect.sizeDelta = new Vector2(240f, 70f);
            Button button = toggle.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.interactable = true;
            TMP_Text label = toggle.GetComponentInChildren<TMP_Text>(true);
            label.text = "자세히 보기";

            SerializedObject screenData = new SerializedObject(screen);
            screenData.FindProperty("cardDetailToggle").objectReferenceValue = button;
            screenData.FindProperty("cardDetailLabel").objectReferenceValue = label;
            screenData.ApplyModifiedPropertiesWithoutUndo();

            Scene();
            Debug.Log("[임시] 카드 자세히 버튼 배치 · 씬 저장 완료");
        }

        private static void Scene()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
