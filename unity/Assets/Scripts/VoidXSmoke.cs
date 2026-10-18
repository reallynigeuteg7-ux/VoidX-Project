#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace VoidX
{
    // Exercises the actual native player. Only compiled into development/editor builds.
    public sealed class VoidXSmoke : MonoBehaviour
    {
        readonly List<string> errors = new();
        string folder;
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) errors.Add(message); }
        void Check(bool value, string reason) { if (!value) errors.Add(reason); }
        IEnumerator Start()
        {
            folder = Environment.GetEnvironmentVariable("VOIDX_SMOKE_OUTPUT") ?? Path.Combine(Application.persistentDataPath, "smoke"); Directory.CreateDirectory(folder);
            var game = GetComponent<VoidXGame>(); yield return new WaitForSeconds(2);
            Check(game.State == VoidXGame.Mode.Menu, "Main menu not active");
            yield return Capture("menu");
            Check(game.Auth && !game.Auth.IsSignedIn, "Guest profile was not initialized");
            Click("Войти через Google"); yield return null;
            Check(GameObject.Find("АККАУНТ"), "Account panel did not open");
            game.Auth.SignIn(); yield return null;
            Check(!game.Auth.IsSignedIn && !game.Auth.IsBusy, "Unavailable Google sign-in blocked guest play or fabricated a login");
            game.UI.ClosePanel();
            Click("ПАРАМЕТРЫ"); yield return new WaitForSeconds(.15f); Check(GameObject.Find("ЧУВСТВИТЕЛЬНОСТЬ slider"), "Settings panel did not open"); yield return Capture("settings"); game.UI.ClosePanel();
            Click("АРСЕНАЛ"); yield return new WaitForSeconds(.15f); Check(GameObject.Find("VX—08"), "Arsenal panel did not open"); game.UI.ClosePanel();
            Click("ИГРАТЬ  →"); yield return new WaitForSeconds(2);
            Check(game.State == VoidXGame.Mode.Playing && game.Wave == 1 && game.Health > 0, "Operation did not start");
            var start = game.Player.transform.position; game.MoveInput = Vector2.up; yield return new WaitForSeconds(1); game.MoveInput = Vector2.zero;
            Check(Vector3.Distance(start, game.Player.transform.position) > 3, "Movement controller did not move");
            game.LookInput = new Vector2(140, -25); yield return null;
            yield return Capture("gameplay");
            var canvas = FindFirstObjectByType<Canvas>(); canvas.enabled = false; yield return Capture("hands"); canvas.enabled = true;
            int before = game.Ammo[0]; game.Shoot(); yield return new WaitForSeconds(.2f); Check(game.Ammo[0] == before - 1, "Rifle did not consume ammunition");
            game.Reload(); Check(game.ReloadLeft > 0, "Reload did not begin"); yield return new WaitForSeconds(.6f); yield return Capture("reload"); yield return new WaitForSeconds(1.2f); Check(game.Ammo[0] == 30 && game.Reserve[0] == 179, "Reload ammo transfer incorrect");
            var head = Array.Find(FindObjectsByType<VoidXGame.Target>(FindObjectsSortMode.None), t => t.head);
            int kills = game.Kills, score = game.Score; Check(head, "No enemy head hitbox");
            for (int i = 0; i < 2 && head && head.gameObject.activeInHierarchy; i++) { Aim(game, head); game.Shoot(); yield return new WaitForSeconds(.2f); }
            Check(game.Kills == kills + 1 && game.Score == score + 150, "Headshots did not eliminate one enemy for 150 points");
            game.SelectWeapon(1); yield return new WaitForSeconds(.5f);
            Check(GameObject.Find("VX—08 Shotgun") && !GameObject.Find("VX—01 Assault Rifle"), "Weapon visibility did not switch to shotgun");
            yield return Capture("shotgun");
            var body = Array.Find(FindObjectsByType<VoidXGame.Target>(FindObjectsSortMode.None), t => !t.head); kills = game.Kills; score = game.Score; Check(body, "No enemy body hitbox");
            if (body) { Aim(game, body); game.Shoot(); }
            Check(game.Ammo[1] == 7, "Shotgun did not fire"); Check(game.Kills == kills + 1 && game.Score == score + 100, "Shotgun pellets counted a single elimination more than once");
            game.Pause(); float elapsed = game.Elapsed; yield return new WaitForSeconds(.3f); Check(game.Elapsed == elapsed && game.State == VoidXGame.Mode.Paused, "Pause did not freeze simulation"); yield return Capture("pause");
            game.Resume(); yield return new WaitForSeconds(.15f); Check(game.Elapsed > elapsed, "Resume did not advance simulation");
            game.Menu(); yield return null; Check(game.State == VoidXGame.Mode.Menu && FindObjectsByType<VoidXGame.Target>(FindObjectsSortMode.None).Length == 0, "Quit did not clean actors");
            yield return CompleteWaves(game);
            File.WriteAllText(Path.Combine(folder, "result.txt"), errors.Count == 0 ? "PASS: native menu, settings, arsenal, start, movement, rifle, headshots, shotgun single-kill scoring, reload, pause, resume, quit, unobstructed spawns, all five waves and victory; no runtime errors." : "FAIL\n" + string.Join("\n", errors));
            Application.Quit(errors.Count == 0 ? 0 : 1);
        }
        IEnumerator CompleteWaves(VoidXGame game)
        {
            foreach (var point in VoidXGame.SpawnPoints) Check(!VoidXGame.Blocked(point, .48f, VoidXWorld.Covers), "Configured spawn is inside cover: " + point);
            game.StartGame(); int[] killsByWave = new int[6]; int previousWave = 1;
            float deadline = Time.realtimeSinceStartup + 120;
            while (game.State == VoidXGame.Mode.Playing && Time.realtimeSinceStartup < deadline)
            {
                if (game.Wave != previousWave)
                {
                    Check(game.Wave == previousWave + 1, "Wave skipped or restarted");
                    Check(killsByWave[previousWave] == 2 + previousWave * 2, "Wave advanced before every scheduled enemy was eliminated");
                    Check(game.Reserve[0] == 180 + (game.Wave - 1) * 60 && game.Reserve[1] == 48 + (game.Wave - 1) * 12, "Intermission supplies granted more than once or missing");
                    previousWave = game.Wave;
                }
                foreach (var target in FindObjectsByType<VoidXGame.Target>(FindObjectsSortMode.None))
                {
                    if (target.head || !target.gameObject.activeInHierarchy) continue;
                    Check(!VoidXGame.Blocked(target.transform.parent.position, .42f, VoidXWorld.Covers), "Live enemy is trapped in cover");
                    int beforeKills = game.Kills; target.Hit(int.MaxValue, false);
                    killsByWave[game.Wave] += game.Kills - beforeKills;
                }
                yield return null;
            }
            Check(game.State == VoidXGame.Mode.Result && game.Wave == 5 && game.ResultTitle == "СЕКТОР ЧИСТ.", "Five-wave run did not reach victory before timeout");
            Check(game.Kills == 40 && game.Score == 5000, "Full run enemy count or victory score incorrect");
            for (int wave = 1; wave <= 5; wave++) Check(killsByWave[wave] == 2 + wave * 2, "Wrong enemy count in wave " + wave);
            yield return Capture("victory"); game.Menu(); yield return null;
        }
        static void Aim(VoidXGame game, VoidXGame.Target target) { target.transform.parent.position = game.Player.transform.position + game.Player.transform.forward * 4; Physics.SyncTransforms(); game.View.transform.LookAt(target.transform.position); }
        void Click(string label) { foreach (var b in FindObjectsByType<Button>(FindObjectsSortMode.None)) if (b.name == label && b.gameObject.activeInHierarchy) { b.onClick.Invoke(); return; } errors.Add("Missing button: " + label); }
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png")); yield return new WaitForSeconds(.2f); }
    }
}
#endif
