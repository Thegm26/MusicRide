using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MusicRoad
{
    public sealed class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }

        private AudioCaptureService capture;
        private ArcadeCarController car;
        private Text scoreText;
        private Text speedText;
        private Text statusText;
        private Text comboText;
        private Text nitroText;
        private Text vehicleText;
        private Text audioInputText;
        private Button connectButton;
        private readonly Image[] audioBars = new Image[3];
        private float score;
        private int combo = 1;
        private float cleanDrivingTime;
        private float lastAudibleUiTime = float.NegativeInfinity;

        public void Initialize(AudioCaptureService audioCapture, ArcadeCarController playerCar)
        {
            Instance = this;
            capture = audioCapture;
            car = playerCar;
            capture.StateChanged += OnCaptureStateChanged;
            CreateInterface();
            OnCaptureStateChanged(
                capture.State,
                Application.isEditor
                    ? "EDITOR DEMO • live computer audio is available in the WebGL build."
                    : "Click CAPTURE COMPUTER AUDIO • share system audio, or the tab playing music.");
        }

        private void Update()
        {
            UpdateAudioMonitor();

            if (car == null)
            {
                return;
            }

            float speedFactor = Mathf.Clamp01(car.SpeedKph / 90f);
            if (car.IsOnRoad && car.SpeedKph > 12f)
            {
                score += Time.deltaTime * (8f + speedFactor * 18f) * combo;
                cleanDrivingTime += Time.deltaTime;
                if (cleanDrivingTime >= 8f && combo < 8)
                {
                    cleanDrivingTime = 0f;
                    combo++;
                }
            }
            else if (!car.IsOnRoad)
            {
                BreakCombo();
            }

            scoreText.text = $"{Mathf.FloorToInt(score):000000}";
            speedText.text = $"{Mathf.RoundToInt(car.SpeedKph):000} km/h";
            comboText.text = $"COMBO x{combo}";
            nitroText.text = car.CanBoost
                ? car.IsBoosting ? "NITRO BOOST" : "NITRO READY"
                : "NO NITRO";
            nitroText.color = car.IsBoosting
                ? new Color(1f, 0.62f, 0.08f)
                : car.CanBoost
                    ? new Color(0.2f, 0.95f, 1f)
                    : new Color(0.58f, 0.63f, 0.7f);
        }

        public void CollectBeatStar()
        {
            score += 250f * combo;
            combo = Mathf.Min(combo + 1, 8);
            cleanDrivingTime = 0f;
        }

        public void BreakCombo()
        {
            combo = 1;
            cleanDrivingTime = 0f;
        }

        private void CreateInterface()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            GameObject canvasObject = new GameObject("Game UI");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            scoreText = CreateText(canvas.transform, font, "000000", 44, TextAnchor.UpperLeft, new Vector2(35f, -30f), new Vector2(420f, 70f));
            speedText = CreateText(canvas.transform, font, "000 km/h", 32, TextAnchor.UpperLeft, new Vector2(38f, -90f), new Vector2(300f, 55f));
            comboText = CreateText(canvas.transform, font, "COMBO x1", 28, TextAnchor.UpperLeft, new Vector2(38f, -140f), new Vector2(300f, 50f));
            nitroText = CreateText(canvas.transform, font, string.Empty, 27, TextAnchor.UpperLeft, new Vector2(38f, -188f), new Vector2(320f, 48f));
            nitroText.color = new Color(0.2f, 0.95f, 1f);
            vehicleText = CreateText(canvas.transform, font, car.VehicleName, 18, TextAnchor.UpperLeft, new Vector2(38f, -232f), new Vector2(400f, 38f));
            vehicleText.color = new Color(0.72f, 0.82f, 0.9f);

            statusText = CreateText(canvas.transform, font, string.Empty, 22, TextAnchor.LowerCenter, new Vector2(0f, 30f), new Vector2(1000f, 80f));
            RectTransform statusRect = statusText.rectTransform;
            statusRect.anchorMin = new Vector2(0.5f, 0f);
            statusRect.anchorMax = new Vector2(0.5f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 24f);

            GameObject buttonObject = new GameObject("Connect Music Button");
            buttonObject.transform.SetParent(canvas.transform, false);
            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(0.05f, 0.75f, 0.9f, 0.92f);
            connectButton = buttonObject.AddComponent<Button>();
            connectButton.targetGraphic = buttonImage;
            connectButton.onClick.AddListener(() => capture.StartCapture());

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.anchoredPosition = new Vector2(-35f, -30f);
            buttonRect.sizeDelta = new Vector2(265f, 64f);

            Text buttonText = CreateText(buttonObject.transform, font, "CONNECT MUSIC", 23, TextAnchor.MiddleCenter, Vector2.zero, buttonRect.sizeDelta);
            RectTransform labelRect = buttonText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            string controlsValue = car.CanBoost
                ? "WASD / ARROWS • DRIVE   |   SHIFT • NITRO   |   LEFT CLICK • FRONTFLIP   |   SPACE • JUMP   |   R • RESET"
                : "WASD / ARROWS • DRIVE   |   LEFT CLICK • FRONTFLIP   |   SPACE • JUMP   |   R • RESET";
            Text controls = CreateText(canvas.transform, font, controlsValue, 18, TextAnchor.UpperRight, new Vector2(-35f, -110f), new Vector2(1120f, 45f));
            RectTransform controlsRect = controls.rectTransform;
            controlsRect.anchorMin = Vector2.one;
            controlsRect.anchorMax = Vector2.one;
            controlsRect.pivot = Vector2.one;
            controlsRect.anchoredPosition = new Vector2(-35f, -110f);

            CreateAudioMonitor(canvas.transform, font);
        }

        private void CreateAudioMonitor(Transform parent, Font font)
        {
            GameObject panelObject = new GameObject("Live Audio Monitor");
            panelObject.transform.SetParent(parent, false);
            Image panel = panelObject.AddComponent<Image>();
            panel.color = new Color(0.025f, 0.035f, 0.065f, 0.82f);
            panel.raycastTarget = false;

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.one;
            panelRect.anchorMax = Vector2.one;
            panelRect.pivot = Vector2.one;
            panelRect.anchoredPosition = new Vector2(-35f, -165f);
            panelRect.sizeDelta = new Vector2(310f, 148f);

            audioInputText = CreateText(panelObject.transform, font, "SONG WINDOW: NOT CONNECTED", 18, TextAnchor.UpperLeft, new Vector2(14f, -12f), new Vector2(285f, 28f));
            string[] labels = { "ROAD", "MID", "HIT" };
            Color[] colors =
            {
                new Color(1f, 0.36f, 0.48f),
                new Color(0.2f, 0.92f, 1f),
                new Color(0.72f, 0.48f, 1f)
            };

            for (int i = 0; i < audioBars.Length; i++)
            {
                float y = -48f - i * 29f;
                CreateText(panelObject.transform, font, labels[i], 14, TextAnchor.MiddleLeft, new Vector2(14f, y), new Vector2(55f, 18f));
                audioBars[i] = CreateMeterBar(panelObject.transform, new Vector2(82f, y), new Vector2(210f, 13f), colors[i]);
            }
        }

        private static Image CreateMeterBar(Transform parent, Vector2 position, Vector2 size, Color color)
        {
            GameObject backgroundObject = new GameObject("Meter Background");
            backgroundObject.transform.SetParent(parent, false);
            Image background = backgroundObject.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.12f);
            background.raycastTarget = false;
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = new Vector2(0f, 1f);
            backgroundRect.anchorMax = new Vector2(0f, 1f);
            backgroundRect.pivot = new Vector2(0f, 1f);
            backgroundRect.anchoredPosition = position;
            backgroundRect.sizeDelta = size;

            GameObject fillObject = new GameObject("Meter Fill");
            fillObject.transform.SetParent(backgroundObject.transform, false);
            Image fill = fillObject.AddComponent<Image>();
            fill.color = color;
            fill.raycastTarget = false;
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.localScale = new Vector3(0f, 1f, 1f);
            return fill;
        }

        private void UpdateAudioMonitor()
        {
            if (audioInputText == null || capture == null)
            {
                return;
            }

            bool receiving = capture.State == AudioCaptureState.Active &&
                capture.HasReceivedFeatures &&
                capture.SecondsSinceLastFeatures < 2f;
            AudioFeatureFrame frame = receiving ? capture.LatestFeatures : default;
            float[] levels = { frame.heavy, frame.vocalLift, frame.onset };

            for (int i = 0; i < audioBars.Length; i++)
            {
                if (audioBars[i] == null)
                {
                    continue;
                }

                Vector3 scale = audioBars[i].rectTransform.localScale;
                scale.x = Mathf.Lerp(scale.x, Mathf.Clamp01(levels[i]), Time.unscaledDeltaTime * 12f);
                audioBars[i].rectTransform.localScale = scale;
            }

            if (receiving && frame.rawLevel > 0.0035f)
            {
                lastAudibleUiTime = Time.unscaledTime;
            }
            bool audible = receiving && Time.unscaledTime - lastAudibleUiTime < 2f;
            if (Application.isEditor)
            {
                audioInputText.text = "SONG WINDOW: EDITOR DEMO";
                audioInputText.color = new Color(1f, 0.78f, 0.35f);
            }
            else if (audible)
            {
                audioInputText.text = $"DSP ACTIVE • ROAD {Mathf.RoundToInt(frame.heavy * 100f):00}%";
                audioInputText.color = new Color(0.35f, 1f, 0.65f);
            }
            else if (capture.State == AudioCaptureState.Active)
            {
                audioInputText.text = "AUDIO CONNECTED • WAITING FOR MUSIC";
                audioInputText.color = new Color(1f, 0.7f, 0.3f);
            }
            else
            {
                audioInputText.text = "SONG WINDOW: NOT CONNECTED";
                audioInputText.color = Color.white;
            }
        }

        private static Text CreateText(Transform parent, Font font, string value, int size, TextAnchor alignment, Vector2 anchoredPosition, Vector2 dimensions)
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
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = dimensions;
            return text;
        }

        private void OnCaptureStateChanged(AudioCaptureState state, string message)
        {
            statusText.text = message;
            bool liveSignal = state == AudioCaptureState.Active && message.StartsWith("LIVE");
            statusText.color = liveSignal
                ? new Color(0.35f, 1f, 0.65f)
                : state == AudioCaptureState.Active
                    ? new Color(1f, 0.7f, 0.3f)
                : state is AudioCaptureState.Denied or AudioCaptureState.NoAudio
                    ? new Color(1f, 0.48f, 0.35f)
                    : Color.white;

            if (connectButton != null)
            {
                connectButton.interactable = state != AudioCaptureState.Requesting && state != AudioCaptureState.Active;
                Text label = connectButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = state == AudioCaptureState.Active ? "AUDIO CONNECTED" : "CAPTURE COMPUTER AUDIO";
                }
            }
        }

        private void OnDestroy()
        {
            if (capture != null)
            {
                capture.StateChanged -= OnCaptureStateChanged;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
