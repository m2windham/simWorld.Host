using SimWorld.God.View;
using UnityEngine;
using UnityEngine.UI;

namespace SimWorldHost
{
    /// <summary>
    /// First vertical slice of the god view: proves the Unity host can call across the engine-free
    /// seam (<c>GodViewSnapshot.Capture()</c>) and render what comes back. Deliberately does not call
    /// <c>Game.NewGame</c> — <see cref="GodViewSnapshot.Capture()"/> is documented safe before a world
    /// exists, so this reads the "nothing founded yet" state honestly rather than faking a save.
    /// </summary>
    public sealed class GodViewBootstrap : MonoBehaviour
    {
        [SerializeField] private Text _label = null!;
        [SerializeField] private float _refreshSeconds = 0.5f;

        private float _nextRefresh;

        private void Awake() => CoreContentBootstrap.EnsureLoaded();

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + _refreshSeconds;

            GodViewSnapshot snapshot = GodViewSnapshot.Capture();
            _label.text = Format(snapshot);
        }

        private static string Format(GodViewSnapshot s)
        {
            return $"Tick {s.TicksGame} — {s.DateLabel}\n"
                + $"Settlements: {s.Civilization.SettlementCount}  "
                + $"Population: {s.Civilization.TotalPopulation}\n"
                + $"Era: {s.Civilization.EraLabel ?? "(none)"}\n"
                + $"Edicts available: {s.Edicts.Count}";
        }
    }
}
