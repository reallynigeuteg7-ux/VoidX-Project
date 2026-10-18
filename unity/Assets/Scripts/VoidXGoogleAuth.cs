using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace VoidX
{
    public sealed class VoidXGoogleAuth : MonoBehaviour
    {
        public bool IsSignedIn { get; private set; }
        public bool IsBusy { get; private set; }
        public string DisplayName { get; private set; } = "Гость";
        public string Email { get; private set; } = "";
        public string UserId { get; private set; } = "";
        public string Message { get; private set; } = "";
        public event Action Changed;
        readonly Queue<string> results = new();
        [Serializable] class Config { public string webClientId; }
        [Serializable, Preserve] class Result { public string state, error, uid, name, email; }
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass bridge;
        AuthCallback callback;
        [Preserve] sealed class AuthCallback : AndroidJavaProxy
        {
            readonly VoidXGoogleAuth auth;
            public AuthCallback(VoidXGoogleAuth owner) : base("com.voidx.auth.GoogleAuthBridge$Callback") { auth = owner; }
            [Preserve] public void onResult(string json) { lock (auth.results) auth.results.Enqueue(json); }
        }
#endif
        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var file = Resources.Load<TextAsset>("VoidXGoogleConfig");
            var config = file ? JsonUtility.FromJson<Config>(file.text) : null;
            if (config == null || string.IsNullOrWhiteSpace(config.webClientId)) return;
            try {
                callback = new AuthCallback(this);
                bridge = new AndroidJavaClass("com.voidx.auth.GoogleAuthBridge");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                bridge.CallStatic("initialize", activity, config.webClientId, callback);
            } catch (Exception) { Message = "Вход Google сейчас недоступен. Попробуйте позже."; Changed?.Invoke(); }
#endif
        }
        public void SignIn()
        {
            if (IsBusy || IsSignedIn) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridge == null) { Message = "Вход Google пока недоступен. Можно продолжить играть гостем."; Changed?.Invoke(); return; }
            IsBusy = true; Message = "Выберите аккаунт в окне Google."; Changed?.Invoke();
            try { bridge.CallStatic("signIn"); } catch (Exception) { IsBusy = false; Message = "Не удалось открыть Google. Попробуйте ещё раз."; Changed?.Invoke(); }
#else
            Message = "Вход через Google доступен в Android-версии."; Changed?.Invoke();
#endif
        }
        public void SignOut()
        {
            lock (results) results.Clear();
#if UNITY_ANDROID && !UNITY_EDITOR
            try { bridge?.CallStatic("signOut"); } catch (Exception) { /* Clear local identity even if the provider is unavailable. */ }
#endif
            SetGuest(); Message = "Вы вышли из аккаунта."; Changed?.Invoke();
        }
        void Update()
        {
            while (true) {
                string json; lock (results) { if (results.Count == 0) break; json = results.Dequeue(); }
                Result result; try { result = JsonUtility.FromJson<Result>(json); } catch (Exception) { continue; }
                if (result == null) continue;
                IsBusy = result.state == "busy";
                if (result.state == "signedIn" && !string.IsNullOrEmpty(result.uid)) {
                    IsSignedIn = true; UserId = result.uid; DisplayName = string.IsNullOrEmpty(result.name) ? "Игрок" : result.name; Email = result.email ?? ""; Message = "Выполнен вход через Google.";
                } else if (result.state == "error") {
                    Message = result.error switch {
                        "cancelled" => "Вход отменён.",
                        "no_account" => "Добавьте Google-аккаунт на устройство и повторите вход.",
                        "network" => "Не удалось подключиться к Google. Проверьте интернет.",
                        "verification" => "Google не подтвердил вход. Попробуйте ещё раз.",
                        _ => "Вход Google сейчас недоступен. Попробуйте позже."
                    };
                } else if (result.state == "guest") { SetGuest(); Message = ""; }
                Changed?.Invoke();
            }
        }
        void SetGuest() { IsSignedIn = IsBusy = false; DisplayName = "Гость"; Email = UserId = ""; }
        void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { bridge?.CallStatic("dispose"); } catch (Exception) { }
            bridge?.Dispose();
#endif
        }
    }
}
