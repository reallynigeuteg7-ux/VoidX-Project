# Вход через Google

VoidX 0.6.0 использует нативный Android Credential Manager и Google ID. Кнопка в главном меню открывает экран аккаунта, затем системный выбор Google-аккаунта. Можно продолжать играть гостем.

## Поток входа

1. `VoidXGoogleAuth` вызывает Android-библиотеку `VoidXGoogle.androidlib`.
2. `GetSignInWithGoogleOption` получает Google ID token с одноразовым случайным nonce.
3. Официальный `GoogleIdTokenVerifier` проверяет подпись Google, issuer, audience и срок действия; мост дополнительно проверяет nonce.
4. В Unity возвращаются только подтверждённые `sub`, имя и email. Токен и секрет клиента не записываются в файлы и журналы.
5. На устройстве хранится только флаг восстановления входа. При следующем запуске Credential Manager пытается получить новую учётную запись из ранее разрешённых. Выход очищает этот флаг и состояние Credential Manager.

Это профиль текущей одиночной игры. Рекорд и настройки остаются на устройстве. Для будущих облачных сохранений, покупок или сетевой игры сервер должен самостоятельно проверять Google ID token и создавать серверную сессию; переданный клиентом UID не является разрешением на доступ.

## Google Cloud

Проект: `voidx-512015`. Экран согласия: VoidX. Адрес поддержки: `voidx-game-support@googlegroups.com`.

- Web OAuth client: `VoidX Google ID`. Его публичный ID находится в `Assets/Resources/VoidXGoogleConfig.json`.
- Android OAuth client: `VoidX Android APK`, пакет `com.voidx.game`.
- SHA-1 текущей подписи APK: `E1:65:19:1D:FC:45:C1:DB:78:12:16:1C:E4:67:FF:93:38:DB:B8:D8`.
- Если Google Play будет подписывать APK своим ключом, нужно зарегистрировать SHA-1 этого ключа отдельным Android-клиентом.

Дополнительные OAuth scopes не запрашиваются. Для входа с базовым профилем действует исключение Google из ограничений списка тестовых пользователей: https://support.google.com/cloud/answer/15549945?hl=en.

## Сборка

`build-unity.ps1` принимает `-GoogleClientId` или `VOIDX_GOOGLE_WEB_CLIENT_ID`. Используется именно ID клиента Web application, а не Android client ID. Если ID не передан, берётся существующий публичный JSON из Resources. Android-сборка прерывается при отсутствии корректного ID.

Модуль компилируется с Android SDK 36, target SDK игры остаётся 35, минимальный Android — 8.0. Зависимости закреплены в Gradle: Credential Manager 1.6.0, Google ID 1.2.1, Google API Client 2.9.1. Секрет OAuth-клиента для нативного входа не используется.

Документация Google: https://developer.android.com/identity/sign-in/credential-manager-siwg-implementation и https://developers.google.com/identity/sign-in/android/backend-auth.
