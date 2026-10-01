using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Screen state machine: menu ↔ commander ↔ skins ↔ options, map selection → battle → results → next destination.
// Every change goes through a short fade. A battle is one GameObject: destroying it clears the whole map.
public class SceneFlow : MonoBehaviour
{
    public enum Screen { None, MainMenu, Commander, Skins, Options, MapSelect, Battle, Results }

    public Screen Current { get; private set; }
    public Battle battle;
    public GameObject screenRoot;
    public HUDController hud;
    public bool Transitioning { get; private set; }
    public event Action<Screen> ScreenChanged;

    GameManager gm;
    CanvasGroup fade;
    Transform canvasT;

    public static Canvas CreateCanvas(Transform parent)
    {
        if (!FindAnyObjectByType<EventSystem>())
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(parent, false);
        }
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 10;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 32;
        return c;
    }

    public void Init(GameManager g)
    {
        gm = g;
        canvasT = g.canvas.transform;
        var f = UI.Fill(canvasT, "Fade", Color.black, true);
        fade = f.gameObject.AddComponent<CanvasGroup>();
        fade.alpha = 0;
        fade.blocksRaycasts = false;
    }

    // ---------------------------------------------------------------- transitions

    void Go(Screen s, Func<GameObject> build, bool instant = false)
    {
        if (instant)
        {
            Swap(s, build);
            return;
        }
        StartCoroutine(FadeSwap(s, build));
    }

    // Requests made during a fade wait for it (a click is never lost).
    IEnumerator FadeSwap(Screen s, Func<GameObject> build)
    {
        while (Transitioning) yield return null;
        Transitioning = true;
        fade.blocksRaycasts = true;
        for (float t = 0; t < 0.22f; t += Time.unscaledDeltaTime)
        {
            fade.alpha = t / 0.22f;
            yield return null;
        }
        fade.alpha = 1;
        Swap(s, build);
        yield return null;
        for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            fade.alpha = 1 - t / 0.3f;
            yield return null;
        }
        fade.alpha = 0;
        fade.blocksRaycasts = false;
        Transitioning = false;
    }

    void Swap(Screen s, Func<GameObject> build)
    {
        if (screenRoot) Destroy(screenRoot);
        screenRoot = build();
        if (screenRoot) screenRoot.transform.SetSiblingIndex(fade.transform.GetSiblingIndex());
        Current = s;
        ScreenChanged?.Invoke(s);
    }

    RectTransform NewScreen(string name)
    {
        var rt = UI.Stretch(canvasT, name);
        return rt;
    }

    void DestroyBattle()
    {
        if (battle)
        {
            Destroy(battle.gameObject);
            battle = null;
        }
        hud = null;
        CameraRig.ResetView();
        Time.timeScale = 1;
    }

    // ---------------------------------------------------------------- screens

    public void ShowMainMenu(bool instant = false) => Go(Screen.MainMenu, () =>
    {
        DestroyBattle();
        MusicManager.Play("music_menu", 1.5f);
        AudioManager.Ambient(null);
        var rt = NewScreen("MainMenu");
        rt.gameObject.AddComponent<MainMenuController>().Build(gm);
        return rt.gameObject;
    }, instant);

    public void ShowCommander() => Go(Screen.Commander, () =>
    {
        var rt = NewScreen("Commander");
        rt.gameObject.AddComponent<CommanderMenuController>().Build(gm);
        return rt.gameObject;
    });

    public void ShowSkins() => Go(Screen.Skins, () =>
    {
        var rt = NewScreen("Skins");
        rt.gameObject.AddComponent<SkinMenuController>().Build(gm);
        return rt.gameObject;
    });

    public void ShowOptions() => Go(Screen.Options, () =>
    {
        var rt = NewScreen("Options");
        rt.gameObject.AddComponent<OptionsMenuController>().Build(gm);
        return rt.gameObject;
    });

    // New run (from the menu) or the next destination of the current run (after a won map).
    public void ShowMapSelect(bool newRun) => Go(Screen.MapSelect, () =>
    {
        DestroyBattle();
        if (newRun) gm.run.StartRun();
        MusicManager.Play("music_menu", 1.5f);
        AudioManager.Ambient(null);
        var rt = NewScreen("MapSelect");
        rt.gameObject.AddComponent<MapSelectionController>().Build(gm, newRun);
        return rt.gameObject;
    });

    public void StartMap(MapData map, RouteData route) => Go(Screen.Battle, () =>
    {
        DestroyBattle();
        gm.run.EnterMap(map);
        battle = Battle.Create(gm.config, map, route, gm.run, gm.save);
        battle.Finished += OnBattleFinished;
        MusicManager.PlayMap(map.musicId);
        AudioManager.Ambient(map.ambientId);
        var rt = NewScreen("HUD");
        hud = rt.gameObject.AddComponent<HUDController>();
        hud.Build(gm, battle);
        return rt.gameObject;
    });

    void OnBattleFinished(MapResult r)
    {
        gm.run.FinishMap(r);
        ShowResults(r);
    }

    public void ShowResults(MapResult r) => Go(Screen.Results, () =>
    {
        // the battle stays behind the results (frozen) until the player leaves this screen
        if (battle) battle.SetPaused(true);
        var rt = NewScreen("Results");
        rt.gameObject.AddComponent<ResultScreenController>().Build(gm, r);
        return rt.gameObject;
    });

    public void AbandonRun()
    {
        if (battle && !battle.over)
        {
            battle.SetPaused(false);
            battle.Defeat("A run foi abandonada.");
            return;
        }
        gm.run.EndRun();
        ShowMainMenu();
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
