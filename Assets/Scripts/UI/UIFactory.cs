using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UiFonts
{
    static Font title, body;

    public static void Init(UISkin skin)
    {
        title = Font.CreateDynamicFontFromOSFont(skin ? skin.titleFonts : new[] { "Georgia" }, 32);
        body = Font.CreateDynamicFontFromOSFont(skin ? skin.bodyFonts : new[] { "Georgia" }, 24);
    }

    public static Font Title => title ? title : (title = Font.CreateDynamicFontFromOSFont(new[] { "Castellar", "Georgia" }, 32));
    public static Font Body => body ? body : (body = Font.CreateDynamicFontFromOSFont(new[] { "Palatino Linotype", "Georgia" }, 24));
}

// uGUI built from code on a 1920x1080 canvas. Frames are pixel art: 1 art pixel = 2 canvas pixels.
public static class UI
{
    public static UISkin Skin;
    public static readonly Color Bone = new Color32(232, 220, 192, 255), Ember = new Color32(242, 163, 58, 255),
        Gold = new Color32(214, 178, 100, 255), Dim = new Color32(150, 142, 132, 255), Bad = new Color32(255, 96, 84, 255),
        Good = new Color32(140, 210, 110, 255), Blood = new Color32(139, 26, 26, 255), Violet = new Color32(170, 110, 230, 255);

    public const float PixelScale = 0.5f;   // Image.pixelsPerUnitMultiplier with referencePixelsPerUnit = 32 → 2 px per art pixel

    public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(Transform parent, string name, float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    public static Image Image(Transform parent, string name, Sprite s, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color? color = null, bool raycast = false)
    {
        var img = Rect(parent, name, anchor, pivot, pos, size).gameObject.AddComponent<Image>();
        img.sprite = s;
        img.color = color ?? Color.white;
        img.raycastTarget = raycast;
        if (s && s.border.sqrMagnitude > 0)
        {
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = PixelScale;
        }
        return img;
    }

    public static Image Fill(Transform parent, string name, Color color, bool raycast = false)
    {
        var img = Stretch(parent, name).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    // Background picture that covers the screen keeping its aspect (cropping the excess).
    public static Image Cover(Transform parent, Sprite s, Color? tint = null)
    {
        var img = Stretch(parent, "Background").gameObject.AddComponent<Image>();
        img.sprite = s;
        img.color = tint ?? Color.white;
        img.raycastTarget = true;
        var fit = img.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = s ? s.rect.width / s.rect.height : 16f / 9f;
        return img;
    }

    public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Sprite style = null, bool raycast = true) =>
        Image(parent, name, style ? style : Skin ? Skin.panel : null, anchor, pivot, pos, size, null, raycast);

    public static Text Text(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize,
        TextAnchor align, Color color, bool title = false, bool shadow = true)
    {
        var t = Rect(parent, name, anchor, pivot, pos, size).gameObject.AddComponent<Text>();
        t.font = title ? UiFonts.Title : UiFonts.Body;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = color;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (shadow)
        {
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.9f);
            sh.effectDistance = new Vector2(2, -2);
        }
        return t;
    }

    public static Text Title(Transform parent, string text, Vector2 anchor, Vector2 pos, int size = 44, float width = 1200)
    {
        var t = Text(parent, "Title", anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(width, size * 1.6f), size, TextAnchor.MiddleCenter, Gold, true);
        t.text = text;
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.25f, 0.05f, 0.05f, 0.9f);
        o.effectDistance = new Vector2(2, -2);
        return t;
    }

    public class Btn
    {
        public Button button;
        public Text label;
        public Image image;
        public RectTransform rect;
    }

    public static Btn Button(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, string text, Action onClick,
        int fontSize = 24, bool title = false, string sound = "ui_click")
    {
        var img = Image(parent, name, Skin ? Skin.buttonNormal : null, anchor, pivot, pos, size, null, true);
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.SpriteSwap;
        b.targetGraphic = img;
        b.spriteState = new SpriteState
        {
            highlightedSprite = Skin ? Skin.buttonHover : null,
            pressedSprite = Skin ? Skin.buttonPressed : null,
            selectedSprite = Skin ? Skin.buttonNormal : null,
            disabledSprite = Skin ? Skin.buttonDisabled : null,
        };
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(() =>
        {
            Deselect();   // otherwise Space would "submit" the last clicked button
            AudioManager.Play(sound);
            onClick?.Invoke();
        });
        img.gameObject.AddComponent<HoverSound>();
        var label = Text(img.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 1), size - new Vector2(16, 6), fontSize, TextAnchor.MiddleCenter, Bone, title);
        label.text = text;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.gameObject.AddComponent<DisabledText>().Bind(b);
        return new Btn { button = b, label = label, image = img, rect = (RectTransform)img.transform };
    }

    // Round icon button (speed, pause): the icon sits on a round frame.
    public static Btn RoundButton(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, float size, string text, Action onClick)
    {
        var img = Image(parent, name, Skin ? Skin.roundNormal : null, anchor, pivot, pos, new Vector2(size, size), null, true);
        img.preserveAspect = true;
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.SpriteSwap;
        b.targetGraphic = img;
        b.spriteState = new SpriteState { highlightedSprite = Skin ? Skin.roundHover : null, pressedSprite = Skin ? Skin.roundPressed : null, selectedSprite = Skin ? Skin.roundNormal : null };
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(() =>
        {
            Deselect();
            AudioManager.Play("ui_click");
            onClick?.Invoke();
        });
        img.gameObject.AddComponent<HoverSound>();
        var label = Text(img.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(size, size), Mathf.RoundToInt(size * 0.34f), TextAnchor.MiddleCenter, Bone, true);
        label.text = text;
        return new Btn { button = b, label = label, image = img, rect = (RectTransform)img.transform };
    }

    public class HpBar
    {
        public RectTransform root;
        public Image fill, trail;
        public Text text;
        float shown = 1, target = 1, hold;

        public void Set(float f)
        {
            f = Mathf.Clamp01(f);
            if (f < target) hold = 0.3f;
            target = f;
        }

        public void Tick(float dt)
        {
            if (shown <= target) shown = target;
            else if ((hold -= dt) <= 0) shown = Mathf.MoveTowards(shown, target, dt / 0.5f);
            fill.rectTransform.anchorMax = new Vector2(target, 1);
            trail.rectTransform.anchorMax = new Vector2(shown, 1);
        }
    }

    public static HpBar Bar(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color, int fontSize = 18)
    {
        var frame = Image(parent, name, Skin ? Skin.barFrame : null, anchor, pivot, pos, size);
        var inner = Stretch(frame.transform, "Inner", 6, 6, 6, 6);
        var trail = inner.gameObject.AddComponent<Image>();
        trail.color = new Color(0.95f, 0.85f, 0.6f, 0.85f);
        trail.rectTransform.anchorMin = Vector2.zero;
        trail.rectTransform.anchorMax = Vector2.one;
        var fillRt = Stretch(frame.transform, "Fill", 6, 6, 6, 6);
        var fill = fillRt.gameObject.AddComponent<Image>();
        fill.color = color;
        var text = Text(frame.transform, "Value", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 1), size, fontSize, TextAnchor.MiddleCenter, Bone);
        // trail and fill share the inner rect; their anchorMax.x is the fraction
        trail.rectTransform.offsetMin = fill.rectTransform.offsetMin = new Vector2(6, 6);
        trail.rectTransform.offsetMax = fill.rectTransform.offsetMax = new Vector2(-6, -6);
        return new HpBar { root = (RectTransform)frame.transform, fill = fill, trail = trail, text = text };
    }

    public static Image Icon(Transform parent, string name, Sprite s, Vector2 anchor, Vector2 pivot, Vector2 pos, float size)
    {
        var img = Image(parent, name, s, anchor, pivot, pos, new Vector2(size, size));
        img.preserveAspect = true;
        return img;
    }

    public static Slider Slider(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float value, Action<float> changed)
    {
        var root = Rect(parent, name, anchor, pivot, pos, size);
        var bg = Image(root, "Track", Skin ? Skin.barFrame : null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x, 22), null, true);
        var fillArea = Stretch(root, "FillArea", 8, (size.y - 10) / 2, 8, (size.y - 10) / 2);
        var fill = Fill(fillArea, "Fill", Ember);
        var handleArea = Stretch(root, "HandleArea", 10, 0, 10, 0);
        var handle = Image(handleArea, "Handle", Skin ? Skin.roundNormal : null, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30), null, true);
        var s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = (RectTransform)handle.transform;
        s.targetGraphic = handle;
        s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        s.minValue = 0;
        s.maxValue = 1;
        s.navigation = new Navigation { mode = Navigation.Mode.None };
        s.value = value;
        s.onValueChanged.AddListener(v => changed?.Invoke(v));
        bg.raycastTarget = true;
        return s;
    }

    public static Toggle Toggle(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, string label, bool value, Action<bool> changed)
    {
        var root = Rect(parent, name, anchor, pivot, pos, new Vector2(520, 44));
        var box = Image(root, "Box", Skin ? Skin.buttonNormal : null, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(40, 40), null, true);
        var check = Image(box.transform, "Check", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20), Ember);
        var t = root.gameObject.AddComponent<Toggle>();
        t.targetGraphic = box;
        t.graphic = check;
        t.isOn = value;
        t.navigation = new Navigation { mode = Navigation.Mode.None };
        t.onValueChanged.AddListener(v =>
        {
            AudioManager.Play("ui_click");
            changed?.Invoke(v);
        });
        var txt = Text(root, "Label", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(56, 0), new Vector2(460, 40), 24, TextAnchor.MiddleLeft, Bone);
        txt.text = label;
        return t;
    }

    public static void Deselect()
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    public static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
    }
}

public class HoverSound : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData e)
    {
        var s = GetComponent<Selectable>();
        if (!s || s.interactable) AudioManager.Play("ui_hover", 0.6f);
    }
}

// Greys out a button label when the button is disabled (SetBase changes the enabled colour).
public class DisabledText : MonoBehaviour
{
    Button b;
    Text t;
    Color baseColor;

    public void Bind(Button button)
    {
        b = button;
        t = GetComponent<Text>();
        baseColor = t.color;
    }

    public void SetBase(Color c) => baseColor = c;

    void LateUpdate()
    {
        if (b && t) t.color = b.interactable ? baseColor : UI.Dim;
    }
}

// Cycles sprites on a UI Image (rig preview frames in menus).
public class UiFlipbook : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 8;
    Image img;
    float t;

    void Start() => img = GetComponent<Image>();

    void Update()
    {
        if (frames == null || frames.Length == 0 || !img) return;
        t += Time.unscaledDeltaTime * fps;
        img.sprite = frames[(int)t % frames.Length];
    }
}
