namespace GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter
{
    using System;
    using System.Reflection;
    using Cysharp.Threading.Tasks;
    using GameFoundation.DI;
    using GameFoundation.Scripts.UIModule.MVP;
    using GameFoundation.Scripts.UIModule.ScreenFlow;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Scripts.UIModule.ScreenFlow.Signals;
    using GameFoundation.Signals;
    using UniT.Logging;
    using UniT.ResourceManagement;
    using UnityEngine;
    using ILogger = UniT.Logging.ILogger;

    /// <summary>
    /// Everything a screen presenter does that does not depend on a UI backend: the
    /// <see cref="ScreenStatus"/> machine, the SignalBus fires, and the
    /// open / close / hide / destroy flow.
    /// </summary>
    /// <remarks>
    /// This class is a PURE EXTRACTION out of <c>BaseScreenPresenter&lt;TView&gt;</c>.
    /// Every member below used to be declared there, with the same body, and
    /// <c>BaseScreenPresenter&lt;TView&gt;</c> still inherits all of them and keeps its
    /// public surface byte-for-byte identical — so <c>ThirdPartyServices</c> and the
    /// consuming repositories are unaffected.
    ///
    /// <para>It exists because <c>BaseScreenPresenter&lt;TView&gt;</c> is constrained on
    /// <see cref="IScreenView"/>, which is the uGUI view contract: it requires a
    /// <c>RectTransform</c>. A UI Toolkit view cannot produce one, so it could not be used
    /// with that presenter at all — the gap flagged when the UI Toolkit view backend
    /// landed. This class is constrained on <see cref="IScreenViewBase"/> instead, which is
    /// backend-neutral, and hands the handful of genuinely backend-shaped members to its
    /// subclasses as abstract members.</para>
    ///
    /// <para>Extraction rather than a second copy: of the roughly 120 lines in the original
    /// presenter, exactly five members touch <c>RectTransform</c> and one waits on
    /// <c>IsReadyToUse</c>. Duplicating the other ~100 lines — the status machine and every
    /// signal fired during a transition — would mean two places to keep a screen-lifecycle
    /// fix in, which is the kind of drift that shows up as one backend's screens leaking
    /// and the other's not.</para>
    /// </remarks>
    public abstract class BaseScreenPresenterCore<TView> : IScreenPresenter where TView : IScreenViewBase
    {
        protected SignalBus SignalBus { get; }
        protected ILogger   Logger    { get; }

        protected BaseScreenPresenterCore(SignalBus signalBus, ILoggerManager loggerManager)
        {
            this.SignalBus = signalBus;
            this.Logger    = loggerManager.GetLogger(this);
        }

        public         TView        View            { get; private set; }
        public         string       ScreenId        { get; private set; }
        public virtual bool         IsClosePrevious { get; protected set; } = false;
        public         ScreenStatus ScreenStatus    { get; protected set; } = ScreenStatus.Closed;

        #region Backend-specific surface

        /// <summary>The view as something that can be moved between layers, backend-agnostic.</summary>
        public abstract IViewSurface ViewSurface { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// Transform-shaped, and therefore uGUI-shaped. Kept on <see cref="IScreenPresenter"/>
        /// because six consuming repositories compile against it; a backend with no
        /// Transform behind its views is expected to reject the call rather than pretend.
        /// </remarks>
        public abstract void SetViewParent(Transform parent);

        /// <inheritdoc cref="SetViewParent(Transform)"/>
        public abstract Transform GetViewParent();

        /// <inheritdoc cref="SetViewParent(Transform)"/>
        public abstract Transform CurrentTransform { get; }

        /// <inheritdoc cref="SetViewParent(Transform)"/>
        public abstract int ViewSiblingIndex { get; set; }

        /// <summary>
        /// Waits until the freshly-set view can be used. Defaults to "immediately".
        /// </summary>
        /// <remarks>
        /// The uGUI backend overrides this to wait on <c>IScreenView.IsReadyToUse</c>,
        /// which covers the frame gap between <c>Instantiate</c> and <c>Awake</c>. A view
        /// built synchronously — a UI Toolkit one, cloned in its constructor — has no such
        /// gap, and making it await a frame would delay every open for nothing.
        /// </remarks>
        protected virtual UniTask WaitForViewReady() { return UniTask.CompletedTask; }

        /// <summary>True while the view object is still usable.</summary>
        /// <remarks>
        /// <c>Equals(null)</c> rather than <c>== null</c>: for a uGUI view this is the
        /// Unity-object null check, which reports a destroyed GameObject as null. The
        /// reference check in front of it is what keeps a plain C# view — which can be a
        /// genuine null — from throwing here.
        /// </remarks>
        protected virtual bool IsViewAlive => this.View is not null && !this.View.Equals(null);

        /// <summary>
        /// Releases the asset the view was built from, on <see cref="DestroyView"/>.
        /// </summary>
        /// <remarks>
        /// Default is the Addressables unload keyed by <see cref="ScreenInfoAttribute"/>,
        /// which is how every uGUI screen is loaded. The attribute lookup is null-guarded:
        /// it was not before, and a presenter without the attribute died with a
        /// <c>NullReferenceException</c> inside <c>DestroyView</c> rather than a message
        /// saying which presenter was missing it.
        /// </remarks>
        protected virtual void UnloadViewAsset()
        {
            var screenInfo = this.GetType().GetCustomAttribute<ScreenInfoAttribute>();

            if (screenInfo == null)
            {
                this.Logger.Warning($"{this.GetType().Name} has no {nameof(ScreenInfoAttribute)}; nothing to unload.");
                return;
            }

            this.GetCurrentContainer().Resolve<IAssetsManager>().Unload(screenInfo.AddressableScreenPath);
        }

        #endregion

        #region Implement IUIPresenter

        public async void SetView(IUIView viewInstance)
        {
            this.View     = (TView)viewInstance;
            this.ScreenId = ScreenHelper.GetScreenId<TView>();
            await this.WaitForViewReady();
            this.OnViewReady();
        }

        public void SetViewParent(IViewLayer layer) { this.ViewSurface.SetParent(layer); }

        public abstract UniTask BindData();

        public virtual async UniTask OpenViewAsync()
        {
            if (this.ScreenStatus == ScreenStatus.Opened)
            {
                this.Dispose();
                await this.BindData();
                return;
            }
            await this.BindData();
            this.ScreenStatus = ScreenStatus.Opened;
            this.SignalBus.Fire(new ScreenShowSignal() { ScreenPresenter = this });
            await this.View.Open();
        }

        public virtual async UniTask CloseViewAsync()
        {
            if (this.ScreenStatus == ScreenStatus.Closed) return;
            this.ScreenStatus = ScreenStatus.Closed;
            await this.View.Close();
            this.SignalBus.Fire(new ScreenCloseSignal() { ScreenPresenter = this });
            this.Dispose();
        }

        public virtual void CloseView()
        {
            this.CloseViewAsync().Forget();
        }

        public virtual void HideView()
        {
            if (this.ScreenStatus is ScreenStatus.Hide or ScreenStatus.Destroyed) return;
            this.ScreenStatus = ScreenStatus.Hide;
            this.View.Hide();
            // this.SignalBus.Fire(new ScreenHideSignal() { ScreenPresenter = this }); // Active this signal later, when need
            this.Dispose();
        }

        public virtual void DestroyView()
        {
            if (this.ScreenStatus == ScreenStatus.Destroyed) return;
            this.ScreenStatus = ScreenStatus.Destroyed;
            if (!this.IsViewAlive) return;
            this.Dispose();
            this.View.DestroySelf();
            this.UnloadViewAsset();
        }

        public virtual void OnOverlap(bool isOverlap)
        {
            this.Logger.Info($"OnOverLap: {isOverlap} - {this.ScreenId}");
        }

        #endregion

        #region Popup flow

        // The popup variant of open / close / hide, shared verbatim by BOTH backends'
        // popup presenters. It lives here — one level above the popup classes — because
        // C# has single inheritance: each backend's popup presenter must derive from that
        // backend's screen presenter to inherit its Transform members, so the two popup
        // classes cannot share a common popup base of their own. The alternative was to
        // copy these three bodies, signals and all, into the UI Toolkit popup presenter.

        /// <summary>The popup form of <see cref="OpenViewAsync"/>.</summary>
        protected async UniTask OpenPopupViewAsync()
        {
            await this.BindData();

            if (this.ScreenStatus == ScreenStatus.Opened) return;
            this.ScreenStatus = ScreenStatus.Opened;
            this.SignalBus.Fire(new ScreenShowSignal() { ScreenPresenter  = this });
            this.SignalBus.Fire(new PopupShowedSignal() { ScreenPresenter = this });
            // wait to end of frame then open screen view, take time to blur background capture last screen
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            await this.View.Open();
        }

        /// <summary>The popup form of <see cref="CloseViewAsync"/>.</summary>
        protected async UniTask ClosePopupViewAsync()
        {
            if (this.ScreenStatus == ScreenStatus.Closed) return;
            this.ScreenStatus = ScreenStatus.Closed;
            await this.View.Close();
            this.SignalBus.Fire(new PopupHiddenSignal() { ScreenPresenter = this });
            this.SignalBus.Fire(new ScreenCloseSignal() { ScreenPresenter = this });
            this.Dispose();
        }

        /// <summary>The popup form of <see cref="HideView"/>.</summary>
        protected void HidePopupView()
        {
            if (this.ScreenStatus == ScreenStatus.Hide) return;
            this.ScreenStatus = ScreenStatus.Hide;
            this.View.Hide();
            this.SignalBus.Fire(new PopupHiddenSignal() { ScreenPresenter = this });
            this.Dispose();
        }

        #endregion

        protected virtual void OnViewReady()
        {
            this.View.ViewDidDestroy += this.OnViewDestroyed;
        }

        protected virtual void OnViewDestroyed()
        {
            this.SignalBus.Fire(new ScreenSelfDestroyedSignal() { ScreenPresenter = this });
        }

        public virtual void Dispose()
        {
        }
    }

}
