using System.Collections.Generic;
using UnityEngine;
using LevelEditor;

namespace EditorExtension;

// Offscreen warning for hardcoded IgnorePlayerWhenOffScreen script
public class LandfallYouDidGreat : MonoBehaviour
{
    private bool _isWarned = false;

    private Dictionary<SpriteRenderer, Color> _originalColors = new Dictionary<SpriteRenderer, Color>();
    private SpriteRenderer[] _renderers;

    private void Start()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in _renderers)
        {
            if (sr != null)
            {
                _originalColors[sr] = sr.color;
            }
        }
    }

    private void Update()
    {
        bool isOffScreen = transform.position.y < Patches.CrateCollisionFix.GetCrateCollisionThreshold();

        if (isOffScreen && !_isWarned)
        {
            _isWarned = true;
            ApplyColor(new Color(0.5f, 0.5f, 0.5f, 0.5f));

            if (!ExtensionUI.IsFixCrateCollision)
            {
                Helper.SendModOutput($"Object {gameObject.name} too low (y < -11f), will ignore player collision!", Helper.LogType.Warning);
            }
        }
        else if (!isOffScreen && _isWarned)
        {
            _isWarned = false;
            RestoreColor();
        }
    }

    private void ApplyColor(Color color)
    {
        if (_renderers == null) return;
        foreach (var sr in _renderers)
        {
            if (sr != null)
            {
                sr.color = color;
            }
        }
    }

    private void RestoreColor()
    {
        if (_renderers == null) return;
        foreach (var sr in _renderers)
        {
            if (sr != null && _originalColors.ContainsKey(sr))
            {
                sr.color = _originalColors[sr];
            }
        }
    }
}