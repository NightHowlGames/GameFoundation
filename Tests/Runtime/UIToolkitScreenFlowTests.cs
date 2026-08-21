#if GDK_VCONTAINER
namespace GameFoundation.UIModule.UITK.Tests
{
    using System;
    using System.Collections;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.CommonScreen;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Scripts.UIModule.ScreenFlow.Managers;
    using GameFoundation.Scripts.UIModule.UITK;
    using GameFoundation.Scripts.UIModule.UITK.CommonScreen;
    using Cuvara.UIToolkit.Managers;
    using GameFoundation.Scripts.UIModule.UITK.Managers;
    using Cuvara.UIToolkit.View;
    using GameFoundation.Scripts.Utilities;
    using GameFoundation.Signals;
    using GameFoundation.UIModule.UIModule;
    using NUnit.Framework;
    using UniT.Logging;
    using UniT.ResourceManagement;
    using UnityEngine;
    using UnityEngine.TestTools;
    using UnityEngine.UIElements;
    using VContainer;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Exercises the second construction path <c>ScreenManager</c> learned: a UI Toolkit
    /// presenter, built from a <c>VisualTreeAsset</c> and parented into a
    /// <c>VisualElementViewLayer</c>, opened through the same
    /// <c>OpenScreen&lt;TPresenter, TModel&gt;</c> every uGUI screen uses.
    /// </summary>
    /// <remarks>
    /// These run in PlayMode on purpose, and could not run anywhere else: the manager
    /// instantiates a presenter through <c>GetCurrentContainer()</c>, which needs a live
    /// <c>SceneScope</c>, and a <c>LifetimeScope</c> only builds its container in
    /// <c>Awake</c> — which never fires in EditMode.
    ///
    /// <para>Two things are stubbed and nothing else: Addressables (the popup has no
    /// entry, and making one means editing the consuming project) and the audio service.
    /// The screen manager, the backend, the factory, the presenter, the view, the popup
    /// signal flow and the real <c>RootUIDocument</c> are all the shipping classes.</para>
    /// </remarks>
    public class UIToolkitScreenFlowTests
    {
        private const string PopupUxmlPath = "Packages/com.gdk.core/Scripts/UIModuleUITK/CommonScreen/NotificationPopup.uxml";
        private const string RootUxmlPath  = "Packages/com.cuvara.uitoolkit/Runtime/Managers/RootUIDocument.uxml";

        // The key the popup declares in its PopupInfo attribute. The whole point of the
        // backend contract is that this ONE key is what both backends address a screen by.
        private const string PopupKey = "UIPopupNoticeUITK";

        private GameObject     scopeObject;
        private GameObject     documentObject;
        private PanelSettings  panelSettings;
        private RootUIDocument rootUIDocument;
        private IScreenManager screenManager;

        [SetUp]
        public void SetUp()
        {
            // A UIDocument with no theme logs about it; that is a rendering concern and not
            // what these tests are about.
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (this.scopeObject != null) Object.DestroyImmediate(this.scopeObject);
            if (this.documentObject != null) Object.DestroyImmediate(this.documentObject);
            if (this.panelSettings != null) Object.DestroyImmediate(this.panelSettings);

            this.screenManager              = null;
            this.rootUIDocument             = null;
            LogAssert.ignoreFailingMessages = false;
        }

        #region The manager path

        [UnityTest]
        public IEnumerator OpenScreen_BuildsAUIToolkitPopupAndParentsItIntoTheOverlayLayer() => UniTask.ToCoroutine(async () =>
        {
            this.BuildScene();

            var okWasClicked = false;

            var presenter = await this.screenManager.OpenScreen<NotificationPopupUIToolkitPresenter, NotificationPopupModel>(new()
            {
                Title    = "Are you sure?",
                Content  = "Do you really want to quit?",
                Type     = NotificationType.Option,
                OkNoticeAction = () => okWasClicked = true,
            });

            Assert.That(presenter, Is.Not.Null, "OpenScreen returned no presenter.");
            Assert.That(presenter.View, Is.Not.Null, "The presenter never got a view — the backend path did not run.");

            // Built from the VisualTreeAsset, not from a prefab.
            Assert.That(presenter.View.TxtTitle.text, Is.EqualTo("Are you sure?"));
            Assert.That(presenter.View.TxtContent.text, Is.EqualTo("Do you really want to quit?"));

            // Option mode shows the two-button row and hides the one-button row.
            Assert.That(presenter.View.NoticeGroup.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(presenter.View.CloseGroup.style.display.value, Is.EqualTo(DisplayStyle.None));

            // Parented into the OVERLAY layer, because PopupInfo says isOverlay: true.
            Assert.That(presenter.View.Root.parent, Is.SameAs(this.rootUIDocument.RootUIOverlayElement),
                "The view is not in the overlay layer; MoveToActiveLayer did not run or picked the wrong layer.");

            Assert.That(presenter.ScreenStatus, Is.EqualTo(ScreenStatus.Opened));
            Assert.That(presenter.View.Root.style.opacity.value, Is.EqualTo(1f).Within(0.001f), "The view opened but is still transparent.");

            // The callback wired in BindData reaches the model.
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = presenter.View.BtnOkNotice;
                presenter.View.BtnOkNotice.SendEvent(submit);
            }

            Assert.That(okWasClicked, Is.True, "Activating OK did not reach NotificationPopupModel.OkNoticeAction.");
        });

        [UnityTest]
        public IEnumerator CloseView_ParksTheViewInTheClosedLayer() => UniTask.ToCoroutine(async () =>
        {
            this.BuildScene();

            var presenter = await this.screenManager.OpenScreen<NotificationPopupUIToolkitPresenter, NotificationPopupModel>(new()
            {
                Title   = "Title",
                Content = "Content",
                Type    = NotificationType.Close,
            });

            await presenter.CloseViewAsync();

            Assert.That(presenter.ScreenStatus, Is.EqualTo(ScreenStatus.Closed));

            // The reparent-on-close goes through the same MoveToHiddenLayer branch, driven
            // by ScreenCloseSignal — i.e. the manager's own event path, not a direct call.
            Assert.That(presenter.View.Root.parent, Is.SameAs(this.rootUIDocument.RootUIClosedElement),
                "A closed UI Toolkit screen was not parked in the closed layer.");
        });

        [UnityTest]
        public IEnumerator OpenScreen_Twice_ReusesTheSamePresenterAndView() => UniTask.ToCoroutine(async () =>
        {
            this.BuildScene();

            var model = new NotificationPopupModel { Title = "T", Content = "C", Type = NotificationType.Close };

            var first  = await this.screenManager.OpenScreen<NotificationPopupUIToolkitPresenter, NotificationPopupModel>(model);
            var second = await this.screenManager.OpenScreen<NotificationPopupUIToolkitPresenter, NotificationPopupModel>(model);

            Assert.That(second, Is.SameAs(first), "The manager rebuilt a screen it had already loaded.");
            Assert.That(second.View.Root.parent, Is.SameAs(this.rootUIDocument.RootUIOverlayElement));
        });

        #endregion

        #region Backend selection

        [Test]
        public void ViewType_IsRecoveredFromThePresentersGenericBase()
        {
            Assert.That(ScreenPresenterViewType.Of(typeof(NotificationPopupUIToolkitPresenter)), Is.EqualTo(typeof(NotificationPopupUIToolkitView)));
            Assert.That(ScreenPresenterViewType.Of(typeof(NotificationPopupPresenter)), Is.EqualTo(typeof(NotificationPopupUIView)));
        }

        [Test]
        public void ViewType_OfSomethingThatIsNotAScreenPresenter_IsNull()
        {
            // The manager reads null as "uGUI", which is the behaviour that existed before
            // any of this. Unknown must never become a new failure mode.
            Assert.That(ScreenPresenterViewType.Of(typeof(string)), Is.Null);
            Assert.That(ScreenPresenterViewType.Of(null), Is.Null);
        }

        [Test]
        public void Backend_HandlesSurfaceViewsAndLeavesUGuiViewsAlone()
        {
            var backend = new UIToolkitScreenViewBackend(new StubAssetsManager());

            Assert.That(backend.CanHandle(typeof(NotificationPopupUIToolkitView)), Is.True);
            Assert.That(backend.CanHandle(typeof(NotificationPopupUIView)), Is.False, "The UI Toolkit backend claimed a uGUI view.");
        }

        #endregion

        #region The factory

        [Test]
        public void Factory_BuildsTheViewAndFindsEveryElement()
        {
            var view = (NotificationPopupUIToolkitView)UIToolkitViewFactory.Create(typeof(NotificationPopupUIToolkitView), LoadUxml(PopupUxmlPath));

            Assert.That(view.TxtTitle, Is.Not.Null);
            Assert.That(view.TxtContent, Is.Not.Null);
            Assert.That(view.BtnOk, Is.Not.Null);
            Assert.That(view.BtnOkNotice, Is.Not.Null);
            Assert.That(view.BtnCancel, Is.Not.Null);
            Assert.That(view.NoticeGroup, Is.Not.Null);
            Assert.That(view.CloseGroup, Is.Not.Null);
        }

        [Test]
        public void Factory_RejectsAUGuiViewTypeByName()
        {
            var exception = Assert.Throws<ArgumentException>(() => UIToolkitViewFactory.Create(typeof(NotificationPopupUIView), LoadUxml(PopupUxmlPath)));

            Assert.That(exception.Message, Does.Contain(nameof(NotificationPopupUIView)));
        }

        [Test]
        public void RootDocumentUxml_CarriesTheThreeLayersRootUIDocumentLooksFor()
        {
            var root = LoadUxml(RootUxmlPath).CloneTree();

            Assert.That(root.Q<VisualElement>("root-ui-show"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("root-ui-closed"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("root-ui-overlay"), Is.Not.Null);
        }

        #endregion

        #region The surface

        [Test]
        public void Surface_ReparentsBetweenLayers()
        {
            var element = new VisualElement();
            var surface = new VisualElementViewSurface(element);
            var first   = new VisualElementViewLayer(new VisualElement());
            var second  = new VisualElementViewLayer(new VisualElement());

            surface.SetParent(first);
            Assert.That(element.parent, Is.SameAs(first.Element));

            surface.SetParent(second);
            Assert.That(element.parent, Is.SameAs(second.Element), "Reparenting left the element in its old layer.");
            Assert.That(first.Element.childCount, Is.Zero, "The element is in two layers at once.");
        }

        [Test]
        public void Surface_RefusesAUGuiLayer()
        {
            var surface = new VisualElementViewSurface(new VisualElement());

            Assert.Throws<InvalidOperationException>(() => surface.SetParent(new TransformViewLayer(new GameObject("uGUI").transform)));
        }

        #endregion

        #region Scene

        /// <summary>Builds the smallest scene a UI Toolkit screen can be opened in.</summary>
        private void BuildScene()
        {
            this.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            // Inactive first: RootUIDocument reads the document's rootVisualElement in
            // Awake, which needs the UIDocument already configured.
            this.documentObject = new GameObject(nameof(RootUIDocument));
            this.documentObject.SetActive(false);

            var uiDocument = this.documentObject.AddComponent<UIDocument>();
            uiDocument.panelSettings = this.panelSettings;
            uiDocument.visualTreeAsset = LoadUxml(RootUxmlPath);

            this.rootUIDocument = this.documentObject.AddComponent<RootUIDocument>();
            this.documentObject.SetActive(true);

            Assert.That(this.rootUIDocument.RootUIShowElement, Is.Not.Null, "RootUIDocument did not resolve its layers; the root document UXML did not load.");

            var assetsManager = new StubAssetsManager();
            assetsManager.Add(PopupKey, LoadUxml(PopupUxmlPath));

            // Same inactive-then-activate trick: LifetimeScope builds in Awake.
            this.scopeObject = new GameObject(nameof(TestSceneScope));
            this.scopeObject.SetActive(false);

            var scope = this.scopeObject.AddComponent<TestSceneScope>();

            scope.Installer = builder =>
            {
                builder.Register<GameFoundation.DI.VContainerWrapper>(Lifetime.Scoped).AsImplementedInterfaces();
                builder.RegisterSignalBus();
                builder.RegisterScreenManager();
                builder.RegisterUIToolkitViewBackend();
                builder.RegisterInstance<IAssetsManager>(assetsManager);
                builder.RegisterInstance<ILoggerManager>(new UnityLoggerManager(LogLevel.Info));
                builder.RegisterInstance<IAudioService>(new StubAudioService());
            };

            this.scopeObject.SetActive(true);

            this.screenManager = scope.Container.Resolve<IScreenManager>();
        }

        private static VisualTreeAsset LoadUxml(string path)
        {
            #if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            Assert.That(asset, Is.Not.Null, $"Could not load {path}.");
            return asset;
            #else
            Assert.Ignore("These tests load their UXML through the AssetDatabase and only run in the Editor.");
            return null;
            #endif
        }

        #endregion
    }
}
#endif
