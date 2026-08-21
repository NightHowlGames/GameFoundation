namespace GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter
{
    using System;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Signals;
    using UniT.Logging;

    /// <summary>The uGUI popup presenter.</summary>
    /// <remarks>
    /// The three bodies that used to sit here — popup open, popup close, popup hide, with
    /// their <c>PopupShowedSignal</c> / <c>PopupHiddenSignal</c> fires and the
    /// end-of-frame yield the background blur depends on — moved to
    /// <see cref="BaseScreenPresenterCore{TView}"/> as <c>OpenPopupViewAsync</c> /
    /// <c>ClosePopupViewAsync</c> / <c>HidePopupView</c>, unchanged, so the UI Toolkit
    /// popup presenter runs the same code rather than a copy of it. Behaviour here is
    /// identical.
    /// </remarks>
    public abstract class BasePopupPresenter<TView> : BaseScreenPresenter<TView> where TView : IScreenView
    {
        protected BasePopupPresenter(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager)
        {
        }

        public override UniTask OpenViewAsync() => this.OpenPopupViewAsync();

        public override UniTask CloseViewAsync() => this.ClosePopupViewAsync();

        public override void HideView() => this.HidePopupView();
    }

    public abstract class BasePopupPresenter<TView, TModel> : BasePopupPresenter<TView>, IScreenPresenter<TModel> where TView : IScreenView
    {
        protected TModel Model { get; private set; }

        protected BasePopupPresenter(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager)
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
