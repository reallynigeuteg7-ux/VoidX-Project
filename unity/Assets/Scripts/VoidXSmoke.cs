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
            Click("ПАРАМЕТРЫ"); yield return new WaitForSeconds(.15f); Check(GameObject.Find("ЧУВСТВИТЕЛЬНОСТЬ slider"), "Settings panel did not open"); yield return Capture("settings"); game.UI.ClosePanel();
            Click("АРСЕНАЛ"); yield return new WaitForSeconds(.15f); Check(GameObject.Find("VX—08"), "Arsenal panel did not open"); game.UI.ClosePanel();
            Click("ИГРАТЬ  →"); yield return new WaitForSeconds(2);
            Check(game.State == VoidXGame.Mode.Playing && game.Wave == 1 && game.Health > 0, "Operation did not start");
            var start = game.Player.transform.position; game.MoveInput = Vector2.up; yield return new WaitForSeconds(1); game.MoveInput = Vector2.zero;
            Check(Vector3.Distance(start, game.Player.transform.position) > 3, "Movement controller did not move");
            game.LookInput = new Vector2(140, -25); yield return null;
            yield return Capture("gameplay");
            int before = game.Ammo[0]; game.Shoot(); yield return new WaitForSeconds(.2f); Check(game.Ammo[0] == before - 1, "Rifle did not consume ammunition");
            game.Reload(); Check(game.ReloadLeft > 0, "Reload did not begin"); yield return new WaitForSeconds(1.9f); Check(game.Ammo[0] == 30 && game.Reserve[0] == 179, "Reload ammo transfer incorrect");
            var head = Array.Find(FindObjectsByType<VoidXGame.Target>(FindObjectsSortMode.None), t => t.head);
            int kills = game.Kills, score = game.Score; Check(head, "No enemy head hitbox");
            for (int i = 0; i < 2 && head && head.gameObject.activeInHierarchy; i++) { Aim(game, head); game.Shoot(); yield return new WaitForSeconds(.2f); }
            Check(game.Kills == kills + 1 && game.Score == score + 150, "Headshots did not eliminate one enemy for 150 points");
            game.SelectWeapon(1); yield return new WaitForSeconds(.3f);
            var body = Array.Find(FindObjectsByType<VoidXGame.Target>(FindObjectsSortMode.None), t => !t.head); kills = game.Kills; score = game.Score; Check(body, "No enemy body hitbox");
            if (body) { Aim(game, body); game.Shoot(); }
            Check(game.Ammo[1] == 7, "Shotgun did not fire"); Check(game.Kills == kills + 1 && game.Score == score + 100, "Shotgun pellets counted a single elimination more than once");
            game.Pause(); float elapsed = game.Elapsed; yield return new WaitForSeconds(.3f); Check(game.Elapsed == elapsed && game.State == VoidXGame.Mode.Paused, "Pause did not freeze simulation"); yield return Capture("pause");
            game.Resume(); yield return new WaitForSeconds(.15f); Check(game.Elapsed > elapsed, "Resume did not advance simulation");
            game.Menu(); yield return null; Check(game.State == VoidXGame.Mode.Menu && FindObjectsByType<VoidXGame.Target>(FindObjectsSortMode.None).Length == 0, "Quit did not clean actors");
            File.WriteAllText(Path.Combine(folder, "result.txt"), errors.Count == 0 ? "PASS: native menu, settings, arsenal, start, movement, rifle, headshots, shotgun single-kill scoring, reload, pause, resume, quit; no runtime errors." : "FAIL\n" + string.Join("\n", errors));
            Application.Quit(errors.Count == 0 ? 0 : 1);
        }
        static void Aim(VoidXGame game, VoidXGame.Target target) { target.transform.parent.position = game.Player.transform.position + game.Player.transform.forward * 4; Physics.SyncTransforms(); game.View.transform.LookAt(target.transform.position); }
        void Click(string label) { foreach (var b in FindObjectsByType<Button>(FindObjectsSortMode.None)) if (b.name == label && b.gameObject.activeInHierarchy) { b.onClick.Invoke(); return; } errors.Add("Missing button: " + label); }
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png")); yield return new WaitForSeconds(.2f); }
    }
}
#endif
