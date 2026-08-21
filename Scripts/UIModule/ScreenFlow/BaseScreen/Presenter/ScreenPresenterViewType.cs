namespace GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.Presenter
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Answers "which view type does this presenter drive?" — the question
    /// <c>ScreenManager</c> has to answer before it can decide how to build the view.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this is needed at all.</b> The manager never knew a screen's view type:
    /// it instantiated a prefab and pulled <c>IScreenView</c> off it with
    /// <c>GetComponent</c>, so the view type stayed implicit. Choosing a backend off the
    /// view type — the suggestion this step was handed — therefore needs the type to be
    /// recovered first, and the only place it is written down is the generic argument of
    /// <c>BaseScreenPresenterCore&lt;TView&gt;</c>.</para>
    ///
    /// <para><b>Why not a new attribute, and why not a member on <c>IScreenPresenter</c>.</b>
    /// An attribute would restate on every presenter a fact its base class already states,
    /// and the two would drift silently the first time someone changed one and not the
    /// other — a presenter re-based onto the UI Toolkit presenter but still marked uGUI
    /// fails at <c>GetComponent</c>, far from the attribute that lied. A <c>Type ViewType</c>
    /// member on <c>IScreenPresenter</c> reads better but is a source break: six consuming
    /// repositories and <c>com.gdk.3rd</c> compile against that interface, and anything
    /// implementing it directly would stop building. Reflection over the base chain reads
    /// the fact where it is already true and breaks nothing.</para>
    ///
    /// <para><b>A presenter that does not derive from <c>BaseScreenPresenterCore&lt;&gt;</c></b>
    /// — implementing <c>IScreenPresenter</c> by hand is legal — resolves to <c>null</c>,
    /// and <c>ScreenManager</c> treats null as "uGUI", which is exactly what happens today.
    /// Unknown means status quo, never a new failure.</para>
    /// </remarks>
    public static class ScreenPresenterViewType
    {
        // Reflection over a base chain per screen open would be wasteful on a path that
        // runs during transitions; the answer is immutable per type, so it is cached.
        // Null is a real answer here and is cached too, hence Dictionary over the
        // null-as-miss shape.
        private static readonly Dictionary<Type, Type> Cache = new();

        /// <summary>
        /// The <c>TView</c> of <paramref name="presenterType"/>, or null if it does not
        /// derive from <see cref="BaseScreenPresenterCore{TView}"/>.
        /// </summary>
        public static Type Of(Type presenterType)
        {
            if (presenterType == null) return null;

            lock (Cache)
            {
                if (Cache.TryGetValue(presenterType, out var cached)) return cached;

                var resolved = Resolve(presenterType);
                Cache[presenterType] = resolved;

                return resolved;
            }
        }

        private static Type Resolve(Type presenterType)
        {
            for (var type = presenterType; type != null; type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BaseScreenPresenterCore<>))
                {
                    return type.GetGenericArguments()[0];
                }
            }

            return null;
        }
    }
}
