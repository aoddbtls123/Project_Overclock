// [임시] 버튼 그림 입히기 메뉴 (11/16). 실행하면 씬까지 저장한다. 실행 뒤 이 파일과 .meta 를 삭제한다. 커밋에 넣지 않는다
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.EditorTools
{
    public static class ButtonSkinBuilder
    {
        private const string ArtFolder = "Assets/04.Art/UI";

        // 9슬라이스 테두리 (픽셀 · 왼쪽 아래 오른쪽 위) — 리벳과 안쪽 둥근 테두리까지
        private static readonly Vector4 Border = new Vector4(8f, 7f, 8f, 7f);

        // 레버 = 투명 클릭 영역 · 오버클럭 = 빨간 철판 그림이 따로 있음
        private static readonly string[] Skip = { "Lever", "OverclockButton" };

        [MenuItem("Overclock/[임시] 버튼 그림 입히기")]
        private static void Build()
        {
            string[] names = { "UI_Button_Normal", "UI_Button_Highlighted", "UI_Button_Pressed", "UI_Button_Disabled" };
            foreach (string name in names)
            {
                string path = ArtFolder + "/" + name + ".png";
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning("[임시] 그림 없음: " + path);
                    return;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.spriteBorder = Border;
                importer.SaveAndReimport();
            }

            Sprite normal = Load(names[0]);
            Sprite highlighted = Load(names[1]);
            Sprite pressed = Load(names[2]);
            Sprite disabled = Load(names[3]);

            int changed = 0;
            foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (EditorUtility.IsPersistent(button) || !button.gameObject.scene.IsValid() || System.Array.IndexOf(Skip, button.name) >= 0)
                {
                    continue;
                }

                Image image = button.targetGraphic as Image;
                if (image == null)
                {
                    continue;
                }

                image.sprite = normal;
                image.type = Image.Type.Sliced;
                image.fillCenter = true;
                // 그림 1 픽셀 = 2 단위 (작은 버튼에서도 테두리가 칸을 덜 차지하게)
                image.pixelsPerUnitMultiplier = 0.5f;
                image.color = Color.white;
                button.transition = Selectable.Transition.SpriteSwap;
                SpriteState state = new SpriteState
                {
                    highlightedSprite = highlighted,
                    selectedSprite = highlighted,
                    pressedSprite = pressed,
                    disabledSprite = disabled,
                };
                button.spriteState = state;
                EditorUtility.SetDirty(image);
                EditorUtility.SetDirty(button);
                changed++;
            }

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[임시] 버튼 그림 입히기 · 버튼 " + changed + "개 · 씬 저장 완료");
        }

        // 회색 박스 때 밝은 버튼 위에 쓰던 어두운 글자 (0.196) 가 어두운 철판 위에서 안 보인다 → 다른 버튼과 같은 밝은 글자로
        [MenuItem("Overclock/[임시] 버튼 글자 밝게")]
        private static void BrightenLabels()
        {
            Color light = new Color(0.9098039f, 0.91764706f, 0.92941177f, 1f);
            int changed = 0;
            foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (EditorUtility.IsPersistent(button) || !button.gameObject.scene.IsValid() || System.Array.IndexOf(Skip, button.name) >= 0)
                {
                    continue;
                }

                foreach (TMPro.TMP_Text label in button.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    if (label.color.r < 0.5f && label.color.g < 0.5f && label.color.b < 0.5f)
                    {
                        label.color = light;
                        EditorUtility.SetDirty(label);
                        changed++;
                    }
                }
            }

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[임시] 버튼 글자 밝게 · " + changed + "개 · 씬 저장 완료");
        }

        private static Sprite Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + name + ".png");
        }
    }
}
