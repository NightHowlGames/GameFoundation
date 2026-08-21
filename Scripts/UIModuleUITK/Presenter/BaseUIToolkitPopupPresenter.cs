namespace GameFoundation.Scripts.UIModule.UITK.Presenter
{
    using System;
    using Cuvara.UIToolkit.Core;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Signals;
    using UniT.Logging;

    /// <summary>The UI Toolkit popup presenter.</summary>
    /// <remarks>
    /// It runs the SAME popup flow as the uGUI <c>BasePopupPresenter</c> — the same
    /// <c>PopupShowedSignal</c> / <c>PopupHiddenSignal</c> fires, the same end-of-frame
    /// yield before <c>View.Open()</c> — because both call the shared bodies on
    /// <see cref="BaseScreenPresenterCore{TView}"/>, not copies of them.
    /// </remarks>
    public abstract class BaseUIToolkitPopupPresenter<TView> : BaseUIToolkitScreenPresenter<TView> where TView : ISurfaceScreenView
    {
        protected BaseUIToolkitPopupPresenter(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager)
        {
        }

        public override UniTask OpenViewAsync() => this.OpenPopupViewAsync();

        public override UniTask CloseViewAsync() => this.ClosePopupViewAsync();

        public override void HideView() => this.HidePopupView();
    }

    /// <summary>The <c>TModel</c> pair of <see cref="BaseUIToolkitPopupPresenter{TView}"/>.</summary>
    public abstract class BaseUIToolkitPopupPresenter<TView, TModel> : BaseUIToolkitPopupPresenter<TView>, IScreenPresenter<TModel> where TView : ISurfaceScreenView
    {
        protected TModel Model { get; private set; }

        protected BaseUIToolkitPopupPresenter(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager)
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
