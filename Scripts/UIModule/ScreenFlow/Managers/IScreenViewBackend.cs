namespace GameFoundation.Scripts.UIModule.ScreenFlow.Managers
{
    using System;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.MVP;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;

    /// <summary>
    /// How <c>ScreenManager</c> builds a view, and where it parents one, for a UI backend
    /// that is not uGUI.
    /// </summary>
    /// <remarks>
    /// <para><b>Why an interface and not a direct call.</b> The UI Toolkit code lives in
    /// <c>GameFoundation.UIModule.UITK</c>, and that assembly already references
    /// <c>GameFoundation.UIModule</c> — where <c>ScreenManager</c> lives. A direct call the
    /// other way would be an assembly reference cycle, which Unity rejects outright. So the
    /// contract is declared here, on the uGUI side, and implemented over there; the manager
    /// never names a <c>VisualElement</c>, a <c>VisualTreeAsset</c> or a <c>UIDocument</c>.</para>
    ///
    /// <para><b>Why the uGUI path is not one of these.</b> It could have been, and the
    /// symmetry would read better — but the uGUI construction path is the one thing in this
    /// step that must stay behaviourally identical for six consuming repositories, and
    /// moving it behind an interface is a change to it. It stays inline in
    /// <c>ScreenManager</c>, byte for byte, and this interface is only consulted for
    /// presenters the uGUI path cannot serve. Nothing that opens a uGUI screen today runs a
    /// line of new code.</para>
    ///
    /// <para><b>Layers, not Transforms.</b> The three roots are handed out as
    /// <see cref="IViewLayer"/> — the seam added in the first commit of this branch — for
    /// the same reason: a UI Toolkit layer is a <c>VisualElement</c> and has no
    /// <c>Transform</c> to return.</para>
    /// </remarks>
    public interface IScreenViewBackend
    {
        /// <summary>True when this backend can build <paramref name="viewType"/>.</summary>
        /// <remarks>
        /// Asked about the VIEW type, not the presenter type: the view is what a backend
        /// actually constructs, and it is what the two backends' contracts differ on
        /// (<c>IScreenView</c> vs <see cref="ISurfaceScreenView"/>). See
        /// <c>ScreenPresenterViewType</c> for how the manager gets from one to the other.
        /// </remarks>
        bool CanHandle(Type viewType);

        /// <summary>The layer an open, non-overlay screen lives in.</summary>
        IViewLayer ScreenLayer { get; }

        /// <summary>The layer a closed or hidden screen is parked in.</summary>
        IViewLayer HiddenLayer { get; }

        /// <summary>The layer an overlay popup lives in.</summary>
        IViewLayer OverlayLayer { get; }

        /// <summary>
        /// Loads the asset at <paramref name="addressableScreenPath"/> and builds
        /// <paramref name="viewType"/> from it. The returned view is NOT parented yet.
        /// </summary>
        /// <remarks>
        /// The key is the same <c>ScreenInfoAttribute.AddressableScreenPath</c> the uGUI
        /// path uses — deliberately, so a screen does not gain a second addressing scheme
        /// just by changing backend. What the key resolves TO differs (a
        /// <c>VisualTreeAsset</c> rather than a prefab), which is the backend's business,
        /// not the manager's.
        /// </remarks>
        UniTask<IUIView> CreateViewAsync(Type viewType, string addressableScreenPath);
    }
}
