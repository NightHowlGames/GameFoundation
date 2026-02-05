namespace GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter
{
    using System;
    using System.Reflection;
    using Cysharp.Threading.Tasks;
    using GameFoundation.DI;
    using GameFoundation.Scripts.UIModule.MVP;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Scripts.UIModule.ScreenFlow.Signals;
    using GameFoundation.Signals;
    using UniT.Logging;
    using UniT.ResourceManagement;
    using UnityEngine;
    using ILogger = UniT.Logging.ILogger;

    public abstract class BaseScreenPresenter<TView> : IScreenPresenter where TView : IScreenView
    {
        protected SignalBus SignalBus { get; }
        protected ILogger   Logger    { get; }

        protected BaseScreenPresenter(SignalBus signalBus, ILoggerManager loggerManager)
        {
            this.SignalBus = signalBus;
            this.Logger    = loggerManager.GetLogger(this);
        }

        public         TView        View            { get; private set; }
        public         string       ScreenId        { get; private set; }
        public virtual bool         IsClosePrevious { get; protected set; } = false;
        public         ScreenStatus ScreenStatus    { get; protected set; } = ScreenStatus.Closed;

        #region Implement IUIPresenter

        public async void SetView(IUIView viewInstance)
        {
            this.View     = (TView)viewInstance;
            this.ScreenId = ScreenHelper.GetScreenId<TView>();
            if (!this.View.IsReadyToUse) await UniTask.WaitUntil(this, state => state.View.IsReadyToUse);
            this.OnViewReady();
        }

        public void SetViewParent(Transform parent)
        {
            if (parent == null)
            {
                this.Logger.Error(parent.name + "is null");
                return;
            }

            if (this.View.Equals(null)) return;
            this.View.RectTransform.SetParent(parent);
        }

        public Transform GetViewParent()
        {
            return this.View.RectTransform.parent;
        }

        public Transform CurrentTransform => this.View.RectTransform;

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
            if (this.View.Equals(null)) return;
            this.Dispose();
            this.View.DestroySelf();
            var key = this.GetType().GetCustomAttribute<ScreenInfoAttribute>().AddressableScreenPath;
            this.GetCurrentContainer().Resolve<IAssetsManager>().Unload(key);
        }

        public virtual void OnOverlap(bool isOverlap)
        {
            this.Logger.Info($"OnOverLap: {isOverlap} - {this.ScreenId}");
        }

        public int ViewSiblingIndex { get => this.View.RectTransform.GetSiblingIndex(); set => this.View.RectTransform.SetSiblingIndex(value); }

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