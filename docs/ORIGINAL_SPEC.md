# ПРОЕКТ: QuadDesk v0.1
## Portable Windows Zone Manager для больших дисплеев

Ты — senior Windows desktop engineer, Win32 developer, software architect и QA engineer.

Нужно не просто спроектировать приложение, а создать, собрать, протестировать и подготовить готовый portable-релиз.

Название проекта:

# QuadDesk

Основная задача QuadDesk:

превратить один большой физический монитор/телевизор Windows в несколько логических рабочих зон для обычных desktop-приложений.

Это должен быть лёгкий бесплатный аналог той части DisplayFusion Pro / FancyZones, которая отвечает за:

- разделение большого дисплея на рабочие области;
- snap окон;
- перемещение окон между зонами;
- maximize внутри зоны;
- сохранение layouts;
- пользовательские layouts;
- rules;
- workspace profiles.

---

# 1. ИСХОДНАЯ КОНФИГУРАЦИЯ

Windows 11 x64.

Физически:

- основной монитор ПК;
- дополнительный телевизор Haier 50" S2 Pro;
- Haier подключён одним HDMI;
- типичное разрешение Haier:

3840×2160 @ 60 Hz.

Haier используется преимущественно для:

- программирования;
- IDE;
- браузера;
- ChatGPT;
- AI-агентов;
- терминалов;
- документации;
- систем мониторинга;
- рабочих приложений.

Windows должна продолжать видеть Haier как:

ОДИН физический монитор.

QuadDesk НЕ должен создавать дополнительные дисплеи Windows.

---

# 2. ГЛАВНЫЙ ПРИНЦИП

Например, пользователь может выбрать обычный layout 2×2:

┌────────────────────┬────────────────────┐
│       ZONE 1       │       ZONE 2       │
├────────────────────┼────────────────────┤
│       ZONE 3       │       ZONE 4       │
└────────────────────┴────────────────────┘

Но приложение НЕ должно быть привязано к 2×2.

Нужны произвольные layouts.

Например:

# MAIN + 3 STACKED

┌──────────────────────────┬──────────────────┐
│                          │      ZONE 2      │
│                          ├──────────────────┤
│          ZONE 1          │      ZONE 3      │
│                          ├──────────────────┤
│                          │      ZONE 4      │
└──────────────────────────┴──────────────────┘

Например:

- Zone 1 = 65% ширины экрана;
- правая колонка = 35%;
- Zone 2 / 3 / 4 делят правую колонку вертикально.

Или:

┌───────────┬───────────┬───────────┐
│     1     │     2     │     3     │
├───────────┼───────────┼───────────┤
│     4     │     5     │     6     │
└───────────┴───────────┴───────────┘

Или:

┌───────────────────────┬─────────────┐
│                       │      2      │
│           1           ├─────────────┤
│                       │      3      │
└───────────────────────┴─────────────┘

Или любые другие прямоугольные комбинации.

---

# 3. ЧТО QUADDESK НЕ ДОЛЖЕН ДЕЛАТЬ

НЕ использовать:

- Virtual Display Driver;
- OBS;
- screen capture;
- video capture;
- framebuffer composition;
- desktop duplication как основу решения;
- GPU video compositor;
- H.264/H.265;
- streaming;
- virtual monitors;
- kernel drivers;
- display drivers;
- DLL injection;
- Electron;
- Chromium;
- WebView;
- cloud;
- telemetry;
- remote services.

QuadDesk должен управлять:

ТОЛЬКО обычными окнами Windows.

---

# 4. ТЕХНОЛОГИИ

Предпочтительный стек:

- C#;
- .NET 8 или более новый стабильный .NET;
- WinForms;
- Win32 API через P/Invoke.

Приложение:

- Windows 11 x64;
- self-contained;
- single-file EXE;
- portable;
- без installer;
- без отдельного .NET Runtime;
- без прав администратора.

---

# 5. АРХИТЕКТУРА LAYOUT ENGINE

КРИТИЧЕСКИ ВАЖНО:

НЕ делать ZoneManager, жёстко рассчитанный под:

width / 2
height / 2

или фиксированные четыре зоны.

Нужен полноценный:

# LayoutEngine

Модель:

Layout
→ LayoutNode tree
→ List<Zone>
→ Physical Rectangles

Рекомендуемая модель:

LayoutNode

- SplitNode
- ZoneNode

SplitNode содержит:

- Orientation:
  - Vertical
  - Horizontal

- Ratio:
  0.0 ... 1.0

- FirstChild
- SecondChild

ZoneNode содержит:

- stable ZoneId;
- display name;
- optional metadata.

---

# 6. ПРИМЕР ВЛОЖЕННОГО SPLIT TREE

Layout:

MAIN + 3 STACKED

визуально:

┌──────────────────────────┬──────────────────┐
│                          │        2         │
│                          ├──────────────────┤
│            1             │        3         │
│                          ├──────────────────┤
│                          │        4         │
└──────────────────────────┴──────────────────┘

Можно представить так:

Root
└── Vertical Split 65/35
    ├── Zone 1
    └── Horizontal Split
        ├── Zone 2
        └── Horizontal Split
            ├── Zone 3
            └── Zone 4

Но LayoutEngine не должен зависеть от конкретного числа уровней.

Нужна рекурсивная реализация.

---

# 7. ОТНОСИТЕЛЬНЫЕ КООРДИНАТЫ

Layouts должны храниться независимо от разрешения монитора.

Предпочтительно использовать:

0.0 ... 1.0

Например:

Zone 1:

x = 0
y = 0
width = 0.65
height = 1

Zone 2:

x = 0.65
y = 0
width = 0.35
height = 0.3333

Zone 3:

x = 0.65
y = 0.3333
width = 0.35
height = 0.3333

Zone 4:

x = 0.65
y = 0.6666
width = 0.35
height = 0.3334

При:

3840×2160

LayoutEngine автоматически конвертирует их в physical pixels.

---

# 8. PIXEL MODE

Дополнительно можно поддержать Pixel Mode.

Например:

Zone 1:

2496×2160

Zones 2–4:

1344×720.

Но основным способом хранения layout должны быть относительные координаты или Split Tree.

Это нужно для корректной работы при:

- другом разрешении;
- смене DPI;
- другом мониторе;
- смене ориентации.

---

# 9. PRESET LAYOUTS

Добавить встроенные layouts.

Минимально:

## SINGLE

1 зона:

100% × 100%.

## DUAL VERTICAL

┌──────────────┬──────────────┐
│      1       │      2       │
└──────────────┴──────────────┘

## DUAL HORIZONTAL

┌─────────────────────────────┐
│              1              │
├─────────────────────────────┤
│              2              │
└─────────────────────────────┘

## TRIPLE COLUMNS

┌──────────┬──────────┬──────────┐
│    1     │    2     │    3     │
└──────────┴──────────┴──────────┘

## TRIPLE MAIN

┌──────────────────────┬──────────────┐
│                      │      2       │
│          1           ├──────────────┤
│                      │      3       │
└──────────────────────┴──────────────┘

## QUAD 2×2

┌──────────────┬──────────────┐
│      1       │      2       │
├──────────────┼──────────────┤
│      3       │      4       │
└──────────────┴──────────────┘

## MAIN + 3 STACKED

┌──────────────────────┬──────────────┐
│                      │      2       │
│                      ├──────────────┤
│          1           │      3       │
│                      ├──────────────┤
│                      │      4       │
└──────────────────────┴──────────────┘

Default ratio:

65% / 35%.

Правая колонка:

33.33%
33.33%
33.34%.

## SIX 3×2

┌──────────┬──────────┬──────────┐
│    1     │    2     │    3     │
├──────────┼──────────┼──────────┤
│    4     │    5     │    6     │
└──────────┴──────────┴──────────┘

---

# 10. CUSTOM LAYOUT EDITOR

Это важная функция.

Пользователь должен иметь возможность самостоятельно создать layout.

Нужен визуальный редактор.

Показывать выбранный физический монитор как прямоугольник.

Начальное состояние:

┌─────────────────────────────┐
│                             │
│          ZONE 1             │
│                             │
└─────────────────────────────┘

Пользователь выбирает зону и может:

- Split Vertical;
- Split Horizontal.

Например:

Split Vertical:

┌──────────────┬──────────────┐
│      1       │      2       │
└──────────────┴──────────────┘

После этого Zone 2:

Split Horizontal:

┌──────────────┬──────────────┐
│              │      2       │
│      1       ├──────────────┤
│              │      3       │
└──────────────┴──────────────┘

Потом нижнюю правую:

Split Horizontal:

┌──────────────┬──────────────┐
│              │      2       │
│              ├──────────────┤
│      1       │      3       │
│              ├──────────────┤
│              │      4       │
└──────────────┴──────────────┘

---

# 11. RESIZABLE SPLITTERS

Разделители между зонами должны быть изменяемыми.

Пользователь может мышью передвинуть splitter.

Например:

50/50

→

65/35.

Сохранять ratio.

Пример:

Vertical Split:

Ratio = 0.65.

Horizontal Split:

Ratio = 0.333.

Минимальный допустимый размер зоны:

configurable.

Например:

minimum width = 200 px
minimum height = 150 px.

Не позволять создать нулевые или отрицательные зоны.

---

# 12. СОХРАНЕНИЕ LAYOUT

Пользователь может:

- Save;
- Save As;
- Rename;
- Duplicate;
- Delete;
- Reset.

Каждый layout имеет:

- ID;
- Name;
- Layout Tree;
- optional Hotkey;
- createdAt;
- updatedAt.

Пример:

Coding
Agents
Browsing
Quad
Main+3
Dual
Single.

---

# 13. ACTIVE LAYOUT

Tray:

Layout
  Single
  Dual Vertical
  Dual Horizontal
  Triple Columns
  Triple Main
  Quad 2×2
  Main + 3
  Six 3×2
  Custom layouts...

Переключение должно происходить без перезапуска приложения.

---

# 14. РЕЖИМЫ QUADDESK

Нужны:

## ENABLED

Zone management активен.

## DISABLED / SINGLE

QuadDesk не вмешивается в окна.

Физический Haier снова используется как обычный монитор.

Например:

Ctrl + Win + 0

→ Disable Zones.

При повторном включении:

возвращается последний активный layout.

---

# 15. HOTKEYS ДЛЯ LAYOUTS

По умолчанию:

Ctrl + Win + 1
→ Single

Ctrl + Win + 2
→ Dual

Ctrl + Win + 3
→ Triple Main

Ctrl + Win + 4
→ Quad

Ctrl + Win + 6
→ Six

Дополнительные custom layouts:

пользователь может назначить самостоятельно.

Не хардкодить архитектуру вокруг этих hotkeys.

---

# 16. HOTKEYS ДЛЯ ZONES

Win + Alt + 1
→ active window → Zone 1

Win + Alt + 2
→ Zone 2

...

Win + Alt + 9
→ Zone 9.

Если layout содержит меньше зон:

лишний hotkey ничего не делает.

---

# 17. НАВИГАЦИЯ МЕЖДУ ZONES

Также:

Win + Alt + Left
Win + Alt + Right
Win + Alt + Up
Win + Alt + Down.

Навигация НЕ должна быть жёстко привязана к сетке.

Использовать геометрический поиск соседней зоны.

Алгоритм:

1. найти центр текущей зоны;
2. рассматривать только зоны в выбранном направлении;
3. отдавать приоритет зонам с пересечением по перпендикулярной оси;
4. затем выбирать ближайшую по расстоянию.

Например для:

Main + 3

если окно в Zone 1:

Right

может переводить его в ближайшую правую зону по вертикальной позиции окна.

Если окно максимально занимает Zone 1:

по умолчанию выбрать Zone 2 или ближайшую к центру текущего окна.

Поведение должно быть предсказуемым.

---

# 18. ВЫБОР TARGET MONITOR

При первом запуске показать:

"Выберите дисплей для QuadDesk"

Для каждого:

- Friendly Name;
- resolution;
- refresh rate;
- primary / secondary;
- coordinates.

Например:

Monitor 1
2560×1440
Primary

HAIER TV
3840×2160
60 Hz
Secondary.

Использовать стабильный identifier.

Не полагаться только на:

DISPLAY1
DISPLAY2.

Использовать при возможности:

QueryDisplayConfig
DisplayConfigGetDeviceInfo
Device Path.

---

# 19. MULTI-MONITOR SAFETY

QuadDesk должен влиять ТОЛЬКО на выбранный физический монитор.

Основной монитор ПК не должен изменяться.

Не snap'ить окна на основном экране.

Не ограничивать maximize на основном экране.

Не показывать overlay на основном экране.

---

# 20. DRAG & SNAP

Во время перетаскивания окна на target monitor:

показать overlay.

Например:

┌──────────────────────┬──────────────┐
│                      │      2       │
│                      ├──────────────┤
│          1           │      3       │
│                      ├──────────────┤
│                      │      4       │
└──────────────────────┴──────────────┘

При наведении:

candidate zone подсвечивается.

После отпускания:

окно занимает эту зону.

Использовать:

SetWinEventHook

например:

EVENT_SYSTEM_MOVESIZESTART
EVENT_SYSTEM_MOVESIZEEND.

Не использовать tight polling loop.

---

# 21. OVERLAY

ZoneOverlay должен быть:

- topmost;
- transparent;
- click-through;
- no activate;
- не получать focus;
- не блокировать mouse;
- не отображаться в Alt+Tab;
- появляться только при необходимости.

Отображать:

- границы zones;
- optional zone number;
- active candidate zone.

Не делать тяжёлых GPU-эффектов.

---

# 22. MAXIMIZE WITHIN ZONE

ЭТО КЛЮЧЕВАЯ ФУНКЦИЯ.

Если окно относится к Zone 1 и пользователь нажимает стандартную:

□ Maximize

оно должно занимать:

только Zone 1.

Не весь физический Haier.

Поддержать:

- стандартную кнопку Maximize;
- double-click по title bar;
- Win+Up;
- приложение, самостоятельно вызывающее maximize.

Не использовать DLL injection.

Не модифицировать приложение.

Использовать:

SetWinEventHook
GetWindowPlacement
SetWindowPlacement
SetWindowPos
IsZoomed
DwmGetWindowAttribute.

---

# 23. MAXIMIZE STATE

QuadDesk должен поддерживать собственное понятие:

Logical Maximized.

Например:

Window A:

ZoneId = main

LogicalState = MaximizedWithinZone.

При этом Windows может считать окно:

SW_SHOWNORMAL

если это необходимо для ограничения rectangle.

QuadDesk должен сохранить UX:

- кнопка maximize;
- restore;
- double click.

При Restore вернуть предыдущий rectangle внутри той же зоны.

---

# 24. FLICKER

При перехвате maximize минимизировать:

- full-screen flash;
- resize flicker;
- jump.

Не использовать медленные delayed timers, если можно обработать событие сразу.

Если невозможно полностью исключить один-frame maximize:

задокументировать это.

Но попытаться сделать максимально незаметно.

---

# 25. WINDOW STATE

Хранить:

HWND → WindowRuntimeState.

Минимально:

- ZoneId;
- PreviousRect;
- LogicalMaximized;
- ManualOverride;
- LayoutId.

Не считать:

process = window.

Каждый HWND независим.

---

# 26. AUTO ZONE DETECTION

Если окно ещё не зарегистрировано:

определить его зону по:

максимальной площади пересечения WindowRect с ZoneRect.

Если overlap слишком маленький:

не считать окно принадлежащим Zone.

---

# 27. DWM BOUNDS

Windows 11 имеет invisible resize borders.

Необходимо корректно работать с:

DwmGetWindowAttribute

DWMWA_EXTENDED_FRAME_BOUNDS.

Нужно добиться визуального совпадения окон с границами zones.

Не должно быть:

- щелей;
- overlap;
- 5–10 px ошибок;
- выхода за соседнюю зону.

---

# 28. DPI

Приложение:

Per-Monitor DPI Aware V2.

Проверить:

100%
125%
150%.

Target monitor и основной монитор могут использовать разный scaling.

Не путать:

logical coordinates
physical pixels.

---

# 29. TASKBAR

QuadDesk НЕ создаёт:

- taskbar;
- Start menu;
- virtual taskbar.

Пользователь хочет убрать Windows taskbar с Haier.

Приложение может показать инструкцию:

Windows Settings
→ Personalization
→ Taskbar
→ Taskbar behaviors
→ Show my taskbar on all displays
→ OFF.

Не менять эту системную настройку автоматически без разрешения пользователя.

---

# 30. FULL BOUNDS / WORK AREA

Настройка:

UseFullMonitorBounds.

true:
zones используют весь физический экран.

false:
zones используют Monitor.WorkArea.

Default:

true.

---

# 31. AUTO ASSIGN RULES

Поддержать rules.

Например:

Chrome → browser
ChatGPT → ai
VS Code → main
Terminal → terminal.

Но rule должен ссылаться на:

LayoutId
ZoneId

а не только ZoneNumber.

Пример:

{
  "process": "Code.exe",
  "layoutId": "coding-main3",
  "zoneId": "main"
}

Также поддержать:

- processName;
- optional windowClass;
- optional titleContains.

---

# 32. RULE MATCHING

AutoAssign применяется только к:

normal top-level application windows.

Не применять к:

- Save As;
- Open;
- confirmation dialogs;
- popup;
- context menu;
- tooltip;
- splash screen;
- owned dialogs.

---

# 33. MANUAL OVERRIDE

Если rule говорит:

Chrome → browser

но пользователь вручную переместил конкретное окно Chrome:

не возвращать его обратно автоматически.

Manual move имеет приоритет.

Rule нужен только для:

initial placement.

---

# 34. WORKSPACE

Добавить:

Save Workspace
Restore Workspace.

Workspace хранит:

- LayoutId;
- Window identity;
- ZoneId;
- rectangle relative to Zone;
- logical maximize state.

Window identity может содержать:

- executable path;
- process name;
- class;
- sanitized title hint.

Не хранить содержимое окна.

---

# 35. WORKSPACE RESTORE

При Restore:

1. включить нужный Layout;
2. пересчитать Zone rectangles;
3. найти уже открытые окна;
4. разместить их по ZoneId;
5. восстановить Logical Maximized state.

Запуск отсутствующих приложений:

optional.

OFF по умолчанию.

---

# 36. SYSTEM TRAY

Меню:

QuadDesk

✓ Enabled

Layout >
  Single
  Dual Vertical
  Dual Horizontal
  Triple Columns
  Triple Main
  Quad 2×2
  Main + 3
  Six 3×2
  Custom...

Move Active Window >
  Zone 1
  Zone 2
  ...

Auto Snap ✓

Edit Layout...

Manage Layouts...

Rules...

Save Workspace

Restore Workspace

Select Monitor...

Settings...

Start with Windows

Open config.json

About

Exit

---

# 37. MAIN WINDOW

Минимальный UI.

Показывать:

QuadDesk

Display:
HAIER TV

Resolution:
3840×2160 @ 60Hz

Status:
ACTIVE

Current Layout:
Main + 3

Visual Preview:

┌──────────────────┬──────────┐
│                  │    2     │
│                  ├──────────┤
│        1         │    3     │
│                  ├──────────┤
│                  │    4     │
└──────────────────┴──────────┘

Кнопки:

Edit Layout
Change Layout
Disable QuadDesk.

---

# 38. CUSTOM LAYOUT EDITOR UX

Не делать Photoshop.

Нужен простой split editor.

Основные действия:

- Split Vertical;
- Split Horizontal;
- Delete Zone;
- Merge;
- Drag Splitter;
- Rename Zone;
- Save;
- Save As;
- Reset.

При выборе zone:

показывать:

Zone ID
Width %
Height %
Pixel preview.

---

# 39. SPLITTER DRAGGING

При drag splitter:

обновлять preview realtime.

Но физические окна можно:

Option A:
обновлять realtime;

или:

Option B:
перемещать после отпускания.

Для MVP предпочтительно:

после отпускания,

чтобы не создавать десятки SetWindowPos вызовов в секунду.

---

# 40. LAYOUT CHANGE

При переключении Layout:

если окна уже принадлежат старым zones:

попытаться сохранить смысл.

Приоритет:

1. стабильный ZoneId;
2. matching semantic name;
3. nearest zone;
4. оставить окно как есть.

Например:

old:

main
chat
terminal

new:

main
chat
agents
terminal

main должен остаться main.

---

# 41. FULLSCREEN / GAME MODE

Не вмешиваться в:

- DirectX exclusive fullscreen;
- games;
- DRM video;
- protected surfaces;
- secure desktop;
- lock screen.

Пользователь может просто:

Disable QuadDesk

и использовать весь Haier как обычный 4K-дисплей.

Добавить:

Ctrl + Win + 0
→ Enable / Disable QuadDesk.

---

# 42. WINDOW FILTER

Перед обработкой HWND проверить:

- IsWindow;
- IsWindowVisible;
- top-level root window;
- owner;
- WS_EX_TOOLWINDOW;
- DWM cloaked;
- window size;
- system shell.

Игнорировать:

Shell_TrayWnd
Progman
WorkerW
StartMenuExperienceHost
SearchHost
ShellExperienceHost
tooltips
menus
IME windows.

Не двигать собственные окна QuadDesk.

---

# 43. PERFORMANCE

КРИТИЧЕСКИ ВАЖНО.

QuadDesk НЕ рендерит рабочий стол.

Не делает capture.

Не делает compositor.

Не занимается streaming.

Поэтому ожидается:

Idle CPU:
≈ 0%.

GPU:
≈ 0%.

RAM:
желательно < 50–100 MB.

Использовать event-driven architecture.

Не использовать:

while(true)
Sleep(10)

если событие доступно через Windows.

---

# 44. DISPLAY EVENTS

Обработать:

WM_DISPLAYCHANGE
WM_DPICHANGED

и изменение monitor topology.

Если Haier отключён:

- приложение не падает;
- zone-management приостанавливается.

Если Haier возвращается:

найти его по stable identifier и восстановить состояние.

---

# 45. WINDOW RACE CONDITIONS

HWND может исчезнуть между event и обработкой.

Перед Win32 operation:

IsWindow(hwnd).

Не хранить stale HWND бесконечно.

Убирать state закрытых окон.

---

# 46. GLOBAL HOTKEYS

Использовать:

RegisterHotKey.

Не ставить low-level keyboard hook, если он не нужен.

Все hotkeys:

должны корректно unregister при Exit.

Если hotkey занят:

показать понятную ошибку и позволить поменять binding.

---

# 47. CONFIG

Рядом с EXE:

QuadDesk.exe
config.json
layouts/
workspaces/
logs/

Пример:

{
  "enabled": true,

  "targetMonitor": {
    "devicePath": "...",
    "friendlyName": "HAIER TV"
  },

  "activeLayoutId": "main-plus-3",

  "autoSnap": true,

  "snapThreshold": 30,

  "useFullMonitorBounds": true,

  "restoreWorkspaceOnStart": false,

  "hotkeys": {
    "toggle": "Ctrl+Win+0",
    "zone1": "Win+Alt+1",
    "zone2": "Win+Alt+2",
    "zone3": "Win+Alt+3",
    "zone4": "Win+Alt+4"
  },

  "rules": []
}

---

# 48. LAYOUT FILE

Например:

layouts/main-plus-3.json

может содержать:

{
  "id": "main-plus-3",
  "name": "Main + 3",

  "root": {
    "type": "split",
    "orientation": "vertical",
    "ratio": 0.65,

    "first": {
      "type": "zone",
      "id": "main",
      "name": "Main"
    },

    "second": {
      "type": "split",
      "orientation": "horizontal",
      "ratio": 0.333333,

      "first": {
        "type": "zone",
        "id": "top-right",
        "name": "Top Right"
      },

      "second": {
        "type": "split",
        "orientation": "horizontal",
        "ratio": 0.5,

        "first": {
          "type": "zone",
          "id": "middle-right",
          "name": "Middle Right"
        },

        "second": {
          "type": "zone",
          "id": "bottom-right",
          "name": "Bottom Right"
        }
      }
    }
  }
}

Важно:

при таком дереве результирующие три правые зоны должны быть приблизительно одинаковой высоты.

Учитывать, что второй horizontal split делит оставшиеся 2/3 пополам.

---

# 49. LOGGING

logs/quad-desk.log.

Логировать:

- application start;
- monitor detected;
- layout selected;
- window snapped;
- window maximize interception;
- config error;
- Win32 error.

Не логировать:

- URL;
- текст браузера;
- clipboard;
- keyboard input;
- содержимое окон;
- персональные данные.

Ротация:

2–5 MB.

---

# 50. SECURITY

QuadDesk:

- не слушает TCP;
- не слушает UDP;
- не делает web requests;
- не имеет telemetry;
- не требует account;
- не требует admin;
- не внедряет DLL;
- не ставит drivers;
- не использует kernel code;
- не считывает keyboard content;
- не считывает clipboard;
- не читает содержимое приложений.

---

# 51. AUTOSTART

Portable.

Start with Windows:

создать shortcut в:

shell:startup.

При выключении:

удалить только собственный shortcut.

Не использовать registry Run без необходимости.

---

# 52. PROJECT STRUCTURE

Пример:

src/
  QuadDesk/

    Program.cs
    QuadDeskApplicationContext.cs

    Native/
      NativeMethods.cs
      WindowNative.cs
      MonitorNative.cs
      DisplayConfigNative.cs
      DwmNative.cs

    Core/
      QuadDeskController.cs
      MonitorManager.cs
      WindowManager.cs
      WindowTracker.cs
      HotkeyManager.cs
      SnapManager.cs
      ModeManager.cs
      RuleManager.cs
      WorkspaceManager.cs

    Layout/
      LayoutEngine.cs
      LayoutManager.cs
      LayoutSerializer.cs
      LayoutGeometry.cs
      ZoneNavigator.cs

    Layout/Models/
      Layout.cs
      LayoutNode.cs
      SplitNode.cs
      ZoneNode.cs
      Zone.cs

    Hooks/
      WinEventHookManager.cs

    Models/
      MonitorInfo.cs
      WindowRuntimeState.cs
      WindowRule.cs
      AppConfig.cs
      Workspace.cs

    Services/
      ConfigService.cs
      LogService.cs
      StartupService.cs

    UI/
      TrayContext.cs
      MainForm.cs
      MonitorSelectorForm.cs
      SettingsForm.cs
      RulesForm.cs
      LayoutEditorForm.cs
      LayoutPreviewControl.cs
      ZoneOverlayForm.cs

tests/
  QuadDesk.Tests/

layouts/

README.md
BUILD.md
LICENSE
build.ps1
publish.ps1

Не складывать всю бизнес-логику в:

Program.cs.

---

# 53. UNIT TESTS — LAYOUT

Обязательно проверить LayoutEngine.

## QUAD

3840×2160

→

4 × 1920×1080.

## MAIN + 3

3840×2160
65/35

ожидаемо:

Zone Main:
2496×2160.

Right:
1344 px width.

Zones 2–4:
примерно 1344×720.

Допуск на rounding:

±1 px.

Сумма размеров всегда должна точно покрывать target monitor.

Не должно быть:

- gap;
- overlap.

---

# 54. UNIT TESTS — COORDINATES

Monitor:

X = 2560
Y = 0
W = 3840
H = 2160.

Проверить zones.

Также:

X = -3840
Y = 0.

Также:

X = 0
Y = -2160.

Layouts должны корректно работать с negative virtual desktop coordinates.

---

# 55. UNIT TESTS — NAVIGATION

Проверить:

- Left;
- Right;
- Up;
- Down

для:

2×2;

Main + 3;

3 columns;

Six 3×2;

nested custom tree.

---

# 56. UNIT TESTS — RULES

Проверить:

process match;

class match;

titleContains;

manual override;

stable ZoneId.

---

# 57. UNIT TESTS — SERIALIZATION

Layout:

save
→ load

должен давать эквивалентное дерево.

Config:

save
→ load.

Workspace:

save
→ load.

---

# 58. MANUAL SMOKE TEST

Минимально проверить:

[ ] приложение запускается

[ ] tray работает

[ ] Haier определяется

[ ] основной монитор не затрагивается

[ ] Quad 2×2 работает

[ ] Main + 3 работает

[ ] Custom Layout работает

[ ] Vertical split работает

[ ] Horizontal split работает

[ ] Nested split работает

[ ] splitter можно двигать

[ ] layout сохраняется

[ ] layout загружается

[ ] Win+Alt+1

[ ] Win+Alt+2

[ ] Win+Alt+3

[ ] Win+Alt+4

[ ] drag snap

[ ] overlay

[ ] Chrome maximize within zone

[ ] VS Code maximize within zone

[ ] Terminal maximize within zone

[ ] double click titlebar

[ ] Win+Up

[ ] restore

[ ] multiple Chrome windows

[ ] disable QuadDesk

[ ] обычный fullscreen Haier после disable

[ ] re-enable

[ ] DPI 100%

[ ] DPI 125%

[ ] DPI 150%

[ ] HDMI disconnect

[ ] HDMI reconnect

[ ] config survives restart.

---

# 59. ОСОБЫЙ MANUAL TEST: MAIN + 3

Использовать:

Zone Main:
VS Code.

Zone 2:
ChatGPT.

Zone 3:
AI Agents Dashboard.

Zone 4:
Windows Terminal.

Проверить:

┌──────────────────────┬──────────────┐
│                      │   ChatGPT    │
│                      ├──────────────┤
│       VS CODE        │    Agents    │
│                      ├──────────────┤
│                      │   Terminal   │
└──────────────────────┴──────────────┘

Каждое приложение:

- snap;
- maximize;
- restore;
- focus;
- keyboard;
- mouse

должны работать штатно.

---

# 60. PRIORITIES

## P0

1. Monitor detection.
2. Stable target monitor.
3. Recursive LayoutEngine.
4. SplitNode / ZoneNode.
5. Built-in layouts.
6. Window → Zone.
7. Global hotkeys.
8. Correct SetWindowPos.
9. DPI.
10. tray.
11. config.
12. portable build.

## P1

13. Maximize within Zone.
14. Double-click maximize.
15. Win+Up.
16. Drag Snap.
17. Overlay.
18. Main + 3 preset.

## P2

19. Visual Layout Editor.
20. Draggable splitters.
21. Custom layouts.
22. AutoAssign rules.
23. Workspace.

Не жертвовать стабильностью P0/P1 ради красоты P2.

Но архитектура P0 должна заранее позволять P2 без переписывания ядра.

---

# 61. КРИТЕРИИ ГОТОВНОСТИ MVP

MVP считается готовым, если:

1. Windows продолжает видеть Haier как один физический монитор.
2. Нет VDD.
3. Нет OBS.
4. Нет custom display driver.
5. Нет GPU compositor.
6. LayoutEngine не привязан к 2×2.
7. Работает Quad 2×2.
8. Работает Main + 3.
9. Окно можно отправить в любую Zone.
10. Maximize ограничивается Zone.
11. Drag Snap работает.
12. Mouse работает штатно.
13. Основной монитор не затрагивается.
14. QuadDesk можно Disable.
15. После Disable Haier становится обычным 4K desktop.
16. CPU idle практически 0%.
17. GPU практически 0%.
18. Admin не требуется.
19. Приложение portable.
20. Есть Release build.

---

# 62. README

README обязательно на русском.

Минимальный onboarding:

1. Распаковать QuadDesk.
2. Запустить QuadDesk.exe.
3. Выбрать HAIER TV.
4. Выбрать Layout.
5. Готово.

Показать:

Main + 3:

┌──────────────────────┬──────────────┐
│                      │      2       │
│                      ├──────────────┤
│          1           │      3       │
│                      ├──────────────┤
│                      │      4       │
└──────────────────────┴──────────────┘

Объяснить:

это не четыре настоящих Windows monitors.

Это логические зоны одного физического дисплея.

Для игр можно:

Disable QuadDesk

и использовать весь физический экран.

---

# 63. LICENSE

MIT License.

Не использовать платные библиотеки.

Не использовать библиотеки с лицензиями, несовместимыми с MIT.

---

# 64. BUILD

Сначала определить доступный SDK.

Затем реально выполнить:

dotnet restore

dotnet build

dotnet test

dotnet publish

Target:

win-x64.

Configuration:

Release.

Publish:

self-contained.

SingleFile:

true.

Если trimming создаёт риск для WinForms/PInvoke:

не использовать trimming.

Надёжность важнее размера EXE.

---

# 65. CODE REVIEW

Перед выпуском отдельно проверить:

- P/Invoke signatures;
- native integer sizes;
- HWND lifetime;
- WinEvent delegate lifetime;
- delegate GC pinning;
- hooks cleanup;
- RegisterHotKey cleanup;
- SetWindowPos flags;
- DWM bounds;
- DPI;
- negative coordinates;
- integer rounding;
- split-tree recursion;
- recursive layout serialization;
- stale HWND;
- race conditions;
- monitor reconnect;
- config corruption;
- bad layout file.

Исправить найденные проблемы.

---

# 66. FAILURE SAFETY

Если config.json повреждён:

не падать.

Сделать backup:

config.json.bad.timestamp

и загрузить безопасные defaults.

Если layout повреждён:

не падать.

Отключить только этот layout.

Не потерять остальные layouts.

---

# 67. OUTPUT

Создать:

QuadDesk-v0.1.0.zip

Внутри минимум:

QuadDesk.exe
config.json
layouts/
README.md
LICENSE

src/
tests/

build.ps1
publish.ps1.

---

# 68. FINAL REPORT

После завершения дать:

## Реализовано

...

## Layout Engine

...

## Maximize within Zone

...

## Проверено

...

## Производительность

Idle CPU:
...

RAM:
...

GPU:
...

## Известные ограничения

...

## Build

QuadDesk.exe
...

## Archive

QuadDesk-v0.1.0.zip

SHA256:
...

Дать ссылку на готовый ZIP.

---

# 69. ПОРЯДОК ВЫПОЛНЕНИЯ

Не тратить время на длинное предварительное обсуждение.

Работать:

DISCOVERY
→ PLAN
→ IMPLEMENT
→ BUILD
→ TEST
→ CODE REVIEW
→ FIX
→ PACKAGE.

Не спрашивать пользователя о мелких инженерных решениях.

Если есть несколько решений:

предпочитать:

1. native Windows mechanism;
2. надёжность;
3. простоту;
4. низкую нагрузку;
5. минимум зависимостей.

---

# 70. ОСОБО ВАЖНО

Три фундаментальных требования проекта:

## A. FLEXIBLE LAYOUTS

QuadDesk не должен считать, что всегда существует четыре одинаковых зоны.

Количество зон, их размер и вложенность произвольны.

## B. MAXIMIZE WITHIN ZONE

Обычная кнопка Windows Maximize должна вести себя максимально близко к отдельному монитору, но внутри Zone.

## C. ZERO DISPLAY VIRTUALIZATION

Никаких виртуальных мониторов, OBS и framebuffer composition.

Пользователь должен видеть в Windows обычный физический Haier, внутри которого QuadDesk организует рабочее пространство.

Начинай реализацию сейчас.
