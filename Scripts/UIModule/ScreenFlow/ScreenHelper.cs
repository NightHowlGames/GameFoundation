namespace GameFoundation.Scripts.UIModule.ScreenFlow
{
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using UnityEngine.SceneManagement;

    public static class ScreenHelper
    {
        public static string GetScreenId<TView>() where TView : IScreenView
        {
            return $"{SceneManager.GetActiveScene().name}/{typeof(TView).Name}";
        }
    }
}