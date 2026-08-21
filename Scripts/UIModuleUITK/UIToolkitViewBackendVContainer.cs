#if GDK_VCONTAINER
namespace GameFoundation.Scripts.UIModule.UITK
{
    using GameFoundation.Scripts.UIModule.ScreenFlow.Managers;
    using GameFoundation.Scripts.UIModule.UITK.Managers;
    using VContainer;

    /// <summary>
    /// Registers the UI Toolkit view backend, which is what makes
    /// <c>ScreenManager.OpenScreen&lt;TPresenter&gt;</c> work for a UI Toolkit presenter.
    /// </summary>
    /// <remarks>
    /// <para>Opt-in, and separate from <c>RegisterScreenManager</c>. A project that does not
    /// call this gets exactly today's <c>ScreenManager</c>: the manager's lookup for a
    /// backend finds nothing and every screen takes the uGUI path. Folding the registration
    /// into <c>RegisterScreenManager</c> would put a UI Toolkit object in six projects that
    /// have no UI Toolkit screens.</para>
    ///
    /// <para>Register it on the same scope as the screen manager — the manager resolves it
    /// through <c>GetCurrentContainer()</c>, which is the scene scope.</para>
    /// </remarks>
    public static class UIToolkitViewBackendVContainer
    {
        public static void RegisterUIToolkitViewBackend(this IContainerBuilder builder)
        {
            builder.Register<UIToolkitScreenViewBackend>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
        }
    }
}
#endif
