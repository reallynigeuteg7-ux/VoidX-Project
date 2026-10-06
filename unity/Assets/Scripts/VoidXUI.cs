using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace VoidX
{
    // Native Unity canvas; no browser or HTML. Anchors scale independently with aspect ratio.
    public sealed class VoidXUI : MonoBehaviour
    {
        VoidXGame game;
        RectTransform safe, stick;
        GameObject menu, hud, pause, result, panel;
        Text record, health, ammo, wave, notice, summary, resultTitle;
        Image hurt, marker;
        Font font;
        Sprite circle;
        PointerArea joystick, look, fire;
        readonly Color ink = new(.025f, .025f, .025f, .9f), soft = new(.76f, .76f, .76f), border = new(.65f, .65f, .65f, .4f);
        Rect lastSafe;
        public void Init(VoidXGame owner)
        {
            game = owner; font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false); var pixels = new Color[4096];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32)); pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(32 - d)); }
            texture.SetPixels(pixels); texture.Apply(); circle = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f));
            var canvasObj = new GameObject("VoidX Mobile HUD"); var canvas = canvasObj.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = canvasObj.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f; canvasObj.AddComponent<GraphicRaycaster>();
            var eventSystem = new GameObject("Input events"); eventSystem.AddComponent<EventSystem>(); eventSystem.AddComponent<StandaloneInputModule>();
            safe = Rect(canvasObj.transform, "Safe area", Vector2.zero, Vector2.one);
            menu = Rect(safe, "Main menu", Vector2.zero, Vector2.one).gameObject;
            Image(menu.transform, "Top shade", new Color(0, 0, 0, .65f), new Vector2(0, .83f), Vector2.one);
            Label(menu.transform, "VOIDX", new Vector2(.04f, .855f), new Vector2(.29f, .97f), 45, TextAnchor.MiddleLeft, Color.white, true);
            Label(menu.transform, "МЁРТВЫЙ СЕКТОР  /  ОФЛАЙН", new Vector2(.04f, .81f), new Vector2(.44f, .865f), 16, TextAnchor.MiddleLeft, soft);
            record = Label(menu.transform, "РЕКОРД  00000", new Vector2(.53f, .86f), new Vector2(.79f, .96f), 20, TextAnchor.MiddleRight, soft);
            Button(menu.transform, "ПАРАМЕТРЫ", new Vector2(.81f, .865f), new Vector2(.96f, .96f), Settings, false, 17);
            var logo = Resources.Load<Texture2D>("VX-logo");
            if (logo) { var image = Rect(menu.transform, "VX emblem", new Vector2(.32f, .36f), new Vector2(.68f, .77f)).gameObject.AddComponent<RawImage>(); image.texture = logo; image.material = Resources.Load<Material>("VoidXEmblem"); image.uvRect = new Rect(.06f, .2f, .88f, .63f); image.raycastTarget = false; image.color = new Color(1, 1, 1, .9f); }
            Image(menu.transform, "Lower shade", new Color(0, 0, 0, .78f), Vector2.zero, new Vector2(1, .29f));
            Label(menu.transform, "ОПЕРАЦИЯ 07", new Vector2(.04f, .17f), new Vector2(.41f, .265f), 29, TextAnchor.MiddleLeft, Color.white, true);
            Label(menu.transform, "Пять волн. Один сектор. Выжить.", new Vector2(.04f, .08f), new Vector2(.43f, .18f), 19, TextAnchor.MiddleLeft, soft);
            Button(menu.transform, "АРСЕНАЛ", new Vector2(.46f, .075f), new Vector2(.65f, .22f), Arsenal, false, 21);
            Button(menu.transform, "ИГРАТЬ  →", new Vector2(.68f, .075f), new Vector2(.96f, .22f), game.StartGame, true, 32);
            hud = Rect(safe, "Combat HUD", Vector2.zero, Vector2.one).gameObject;
            var lookRect = Rect(hud.transform, "Look area", new Vector2(.38f, 0), Vector2.one); var lookImage = lookRect.gameObject.AddComponent<Image>(); lookImage.color = Color.clear; look = lookRect.gameObject.AddComponent<PointerArea>(); look.Drag = delta => game.LookInput += delta;
            Image(hud.transform, "Status strip", new Color(0, 0, 0, .5f), new Vector2(.015f, .88f), new Vector2(.37f, .98f));
            health = Label(hud.transform, "100  HP", new Vector2(.03f, .89f), new Vector2(.15f, .97f), 26, TextAnchor.MiddleLeft, Color.white, true);
            wave = Label(hud.transform, "ВОЛНА 01 / 05", new Vector2(.17f, .895f), new Vector2(.36f, .975f), 18, TextAnchor.MiddleLeft, soft);
            Button(hud.transform, "Ⅱ", new Vector2(.91f, .88f), new Vector2(.98f, .98f), game.Pause, false, 28);
            notice = Label(hud.transform, "", new Vector2(.25f, .66f), new Vector2(.75f, .78f), 30, TextAnchor.MiddleCenter, Color.white, true);
            Image(hud.transform, "Reticle horizontal", new Color(1, 1, 1, .9f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(18, 2));
            Image(hud.transform, "Reticle vertical", new Color(1, 1, 1, .9f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(2, 18));
            marker = Image(hud.transform, "Hit marker", Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(26, 26)); marker.sprite = circle; marker.gameObject.AddComponent<Outline>().effectColor = Color.black; marker.raycastTarget = false;
            var joyRect = Rect(hud.transform, "Movement joystick", new Vector2(.115f, .22f), new Vector2(.115f, .22f), new Vector2(156, 156));
            var joyImage = joyRect.gameObject.AddComponent<Image>(); joyImage.sprite = circle; joyImage.color = new Color(1, 1, 1, .09f); joyRect.gameObject.AddComponent<Outline>().effectColor = border;
            stick = Rect(joyRect, "Thumb", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(58, 58)); var thumb = stick.gameObject.AddComponent<Image>(); thumb.sprite = circle; thumb.color = new Color(1, 1, 1, .27f); thumb.raycastTarget = false;
            joystick = joyRect.gameObject.AddComponent<PointerArea>();
            joystick.Position = pos => { RectTransformUtility.ScreenPointToLocalPointInRectangle(joyRect, pos, null, out var local); var offset = Vector2.ClampMagnitude(local, 58); stick.anchoredPosition = offset; game.MoveInput = offset / 58; };
            joystick.Up = () => { stick.anchoredPosition = Vector2.zero; game.MoveInput = Vector2.zero; };
            var fireRect = Rect(hud.transform, "Fire", new Vector2(.875f, .26f), new Vector2(.875f, .26f), new Vector2(132, 132)); var fireImage = fireRect.gameObject.AddComponent<Image>(); fireImage.sprite = circle; fireImage.color = new Color(1, 1, 1, .18f); fireRect.gameObject.AddComponent<Outline>().effectColor = soft;
            Label(fireRect, "ОГОНЬ", Vector2.zero, Vector2.one, 20, TextAnchor.MiddleCenter, Color.white, true);
            fire = fireRect.gameObject.AddComponent<PointerArea>(); fire.Down = () => game.FireInput = true; fire.Up = () => game.FireInput = false; fire.Drag = delta => game.LookInput += delta;
            ammo = Label(hud.transform, "30 / 180", new Vector2(.39f, .035f), new Vector2(.62f, .115f), 27, TextAnchor.MiddleCenter, Color.white, true);
            Button(hud.transform, "ПЕРЕЗАРЯДКА", new Vector2(.66f, .05f), new Vector2(.805f, .15f), game.Reload, false, 15);
            Button(hud.transform, "ОРУЖИЕ", new Vector2(.83f, .05f), new Vector2(.975f, .15f), () => game.SelectWeapon(1 - game.WeaponIndex), false, 17);
            hurt = Image(hud.transform, "Damage flash", Color.clear, Vector2.zero, Vector2.one); hurt.raycastTarget = false;
            pause = Overlay("Pause"); Label(pause.transform, "ПАУЗА", new Vector2(.2f, .7f), new Vector2(.8f, .86f), 45, TextAnchor.MiddleCenter, Color.white, true);
            Button(pause.transform, "ПРОДОЛЖИТЬ", new Vector2(.33f, .47f), new Vector2(.67f, .62f), game.Resume, true, 26);
            Button(pause.transform, "ПАРАМЕТРЫ", new Vector2(.33f, .29f), new Vector2(.67f, .43f), Settings, false, 22);
            Button(pause.transform, "В ГЛАВНОЕ МЕНЮ", new Vector2(.33f, .12f), new Vector2(.67f, .25f), game.Menu, false, 21);
            result = Overlay("Result"); resultTitle = Label(result.transform, "", new Vector2(.1f, .68f), new Vector2(.9f, .87f), 43, TextAnchor.MiddleCenter, Color.white, true);
            summary = Label(result.transform, "", new Vector2(.15f, .4f), new Vector2(.85f, .64f), 26, TextAnchor.MiddleCenter, soft);
            Button(result.transform, "СНОВА В БОЙ", new Vector2(.19f, .14f), new Vector2(.48f, .3f), game.StartGame, true, 24);
            Button(result.transform, "ГЛАВНОЕ МЕНЮ", new Vector2(.52f, .14f), new Vector2(.81f, .3f), game.Menu, false, 21);
            Refresh(); UpdateSafeArea();
        }
        RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 size = default)
        {
            var o = new GameObject(name, typeof(RectTransform)); var r = o.GetComponent<RectTransform>(); r.SetParent(parent, false); r.anchorMin = min; r.anchorMax = max; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; if (min == max) r.sizeDelta = size; return r;
        }
        Image Image(Transform parent, string name, Color color, Vector2 min, Vector2 max, Vector2 size = default)
        {
            var image = Rect(parent, name, min, max, size).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        Text Label(Transform parent, string text, Vector2 min, Vector2 max, int size, TextAnchor align, Color color, bool bold = false)
        {
            var label = Rect(parent, text, min, max).gameObject.AddComponent<Text>(); label.text = text; label.font = font; label.fontSize = size; label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; label.alignment = align; label.color = color; label.raycastTarget = false; label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = size; return label;
        }
        Button Button(Transform parent, string text, Vector2 min, Vector2 max, Action action, bool primary = false, int size = 22)
        {
            var image = Image(parent, text, primary ? Color.white : ink, min, max); image.raycastTarget = true;
            var outline = image.gameObject.AddComponent<Outline>(); outline.effectColor = primary ? Color.white : border; outline.effectDistance = new Vector2(1, -1);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; var colors = button.colors; colors.highlightedColor = new Color(.78f, .78f, .78f); colors.pressedColor = new Color(.5f, .5f, .5f); button.colors = colors; button.onClick.AddListener(() => action());
            Label(image.transform, text, new Vector2(.05f, .05f), new Vector2(.95f, .95f), size, TextAnchor.MiddleCenter, primary ? Color.black : Color.white, primary); return button;
        }
        GameObject Overlay(string name)
        {
            var image = Image(safe, name, new Color(0, 0, 0, .88f), Vector2.zero, Vector2.one); image.raycastTarget = true; return image.gameObject;
        }
        void BeginPanel(string title)
        {
            ClosePanel(); panel = Overlay(title); Label(panel.transform, title, new Vector2(.06f, .8f), new Vector2(.8f, .95f), 36, TextAnchor.MiddleLeft, Color.white, true);
            Button(panel.transform, "ЗАКРЫТЬ", new Vector2(.81f, .82f), new Vector2(.95f, .94f), ClosePanel, false, 18);
        }
        public void ClosePanel() { if (panel) { Destroy(panel); panel = null; game.SaveSettings(); } }
        void Settings()
        {
            BeginPanel("ПАРАМЕТРЫ"); Slider("ЧУВСТВИТЕЛЬНОСТЬ", .61f, game.Sensitivity / 100, v => game.Sensitivity = v * 100); Slider("ГРОМКОСТЬ", .41f, game.Volume, v => game.Volume = v);
            Label(panel.transform, "ГРАФИКА", new Vector2(.07f, .16f), new Vector2(.3f, .27f), 21, TextAnchor.MiddleLeft, soft);
            string[] names = { "НИЗКАЯ", "СРЕДНЯЯ", "ВЫСОКАЯ" };
            for (int i = 0; i < 3; i++) { int n = i; Button(panel.transform, names[i] + (game.Quality == i ? " ✓" : ""), new Vector2(.34f + i * .2f, .16f), new Vector2(.52f + i * .2f, .27f), () => { game.Quality = n; game.SaveSettings(); Settings(); }, game.Quality == i, 18); }
        }
        void Slider(string title, float y, float value, Action<float> changed)
        {
            Label(panel.transform, title, new Vector2(.07f, y), new Vector2(.36f, y + .12f), 21, TextAnchor.MiddleLeft, soft);
            var root = Rect(panel.transform, title + " slider", new Vector2(.4f, y + .035f), new Vector2(.91f, y + .085f));
            Image(root, "Track", new Color(.4f, .4f, .4f), new Vector2(0, .38f), new Vector2(1, .62f));
            var area = Rect(root, "Fill area", Vector2.zero, Vector2.one); var fill = Image(area, "Fill", Color.white, Vector2.zero, Vector2.one);
            var handleArea = Rect(root, "Handle area", Vector2.zero, Vector2.one); var handle = Image(handleArea, "Handle", Color.white, Vector2.zero, Vector2.zero, new Vector2(28, 45)); handle.raycastTarget = true;
            var slider = root.gameObject.AddComponent<Slider>(); slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle; slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight; slider.value = value; slider.onValueChanged.AddListener(v => changed(v));
        }
        void Arsenal()
        {
            BeginPanel("АРСЕНАЛ"); int first = PlayerPrefs.GetInt("voidx.firstWeapon", 0);
            Label(panel.transform, "VX—01", new Vector2(.08f, .5f), new Vector2(.43f, .72f), 44, TextAnchor.MiddleLeft, Color.white, true);
            Label(panel.transform, "Штурмовая винтовка\n30 патронов · автоматический огонь", new Vector2(.08f, .29f), new Vector2(.45f, .49f), 23, TextAnchor.UpperLeft, soft);
            Label(panel.transform, "VX—08", new Vector2(.55f, .5f), new Vector2(.9f, .72f), 44, TextAnchor.MiddleLeft, Color.white, true);
            Label(panel.transform, "Дробовик\n8 патронов · ближняя дистанция", new Vector2(.55f, .29f), new Vector2(.93f, .49f), 23, TextAnchor.UpperLeft, soft);
            for (int i = 0; i < 2; i++) { int n = i; Button(panel.transform, first == i ? "ВЫБРАНО ✓" : "ВЫБРАТЬ", new Vector2(.08f + i * .47f, .12f), new Vector2(.43f + i * .47f, .25f), () => { PlayerPrefs.SetInt("voidx.firstWeapon", n); PlayerPrefs.Save(); Arsenal(); }, first == i, 22); }
        }
        public void Refresh()
        {
            if (!menu) return; ClosePanel(); menu.SetActive(game.State == VoidXGame.Mode.Menu); hud.SetActive(game.State == VoidXGame.Mode.Playing); pause.SetActive(game.State == VoidXGame.Mode.Paused); result.SetActive(game.State == VoidXGame.Mode.Result);
            record.text = "РЕКОРД  " + game.Best.ToString("00000"); resultTitle.text = game.ResultTitle; summary.text = "СЧЁТ  " + game.Score.ToString("00000") + "\nУСТРАНЕНИЙ  " + game.Kills + "    ВОЛНА  " + game.Wave + " / 5\nВРЕМЯ  " + TimeSpan.FromSeconds(game.Elapsed).ToString(@"mm\:ss");
        }
        public void ClearInput() { joystick?.ResetPointer(); look?.ResetPointer(); fire?.ResetPointer(); }
        void LateUpdate()
        {
            if (!game) return; UpdateSafeArea();
            health.text = game.Health + "  HP"; wave.text = "ВОЛНА 0" + game.Wave + " / 05";
            ammo.text = game.ReloadLeft > 0 ? "ПЕРЕЗАРЯДКА…" : game.Ammo[game.WeaponIndex].ToString("00") + "  /  " + game.Reserve[game.WeaponIndex];
            notice.text = game.NoticeLeft > 0 ? game.Notice : ""; marker.color = new Color(1, 1, 1, game.HitMarker > 0 ? .85f : 0); hurt.color = new Color(1, 1, 1, game.DamageFlash * .2f);
        }
        void UpdateSafeArea()
        {
            Rect area = Screen.safeArea; if (area == lastSafe || Screen.width <= 0 || Screen.height <= 0) return; lastSafe = area; safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height); safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        }
    }
    public sealed class PointerArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler
    {
        public Action Down, Up; public Action<Vector2> Drag, Position; int pointer = int.MinValue;
        public void OnInitializePotentialDrag(PointerEventData data) { data.useDragThreshold = false; }
        public void OnPointerDown(PointerEventData data) { if (pointer != int.MinValue) return; pointer = data.pointerId; Down?.Invoke(); Position?.Invoke(data.position); }
        public void OnDrag(PointerEventData data) { if (pointer != data.pointerId) return; Drag?.Invoke(data.delta); Position?.Invoke(data.position); }
        public void OnPointerUp(PointerEventData data) { if (pointer != data.pointerId) return; ResetPointer(); }
        public void ResetPointer() { pointer = int.MinValue; Up?.Invoke(); }
        void OnDisable() { ResetPointer(); }
    }
}
