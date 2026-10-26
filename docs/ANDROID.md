# PocketVault Android — этап 3/4

Android MVP 1.0.0: тёмный минималистичный мобильный интерфейс, выбор одного
документа через Storage Access Framework, шифрование и расшифровка совместимых
с Windows `.pvlt` v1, системное сохранение результата. Лимит исходного и
восстановленного файла — 32 МиБ. Реализация интерфейса и проверки текущего
этапа описаны в [отчёте этапа 3](ANDROID_STAGE3.md). Этап 4 и публикация не входят
в текущую работу. Ниже сохранены инструкции сборки и исторические результаты
этапов 1–2; они не заменяют проверки нового интерфейса.

## Технологии и границы

- .NET 10, .NET MAUI 10, C#, XAML; единственный TargetFramework — `net10.0-android`.
- Минимум Android 10 / API 29; compile/target SDK определяется установленным
  .NET Android workload (при проверке использован API 36).
- ApplicationId: `com.lantsov.pocketvault`; название: `PocketVault`;
  ApplicationDisplayVersion: `1.0.0`; ApplicationVersion: `1`.
- Единственная прямая NuGet-зависимость приложения — `Microsoft.Maui.Controls`,
  версия берётся из установленного MAUI SDK (`$(MauiVersion)`).
- `PocketVault.Core` подключён через ProjectReference. Приложение непосредственно
  вызывает `VaultFormat.Encrypt/Decrypt`. В Core добавлена перегрузка `Decrypt`
  с лимитом восстановленного файла, который проверяется до выделения массива.
  Старый API и стандартный лимит Windows 128 МиБ сохранены.
- Формат `.pvlt` v1, AES-256-GCM, PBKDF2-HMAC-SHA256 (600 000 итераций),
  52-байтовый заголовок и его аутентификация как AAD сохранены.
- Из `VaultFiles` используется только генератор нейтрального имени контейнера.
  Windows handle, удаление исходника и файловые операции Windows на Android не вызываются.
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

Нажмите «Обзор файлов» и выберите один документ в системном диалоге. Приложение
покажет имя и доступный размер. Расширение `.pvlt` распознаётся без учёта регистра.
Для шифрования введите пароль и подтверждение; для расшифровки — пароль.
После обработки выберите место в системном диалоге «Сохранить».

Шифрование предлагает имя `vault-yyyyMMdd-xxxxxxxx.pvlt`; исходное имя находится
только в зашифрованном ZIP. Расшифровка предлагает исходное имя только после
успешной аутентификации и проверки ZIP. При неверном пароле, повреждении или
превышении лимита диалог создания результата не открывается.

Кнопки и поля блокируются на время выбора, криптографии и сохранения. Индикатор
не показывает выдуманный процент. Основное действие доступно только при выбранном
файле, непустом пароле и совпавшем подтверждении для шифрования. Техническая
кнопка проверки криптографии удалена из пользовательского интерфейса; входы
проверок через ADB доступны только в Debug.

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
Этот автоматический вход включён только в Debug; в текущем пользовательском
интерфейсе отдельной кнопки проверки нет.
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

## Исторические ограничения этапа 1

- Физическое Android-устройство не подключено.
- Минимальный Android 10 / API 29 и ARM64 runtime отдельно не проверены.
- Работа с документами и Storage Access Framework добавлена на этапе 2, см. ниже.
- Проверки синтетические; большие пользовательские файлы и нагрузка на память
  мобильного устройства не тестировались. Ограничения in-memory Core сохранены.
- Подпись для распространения, Google Play и Android-релиз не настраивались.

## Документы, память и безопасность этапа 2

- `ACTION_OPEN_DOCUMENT`, `CATEGORY_OPENABLE`, MIME `*/*`: один документ,
  включая изображения, ZIP, произвольные бинарные файлы и `.pvlt`.
  Имя и необязательный размер берутся из `IOpenableColumns`.
  Путь на накопителе не вычисляется; чтение и запись идут через `ContentResolver`.
- `ACTION_CREATE_DOCUMENT` создаёт новый результат. По
  [контракту Android SAF](https://developer.android.com/training/data-storage/shared/documents-files#create-file)
  совпавшее имя получает суффикс, существующий файл не перезаписывается.
  Ответ с URI исходного документа дополнительно отклоняется без записи/удаления.
- Ни разрешения всего накопителя, ни `MANAGE_EXTERNAL_STORAGE`, ни доступ к
  дереву каталогов не запрашиваются. URI-гранты не сохраняются между запусками;
  после потери доступа документ нужно выбрать заново.
- Имена проверяются `VaultFormat.ValidateFileName` для переносимости на Windows:
  пути, зарезервированные имена устройств и недопустимые символы отклоняются.
  Имя не исправляется молча. Unicode, кириллица и допустимые специальные символы
  сохраняются точно.
- Известный размер проверяется до чтения. Независимо от метаданных поток
  читается с жёстким лимитом 32 МиБ плюс один байт для обнаружения превышения.
  Для контейнера допускаются ещё 1 МиБ ZIP overhead и 68 байт заголовка/тега.
  Windows-контейнер с большим содержимым может быть отклонён Android: это
  ограничение приложения, а не новая версия формата.
- После GCM-аутентификации Core проверяет `ZipArchiveEntry.Length` до создания
  восстановленного массива. Фактический поток ZIP также проверяется на точную
  длину. Маленький контейнер с распакованным файлом больше 32 МиБ отклоняется.
- Чтение, ZIP, криптография, запись и очистка выполняются вне UI-потока.
  Повторная операция блокируется как UI, так и сервисом. Отмена диалога
  возвращает рабочее состояние. Пароль очищается из полей перед обработкой.
- Открытый ZIP и восстановленный файл существуют только в памяти; временные
  файлы в cache/files не создаются. Доступные буферы обнуляются, включая ошибку
  и отмену сохранения. Это не гарантия стирания всех копий: .NET/Android могут
  копировать данные; строки пароля в управляемой памяти нельзя гарантированно
  стереть. Preferences, логи документов/паролей, сеть и облачные SDK не используются.
- Ошибка открытия нового документа, записи, flush или close вызывает попытку
  `DocumentsContract.DeleteDocument` только для созданного результата. При
  отсутствии прав, неподдерживаемом удалении или ошибке провайдера пользователь
  видит предупреждение о возможном пустом/частичном файле и необходимости
  проверить его вручную. Общей атомарности для сторонних DocumentsProvider нет;
  после успешного close приложение также не может гарантировать завершение
  внутренней синхронизации удалённого провайдера.
- Исходные документы никогда не удаляются. Этап 2 не включал финальный дизайн;
  новый интерфейс описан в [этапе 3](ANDROID_STAGE3.md). Потоковое шифрование
  и публикация в текущую работу не входят.

## Воспроизводимые проверки этапа 2

Windows: `./scripts/build.ps1` собирает Release и выполняет 74 проверки:
43 существующие Core/Windows/WPF и 31 новую проверку сервиса Android.
Новые проверки компилируют тот же `DocumentOperations.cs`, который использует
Android, с имитацией интерфейса провайдера. Они проверяют точное имя/байты,
пустой файл, Unicode, специальные символы, неверный пароль, повреждение,
обрезание, лишние байты, безопасные имена, метаданные и жёсткие лимиты чтения,
границу 32 МиБ, сжатый ZIP 32 МиБ + 1, отмены, отсутствие прав, ошибки записи,
flush/close, успешное и запрещённое удаление частичного результата,
защиту исходного URI, очистку выходных буферов и повторный запуск.

Те же 31 проверки на настоящем Android runtime в Debug:

```powershell
./scripts/test-android-documents.ps1 `
  -AdbPath 'C:\Users\lants\AppData\Local\Android\Sdk\platform-tools\adb.exe' `
  -Serial emulator-5554
```

Скрипт устанавливает Debug APK, передаёт уникальный `document_smoke_run_id`,
ждёт свежий `files/document-smoke-result.json`, проверяет Android runtime,
API и все 31 результата; сохраняет отчёт в `dist/android/`. Этот вход и
синтетические тесты провайдера исключены из Release.
Он не заменяет проверку реальных системных диалогов и стороннего провайдера.
Crypto smoke-test этапа 1 отдельно проверяет оба исходных legacy fixtures.

Для ручного обмена с Windows есть синтетический корпус:

```powershell
./.tools/dotnet/dotnet.exe PocketVault.Tests/bin/Release/net10.0-windows/PocketVault.Tests.dll `
  --android-interop-prepare ./dist/android/new-interop-directory
```

Команда требует новый каталог и создаёт Unicode-файл с бинарным содержимым и
`windows.pvlt`. Публичный тестовый пароль — `Stage2-Test-2026`.
Передайте оба файла на Android. Через UI зашифруйте исходник и расшифруйте
`windows.pvlt`, проверив предложенное Unicode-имя. Скопируйте результаты в этот
же каталог как `android.pvlt` и `android-restored.bin`, затем выполните:

```powershell
./.tools/dotnet/dotnet.exe PocketVault.Tests/bin/Release/net10.0-windows/PocketVault.Tests.dll `
  --android-interop-verify ./dist/android/new-interop-directory
```

Проверка расшифровывает Android-контейнер настоящим Windows `VaultFiles.Decrypt`,
сравнивает исходное имя и каждый байт в обоих направлениях, печатает SHA-256.
Это публичные синтетические данные; пользовательские файлы не нужны.

## Результат проверки этапа 2

26 октября 2026 года, Windows x64; те же .NET SDK 10.0.403, runtime 10.0.14,
MAUI 10.0.0 и Android SDK pack 36.1.2, что на этапе 1.
Для сборки использован JDK `C:\Users\lants\.jdks\jbr-21.0.11`.
Проверки Android выполнялись на AVD `Medium_Phone_API_36.0`, Android 16 /
API 36, x86_64, Google APIs Play Store, `emulator-5554`, Emulator 36.2.12/WHPX.

| Проверка | Фактический результат |
| --- | --- |
| Windows solution Release | 0 предупреждений, 0 ошибок |
| Windows/Core/WPF и общий сервис Android | 74/74, включая все 43 прежние проверки |
| Android Debug APK | 0 предупреждений, 0 ошибок; установлен и запущен |
| Android Release APK, trimming/AOT ARM64 и x64 | 0 предупреждений, 0 ошибок; x64 установлен и запущен |
| Сервис документов на Android runtime, Debug | 31/31; [JSON-отчёт](verification/android-documents-api36.json), runId `e940ded91cab49efb6ba88303e1b2bde` |
| Криптография и оба legacy fixtures на Android, Debug | 11/11; [JSON-отчёт](verification/android-stage2-crypto-api36.json), runId `672530df28d84b3ba91e61a3ca83c0c6` |
| Android → Windows через реальные UI/SAF | Файл выбран в Downloads; нейтральный `vault-20261026-ee76c557.pvlt` сохранён Android; Windows `VaultFiles.Decrypt` восстановил точное имя и все байты |
| Windows → Android через реальные UI/SAF | Windows `VaultFiles.Encrypt`; контейнер выбран Android; после проверки пароля системный диалог предложил точное имя; сохранённый файл побайтово совпал |
| Неверный пароль в Android UI | Ошибка аутентификации; диалог создания результата не открыт |
| Отмена выбора/сохранения в Android UI | Показан статус отмены; кнопки снова доступны; повторная операция успешна |
| Release после trimming/AOT: SAF и расшифровка | Исходное Unicode-имя восстановлено, файл сохранён, все байты совпали |
| Совпадение имени в системном сохранении | В Release создан `Обмен #1 (a+b) & тест (1).bin`; прежний файл сохранён |
| Приватные `files/cache` после Debug-операций | Только `profileInstalled` и два JSON-отчёта; открытый ZIP/восстановленный файл отсутствуют |
| Release manifest | Нет `INTERNET`, `READ/WRITE_EXTERNAL_STORAGE` и `MANAGE_EXTERNAL_STORAGE`; backup отключён |

Файл для межплатформенного обмена: `Обмен #1 (a+b) & тест.bin`, 320 байт,
включая UTF-8 и все значения байта 0–255. SHA-256:
`E42067911761887D497358D512D2B04A8472CBCCA7CA10038A445BF061728570`.
Сохранено [описание фактического SAF-обмена](verification/android-stage2-saf-api36.json).
Android UI проверялся через ADB/UI Automator; управление окном Windows через
Computer Use завершилось таймаутом и для этих результатов не использовалось.

Ограничения проверки: физическое устройство не подключалось; API 29 и ARM64
runtime не запускались. Сбои провайдера, отозванные права, неизвестный/ложный
размер и удаления частичного результата проверены синтетической имитацией
`IDocumentAccess` на обоих runtime, а не сбоями настоящего стороннего облачного
DocumentsProvider. Реальный SAF проверен с системным Downloads на API 36.
Предельные 32 МиБ проверены синтетическим потоком/сжатым контейнером; пик памяти
для случайных несжимаемых 32 МиБ на слабом физическом устройстве не измерялся.
Атомарность провайдеров и гарантированное стирание строк пароля не заявляются.
На момент завершения этапа 2 этапы 3–4 ещё не начинались, публикация не выполнялась.
Текущее состояние интерфейса и новые результаты проверки — в [этапе 3](ANDROID_STAGE3.md).
