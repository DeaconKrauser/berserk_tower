using UnityEngine;
using UnityEngine.SceneManagement;

// Boots the game in the single scene: data, persistent services (save, audio, camera, UI) and the screen flow.
// Only meta progression and settings persist; a run lives in RunManager memory and dies with the process.
public class GameManager : MonoBehaviour
{
    public const string GameScene = "SampleScene";
    public static GameManager I;
    public static bool SuppressAutoBoot;     // tests boot manually with their own save file

    public GameConfig config;
    public SaveSystem save;
    public RunManager run;
    public SceneFlow flow;
    public Canvas canvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        if (SuppressAutoBoot || I || s.name != GameScene) return;
        // a separate meta file for automated runs keeps the player's real save untouched
        Boot(Automation.RequestedBy(Automation.CommandLine) ? Automation.Arg("-savepath") ?? System.IO.Path.Combine(Application.persistentDataPath, "smoke_meta.json") : null);
    }

    public static GameManager Boot(string savePath = null)
    {
        if (I) return I;
        var go = new GameObject("GameManager");
        var gm = go.AddComponent<GameManager>();
        I = gm;
        Time.timeScale = 1;
        gm.config = GameConfig.Load();
        if (!gm.config)
        {
            Debug.LogError("GameConfig não encontrado em Resources. Rode Bastião/Recriar conteúdo.");
            return gm;
        }
        gm.save = new SaveSystem(savePath);
        gm.run = new RunManager(gm.config, gm.save);
        UI.Skin = gm.config.ui;
        UiFonts.Init(gm.config.ui);
        CameraRig.Setup();
        AudioManager.Create(gm.config.audio, go.transform);
        gm.canvas = SceneFlow.CreateCanvas(go.transform);
        gm.flow = go.AddComponent<SceneFlow>();
        gm.flow.Init(gm);
        gm.ApplySettings(firstBoot: true);
        gm.flow.ShowMainMenu(instant: true);
        return gm;
    }

    public void ApplySettings(bool firstBoot = false)
    {
        var s = save.Data.settings;
        CameraRig.ShakeEnabled = s.screenShake;
        SettingsCache.DamageNumbers = s.showDamageNumbers;
        if (AudioManager.I) AudioManager.I.ApplySettings(s);
        if (MusicManager.I) MusicManager.I.Refresh();
        // Window mode and resolution follow the player's options, except in automated/test runs that set their own window.
        if (!Application.isEditor && !Automation.Enabled)
        {
            int w = s.resolutionWidth > 0 ? s.resolutionWidth : Display.main.systemWidth;
            int h = s.resolutionHeight > 0 ? s.resolutionHeight : Display.main.systemHeight;
            var mode = s.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (!firstBoot || Screen.width != w || Screen.height != h || Screen.fullScreenMode != mode)
                Screen.SetResolution(w, h, mode);
        }
    }

    void OnDestroy()
    {
        if (I == this) I = null;
        Time.timeScale = 1;
    }
}
