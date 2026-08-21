namespace GameFoundation.Scripts.UIModule.UITK.Managers
{
    using System;
    using Cuvara.UIToolkit.Input;
    using Cuvara.UIToolkit.Managers;
    using GameFoundation.Scripts.UIModule.ScreenFlow.Managers;
    using UnityEngine.UIElements;

    /// <summary>
    /// The back/escape POLICY: what this framework does when the user presses Back.
    /// </summary>
    /// <remarks>
    /// <para><b>This is the host half of a deliberate split.</b> Detecting the press lives
    /// in <c>com.cuvara.uitoolkit</c> as <see cref="BackNavigationSource"/>, which registers
    /// for <c>NavigationCancelEvent</c> and raises a plain C# event. Deciding what the press
    /// MEANS lives here, because "what does Back do" has a different answer in every
    /// application and a package that answered it would be imposing one host's screen flow
    /// on every other host. The package knows nothing about
    /// <see cref="IScreenManager"/>; this class is where the two meet.</para>
    ///
    /// <para><b>What a cancel event covers.</b> UI Toolkit raises it from whichever input
    /// backend is active, so Escape, gamepad B and the Android back button all arrive the
    /// same way — three inputs the legacy <c>Input.GetKeyDown(KeyCode.Escape)</c> poll in
    /// <c>ScreenManager.Tick</c> covered exactly one of, and only on desktop. That poll is
    /// still there, still works where the legacy Input Manager exists, and is compiled out
    /// where it does not: with Active Input Handling set to "Input System Package (New)",
    /// <c>UnityEngine.Input</c> throws rather than returning false.</para>
    ///
    /// <para><b>Not registered anywhere by default.</b> Construct it from the scope that
    /// owns the <see cref="RootUIDocument"/> and dispose it with that scope. Back-to-close
    /// is opt-in on the uGUI side too — <c>enableBackToClose</c> defaults to false, and has
    /// never had a call site — so switching it on for every project that adopts the UI
    /// Toolkit backend would be a behaviour change smuggled in with a rendering one.</para>
    /// </remarks>
    public sealed class UIToolkitBackNavigation : IDisposable
    {
        private readonly IScreenManager       screenManager;
        private readonly BackNavigationSource source;

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
        public bool Enabled { get => this.source.Enabled; set => this.source.Enabled = value; }

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

        /// <summary>Binds a screen manager to a cancel source registered on <paramref name="root"/>.</summary>
        public UIToolkitBackNavigation(IScreenManager screenManager, VisualElement root)
        {
            this.screenManager = screenManager ?? throw new ArgumentNullException(nameof(screenManager));

            if (root == null) throw new ArgumentNullException(nameof(root));

            this.source = new(root);
            this.source.BackRequested += this.OnBackRequested;
        }

        /// <summary>Registers against the three-layer root of a <see cref="RootUIDocument"/>.</summary>
        public UIToolkitBackNavigation(IScreenManager screenManager, RootUIDocument rootUIDocument)
            : this(screenManager, RootElementOf(rootUIDocument))
        {
        }

        private static VisualElement RootElementOf(RootUIDocument rootUIDocument)
        {
            if (rootUIDocument == null) throw new ArgumentNullException(nameof(rootUIDocument));

            var element = rootUIDocument.RootVisualElement;

            if (element == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(RootUIDocument)} on {rootUIDocument.gameObject.name} has no rootVisualElement, so there is "
                    + "nothing to register a cancel handler on. Is a source asset assigned to the UIDocument?");
            }

            return element;
        }

        private void OnBackRequested()
        {
            if (this.disposed) return;

            // Nothing open means nothing to go back from. Without this, back at an empty
            // screen stack opens the quit confirmation over whatever non-UI scene is
            // showing — which the uGUI path does too, but only because it could never be
            // reached with an empty stack in the first place.
            if (this.screenManager.ActiveScreenCount == 0) return;

            ++this.HandledCount;

            if (this.BackAction != null)
                this.BackAction.Invoke();
            else
                this.screenManager.HandleBackNavigation();
        }

        public void Dispose()
        {
            if (this.disposed) return;
            this.disposed = true;

            this.source.BackRequested -= this.OnBackRequested;
            this.source.Dispose();
        }
    }
}
