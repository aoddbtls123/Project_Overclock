// [임시] 기계 스프라이트 씬 배치 메뉴 (11/16). 한 번 실행하고 씬 저장 뒤 이 파일과 .meta 를 삭제한다. 커밋에 넣지 않는다
using Overclock.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.EditorTools
{
    public static class MachineSpriteSceneBuilder
    {
        private const string ArtFolder = "Assets/04.Art/Machine";
        private const string ContainerName = "MachineArt";

        // 320×320 원본 캔버스를 4배로 그린다 (1920×1080 기준 1 픽셀 = 4 단위)
        private const float Scale = 4f;
        private const float CanvasSize = 320f * Scale;

        // 그림 캔버스 가운데가 GachaMachine 안에서 놓이는 높이 (위 정보 패널과 겹치지 않게 내림)
        private const float ArtCenterY = -38f;

        [MenuItem("Overclock/[임시] 기계 스프라이트 배치")]
        private static void Build()
        {
            GameObject machineObject = GameObject.Find("Canvas/ShakeRoot/GachaMachine");
            if (machineObject == null)
            {
                Debug.LogWarning("[임시] Run 씬을 열고 실행하세요 (Canvas/ShakeRoot/GachaMachine 없음)");
                return;
            }

            Transform machine = machineObject.transform;
            if (machine.Find(ContainerName) != null)
            {
                Debug.LogWarning("[임시] 이미 배치되어 있습니다. 다시 하려면 Run.unity 를 Discard 한 뒤 실행하세요");
                return;
            }

            ConfigureImporters();

            Transform shakeRoot = machine.parent;
            GachaMachineView machineView = machine.GetComponent<GachaMachineView>();
            MachineShutterView shutterView = Object.FindFirstObjectByType<MachineShutterView>(FindObjectsInactive.Include);
            RunClearView clearView = Object.FindFirstObjectByType<RunClearView>(FindObjectsInactive.Include);
            RunFailView failView = Object.FindFirstObjectByType<RunFailView>(FindObjectsInactive.Include);

            // 1. 그림 레이어 (뒤 → 앞). 모두 같은 320 캔버스 좌표라 같은 크기 · 같은 가운데로 겹치면 맞물린다
            RectTransform art = CreateRect(ContainerName, machine);
            art.SetSiblingIndex(0);
            art.anchoredPosition = new Vector2(0f, ArtCenterY);
            art.sizeDelta = new Vector2(CanvasSize, CanvasSize);

            Image gearSpin = CreateLayer("GearSpin", art, "Machine_GearSpin");
            SetPivotAt(gearSpin.rectTransform, 108f, 154.5f);
            Image gearHub = CreateLayer("GearHub", art, "Machine_GearHub");
            Image pipeLeft = CreateLayer("PipeLeft", art, "Machine_PipeLeft");
            Image pipeRight = CreateLayer("PipeRight", art, "Machine_PipeRight");
            Image body = CreateLayer("Body", art, "Machine_Body");
            Image leverPlate = CreateLayer("LeverPlate", art, "Machine_LeverPlate");
            Image leverHandle = CreateLayer("LeverHandle", art, "Machine_LeverHandle");
            SetPivotAt(leverHandle.rectTransform, 209f, 160f);
            Image leverCover = CreateLayer("LeverCover", art, "Machine_LeverCover");
            Image topLampFrame = CreateLayer("TopLampFrame", art, "Machine_TopLampFrame");
            Image topLampOff = CreateLayer("TopLampOff", art, "Machine_TopLampOff");
            Image topLampOn = CreateLayer("TopLampOn", art, "Machine_TopLampOn");
            topLampOn.color = new Color(1f, 1f, 1f, 0f);
            Image lampLeftOff = CreateLayer("LampLeftOff", art, "Machine_LampLeftOff");
            Image lampLeftOn = CreateLayer("LampLeftOn", art, "Machine_LampLeftOn");
            Image lampRightOff = CreateLayer("LampRightOff", art, "Machine_LampRightOff");
            Image lampRightOn = CreateLayer("LampRightOn", art, "Machine_LampRightOn");

            // 2. 회색 박스 부품 숨김 (삭제하지 않음)
            HideChild(machine, "Body");
            HideChild(machine, "ClearLamp");
            HideChild(machine, "GearBig");
            HideChild(machine, "GearSmall");
            HideChild(machine, "PistonHousing");
            HideChild(machine, "PistonRod");
            HideChild(machine, "GaugeDial");
            HideChild(machine, "Needle");
            Image machineBackground = machine.GetComponent<Image>();
            if (machineBackground != null)
            {
                machineBackground.enabled = false;
            }

            // 3. 출력 창: 회색 박스 창 → 구멍 크기 (62×74 픽셀). Outline(등급 테두리)은 이 오브젝트에 그대로 둔다
            RectTransform outputWindow = FindRect(machine, "OutputWindow");
            outputWindow.anchoredPosition = PixelToMachine(160f, 156f);
            outputWindow.sizeDelta = new Vector2(62f * Scale, 74f * Scale);
            Image outputWindowImage = outputWindow.GetComponent<Image>();
            outputWindowImage.color = new Color32(57, 49, 48, 255);
            Image windowArt = CreateStretched("WindowArt", outputWindow, "Machine_Window");
            // 셔터 고정대는 창 그림보다 앞이어야 한다 (몸통과 겹치는 픽셀은 몸통 색으로 구워 둠)
            Image fixture = CreateLayer("ShutterFixture", outputWindow, "Machine_ShutterFixture");
            fixture.rectTransform.anchoredPosition = new Vector2(0f, ArtCenterY) - outputWindow.anchoredPosition;

            // 4. 레버: 클릭 영역만 남기고 회색 그림은 숨김. 손잡이 회전은 그림 레이어가 한다
            RectTransform lever = FindRect(machine, "Lever");
            lever.anchoredPosition = PixelToMachine(217.5f, 143.5f);
            lever.sizeDelta = new Vector2(180f, 280f);
            Image leverHitArea = lever.GetComponent<Image>();
            if (leverHitArea != null)
            {
                leverHitArea.color = new Color(0f, 0f, 0f, 0f);
                leverHitArea.raycastTarget = true;
            }

            HideChild(lever, "LeverAxis");
            HideChild(lever, "LeverHandle");
            RectTransform leverLabel = FindRect(lever, "LeverLabel");
            CenterAnchors(leverLabel);
            leverLabel.anchoredPosition = new Vector2(100f, -178f);

            // 5. 기계 위에 얹혀 있던 것들
            RectTransform counter = FindRect(machine, "MechanicalCounter");
            counter.anchoredPosition = PixelToMachine(160f, 105f);
            counter.localScale = new Vector3(0.85f, 0.85f, 1f);
            FindRect(machine, "OverclockButton").anchoredPosition = new Vector2(440f, -40f);
            FindRect(machine, "HarvestTray").anchoredPosition = new Vector2(0f, -385f);

            RectTransform ventLeft = FindRect(machine, "SteamVentL");
            ventLeft.anchoredPosition = PixelToMachine(104f, 96f);
            DisableGraphic(ventLeft);
            RectTransform ventRight = FindRect(machine, "SteamVentR");
            ventRight.anchoredPosition = PixelToMachine(215f, 96f);
            DisableGraphic(ventRight);

            Transform steam = machine.Find("Steam");
            string[] puffSprites = { "Steam_Short", "Steam_Rest1", "Steam_Burst" };
            for (int i = 0; steam != null && i < steam.childCount; i++)
            {
                Image puff = steam.GetChild(i).GetComponent<Image>();
                if (puff != null)
                {
                    puff.sprite = LoadSprite(puffSprites[i % puffSprites.Length]);
                    puff.preserveAspect = true;
                }
            }

            // 6. 출력 창 안 (ShakeRoot 자식들). 창 가운데 = ShakeRoot 기준 machine 위치 + 창 위치
            Vector2 machineOffset = ((RectTransform)machine).anchoredPosition;
            Vector2 windowCenter = machineOffset + outputWindow.anchoredPosition;
            RectTransform glow = FindRect(shakeRoot, "IconGlow");
            glow.anchoredPosition = windowCenter + new Vector2(0f, 52f);
            glow.sizeDelta = new Vector2(200f, 200f);
            FindRect(shakeRoot, "LastPartIcon").anchoredPosition = windowCenter + new Vector2(0f, 52f);
            RectTransform lastPartText = FindRect(shakeRoot, "LastPartText");
            lastPartText.anchoredPosition = windowCenter + new Vector2(0f, -88f);
            lastPartText.sizeDelta = new Vector2(240f, 60f);
            TMP_Text lastPartLabel = lastPartText.GetComponent<TMP_Text>();
            if (lastPartLabel != null)
            {
                lastPartLabel.enableAutoSizing = true;
                lastPartLabel.fontSizeMin = 18f;
                lastPartLabel.fontSizeMax = 36f;
            }

            FindRect(shakeRoot, "ResultText").anchoredPosition = new Vector2(0f, -505f);

            // 7. 셔터: 고정대 아래 구멍 (62×65 픽셀). 판 1장 = 철판 10장을 합친 그림
            RectTransform shutter = FindRect(shakeRoot, "Shutter");
            shutter.anchoredPosition = machineOffset + PixelToMachine(160f, 160.5f);
            shutter.sizeDelta = new Vector2(62f * Scale, 65f * Scale);
            RectTransform plate = FindRect(shutter, "ShutterMask/Plate");
            plate.sizeDelta = shutter.sizeDelta;
            plate.anchoredPosition = new Vector2(0f, shutter.sizeDelta.y);
            Image plateImage = plate.GetComponent<Image>();
            plateImage.sprite = LoadSprite("Machine_ShutterPlate");
            plateImage.color = Color.white;
            for (int i = 0; i < plate.childCount; i++)
            {
                plate.GetChild(i).gameObject.SetActive(false);
            }

            string[] gapSprites = { "Steam_Rest1", "Steam_Rest2", "Steam_Rest3" };
            int gapIndex = 0;
            for (int i = 0; i < shutter.childCount; i++)
            {
                Transform child = shutter.GetChild(i);
                if (!child.name.StartsWith("GapPuff"))
                {
                    continue;
                }

                Image gapPuff = child.GetComponent<Image>();
                if (gapPuff != null)
                {
                    gapPuff.sprite = LoadSprite(gapSprites[gapIndex % gapSprites.Length]);
                    gapPuff.preserveAspect = true;
                    gapIndex++;
                }
            }

            if (shutterView != null)
            {
                SerializedObject shutterSettings = new SerializedObject(shutterView.Settings);
                SetFloatArray(shutterSettings.FindProperty("gapPuffOffsetsX"), new[] { -68f, 51f, -23f, 85f, 11f, -90f, 34f });
                shutterSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(shutterView.Settings);
            }

            // 8. 연결 교체
            SerializedObject machineData = new SerializedObject(machineView);
            SerializedProperty gears = machineData.FindProperty("gears");
            gears.arraySize = 1;
            gears.GetArrayElementAtIndex(0).FindPropertyRelative("gear").objectReferenceValue = gearSpin.rectTransform;
            gears.GetArrayElementAtIndex(0).FindPropertyRelative("speedRatio").floatValue = 1f;
            machineData.FindProperty("leverHandle").objectReferenceValue = leverHandle.rectTransform;
            machineData.FindProperty("piston").objectReferenceValue = null;
            machineData.FindProperty("needle").objectReferenceValue = null;
            machineData.ApplyModifiedPropertiesWithoutUndo();

            if (clearView != null)
            {
                SerializedObject clearData = new SerializedObject(clearView);
                SetObjectArray(clearData.FindProperty("lightStages"), topLampOn, lampLeftOn, lampRightOn, windowArt);
                clearData.ApplyModifiedPropertiesWithoutUndo();
            }

            if (failView != null)
            {
                SerializedObject failData = new SerializedObject(failView);
                SetObjectArray(failData.FindProperty("lightStages"), lampLeftOn, lampRightOn);
                SetObjectArray(failData.FindProperty("dimTargets"), body, gearSpin, gearHub, pipeLeft, pipeRight, leverPlate, leverHandle, leverCover,
                    topLampFrame, topLampOff, lampLeftOff, lampRightOff, windowArt, fixture, plateImage);
                failData.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(machineObject.scene);
            Debug.Log("[임시] 기계 스프라이트 배치 완료 — Ctrl+S 로 씬 저장, File > Save Project 로 ShutterSettings 저장");
        }

        private static void ConfigureImporters()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static Sprite LoadSprite(string fileName)
        {
            string path = ArtFolder + "/" + fileName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning("[임시] 스프라이트 없음: " + path);
            }

            return sprite;
        }

        // 320 캔버스 픽셀 좌표 (왼쪽 위 원점) → GachaMachine 안 위치
        private static Vector2 PixelToMachine(float pixelX, float pixelY)
        {
            return new Vector2((pixelX - 160f) * Scale, ArtCenterY + (160f - pixelY) * Scale);
        }

        // 캔버스 전체 크기 레이어의 피벗을 부품 축에 맞춘다. 위치도 같이 옮겨 그림은 제자리
        private static void SetPivotAt(RectTransform rect, float pixelX, float pixelY)
        {
            rect.pivot = new Vector2(pixelX / 320f, 1f - pixelY / 320f);
            rect.anchoredPosition = new Vector2((pixelX - 160f) * Scale, (160f - pixelY) * Scale);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            CenterAnchors(rect);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static Image CreateLayer(string name, Transform parent, string spriteName)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.sizeDelta = new Vector2(CanvasSize, CanvasSize);
            return AddImage(rect, spriteName);
        }

        private static Image CreateStretched(string name, Transform parent, string spriteName)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            return AddImage(rect, spriteName);
        }

        private static Image AddImage(RectTransform rect, string spriteName)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(spriteName);
            image.raycastTarget = false;
            return image;
        }

        private static void CenterAnchors(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
        }

        private static RectTransform FindRect(Transform parent, string path)
        {
            Transform found = parent.Find(path);
            if (found == null)
            {
                throw new System.InvalidOperationException("[임시] 씬에서 찾지 못함: " + parent.name + "/" + path);
            }

            return (RectTransform)found;
        }

        private static void HideChild(Transform parent, string path)
        {
            FindRect(parent, path).gameObject.SetActive(false);
        }

        private static void DisableGraphic(Transform target)
        {
            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.enabled = false;
            }
        }

        private static void SetObjectArray(SerializedProperty property, params Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void SetFloatArray(SerializedProperty property, float[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).floatValue = values[i];
            }
        }
    }
}
