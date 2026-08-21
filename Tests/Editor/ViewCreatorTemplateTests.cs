namespace GameFoundation.UIModule.UITK.Editor.Tests
{
    using System;
    using System.Linq;
    using System.Reflection;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Editor.Tools.ViewCreatorWizard;
    using GameFoundation.Scripts.UIModule.MVP;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using Cuvara.UIToolkit.Collections;
    using GameFoundation.Scripts.UIModule.UITK.Presenter;
    using Cuvara.UIToolkit.View;
    using GameFoundation.Signals;
    using NUnit.Framework;
    using UniT.Logging;
    using Wizard = GameFoundation.Editor.Tools.ViewCreatorWizard.ViewCreatorWizard;

    /// <summary>
    /// The View Creator Wizard's output.
    /// </summary>
    /// <remarks>
    /// <para><b>These exist because the templates were stale and nothing said so.</b> They
    /// are string constants: nothing compiles them, no reference to <c>Zenject</c> or
    /// <c>ILogService</c> inside one is a compile error, and the wizard therefore went on
    /// happily generating code that could not build for as long as nobody used it. The
    /// checks below are the substitute for a compiler.</para>
    ///
    /// <para>They are two kinds. The textual ones pin what must and must not appear in the
    /// generated source. The reflection ones are the interesting half: they assert against
    /// the REAL base types that the constructors the templates emit actually exist. A future
    /// migration that changes a presenter's constructor — which is exactly what happened
    /// twice already — fails here rather than in a user's generated file three months
    /// later.</para>
    ///
    /// <para>EditMode, and a separate assembly, because <c>ViewCreatorWizard</c> lives in
    /// <c>GameFoundation.Editor</c>: a PlayMode test assembly compiles for players and
    /// cannot reference an Editor-only one.</para>
    /// </remarks>
    public class ViewCreatorTemplateTests
    {
        private static readonly ViewType[] AllTypes = { ViewType.Item, ViewType.Popup, ViewType.Screen };

        private static readonly ViewBackend[] AllBackends = { ViewBackend.UGUI, ViewBackend.UIToolkit };

        private static string Render(ViewType type, ViewBackend backend, bool hasModel)
        {
            return Wizard.SelectScriptTemplate(type, backend, hasModel)
                .Replace("X_NAME_SPACE", "Game.Feature")
                .Replace("X_MODEL_NAME", "ShopScreenModel")
                .Replace("X_VIEW_NAME", "ShopScreenView")
                .Replace("X_PRESENTER_NAME", "ShopScreenPresenter");
        }

        #region Staleness

        [Test]
        public void NoTemplateStillReferencesZenject()
        {
            // Zenject was removed from this package. SignalBus is GameFoundation.Signals now.
            foreach (var type in AllTypes)
            foreach (var backend in AllBackends)
            foreach (var hasModel in new[] { true, false })
            {
                Assert.That(Render(type, backend, hasModel), Does.Not.Contain("Zenject"), $"{backend}/{type}/hasModel={hasModel}");
            }
        }

        [Test]
        public void NoTemplateStillReferencesTheDeletedLogService()
        {
            foreach (var type in AllTypes)
            foreach (var backend in AllBackends)
            foreach (var hasModel in new[] { true, false })
            {
                var rendered = Render(type, backend, hasModel);

                Assert.That(rendered, Does.Not.Contain("ILogService"), $"{backend}/{type}/hasModel={hasModel}");
                Assert.That(rendered, Does.Not.Contain("Utilities.LogService"), $"{backend}/{type}/hasModel={hasModel}");
            }
        }

        [Test]
        public void EveryScreenAndPopupTemplateTakesTheCurrentConstructorPair()
        {
            foreach (var type in new[] { ViewType.Popup, ViewType.Screen })
            foreach (var backend in AllBackends)
            foreach (var hasModel in new[] { true, false })
            {
                Assert.That(Render(type, backend, hasModel), Does.Contain("SignalBus signalBus, ILoggerManager loggerManager"),
                    $"{backend}/{type}/hasModel={hasModel}");
            }
        }

        [Test]
        public void EveryScreenAndPopupTemplateReturnsUniTaskFromBindData()
        {
            // BindData returns UniTask on BaseScreenPresenterCore. The old templates emitted
            // `public override void BindData(...)`, which does not override anything.
            foreach (var type in new[] { ViewType.Popup, ViewType.Screen })
            foreach (var backend in AllBackends)
            foreach (var hasModel in new[] { true, false })
            {
                var rendered = Render(type, backend, hasModel);

                Assert.That(rendered, Does.Contain("public override UniTask BindData"), $"{backend}/{type}/hasModel={hasModel}");
                Assert.That(rendered, Does.Not.Contain("public override void BindData"), $"{backend}/{type}/hasModel={hasModel}");
            }
        }

        [Test]
        public void EveryGeneratedPresenterConstructorIsPreserved()
        {
            // Presenters are resolved reflectively and are otherwise stripper bait on IL2CPP.
            foreach (var type in new[] { ViewType.Popup, ViewType.Screen })
            foreach (var backend in AllBackends)
            foreach (var hasModel in new[] { true, false })
            {
                Assert.That(Render(type, backend, hasModel), Does.Contain("[Preserve]"), $"{backend}/{type}/hasModel={hasModel}");
            }
        }

        [Test]
        public void TheItemTemplateNoLongerCallsAConstructorThatDoesNotExist()
        {
            // BaseUIItemPresenter<TView, TModel> takes nothing. The old template emitted
            // `: base(assetsManager)`.
            var rendered = Render(ViewType.Item, ViewBackend.UGUI, true);

            Assert.That(rendered, Does.Not.Contain("base(assetsManager)"));
            Assert.That(rendered, Does.Not.Contain("IAssetsManager"));
        }

        [Test]
        public void EveryPlaceholderIsSubstituted()
        {
            // A leftover X_ token is a template that names a placeholder the wizard does not
            // replace — generated code that will not compile for a reason nobody can see.
            foreach (var type in AllTypes)
            foreach (var backend in AllBackends)
            foreach (var hasModel in new[] { true, false })
            {
                Assert.That(Render(type, backend, hasModel), Does.Not.Contain("X_"), $"{backend}/{type}/hasModel={hasModel}");
            }
        }

        #endregion

        #region The two backends

        [Test]
        public void TheUGUITemplatesStillEmitUGUIViews()
        {
            // The uGUI path is permanent. Nothing here migrates it.
            foreach (var type in new[] { ViewType.Popup, ViewType.Screen })
            foreach (var hasModel in new[] { true, false })
            {
                Assert.That(Render(type, ViewBackend.UGUI, hasModel), Does.Contain(": BaseView"), $"{type}/hasModel={hasModel}");
            }
        }

        [Test]
        public void TheUIToolkitTemplatesEmitAUIToolkitViewWithTheConstructorTheBackendCalls()
        {
            // UIToolkitViewFactory builds every view through a public (VisualTreeAsset)
            // constructor. A generated view without one is unconstructable at runtime.
            foreach (var type in new[] { ViewType.Popup, ViewType.Screen })
            foreach (var hasModel in new[] { true, false })
            {
                var rendered = Render(type, ViewBackend.UIToolkit, hasModel);

                Assert.That(rendered, Does.Contain(": BaseUIToolkitView, ISurfaceScreenView"), $"{type}/hasModel={hasModel}");
                Assert.That(rendered, Does.Contain("ShopScreenView(VisualTreeAsset visualTreeAsset) : base(visualTreeAsset)"), $"{type}/hasModel={hasModel}");
            }
        }

        [Test]
        public void TheUIToolkitTemplatesBridgeToTheHostContract()
        {
            // BaseUIToolkitView now lives in com.cuvara.uitoolkit, which knows nothing about
            // this framework's screen flow. ISurfaceScreenView is the bridge, and it adds no
            // member the package base does not already have — so a generated view that omits
            // it compiles but cannot be opened through ScreenManager, which is exactly the
            // kind of failure a template test should catch rather than a user.
            foreach (var type in new[] { ViewType.Popup, ViewType.Screen })
            foreach (var hasModel in new[] { true, false })
            {
                var rendered = Render(type, ViewBackend.UIToolkit, hasModel);

                Assert.That(rendered, Does.Contain("using Cuvara.UIToolkit.View;"), $"{type}/hasModel={hasModel}");
                Assert.That(rendered, Does.Contain("using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;"), $"{type}/hasModel={hasModel}");
                Assert.That(rendered, Does.Contain("this.StretchToParent();"), $"{type}/hasModel={hasModel}");
            }
        }

        [Test]
        public void TheUIToolkitItemTemplatePointsAtThePackage()
        {
            Assert.That(Render(ViewType.Item, ViewBackend.UIToolkit, true), Does.Contain("using Cuvara.UIToolkit.Collections;"));
        }

        [Test]
        public void TheUIToolkitItemTemplateTakesAVisualElement_NotAVisualTreeAsset()
        {
            // A row is cloned by the adapter, which owns the recycling; the row view receives
            // the already-cloned element. This is the one signature that differs from a screen.
            var rendered = Render(ViewType.Item, ViewBackend.UIToolkit, true);

            Assert.That(rendered, Does.Contain(": BaseUIToolkitItemView"));
            Assert.That(rendered, Does.Contain("ShopScreenView(VisualElement root) : base(root)"));
            Assert.That(rendered, Does.Not.Contain("VisualTreeAsset"));
        }

        [Test]
        public void TheUIToolkitPresentersDeriveFromTheUIToolkitBases()
        {
            Assert.That(Render(ViewType.Screen, ViewBackend.UIToolkit, true), Does.Contain(": BaseUIToolkitScreenPresenter<ShopScreenView, ShopScreenModel>"));
            Assert.That(Render(ViewType.Screen, ViewBackend.UIToolkit, false), Does.Contain(": BaseUIToolkitScreenPresenter<ShopScreenView>"));
            Assert.That(Render(ViewType.Popup, ViewBackend.UIToolkit, true), Does.Contain(": BaseUIToolkitPopupPresenter<ShopScreenView, ShopScreenModel>"));
            Assert.That(Render(ViewType.Popup, ViewBackend.UIToolkit, false), Does.Contain(": BaseUIToolkitPopupPresenter<ShopScreenView>"));
        }

        [Test]
        public void EveryCombinationProducesADistinctTemplate()
        {
            var rendered = (from type in AllTypes
                            from backend in AllBackends
                            from hasModel in new[] { true, false }
                            select Wizard.SelectScriptTemplate(type, backend, hasModel)).ToList();

            // Item ignores hasModel by design, so the two Item rows per backend are the same
            // string; everything else must be distinct.
            Assert.That(rendered.Distinct().Count(), Is.EqualTo(rendered.Count - 2));
        }

        [Test]
        public void ANonModelTemplateDoesNotDeclareAModel()
        {
            foreach (var type in new[] { ViewType.Popup, ViewType.Screen })
            foreach (var backend in AllBackends)
            {
                Assert.That(Render(type, backend, false), Does.Not.Contain("ShopScreenModel"), $"{backend}/{type}");
            }
        }

        #endregion

        #region The starter UXML

        [Test]
        public void TheScreenUxmlNamesTheElementTheGeneratedViewQueriesFor()
        {
            // The single most likely way for generated code to "work" and produce a null
            // reference: the view queries for a name the UXML does not carry.
            var uxml = Wizard.SelectUxmlTemplate(ViewType.Screen);
            var code = Render(ViewType.Screen, ViewBackend.UIToolkit, true);

            Assert.That(code, Does.Contain("Q<Label>(\"title\")"));
            Assert.That(uxml, Does.Contain("name=\"title\""));
        }

        [Test]
        public void ThePopupUxmlNamesBothElementsTheGeneratedViewQueriesFor()
        {
            var uxml = Wizard.SelectUxmlTemplate(ViewType.Popup);
            var code = Render(ViewType.Popup, ViewBackend.UIToolkit, true);

            Assert.That(code, Does.Contain("Q<Label>(\"title\")"));
            Assert.That(code, Does.Contain("Q<Button>(\"btn-close\")"));
            Assert.That(uxml, Does.Contain("name=\"title\""));
            Assert.That(uxml, Does.Contain("name=\"btn-close\""));
        }

        [Test]
        public void TheScreenAndPopupUxmlDeclareTheSafeAreaNamespaceTheyUse()
        {
            // A gf: prefix with no matching xmlns is a UXML import error at asset time.
            foreach (var type in new[] { ViewType.Popup, ViewType.Screen })
            {
                var uxml = Wizard.SelectUxmlTemplate(type);

                Assert.That(uxml, Does.Contain("<gf:SafeAreaElement"), $"{type}");
                Assert.That(uxml, Does.Contain("xmlns:gf=\"Cuvara.UIToolkit.Utilities\""), $"{type}");
            }
        }

        [Test]
        public void TheItemUxmlHasNoSafeArea()
        {
            // A row sits inside a screen that already handles the notch. A second one would
            // inset every row by it again.
            Assert.That(Wizard.SelectUxmlTemplate(ViewType.Item), Does.Not.Contain("SafeAreaElement"));
        }

        [Test]
        public void EveryUxmlTemplateIsWellFormedXmlOnceSubstituted()
        {
            foreach (var type in AllTypes)
            {
                var uxml = Wizard.SelectUxmlTemplate(type)
                    .Replace("X_UXML_ROOT_NAME", "shop-screen-view")
                    .Replace("X_VIEW_NAME", "ShopScreenView");

                Assert.DoesNotThrow(() => System.Xml.Linq.XDocument.Parse(uxml), $"{type}");
            }
        }

        #endregion

        #region Selection, and its negative paths

        [Test]
        public void AnUnknownViewType_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Wizard.SelectScriptTemplate((ViewType)99, ViewBackend.UGUI, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => Wizard.SelectUxmlTemplate((ViewType)99));
        }

        [Test]
        public void AnUnknownBackend_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Wizard.SelectScriptTemplate(ViewType.Screen, (ViewBackend)99, true));
        }

        [Test]
        public void KebabCase_SplitsOnEveryCapital()
        {
            Assert.That(Wizard.ToKebabCase("ShopPopupView"), Is.EqualTo("shop-popup-view"));
        }

        [Test]
        public void KebabCase_LeavesAnAlreadyLowercaseNameAlone()
        {
            Assert.That(Wizard.ToKebabCase("shop"), Is.EqualTo("shop"));
        }

        [Test]
        public void KebabCase_OfNullOrEmpty_ReturnsTheInput()
        {
            Assert.That(Wizard.ToKebabCase(null), Is.Null);
            Assert.That(Wizard.ToKebabCase(string.Empty), Is.Empty);
        }

        #endregion

        #region The real types the templates target

        // The half that a compiler would otherwise be the only thing to catch. Each of these
        // is the exact signature one of the templates emits a call to.

        [Test]
        public void TheUGUIScreenAndPopupBases_TakeSignalBusAndLoggerManager()
        {
            AssertHasConstructor(typeof(BaseScreenPresenter<>), typeof(SignalBus), typeof(ILoggerManager));
            AssertHasConstructor(typeof(BaseScreenPresenter<,>), typeof(SignalBus), typeof(ILoggerManager));
            AssertHasConstructor(typeof(BasePopupPresenter<>), typeof(SignalBus), typeof(ILoggerManager));
            AssertHasConstructor(typeof(BasePopupPresenter<,>), typeof(SignalBus), typeof(ILoggerManager));
        }

        [Test]
        public void TheUIToolkitScreenAndPopupBases_TakeSignalBusAndLoggerManager()
        {
            AssertHasConstructor(typeof(BaseUIToolkitScreenPresenter<>), typeof(SignalBus), typeof(ILoggerManager));
            AssertHasConstructor(typeof(BaseUIToolkitScreenPresenter<,>), typeof(SignalBus), typeof(ILoggerManager));
            AssertHasConstructor(typeof(BaseUIToolkitPopupPresenter<>), typeof(SignalBus), typeof(ILoggerManager));
            AssertHasConstructor(typeof(BaseUIToolkitPopupPresenter<,>), typeof(SignalBus), typeof(ILoggerManager));
        }

        [Test]
        public void TheItemBase_TakesNothing()
        {
            AssertHasConstructor(typeof(BaseUIItemPresenter<,>));
            AssertHasConstructor(typeof(BaseUIToolkitItemPresenter<,>));
        }

        [Test]
        public void BindData_ReturnsUniTaskOnTheScreenAndPopupBases()
        {
            var bindData = typeof(BaseScreenPresenterCore<>).GetMethod("BindData", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

            Assert.That(bindData, Is.Not.Null, "BaseScreenPresenterCore<TView>.BindData() is gone; the templates name it.");
            Assert.That(bindData.ReturnType, Is.EqualTo(typeof(UniTask)));
        }

        [Test]
        public void TheUIToolkitViewBase_TakesAVisualTreeAsset()
        {
            AssertHasConstructor(typeof(BaseUIToolkitView), typeof(UnityEngine.UIElements.VisualTreeAsset));
        }

        [Test]
        public void TheUIToolkitItemViewBase_TakesAVisualElement()
        {
            AssertHasConstructor(typeof(BaseUIToolkitItemView), typeof(UnityEngine.UIElements.VisualElement));
        }

        private static void AssertHasConstructor(Type type, params Type[] parameterTypes)
        {
            var found = type
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameterTypes));

            Assert.That(found, Is.True,
                $"{type.Name} has no ({string.Join(", ", parameterTypes.Select(t => t.Name))}) constructor, but a wizard template emits a call to one. "
                + $"Found: {string.Join(" | ", type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(c => "(" + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name)) + ")"))}");
        }

        #endregion
    }
}
