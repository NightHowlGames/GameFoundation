namespace GameFoundation.Scripts.UIModule.UITK.Managers
{
    using System;
    using GameFoundation.Scripts.UIModule.ScreenFlow.Managers;
    using UnityEngine.UIElements;

    /// <summary>
    /// The UI Toolkit back/escape path: a <see cref="NavigationCancelEvent"/> on a panel
    /// root drives <see cref="IScreenManager.HandleBackNavigation"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Why an event and not a poll.</b> The uGUI path is
    /// <c>Input.GetKeyDown(KeyCode.Escape)</c> in <c>ScreenManager.Tick</c>, which is legacy
    /// Input Manager. The consuming project's <c>ProjectSettings.asset</c> has
    /// <c>activeInputHandler: 1</c> — Input System package only — where legacy
    /// <c>UnityEngine.Input</c> throws rather than returning false, so that path is not
    /// merely disabled, it is unusable. It is left in place and compiled behind
    /// <c>ENABLE_LEGACY_INPUT_MANAGER</c>; this is the path that works here.</para>
    ///
    /// <para><b>What a cancel event covers.</b> UI Toolkit raises
    /// <c>NavigationCancelEvent</c> from the active input backend, so Escape, gamepad B and
    /// the Android back button all arrive as the same event — three inputs the uGUI poll
    /// covered exactly one of, and only on desktop.</para>
    ///
    /// <para><b>Where the callback is registered, and the caveat.</b> On the element handed
    /// in, with <c>TrickleDown</c>, so a cancel aimed at any focused descendant passes
    /// through the root first and is handled once regardless of what has focus. Navigation
    /// events are routed by the panel's focus controller; if the panel has no focused
    /// element at all, whether the event reaches the root is Unity's dispatch behaviour and
    /// not something this class can assert. That routing is NOT verified here — the tests
    /// send the event at the root and assert this class's reaction to it. Verifying that a
    /// real Android back press produces a <c>NavigationCancelEvent</c> needs a device.</para>
    ///
    /// <para><b>Not registered anywhere by default.</b> Construct it from the scope that owns
    /// the <see cref="RootUIDocument"/> and dispose it with that scope. It is deliberately
    /// not wired into <see cref="RootUIDocument"/>: back-to-close is opt-in on the uGUI side
    /// too (<c>enableBackToClose</c> defaults to false) and turning it on for every project
    /// that adopts the UI Toolkit backend would be a behaviour change smuggled in with a
    /// rendering one.</para>
    /// </remarks>
    public sealed class UIToolkitBackNavigation : IDisposable
    {
        private readonly IScreenManager screenManager;
        private readonly VisualElement  root;

        private bool disposed;

        /// <summary>
        /// Gate for this handler alone, defaulting to true.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="IScreenManager.IsBackToCloseEnabled"/> on purpose:
        /// constructing this class IS the opt-in, so requiring a second flag to be set as
        /// well would just be a way to have it silently do nothing. Set it false to suspend
        /// back handling — during a cutscene, say — without tearing the registration down.
        /// </remarks>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Runs instead of <see cref="IScreenManager.HandleBackNavigation"/> when set.
        /// </summary>
        /// <remarks>
        /// The manager's default root action opens <c>NotificationPopupPresenter</c> — the
        /// uGUI quit confirmation, which needs a uGUI prefab and a <c>RootUICanvas</c>. A
        /// project running UI Toolkit only has neither, so it needs a way to say what back
        /// means at the root (most likely <c>NotificationPopupUIToolkitPresenter</c>)
        /// without the manager having to know which backend is installed.
        /// </remarks>
        public Action BackAction { get; set; }

        /// <summary>How many cancel events this handler has acted on. For tests and telemetry.</summary>
        public int HandledCount { get; private set; }

        public UIToolkitBackNavigation(IScreenManager screenManager, VisualElement root)
        {
            this.screenManager = screenManager ?? throw new ArgumentNullException(nameof(screenManager));
            this.root          = root ?? throw new ArgumentNullException(nameof(root));

            this.root.RegisterCallback<NavigationCancelEvent>(this.OnNavigationCancel, TrickleDown.TrickleDown);
        }

        /// <summary>Registers against the three-layer root of a <see cref="RootUIDocument"/>.</summary>
        public UIToolkitBackNavigation(IScreenManager screenManager, RootUIDocument rootUIDocument)
            : this(screenManager, RootElementOf(rootUIDocument))
        {
        }

        private static VisualElement RootElementOf(RootUIDocument rootUIDocument)
        {
            if (rootUIDocument == null) throw new ArgumentNullException(nameof(rootUIDocument));

            var element = rootUIDocument.UIDocument == null ? null : rootUIDocument.UIDocument.rootVisualElement;

            if (element == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(RootUIDocument)} on {rootUIDocument.gameObject.name} has no rootVisualElement, so there is "
                    + "nothing to register a cancel handler on. Is a source asset assigned to the UIDocument?");
            }

            return element;
        }

        private void OnNavigationCancel(NavigationCancelEvent evt)
        {
            if (this.disposed || !this.Enabled) return;

            // Nothing open means nothing to go back from. Without this, back at an empty
            // screen stack opens the quit confirmation over whatever non-UI scene is
            // showing — which the uGUI path does too, but only because it could never be
            // reached with an empty stack in the first place.
            if (this.screenManager.ActiveScreenCount == 0) return;

            ++this.HandledCount;

            // Stop here rather than let it keep trickling: the event has been consumed by
            // the screen flow, and letting it reach a focused Button underneath would
            // dismiss two things for one press.
            evt.StopPropagation();

            if (this.BackAction != null)
                this.BackAction.Invoke();
            else
                this.screenManager.HandleBackNavigation();
        }

        public void Dispose()
        {
            if (this.disposed) return;
            this.disposed = true;

            this.root.UnregisterCallback<NavigationCancelEvent>(this.OnNavigationCancel, TrickleDown.TrickleDown);
        }
    }
}
