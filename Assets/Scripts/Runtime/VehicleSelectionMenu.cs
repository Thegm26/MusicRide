using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MusicRoad
{
    public sealed class VehicleSelectionMenu : MonoBehaviour
    {
        private Action<int> selected;

        public static void Show(VehicleSpec[] vehicles, Action<int> onSelected)
        {
            GameObject menu = new GameObject("Vehicle Selection Menu");
            VehicleSelectionMenu controller = menu.AddComponent<VehicleSelectionMenu>();
            controller.selected = onSelected;
            controller.CreateInterface(vehicles);
        }

        private void CreateInterface(VehicleSpec[] vehicles)
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
            Image background = CreateImage(transform, "Garage Background", new Color(0.018f, 0.026f, 0.055f, 1f));
            Stretch(background.rectTransform);

            Image glow = CreateImage(background.transform, "Garage Glow", new Color(0.04f, 0.34f, 0.48f, 0.3f));
            RectTransform glowRect = glow.rectTransform;
            glowRect.anchorMin = new Vector2(0f, 0.78f);
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;

            Text title = CreateText(background.transform, font, "CHOOSE YOUR RIDE", 60, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(1000f, 85f));
            title.color = new Color(0.25f, 0.94f, 1f);

            Text subtitle = CreateText(background.transform, font, "Every vehicle changes speed, weight, steering and abilities", 24, TextAnchor.MiddleCenter);
            SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(1100f, 45f));
            subtitle.color = new Color(0.75f, 0.82f, 0.9f);

            for (int i = 0; i < vehicles.Length; i++)
            {
                int row = i < 4 ? 0 : 1;
                int rowCount = row == 0 ? 4 : vehicles.Length - 4;
                int column = row == 0 ? i : i - 4;
                float x = (column - (rowCount - 1) * 0.5f) * 390f;
                float y = row == 0 ? 135f : -210f;
                CreateVehicleCard(background.transform, font, vehicles[i], i, new Vector2(x, y));
            }

            Text tip = CreateText(background.transform, font, "SPORT + MUSCLE: SHIFT NITRO   •   ALL CARS: WASD DRIVE, SPACE JUMP", 20, TextAnchor.MiddleCenter);
            SetRect(tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1200f, 42f));
            tip.color = new Color(0.56f, 0.69f, 0.8f);
        }

        private void CreateVehicleCard(Transform parent, Font font, VehicleSpec vehicle, int index, Vector2 position)
        {
            GameObject cardObject = new GameObject($"{vehicle.DisplayName} Card");
            cardObject.transform.SetParent(parent, false);
            Image card = cardObject.AddComponent<Image>();
            card.color = vehicle.CanNitro
                ? new Color(0.04f, 0.32f, 0.42f, 0.96f)
                : new Color(0.09f, 0.12f, 0.2f, 0.96f);
            Button button = cardObject.AddComponent<Button>();
            button.targetGraphic = card;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.12f, 0.64f, 0.72f, 1f);
            colors.pressedColor = new Color(0.08f, 0.8f, 0.85f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(() => Select(index));

            SetRect(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(350f, 290f));

            Text category = CreateText(cardObject.transform, font, vehicle.Category, 17, TextAnchor.UpperCenter);
            SetRect(category.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(-30f, 30f));
            category.color = new Color(0.37f, 0.95f, 1f);

            Text name = CreateText(cardObject.transform, font, vehicle.DisplayName, 30, TextAnchor.UpperCenter);
            SetRect(name.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(-24f, 54f));

            Text stats = CreateText(
                cardObject.transform,
                font,
                $"SPEED      {Rating(vehicle.SpeedRating)}\nHANDLING  {Rating(vehicle.HandlingRating)}\nWEIGHT     {vehicle.WeightLabel}",
                20,
                TextAnchor.UpperLeft);
            SetRect(stats.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(-62f, 96f));
            stats.color = new Color(0.84f, 0.88f, 0.94f);

            Text nitro = CreateText(cardObject.transform, font, vehicle.CanNitro ? "NITRO EQUIPPED" : "NO NITRO", 21, TextAnchor.MiddleCenter);
            SetRect(nitro.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 23f), new Vector2(-30f, 42f));
            nitro.color = vehicle.CanNitro ? new Color(1f, 0.64f, 0.12f) : new Color(0.58f, 0.63f, 0.7f);
        }

        private void Select(int index)
        {
            Action<int> callback = selected;
            selected = null;
            Destroy(gameObject);
            callback?.Invoke(index);
        }

        private static string Rating(int value)
        {
            return $"{value} / 5";
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
    }
}
