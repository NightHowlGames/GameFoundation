namespace GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter
{
    using System;
    using Cuvara.UIToolkit.Core;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Signals;
    using UniT.Logging;
    using UnityEngine;

    /// <summary>The uGUI screen presenter.</summary>
    /// <remarks>
    /// Its public surface is unchanged. What used to be its whole body — the
    /// <c>ScreenStatus</c> machine, the signal fires, <c>BindData</c> /
    /// <c>OpenViewAsync</c> / <c>CloseViewAsync</c> / <c>HideView</c> / <c>DestroyView</c>,
    /// <c>Dispose</c> — now lives in <see cref="BaseScreenPresenterCore{TView}"/>, which is
    /// constrained on the backend-neutral <c>IScreenViewBase</c>. What is left here is
    /// exactly the part that needs a <c>RectTransform</c>, plus the <c>IsReadyToUse</c>
    /// wait, which only a MonoBehaviour view has a reason for.
    /// </remarks>
    public abstract class BaseScreenPresenter<TView> : BaseScreenPresenterCore<TView> where TView : IScreenView
    {
        protected BaseScreenPresenter(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager)
        {
        }

        /// <inheritdoc/>
        /// <remarks>Covers the frame between <c>Instantiate</c> and <c>Awake</c>.</remarks>
        protected override async UniTask WaitForViewReady()
        {
            if (!this.View.IsReadyToUse) await UniTask.WaitUntil(this, state => state.View.IsReadyToUse);
        }

        public override void SetViewParent(Transform parent)
        {
            if (parent == null)
            {
                // Was `parent.name` — which dereferenced the very reference this branch
                // exists because it is null, so the guard threw instead of reporting.
                this.Logger.Error($"{this.GetType().Name}: cannot set view parent, parent is null");
                return;
            }

            if (!this.IsViewAlive) return;
            this.View.RectTransform.SetParent(parent);
        }

        public override Transform GetViewParent() => this.View.RectTransform.parent;

        public override Transform CurrentTransform => this.View.RectTransform;

        private IViewSurface viewSurface;

        // Cached: the screen flow reparents on every open and close, and allocating a
        // wrapper per call would put garbage on a path that runs during transitions.
        public override IViewSurface ViewSurface => this.viewSurface ??= new RectTransformViewSurface(this.View.RectTransform);

        public override int ViewSiblingIndex { get => this.View.RectTransform.GetSiblingIndex(); set => this.View.RectTransform.SetSiblingIndex(value); }
    }

    public abstract class BaseScreenPresenter<TView, TModel> : BaseScreenPresenter<TView>, IScreenPresenter<TModel> where TView : IScreenView
    {
        protected TModel Model { get; private set; }

        protected BaseScreenPresenter(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager)
        {
        }

        public virtual async UniTask OpenViewAsync(TModel model)
        {
            this.Model = model ?? throw new ArgumentNullException();
            await this.OpenViewAsync();
        }

        public sealed override UniTask BindData()
        {
            return this.BindData(this.Model);
        }

        public abstract UniTask BindData(TModel screenModel);
    }
}
