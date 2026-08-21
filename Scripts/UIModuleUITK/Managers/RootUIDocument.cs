namespace GameFoundation.Scripts.UIModule.UITK.Managers
{
    using GameFoundation.Scripts.UIModule.UITK.View;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// The UI Toolkit analogue of <c>RootUICanvas</c>: one <see cref="UIDocument"/> whose
    /// <c>rootVisualElement</c> carries the three layers a screen can live in.
    /// </summary>
    /// <remarks>
    /// Still a MonoBehaviour, because <see cref="UIDocument"/> still is one — that is the
    /// only piece of the UI Toolkit path that has to live on a GameObject.
    ///
    /// Layers are resolved BY NAME from the UXML, rather than serialized references, since
    /// there is no such thing as an inspector reference to a VisualElement. A name that
    /// does not resolve is reported once at Awake and falls back to the document root, so a
    /// mis-named layer shows up in the log instead of as a screen that renders nowhere.
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    public class RootUIDocument : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        [SerializeField] private string rootUIShowElementName    = "root-ui-show";
        [SerializeField] private string rootUIClosedElementName  = "root-ui-closed";
        [SerializeField] private string rootUIOverlayElementName = "root-ui-overlay";

        public UIDocument UIDocument => this.uiDocument;

        public VisualElement RootUIShowElement    { get; private set; }
        public VisualElement RootUIClosedElement  { get; private set; }
        public VisualElement RootUIOverlayElement { get; private set; }

        /// <summary>The three roots as backend-agnostic layers, cached — never per reparent.</summary>
        public VisualElementViewLayer ShowLayer    { get; private set; }
        public VisualElementViewLayer ClosedLayer  { get; private set; }
        public VisualElementViewLayer OverlayLayer { get; private set; }

        private void Awake()
        {
            if (this.uiDocument == null) this.uiDocument = this.GetComponent<UIDocument>();

            var root = this.uiDocument.rootVisualElement;

            if (root == null)
            {
                // Nothing to resolve against, and constructing layers over a null element
                // would throw out of Awake. Report it and leave the layers null; the
                // failure is already in the log by the time anything tries to use them.
                Debug.LogError($"{nameof(RootUIDocument)} on {this.gameObject.name} has no rootVisualElement; is a source asset assigned to the UIDocument?", this);
                return;
            }

            this.RootUIShowElement    = this.Resolve(root, this.rootUIShowElementName);
            this.RootUIClosedElement  = this.Resolve(root, this.rootUIClosedElementName);
            this.RootUIOverlayElement = this.Resolve(root, this.rootUIOverlayElementName);

            this.ShowLayer    = new(this.RootUIShowElement);
            this.ClosedLayer  = new(this.RootUIClosedElement);
            this.OverlayLayer = new(this.RootUIOverlayElement);
        }

        private VisualElement Resolve(VisualElement root, string elementName)
        {
            if (string.IsNullOrEmpty(elementName)) return root;

            var element = root.Q<VisualElement>(elementName);

            if (element == null)
            {
                Debug.LogError($"Can not find VisualElement named '{elementName}' in {this.gameObject.name}; falling back to the document root.", this);
                return root;
            }

            return element;
        }
    }
}
