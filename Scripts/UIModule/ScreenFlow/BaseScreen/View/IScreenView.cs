namespace GameFoundation.Scripts.UIModule.ScreenFlow.BaseScreen.View
{
    using System;
    using Cuvara.UIToolkit.Core;
    using Cysharp.Threading.Tasks;
    using GameFoundation.Scripts.UIModule.MVP;
    using UnityEngine;

    /// <summary>
    /// Everything a screen's view does that is not tied to a UI backend: it opens,
    /// closes, shows, hides, destroys itself, and says when it did.
    /// </summary>
    /// <remarks>
    /// This interface is a PURE RELOCATION out of <see cref="IScreenView"/> — every
    /// member below used to be declared there, and <see cref="IScreenView"/> still
    /// inherits all of them. No member was added to, removed from, or renamed on the
    /// contract that <c>BaseView</c>, ThirdPartyServices and the consuming repositories
    /// compile against, so all existing implementers and call sites are unaffected.
    ///
    /// It exists because <c>RectTransform</c> — the one member here that a UI Toolkit
    /// view cannot produce, since a <c>VisualElement</c> is not a <c>Transform</c> and
    /// has no GameObject behind it — was declared on the view CONTRACT rather than on
    /// the uGUI implementation. Splitting it off is the smallest change that lets a
    /// non-uGUI view be a screen view at all. <c>IsReadyToUse</c> comes with it:
    /// it exists for the frame gap between <c>Instantiate</c> and <c>Awake</c>, which
    /// a UI Toolkit view — built by a synchronous <c>CloneTree</c> in its constructor —
    /// does not have.
    /// </remarks>
    public interface IScreenViewBase : IUIView
    {
        public UniTask Open();
        public UniTask Close();
        public void    Hide();
        public void    Show();

        public void DestroySelf();

        public event Action ViewDidClose;
        public event Action ViewDidOpen;
        public event Action ViewDidDestroy;
    }

    /// <summary>
    /// A screen view that can hand out an <see cref="IViewSurface"/> for itself.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT folded into <see cref="IScreenViewBase"/>. Doing that would add
    /// a member every existing implementer of <see cref="IScreenView"/> would have to
    /// write, which is a source break for the six repositories consuming this package —
    /// and it would buy nothing for the uGUI path, where <c>BaseScreenPresenter</c>
    /// already builds a <c>RectTransformViewSurface</c> from <c>RectTransform</c> itself.
    /// A backend whose view cannot be reduced to a <c>RectTransform</c> implements this
    /// instead, and nothing existing has to change.
    /// </remarks>
    public interface ISurfaceScreenView : IScreenViewBase
    {
        public IViewSurface ViewSurface { get; }
    }

    /// <summary>
    /// The responsibilities of a view are:
    /// -Handle references to elements needed for drawing (Textures, FXs, etc)
    /// -Perform Animations
    /// -Receive User Input
    /// -..
    /// </summary>
    /// <remarks>
    /// The uGUI screen view. Shape is unchanged: it still requires exactly
    /// <c>RectTransform</c>, <c>IsReadyToUse</c>, and the members now inherited from
    /// <see cref="IScreenViewBase"/>.
    /// </remarks>
    public interface IScreenView : IScreenViewBase
    {
        public RectTransform RectTransform { get; }
        public bool          IsReadyToUse  { get; }
    }
}
