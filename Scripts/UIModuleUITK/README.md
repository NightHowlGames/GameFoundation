# UI Toolkit screen backend

The UI Toolkit half of the screen flow. It sits behind the same `IScreenManager` the
uGUI screens use — `OpenScreen<TPresenter>()` and `OpenScreen<TPresenter, TModel>(model)`
open a UI Toolkit screen exactly the way they open a uGUI one — and it is entirely
opt-in: a project that does not register it gets the uGUI path and nothing else, byte
for byte.

## How the manager picks a backend

`ScreenManager` decides per presenter, off the **view type**:

1. `ScreenPresenterViewType.Of(presenterType)` recovers `TView` from the generic base
   `BaseScreenPresenterCore<TView>`. A presenter that does not derive from it resolves
   to `null`.
2. `IScreenViewBackend.CanHandle(viewType)` is asked. `UIToolkitScreenViewBackend`
   answers yes for anything implementing `ISurfaceScreenView`.
3. No backend, no view type, or a "no" — the unchanged uGUI path: load a prefab,
   `Instantiate` it into its layer, `GetComponent<IScreenView>()`.

Nothing new is written on a screen to make this work: no attribute, no registration per
screen, no interface member. Deriving from `BaseUIToolkitScreenPresenter<,>` or
`BaseUIToolkitPopupPresenter<,>` is the whole declaration.

## What a project has to do

Three things, none of them per screen.

**1. Register the backend**, on the same scope as the screen manager:

```csharp
builder.RegisterScreenManager();
builder.RegisterUIToolkitViewBackend();   // GameFoundation.Scripts.UIModule.UITK
```

**2. Put a `RootUIDocument` in the scene** — the counterpart of `RootUICanvas`. A
GameObject with a `UIDocument` and a `RootUIDocument` on it, with `PanelSettings`
assigned and `Managers/RootUIDocument.uxml` (shipped here) as the source asset. That
UXML carries the three layers the component resolves by name:

| Layer | Element name | Holds |
|---|---|---|
| Show | `root-ui-show` | the open, non-overlay screen |
| Closed | `root-ui-closed` | closed and hidden screens |
| Overlay | `root-ui-overlay` | popups declared `isOverlay: true` |

A custom root UXML is fine; either keep those names or set them on the component.

**3. Address the `VisualTreeAsset`** under the key in the screen's `[ScreenInfo]` /
`[PopupInfo]` attribute. Same key mechanism as uGUI, different asset behind it — the
backend loads a `VisualTreeAsset`, not a prefab, and builds the view by calling its
`(VisualTreeAsset)` constructor. A view with no such constructor is reported by name.

## Writing a screen

```csharp
public class MyPopupView : BaseUIToolkitView
{
    public Label Title { get; }

    public MyPopupView(VisualTreeAsset asset) : base(asset)
    {
        this.Title = this.Root.Q<Label>("title");
    }
}

[PopupInfo("MyPopupUxmlAddress", isOverlay: true)]
public class MyPopupPresenter : BaseUIToolkitPopupPresenter<MyPopupView, MyPopupModel>
{
    public MyPopupPresenter(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager) { }

    public override UniTask BindData(MyPopupModel model)
    {
        this.View.Title.text = model.Title;
        return UniTask.CompletedTask;
    }
}
```

`NotificationPopupUIToolkitView` / `NotificationPopupUIToolkitPresenter` are the worked
example, alongside the uGUI popup they were ported from.

## What is deliberately not here

- **Transform members.** `SetViewParent(Transform)`, `GetViewParent`,
  `CurrentTransform` and `ViewSiblingIndex` throw `NotSupportedException` on a UI Toolkit
  presenter: a `VisualElement` has no `Transform`. Use `SetViewParent(IViewLayer)` or
  `ViewSurface`.
- **`ManualInitScreenSignal`.** It means "find the view already sitting in the
  RootUICanvas hierarchy"; a UI Toolkit view is not in that hierarchy. The manager logs
  and returns rather than reporting a missing GameObject.
- **Transitions.** `PlayIntroAnim` / `PlayOutroAnim` are virtual hooks that complete
  immediately. The uGUI `UIScreenTransition` is a MonoBehaviour and has no analogue yet.

## Tests

`Tests/Runtime` (PlayMode) opens the notification popup through the real
`ScreenManager`, the real backend and a real `RootUIDocument`, stubbing only
Addressables and audio. PlayMode rather than EditMode because the manager builds
presenters through `GetCurrentContainer()`, which needs a live `SceneScope`, and a
`LifetimeScope` only builds its container in `Awake`.
