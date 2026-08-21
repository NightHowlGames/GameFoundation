namespace GameFoundation.Scripts.UIModule.UITK.Managers
{
    using System;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.MVP;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Scripts.UIModule.ScreenFlow.Managers;
    using GameFoundation.Scripts.UIModule.UITK.View;
    using UniT.ResourceManagement;
    using UnityEngine.Scripting;
    using UnityEngine.UIElements;
    using Object = UnityEngine.Object;

    /// <summary>
    /// The UI Toolkit half of <see cref="IScreenViewBackend"/>: loads a
    /// <see cref="VisualTreeAsset"/> and hands back the three
    /// <see cref="VisualElementViewLayer"/>s of the scene's <see cref="RootUIDocument"/>.
    /// </summary>
    /// <remarks>
    /// <para>This is the class that makes a UI Toolkit presenter openable through
    /// <c>ScreenManager</c> at all. Everything above it — the seam, the view base, the
    /// presenters, the ported popup — compiled without it, but the manager had exactly one
    /// way to build a view (<c>Instantiate(prefab).GetComponent&lt;IScreenView&gt;()</c>)
    /// and a plain C# view has no answer to it.</para>
    ///
    /// <para><b>How it finds the layers, and what would be better.</b>
    /// <c>FindObjectOfType</c>, cached after the first hit — deliberately the same shape as
    /// <c>ScreenManager.RootUICanvas</c>, so the two backends behave alike when a scene
    /// loads, unloads and reloads. It is not the better design: a scene-scoped
    /// <c>RegisterComponentInHierarchy&lt;RootUIDocument&gt;()</c> would let VContainer own
    /// the lookup and fail loudly at container build if the scene has no document, instead
    /// of at the first screen open. That change is available (see
    /// <c>UIToolkitViewBackendVContainer</c>) but it is NOT made the default here: it would
    /// require every consuming scene to have the document present when its scope builds,
    /// which is a behaviour change bigger than this step, and it would diverge from the
    /// uGUI root lookup that is being deliberately left alone. Stated rather than
    /// smuggled — replacing both lookups together is its own change.</para>
    /// </remarks>
    public class UIToolkitScreenViewBackend : IScreenViewBackend
    {
        private readonly IAssetsManager assetsManager;

        private RootUIDocument rootUIDocument;

        [Preserve]
        public UIToolkitScreenViewBackend(IAssetsManager assetsManager)
        {
            this.assetsManager = assetsManager;
        }

        /// <summary>The scene's document, found once and cached.</summary>
        /// <remarks>
        /// The <c>!this.rootUIDocument</c> test is the Unity-object null check, so a
        /// document destroyed by a scene change is re-found rather than returned dead —
        /// which is exactly why <c>RootUICanvas</c> is written the same way.
        /// </remarks>
        public RootUIDocument RootUIDocument
        {
            get
            {
                if (!this.rootUIDocument) this.rootUIDocument = Object.FindObjectOfType<RootUIDocument>();

                if (!this.rootUIDocument)
                {
                    throw new InvalidOperationException(
                        $"No {nameof(RootUIDocument)} in the loaded scenes. A UI Toolkit screen needs one "
                        + $"(a GameObject with a {nameof(UIDocument)} and a {nameof(RootUIDocument)} on it) the way a uGUI "
                        + "screen needs a RootUICanvas.");
                }

                return this.rootUIDocument;
            }
        }

        public bool CanHandle(Type viewType)
        {
            // The whole discriminator: a view that can hand out its own IViewSurface is one
            // this backend can build and parent. A uGUI view cannot, and falls through to
            // the manager's unchanged prefab path.
            return typeof(ISurfaceScreenView).IsAssignableFrom(viewType);
        }

        public IViewLayer ScreenLayer  => this.RootUIDocument.ShowLayer;
        public IViewLayer HiddenLayer  => this.RootUIDocument.ClosedLayer;
        public IViewLayer OverlayLayer => this.RootUIDocument.OverlayLayer;

        public async UniTask<IUIView> CreateViewAsync(Type viewType, string addressableScreenPath)
        {
            // Same key as the uGUI path, different asset type behind it: the screen keeps
            // one address, and the backend decides what that address resolves to.
            var visualTreeAsset = await this.assetsManager.LoadAsync<VisualTreeAsset>(addressableScreenPath);

            return UIToolkitViewFactory.Create(viewType, visualTreeAsset);
        }
    }
}
