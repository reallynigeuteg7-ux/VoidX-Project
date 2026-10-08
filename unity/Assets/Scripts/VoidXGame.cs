using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidX
{
    public sealed class VoidXGame : MonoBehaviour
    {
        public enum Mode { Menu, Playing, Paused, Result }
        public Mode State { get; private set; } = Mode.Menu;
        public int Health { get; private set; } = 100;
        public int Wave { get; private set; }
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public int Best => PlayerPrefs.GetInt("voidx.best", 0);
        public int WeaponIndex { get; private set; }
        public readonly int[] Ammo = { 30, 8 }, Reserve = { 180, 48 };
        public static readonly int[] Magazine = { 30, 8 };
        public float Sensitivity = 50, Volume = .65f;
        public int Quality = 1;
        public float Elapsed, ReloadLeft;
        public string Notice = "", ResultTitle = "";
        public float NoticeLeft, HitMarker, DamageFlash;
        public Vector2 MoveInput, LookInput;
        public bool FireInput;
        public bool Touch => Application.isMobilePlatform || Input.touchSupported;
        public bool AutomatedCheck { get; private set; }
        public Camera View { get; private set; }
        public CharacterController Player { get; private set; }
        public VoidXUI UI { get; private set; }
        Transform[] guns;
        VoidXWeaponPose[] weaponPoses;
        Light muzzleLight;
        GameObject muzzleFlash;
        AudioSource audioSource;
        AudioClip rifleSound, shotgunSound, hitSound, reloadSound;
        float yaw, pitch, cooldown, recoil, flashLeft, stepPhase, spawnTime, between, navTime, verticalSpeed, pumpLeft;
        int toSpawn, shotCount, hitCount;
        FlowMap navigation;
        readonly List<Operative> enemies = new();
        readonly List<Bullet> bullets = new();
        readonly List<Pickup> pickups = new();
        readonly List<Effect> effects = new();
        readonly Vector3[] spawns = { new(-16, 0, -18), new(0, 0, -20), new(17, 0, -17), new(-21, 0, -7), new(21, 0, -6), new(-20, 0, 11), new(20, 0, 12), new(0, 0, 21) };
        sealed class Operative { public Transform root; public Transform[] legs; public int hp; public float phase, shoot; }
        sealed class Bullet { public Transform root; public Vector3 velocity; public float life; }
        sealed class Pickup { public Transform root; }
        sealed class Effect { public Transform root; public Vector3 velocity; public float life; }
        public sealed class Target : MonoBehaviour { public Action<int, bool> Hit; public bool head; }

        void Awake()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            AutomatedCheck = Array.IndexOf(Environment.GetCommandLineArgs(), "--voidx-smoke") >= 0;
#endif
            Application.targetFrameRate = 60; Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (AutomatedCheck) Application.runInBackground = true;
            Screen.orientation = ScreenOrientation.LandscapeLeft; Input.multiTouchEnabled = true;
            Sensitivity = PlayerPrefs.GetFloat("voidx.sensitivity", 50); Volume = PlayerPrefs.GetFloat("voidx.volume", .65f); Quality = PlayerPrefs.GetInt("voidx.quality", 1);
            VoidXWorld.Build();
            var player = new GameObject("Player"); player.layer = 2; Player = player.AddComponent<CharacterController>(); Player.height = 1.75f; Player.radius = .34f; Player.center = new Vector3(0, .9f, 0); Player.stepOffset = .28f; Player.skinWidth = .025f; Player.enabled = false;
            View = new GameObject("FPS camera").AddComponent<Camera>(); View.tag = "MainCamera"; View.transform.SetParent(player.transform, false); View.transform.localPosition = new Vector3(0, 1.65f, 0); View.fieldOfView = 74; View.nearClipPlane = .035f; View.farClipPlane = 130; View.allowHDR = true;
            View.allowMSAA = !Application.isMobilePlatform;
            var cameraData = View.GetUniversalAdditionalCameraData(); cameraData.renderPostProcessing = false;
            View.gameObject.AddComponent<AudioListener>();
            var volume = new GameObject("Monochrome film grade").AddComponent<UnityEngine.Rendering.Volume>(); volume.isGlobal = true; volume.sharedProfile = Resources.Load<VolumeProfile>("VoidXGrade");
            // A small, separately lit first-person stage renders over the arena; nearby walls cannot cut through hands.
            const int viewLayer = 31; View.cullingMask &= ~(1 << viewLayer);
            var weaponCamera = new GameObject("First person presentation camera").AddComponent<Camera>(); weaponCamera.transform.position = new Vector3(0,-200,0); weaponCamera.cullingMask = 1 << viewLayer;
            weaponCamera.fieldOfView = View.fieldOfView; weaponCamera.nearClipPlane = .025f; weaponCamera.farClipPlane = 3; weaponCamera.allowHDR = true; weaponCamera.allowMSAA = !Application.isMobilePlatform;
            var weaponData = weaponCamera.GetUniversalAdditionalCameraData(); weaponData.renderType = CameraRenderType.Overlay; weaponData.renderPostProcessing = true; weaponData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; cameraData.cameraStack.Add(weaponCamera);
            void HandLight(string name,Vector3 at,float intensity) { var light=new GameObject(name).AddComponent<Light>(); light.transform.position=weaponCamera.transform.position+at; light.type=LightType.Point; light.range=2.5f; light.intensity=intensity; light.color=Color.white; light.cullingMask=1<<viewLayer; light.shadows=LightShadows.None; }
            HandLight("Soft glove key",new Vector3(-.5f,.45f,.3f),1.2f); HandLight("Soft glove fill",new Vector3(.5f,.25f,.15f),.5f);
            guns = new[] { VoidXWorld.Weapon(weaponCamera.transform, 0), VoidXWorld.Weapon(weaponCamera.transform, 1) };
            weaponPoses = new[] { guns[0].GetComponent<VoidXWeaponPose>(), guns[1].GetComponent<VoidXWeaponPose>() };
            foreach (var gun in guns) { gun.localPosition = new Vector3(.18f, -.11f, .56f); foreach(var t in gun.GetComponentsInChildren<Transform>())t.gameObject.layer=viewLayer; gun.gameObject.SetActive(false); }
            muzzleFlash = VoidXWorld.Part(View.transform, "Muzzle flash", new Vector3(.18f, -.127f, 1.30f), new Vector3(.095f, .095f, .13f), VoidXWorld.Glow, PrimitiveType.Sphere); muzzleFlash.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; muzzleFlash.SetActive(false);
            muzzleLight = new GameObject("Muzzle illumination").AddComponent<Light>(); muzzleLight.transform.SetParent(View.transform, false); muzzleLight.transform.localPosition = new Vector3(.18f, -.127f, 1.24f); muzzleLight.type = LightType.Point; muzzleLight.range = 4; muzzleLight.intensity = 0;
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend = 0;
            rifleSound = Synth("Rifle", .19f, 2200, .8f); shotgunSound = Synth("Shotgun", .32f, 900, 1); hitSound = Synth("Hit", .1f, 680, .16f, true); reloadSound = Synth("Reload", .17f, 180, .17f, true);
            ApplyQuality(); UI = gameObject.AddComponent<VoidXUI>(); UI.Init(this); Menu();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (AutomatedCheck) { UnityEngine.EventSystems.EventSystem.current.enabled = false; gameObject.AddComponent<VoidXSmoke>(); }
#endif
        }
        AudioClip Synth(string name, float duration, float frequency, float power, bool tone = false)
        {
            const int rate = 22050; int count = Mathf.RoundToInt(duration * rate); var data = new float[count]; var rng = new System.Random(42); float low = 0;
            for (int i = 0; i < count; i++) { float t = i / (float)rate, env = Mathf.Exp(-t / duration * 8), noise = (float)rng.NextDouble() * 2 - 1; low = Mathf.Lerp(low, noise, Mathf.Clamp01(frequency / rate)); data[i] = (tone ? Mathf.Sin(t * frequency * 6.28f) : low + Mathf.Sin(t * 85 * 6.28f) * .18f) * env * power; }
            var clip = AudioClip.Create(name, count, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        void Sound(AudioClip clip) { if (Volume > 0) audioSource.PlayOneShot(clip, Volume); }
        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("voidx.sensitivity", Sensitivity); PlayerPrefs.SetFloat("voidx.volume", Volume); PlayerPrefs.SetInt("voidx.quality", Quality); PlayerPrefs.Save(); ApplyQuality();
        }
        public void ApplyQuality()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            // GLES camera stacks can have a single-sample backbuffer even when MSAA is requested.
            // Use the final camera's SMAA on Android so both passes always agree on attachment samples.
            if (pipeline) { pipeline.renderScale = Quality == 0 ? .75f : Quality == 2 ? 1 : .9f; pipeline.shadowDistance = Quality == 0 ? 0 : 45; pipeline.msaaSampleCount = Application.isMobilePlatform ? 1 : Quality == 2 ? 4 : 2; }
            if (View) View.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        }
        void ResetInput() { MoveInput = LookInput = Vector2.zero; FireInput = false; UI?.ClearInput(); }
        public void StartGame()
        {
            ClearActors(); ResetInput(); Health = 100; Score = Kills = Wave = shotCount = hitCount = 0; Elapsed = ReloadLeft = between = recoil = pitch = DamageFlash = HitMarker = 0;
            Ammo[0] = 30; Ammo[1] = 8; Reserve[0] = 180; Reserve[1] = 48; yaw = 180; Player.enabled = false; Player.transform.position = new Vector3(0, .04f, 20); Player.transform.rotation = Quaternion.Euler(0, yaw, 0); Player.enabled = true;
            View.transform.localPosition = new Vector3(0, 1.65f, 0); View.transform.localRotation = Quaternion.identity;
            State = Mode.Playing; SelectWeapon(PlayerPrefs.GetInt("voidx.firstWeapon", 0)); StartWave(); LockMouse(true); UI.Refresh();
        }
        public void Menu()
        {
            State = Mode.Menu; Player.enabled = false; ResetInput(); LockMouse(false); ClearActors(); foreach (var gun in guns) gun.gameObject.SetActive(false); muzzleFlash.SetActive(false); muzzleLight.intensity = 0; UI.Refresh();
        }
        public void Pause()
        {
            if (State != Mode.Playing) return; State = Mode.Paused; ResetInput(); LockMouse(false); muzzleFlash.SetActive(false); muzzleLight.intensity = 0; UI.Refresh();
        }
        public void Resume() { if (State != Mode.Paused) return; State = Mode.Playing; ResetInput(); LockMouse(true); UI.Refresh(); }
        void LockMouse(bool value) { if (!Application.isMobilePlatform) { Cursor.lockState = value && !AutomatedCheck ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !value || AutomatedCheck; } }
        void OnApplicationPause(bool paused) { if (paused) Pause(); }
        void OnApplicationFocus(bool focused) { if (!focused && !AutomatedCheck) Pause(); }
        void Finish(bool win)
        {
            State = Mode.Result; ResetInput(); LockMouse(false); if (win) Score += 1000; ResultTitle = win ? "СЕКТОР ЧИСТ." : "СИГНАЛ ПОТЕРЯН.";
            if (Score > Best) { PlayerPrefs.SetInt("voidx.best", Score); PlayerPrefs.Save(); } UI.Refresh();
        }
        public void SelectWeapon(int index) { WeaponIndex = Mathf.Clamp(index, 0, 1); ReloadLeft = pumpLeft = 0; cooldown = .25f; for (int i = 0; i < guns.Length; i++) { guns[i].gameObject.SetActive(i == WeaponIndex); weaponPoses[i].Animate(0,0); } }
        public void Reload() { if (State != Mode.Playing || ReloadLeft > 0 || Ammo[WeaponIndex] >= Magazine[WeaponIndex] || Reserve[WeaponIndex] <= 0) return; ReloadLeft = WeaponIndex == 0 ? 1.75f : 2.3f; Sound(reloadSound); }
        void StartWave() { Wave++; toSpawn = 2 + Wave * 2; spawnTime = .3f; between = 0; Notice = "ВОЛНА 0" + Wave; NoticeLeft = 3; }
        void Spawn()
        {
            Vector3 at = spawns[0]; float best = -1;
            foreach (var point in spawns) { float distance = Vector3.Distance(point, Player.transform.position); if (distance < 12) continue; bool crowded = enemies.Exists(e => Vector3.Distance(e.root.position, point) < 2); float value = distance + UnityEngine.Random.value * 25 - (crowded ? 100 : 0); if (value > best) { best = value; at = point; } }
            var enemy = new Operative { hp = 65 + Wave * 8, phase = UnityEngine.Random.value * 6, shoot = 1.7f + UnityEngine.Random.value * 1.8f };
            enemy.root = VoidXWorld.Operative(null, out enemy.legs); enemy.root.position = at;
            AddTarget(enemy, new Vector3(0, .9f, 0), new Vector3(.65f, 1.1f, .5f), false); AddTarget(enemy, new Vector3(0, 1.62f, 0), new Vector3(.34f, .31f, .34f), true);
            enemies.Add(enemy); toSpawn--;
        }
        void AddTarget(Operative enemy, Vector3 position, Vector3 size, bool head)
        {
            var o = new GameObject(head ? "Head hitbox" : "Body hitbox"); o.transform.SetParent(enemy.root, false); o.transform.localPosition = position; o.AddComponent<BoxCollider>().size = size;
            var target = o.AddComponent<Target>(); target.head = head; target.Hit = (damage, isHead) => DamageEnemy(enemy, damage, isHead);
        }
        void DamageEnemy(Operative enemy, int damage, bool head)
        {
            if (enemy.hp <= 0) return;
            enemy.hp -= damage; HitMarker = .12f; hitCount++; Sound(hitSound);
            if (enemy.hp > 0) return;
            Kills++; Score += head ? 150 : 100;
            if (Kills % 3 == 0) { var p = VoidXWorld.Part(null, "Medkit +30", enemy.root.position + Vector3.up * .3f, new Vector3(.45f, .35f, .32f), VoidXWorld.White); VoidXWorld.Part(p.transform, "Cross", new Vector3(0, 0, -.51f), new Vector3(.18f, .7f, .03f), VoidXWorld.Black); VoidXWorld.Part(p.transform, "Cross", new Vector3(0, 0, -.52f), new Vector3(.65f, .18f, .03f), VoidXWorld.Black); pickups.Add(new Pickup { root = p.transform }); }
            Burst(enemy.root.position + Vector3.up, 14); enemy.root.gameObject.SetActive(false); Destroy(enemy.root.gameObject); enemies.Remove(enemy);
        }
        public void Shoot()
        {
            if (State != Mode.Playing || cooldown > 0 || ReloadLeft > 0) return;
            if (Ammo[WeaponIndex] <= 0) { Reload(); return; }
            Ammo[WeaponIndex]--; shotCount++; cooldown = WeaponIndex == 0 ? .1f : .7f; recoil = WeaponIndex == 0 ? .065f : .13f; flashLeft = .045f; Sound(WeaponIndex == 0 ? rifleSound : shotgunSound);
            if (WeaponIndex == 1) pumpLeft = .48f;
            int pellets = WeaponIndex == 0 ? 1 : 8; float spread = WeaponIndex == 0 ? .007f : .055f;
            for (int i = 0; i < pellets; i++)
            {
                Vector3 direction = (View.transform.forward + View.transform.right * UnityEngine.Random.Range(-spread, spread) + View.transform.up * UnityEngine.Random.Range(-spread, spread)).normalized;
                // Player is on Ignore Raycast: rays begin at the camera and cannot skip nearby cover.
                if (!Physics.Raycast(View.transform.position, direction, out var hit, 100)) continue;
                var target = hit.collider.GetComponent<Target>(); if (target) target.Hit?.Invoke((WeaponIndex == 0 ? 28 : 16) * (target.head ? 2 : 1), target.head);
                Burst(hit.point, target ? 5 : 3);
            }
        }
        void Burst(Vector3 point, int count)
        {
            for (int i = 0; i < count && effects.Count < 95; i++) { var o = VoidXWorld.Part(null, "Impact spark", point, Vector3.one * UnityEngine.Random.Range(.018f, .045f), VoidXWorld.Glow); o.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; effects.Add(new Effect { root = o.transform, velocity = UnityEngine.Random.insideUnitSphere * 2.5f + Vector3.up * 1.5f, life = .35f + UnityEngine.Random.value * .2f }); }
        }
        void Hurt(int damage) { if (State != Mode.Playing) return; Health = Mathf.Max(0, Health - damage); DamageFlash = .65f; if (Health <= 0) Finish(false); }
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (!AutomatedCheck && Input.GetKeyDown(KeyCode.Escape)) { if (State == Mode.Playing) Pause(); else if (State == Mode.Paused) Resume(); else UI.ClosePanel(); }
            if (State == Mode.Menu)
            {
                Player.transform.position = new Vector3(Mathf.Sin(Time.time * .04f) * 3, 1.2f, 20); Player.transform.rotation = Quaternion.identity; View.transform.LookAt(new Vector3(0, 2.4f, -16)); return;
            }
            if (State != Mode.Playing) return;
            Elapsed += dt; NoticeLeft = Mathf.Max(0, NoticeLeft - dt); HitMarker = Mathf.Max(0, HitMarker - dt); DamageFlash = Mathf.Max(0, DamageFlash - dt * 2);
            cooldown -= dt; recoil = Mathf.Lerp(recoil, 0, dt * 13); flashLeft -= dt; muzzleFlash.SetActive(flashLeft > 0); muzzleLight.intensity = flashLeft > 0 ? 2.8f : 0;
            if (ReloadLeft > 0) { ReloadLeft -= dt; if (ReloadLeft <= 0) { int n = Mathf.Min(Magazine[WeaponIndex] - Ammo[WeaponIndex], Reserve[WeaponIndex]); Ammo[WeaponIndex] += n; Reserve[WeaponIndex] -= n; ReloadLeft = 0; } }
            if (!AutomatedCheck) { if (Input.GetKeyDown(KeyCode.R)) Reload(); if (Input.GetKeyDown(KeyCode.Q)) SelectWeapon(1 - WeaponIndex); if (Input.GetKeyDown(KeyCode.Alpha1)) SelectWeapon(0); if (Input.GetKeyDown(KeyCode.Alpha2)) SelectWeapon(1); }
            Vector2 move = MoveInput + (AutomatedCheck ? Vector2.zero : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))); move = Vector2.ClampMagnitude(move, 1);
            Vector2 look = LookInput; LookInput = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked) look += new Vector2(Input.GetAxis("Mouse X") * 12, Input.GetAxis("Mouse Y") * 12);
            yaw += look.x * (.035f + Sensitivity * .0014f); pitch = Mathf.Clamp(pitch - look.y * (.035f + Sensitivity * .0014f), -70, 70);
            Player.transform.rotation = Quaternion.Euler(0, yaw, 0); View.transform.localRotation = Quaternion.Euler(pitch - recoil * 28, 0, 0);
            if (Player.isGrounded && verticalSpeed < 0) verticalSpeed = -1; verticalSpeed -= 12 * dt;
            Vector3 velocity = (Player.transform.right * move.x + Player.transform.forward * move.y) * (!AutomatedCheck && Input.GetKey(KeyCode.LeftShift) ? 5.4f : 4.25f); velocity.y = verticalSpeed; Player.Move(velocity * dt);
            stepPhase += move.magnitude * dt * 9; View.transform.localPosition = new Vector3(0, 1.65f + Mathf.Sin(stepPhase) * .018f * move.magnitude, 0);
            float reloadProgress = ReloadLeft > 0 ? 1 - ReloadLeft / (WeaponIndex == 0 ? 1.75f : 2.3f) : 0;
            float reloadPose = Mathf.Pow(Mathf.Sin(reloadProgress * Mathf.PI), 2);
            var gun = guns[WeaponIndex]; gun.localPosition = new Vector3(.18f + Mathf.Sin(stepPhase) * .006f, -.11f + Mathf.Cos(stepPhase * 2) * .004f - .035f * reloadPose, .56f - recoil);
            gun.localRotation = Quaternion.Euler(recoil * 40 + 10 * reloadPose, -8 * reloadPose, 18 * reloadPose);
            pumpLeft = Mathf.Max(0, pumpLeft - dt); weaponPoses[WeaponIndex].Animate(reloadProgress, pumpLeft > 0 ? Mathf.Sin((.48f - pumpLeft) / .48f * Mathf.PI) : 0);
            muzzleFlash.transform.position = View.transform.TransformPoint(gun.localPosition + gun.localRotation * new Vector3(0,.013f,.74f)); muzzleFlash.transform.rotation = View.transform.rotation * gun.localRotation; muzzleLight.transform.position = View.transform.TransformPoint(gun.localPosition + gun.localRotation * new Vector3(0,.013f,.68f));
            if (FireInput || (Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButton(0))) Shoot();
            if (toSpawn > 0) { spawnTime -= dt; if (spawnTime <= 0 && enemies.Count < 6) { Spawn(); spawnTime = 1.3f; } }
            navTime -= dt; if (navTime <= 0) { navigation = new FlowMap(Player.transform.position, VoidXWorld.Covers); navTime = .65f; }
            UpdateEnemies(dt); UpdateProjectiles(dt); UpdateEffects(dt);
            if (State == Mode.Playing && toSpawn == 0 && enemies.Count == 0)
            {
                if (Wave >= 5) { Finish(true); return; }
                if (between <= 0) { between = 5; Health = Mathf.Min(100, Health + 25); Reserve[0] += 60; Reserve[1] += 12; Notice = "ПЕРЕДЫШКА · +25 HP"; NoticeLeft = 4; }
                else { between -= dt; if (between <= 0) StartWave(); }
            }
        }
        void UpdateEnemies(float dt)
        {
            foreach (var enemy in enemies)
            {
                Vector3 at = enemy.root.position, target = Player.transform.position; float distance = Vector3.Distance(at, target); bool sees = ClearLine(at, target, VoidXWorld.Covers);
                bool walking = distance > 6 || !sees; enemy.phase += dt * 5;
                if (walking)
                {
                    Vector3 next = sees ? target : navigation.Next(at); Vector3 direction = (next - at).normalized;
                    foreach (var other in enemies) if (other != enemy) { Vector3 d = at - other.root.position; if (d.magnitude < 1.2f && d.magnitude > .01f) direction += d.normalized * (1.2f - d.magnitude); }
                    enemy.root.position = MoveCircle(at, direction.x * (1.35f + Wave * .13f) * dt, direction.z * (1.35f + Wave * .13f) * dt, .42f, VoidXWorld.Covers);
                }
                enemy.root.rotation = Quaternion.LookRotation(new Vector3(target.x - at.x, 0, target.z - at.z));
                for (int i = 0; i < 2; i++) enemy.legs[i].localRotation = Quaternion.Euler(walking ? Mathf.Sin(enemy.phase) * 26 * (i == 0 ? 1 : -1) : 0, 0, 0);
                enemy.shoot -= dt;
                if (sees && distance < 29 && enemy.shoot <= 0)
                {
                    enemy.shoot = 2.4f - Wave * .16f + UnityEngine.Random.value * .7f;
                    Vector3 start = at + Vector3.up * 1.25f; Vector3 aim = (target + new Vector3(UnityEngine.Random.Range(-.6f, .6f), 1.5f + UnityEngine.Random.Range(-.15f, .15f), UnityEngine.Random.Range(-.6f, .6f)) - start).normalized;
                    var o = VoidXWorld.Part(null, "Enemy tracer", start, new Vector3(.028f, .028f, .48f), VoidXWorld.Glow); o.transform.rotation = Quaternion.LookRotation(aim); o.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    bullets.Add(new Bullet { root = o.transform, velocity = aim * (12 + Wave), life = 4 });
                }
            }
        }
        void UpdateProjectiles(float dt)
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var p = bullets[i]; Vector3 from = p.root.position, to = from + p.velocity * dt; p.life -= dt; p.root.position = to;
                if (!ClearLine(from, to, VoidXWorld.Covers) || Mathf.Abs(to.x) > 24 || Mathf.Abs(to.z) > 24) p.life = 0;
                else
                {
                    Vector3 centre = Player.transform.position + Vector3.up * 1.4f, segment = to - from; float fraction = Mathf.Clamp01(Vector3.Dot(centre - from, segment) / segment.sqrMagnitude);
                    if (Vector3.Distance(from + segment * fraction, centre) < .48f) { Hurt(8 + Wave * 2); p.life = 0; }
                }
                if (p.life <= 0) { Destroy(p.root.gameObject); bullets.RemoveAt(i); }
            }
        }
        void UpdateEffects(float dt)
        {
            for (int i = effects.Count - 1; i >= 0; i--) { var fx = effects[i]; fx.root.position += fx.velocity * dt; fx.velocity.y -= 7 * dt; fx.life -= dt; if (fx.life <= 0) { Destroy(fx.root.gameObject); effects.RemoveAt(i); } }
            for (int i = pickups.Count - 1; i >= 0; i--) { var p = pickups[i]; p.root.Rotate(0, dt * 55, 0); var v = p.root.position; v.y = .3f + Mathf.Sin(Elapsed * 3) * .06f; p.root.position = v; if (Health < 100 && Vector3.Distance(Player.transform.position, v) < 1.25f) { Health = Mathf.Min(100, Health + 30); Destroy(p.root.gameObject); pickups.RemoveAt(i); Sound(reloadSound); } }
        }
        void ClearActors()
        {
            foreach (var e in enemies) Destroy(e.root.gameObject); foreach (var p in bullets) Destroy(p.root.gameObject); foreach (var p in pickups) Destroy(p.root.gameObject); foreach (var e in effects) Destroy(e.root.gameObject);
            enemies.Clear(); bullets.Clear(); pickups.Clear(); effects.Clear();
        }
        public static bool Blocked(Vector3 at, float radius, List<VoidXWorld.Cover> covers)
        {
            if (Mathf.Abs(at.x) > 24 - radius || Mathf.Abs(at.z) > 24 - radius) return true;
            foreach (var c in covers) { float dx = at.x - Mathf.Clamp(at.x, c.centre.x - c.size.x * .5f, c.centre.x + c.size.x * .5f), dz = at.z - Mathf.Clamp(at.z, c.centre.y - c.size.y * .5f, c.centre.y + c.size.y * .5f); if (dx * dx + dz * dz < radius * radius) return true; } return false;
        }
        public static Vector3 MoveCircle(Vector3 at, float dx, float dz, float radius, List<VoidXWorld.Cover> covers)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(new Vector2(dx, dz).magnitude / (radius * .5f)));
            for (int i = 0; i < steps; i++) { var next = at + Vector3.right * (dx / steps); if (!Blocked(next, radius, covers)) at = next; next = at + Vector3.forward * (dz / steps); if (!Blocked(next, radius, covers)) at = next; } return at;
        }
        public static bool ClearLine(Vector3 from, Vector3 to, List<VoidXWorld.Cover> covers)
        {
            foreach (var c in covers)
            {
                float lo = 0, hi = 1;
                for (int i = 0; i < 2; i++) { float p = i == 0 ? from.x : from.z, d = i == 0 ? to.x - from.x : to.z - from.z, centre = i == 0 ? c.centre.x : c.centre.y, half = (i == 0 ? c.size.x : c.size.y) * .5f;
                    if (Mathf.Abs(d) < .00001f) { if (p < centre - half || p > centre + half) { lo = 2; break; } } else { float a = (centre - half - p) / d, b = (centre + half - p) / d; lo = Mathf.Max(lo, Mathf.Min(a, b)); hi = Mathf.Min(hi, Mathf.Max(a, b)); } }
                if (lo <= hi && hi >= 0 && lo <= 1) return false;
            } return true;
        }
        public sealed class FlowMap
        {
            const int Size = 32; const float Cell = 1.5f; readonly int[] distance = new int[Size * Size]; readonly bool[] free = new bool[Size * Size];
            static Vector3 At(int i) => new(-24 + (i % Size + .5f) * Cell, 0, -24 + (i / Size + .5f) * Cell);
            static int Index(Vector3 p) => Mathf.Clamp(Mathf.FloorToInt((p.z + 24) / Cell), 0, Size - 1) * Size + Mathf.Clamp(Mathf.FloorToInt((p.x + 24) / Cell), 0, Size - 1);
            static IEnumerable<int> Neighbours(int i) { int x = i % Size, z = i / Size; if (x > 0) yield return i - 1; if (x < Size - 1) yield return i + 1; if (z > 0) yield return i - Size; if (z < Size - 1) yield return i + Size; }
            public FlowMap(Vector3 target, List<VoidXWorld.Cover> covers)
            {
                for (int i = 0; i < distance.Length; i++) { distance[i] = -1; free[i] = !Blocked(At(i), .48f, covers); }
                int start = Index(target); if (!free[start]) { float best = float.MaxValue; for (int i = 0; i < distance.Length; i++) if (free[i] && (At(i) - target).sqrMagnitude < best) { best = (At(i) - target).sqrMagnitude; start = i; } }
                var queue = new Queue<int>(); queue.Enqueue(start); distance[start] = 0;
                while (queue.Count > 0) { int i = queue.Dequeue(); foreach (int next in Neighbours(i)) if (free[next] && distance[next] < 0) { distance[next] = distance[i] + 1; queue.Enqueue(next); } }
            }
            public Vector3 Next(Vector3 at) { int index = Index(at), best = index, d = distance[index] < 0 ? int.MaxValue : distance[index]; foreach (int next in Neighbours(index)) if (distance[next] >= 0 && distance[next] < d) { d = distance[next]; best = next; } return At(best); }
        }
    }
}
