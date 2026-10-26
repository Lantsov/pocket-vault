# PocketVault Android — этап 1/4

Техническая сборка Android MVP 1.0.0. На этом этапе доступны логотип,
название приложения и кнопка «Проверить криптографию» с результатами реальных
проверок на Android runtime. Работа с документами и выбор файлов относятся к
следующим этапам и пока отсутствуют.

## Технологии и границы

- .NET 10, .NET MAUI 10, C#, XAML; единственный TargetFramework — `net10.0-android`.
- Минимум Android 10 / API 29; compile/target SDK определяется установленным
  .NET Android workload (при проверке использован API 36).
- ApplicationId: `com.lantsov.pocketvault`; название: `PocketVault`;
  ApplicationDisplayVersion: `1.0.0`; ApplicationVersion: `1`.
- Единственная прямая NuGet-зависимость приложения — `Microsoft.Maui.Controls`,
  версия берётся из установленного MAUI SDK (`$(MauiVersion)`).
- `PocketVault.Core` подключён через ProjectReference. Приложение непосредственно
  вызывает существующий `VaultFormat.Encrypt/Decrypt`; исходники Core не изменены.
- Формат `.pvlt` v1, AES-256-GCM, PBKDF2-HMAC-SHA256 (600 000 итераций),
  52-байтовый заголовок и его аутентификация как AAD сохранены.
- `VaultFiles` не вызывается: Windows handle, удаление исходника и файловые
  операции Windows не входят в Android-фундамент.
- Приложение не использует веб-технологии и не выполняет сетевых операций.
  Исходный Android manifest не запрашивает разрешений на сеть и доступ к общему
  хранилищу; резервное копирование отключено. Инструменты .NET Android добавляют
  `INTERNET` в итоговый Debug manifest для отладки.

`PocketVault.Android.slnx` содержит только Android и Core. Исходный
`PocketVault.slnx`, `PocketVault.App`, Windows-версия 1.1.1,
`scripts/build.ps1` и `.github/workflows/build.yml` не изменены.
Android SDK и MAUI workload не нужны для Windows solution.
Android-релиз и GitHub Releases на этом этапе не создаются и не изменяются.

## Инструменты

Нужны .NET SDK 10 (учитывается корневой `global.json`), официальный workload
`maui-android`, JDK 21 и Android SDK с platform API 36, build-tools 36.0.0,
platform-tools (ADB). Для запуска нужен Android API 29+ с включённой USB-отладкой
либо эмулятор с соответствующим system image и аппаратным ускорением.

```powershell
dotnet --info
dotnet workload install maui-android
dotnet workload list
```

При отсутствии Android SDK можно использовать официальный MSBuild target
`InstallAndroidDependencies`, задав каталоги SDK/JDK и принятие лицензий:

```powershell
dotnet build PocketVault.Android/PocketVault.Android.csproj `
  -t:InstallAndroidDependencies -f net10.0-android `
  -p:AndroidSdkDirectory=C:\Android\Sdk `
  -p:JavaSdkDirectory=C:\Android\jdk-21 `
  -p:AcceptAndroidSDKLicenses=True
```

Существующие SDK и пользовательские настройки удалять не требуется.
Официальные инструкции: [установка .NET MAUI](https://learn.microsoft.com/dotnet/maui/get-started/installation)
и [изменения .NET MAUI 10 / Android](https://learn.microsoft.com/dotnet/maui/whats-new/dotnet-10).

## Сборка

Из корня репозитория, PowerShell:

```powershell
./scripts/build-android.ps1 `
  -AndroidSdkDirectory "$env:LOCALAPPDATA\Android\Sdk" `
  -JavaSdkDirectory 'C:\path\to\jdk-21'
```

По умолчанию создаётся Debug APK с отладочной подписью. Скрипт использует
локальный `.tools/dotnet/dotnet.exe`, если он есть, иначе `dotnet` из PATH.
Можно явно задать `-DotnetPath`. NuGet-кэш и CLI home находятся внутри `.tools`.
`-PackageSource` позволяет использовать подготовленный локальный NuGet-источник.

Эквивалент без скрипта:

```powershell
dotnet build PocketVault.Android.slnx -c Debug `
  -p:AndroidSdkDirectory="$env:LOCALAPPDATA\Android\Sdk" `
  -p:JavaSdkDirectory='C:\path\to\jdk-21' `
  -p:AndroidPackageFormats=apk
```

APK: `PocketVault.Android/bin/Debug/net10.0-android/com.lantsov.pocketvault-Signed.apk`.
Сборка включает managed assemblies в APK, поэтому обычный `adb install` работает
без fast deployment из IDE. Для локальной Release-сборки есть
`./scripts/build-android.ps1 -Configuration Release` с теми же SDK-параметрами;
это не публикация и не Android-релиз. Для воспроизведения результата используйте
версии инструментов из раздела проверки ниже.

Проверка Windows отдельно, без Android SDK:

```powershell
./scripts/build.ps1
```

Скрипт собирает исходный Windows solution и запускает существующий исполняемый
набор тестов; `dotnet test` не заменяет этот запуск.

## Запуск приложения

Запустите имеющийся AVD через Android Studio либо командой
`emulator -avd <имя_AVD>`. Подключённое устройство должно появиться как `device`:

```powershell
adb devices -l
adb -s <serial> install -r ./PocketVault.Android/bin/Debug/net10.0-android/com.lantsov.pocketvault-Signed.apk
adb -s <serial> shell am start -n com.lantsov.pocketvault/.MainActivity
```

На экране нажмите «Проверить криптографию». Проверка выполняется в фоне,
кнопка временно блокируется. Результаты каждого пункта или ошибка выводятся
на экране. Каждый клик запускает тест заново.

## Воспроизводимый smoke-test на Android runtime

После сборки Debug APK:

```powershell
./scripts/test-android-crypto.ps1 `
  -AdbPath "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" `
  -Serial emulator-5556
```

`-Serial` можно опустить, если подключено ровно одно устройство.
`-ApkPath` позволяет явно указать Debug APK. Скрипт устанавливает его с `adb install -r`,
останавливает приложение и запускает Activity с уникальным `crypto_smoke_run_id`.
Этот автоматический вход включён только в Debug; кнопка работает также в Release.
Скрипт не удаляет установленное приложение; конфликт подписи приводит к ошибке.

Приложение само выполняет 11 проверок и записывает
`files/crypto-smoke-result.json` в приватном каталоге. Скрипт читает файл через
`adb shell run-as`, ждёт совпадения ID текущего запуска, проверяет Android runtime,
API, количество пунктов и каждый результат. Отчёт сохраняется в
`dist/android/crypto-smoke-<runId>.json`. Ошибка или таймаут завершают скрипт
ошибкой; сообщение ADB об успешном старте Activity не считается прохождением теста.

Проверки:

1. AES-256-GCM: известные ciphertext/tag для нулевого 256-битного ключа,
   96-битного IV и 16 нулевых байтов; расшифровка и отказ при изменении AAD.
2. PBKDF2-HMAC-SHA256: фиксированный вектор из
   [RFC 7914, раздел 11](https://www.rfc-editor.org/rfc/rfc7914.html#section-11).
3. ZIP: сжатие/чтение Unicode-имени и точного бинарного содержимого.
4. Создание `.pvlt` через Core с проверкой версии, алгоритмов, заголовка и 600 000 итераций.
5. Расшифровка нового контейнера с точным сравнением имени и всех байтов.
6. Исходный `v1-container.base64`: `наследие.txt`, UTF-8
   `PocketVault v1 — совместимость формата.\n`, пароль `Пароль совместимости 2026`.
7. Исходный `v1.1.0-container.base64`: `v1.1.0-example.txt`, UTF-8
   `PocketVault 1.1.0 compatibility fixture.\n`, пароль `Public fixture password 1.1.0`.
8. Неверный пароль отклоняется с ошибкой аутентификации.
9. Изменённые шифротекст и тег отклоняются.
10. Изменённые соль и nonce в заголовке отклоняются.
11. Пустой файл восстанавливается с точным именем и нулевой длиной.

`\n` выше означает один LF без BOM. Fixtures встроены ссылками на существующие
файлы Windows-тестов, не копируются и не пересоздаются.
Контейнерные проверки и оба fixtures используют настоящие 600 000 итераций.
Отдельная реализация AES, PBKDF2 или ZIP не добавлена: используются API .NET.
Прямой запуск smoke-test вне Android запрещён проверкой платформы.

## Результат проверки этапа 1

Локально, 26 октября 2026 года (Windows x64):

| Компонент | Использованная версия |
| --- | --- |
| .NET SDK / MSBuild | 10.0.403 / 18.9.15 |
| .NET runtime | 10.0.14 |
| MAUI / workload | 10.0.0 / `maui-android 10.0.0/10.0.100` |
| .NET Android SDK pack | `Microsoft.Android.Sdk.Windows 36.1.2` |
| Android platform | API 36, revision 2 |
| Android build-tools | 36.0.0 |
| JDK | установленный JetBrains Runtime OpenJDK 21.0.11 |
| Android Emulator | 36.2.12; WHPX |
| AVD | `Medium_Phone_API_36.0`, Android 16 / API 36, x86_64, Google APIs Play Store |

Существующие Android SDK и JDK использованы без удаления/замены. Workload добавлен
только в локальный `.tools/dotnet`. Из-за зависания IPv6-соединений сетевого клиента
.NET официальные NUPKG с `api.nuget.org/v3-flatcontainer` загружены через `curl`
в `.tools/nuget-source`. Установка выполнена командой:

```powershell
./.tools/dotnet/dotnet.exe workload install maui-android `
  --skip-manifest-update --source ./.tools/nuget-source
```

Android собран `scripts/build-android.ps1` с `-PackageSource .tools/nuget-source`
и явными путями существующих SDK/JDK. Эти локальные кэши не входят в Git.
В обычном окружении достаточно стандартного NuGet-источника и инструкции выше.
В проекте отключён `UseDefaultPublishRuntimeIdentifier`: SDK 10.0.403 иначе
добавляет RID настольного хоста `win-x64` к Android Release с trimming.
Android solution собирается для `android-arm64` и `android-x64`.

| Проверка | Результат |
| --- | --- |
| Windows solution Release | Успешно; 0 предупреждений, 0 ошибок |
| Существующие Windows/Core/WPF-тесты | 43/43, 0 ошибок |
| Windows без MAUI/Android workload | Сборка и тесты прошли до установки workload |
| Android Debug APK | Успешно; 0 предупреждений, 0 ошибок |
| Android Release APK, trimming и AOT для ARM64/x64 | Успешно; 0 предупреждений, 0 ошибок |
| Установка и запуск Android Debug | Успешно на AVD API 36 через ADB |
| Crypto smoke-test на Android runtime | 11/11, включая оба fixtures и точное восстановление имени/байтов |
| Кнопка в Release-приложении после trimming/AOT | 11/11; подтверждено текстом экрана и новым `PASS` в logcat |

Сохранён [отчёт Android runtime](verification/android-crypto-smoke-api36.json),
полученный `scripts/test-android-crypto.ps1` с runId
`d338dc60381445559ead4f8bb074923a`. Отчёт относится к запуску на Android,
а не к результатам Windows-тестов.

Release также установлен и запущен на том же эмуляторе. Проверка запущена
нажатием реальной кнопки, runId `81a63996b2694336962c2ab9458c61d9`;
все 11 результатов на экране — `OK`, ошибок AndroidRuntime не обнаружено.
В Release manifest нет разрешения `INTERNET`.

## Пока не проверено и ограничения

- Физическое Android-устройство не подключено.
- Минимальный Android 10 / API 29 и ARM64 runtime отдельно не проверены.
- Работа с документами, Storage Access Framework, выбор файлов и остальной MVP
  не входят в этап 1.
- Проверки синтетические; большие пользовательские файлы и нагрузка на память
  мобильного устройства не тестировались. Ограничения in-memory Core сохранены.
- Подпись для распространения, Google Play и Android-релиз не настраивались.
