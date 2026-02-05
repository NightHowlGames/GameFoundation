namespace GameFoundation.Scripts.UIModule.Utilities
{
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.Signals;
    using GameFoundation.Signals;
    #if GDK_ZENJECT
    using Zenject;
    #endif
    #if GDK_VCONTAINER
    using VContainer;
    #endif

    public static class ExtensionMethod
    {
        #if GDK_ZENJECT
        /// <summary>
        /// Utils use to initialize a screen presenter manually, and the view is already initialized on the scene
        /// </summary>
        /// <param name="container"></param>
        /// <param name="autoBindData"></param>
        /// <typeparam name="T"> Type of screen presenter</typeparam>
        public static void InitScreenManually<T>(this DiContainer container, bool autoBindData = false) where T : IScreenPresenter
        {
            container.Bind<T>().AsSingle().OnInstantiated<T>((context, presenter) =>
            {
                context.Container.Resolve<SignalBus>().Fire(new ManualInitScreenSignal()
                {
                    ScreenPresenter = presenter,
                    IncludingBindData = autoBindData,
                });
            }).NonLazy();
        }
        #endif

        #if GDK_VCONTAINER
        public static void InitScreenManually<T>(this IContainerBuilder builder, bool autoBindData = false) where T : IScreenPresenter
        {
            builder.RegisterBuildCallback(container => container.Resolve<SignalBus>().Fire(new ManualInitScreenSignal
            {
                ScreenPresenter   = container.Instantiate<T>(),
                IncludingBindData = autoBindData,
            }));
        }
        #endif
    }
}