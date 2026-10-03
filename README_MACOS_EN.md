# AppuruPie (苹果派) · The Cross-Platform Rework, on macOS

> This file describes **this fork's own state**: taking the Windows mouse-gesture wheel and rebuilding it as a cross-platform product that actually runs on Apple Silicon macOS.
> For the upstream Windows product, read [README.md](README.md) / [README_EN.md](README_EN.md).
> The evidence behind everything below lives in [`ARCHITECTURE.md`](ARCHITECTURE.md) (architecture & progress snapshot), [`PORTING_FEASIBILITY.md`](PORTING_FEASIBILITY.md) (12 feasibility questions) and [`PORTING_REFERENCE.md`](PORTING_REFERENCE.md) (capability & license audit).

**中文：** [README_MACOS.md](README_MACOS.md)

---

## In one line

**One copy of the business logic: WPF keeps running it on Windows, while macOS runs it on Avalonia plus a native `CGEventTap` — and the legacy project was not touched at all.**

AppuruPie is not "translate the WPF app into Avalonia". The product logic — wheel, gestures, configuration, actions — is sunk into a platform-neutral `StarPie.Core`, so the macOS side only rewrites the layer that talks to the operating system. The Windows WPF project references **the same files on disk** through `<Compile Link>`, so shared logic is not kept in sync by copy-paste.

---

## What runs today (measured on this machine, 2026-10-03)

| Gate | Command | Measured result |
| :--- | :--- | :--- |
| Build | `dotnet build StarPie.sln -c Release` | **0 warnings, 0 errors** |
| End-to-end self-test | `dotnet src/StarPie.App/bin/Release/net8.0/StarPie.dll --selftest` | **144 assertions PASS → `SELFTEST APPROVED`** (rc=0) |
| Union guard | `python3 tools/check_system_presets.py` | `[OK]` (63 Windows case labels ↔ 63 Core ↔ 42 enum members ↔ 42 macOS entries, all registered) |
| Packaging | `sh tools/bundle-macos.sh` | Produces `AppuruPie.app/`, ad-hoc signed, `codesign --verify --deep --strict` rc=0 |
| Legacy regression surface | `git status WinPieGestures plugin/sdk` | **No modified lines** |

Scale (the first two rows were measured just now; the last two come from the measurement baseline in `PORTING_FEASIBILITY.md` — I did not independently re-derive the de-duplication):

| Metric | Value |
| :--- | :--- |
| `StarPie.Core` compilation units | **69 files / 12,658 lines** (56 linked from `WinPieGestures/` and the plugin SDK, 13 owned by Core) |
| Native bridge exports | `libstarpie_macos.dylib`: **60 `starpie_*` exports** measured (input + system + overlay policy) |
| Win32 surface to replace | **79 unique entry points** (from 184 `[DllImport]` declarations across 25 files) |
| UI rewrite scope | 20 `*.xaml.cs` / 33,542 lines (rebuilt on Avalonia, not translated XAML-by-XAML) |

### What already works on macOS

- **The wheel itself**: `CGEventTap` filter-tap interception → polar hit-test → Avalonia transparent overlay → execute on release; 4 / 8 / 12 sectors, per-layer multi-level wheels, wheel-scroll layer switching with a layer badge;
- **Four cut-shape families**: `Original` (with gap inset + corner-radius clamping) / `Circle` / `HexagonHive` / the capsule family, every constant copied from Windows, all emitted as vertex polylines;
- **Themes & colors**: seven theme paths plus custom presets, matched value-by-value (including two shipped Windows quirks, see below);
- **Trail gestures**: sampling threshold, 8-direction quantization, `Auto` opposite-side hint and two-stage clamping all live in Core where they can be asserted; only the drawing lives in UI;
- **Actions**: hotkey injection, launch app, open URL / folder, run command, taskbar window slots, 42 system-control presets;
- **OCR engine**: Vision `VNRecognizeTextRequest` end to end (PNG in, JSON out), 33 languages read from the engine at runtime, one round trip each for Chinese and English;
- **Resident form**: `LSUIElement` menu-bar app, tray menu, autostart via `SMAppService`, single-instance socket handoff (a second instance opens the primary's settings window).

---

## What actually distinguishes this project: making "cross-platform" falsifiable

Cross-platform reworks usually die by treating "wired up" as "verified". Three things push back here:

### 1. Every native boundary ships with a gauge that can turn red

The 144 `--selftest` assertions are not "it runs without throwing" — they come with **mutation testing**: deliberately break the implementation, the assertion must report red. Real cases from this round:

- Swap x / y in `SetPosition` ⇒ `FAIL … 588.4,779.7 → 779.0,588.0`;
- Route `"toggletopmost"` to `WindowSwitch` ⇒ the dispatch assertion reports `ToggleTopmost→WindowSwitch (expected WindowTopmost)`;
- Remove the y-flip in OCR results ⇒ the **first version of the gauge stayed green**, because a flipped y is still inside `[0, image height]`. After switching to a direction-sensitive ordering gauge (the upper line's y must be strictly less than the lower line's), that same mutation reports red immediately.

> The lesson is written into the docs: **a gauge must have the same shape as the defect it claims to prevent** — "within range" cannot catch "direction reversed"; and expected values must not be recomputed by calling the function under test (a self-referential gauge caught only 1 of 4 mutations).

### 2. A union guard: no existing user configuration may silently stop working

Windows' `ExecuteSystem` has 63 case labels (stable IDs + synonym spellings + **legacy Chinese values**).
`tools/check_system_presets.py` reads that label set **from the Windows source file at runtime**, compares it against the Core table in both directions, and additionally asserts that `KnownLabels` and the `switch` branches are the same set — two sources of truth drift, sooner or later.
One missing entry means some sector in some user's config quietly stops firing on macOS.

### 3. No silent degradation

Anything the platform cannot do must fail **by name**, not with a generic Unsupported:

- `ITrayService.ClickEventsSupported = false` (macOS does not dispatch tray clicks);
- `PlatformFailure.PermissionRequired` (Accessibility / Input Monitoring / Screen Recording missing);
- `PlatformFailure.NotSupported` (macOS has no equivalent of Windows elevated-launch semantics);
- Screen capture is not wired yet, so the OCR action now answers explicitly: "engine available, ScreenCaptureKit missing".

The reason is mundane: when a background tool fails silently, users report it as a bug — repeatedly.

---

## Three design constraints on the abstraction layer

- **R-A — interfaces express product needs, not Win32 wrappers.** Anti-examples: `GetForegroundWindow`, `SetWindowPos`. Positive examples: `IWindowService.ActiveWindow`, `MoveTo(target, topLeft)`. Every member traces back to a real call site in the existing code.
- **R-B — hot-path semantics belong in the contract, not in "later".** `ArmTrigger` / `ReturnInterceptedPress` / `DiscardInterceptedPress`, `BeginExclusiveModifierRecording`, `PermissionLost + StateChanged` (revoking permission on macOS does **not** interrupt an installed tap — you have to notice it yourself); `IMouseService.SetPosition` explicitly requires an implementation that "produces no move event", otherwise the wheel drives itself with its own repositioning.
- **R-C — platform differences surface as capability bits.** See the previous section.

Stylistically this is not a new invention: the plugin SDK already evolved to 1.8 with the same shape of semantic interfaces (`IHostActionInvoker` / `IHostWindowService` / `IHostWheelService` + capability gates), so the platform layer grows in that image and plugin contracts can map straight onto platform-service injection.

---

## macOS-only traps (all measured on this machine, not copied from a list)

| Fact | Consequence & fix |
| :--- | :--- |
| `CGWarpMouseCursorPosition` **injects no move events into the tap** (50 in-place warps → `tapCallbacks` delta = 0) | The original assertion said "≤2 events after warp" and was in fact measuring **the user's hand** — it turned red at random while the user moved the mouse. Replaced with "coordinate round-trip ≤1px and finite" |
| A `NativeMenu` exported to a `TrayIcon` **cannot be replaced by a second one** | `open AppuruPie.app` SIGABRTs within 1 second (`AvaloniaNativeMenuExporter.SetMenu`). Fix: mutate `NativeMenu.Items` only, or rebuild the `TrayIcon` together with the menu |
| The output directory **must carry the `.app` suffix** | Without it, `open` means "open a folder": `lsregister -f` returns `-10811`, `open` returns 0 and no process exists |
| `File.Exists` is **always false for a `.app`** (on Unix it is a directory) | Judging bundle presence that way reports Calculator / Activity Monitor / Finder all as "path missing". Switched to `Directory.Exists` + `Contents/Info.plist` |
| A `.mm` export surface must be wrapped in one `extern "C"` block | Without it the symbol becomes `__Z21starpie_ocr_languagesPci`, `dotnet build` stays green, and only the runtime P/Invoke fails to find the entry point. Gauge: `nm -gU` must show a bare name without `__Z` |
| Creating an `NSPanel` synchronously inside a run-loop source callback **recursively deadlocks the runloop** | The tap callback does pure math (polar hit-test) only; every UI action goes through `Dispatcher.UIThread.Post` |
| Measured cross-language callback cost: 2.12µs mean / 176µs worst per event | At most one redraw per frame: hover writes a single integer, an 8ms timer picks up the change and then `InvalidateVisual` — otherwise the UI thread is held hostage by mouse frequency |
| `Path.GetFileNameWithoutExtension` does **not** treat `\` as a separator on Unix | A Windows path like `C:\VS\devenv.exe` becomes one giant file name ⇒ per-app profile matching fails silently and the user says "but I did configure a wheel for it" |
| Only apps with `activationPolicy == Regular` count as window slots; Dock items that are pinned but not running are invisible | Different gauge from the Windows taskbar: the same config index can point at a different app on the two platforms. Recorded in a code comment and in the audit table rather than marked as done |
| Local SDK facts: `VNRecognizedTextObservation` only has `topCandidates:` (plural); `CGDisplayCreateImage` is marked obsoleted in macOS 15 | Writing these APIs from memory gets rejected by the compiler on the spot; the capture half needs ScreenCaptureKit |

---

## Getting started

```bash
export PATH="/opt/homebrew/bin:$PATH"

# 1. Build (the native dylib is built incrementally by the csproj's BuildNativeBridge target,
#    which Windows skips entirely)
dotnet build StarPie.sln -c Release

# 2. Headless gate: 144 assertions
dotnet src/StarPie.App/bin/Release/net8.0/StarPie.dll --selftest

# 3. Produce the .app bundle (ad-hoc signed)
sh tools/bundle-macos.sh

# 4. Run it
open AppuruPie.app                    # menu-bar resident
open AppuruPie.app --args --settings  # settings window (currently the "Appearance & Shape + trigger key" page)

# 5. Install it as a normal Mac app (optional)
sh tools/install-macos.sh             # falls back to ~/Applications when /Applications is not writable; then ⌘+Space → "AppuruPie"
```

The default summon combo is **Ctrl + left button** — deliberately not the right button, so it coexists with other gesture software (`SettingsWindow.cs:59`; switchable to right / middle / side button, with optional modifiers).
Once installed in `/Applications` it has neither a Dock icon nor a main window (`LSUIElement`): the apple-pie menu-bar icon is the only resident entry point. If that icon is hard to find, launch it from a terminal again — the second instance does not grab the trigger keys, it only asks the running one to open its settings window.

The first run needs **Accessibility / Input Monitoring** granted in System Settings → Privacy & Security. When a permission is missing the app reports `PermissionRequired` and says what to click next; it never pretends to be working.

Run modes: `--selftest` (end-to-end assertions), `--settings` (settings page; closing the window exits), `--gui` (visible wheel), `--tray` (resident form), `--watch` (headless chain), `--warp-probe` (measure whether warp injects events), `--config <file> [--app <bundle id>]` (demonstrate the configuration chain and per-app matching).

---

## Porting discipline

- **R-1 don't touch the legacy project**: this Mac cannot compile the `net8.0-windows` WPF project, so Windows behaviour is guaranteed precisely by not having changed it. New and old projects coexist, connected by the same Core source files.
- **R-2 platform checks live only in `StarPie.Platform.*`**: no `#if WINDOWS` / `MACOS` and no `OperatingSystem.IsMacOS()` in Core or UI.
- **R-3 every native boundary gets a falsification test**: the A/B halves of `spikes/macos-input` are the template — with the marker it must count 0, without it all 300, and half of that is the same as not testing.
- **R-4 reuse passes the license gate first**: see the last section.
- **R-5 the upstream red lines do not relax because the framework changed**: under 16ms from right-button press to rendered wheel, no IO on the callback thread, polar hit-testing with no Euclidean distance (macOS coordinates are also "points, top-left origin, Y down", so the `atan2` convention is unchanged).

---

## Current boundaries (explicitly not done — do not read these as done)

- **The settings UI has one cell only**: "Appearance & Shape + trigger key" reads and writes the real `config.json`, and the right-hand pane embeds the **same `WheelControl` used at runtime** (the preview is what the wheel will draw, not a mock-up). **Not done**: gestures & actions page, triggers & scenes page, advanced & system page, and the i18n terms shared by all four pages.
- **Screen capture not wired**: the OCR engine works; the `ScreenCaptureKit` half is missing.
- **Overlay visuals unverified on device**: the code is in the product (it logs the window level / collection it reads back), but "above the fullscreen Space without stealing focus" needs human eyes.
- **`StarPie.Platform.Windows` does not exist yet**: Core / UI depend only on abstractions so nothing blocks, but the Windows backend is still the legacy WPF app.
- **Config migration is an open product question**: real user configs contain Windows paths such as `G:\剪映\...` as action parameters, which cannot launch on macOS — the failure is explicit, but "how do we help users replace these" has no answer yet.
- **Ad-hoc signing only on this machine**: Developer ID and notarization need a certificate.
- **One documented spec contradicts the code**: `AGENTS.md` §3.1 states $\theta_i = -\pi/2 + i\Delta\theta$, which does not match the implementation — sector 0 is due **east** (settled from `GestureController.cs:1069-1076` and `:2310-2312`, verified against 72 sample points). The document is what should change.
- **Pending authorization for reshuffles U1–U5**: four "pure type parked in the wrong file" moves (`PluginEventService`, the `SoundType` enum, the `ProgramItem` DTO, moving the `ICollectionView` view models into UI) would bring roughly 14k lines into Core, but each one edits `WinPieGestures/` and therefore conflicts with R-1 — the maintainer has to say yes.

---

## On naming

This project is **AppuruPie (苹果派)**; the upstream Windows product is called `StarPie`. The following **runtime literal identifiers** stay as they are, because renaming them would turn the docs into a false description or force users to re-grant permissions:

`CFBundleIdentifier=com.starpie.menubar`, `CFBundleExecutable=StarPie`, `libstarpie_macos.dylib`,
`~/Library/Application Support/StarPie/` (log and plugin data root), `StarPie.Plugin.Abstractions`.

The brand layer (`CFBundleName` / `CFBundleDisplayName` / permission prompt copy / tray menu) already says AppuruPie.

---

## License & third parties

- The upstream baseline and this fork's terms: see [LICENSE](LICENSE) and the license section of [README.md](README.md).
- Reuse follows the eight landing rules in `PORTING_REFERENCE.md` §3.3, registered file-by-file in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) (source repo / commit / path / SPDX / original copyright).
- One audit finding that changes the plan: **Kando is an Electron app with an MIT ObjC++ N-API addon and has no `CGEventTap` on macOS at all** (its global trigger is a keyboard shortcut only; no event interception or replay). So "reuse Kando's proven macOS input backend" cannot be executed literally — what is legally reusable is the injection recipe, window enumeration, AX raise, the key-code table, the gesture algorithm and a list of pitfalls; the **intercepting, replaying, self-reviving filter-tap has to be built here**.
- GPL / CC-BY-NC / custom-licensed projects (AltTab, MiddleClick, Mos, Mac Mouse Fix) are **design references only**; no code is copied.

---

## Related documents

| File | What it owns |
| :--- | :--- |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Real layout, abstraction constraints, progress snapshot, ordered next steps |
| [`PORTING_FEASIBILITY.md`](PORTING_FEASIBILITY.md) | The 12 feasibility questions: what is reusable / must be rewritten / can only degrade / is not worth migrating yet |
| [`PORTING_REFERENCE.md`](PORTING_REFERENCE.md) | Platform capability table, Kando & Avalonia audits, license audit, on-device spike numbers |
| [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) | Per-file reuse registry |
| [`AGENTS.md`](AGENTS.md) | Upstream architecture and engineering red lines (not relaxed by the framework change) |
| [`plugin/README.md`](plugin/README.md) | Plugin SDK and host service contracts |
