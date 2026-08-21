namespace GameFoundation.Scripts.UIModule.ScreenFlow
{
    using Cuvara.UIToolkit.Core;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using UnityEngine.SceneManagement;

    public static class ScreenHelper
    {
        /// <summary>The screen id of a view type: active scene name plus the type name.</summary>
        /// <remarks>
        /// The constraint was widened from <c>IScreenView</c> to <see cref="IScreenViewBase"/>
        /// so that a non-uGUI view — which cannot satisfy <c>IScreenView</c>, since that
        /// requires a <c>RectTransform</c> — can still have an id. Widening a constraint
        /// accepts everything it accepted before, so no existing call site changes; the
        /// body does not look at the type beyond its name.
        /// </remarks>
        public static string GetScreenId<TView>() where TView : IScreenViewBase
        {
            return $"{SceneManager.GetActiveScene().name}/{typeof(TView).Name}";
        }
    }
}
