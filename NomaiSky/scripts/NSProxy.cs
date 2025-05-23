using UnityEngine;

namespace NomaiSky;

public class NSProxy : ProxyPlanet
{
    public GameObject planet;
    public override void Awake()
    {
        base.Awake();
        _mieCurveMaxVal = 0.1f;
        _mieCurve = AnimationCurve.EaseInOut(0.0011f, 1, 1, 0);
        // Start off
        _outOfRange = false;
        ToggleRendering(false);
    }
    public override void Initialize()
    {
        _realObjectTransform = planet.transform;
    }
    public override void Update()
    {
        if (planet == null || !planet.activeSelf)
        {
            _outOfRange = false;
            ToggleRendering(false);
            enabled = false;
            return;
        }
        base.Update();
    }
    public override void ToggleRendering(bool on)
    {
        base.ToggleRendering(on);
        foreach (Transform child in transform) child.gameObject.SetActive(on);
    }
}