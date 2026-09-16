using System.Diagnostics;

using SimWorld.God.View;
using SimWorld.Scenario;
using SimWorld.Sim;

using UnityEngine;
using UnityEngine.UI;

using Debug = UnityEngine.Debug;

namespace SimWorldHost
{
    /// <summary>
    /// Gives the renderer something to render. <see cref="GodViewBootstrap"/> deliberately never starts a
    /// game — it reads the "nothing founded yet" state honestly rather than faking a save — which is right
    /// for the god view and leaves <see cref="MapRenderer"/> with no map at all. This starts one: found a
    /// civilization, open its settlement through <c>God/View</c>, then drive the tick loop.
    ///
    /// <para/><b>The settlement is opened by tile through the seam, not by reaching into the world.</b> The
    /// core's own integration test walks <c>game.World.worldObjects.OfType&lt;Settlement&gt;()</c> because it
    /// is inside the core; a host that did the same would be holding a live object the seam exists to keep
    /// away from it. <see cref="GodViewSnapshot.Settlements"/> carries the tile, and
    /// <see cref="GodCommands.OpenSettlement"/> takes one, so the whole path is defNames and integers.
    ///
    /// <para/><b>Starting a game is the one thing that is not a View call</b>, because there is no seam for
    /// it and a god cannot open a settlement in a world nobody generated. That is bootstrap, the same
    /// category as <see cref="CoreContentBootstrap"/>, and it is confined to this file.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimHarness : MonoBehaviour
    {
        [Header("New game")]
        [SerializeField] private string _seed = "simworld-host";
        [SerializeField] private int _bandSize = 20;
        [SerializeField] private int _worldSubdivisions = 3;
        [SerializeField] private bool _startOnAwake = true;

        [Header("Tick pacing")]
        [SerializeField] private bool _paused;
        [SerializeField] private float _ticksPerSecond = 30f;

        /// <summary>
        /// Ticks per frame are capped so a slow tick cannot spiral. Without it, a frame that overran its
        /// budget would ask for more ticks next frame, which overruns further — the editor stops responding
        /// and it looks like a hang rather than a game running behind.
        /// </summary>
        [SerializeField] private int _maxTicksPerFrame = 8;

        [Header("Optional status readout")]
        [SerializeField] private Text _status;

        private float _tickCredit;

        /// <summary>True once a world exists and a settlement has been opened.</summary>
        public bool Running { get; private set; }

        /// <summary>The tile whose interior is open, or -1.</summary>
        public int OpenTile { get; private set; } = -1;

        /// <summary>Plain words for what the harness did or could not do.</summary>
        public string Status { get; private set; } = "Not started.";

        /// <summary>Pause or resume the tick loop without tearing the world down.</summary>
        public bool Paused
        {
            get { return _paused; }
            set { _paused = value; }
        }

        private void Awake()
        {
            CoreContentBootstrap.EnsureLoaded();
            if (_startOnAwake) StartGame();
        }

        [ContextMenu("Start game")]
        public void StartGame()
        {
            var watch = Stopwatch.StartNew();

            // World generation and founding are seconds of main-thread work on a 200x200 interior. Say so
            // in the log rather than let a one-off freeze read as the crash this project has already spent
            // an evening chasing.
            Debug.Log("[SimHarness] Generating world and founding a settlement — this blocks for a few seconds.");

            Game.NewGame(
                ScenarioDefOf.TribalStart.scenario,
                _seed,
                subdivisionOverride: _worldSubdivisions,
                soloStart: true,
                bandSize: _bandSize);

            long worldMs = watch.ElapsedMilliseconds;

            GodViewSnapshot god = GodViewSnapshot.Capture();
            if (god.Settlements.Count == 0)
            {
                Running = false;
                Status = "A world generated but founded no settlement.";
                Debug.LogWarning("[SimHarness] " + Status);
                return;
            }

            int tile = god.Settlements[0].Tile;
            GodCommandResult opened = GodCommands.OpenSettlement(tile);
            if (opened.Outcome != GodCommandOutcome.Done)
            {
                Running = false;
                Status = $"Could not open settlement on tile {tile}: {opened.Reason}";
                Debug.LogWarning("[SimHarness] " + Status);
                return;
            }

            OpenTile = tile;
            Running = true;
            _tickCredit = 0f;
            Status = $"{god.Settlements[0].Name} open on tile {tile}.";
            Debug.Log($"[SimHarness] {Status} World {worldMs} ms, interior {watch.ElapsedMilliseconds - worldMs} ms.");
        }

        private void Update()
        {
            if (_status != null) _status.text = Status;
            if (!Running || _paused || _ticksPerSecond <= 0f) return;

            _tickCredit += Time.unscaledDeltaTime * _ticksPerSecond;
            int ticks = Mathf.Min(_maxTicksPerFrame, Mathf.FloorToInt(_tickCredit));
            if (ticks <= 0) return;

            _tickCredit -= ticks;

            // Drop any credit beyond one frame's cap. Carrying it would queue up debt the loop can never
            // pay back, which is the same spiral the cap exists to prevent, one frame later.
            if (_tickCredit > _maxTicksPerFrame) _tickCredit = _maxTicksPerFrame;

            TickManager tm = Find.TickManager;
            if (tm == null) return;
            for (int i = 0; i < ticks; i++) tm.DoSingleTick();
        }
    }
}
