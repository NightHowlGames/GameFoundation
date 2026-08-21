namespace GameFoundation.Scripts.UIModule.UITK.View
{
    using System;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using UnityEngine.UIElements;

    /// <summary>The UI Toolkit implementation of <see cref="IViewLayer"/>: a VisualElement.</summary>
    /// <remarks>
    /// The mirror of <c>TransformViewLayer</c>. Constructed once per root and cached, not
    /// allocated per reparent — the screen flow reparents on every open and close.
    /// </remarks>
    public sealed class VisualElementViewLayer : IViewLayer
    {
        public VisualElement Element { get; }

        public VisualElementViewLayer(VisualElement element)
        {
            this.Element = element ?? throw new ArgumentNullException(nameof(element));
        }
    }

    /// <summary>The UI Toolkit implementation of <see cref="IViewSurface"/>: a VisualElement.</summary>
    public sealed class VisualElementViewSurface : IViewSurface
    {
        private readonly VisualElement element;

        public VisualElementViewSurface(VisualElement element)
        {
            this.element = element ?? throw new ArgumentNullException(nameof(element));
        }

        public void SetParent(IViewLayer layer)
        {
            // Fails loudly rather than silently doing nothing, exactly as the uGUI surface
            // does: handing a uGUI layer to a UI Toolkit surface is a wiring mistake, and a
            // no-op here would surface much later as a screen that renders nowhere at all,
            // with nothing in the log to say why.
            if (layer is not VisualElementViewLayer visualElementLayer)
            {
                throw new InvalidOperationException(
                    $"A UI Toolkit view can only be parented into a {nameof(VisualElementViewLayer)}, got " +
                    $"{layer?.GetType().Name ?? "null"}.");
            }

            // Add() detaches from the previous parent first, so this is a reparent, not a
            // double-parent — matching Transform.SetParent's semantics.
            visualElementLayer.Element.Add(this.element);
        }
    }
}
