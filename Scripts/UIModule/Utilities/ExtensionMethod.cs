namespace GameFoundation.Scripts.UIModule.Utilities
{
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter;
    using GameFoundation.Scripts.UIModule.ScreenFlow.Signals;
    using GameFoundation.Signals;
    #if GDK_VCONTAINER
    using VContainer;
    #endif

    public static class ExtensionMethod
    {
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