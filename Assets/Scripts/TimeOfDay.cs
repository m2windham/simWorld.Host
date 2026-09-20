using UnityEngine;

namespace SimWorldHost
{
    /// <summary>
    /// Controls directional sun lighting and ambient environment for the settlement view.
    /// Provides the canonical Timberborn-style golden afternoon baseline (pitch 35°, yaw 45°, #FFF1D6,
    /// 1.25 lux, trilight ambient) and provides hooks for diurnal time-of-day progression.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class TimeOfDay : MonoBehaviour
    {
        [Header("Sun Reference")]
        [SerializeField] private Light _sun;

        [Header("Time of Day")]
        [Tooltip("Hour of the day from 0.0 to 24.0. Canonical reference is 14.0 (2:00 PM golden afternoon).")]
        [SerializeField, Range(0f, 24f)] private float _hour = 14f;

        [Tooltip("When enabled in play mode, time progresses automatically according to Day Length.")]
        [SerializeField] private bool _enableCycle;

        [Tooltip("Full 24-hour cycle duration in real-time seconds.")]
        [SerializeField] private float _dayLengthSeconds = 120f;

        [Header("Canonical Baseline (Hour 14)")]
        [SerializeField] private float _basePitch = 35f;
        [SerializeField] private float _baseYaw = 45f;
        [SerializeField] private float _baseIntensity = 1.25f;
        [SerializeField] private Color _sunColor = new Color(1f, 0.9451f, 0.8392f, 1f); // #FFF1D6

        [Header("Ambient Lighting (Trilight)")]
        [SerializeField] private bool _manageAmbient = true;
        [SerializeField] private Color _skyColor = new Color(0.4941f, 0.6353f, 0.7216f, 1f);     // #7EA2B8
        [SerializeField] private Color _equatorColor = new Color(0.6196f, 0.5412f, 0.4549f, 1f); // #9E8A74
        [SerializeField] private Color _groundColor = new Color(0.3412f, 0.2745f, 0.2078f, 1f);  // #574635

        /// <summary>
        /// Current hour of the day (0–24). Setting this recalculates sun position and ambient values.
        /// </summary>
        public float Hour
        {
            get => _hour;
            set
            {
                _hour = Mathf.Repeat(value, 24f);
                Apply();
            }
        }

        /// <summary>
        /// Whether time progression is active in Play Mode.
        /// </summary>
        public bool EnableCycle
        {
            get => _enableCycle;
            set => _enableCycle = value;
        }

        /// <summary>
        /// Real-world seconds for a full 24h diurnal cycle.
        /// </summary>
        public float DayLengthSeconds
        {
            get => _dayLengthSeconds;
            set => _dayLengthSeconds = Mathf.Max(1f, value);
        }

        private void Reset()
        {
            _sun = GetComponent<Light>();
            if (_sun == null)
            {
                _sun = FindAnyObjectByType<Light>();
            }
        }

        private void Awake()
        {
            if (_sun == null)
            {
                _sun = GetComponent<Light>();
            }
            if (_sun == null)
            {
                _sun = FindAnyObjectByType<Light>();
            }
            Apply();
        }

        private void Update()
        {
            if (_enableCycle && Application.isPlaying && _dayLengthSeconds > 0f)
            {
                _hour = Mathf.Repeat(_hour + (Time.deltaTime / _dayLengthSeconds) * 24f, 24f);
                Apply();
            }
        }

        private void OnValidate()
        {
            if (_sun == null)
            {
                _sun = GetComponent<Light>();
            }
            Apply();
        }

        /// <summary>
        /// Explicitly sets the time of day to a given hour.
        /// </summary>
        public void SetTime(float hour)
        {
            _hour = Mathf.Repeat(hour, 24f);
            Apply();
        }

        /// <summary>
        /// Applies current time settings to the directional sun and ambient lighting.
        /// If not cycling, exact canonical values are preserved.
        /// </summary>
        public void Apply()
        {
            if (_sun == null) return;

            if (!_enableCycle && Mathf.Approximately(_hour, 14f))
            {
                // Exact canonical baseline
                _sun.transform.rotation = Quaternion.Euler(_basePitch, _baseYaw, 0f);
                _sun.color = _sunColor;
                _sun.intensity = _baseIntensity;
                _sun.shadows = LightShadows.Soft;
            }
            else
            {
                // Diurnal arc progression
                // Canonical hour 14 aligns with pitch 35°, yaw 45°
                float hourDelta = _hour - 14f;
                float pitch = Mathf.Clamp(_basePitch + Mathf.Sin((_hour - 6f) / 12f * Mathf.PI) * 25f - 20f, -90f, 90f);
                float yaw = _baseYaw + hourDelta * 15f;

                _sun.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

                // Day / night intensity modulation
                float sunHeight = Mathf.Sin((_hour - 6f) / 12f * Mathf.PI);
                float intensityFactor = Mathf.Clamp01(sunHeight * 1.3f);
                _sun.intensity = _baseIntensity * intensityFactor;
                _sun.color = _sunColor;
                _sun.shadows = intensityFactor > 0.05f ? LightShadows.Soft : LightShadows.None;
            }

            if (_manageAmbient)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = _skyColor;
                RenderSettings.ambientEquatorColor = _equatorColor;
                RenderSettings.ambientGroundColor = _groundColor;
                RenderSettings.ambientIntensity = 1.0f;
            }
        }
    }
}
