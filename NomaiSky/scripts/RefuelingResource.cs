using UnityEngine;

namespace NomaiSky;

public class RefuelingResource : MonoBehaviour
{
    public string Name;
    public Color FluidColor = Color.black;
    public Color FlameColor = Color.black; // black sets default
    public Texture2D FlameTexture; // supplying a ramp per resource is easier than trying to tint the material
    public bool IsDrainable = false;
    public float Amount = 10000; // ship holds 10000
    public float Efficiency = 1; // multiplies refueling speed, but not the speed of this draining
    public bool Submersible = true;
    private OWTriggerVolume _trigger;

    public void Awake()
    {
        if (Submersible)
        {
            _trigger = GetComponent<OWTriggerVolume>();
            _trigger.OnEntry += OnEntry;
            _trigger.OnExit += OnExit;
        }
    }

    public void OnDestroy()
    {
        if (Submersible)
        {
            _trigger.OnEntry -= OnEntry;
            _trigger.OnExit -= OnExit;
        }
    }

    public void Drain(float amount)
    {
        Amount = Mathf.Max(0, Amount - amount);
    }

    private void OnEntry(GameObject other)
    {
        if (other.GetAttachedOWRigidbody().CompareTag("Player"))
        {
            RefuelingTool.Instance.SubmergeInResource(this);
        }
    }

    private void OnExit(GameObject other)
    {
        if (other.GetAttachedOWRigidbody().CompareTag("Player"))
        {
            RefuelingTool.Instance.UnsubmergeInResource();
        }
    }
}
