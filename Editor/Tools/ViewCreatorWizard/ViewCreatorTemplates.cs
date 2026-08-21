namespace GameFoundation.Editor.Tools.ViewCreatorWizard
{
    /// <summary>
    /// The code the wizard writes to disk.
    /// </summary>
    /// <remarks>
    /// <para><b>These templates were emitting code that does not compile.</b> Every one of
    /// them predates two migrations that landed in this package and were never followed
    /// through to here, so the wizard's output was stale in four separate ways:</para>
    /// <list type="bullet">
    /// <item><c>using Zenject;</c> — Zenject was removed (commit "Remove zenject").
    /// <c>SignalBus</c> now lives in <c>GameFoundation.Signals</c>; the project is on
    /// VContainer.</item>
    /// <item><c>ILogService</c> from <c>GameFoundation.Scripts.Utilities.LogService</c> —
    /// that namespace is gone. Presenters take <c>UniT.Logging.ILoggerManager</c>.</item>
    /// <item><c>public override void BindData(...)</c> — <c>BindData</c> has returned
    /// <c>UniTask</c> on the screen and popup presenters for some time, so the generated
    /// override did not match the abstract member it was overriding.</item>
    /// <item>The non-model popup and screen templates called <c>base(signalBus)</c>, and
    /// the item template called <c>base(assetsManager)</c>. Neither of those constructors
    /// exists: the screen and popup bases take <c>(SignalBus, ILoggerManager)</c> and
    /// <c>BaseUIItemPresenter&lt;TView, TModel&gt;</c> takes nothing at all.</item>
    /// </list>
    ///
    /// <para><b>Two backends, both generated.</b> The uGUI templates keep emitting
    /// <c>BaseView</c> subclasses and are paired with a prefab, exactly as before — that
    /// path is permanent, not a migration staging post. The UI Toolkit templates emit a
    /// <c>BaseUIToolkitView</c> plus a starter <c>.uxml</c> instead of a prefab, and are
    /// modelled on <c>NotificationPopupUIToolkitView</c>, which is the one UI Toolkit
    /// screen in this package that is known to run: same <c>(VisualTreeAsset)</c>
    /// constructor, same stretch-to-layer block, same UQuery-in-the-constructor shape.</para>
    ///
    /// <para><c>[Preserve]</c> is emitted on every generated constructor. Presenters are
    /// resolved reflectively through the container and are otherwise a prime candidate for
    /// the managed stripper on IL2CPP targets; the hand-written presenters in this package
    /// all carry it.</para>
    /// </remarks>
    public partial class ViewCreatorWizard
    {
        #region uGUI

        private const string ITEM_VIEW_TEMPLATE =
            @"namespace X_NAME_SPACE
{
    using GameFoundation.Scripts.UIModule.MVP;

    public class X_MODEL_NAME
    {
    }

    public class X_VIEW_NAME : TViewMono
    {
    }

    public class X_PRESENTER_NAME : BaseUIItemPresenter<X_VIEW_NAME, X_MODEL_NAME>
    {
        public override void BindData(X_MODEL_NAME param) { }
    }
}";

        private const string POPUP_VIEW_TEMPLATE =
            @"namespace X_NAME_SPACE
{
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Signals;
    using UniT.Logging;
    using UnityEngine.Scripting;

    public class X_MODEL_NAME
    {
    }

    public class X_VIEW_NAME : BaseView
    {
    }

    [PopupInfo(nameof(X_VIEW_NAME))]
    public class X_PRESENTER_NAME : BasePopupPresenter<X_VIEW_NAME, X_MODEL_NAME>
    {
        [Preserve]
        public X_PRESENTER_NAME(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager) { }

        public override UniTask BindData(X_MODEL_NAME popupModel)
        {
            return UniTask.CompletedTask;
        }
    }
}";

        private const string POPUP_VIEW_NON_MODEL_TEMPLATE =
            @"namespace X_NAME_SPACE
{
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Signals;
    using UniT.Logging;
    using UnityEngine.Scripting;

    public class X_VIEW_NAME : BaseView
    {
    }

    [PopupInfo(nameof(X_VIEW_NAME))]
    public class X_PRESENTER_NAME : BasePopupPresenter<X_VIEW_NAME>
    {
        [Preserve]
        public X_PRESENTER_NAME(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager) { }

        public override UniTask BindData()
        {
            return UniTask.CompletedTask;
        }
    }
}";

        private const string SCREEN_VIEW_TEMPLATE =
            @"namespace X_NAME_SPACE
{
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Signals;
    using UniT.Logging;
    using UnityEngine.Scripting;

    public class X_MODEL_NAME
    {
    }

    public class X_VIEW_NAME : BaseView
    {
    }

    [ScreenInfo(nameof(X_VIEW_NAME))]
    public class X_PRESENTER_NAME : BaseScreenPresenter<X_VIEW_NAME, X_MODEL_NAME>
    {
        [Preserve]
        public X_PRESENTER_NAME(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager) { }

        public override UniTask BindData(X_MODEL_NAME screenModel)
        {
            return UniTask.CompletedTask;
        }
    }
}";

        private const string SCREEN_VIEW_NON_MODEL_TEMPLATE =
            @"namespace X_NAME_SPACE
{
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Signals;
    using UniT.Logging;
    using UnityEngine.Scripting;

    public class X_VIEW_NAME : BaseView
    {
    }

    [ScreenInfo(nameof(X_VIEW_NAME))]
    public class X_PRESENTER_NAME : BaseScreenPresenter<X_VIEW_NAME>
    {
        [Preserve]
        public X_PRESENTER_NAME(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager) { }

        public override UniTask BindData()
        {
            return UniTask.CompletedTask;
        }
    }
}";

        #endregion

        #region UI Toolkit

        // The UI Toolkit view has no [SerializeField]s and no Awake, so the elements are
        // found in the constructor and held — the shape NotificationPopupUIToolkitView
        // uses. StretchToParent() is there for the same reason it is there: CloneTree
        // returns a TemplateContainer with no size of its own, and the screen flow parents
        // it into a layer expecting it to fill that layer.
        //
        // The view base now comes from com.cuvara.uitoolkit, which knows nothing about this
        // framework's screen flow. ISurfaceScreenView is what bridges the two, and it adds
        // no member the package base does not already have — so declaring it costs the
        // generated view nothing but the name.

        private const string SCREEN_VIEW_UITK_TEMPLATE =
            @"namespace X_NAME_SPACE
{
    using Cuvara.UIToolkit.View;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Scripts.UIModule.UITK.Presenter;
    using GameFoundation.Signals;
    using UniT.Logging;
    using UnityEngine.Scripting;
    using UnityEngine.UIElements;

    public class X_MODEL_NAME
    {
    }

    public class X_VIEW_NAME : BaseUIToolkitView, ISurfaceScreenView
    {
        public Label Title { get; }

        // The backend builds every UI Toolkit view through this exact constructor.
        // Changing its signature makes the view unconstructable at runtime.
        public X_VIEW_NAME(VisualTreeAsset visualTreeAsset) : base(visualTreeAsset)
        {
            this.StretchToParent();

            this.Title = this.Root.Q<Label>(""title"");
        }
    }

    [ScreenInfo(nameof(X_VIEW_NAME))]
    public class X_PRESENTER_NAME : BaseUIToolkitScreenPresenter<X_VIEW_NAME, X_MODEL_NAME>
    {
        [Preserve]
        public X_PRESENTER_NAME(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager) { }

        public override UniTask BindData(X_MODEL_NAME screenModel)
        {
            return UniTask.CompletedTask;
        }
    }
}";

        private const string SCREEN_VIEW_UITK_NON_MODEL_TEMPLATE =
            @"namespace X_NAME_SPACE
{
    using Cuvara.UIToolkit.View;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Scripts.UIModule.UITK.Presenter;
    using GameFoundation.Signals;
    using UniT.Logging;
    using UnityEngine.Scripting;
    using UnityEngine.UIElements;

    public class X_VIEW_NAME : BaseUIToolkitView, ISurfaceScreenView
    {
        public Label Title { get; }

        public X_VIEW_NAME(VisualTreeAsset visualTreeAsset) : base(visualTreeAsset)
        {
            this.StretchToParent();

            this.Title = this.Root.Q<Label>(""title"");
        }
    }

    [ScreenInfo(nameof(X_VIEW_NAME))]
    public class X_PRESENTER_NAME : BaseUIToolkitScreenPresenter<X_VIEW_NAME>
    {
        [Preserve]
        public X_PRESENTER_NAME(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager) { }

        public override UniTask BindData()
        {
            return UniTask.CompletedTask;
        }
    }
}";

        // A row takes a VisualElement, not a VisualTreeAsset — the adapter owns the clone
        // because it owns the recycling. See IUIToolkitItemView's remarks.
        private const string ITEM_VIEW_UITK_TEMPLATE =
            @"namespace X_NAME_SPACE
{
    using Cuvara.UIToolkit.Collections;
    using UnityEngine.UIElements;

    public class X_MODEL_NAME
    {
    }

    public class X_VIEW_NAME : BaseUIToolkitItemView
    {
        public Label Title { get; }

        public X_VIEW_NAME(VisualElement root) : base(root)
        {
            this.Title = this.Root.Q<Label>(""title"");
        }
    }

    public class X_PRESENTER_NAME : BaseUIToolkitItemPresenter<X_VIEW_NAME, X_MODEL_NAME>
    {
        public override void BindData(X_MODEL_NAME param)
        {
        }
    }
}";

        private const string POPUP_VIEW_UITK_TEMPLATE =
            @"namespace X_NAME_SPACE
{
    using Cuvara.UIToolkit.View;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Scripts.UIModule.UITK.Presenter;
    using GameFoundation.Signals;
    using UniT.Logging;
    using UnityEngine.Scripting;
    using UnityEngine.UIElements;

    public class X_MODEL_NAME
    {
    }

    public class X_VIEW_NAME : BaseUIToolkitView, ISurfaceScreenView
    {
        public Label  Title    { get; }
        public Button BtnClose { get; }

        public X_VIEW_NAME(VisualTreeAsset visualTreeAsset) : base(visualTreeAsset)
        {
            this.StretchToParent();

            this.Title    = this.Root.Q<Label>(""title"");
            this.BtnClose = this.Root.Q<Button>(""btn-close"");
        }
    }

    [PopupInfo(nameof(X_VIEW_NAME))]
    public class X_PRESENTER_NAME : BaseUIToolkitPopupPresenter<X_VIEW_NAME, X_MODEL_NAME>
    {
        [Preserve]
        public X_PRESENTER_NAME(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager) { }

        public override UniTask BindData(X_MODEL_NAME popupModel)
        {
            // Unregister before registering: BindData runs again on every re-open, and
            // `clicked` is a plain multicast delegate, so adding without removing fires
            // the handler once per open.
            this.View.BtnClose.clicked -= this.CloseView;
            this.View.BtnClose.clicked += this.CloseView;

            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (this.View != null) this.View.BtnClose.clicked -= this.CloseView;
        }
    }
}";

        private const string POPUP_VIEW_UITK_NON_MODEL_TEMPLATE =
            @"namespace X_NAME_SPACE
{
    using Cuvara.UIToolkit.View;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using GameFoundation.Scripts.UIModule.UITK.Presenter;
    using GameFoundation.Signals;
    using UniT.Logging;
    using UnityEngine.Scripting;
    using UnityEngine.UIElements;

    public class X_VIEW_NAME : BaseUIToolkitView, ISurfaceScreenView
    {
        public Label  Title    { get; }
        public Button BtnClose { get; }

        public X_VIEW_NAME(VisualTreeAsset visualTreeAsset) : base(visualTreeAsset)
        {
            this.StretchToParent();

            this.Title    = this.Root.Q<Label>(""title"");
            this.BtnClose = this.Root.Q<Button>(""btn-close"");
        }
    }

    [PopupInfo(nameof(X_VIEW_NAME))]
    public class X_PRESENTER_NAME : BaseUIToolkitPopupPresenter<X_VIEW_NAME>
    {
        [Preserve]
        public X_PRESENTER_NAME(SignalBus signalBus, ILoggerManager loggerManager) : base(signalBus, loggerManager) { }

        public override UniTask BindData()
        {
            this.View.BtnClose.clicked -= this.CloseView;
            this.View.BtnClose.clicked += this.CloseView;

            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (this.View != null) this.View.BtnClose.clicked -= this.CloseView;
        }
    }
}";

        #endregion

        #region UXML

        // Element names match the UQuery lookups in the generated view, and the
        // SafeAreaElement wraps the content so a generated screen is notch-correct from
        // the first run rather than after someone remembers.

        private const string SCREEN_UXML_TEMPLATE =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:gf=""Cuvara.UIToolkit.Utilities"" editor-extension-mode=""False"">
    <ui:VisualElement name=""X_UXML_ROOT_NAME"" style=""flex-grow: 1;"">
        <gf:SafeAreaElement name=""safe-area"" style=""flex-grow: 1;"">
            <ui:Label name=""title"" text=""X_VIEW_NAME"" />
        </gf:SafeAreaElement>
    </ui:VisualElement>
</ui:UXML>";

        // No SafeAreaElement on a row: a row lives inside a collection which lives inside a
        // screen, and the screen is where the notch is dealt with. Nesting a second safe
        // area inside the first would inset every row by the notch again.
        private const string ITEM_UXML_TEMPLATE =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<ui:UXML xmlns:ui=""UnityEngine.UIElements"" editor-extension-mode=""False"">
    <ui:VisualElement name=""X_UXML_ROOT_NAME"">
        <ui:Label name=""title"" text=""X_VIEW_NAME"" />
    </ui:VisualElement>
</ui:UXML>";

        private const string POPUP_UXML_TEMPLATE =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:gf=""Cuvara.UIToolkit.Utilities"" editor-extension-mode=""False"">
    <ui:VisualElement name=""X_UXML_ROOT_NAME"" style=""flex-grow: 1; align-items: center; justify-content: center;"">
        <ui:VisualElement name=""dimmer"" style=""position: absolute; left: 0; top: 0; right: 0; bottom: 0; background-color: rgba(0, 0, 0, 0.6);"" />

        <gf:SafeAreaElement name=""safe-area"" style=""align-items: center; justify-content: center;"">
            <ui:VisualElement name=""panel"" style=""padding: 24px; background-color: rgb(38, 38, 38);"">
                <ui:Label name=""title"" text=""X_VIEW_NAME"" />
                <ui:Button name=""btn-close"" text=""Close"" />
            </ui:VisualElement>
        </gf:SafeAreaElement>
    </ui:VisualElement>
</ui:UXML>";

        #endregion
    }
}
