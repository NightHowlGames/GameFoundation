namespace GameFoundation.Scripts.UIModule.MVP
{
    using System;
    using UnityEngine;

    public interface IUIItemPresenter : IUIPresenter
    {
        /// <summary>
        /// Show/Hide view
        /// </summary>
        /// <param name="value"></param>
        void SetActiveView(bool value);
    }

    public interface IUIItemPresenter<in TView, in TModel>
    {
        public void SetView(TView viewInstance);

        public void BindData(TModel param);
    }

    /// <summary>
    /// Base UI presenter for item
    /// </summary>
    /// <typeparam name="TView">Type of view</typeparam>
    public abstract class BaseUIItemPresenter<TView> : IUIItemPresenter, IUIView where TView : MonoBehaviour
    {
        public TView View { get; private set; }

        /// <summary>
        /// Set view manually
        /// </summary>
        /// <param name="viewInstance"></param>
        public virtual void SetView(TView viewInstance)
        {
            this.View = viewInstance;
        }

        public void SetView(IUIView viewInstance)
        {
            this.View = (TView)viewInstance;
        }

        public virtual void SetActiveView(bool value)
        {
            if (this.View != null) this.View.gameObject.SetActive(value);
        }
    }

    public abstract class BaseUIItemPresenter<TView, TModel> : BaseUIItemPresenter<TView>, IUIItemPresenter<TView, TModel>, IDisposable where TView : MonoBehaviour
    {
        public virtual void OnViewReady()
        {
        }

        public abstract void BindData(TModel param);

        public virtual void Dispose()
        {
        }
    }

    public class TViewMono : MonoBehaviour, IUIView
    {
    }
}