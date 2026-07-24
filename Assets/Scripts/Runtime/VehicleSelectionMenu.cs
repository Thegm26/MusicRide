using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MusicRoad
{
    public sealed class VehicleSelectionMenu : MonoBehaviour
    {
        private const int PreviewLayer = 31;
        private Action<int> selected;
        private VehicleSpec[] vehicles;
        private GameObject[] prefabs;
        private readonly Image[] cardImages = new Image[7];
        private Text selectedName;
        private Text selectedStats;
        private Text selectedAbility;
        private Text startLabel;
        private GameObject previewStudio;
        private GameObject previewVehicle;
        private RenderTexture previewTexture;
        private int selectedIndex;

        public static void Show(VehicleSpec[] vehicleSpecs, GameObject[] vehiclePrefabs, Action<int> onSelected)
        {
            GameObject menu = new GameObject("Vehicle Selection Menu");
            VehicleSelectionMenu controller = menu.AddComponent<VehicleSelectionMenu>();
            controller.vehicles = vehicleSpecs;
            controller.prefabs = vehiclePrefabs;
            controller.selected = onSelected;
            controller.CreatePreviewStudio();
            controller.CreateInterface();
            controller.Choose(0);
        }

        private void Update()
        {
            if (previewVehicle != null)
            {
                previewVehicle.transform.Rotate(0f, 18f * Time.unscaledDeltaTime, 0f, Space.World);
            }
        }

        private void CreateInterface()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            gameObject.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Image background = CreateImage(transform, "Garage Background", new Color(0.012f, 0.02f, 0.045f, 1f));
            Stretch(background.rectTransform);

            Image headerGlow = CreateImage(background.transform, "Garage Header Glow", new Color(0.02f, 0.38f, 0.5f, 0.34f));
            RectTransform glowRect = headerGlow.rectTransform;
            glowRect.anchorMin = new Vector2(0f, 0.8f);
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;

            Text title = CreateText(background.transform, font, "CHOOSE YOUR RIDE", 54, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(1000f, 75f));
            title.color = new Color(0.25f, 0.94f, 1f);

            Text subtitle = CreateText(background.transform, font, "Pick a vehicle, inspect it, then start the ride", 22, TextAnchor.MiddleCenter);
            SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -105f), new Vector2(1000f, 40f));
            subtitle.color = new Color(0.72f, 0.82f, 0.9f);

            Image previewFrame = CreateImage(background.transform, "Vehicle Showroom", new Color(0.025f, 0.055f, 0.085f, 0.98f));
            SetRect(previewFrame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-390f, 35f), new Vector2(920f, 650f));

            GameObject previewObject = new GameObject("Live Vehicle Preview");
            previewObject.transform.SetParent(previewFrame.transform, false);
            RawImage preview = previewObject.AddComponent<RawImage>();
            preview.texture = previewTexture;
            preview.color = Color.white;
            preview.raycastTarget = false;
            Stretch(preview.rectTransform);

            Image detailPanel = CreateImage(previewFrame.transform, "Selected Vehicle Details", new Color(0.01f, 0.025f, 0.05f, 0.84f));
            RectTransform detailRect = detailPanel.rectTransform;
            detailRect.anchorMin = new Vector2(0f, 0f);
            detailRect.anchorMax = new Vector2(1f, 0f);
            detailRect.pivot = new Vector2(0.5f, 0f);
            detailRect.anchoredPosition = Vector2.zero;
            detailRect.sizeDelta = new Vector2(0f, 145f);

            selectedName = CreateText(detailPanel.transform, font, string.Empty, 34, TextAnchor.UpperLeft);
            SetRect(selectedName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(390f, 48f));
            selectedStats = CreateText(detailPanel.transform, font, string.Empty, 20, TextAnchor.UpperLeft);
            SetRect(selectedStats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -72f), new Vector2(600f, 40f));
            selectedStats.color = new Color(0.72f, 0.82f, 0.9f);
            selectedAbility = CreateText(detailPanel.transform, font, string.Empty, 23, TextAnchor.MiddleRight);
            SetRect(selectedAbility.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(300f, 55f));

            for (int i = 0; i < vehicles.Length; i++)
            {
                int row = i / 2;
                int column = i % 2;
                float x = 350f + column * 310f;
                float y = 220f - row * 132f;
                CreateVehicleButton(background.transform, font, vehicles[i], i, new Vector2(x, y));
            }

            GameObject startObject = new GameObject("Start Ride Button");
            startObject.transform.SetParent(background.transform, false);
            Image startImage = startObject.AddComponent<Image>();
            startImage.color = new Color(0.05f, 0.78f, 0.88f, 1f);
            Button startButton = startObject.AddComponent<Button>();
            startButton.targetGraphic = startImage;
            ColorBlock startColors = startButton.colors;
            startColors.highlightedColor = new Color(0.22f, 1f, 1f, 1f);
            startColors.pressedColor = new Color(1f, 0.6f, 0.1f, 1f);
            startButton.colors = startColors;
            startButton.onClick.AddListener(StartRide);
            SetRect(startImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-390f, 42f), new Vector2(520f, 82f));
            startLabel = CreateText(startObject.transform, font, "START RIDE", 30, TextAnchor.MiddleCenter);
            Stretch(startLabel.rectTransform);

            Text tip = CreateText(background.transform, font, "SPORT + MUSCLE: SHIFT NITRO   •   ALL CARS: WASD DRIVE, SPACE JUMP", 17, TextAnchor.MiddleRight);
            SetRect(tip.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-45f, 24f), new Vector2(850f, 34f));
            tip.color = new Color(0.5f, 0.64f, 0.76f);
        }

        private void CreateVehicleButton(Transform parent, Font font, VehicleSpec vehicle, int index, Vector2 position)
        {
            GameObject buttonObject = new GameObject($"{vehicle.DisplayName} Selector");
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.075f, 0.1f, 0.17f, 0.98f);
            cardImages[index] = image;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.08f, 0.45f, 0.55f, 1f);
            colors.pressedColor = new Color(0.08f, 0.72f, 0.78f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => Choose(index));
            SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(280f, 108f));

            Text name = CreateText(buttonObject.transform, font, vehicle.DisplayName, 24, TextAnchor.UpperLeft);
            SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(-28f, 36f));
            Text category = CreateText(buttonObject.transform, font, $"{vehicle.Category}  •  {Rating(vehicle.SpeedRating)} SPEED", 15, TextAnchor.LowerLeft);
            SetRect(category.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(16f, 13f), new Vector2(-28f, 28f));
            category.color = vehicle.CanNitro ? new Color(1f, 0.64f, 0.12f) : new Color(0.48f, 0.84f, 0.9f);
        }

        private void Choose(int index)
        {
            selectedIndex = Mathf.Clamp(index, 0, vehicles.Length - 1);
            VehicleSpec vehicle = vehicles[selectedIndex];
            for (int i = 0; i < cardImages.Length; i++)
            {
                if (cardImages[i] == null)
                {
                    continue;
                }

                cardImages[i].color = i == selectedIndex
                    ? new Color(0.04f, 0.4f, 0.5f, 1f)
                    : new Color(0.075f, 0.1f, 0.17f, 0.98f);
            }

            selectedName.text = $"{vehicle.DisplayName}  /  {vehicle.Category}";
            selectedStats.text = $"SPEED {Rating(vehicle.SpeedRating)}     HANDLING {Rating(vehicle.HandlingRating)}     {vehicle.WeightLabel}";
            selectedAbility.text = vehicle.CanNitro ? "SHIFT NITRO EQUIPPED" : "NO NITRO";
            selectedAbility.color = vehicle.CanNitro ? new Color(1f, 0.64f, 0.12f) : new Color(0.62f, 0.68f, 0.76f);
            startLabel.text = $"START WITH {vehicle.DisplayName}";
            ShowPreview(selectedIndex);
        }

        private void StartRide()
        {
            Action<int> callback = selected;
            selected = null;
            int index = selectedIndex;
            Destroy(gameObject);
            callback?.Invoke(index);
        }

        private void CreatePreviewStudio()
        {
            previewStudio = new GameObject("Garage Preview Studio");
            previewTexture = new RenderTexture(768, 512, 24, RenderTextureFormat.ARGB32)
            {
                name = "Garage Vehicle Preview",
                antiAliasing = 2
            };
            previewTexture.Create();

            GameObject cameraObject = new GameObject("Garage Preview Camera");
            cameraObject.transform.SetParent(previewStudio.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.035f, 0.055f);
            camera.cullingMask = 1 << PreviewLayer;
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30f;
            camera.targetTexture = previewTexture;
            cameraObject.transform.position = new Vector3(5.7f, 2.8f, -6.4f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.65f, 0f));

            CreatePreviewLight("Showroom Key", new Vector3(-3f, 6f, -4f), 2.1f, 16f);
            CreatePreviewLight("Showroom Fill", new Vector3(4f, 2.5f, 2f), 1.35f, 12f);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Showroom Floor";
            floor.transform.SetParent(previewStudio.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.52f, 0f);
            floor.transform.localScale = new Vector3(3.8f, 0.04f, 3.8f);
            SetLayerRecursively(floor, PreviewLayer);
            Collider floorCollider = floor.GetComponent<Collider>();
            if (floorCollider != null)
            {
                Destroy(floorCollider);
            }
            Material floorMaterial = new Material(Shader.Find("Standard"));
            floorMaterial.color = new Color(0.055f, 0.09f, 0.12f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
        }

        private void CreatePreviewLight(string lightName, Vector3 position, float intensity, float range)
        {
            GameObject lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(previewStudio.transform, false);
            lightObject.transform.localPosition = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.white;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << PreviewLayer;
        }

        private void ShowPreview(int index)
        {
            if (previewVehicle != null)
            {
                Destroy(previewVehicle);
            }

            GameObject prefab = prefabs != null && index < prefabs.Length ? prefabs[index] : null;
            if (prefab == null)
            {
                previewVehicle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                previewVehicle.transform.localScale = vehicles[index].ColliderSize;
            }
            else
            {
                previewVehicle = Instantiate(prefab);
            }

            previewVehicle.name = $"{vehicles[index].DisplayName} Garage Preview";
            previewVehicle.transform.SetParent(previewStudio.transform, false);
            previewVehicle.transform.localPosition = vehicles[index].VisualOffset;
            previewVehicle.transform.localRotation = Quaternion.Euler(0f, -24f, 0f);
            previewVehicle.transform.localScale = Vector3.one;
            StripPreviewPhysics(previewVehicle);
            SetLayerRecursively(previewVehicle, PreviewLayer);
        }

        private static void StripPreviewPhysics(GameObject visual)
        {
            Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Destroy(colliders[i]);
            }

            Rigidbody[] bodies = visual.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Destroy(bodies[i]);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static string Rating(int value)
        {
            return $"{value}/5";
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(Transform parent, Font font, string value, int size, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(value);
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void OnDestroy()
        {
            if (previewStudio != null)
            {
                Destroy(previewStudio);
            }

            if (previewTexture != null)
            {
                previewTexture.Release();
                Destroy(previewTexture);
            }
        }
    }
}
