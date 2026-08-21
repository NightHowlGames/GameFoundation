# Changelog

Changes made to **this fork** of GameFoundation, relative to upstream
`GameDevelopmentKit/GameFoundation`.

This file starts at the UI Toolkit work and does not attempt to reconstruct the history
before it. It exists because the fork's divergence from upstream is permanent and one-way,
and nothing else records what we changed — which is the expensive part of maintaining a
fork, not the changes themselves.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- **A UI Toolkit screen backend, selected per presenter type.** `ScreenManager` gained a
  second construction path behind `IScreenViewBackend`; a presenter opts in by deriving
  from `BaseUIToolkitScreenPresenter` / `BaseUIToolkitPopupPresenter` instead of the uGUI
  bases. The uGUI/prefab path is untouched and permanent — it remains the right technology
  for world-space UI, HP bars, damage numbers, floating text and anything driven by
  `Animator`, DOTween or `ParticleSystem` (see `docs/UI-ARCHITECTURE.md` in the client).
- **A view-surface seam.** `IScreenViewBase` splits the backend-neutral view lifecycle out
  of `IScreenView`, which keeps `RectTransform`; `ISurfaceScreenView` adds a backend-neutral
  `IViewSurface`. This is what lets one `ScreenManager` drive two backends without either
  knowing about the other.
- **`IScreenManager` surface for back navigation**: `EnableBackToClose`,
  `IsBackToCloseEnabled`, `ActiveScreenCount`, `HandleBackNavigation`.
- **UI Toolkit templates in `ViewCreatorWizard`**, behind a Backend dropdown (uGUI |
  UIToolkit), emitting a view/presenter pair plus a starter `.uxml` instead of a prefab.

### Changed

- **The UI Toolkit runtime moved out to `com.cuvara.uitoolkit`** (`Cuvara/UIToolkit`), a
  standalone package that depends on nothing here. What remains in this fork is the
  adapter: `UIToolkitScreenViewBackend`, `UIToolkitBackNavigation`, the two presenter bases
  and the notification popup presenter. The dependency runs one way — this package
  references the new one, never the reverse.

### Fixed

- **`ScreenManager.Tick` threw once per frame on any project using the new Input System
  alone.** The condition evaluated `Input.GetKeyDown` *before* the `enableBackToClose`
  short-circuit, and with Active Input Handling set to "Input System Package (New)"
  (`activeInputHandler: 1`) the legacy `UnityEngine.Input` API throws
  `InvalidOperationException` rather than returning false. The poll is now behind
  `#if ENABLE_LEGACY_INPUT_MANAGER`; a project on legacy or "Both" input keeps the original
  path byte-for-byte. The throw was latent in `IndieRPGMMOAdventure`, which does not call
  `RegisterGameFoundation`, and live in the other consumers of this package.
- **The back/escape flow was unreachable.** `EnableBackToClose` was public on
  `ScreenManager` but absent from `IScreenManager`, which is what every consumer resolves,
  so `enableBackToClose` had always been false and no call site existed anywhere.
- **`ViewCreatorWizard` emitted code that does not compile**, in four independent ways, each
  predating a migration that landed here and was never followed through to the templates:
  `using Zenject;` after Zenject was removed, `ILogService` from a namespace that no longer
  exists, a `void BindData` override against an abstract member returning `UniTask`, and
  base-constructor calls matching no existing constructor. `[Preserve]` is now emitted on
  every generated constructor, since presenters are resolved reflectively and are otherwise
  stripper bait on IL2CPP.
- **Every `.cs.meta` under the UI Toolkit tree carried only `fileFormatVersion` and `guid`**,
  with no `MonoImporter` block, and five assets had no `.meta` at all. A missing `.meta`
  does not fail a build here — it fails a *consumer's* whole test suite, because Unity logs
  an Error and the test framework turns an unexpected log error into an exception.
