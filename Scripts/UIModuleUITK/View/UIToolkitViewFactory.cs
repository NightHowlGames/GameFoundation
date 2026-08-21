namespace GameFoundation.Scripts.UIModule.UITK.View
{
    using System;
    using System.Linq;
    using GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View;
    using UnityEngine.UIElements;

    /// <summary>
    /// Builds a <see cref="BaseUIToolkitView"/> subclass from the
    /// <see cref="VisualTreeAsset"/> it was authored against.
    /// </summary>
    /// <remarks>
    /// Split out of the backend, rather than folded into it, so that the one genuinely
    /// tricky part of the UI Toolkit construction path — turning a <c>Type</c> and an asset
    /// into a live view — can be exercised by an EditMode test with no scene, no container,
    /// no Addressables and no <c>UIDocument</c> in sight. That test is the reason this class
    /// is public.
    ///
    /// <para><c>Activator</c> rather than the DI container: a UI Toolkit view is the
    /// counterpart of a uGUI view prefab, and a uGUI view is never injected either — it is
    /// a MonoBehaviour with <c>[SerializeField]</c>s, and everything it needs arrives from
    /// its presenter, which IS injected. Keeping the view container-free also keeps this
    /// callable from a test.</para>
    /// </remarks>
    public static class UIToolkitViewFactory
    {
        /// <summary>
        /// Constructs <paramref name="viewType"/> by calling its
        /// <c>(VisualTreeAsset)</c> constructor.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// <paramref name="viewType"/> is not an <see cref="ISurfaceScreenView"/>, is
        /// abstract, or has no public constructor taking a single
        /// <see cref="VisualTreeAsset"/>.
        /// </exception>
        public static ISurfaceScreenView Create(Type viewType, VisualTreeAsset visualTreeAsset)
        {
            if (viewType == null) throw new ArgumentNullException(nameof(viewType));
            if (visualTreeAsset == null) throw new ArgumentNullException(nameof(visualTreeAsset));

            if (!typeof(ISurfaceScreenView).IsAssignableFrom(viewType))
            {
                throw new ArgumentException($"{viewType.Name} is not an {nameof(ISurfaceScreenView)}; the UI Toolkit backend cannot build it.", nameof(viewType));
            }

            if (viewType.IsAbstract)
            {
                throw new ArgumentException($"{viewType.Name} is abstract and cannot be constructed. A screen's TView must be a concrete view.", nameof(viewType));
            }

            var constructor = viewType.GetConstructor(new[] { typeof(VisualTreeAsset) });

            if (constructor == null)
            {
                // Named explicitly, because the alternative — a MissingMethodException out
                // of Activator — says only "no matching constructor" and leaves the reader
                // to work out which constructor was wanted.
                var found = string.Join(", ", viewType.GetConstructors().Select(Describe));

                throw new ArgumentException(
                    $"{viewType.Name} has no public constructor taking a single {nameof(VisualTreeAsset)}, which is how the "
                    + $"UI Toolkit backend builds a view. Found: {(found.Length == 0 ? "none" : found)}.",
                    nameof(viewType));
            }

            return (ISurfaceScreenView)constructor.Invoke(new object[] { visualTreeAsset });
        }

        private static string Describe(System.Reflection.ConstructorInfo constructor)
        {
            return $"({string.Join(", ", constructor.GetParameters().Select(parameter => parameter.ParameterType.Name))})";
        }
    }
}
