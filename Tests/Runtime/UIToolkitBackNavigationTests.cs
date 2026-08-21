#if GDK_VCONTAINER
namespace GameFoundation.UIModule.UITK.Tests
{
    using System;
    using System.Collections;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.CommonScreen;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.Managers;
    using GameFoundation.Scripts.UIModule.UITK;
    using GameFoundation.Scripts.UIModule.UITK.CommonScreen;
    using Cuvara.UIToolkit.Managers;
    using GameFoundation.Scripts.UIModule.UITK.Managers;
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
    /// The UI Toolkit back/escape path, and the evidence about the uGUI one it replaces.
    /// </summary>
    /// <remarks>
    /// <para><b>What these tests DO cover:</b> that a <c>NavigationCancelEvent</c> arriving
    /// at the panel root drives the screen flow — closing the top screen when there is one
    /// underneath, running the configured root action when there is not — and that every
    /// path which should do nothing does nothing.</para>
    ///
    /// <para><b>What they DELIBERATELY do not cover, and cannot:</b> that a physical Android
    /// back press, a gamepad B, or an Escape key actually produces a
    /// <c>NavigationCancelEvent</c> at that root. That is Unity's input backend and its
    /// focus-controller routing, it needs a device or a live input system with focus, and
    /// asserting it here would mean asserting on a mock of Unity rather than on Unity. The
    /// events below are sent directly at the root.</para>
    /// </remarks>
    public class UIToolkitBackNavigationTests
    {
        private const string PopupUxmlPath = "Packages/com.gdk.core/Scripts/UIModuleUITK/CommonScreen/NotificationPopup.uxml";
        private const string RootUxmlPath  = "Packages/com.cuvara.uitoolkit/Runtime/Managers/RootUIDocument.uxml";

        private const string PopupKey  = "UIPopupNoticeUITK";
        private const string SecondKey = "UITestSecondScreen";

        private GameObject     scopeObject;
        private GameObject     documentObject;
        private PanelSettings  panelSettings;
        private RootUIDocument rootUIDocument;
        private IScreenManager screenManager;

        private UIToolkitBackNavigation backNavigation;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            this.backNavigation?.Dispose();
            this.backNavigation = null;

            if (this.scopeObject != null) Object.DestroyImmediate(this.scopeObject);
            if (this.documentObject != null) Object.DestroyImmediate(this.documentObject);
            if (this.panelSettings != null) Object.DestroyImmediate(this.panelSettings);

            this.screenManager              = null;
            this.rootUIDocument             = null;
            LogAssert.ignoreFailingMessages = false;
        }

        #region The legacy path this replaces

        [Test]
        public void TheLegacyEscapePoll_IsCompiledOutWhereLegacyInputIsUnavailable()
        {
            // The evidence, asserted rather than asserted-about. ENABLE_LEGACY_INPUT_MANAGER
            // is defined by Unity when Active Input Handling is "Input Manager (Old)" or
            // "Both", and undefined when it is "Input System Package (New)" — which is what
            // this project has (ProjectSettings.asset: activeInputHandler: 1).
            //
            // Where the define is absent, UnityEngine.Input throws on every call, and
            // ScreenManager.Tick evaluates Input.GetKeyDown BEFORE the enableBackToClose
            // short-circuit — so the uGUI back flow was not merely switched off, it was a
            // per-frame exception waiting for ScreenManager to be ticked. It is ticked:
            // RegisterScreenManager registers it AsImplementedInterfaces, which includes
            // ITickable.
            #if ENABLE_LEGACY_INPUT_MANAGER
            Assert.DoesNotThrow(() => Input.GetKeyDown(KeyCode.Escape),
                "ENABLE_LEGACY_INPUT_MANAGER is defined, so legacy Input must work and the uGUI escape path is live.");
            #else
            Assert.Throws<InvalidOperationException>(() => Input.GetKeyDown(KeyCode.Escape),
                "ENABLE_LEGACY_INPUT_MANAGER is undefined, so legacy Input must throw — which is why Tick is guarded.");
            #endif
        }

        [Test]
        public void TickDoesNotThrow_WhateverTheInputHandlingIs()
        {
            // The regression guard for the fix: whatever Active Input Handling the consuming
            // project is set to, ticking the manager must be safe.
            this.BuildScene();

            var tickable = (GameFoundation.DI.ITickable)this.screenManager;

            Assert.DoesNotThrow(() => tickable.Tick());
        }

        [Test]
        public void BackToClose_IsOffUntilSomebodyTurnsItOn()
        {
            // enableBackToClose has never had a call site anywhere; this pins the default so
            // that adding the UI Toolkit path does not quietly switch the uGUI one on.
            this.BuildScene();

            Assert.That(this.screenManager.IsBackToCloseEnabled, Is.False);

            this.screenManager.EnableBackToClose(true);
            Assert.That(this.screenManager.IsBackToCloseEnabled, Is.True);
        }

        #endregion

        #region The UI Toolkit path

        [UnityTest]
        public IEnumerator Cancel_WithTwoScreensOpen_ClosesTheTopOne() => UniTask.ToCoroutine(async () =>
        {
            this.BuildScene();

            await this.OpenNotificationPopup();
            var second = await this.screenManager.OpenScreen<SecondUIToolkitPresenter>();

            Assert.That(this.screenManager.ActiveScreenCount, Is.EqualTo(2), "precondition: two screens open");
            Assert.That(second.ScreenStatus, Is.EqualTo(ScreenStatus.Opened), "precondition: the second screen is open");

            this.backNavigation = new(this.screenManager, this.rootUIDocument);

            this.SendCancel();

            await UniTask.DelayFrame(3);

            Assert.That(this.backNavigation.HandledCount, Is.EqualTo(1));
            Assert.That(second.ScreenStatus, Is.Not.EqualTo(ScreenStatus.Opened), "the top screen should have been closed");
        });

        [UnityTest]
        public IEnumerator Cancel_WithOneScreenOpen_RunsTheConfiguredBackAction() => UniTask.ToCoroutine(async () =>
        {
            this.BuildScene();

            await this.OpenNotificationPopup();

            Assert.That(this.screenManager.ActiveScreenCount, Is.EqualTo(1), "precondition: one screen open");

            var backActionRan = 0;

            this.backNavigation = new(this.screenManager, this.rootUIDocument) { BackAction = () => ++backActionRan };

            this.SendCancel();

            await UniTask.DelayFrame(1);

            // BackAction exists precisely so a UI-Toolkit-only project never reaches the
            // manager's default root action, which opens the uGUI quit popup and therefore
            // needs a prefab and a RootUICanvas that such a project does not have.
            Assert.That(backActionRan, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator Cancel_TwiceInARow_IsHandledTwice() => UniTask.ToCoroutine(async () =>
        {
            this.BuildScene();

            await this.OpenNotificationPopup();

            var ran = 0;
            this.backNavigation = new(this.screenManager, this.rootUIDocument) { BackAction = () => ++ran };

            this.SendCancel();
            this.SendCancel();

            await UniTask.DelayFrame(1);

            Assert.That(ran, Is.EqualTo(2));
        });

        [UnityTest]
        public IEnumerator TheEventIsConsumed_SoNothingUnderneathAlsoHandlesIt() => UniTask.ToCoroutine(async () =>
        {
            this.BuildScene();

            await this.OpenNotificationPopup();

            var reachedTheLayer = 0;
            this.rootUIDocument.RootUIOverlayElement.RegisterCallback<NavigationCancelEvent>(_ => ++reachedTheLayer);

            this.backNavigation = new(this.screenManager, this.rootUIDocument) { BackAction = () => { } };

            // Aimed at a DESCENDANT of the root, so the root's trickle-down callback runs
            // first and the layer's callback would run second — if the event were still
            // propagating.
            this.SendCancel(this.rootUIDocument.RootUIOverlayElement);

            await UniTask.DelayFrame(1);

            Assert.That(this.backNavigation.HandledCount, Is.EqualTo(1), "precondition: the handler ran");
            Assert.That(reachedTheLayer, Is.EqualTo(0), "a consumed cancel must not keep trickling down");
        });

        #endregion

        #region Everything that must do nothing

        [UnityTest]
        public IEnumerator Cancel_WithNothingOpen_DoesNothing() => UniTask.ToCoroutine(async () =>
        {
            this.BuildScene();

            var ran = 0;
            this.backNavigation = new(this.screenManager, this.rootUIDocument) { BackAction = () => ++ran };

            Assert.That(this.screenManager.ActiveScreenCount, Is.EqualTo(0), "precondition: nothing open");

            this.SendCancel();

            await UniTask.DelayFrame(1);

            Assert.That(ran, Is.EqualTo(0));
            Assert.That(this.backNavigation.HandledCount, Is.EqualTo(0));
        });

        [UnityTest]
        public IEnumerator Cancel_WhileDisabled_DoesNothing() => UniTask.ToCoroutine(async () =>
        {
            this.BuildScene();

            await this.OpenNotificationPopup();

            var ran = 0;
            this.backNavigation = new(this.screenManager, this.rootUIDocument) { BackAction = () => ++ran, Enabled = false };

            this.SendCancel();

            await UniTask.DelayFrame(1);

            Assert.That(ran, Is.EqualTo(0));
        });

        [UnityTest]
        public IEnumerator Cancel_AfterDispose_DoesNothing() => UniTask.ToCoroutine(async () =>
        {
            this.BuildScene();

            await this.OpenNotificationPopup();

            var ran = 0;
            this.backNavigation = new(this.screenManager, this.rootUIDocument) { BackAction = () => ++ran };

            this.backNavigation.Dispose();

            this.SendCancel();

            await UniTask.DelayFrame(1);

            Assert.That(ran, Is.EqualTo(0));
        });

        [UnityTest]
        public IEnumerator Dispose_IsIdempotent() => UniTask.ToCoroutine(async () =>
        {
            this.BuildScene();

            this.backNavigation = new(this.screenManager, this.rootUIDocument);

            Assert.DoesNotThrow(() =>
            {
                this.backNavigation.Dispose();
                this.backNavigation.Dispose();
            });

            await UniTask.CompletedTask;
        });

        [Test]
        public void ConstructingWithNoScreenManager_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new UIToolkitBackNavigation(null, new VisualElement()));
        }

        [Test]
        public void ConstructingWithNoRootElement_Throws()
        {
            this.BuildScene();

            Assert.Throws<ArgumentNullException>(() => new UIToolkitBackNavigation(this.screenManager, (VisualElement)null));
        }

        [Test]
        public void ConstructingWithNoRootUIDocument_Throws()
        {
            this.BuildScene();

            Assert.Throws<ArgumentNullException>(() => new UIToolkitBackNavigation(this.screenManager, (RootUIDocument)null));
        }

        #endregion

        #region ActiveScreenCount

        [UnityTest]
        public IEnumerator ActiveScreenCount_TracksOpensAndCloses() => UniTask.ToCoroutine(async () =>
        {
            // The number the back handler branches on. If it did not move, back would
            // always take the root branch and never close anything.
            this.BuildScene();

            Assert.That(this.screenManager.ActiveScreenCount, Is.EqualTo(0));

            await this.OpenNotificationPopup();
            Assert.That(this.screenManager.ActiveScreenCount, Is.EqualTo(1));

            var second = await this.screenManager.OpenScreen<SecondUIToolkitPresenter>();
            Assert.That(this.screenManager.ActiveScreenCount, Is.EqualTo(2));

            await second.CloseViewAsync();
            await UniTask.DelayFrame(2);

            Assert.That(this.screenManager.ActiveScreenCount, Is.EqualTo(1));
        });

        #endregion

        #region Scene

        private UniTask<NotificationPopupUIToolkitPresenter> OpenNotificationPopup()
        {
            return this.screenManager.OpenScreen<NotificationPopupUIToolkitPresenter, NotificationPopupModel>(new()
            {
                Title   = "Are you sure?",
                Content = "Back navigation test.",
                Type    = NotificationType.Close,
            });
        }

        /// <summary>Synthesises a cancel aimed at <paramref name="target"/>, or at the root.</summary>
        /// <remarks>
        /// Setting <c>target</c> and calling <c>SendEvent</c> is the documented way to
        /// synthesise a UI Toolkit event. What is being synthesised is only the DISPATCH —
        /// that a cancel exists and is aimed somewhere; that a real back press produces one
        /// is Unity's input backend, and is not asserted anywhere in this file.
        /// </remarks>
        private void SendCancel(VisualElement target = null)
        {
            var root = this.rootUIDocument.UIDocument.rootVisualElement;

            using var evt = NavigationCancelEvent.GetPooled();
            evt.target = target ?? root;
            root.SendEvent(evt);
        }

        private void BuildScene()
        {
            this.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            this.documentObject = new GameObject(nameof(RootUIDocument));
            this.documentObject.SetActive(false);

            var uiDocument = this.documentObject.AddComponent<UIDocument>();
            uiDocument.panelSettings   = this.panelSettings;
            uiDocument.visualTreeAsset = LoadUxml(RootUxmlPath);

            this.rootUIDocument = this.documentObject.AddComponent<RootUIDocument>();
            this.documentObject.SetActive(true);

            Assert.That(this.rootUIDocument.RootUIShowElement, Is.Not.Null, "RootUIDocument did not resolve its layers.");

            var assetsManager = new StubAssetsManager();
            var popupUxml     = LoadUxml(PopupUxmlPath);

            // The second screen only needs SOME visual tree to clone; what it draws is not
            // what is being tested, its presence in activeScreens is.
            assetsManager.Add(PopupKey, popupUxml);
            assetsManager.Add(SecondKey, popupUxml);

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
