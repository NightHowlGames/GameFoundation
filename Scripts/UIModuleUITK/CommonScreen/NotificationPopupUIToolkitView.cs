namespace GameFoundation.Scripts.UIModule.UITK.CommonScreen
{
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.CommonScreen;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.UITK.Presenter;
    using GameFoundation.Scripts.UIModule.UITK.View;
    using GameFoundation.Scripts.Utilities;
    using GameFoundation.Signals;
    using UniT.Logging;
    using UnityEngine.Scripting;
    using UnityEngine.UIElements;

    /// <summary>
    /// The UI Toolkit form of <see cref="NotificationPopupUIView"/>, built from
    /// <c>NotificationPopup.uxml</c>.
    /// </summary>
    /// <remarks>
    /// It lives ALONGSIDE the uGUI view, which is untouched — coexistence of the two
    /// backends behind one seam is the point of the port, not replacement. The model
    /// (<see cref="NotificationPopupModel"/>) and the mode enum
    /// (<see cref="NotificationType"/>) are reused from the uGUI screen rather than
    /// duplicated: neither has anything backend-shaped in it.
    ///
    /// <para>Elements are found with UQuery once, in the constructor, and held. Runtime
    /// data binding exists in 6000.3 and would replace the text assignments below, but
    /// mixing it in here would make this diff about two mechanisms at once; the seam is
    /// what is being proven.</para>
    ///
    /// <para>The uGUI view's two <c>GameObject</c>s toggled with <c>SetActive</c> become
    /// two <c>VisualElement</c>s toggled with <c>display</c>. <c>display: none</c> is the
    /// right analogue and <c>visibility: hidden</c> is not: a hidden element still takes
    /// its space in the layout, so the panel would keep a gap the size of the button row
    /// that is not showing.</para>
    /// </remarks>
    public class NotificationPopupUIToolkitView : BaseUIToolkitView
    {
        public Label  TxtTitle    { get; }
        public Label  TxtContent  { get; }
        public Button BtnOk       { get; }
        public Button BtnOkNotice { get; }
        public Button BtnCancel   { get; }

        /// <summary>The Option-mode button row — the uGUI view's <c>noticeObj</c>.</summary>
        public VisualElement NoticeGroup { get; }

        /// <summary>The Close-mode button row — the uGUI view's <c>closeObj</c>.</summary>
        public VisualElement CloseGroup { get; }

        public NotificationPopupUIToolkitView(VisualTreeAsset visualTreeAsset) : base(visualTreeAsset)
        {
            // CloneTree returns a TemplateContainer, which is a plain flex item with no
            // size of its own. The screen flow parents it into a layer and expects it to
            // cover that layer, so stretch it here rather than asking every layer's USS to
            // know about it.
            this.Root.style.position = Position.Absolute;
            this.Root.style.left     = 0;
            this.Root.style.top      = 0;
            this.Root.style.right    = 0;
            this.Root.style.bottom   = 0;

            this.TxtTitle    = this.Root.Q<Label>("txt-title");
            this.TxtContent  = this.Root.Q<Label>("txt-content");
            this.BtnOk       = this.Root.Q<Button>("btn-ok");
            this.BtnOkNotice = this.Root.Q<Button>("btn-ok-notice");
            this.BtnCancel   = this.Root.Q<Button>("btn-cancel");
            this.NoticeGroup = this.Root.Q<VisualElement>("notice-group");
            this.CloseGroup  = this.Root.Q<VisualElement>("close-group");
        }

        /// <summary>Sets the two texts. The uGUI presenter assigned <c>.text</c> directly.</summary>
        public void SetContent(string title, string content)
        {
            this.TxtTitle.text   = title;
            this.TxtContent.text = content;
        }

        /// <summary>Shows the button row for <paramref name="type"/> and hides the other.</summary>
        public void SetMode(NotificationType type)
        {
            SetDisplayed(this.NoticeGroup, type == NotificationType.Option);
            SetDisplayed(this.CloseGroup, type == NotificationType.Close);
        }

        private static void SetDisplayed(VisualElement element, bool displayed)
        {
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>
    /// The UI Toolkit notification popup presenter — the same screen, on the other backend.
    /// </summary>
    /// <remarks>
    /// The only difference from <c>NotificationPopupPresenter</c> is the base class:
    /// <see cref="BaseUIToolkitPopupPresenter{TView, TModel}"/> instead of
    /// <c>BasePopupPresenter</c>. Both run the identical popup flow — the same status
    /// machine and the same <c>PopupShowedSignal</c> / <c>PopupHiddenSignal</c> fires —
    /// because both inherit it from <c>BaseScreenPresenterCore</c>.
    ///
    /// <para>Openable through <c>ScreenManager</c> like any other screen. The manager
    /// picks the UI Toolkit construction path off this presenter's view type and builds
    /// the view from the <c>VisualTreeAsset</c> at <c>UIPopupNoticeUITK</c>; that needs
    /// <c>RegisterUIToolkitViewBackend()</c> called on the scope and a
    /// <c>RootUIDocument</c> in the scene. See <c>Scripts/UIModuleUITK/README.md</c>.</para>
    /// </remarks>
    [PopupInfo("UIPopupNoticeUITK", true, false, true)]
    public class NotificationPopupUIToolkitPresenter : BaseUIToolkitPopupPresenter<NotificationPopupUIToolkitView, NotificationPopupModel>
    {
        private readonly IAudioService audioManager;

        [Preserve]
        public NotificationPopupUIToolkitPresenter(SignalBus signalBus, ILoggerManager loggerManager, IAudioService audioManager) : base(signalBus, loggerManager)
        {
            this.audioManager = audioManager;
        }

        public override UniTask BindData(NotificationPopupModel popupModel)
        {
            this.RegisterCallbacks();
            this.View.SetContent(popupModel.Title, popupModel.Content);
            this.View.SetMode(popupModel.Type);
            return UniTask.CompletedTask;
        }

        private void RegisterCallbacks()
        {
            // Unregister first: BindData runs again on every re-open of an already-opened
            // popup, and `clicked` is a plain multicast delegate — adding without removing
            // would fire the handler once per open. (The uGUI view is spared this only
            // because Dispose happens to remove two of its three listeners.)
            this.UnregisterCallbacks();

            this.View.BtnOk.clicked       += this.OkAction;
            this.View.BtnOkNotice.clicked += this.OkNoticeAction;
            this.View.BtnCancel.clicked   += this.CloseView;
        }

        private void UnregisterCallbacks()
        {
            this.View.BtnOk.clicked       -= this.OkAction;
            this.View.BtnOkNotice.clicked -= this.OkNoticeAction;
            this.View.BtnCancel.clicked   -= this.CloseView;
        }

        public override void CloseView()
        {
            this.audioManager.PlaySound("button_click");
            base.CloseView();
            this.Model.CloseAction?.Invoke();
            this.Model.CancelAction?.Invoke();
        }

        private void OkAction()
        {
            this.audioManager.PlaySound("button_click");
            this.CloseView();
            this.Model.OkAction?.Invoke();
        }

        private void OkNoticeAction()
        {
            this.audioManager.PlaySound("button_click");
            this.CloseView();
            this.Model.OkNoticeAction?.Invoke();
        }

        public override void Dispose()
        {
            base.Dispose();
            if (this.View != null) this.UnregisterCallbacks();
        }
    }
}
