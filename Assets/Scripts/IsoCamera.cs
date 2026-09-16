using UnityEngine;

namespace SimWorldHost
{
    /// <summary>
    /// The fixed isometric camera of <c>docs/host/rendering-method.md</c>. It pans and it zooms; it never
    /// rotates, and that is a load-bearing guarantee rather than a missing feature — back faces and hidden
    /// geometry can be culled at author time and walls can be facades precisely because no viewer will ever
    /// see round them. The rotation is written once, in <see cref="Apply"/>, and nothing else assigns it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class IsoCamera : MonoBehaviour
    {
        /// <summary>Pitch and yaw of the classic two-to-one-ish isometric view, in degrees.</summary>
        public const float Pitch = 30f;

        /// <summary>Yaw. 45 degrees puts map X and Z symmetrically across the screen.</summary>
        public const float Yaw = 45f;

        [Header("Framing")]
        [SerializeField] private Vector3 _pivot = new Vector3(100f, 0f, 100f);
        [SerializeField] private float _viewSize = 60f;
        [SerializeField] private float _minViewSize = 6f;
        [SerializeField] private float _maxViewSize = 260f;

        [Header("Controls")]
        [SerializeField] private float _panCellsPerSecond = 40f;
        [SerializeField] private float _zoomStep = 0.12f;
        [SerializeField] private bool _dragToPan = true;

        [Header("Auto-frame")]
        [SerializeField] private MapRenderer _renderer;

        private Camera _camera;
        private bool _framed;
        private Vector3 _dragOrigin;

        /// <summary>The ground point the camera is centred on.</summary>
        public Vector3 Pivot
        {
            get { return _pivot; }
            set { _pivot = value; Apply(); }
        }

        /// <summary>Half the vertical extent of the view, in cells.</summary>
        public float ViewSize
        {
            get { return _viewSize; }
            set { _viewSize = Mathf.Clamp(value, _minViewSize, _maxViewSize); Apply(); }
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 2000f;
            Apply();
        }

        private void LateUpdate()
        {
            AutoFrameOnce();
            HandleZoom();
            HandlePan();
            Apply();
        }

        /// <summary>Centre on a map of this size and zoom out far enough to hold all of it.</summary>
        public void Frame(int sizeX, int sizeZ)
        {
            _pivot = new Vector3(sizeX * 0.5f, 0f, sizeZ * 0.5f);

            // The map's diagonal is what spans the screen under a 45 degree yaw, not its edge.
            float diagonal = Mathf.Sqrt(sizeX * (float)sizeX + sizeZ * (float)sizeZ);
            _viewSize = Mathf.Clamp(diagonal * 0.6f, _minViewSize, _maxViewSize);
            Apply();
        }

        private void AutoFrameOnce()
        {
            if (_framed || _renderer == null) return;
            if (!_renderer.HasScene || _renderer.SizeX <= 0) return;

            Frame(_renderer.SizeX, _renderer.SizeZ);
            _framed = true;
        }

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f)) return;

            // Multiplicative, so a step feels the same close in as far out.
            _viewSize = Mathf.Clamp(_viewSize * Mathf.Exp(-scroll * _zoomStep), _minViewSize, _maxViewSize);
        }

        private void HandlePan()
        {
            // Screen right and screen "up the map" flattened onto the ground, so panning follows the keys
            // the way the view looks rather than the way world axes happen to run.
            Vector3 right = transform.right;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (!Mathf.Approximately(h, 0f) || !Mathf.Approximately(v, 0f))
            {
                // Scale with zoom: at a wide view a keypress should cross a comparable part of the screen.
                float speed = _panCellsPerSecond * (_viewSize / 60f);
                _pivot += (right * h + forward * v) * (speed * Time.unscaledDeltaTime);
            }

            if (!_dragToPan) return;

            if (Input.GetMouseButtonDown(2)) _dragOrigin = Input.mousePosition;
            else if (Input.GetMouseButton(2))
            {
                Vector3 delta = Input.mousePosition - _dragOrigin;
                _dragOrigin = Input.mousePosition;

                // Orthographic: one screen pixel is a fixed number of world units, so the drag can be
                // converted exactly rather than tuned by feel.
                float unitsPerPixel = _viewSize * 2f / Mathf.Max(1, Screen.height);
                _pivot -= (right * delta.x + forward * delta.y) * unitsPerPixel;
            }
        }

        private void Apply()
        {
            if (_camera == null) _camera = GetComponent<Camera>();

            // The one place rotation is ever written.
            transform.rotation = Quaternion.Euler(Pitch, Yaw, 0f);

            // Orthographic, so distance changes nothing about the image — it only has to clear the tallest
            // thing on the map and stay inside the far plane.
            transform.position = _pivot - transform.forward * 500f;

            _camera.orthographic = true;
            _camera.orthographicSize = _viewSize;
        }
    }
}
