namespace GameFoundation.Scripts.UIModule.ScreenFlow.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Cysharp.Threading.Tasks;
    using GameFoundation.DI;
    using GameFoundation.Scripts.UIModule.CommonScreen;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Scripts.UIModule.ScreenFlow.Signals;
    using GameFoundation.Signals;
    using R3;
    using UniT.Logging;
    using UniT.ResourceManagement;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Scripting;
    using IInitializable = GameFoundation.DI.IInitializable;
    using ILogger = UniT.Logging.ILogger;
    using ITickable = GameFoundation.DI.ITickable;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Control open and close flow of all screens
    /// </summary>
    public interface IScreenManager
    {
        /// <summary>
        /// Current screen shown on top.
        /// </summary>
        public ReactiveProperty<IScreenPresenter> CurrentActiveScreen { get; }

        public ReactiveProperty<IScreenPresenter> RootScreen { get; }

        public ReactiveProperty<IScreenPresenter> RootPopup { get; }

        /// <summary>
        /// Get root canvas of all screen, use to disable UI for creative purpose
        /// </summary>
        public RootUICanvas RootUICanvas { get; }

        /// <summary>
        /// Get root transform of all screen, used as the parent transform of each screen
        /// </summary>
        public Transform CurrentRootScreen { get; }

        public Transform CurrentHiddenRoot { get; }

        /// <summary>
        /// Get overlay transform
        /// </summary>
        public Transform CurrentOverlayRoot { get; }

        /// <summary>The same three roots as <see cref="IViewLayer"/>, for backend-agnostic callers.</summary>
        public IViewLayer ScreenLayer  { get; }

        public IViewLayer HiddenLayer  { get; }

        public IViewLayer OverlayLayer { get; }

        /// <summary>
        /// Get instance of a screen
        /// </summary>
        /// <typeparam name="TPresenter">Type of screen presenter</typeparam>
        public UniTask<TPresenter> GetScreen<TPresenter>() where TPresenter : IScreenPresenter;

        public UniTask<IScreenPresenter> GetScreen(Type presenterType);

        /// <summary>
        /// Open a screen by type
        /// </summary>
        /// <typeparam name="TPresenter">Type of screen presenter</typeparam>
        public UniTask<TPresenter> OpenScreen<TPresenter>() where TPresenter : IScreenPresenter;

        public UniTask<TPresenter> OpenScreen<TPresenter, TModel>(TModel model) where TPresenter : IScreenPresenter<TModel>;

        /// <summary>
        /// Close a screen on top
        /// </summary>
        public UniTask CloseCurrentScreen();

        /// <summary>
        /// Close to a screen in queue
        /// </summary>
        public UniTask CloseAllLastOverlayScreenAsync();

        /// <summary>
        /// Close all screen on current scene
        /// </summary>
        public void CloseAllScreen();

        /// <summary>
        /// Close all screen on current scene async
        /// </summary>
        public UniTask CloseAllScreenAsync();

        /// <summary>
        /// Cleanup/ destroy all screen on current scene
        /// </summary>
        public void CleanUpAllScreen();

        /// <summary>
        /// How many screens are currently open, in either backend.
        /// </summary>
        /// <remarks>
        /// Added for the back-navigation flow, which has to answer exactly one question —
        /// "is there a screen underneath the one on top?" — and had no way to ask it: the
        /// list it needs is <c>ScreenManager</c>'s private <c>activeScreens</c>, and
        /// <see cref="CurrentActiveScreen"/> tells you what is on top, not how deep the
        /// stack is. <c>ScreenManager.Tick</c> reached straight into the private field
        /// because it lives in the class; the UI Toolkit back handler does not, and
        /// re-deriving the answer from the reactive properties would be a guess rather than
        /// the same number.
        ///
        /// <para>Read-only, and additive: <c>ScreenManager</c> is the only implementer of
        /// this interface in the package, in <c>com.gdk.3rd</c>, or in the consuming
        /// project, so nothing else has to grow a member.</para>
        /// </remarks>
        public int ActiveScreenCount { get; }

        /// <summary>
        /// Enables the back/escape flow: close the top screen, or offer to quit at the root.
        /// </summary>
        /// <remarks>
        /// Was public on <c>ScreenManager</c> only, and therefore unreachable through the
        /// interface every consumer actually holds — which is a large part of why the flow
        /// has never been switched on anywhere. See the BackToClose region for the rest.
        /// </remarks>
        public void EnableBackToClose(bool enable);

        /// <summary>Whether the back/escape flow is enabled. False until someone turns it on.</summary>
        public bool IsBackToCloseEnabled { get; }

        /// <summary>
        /// Runs one back/escape action: closes the top screen, or opens the quit
        /// confirmation when the top screen is the only one.
        /// </summary>
        /// <remarks>
        /// The body that used to sit inline in <c>Tick</c>, extracted so that a second input
        /// source can drive it. It is not gated on <see cref="IsBackToCloseEnabled"/> — the
        /// caller decides — because the two callers gate differently: the legacy poll asks
        /// every frame, a <c>NavigationCancelEvent</c> arrives already meaning "the user
        /// pressed back".
        /// </remarks>
        public void HandleBackNavigation();
    }

    public class ScreenManager : IScreenManager, ITickable, IInitializable, IDisposable
    {
        #region Constructors

        private readonly SignalBus      signalBus;
        private readonly IAssetsManager assetsManager;
        private readonly ILogger        logger;

        private readonly List<IScreenPresenter>                   activeScreens               = new();
        private readonly Dictionary<Type, IScreenPresenter>       typeToLoadedScreenPresenter = new();
        private readonly Dictionary<Type, Task<IScreenPresenter>> typeToPendingScreen         = new();

        [Preserve]
        public ScreenManager(SignalBus signalBus, IAssetsManager assetsManager, ILoggerManager loggerManager)
        {
            this.signalBus     = signalBus;
            this.assetsManager = assetsManager;
            this.logger        = loggerManager.GetLogger(this);

            this.signalBus.Subscribe<StartLoadingNewSceneSignal>(this.CleanUpAllScreen);
            this.signalBus.Subscribe<ScreenShowSignal>(this.OnShowScreen);
            this.signalBus.Subscribe<ScreenCloseSignal>(this.OnCloseScreen);
            this.signalBus.Subscribe<ManualInitScreenSignal>(this.OnManualInitScreen);
            this.signalBus.Subscribe<ScreenSelfDestroyedSignal>(this.OnDestroyScreen);
            this.signalBus.Subscribe<PopupBlurBgShowedSignal>(this.OnPopupBlurBgShowed);
        }

        #endregion

        #region Implement IScreenManager

        public ReactiveProperty<IScreenPresenter> CurrentActiveScreen { get; } = new();
        public ReactiveProperty<IScreenPresenter> RootScreen          { get; } = new();
        public ReactiveProperty<IScreenPresenter> RootPopup           { get; } = new();

        private RootUICanvas rootUICanvas;

        public RootUICanvas RootUICanvas
        {
            get
            {
                if (!this.rootUICanvas) this.rootUICanvas = Object.FindObjectOfType<RootUICanvas>();
                return this.rootUICanvas;
            }
        }

        public Transform CurrentRootScreen  => this.RootUICanvas.RootUIShowTransform;
        public Transform CurrentHiddenRoot  => this.RootUICanvas.RootUIClosedTransform;
        public Transform CurrentOverlayRoot => this.RootUICanvas.RootUIOverlayTransform;

        // Built once on first use, not per access: these are handed out on every screen
        // open and close.
        private IViewLayer screenLayer;
        private IViewLayer hiddenLayer;
        private IViewLayer overlayLayer;

        public IViewLayer ScreenLayer  => this.screenLayer  ??= new TransformViewLayer(this.CurrentRootScreen);
        public IViewLayer HiddenLayer  => this.hiddenLayer  ??= new TransformViewLayer(this.CurrentHiddenRoot);
        public IViewLayer OverlayLayer => this.overlayLayer ??= new TransformViewLayer(this.CurrentOverlayRoot);

        #region Non-uGUI backend

        // These three properties stay uGUI: they are on IScreenManager, callers already
        // hold them, and making them backend-dependent would change what an existing
        // caller gets back. A non-uGUI screen's layers come from its backend below.

        private IScreenViewBackend viewBackend;
        private bool               viewBackendResolved;

        /// <summary>
        /// The backend for views the uGUI path cannot build, or null when there is none.
        /// </summary>
        /// <remarks>
        /// <para><b>Why it is resolved lazily instead of injected.</b> A constructor
        /// parameter is the obvious shape and is wrong here: VContainer has no optional
        /// constructor dependency, so adding one would make <c>ScreenManager</c>
        /// unresolvable in every project that does not register a backend — which is all
        /// six consuming repositories plus <c>com.gdk.3rd</c>, none of which use UI Toolkit.
        /// <c>TryResolve</c> against the current container asks the same question without
        /// making the answer mandatory, and it is asked once, not per screen.</para>
        ///
        /// <para>The setter exists so a test can supply a backend without a scene, a
        /// container or a <c>UIDocument</c>. Setting it also marks resolution done, so an
        /// explicitly-supplied backend is never overwritten by a container lookup.</para>
        /// </remarks>
        public IScreenViewBackend ViewBackend
        {
            get
            {
                if (this.viewBackendResolved) return this.viewBackend;
                this.viewBackendResolved = true;

                try
                {
                    // No container (no SceneScope yet) and no registration are both normal:
                    // a uGUI-only project never registers one, and asking must not throw
                    // through OpenScreen.
                    if (this.GetCurrentContainer().TryResolve<IScreenViewBackend>(out var resolved)) this.viewBackend = resolved;
                }
                catch (Exception e)
                {
                    this.logger.Debug($"No {nameof(IScreenViewBackend)} available ({e.GetType().Name}); the uGUI path is the only one.");
                }

                return this.viewBackend;
            }
            set
            {
                this.viewBackend         = value;
                this.viewBackendResolved = true;
                this.presenterToBackend.Clear();
            }
        }

        // Which backend serves which presenter type. Null value = the uGUI path, which is
        // both the answer for every existing screen and the answer when nothing is known.
        private readonly Dictionary<Type, IScreenViewBackend> presenterToBackend = new();

        /// <summary>
        /// The backend for <paramref name="presenterType"/>, or null for the uGUI path.
        /// </summary>
        /// <remarks>
        /// Decided off the VIEW type, as suggested when this step was handed over, and it
        /// does hold — but only because <c>ScreenPresenterViewType</c> can recover the view
        /// type from the presenter's generic base first; the manager itself never had it.
        /// The test is asked of the backend (<c>CanHandle</c>) rather than hard-coded to
        /// <c>ISurfaceScreenView</c> here, so <c>ScreenManager</c> holds no opinion about
        /// what any backend's views look like.
        /// </remarks>
        private IScreenViewBackend GetBackendFor(Type presenterType)
        {
            if (this.presenterToBackend.TryGetValue(presenterType, out var cached)) return cached;

            var backend  = this.ViewBackend;
            var viewType = ScreenPresenterViewType.Of(presenterType);
            var result   = backend != null && viewType != null && backend.CanHandle(viewType) ? backend : null;

            this.presenterToBackend[presenterType] = result;

            return result;
        }

        /// <summary>Parks a screen's view in whichever hidden layer its backend uses.</summary>
        /// <remarks>
        /// The uGUI branch is the call this replaced, unchanged. The split exists because
        /// <c>SetViewParent(Transform)</c> throws on a UI Toolkit presenter by design — a
        /// <c>VisualElement</c> has no <c>Transform</c> — so the two backends cannot share
        /// one reparent call.
        /// </remarks>
        private void MoveToHiddenLayer(IScreenPresenter screenPresenter)
        {
            var backend = this.GetBackendFor(screenPresenter.GetType());

            if (backend == null) screenPresenter.SetViewParent(this.CurrentHiddenRoot);
            else screenPresenter.SetViewParent(backend.HiddenLayer);
        }

        /// <summary>Moves a screen's view into its open layer — overlay or screen.</summary>
        private void MoveToActiveLayer(IScreenPresenter screenPresenter)
        {
            var backend   = this.GetBackendFor(screenPresenter.GetType());
            var isOverlay = this.CheckPopupIsOverlay(screenPresenter);

            if (backend == null) screenPresenter.SetViewParent(isOverlay ? this.CurrentOverlayRoot : this.CurrentRootScreen);
            else screenPresenter.SetViewParent(isOverlay ? backend.OverlayLayer : backend.ScreenLayer);
        }

        #endregion

        private IScreenPresenter previousActiveScreen;

        public async UniTask<T> OpenScreen<T>() where T : IScreenPresenter
        {
            var nextScreen = await this.GetScreen<T>() ?? throw new InvalidOperationException($"The {typeof(T).Name} screen does not exist");
            this.MoveToHiddenLayer(nextScreen);
            this.MoveToActiveLayer(nextScreen);
            await nextScreen.OpenViewAsync();
            return nextScreen;
        }

        public async UniTask<TPresenter> OpenScreen<TPresenter, TModel>(TModel model) where TPresenter : IScreenPresenter<TModel>
        {
            var nextScreen = await this.GetScreen<TPresenter>() ?? throw new InvalidOperationException($"The {typeof(TPresenter).Name} screen does not exist");
            this.MoveToHiddenLayer(nextScreen);
            this.MoveToActiveLayer(nextScreen);
            await nextScreen.OpenViewAsync(model);
            return nextScreen;
        }

        public async UniTask<T> GetScreen<T>() where T : IScreenPresenter
        {
            return (T)await this.GetScreen(typeof(T));
        }

        public async UniTask<IScreenPresenter> GetScreen(Type screenType)
        {
            if (this.typeToLoadedScreenPresenter.TryGetValue(screenType, out var screenPresenter)) return screenPresenter;

            if (!this.typeToPendingScreen.TryGetValue(screenType, out var loadingTask))
            {
                loadingTask = InstantiateScreen();
                this.typeToPendingScreen.Add(screenType, loadingTask);
            }

            var result = await loadingTask;
            this.typeToPendingScreen.Remove(screenType);

            return result;

            async Task<IScreenPresenter> InstantiateScreen()
            {
                screenPresenter = (this.GetCurrentContainer().Instantiate(screenType) as IScreenPresenter)!;
                var screenInfo = screenPresenter.GetType().GetCustomAttribute<ScreenInfoAttribute>();

                var backend = this.GetBackendFor(screenType);

                if (backend == null)
                {
                    // The uGUI path, unchanged: load a prefab, instantiate it straight into
                    // its layer, and take IScreenView off the instantiated GameObject.
                    var prefab     = await this.assetsManager.LoadAsync<GameObject>(screenInfo.AddressableScreenPath);
                    var viewObject = Object.Instantiate(prefab, this.CheckPopupIsOverlay(screenPresenter) ? this.CurrentOverlayRoot : this.CurrentRootScreen).GetComponent<IScreenView>();

                    screenPresenter.SetView(viewObject);
                }
                else
                {
                    // The backend path. It differs in two ways and only two: the view is
                    // built rather than Instantiate'd (there is no GameObject to hang it
                    // on), and parenting is therefore a separate step instead of an
                    // argument to Instantiate. SetView comes first because the parenting
                    // goes through the presenter's ViewSurface, which is the view's own.
                    var view = await backend.CreateViewAsync(ScreenPresenterViewType.Of(screenType), screenInfo.AddressableScreenPath);

                    screenPresenter.SetView(view);
                    this.MoveToActiveLayer(screenPresenter);
                }

                this.typeToLoadedScreenPresenter.Add(screenType, screenPresenter);

                return screenPresenter;
            }
        }

        public async UniTask CloseCurrentScreen()
        {
            if (this.activeScreens.Count > 0) await this.activeScreens.Last().CloseViewAsync();
        }

        public async UniTask CloseAllLastOverlayScreenAsync()
        {
            if (this.activeScreens.Count == 0 || !this.CheckPopupIsOverlay(this.activeScreens.Last())) return;

            var tasks = new List<UniTask>();
            for (var i = this.activeScreens.Count - 1; i > 0; i--)
            {
                if (this.CheckPopupIsOverlay(this.activeScreens[i]))
                    tasks.Add(this.activeScreens[i].CloseViewAsync());
                else
                    break;
            }

            this.CurrentActiveScreen.Value = this.activeScreens.Last();
            this.previousActiveScreen      = null;

            await UniTask.WhenAll(tasks);
        }

        public void CloseAllScreen()
        {
            var cacheActiveScreens = this.activeScreens.ToList();
            this.activeScreens.Clear();

            foreach (var screen in cacheActiveScreens) screen.CloseViewAsync().Forget();

            this.CurrentActiveScreen.Value = null;
            this.RootScreen.Value          = null;
            this.RootPopup.Value           = null;
            this.previousActiveScreen      = null;
        }

        public async UniTask CloseAllScreenAsync()
        {
            var tasks              = new List<UniTask>();
            var cacheActiveScreens = this.activeScreens.ToList();
            this.activeScreens.Clear();

            foreach (var screen in cacheActiveScreens) tasks.Add(screen.CloseViewAsync());

            this.CurrentActiveScreen.Value = null;
            this.RootScreen.Value          = null;
            this.RootPopup.Value           = null;
            this.previousActiveScreen      = null;

            await UniTask.WhenAll(tasks);
        }

        public void CleanUpAllScreen()
        {
            this.activeScreens.Clear();
            this.CurrentActiveScreen.Value = null;
            this.RootScreen.Value          = null;
            this.RootPopup.Value           = null;
            this.previousActiveScreen      = null;

            foreach (var screen in this.typeToLoadedScreenPresenter)
            {
                if (screen.Value.ScreenStatus != ScreenStatus.Opened) continue;
                screen.Value.Dispose();
            }

            this.typeToLoadedScreenPresenter.Clear();
        }

        #endregion

        #region Check Overlay Popup

        private bool CheckScreenIsPopup(IScreenPresenter screenPresenter)
        {
            return screenPresenter.GetType().GetCustomAttribute<PopupInfoAttribute>() is { };
        }

        private bool CheckPopupIsOverlay(IScreenPresenter screenPresenter)
        {
            return screenPresenter.GetType().GetCustomAttribute<PopupInfoAttribute>() is { IsOverlay: true };
        }

        #endregion

        #region Handle events

        void IInitializable.Initialize()
        {
        }

        void IDisposable.Dispose()
        {
            this.signalBus.Unsubscribe<StartLoadingNewSceneSignal>(this.CleanUpAllScreen);
            this.signalBus.Unsubscribe<ScreenShowSignal>(this.OnShowScreen);
            this.signalBus.Unsubscribe<ScreenCloseSignal>(this.OnCloseScreen);
            this.signalBus.Unsubscribe<ManualInitScreenSignal>(this.OnManualInitScreen);
            this.signalBus.Unsubscribe<ScreenSelfDestroyedSignal>(this.OnDestroyScreen);
            this.signalBus.Unsubscribe<PopupBlurBgShowedSignal>(this.OnPopupBlurBgShowed);
        }

        private void OnShowScreen(ScreenShowSignal signal)
        {
            this.previousActiveScreen      = this.CurrentActiveScreen.Value;
            this.CurrentActiveScreen.Value = signal.ScreenPresenter;

            // if show the screen that already in the active screens list, remove current one in list and add it to the last of list
            if (this.activeScreens.Contains(signal.ScreenPresenter)) this.activeScreens.Remove(signal.ScreenPresenter);

            this.activeScreens.Add(signal.ScreenPresenter);

            if (!this.CheckScreenIsPopup(signal.ScreenPresenter))
            {
                this.RootScreen.Value = signal.ScreenPresenter;
            }

            if (this.previousActiveScreen != null && this.previousActiveScreen != this.CurrentActiveScreen.Value)
            {
                if (this.CurrentActiveScreen.Value.IsClosePrevious)
                {
                    this.previousActiveScreen.CloseViewAsync();
                    this.previousActiveScreen = null;
                }
                else
                {
                    //With the current screen is popup, the previous screen will be hide after the blur background is shown
                    if (!this.CheckScreenIsPopup(this.CurrentActiveScreen.Value))
                    {
                        this.previousActiveScreen.HideView();
                    }
                    else
                    {
                        this.RootPopup.Value ??= this.CurrentActiveScreen.Value;
                        if (!this.CheckScreenIsPopup(this.previousActiveScreen))
                        {
                            // If the previous screen is a screen, it will be overlap
                            this.previousActiveScreen.OnOverlap(true);
                        }
                        else
                        {
                            if (!this.CheckPopupIsOverlay(this.CurrentActiveScreen.Value))
                            {
                                this.RootPopup.Value = this.CurrentActiveScreen.Value;
                                this.previousActiveScreen.HideView();
                            }
                            else
                            {
                                this.previousActiveScreen.OnOverlap(true);
                            }
                        }
                    }
                }
            }
        }

        private void OnCloseScreen(ScreenCloseSignal signal)
        {
            var closeScreenPresenter                                                 = signal.ScreenPresenter;
            if (closeScreenPresenter == this.RootPopup.Value) this.RootPopup.Value   = null;
            if (closeScreenPresenter == this.RootScreen.Value) this.RootScreen.Value = null;
            if (this.activeScreens.LastOrDefault() == closeScreenPresenter)
            {
                // If close the screen on the top, will be open again the behind screen if available
                this.CurrentActiveScreen.Value = null;
                this.activeScreens.Remove(closeScreenPresenter);

                if (this.activeScreens.Count > 0)
                {
                    var nextScreen = this.activeScreens.Last();

                    if (nextScreen.ScreenStatus == ScreenStatus.Opened)
                    {
                        this.OnShowScreen(new() { ScreenPresenter = nextScreen });
                        nextScreen.OnOverlap(false);
                    }
                    else
                        nextScreen.OpenViewAsync();
                }
            }
            else
            {
                this.activeScreens.Remove(closeScreenPresenter);
            }

            if (closeScreenPresenter != null) this.MoveToHiddenLayer(closeScreenPresenter);
        }

        private void OnManualInitScreen(ManualInitScreenSignal signal)
        {
            var screenPresenter = signal.ScreenPresenter;
            var screenType      = screenPresenter.GetType();

            if (!this.typeToLoadedScreenPresenter.TryAdd(screenType, screenPresenter)) return;

            if (this.GetBackendFor(screenType) != null)
            {
                // Manual init means "the view is already sitting in the RootUICanvas
                // hierarchy, go find it by name". A non-uGUI view is not in that hierarchy
                // and is not a Transform, so there is nothing to find. Say so rather than
                // fall through into Transform.Find and report a missing object.
                this.logger.Error($"{screenType.Name} uses a non-uGUI view backend; {nameof(ManualInitScreenSignal)} is uGUI-only.");
                return;
            }

            var screenInfo = screenPresenter.GetType().GetCustomAttribute<ScreenInfoAttribute>();

            var viewObj = this.CurrentRootScreen.Find(screenInfo.AddressableScreenPath);

            if (viewObj != null)
            {
                screenPresenter.SetView(viewObj.GetComponent<IScreenView>());

                if (signal.IncludingBindData) screenPresenter.BindData();
            }
            else
            {
                this.logger.Error($"The {screenInfo.AddressableScreenPath} object may be not instantiated in the RootUICanvas!!!");
            }
        }

        private void OnDestroyScreen(ScreenSelfDestroyedSignal signal)
        {
            var screenPresenter = signal.ScreenPresenter;
            var screenType      = screenPresenter.GetType();

            if (this.previousActiveScreen != null && this.previousActiveScreen.Equals(screenPresenter)) this.previousActiveScreen = null;
            this.typeToLoadedScreenPresenter.Remove(screenType);
            this.activeScreens.Remove(screenPresenter);
        }

        private void OnPopupBlurBgShowed()
        {
            if (this.previousActiveScreen != null && this.previousActiveScreen.ScreenStatus != ScreenStatus.Hide) this.previousActiveScreen.HideView();
        }

        #endregion

        #region BackToClose

        private bool enableBackToClose = false;

        public void EnableBackToClose(bool enable)
        {
            this.enableBackToClose = enable;
        }

        public bool IsBackToCloseEnabled => this.enableBackToClose;

        public int ActiveScreenCount => this.activeScreens.Count;

        /// <summary>
        /// The legacy Input Manager poll. Preserved exactly, and compiled only where the
        /// legacy Input Manager exists.
        /// </summary>
        /// <remarks>
        /// <para><b>This flow is dead in the consuming project, and the guard is why it is
        /// not also throwing.</b> <c>IndieRPGMMOAdventure/ProjectSettings/ProjectSettings.asset</c>
        /// has <c>activeInputHandler: 1</c> — Input System package only, not "Both" — so
        /// <c>UnityEngine.Input</c> throws <c>InvalidOperationException</c> on every call.
        /// The condition below evaluates <c>Input.GetKeyDown</c> FIRST, before the
        /// <c>enableBackToClose</c> short-circuit, so it would throw once per frame for as
        /// long as <c>ScreenManager</c> is ticked. <c>ScreenManagerVContainer</c> registers
        /// it <c>AsImplementedInterfaces()</c>, which includes <see cref="ITickable"/>, so any
        /// project calling <c>RegisterGameFoundation</c> ticks it. <b>This project does not
        /// call it</b> — <c>Assets/</c> has no reference to <c>GameFoundationVContainer</c> —
        /// so the throw is latent here and live in the other consumers of this package. Do
        /// not read a quiet console as evidence the flow is safe.</para>
        ///
        /// <para><c>ENABLE_LEGACY_INPUT_MANAGER</c> is defined by Unity when Active Input
        /// Handling is "Input Manager (Old)" or "Both", and undefined when it is
        /// "Input System Package (New)". Guarding on it keeps the uGUI escape path working
        /// byte-for-byte in any project that still has legacy input — nothing is removed —
        /// while a project on the new Input System alone gets a Tick that does nothing
        /// instead of a Tick that throws. The UI Toolkit path
        /// (<c>UIToolkitBackNavigation</c>) is what covers the second case, and it covers
        /// gamepad B and the Android back button with it.</para>
        ///
        /// <para>Independently of input handling, the flow has never been reachable: until
        /// this change <c>EnableBackToClose</c> was public on this class but absent from
        /// <see cref="IScreenManager"/>, which is what every consumer resolves, and it has
        /// zero call sites in this package, in <c>com.gdk.3rd</c> or in the consuming
        /// project. <c>enableBackToClose</c> has therefore always been false.</para>
        /// </remarks>
        void ITickable.Tick()
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            // back button flow
            if (!Input.GetKeyDown(KeyCode.Escape) || !this.enableBackToClose) return;

            this.HandleBackNavigation();
            #endif
        }

        public void HandleBackNavigation()
        {
            if (this.activeScreens.Count > 1)
            {
                Debug.Log("Close last screen");
                this.activeScreens.Last().CloseViewAsync();
            }
            else
            {
                Debug.Log("Show popup confirm quit app");

                this.OpenScreen<NotificationPopupPresenter, NotificationPopupModel>(new()
                {
                    Content        = "Do you really want to quit?",
                    Title          = "Are you sure?",
                    Type           = NotificationType.Option,
                    OkNoticeAction = this.QuitApplication,
                }).Forget();
            }
        }

        private void QuitApplication()
        {
            #if UNITY_EDITOR
            EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        #endregion
    }
}