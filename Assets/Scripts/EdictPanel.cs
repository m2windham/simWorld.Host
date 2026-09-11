using System.Collections.Generic;
using SimWorld.God.View;
using UnityEngine;
using UnityEngine.UI;

namespace SimWorldHost
{
    /// <summary>
    /// The write half of the god view. Reads <see cref="GodViewSnapshot.Edicts"/> for what to show and
    /// calls <see cref="GodCommands.IssueEdict"/> for what a click does — never anything else. Every row's
    /// button carries <see cref="EdictOption.Reason"/> verbatim rather than a re-derived explanation, per
    /// the read model's own contract (spec &#167;12a): a reimplemented rule is a rule that drifts.
    /// </summary>
    public sealed class EdictPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform _content = null!;
        [SerializeField] private GameObject _rowTemplate = null!;
        [SerializeField] private float _refreshSeconds = 1f;

        private readonly List<GameObject> _rows = new();
        private float _nextRefresh;

        private void Awake() => CoreContentBootstrap.EnsureLoaded();

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + _refreshSeconds;

            IReadOnlyList<EdictOption> edicts = GodViewSnapshot.Capture().Edicts;
            Rebuild(edicts);
        }

        private void Rebuild(IReadOnlyList<EdictOption> edicts)
        {
            foreach (GameObject row in _rows) Destroy(row);
            _rows.Clear();

            foreach (EdictOption edict in edicts)
            {
                GameObject row = Instantiate(_rowTemplate, _content);
                row.SetActive(true);
                row.name = "Edict_" + edict.DefName;

                Text label = row.transform.Find("Label").GetComponent<Text>();
                label.text = $"{edict.Label} — {edict.Reason}";

                Button button = row.transform.Find("Button").GetComponent<Button>();
                Text buttonLabel = button.transform.Find("Text").GetComponent<Text>();
                buttonLabel.text = edict.Availability == EdictAvailability.Active ? "Active" : "Issue";
                button.interactable = edict.Availability == EdictAvailability.Available;

                string defName = edict.DefName; // local copy for the closure
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Issue(defName));

                _rows.Add(row);
            }
        }

        private static void Issue(string defName)
        {
            GodCommandResult result = GodCommands.IssueEdict(defName);
            Debug.Log($"[EdictPanel] IssueEdict({defName}) -> {result.Outcome}: {result.Reason}");
        }
    }
}
