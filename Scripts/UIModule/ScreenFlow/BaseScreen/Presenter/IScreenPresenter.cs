namespace GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter
{
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using System;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.MVP;
    using UnityEngine;

    /// <summary>
    /// The Presenter is the link between the Model and the View. It holds the state of the View and updates it depending on that state and on external events:
    /// - Holds the application state needed for that view
    /// - Controls view flow
    /// - Shows/hides/activates/deactivates/updates the view or parts of the view depending on the state.
    /// - Handles events either triggered by the player in the View (e.g. the player touched a button) or triggered by the Model (e.g. the player has gained XP and that triggered a Level Up event so the controller updates the level Number in the view)
    /// </summary>
    public interface IScreenPresenter : IUIPresenter, IDisposable
    {
        public string       ScreenId        { get; }
        public bool         IsClosePrevious { get; }
        public ScreenStatus ScreenStatus    { get; }

        public void SetViewParent(Transform parent);

        public Transform GetViewParent();

        public Transform CurrentTransform { get; }

        /// <summary>The view as something that can be moved between layers, backend-agnostic.</summary>
        /// <remarks>
        /// Added beside the Transform members rather than replacing them: those are part
        /// of a public API that ThirdPartyServices and six consuming repositories compile
        /// against, and replacing them would make this a coordinated multi-repo break for
        /// no benefit today. Prefer this member in new code; the Transform ones stay for
        /// the uGUI backend.
        /// </remarks>
        public IViewSurface ViewSurface { get; }

        /// <summary>Backend-agnostic form of <see cref="SetViewParent(Transform)"/>.</summary>
        public void SetViewParent(IViewLayer layer);

        public UniTask BindData();

        public UniTask OpenViewAsync();
        public UniTask CloseViewAsync();
        public void    CloseView();
        public void    HideView();
        public void    DestroyView();

        /// <summary>
        /// Called when the screen is overlap by another screen
        /// </summary>
        public void OnOverlap(bool isOverlap);

        public int ViewSiblingIndex { get; set; }
    }

    public interface IScreenPresenter<in TModel> : IScreenPresenter
    {
        public UniTask OpenViewAsync(TModel model);
    }

    public enum ScreenStatus
    {
        Opened,
        Closed,
        Hide,
        Destroyed,
    }
}