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

## Collections — the UI Toolkit side of the scroll/list layer

`Scripts/UIModuleUITK/Collections/` holds UI Toolkit counterparts of the three OSA
adapters in `Scripts/UIModule/Adapter/`. **The OSA adapters and the OSA plugin stay
exactly where they are** — six repositories and `com.gdk.3rd` compile against this
package, and dropping the dependency is a separate decision for after nothing uses it.
These are an alternative, not a replacement.

| OSA (`Scripts/UIModule/Adapter/`) | UI Toolkit (`Scripts/UIModuleUITK/Collections/`) | Built on |
|---|---|---|
| `BasicListAdapter` | `UIToolkitListAdapter` | `ListView` |
| `BasicGridAdapter` | `UIToolkitGridAdapter` | `ListView` of rows |
| `MultiplePrefabsListAdapter` | `UIToolkitMultiTemplateListAdapter` | `ListView` + per-template pools |

The MVP layer is mirrored rather than reused: `BaseUIItemPresenter<TView>` is declared
`where TView : MonoBehaviour`, and a `VisualElement` is not one. `BaseUIToolkitItemView`
/ `BaseUIToolkitItemPresenter<TView, TModel>` keep the same member names and the same
`SetView` → `OnViewReady` → `BindData` order, so a presenter ports across by changing
its base class and its element lookups.

```csharp
public class InventoryRowView : BaseUIToolkitItemView
{
    public Label Name { get; }

    public InventoryRowView(VisualElement root) : base(root) { this.Name = root.Q<Label>("name"); }
}

public class InventoryRowPresenter : BaseUIToolkitItemPresenter<InventoryRowView, InventoryRowModel>
{
    public override void BindData(InventoryRowModel model) { this.View.Name.text = model.Name; }
}

var adapter = new UIToolkitListAdapter<InventoryRowModel, InventoryRowView, InventoryRowPresenter>(
    this.View.Root.Q<ListView>("rows"), this.rowTemplate, fixedItemHeight: 64f);

adapter.SetItems(models);
```

An adapter takes an `IDependencyContainer` as its last constructor argument; leave it
null and it resolves the current `SceneScope`'s container lazily, which is what the OSA
adapters do in `Awake`. Passing one is what makes them testable without a scene.

### The two decisions worth knowing about

**Row height is `FixedHeight`, not `DynamicHeight`.** `UIToolkitListAdapter` and
`UIToolkitGridAdapter` set `CollectionVirtualizationMethod.FixedHeight` whenever a
non-zero height is passed, which is the recommended call. `DynamicHeight` measures every
row as it binds and rebinds after layout resolves — at least two binds per appearance —
and is the path with the scroll-jump history. Pass `0` to leave the control's authored
settings alone; that is the escape hatch for a genuinely variable-height list, taken by
the caller rather than by default.

`UIToolkitMultiTemplateListAdapter` is the deliberate exception and defaults to
`DynamicHeight`, because rows of different templates have different heights and
`FixedHeight` can express exactly one. The uGUI original says the same thing in its own
terms — it calls `RequestChangeItemSizeAndUpdateLayout` per item out of
`InitItemAdapter`. Pass a `fixedItemHeight` to get the fast path back when every
template really does share a height.

**Heterogeneous rows use an empty shell plus per-template pools.** A `ListView` has
exactly one `makeItem`, so the three readings of "different rows in one list" were
weighed:

- *A superset element with toggled `display`* — rejected. The cost is
  templates × pooled elements, so a fourth row type makes every existing row heavier
  whether or not it is ever that type, hidden subtrees still take part in style
  resolution, and every presenter for every type is built for every element.
- *`TreeView`* — rejected, and it is the suggestion to be most careful about because it
  sounds right. `TreeView` models hierarchy (expand state, indentation, `TreeViewItemData`
  ids). It has the same single `makeItem`, so it does not address heterogeneity at all.
- *An empty shell whose child is swapped from a per-template pool* — chosen. Cost is
  bounded by what is on screen, and scrolling through a run of same-template rows never
  touches the pools. This is exactly the policy `MultiplePrefabsListAdapter.IsRecyclable`
  expresses in OSA; OSA can put it in the library because its recycler asks which items
  are interchangeable, and `ListView`'s does not, so the pool moves into the adapter.

### Lifetime

`Dispose` happens once per presenter, in `destroyItem` — not on every rebind, which is
what the OSA adapters do (they call `presenter.Dispose()` and then keep using it).
Release per-bind subscriptions at the top of `BindData`. Disposing the adapter disposes
every presenter it built and unhooks it from the control.

### Tests

`Tests/Runtime/Collections/` — 28 PlayMode tests. They build a real `UIDocument` with a
sized viewport, because virtualization is a consequence of layout: a test that never
lays out would assert over an empty list and pass. Every panel test therefore also
asserts that something *was* realized. Covered: 500 rows realizing ~12 elements,
scrolling rebinding those elements without allocating more, grid rows hiding the
trailing partial row's padding cells, template pooling across alternating rows, and the
negative paths — empty and null sources, a shrunken source, unknown template names,
wrong view and presenter types, missing constructors, and use after `Dispose`.

One known limitation of the harness, not of the adapters: in a headless test panel the
`ScrollView` clamps its scrollable range to the realized rows rather than to
`itemsSource.Count * fixedItemHeight`, so a jump to row 400 lands a few rows down. The
scroll test therefore asserts that the bound window advanced and that elements were
rebound, rather than asserting a destination.

## Tests

`Tests/Runtime` (PlayMode) opens the notification popup through the real
`ScreenManager`, the real backend and a real `RootUIDocument`, stubbing only
Addressables and audio. PlayMode rather than EditMode because the manager builds
presenters through `GetCurrentContainer()`, which needs a live `SceneScope`, and a
`LifetimeScope` only builds its container in `Awake`.
