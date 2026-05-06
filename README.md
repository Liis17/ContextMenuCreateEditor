# Context Menu Create Editor

Десктопное приложение для Windows, позволяющее управлять пунктами подменю **«Создать»** в контекстном меню Проводника. Без прав администратора — все записи идут в `HKEY_CURRENT_USER\Software\Classes`.

WPF + .NET 10 (Windows), [WPF-UI](https://github.com/lepoco/wpfui) для Fluent-оформления.

---

## Возможности

- Просмотр списка собственных и системных пунктов «Создать».
- Добавление новых пунктов с произвольным именем в меню, именем создаваемого файла, расширением и шаблоном содержимого.
- Редактирование и удаление собственных пунктов (системные защищены от изменений).
- Поиск по списку (имя в меню, имя файла, расширение).
- Многострочный шаблон содержимого: при создании файла Explorer копирует подготовленный текст (например, скелет JSON, заголовок Markdown, базовый HTML).
- **Command-режим для файлов без расширения** — `Dockerfile`, `Makefile`, `.gitignore` и т.п. создаются с точным именем, без вынужденного `Dockerfile.dockerfile`.
- Резервная копия затрагиваемой ветки реестра (`reg export HKCU\Software\Classes`) одной кнопкой.
- Перезапуск Проводника (Shell) и обновление ассоциаций через `SHChangeNotify`.
- Защита от перезаписи чужих ассоциаций: при конфликте расширения предлагается выбор «Использовать существующий ProgID / Пересоздать принудительно / Отмена».
- Логирование в `%LOCALAPPDATA%\ContextMenuCreateEditor\app.log`, глобальные обработчики необработанных исключений.

---

## Сборка

Требуется .NET 10 SDK (на момент написания — preview).

```powershell
git clone <repo>
cd ContextMenuCreateEditor
dotnet build ContextMenuCreateEditor.sln
```

Запуск отладочной сборки:

```powershell
dotnet run --project ContextMenuCreateEditor.WPF
```

Или собранный `.exe`:

```
ContextMenuCreateEditor.WPF\bin\Debug\net10.0-windows\ContextMenuCreateEditor.WPF.exe
```

---

## Использование

### Добавление обычного пункта (с расширением)

1. Кнопка **«Добавить»**.
2. Заполнить:
   - **Имя в меню «Создать»** — например, `Markdown файл`.
   - **Имя создаваемого файла** — например, `New Document`.
   - **Расширение** — `.md`.
   - **Шаблон содержимого** — `# Заголовок` и т.п.
3. **OK**. Пункт появится в Проводнике сразу (ассоциации обновляются через `SHChangeNotify`).
4. ПКМ в Проводнике → **Создать** → **Markdown файл** → создаётся файл с шаблоном внутри.

### Добавление файла без расширения (Dockerfile, Makefile)

1. **Добавить**.
2. Заполнить:
   - **Имя в меню** — `Dockerfile`.
   - **Имя файла** — `Dockerfile`.
   - **Расширение** — оставить пустым.
   - **Шаблон** — содержимое Dockerfile.
3. **OK**.

При пустом расширении приложение автоматически переключается в **Command-режим**: в `HKCU\Software\Classes\.dockerfile\ShellNew\Command` пишется команда вызова самого приложения. Когда пользователь жмёт «Создать → Dockerfile» в Проводнике, Explorer запускает приложение с путём целевой папки, и оно создаёт файл с **точным** именем (`Dockerfile`, без `.dockerfile` на конце). Коллизии разруливаются суффиксом `Dockerfile (1)`, `Dockerfile (2)`.

### Редактирование

Навести курсор на карточку → кликнуть по иконке карандаша → изменить любое поле → OK. Если меняется расширение — старая запись удаляется и создаётся новая.

### Удаление

Навести курсор → кликнуть по иконке корзины → подтвердить. Удаляется ключ ShellNew, ProgID (если он наш) и файл шаблона.

### Резервная копия

Кнопка **«Резервная копия»** запускает `reg export HKCU\Software\Classes` и сохраняет `.reg`-файл в `%LOCALAPPDATA%\ContextMenuCreateEditor\Backups\`. Восстановление — двойным кликом по `.reg`-файлу.

### Перезапуск Проводника

Если меню не обновилось — кнопка **«Перезапустить проводник»** (с подтверждением). Снимает все процессы `explorer.exe` через `taskkill /F /IM explorer.exe` и поднимает shell заново.

---

## Архитектура

```
ContextMenuCreateEditor.WPF/
├── App.xaml(.cs)              Bootstrap, DI вручную, обработчики ошибок, CLI --create
├── MainWindow.xaml(.cs)       Главное окно (DataContext = MainViewModel)
├── Models/
│   └── ShellNewItem.cs
├── Services/
│   ├── HkcuRegistryService    HKCU\Software\Classes — чтение/запись ShellNew
│   ├── TemplateStorage        Шаблоны в %LOCALAPPDATA%\…\Templates\
│   ├── IconService            SHGetFileInfo + SHGetStockIconInfo (с try/finally)
│   ├── ExplorerRefresher      SHChangeNotify + taskkill/start explorer.exe
│   ├── RegistryBackupService  reg.exe export
│   ├── FileCreator            CLI-обработчик для Command-режима
│   └── AppLogger              Простой файловый лог
├── ViewModels/
│   ├── MainViewModel          ObservableCollection, ICollectionView с фильтром
│   ├── ItemEditorViewModel
│   ├── ShellNewItemViewModel
│   └── RelayCommand
├── Views/
│   ├── EditItemDialog         Add / Edit
│   └── ConflictDialog         Три варианта при конфликте расширения
└── UserControls/
    └── PreviewItem            Карточка с двумя icon-only кнопками на hover
```

Логика — в сервисах, состояние UI — во ViewModel'ях, View — без code-behind за исключением маршалинга событий. View↔VM связаны через DataContext и события (без singleton'ов).

---

## Реестр: что и куда пишется

Для пункта с расширением:

```
HKCU\Software\Classes\.<ext>
    (default) = CustomShellNew<GUID>
HKCU\Software\Classes\.<ext>\ShellNew
    NullFile = ""                                  ; либо
    FileName = "<абс. путь к шаблону>"
    CreatedBy = "ContextMenuCreateEditor"
HKCU\Software\Classes\CustomShellNew<GUID>
    (default) = "<DisplayName>"
    CreatedBy = "ContextMenuCreateEditor"
```

Для пункта без расширения (Command-режим):

```
HKCU\Software\Classes\.<derived>\ShellNew
    Command = "<exe>" --create "<templatePath>" "%1" "<baseName>"
    CreatedBy = "ContextMenuCreateEditor"
```

Метка `CreatedBy=ContextMenuCreateEditor` отличает собственные пункты от системных. Старый формат (`CustomShellNew<GUID>` без метки) тоже распознаётся как собственный.

При конфликте — если default-value у `\.<ext>` указывает на чужой ProgID — приложение **никогда** не перезапишет ProgID без явного выбора пользователя в диалоге.

---

## Ограничения

- Регистрация только в HKCU (для текущего пользователя). Запись в HKLM/HKCR требует UAC и в текущей версии не реализована.
- Команда в Command-режиме хранит **абсолютный путь** к `.exe`. Если переместить приложение, существующие Dockerfile-подобные пункты сломаются — пересоздай их через приложение из нового места.
- Стандартный (FileName) ShellNew всегда создаёт файл вида «Новый &lt;FriendlyName&gt;.&lt;ext&gt;» — это поведение Explorer'а, обходится только через Command-режим.
- Только Windows. Кроссплатформенность не предусмотрена (зависит от Win32 API: `SHGetFileInfo`, `SHChangeNotify`, реестр).

---

## Хранилище данных

- `%LOCALAPPDATA%\ContextMenuCreateEditor\Templates\` — файлы шаблонов содержимого.
- `%LOCALAPPDATA%\ContextMenuCreateEditor\Backups\` — `.reg`-бэкапы.
- `%LOCALAPPDATA%\ContextMenuCreateEditor\app.log` — лог.

---

## Лицензия

Не указана.
