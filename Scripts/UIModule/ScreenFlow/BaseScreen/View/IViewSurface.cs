namespace GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View
{
    /// <summary>
    /// A place a screen's view can live in — one of the three roots the screen flow
    /// moves views between (shown, hidden, overlay).
    /// </summary>
    /// <remarks>
    /// Deliberately empty. The screen flow never inspects a layer; it only hands one
    /// to <see cref="IViewSurface.SetParent"/>. Keeping it opaque is what lets a
    /// uGUI <c>Transform</c> and a UI Toolkit <c>VisualElement</c> both be a layer
    /// without either backend leaking into <c>ScreenManager</c>.
    /// </remarks>
    public interface IViewLayer
    {
    }

    /// <summary>
    /// The one thing the screen flow does to a view that is backend-specific:
    /// move it between layers.
    /// </summary>
    /// <remarks>
    /// This exists because <c>IScreenView.RectTransform</c> is declared on the view
    /// CONTRACT, not on an implementation — so no non-uGUI view can satisfy the
    /// contract today. This interface is the seam that removes that constraint.
    ///
    /// It carries ONE member on purpose. <c>SetViewParent</c> is the only one of
    /// <c>IScreenPresenter</c>'s four Transform-shaped members with a call site
    /// anywhere in this package, in ThirdPartyServices, or in the consuming project
    /// — <c>GetViewParent</c>, <c>CurrentTransform</c> and <c>ViewSiblingIndex</c>
    /// have none. Abstracting members nobody calls would add surface to maintain and
    /// prove nothing; they stay on <see cref="IScreenPresenter"/> unchanged, and a
    /// second backend can leave them uGUI-only until something actually needs them.
    /// </remarks>
    public interface IViewSurface
    {
        void SetParent(IViewLayer layer);
    }
}
