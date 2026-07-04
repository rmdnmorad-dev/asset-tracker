using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VampFrost
{
    public static class UIBuilder
    {
        public static Canvas Root { get; private set; }

        static Font font;
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static readonly Color PanelDark = new Color(.05f, .07f, .12f, .92f);
        public static readonly Color BtnBlue = new Color(.10f, .16f, .28f, 1f);
        public static readonly Color Frost = new Color(.55f, .88f, 1f);
        public static readonly Color BloodRed = new Color(.78f, .12f, .18f);

        public static void Init()
        {
            var go = new GameObject("UI");
            Root = go.AddComponent<Canvas>();
            Root.renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = .5f;
            go.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        static RectTransform RT(GameObject go, Transform parent)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static Image Stretch(Transform parent, string name, Color c)
        {
            var go = new GameObject(name);
            var rt = RT(go, parent);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = SpriteFactory.White;
            img.color = c;
            return img;
        }

        public static Image Panel(Transform parent, string name, Vector2 size, Vector2 pos,
                                  Color c, Vector2? anchor = null)
        {
            var go = new GameObject(name);
            var rt = RT(go, parent);
            var a = anchor ?? new Vector2(.5f, .5f);
            rt.anchorMin = rt.anchorMax = a;
            rt.pivot = a;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.sprite = SpriteFactory.White;
            img.color = c;
            return img;
        }

        public static Text Txt(Transform parent, string s, int size, Color c, Vector2 pos,
                               Vector2 box, TextAnchor align = TextAnchor.MiddleCenter,
                               Vector2? anchor = null, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("txt");
            var rt = RT(go, parent);
            var a = anchor ?? new Vector2(.5f, .5f);
            rt.anchorMin = rt.anchorMax = a; rt.pivot = a;
            rt.sizeDelta = box; rt.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.font = Font; t.fontSize = size; t.color = c;
            t.alignment = align; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = s;
            t.raycastTarget = false;
            return t;
        }

        public static Button Btn(Transform parent, string label, Vector2 size, Vector2 pos,
                                 UnityAction onClick, Vector2? anchor = null,
                                 int fontSize = 26, Color? bg = null)
        {
            var img = Panel(parent, "btn_" + label, size, pos, bg ?? BtnBlue, anchor);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.35f, 1.35f, 1.5f);
            cb.pressedColor = new Color(.7f, 1.6f, 1.8f);
            cb.disabledColor = new Color(.5f, .5f, .5f, .6f);
            b.colors = cb;
            Txt(img.transform, label, fontSize, Color.white, Vector2.zero, size);
            b.onClick.AddListener(() => GameEvents.OnUIClick?.Invoke());
            if (onClick != null) b.onClick.AddListener(onClick);
            AddHover(img.gameObject);
            return b;
        }

        public static void AddHover(GameObject go)
        {
            var trig = go.AddComponent<EventTrigger>();
            var e = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            e.callback.AddListener(_ => GameEvents.OnUIHover?.Invoke());
            trig.triggers.Add(e);
        }

        /// Returns the fill RectTransform. Set fill.localScale.x = 0..1.
        public static RectTransform Bar(Transform parent, Vector2 size, Vector2 pos,
                                        Color bg, Color fill, Vector2? anchor = null)
        {
            var back = Panel(parent, "bar", size, pos, bg, anchor);
            var go = new GameObject("fill");
            var rt = RT(go, back.transform);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, .5f);
            rt.pivot = new Vector2(0f, .5f);
            rt.sizeDelta = new Vector2(size.x - 4, size.y - 4);
            rt.anchoredPosition = new Vector2(2, 0);
            var img = go.AddComponent<Image>();
            img.sprite = SpriteFactory.White; img.color = fill;
            img.raycastTarget = false;
            return rt;
        }

        public static Slider SliderRow(Transform parent, string label, Vector2 pos,
                                       float init, UnityAction<float> onChange)
        {
            var row = Panel(parent, "row_" + label, new Vector2(460, 44), pos,
                            new Color(0, 0, 0, 0));
            Txt(row.transform, label, 22, Color.white, new Vector2(0, 0),
                new Vector2(130, 40), TextAnchor.MiddleLeft, new Vector2(0f, .5f));

            var sgo = new GameObject("slider");
            var srt = RT(sgo, row.transform);
            srt.anchorMin = srt.anchorMax = new Vector2(1f, .5f);
            srt.pivot = new Vector2(1f, .5f);
            srt.sizeDelta = new Vector2(300, 22);
            srt.anchoredPosition = new Vector2(0, 0);
            var slider = sgo.AddComponent<Slider>();

            var bg = Stretch(sgo.transform, "bg", new Color(.12f, .15f, .22f));
            bg.raycastTarget = true;

            var fillArea = new GameObject("fillArea");
            var fart = RT(fillArea, sgo.transform);
            fart.anchorMin = new Vector2(0, .25f); fart.anchorMax = new Vector2(1, .75f);
            fart.offsetMin = new Vector2(4, 0); fart.offsetMax = new Vector2(-4, 0);
            var fillGo = new GameObject("fill");
            var frt = RT(fillGo, fillArea.transform);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            var fimg = fillGo.AddComponent<Image>();
            fimg.sprite = SpriteFactory.White; fimg.color = Frost;

            var handleArea = new GameObject("handleArea");
            var hart = RT(handleArea, sgo.transform);
            hart.anchorMin = Vector2.zero; hart.anchorMax = Vector2.one;
            hart.offsetMin = new Vector2(8, 0); hart.offsetMax = new Vector2(-8, 0);
            var handleGo = new GameObject("handle");
            var hrt = RT(handleGo, handleArea.transform);
            hrt.sizeDelta = new Vector2(18, 30);
            var himg = handleGo.AddComponent<Image>();
            himg.sprite = SpriteFactory.White; himg.color = Color.white;

            slider.fillRect = frt;
            slider.handleRect = hrt;
            slider.targetGraphic = himg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 1;
            slider.value = init;
            slider.onValueChanged.AddListener(onChange);
            AddHover(sgo);
            return slider;
        }
    }
}
