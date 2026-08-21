namespace GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View
{
    using Cuvara.UIToolkit.Core;
    using UnityEngine;

    /// <summary>The uGUI implementation of <see cref="IViewLayer"/>: a Transform.</summary>
    /// <remarks>
    /// Constructed once per root by <c>ScreenManager</c> and cached, not allocated per
    /// reparent — the screen flow reparents on every open and close.
    /// </remarks>
    public sealed class TransformViewLayer : IViewLayer
    {
        public Transform Transform { get; }

        public TransformViewLayer(Transform transform) { this.Transform = transform; }
    }

    /// <summary>The uGUI implementation of <see cref="IViewSurface"/>: a RectTransform.</summary>
    public sealed class RectTransformViewSurface : IViewSurface
    {
        private readonly RectTransform rectTransform;

        public RectTransformViewSurface(RectTransform rectTransform) { this.rectTransform = rectTransform; }

        public void SetParent(IViewLayer layer)
        {
            // Fails loudly rather than silently doing nothing: handing a UI Toolkit layer
            // to a uGUI surface is a wiring mistake, and a no-op here would surface much
            // later as a screen that renders in the wrong place with nothing in the log.
            if (layer is not TransformViewLayer transformLayer)
            {
                throw new System.InvalidOperationException(
                    $"A uGUI view can only be parented into a {nameof(TransformViewLayer)}, got " +
                    $"{layer?.GetType().Name ?? "null"}.");
            }

            this.rectTransform.SetParent(transformLayer.Transform);
        }
    }
}
