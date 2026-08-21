namespace GameFoundation.Scripts.UIModule.UITK.Presenter
{
    using System;
    using Cuvara.UIToolkit.Core;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Signals;
    using UniT.Logging;
    using UnityEngine;

    /// <summary>
    /// The UI Toolkit screen presenter: <see cref="BaseScreenPresenterCore{TView}"/>
    /// constrained on <see cref="ISurfaceScreenView"/> instead of <c>IScreenView</c>.
    /// </summary>
    /// <remarks>
    /// This is the piece the UI Toolkit view backend was missing. <c>BaseUIToolkitView</c>
    /// satisfies <see cref="ISurfaceScreenView"/>, but every presenter base in the package
    /// was constrained on <c>IScreenView</c> — the uGUI contract, which requires a
    /// <c>RectTransform</c> — so no UI Toolkit view could be driven by one. Swapping the
    /// constraint is the whole of the change; the lifecycle body is inherited, not copied.
    ///
    /// <para>The four Transform-shaped members of <see cref="IScreenPresenter"/> throw
    /// here rather than returning null or doing nothing. None of them has a call site in
    /// this package, in ThirdPartyServices, or in the consuming project — only
    /// <c>SetViewParent</c> is called, and the screen flow calls the
    /// <see cref="IViewLayer"/> overload, which works. A silent no-op would turn a wiring
    /// mistake into a screen that renders nowhere with nothing in the log; a throw names
    /// the presenter and the member on the spot. They stay implementable later if
    /// something genuinely needs a sibling-index equivalent.</para>
    /// </remarks>
    public abstract class BaseUIToolkitScreenPresenter<TView> : BaseScreenPresenterCore<TView> where TView : ISurfaceScreenView
    {
        protected BaseUIToolkitScreenPresenter(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager)
        {
        }

        /// <summary>The view's own surface — no wrapper to allocate or cache.</summary>
        /// <remarks>
        /// The uGUI presenter has to build a <c>RectTransformViewSurface</c> around its
        /// view's <c>RectTransform</c> and cache it. A view that implements
        /// <see cref="ISurfaceScreenView"/> already owns and caches its surface.
        /// </remarks>
        public override IViewSurface ViewSurface => this.View.ViewSurface;

        public override void SetViewParent(Transform parent) => throw this.NotTransformBacked(nameof(this.SetViewParent));

        public override Transform GetViewParent() => throw this.NotTransformBacked(nameof(this.GetViewParent));

        public override Transform CurrentTransform => throw this.NotTransformBacked(nameof(this.CurrentTransform));

        public override int ViewSiblingIndex
        {
            get => throw this.NotTransformBacked(nameof(this.ViewSiblingIndex));
            set => throw this.NotTransformBacked(nameof(this.ViewSiblingIndex));
        }

        private NotSupportedException NotTransformBacked(string member)
        {
            return new NotSupportedException(
                $"{this.GetType().Name}.{member} is not available on the UI Toolkit backend: a VisualElement " +
                $"has no Transform behind it. Use the {nameof(IViewLayer)} overload of SetViewParent, or "     +
                $"{nameof(this.ViewSurface)}.");
        }
    }

    /// <summary>The <c>TModel</c> pair of <see cref="BaseUIToolkitScreenPresenter{TView}"/>.</summary>
    /// <remarks>
    /// The body mirrors <c>BaseScreenPresenter&lt;TView, TModel&gt;</c> line for line. It
    /// cannot inherit it: C# has single inheritance, and this class must derive from the
    /// UI Toolkit screen presenter to pick up the backend members above. The same
    /// constraint already produced the identical pair on <c>BasePopupPresenter</c> before
    /// any of this, so the shape is not new — it is eighteen lines of <c>Model</c>
    /// plumbing with no lifecycle logic in it.
    /// </remarks>
    public abstract class BaseUIToolkitScreenPresenter<TView, TModel> : BaseUIToolkitScreenPresenter<TView>, IScreenPresenter<TModel> where TView : ISurfaceScreenView
    {
        protected TModel Model { get; private set; }

        protected BaseUIToolkitScreenPresenter(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager)
        {
        }

        public virtual async UniTask OpenViewAsync(TModel model)
        {
            this.Model = model ?? throw new ArgumentNullException(nameof(model));
            await this.OpenViewAsync();
        }

        public sealed override UniTask BindData()
        {
            return this.BindData(this.Model);
        }

        public abstract UniTask BindData(TModel screenModel);
    }
}
