namespace GameFoundation.Scripts.UIModule.UITK.View
{
    using System;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using UnityEngine.UIElements;

    /// <summary>
    /// The UI Toolkit analogue of <c>BaseView</c>: a plain C# class, no MonoBehaviour,
    /// no <c>[SerializeField]</c>, no GameObject.
    /// </summary>
    /// <remarks>
    /// It satisfies <see cref="ISurfaceScreenView"/> — that is, <c>IScreenView</c> minus
    /// <c>RectTransform</c> and <c>IsReadyToUse</c>, plus the backend-agnostic
    /// <see cref="IViewSurface"/> that replaces the first of them.
    ///
    /// <para>What is deliberately absent, and why:</para>
    /// <list type="bullet">
    /// <item><c>RectTransform</c> — a <c>VisualElement</c> is not a <c>Transform</c> and has
    /// no GameObject behind it. Moving between layers, the only thing the screen flow
    /// actually did with it, goes through <see cref="ViewSurface"/> instead.</item>
    /// <item><c>IsReadyToUse</c> and the <c>await UniTask.WaitUntil(...)</c> that guards it in
    /// <c>BaseScreenPresenter.SetView</c> — those exist for the frame gap between
    /// <c>Instantiate</c> and <c>Awake</c>. A UI Toolkit view has no such gap: its root is
    /// built by a synchronous <c>CloneTree</c> before the constructor returns, so the view
    /// is usable the instant it exists. The uGUI path keeps both, unchanged.</item>
    /// <item><c>UIScreenTransition</c> — a uGUI MonoBehaviour. Intro/outro are virtual hooks
    /// here (<see cref="PlayIntroAnim"/> / <see cref="PlayOutroAnim"/>) that default to
    /// completing immediately; a UI Toolkit transition backend is a separate decision.</item>
    /// </list>
    ///
    /// <para>Show/hide maps 1:1 onto the uGUI <c>CanvasGroup</c> pair:
    /// <c>alpha</c> becomes <c>style.opacity</c>, and <c>blocksRaycasts</c> becomes
    /// <c>pickingMode</c> — <c>Position</c> when interactive, <c>Ignore</c> when not.
    /// Opacity alone would leave an invisible view still swallowing clicks.</para>
    /// </remarks>
    public abstract class BaseUIToolkitView : ISurfaceScreenView
    {
        public event Action ViewDidClose;
        public event Action ViewDidOpen;
        public event Action ViewDidDestroy;

        /// <summary>The root element of this view. Never null after construction.</summary>
        /// <remarks>
        /// Public, and deliberately: it is the exact counterpart of <c>IScreenView.RectTransform</c>
        /// on the uGUI view, which is public for the same reason — the thing that says where
        /// this view actually sits in the tree. Screen flow does not use it (it goes through
        /// <see cref="ViewSurface"/>); tests and debug tooling need it to assert or report
        /// what a view is attached to.
        /// </remarks>
        public VisualElement Root { get; }

        private IViewSurface viewSurface;

        // Cached: the screen flow reparents on every open and close, and allocating a
        // wrapper per call would put garbage on a path that runs during transitions.
        public IViewSurface ViewSurface => this.viewSurface ??= new VisualElementViewSurface(this.Root);

        /// <summary>Builds a view around an already-constructed root element.</summary>
        protected BaseUIToolkitView(VisualElement root)
        {
            this.Root = root ?? throw new ArgumentNullException(nameof(root));

            // Start invisible and non-interactive, so the view is created unseen and the
            // Open() transition is what reveals it — the same reason BaseView.Awake calls
            // UpdateAlpha(0).
            this.UpdateAlpha(0);
        }

        /// <summary>Builds a view by cloning <paramref name="visualTreeAsset"/>.</summary>
        /// <remarks>
        /// <c>CloneTree</c> is synchronous, which is the whole reason this class needs no
        /// <c>IsReadyToUse</c>: by the time the constructor returns, the hierarchy exists.
        /// </remarks>
        protected BaseUIToolkitView(VisualTreeAsset visualTreeAsset)
            : this(CloneRoot(visualTreeAsset))
        {
        }

        private static VisualElement CloneRoot(VisualTreeAsset visualTreeAsset)
        {
            if (visualTreeAsset == null) throw new ArgumentNullException(nameof(visualTreeAsset));
            return visualTreeAsset.CloneTree();
        }

        public virtual async UniTask Open()
        {
            this.UpdateAlpha(1f);
            await this.PlayIntroAnim();
            this.ViewDidOpen?.Invoke();
        }

        public virtual async UniTask Close()
        {
            await this.PlayOutroAnim();
            this.UpdateAlpha(0);
            this.ViewDidClose?.Invoke();
        }

        public void Hide() { this.UpdateAlpha(0); }

        public void Show() { this.UpdateAlpha(1); }

        public void DestroySelf()
        {
            // A VisualElement is not a Unity object; "destroy" means detaching it from
            // whatever layer holds it and dropping the last reference to it.
            this.Root.RemoveFromHierarchy();
            this.OnDestroySelf();
            this.ViewDidDestroy?.Invoke();
        }

        /// <summary>Override to release anything the view registered (callbacks, schedulers).</summary>
        protected virtual void OnDestroySelf()
        {
        }

        /// <summary>Intro transition. Defaults to none.</summary>
        protected virtual UniTask PlayIntroAnim() { return UniTask.CompletedTask; }

        /// <summary>Outro transition. Defaults to none.</summary>
        protected virtual UniTask PlayOutroAnim() { return UniTask.CompletedTask; }

        protected void UpdateAlpha(float value)
        {
            this.Root.style.opacity = value;
            this.Root.pickingMode   = value >= 1 ? PickingMode.Position : PickingMode.Ignore;
        }
    }
}
