using NewHorizons;
using NewHorizons.Handlers;
using UnityEngine;

namespace NomaiSky;

[UsedInUnityProject]
public class RefuelingTool : OWItem
{
    public static RefuelingTool Instance { get; private set; }

    public IInputCommands ActivateKey = InputLibrary.toolActionPrimary;
    private ScreenPrompt _activatePrompt;

    private bool _toolActive;
    private bool _fillingFuel;
    private RefuelingResource _currentResource;
    private RefuelingResource _submergedResource;

    public float Range = 5f;
    public float Speed = 500f;

    public ShipFuelGauge FuelGauge;
    private ShipResources _shipResources;
    private MeshRenderer[] _thrusterRenderers;
    private Light[] _thrusterLights;
    private Color _currentThrusterColor = Color.black;
    private Color _defaultThrusterColor;
    private Texture _defaultThrusterRamp;

    public GameObject Indicator;
    [ColorUsage(false, true)]
    public Color IndicatorIdleColor;
    [ColorUsage(false, true)]
    public Color IndicatorFillingColor;
    [ColorUsage(false, true)]
    public Color IndicatorFullColor;
    public float BlinkTime = 0.25f;
    private float _blinkTimer = 0;
    private OWRenderer _indicatorRenderer;
    private Light _indicatorLight;
    public ParticleSystem Particles;

    public AudioClip VacuumAudio;
    private OWAudioSource _vacuumAudioSource;
    public AudioClip FluidAudio;
    private OWAudioSource _fluidAudioSource;
    public AudioClip ActivateAudio;
    public AudioClip DeactivateAudio;
    public AudioClip FullAudio;
    private OWAudioSource _oneShotAudioSource;

    public override void Awake()
    {
        Instance = this;
        _type = ItemType.VisionTorch;

        GlobalMessenger.AddListener("GamePaused", HidePrompt);
        GlobalMessenger.AddListener("GameUnpaused", ShowPrompt);
        GlobalMessenger.AddListener("EnterMapView", HidePrompt);
        GlobalMessenger.AddListener("ExitMapView", ShowPrompt);
        GlobalMessenger.AddListener("EnterFlightConsole", HidePrompt);
        GlobalMessenger.AddListener("ExitFlightConsole", ShowPrompt);
        base.Awake();
    }

    public void Start()
    {
        _activatePrompt = new ScreenPrompt(ActivateKey, TranslationHandler.GetTranslation("RefuelingTool_Prompt", TranslationHandler.TextType.UI) + "   <CMD>");

        _shipResources = Locator.GetShipTransform().GetComponent<ShipResources>();
        FuelGauge._shipResources = _shipResources;

        PlayerAudioController playerAudioController = Locator.GetPlayerAudioController();
        _vacuumAudioSource = Instantiate(
            playerAudioController._oneShotSource,
            playerAudioController._oneShotSource.transform.parent
        );
        _fluidAudioSource = Instantiate(
            playerAudioController._oneShotSource,
            playerAudioController._oneShotSource.transform.parent
        );
        _oneShotAudioSource = Instantiate(
            playerAudioController._oneShotSource,
            playerAudioController._oneShotSource.transform.parent
        );
        _vacuumAudioSource.clip = VacuumAudio;
        _vacuumAudioSource.loop = true;
        _vacuumAudioSource.SetMaxVolume(1f);
        _fluidAudioSource.clip = FluidAudio;
        _fluidAudioSource.loop = true;
        _fluidAudioSource.SetMaxVolume(1f);
        _oneShotAudioSource.SetMaxVolume(0.5f);

        _indicatorRenderer = Indicator.GetComponentInChildren<OWRenderer>();
        _indicatorLight = Indicator.GetComponentInChildren<Light>();
        SetIndicator(Color.black);

        try
        {
            ThrusterFlameController[] flames = Locator.GetShipTransform().GetComponentsInChildren<ThrusterFlameController>(true);
            _thrusterRenderers = new MeshRenderer[flames.Length];
            _thrusterLights = new Light[flames.Length];
            for (int i = 0; i < flames.Length; i++)
            {
                _thrusterRenderers[i] = flames[i].GetComponent<MeshRenderer>();
                _thrusterLights[i] = flames[i].GetComponentInChildren<Light>();
            }
            _defaultThrusterColor = _thrusterLights[0].color;
            _defaultThrusterRamp = _thrusterRenderers[0].material.mainTexture;
        }
        catch
        {
            Debug.LogError("Failed to locate ship thrusters");
        }

        enabled = false;
    }

    public override void OnDestroy()
    {
        GlobalMessenger.RemoveListener("GamePaused", HidePrompt);
        GlobalMessenger.RemoveListener("GameUnpaused", ShowPrompt);
        GlobalMessenger.RemoveListener("EnterMapView", HidePrompt);
        GlobalMessenger.RemoveListener("ExitMapView", ShowPrompt);
        GlobalMessenger.AddListener("EnterFlightConsole", HidePrompt);
        GlobalMessenger.AddListener("ExitFlightConsole", ShowPrompt);
        base.OnDestroy();
        Destroy(_vacuumAudioSource.gameObject);
        Destroy(_fluidAudioSource.gameObject);
        Destroy(_oneShotAudioSource.gameObject);

    }

    public override string GetDisplayName()
    {
        return TranslationHandler.GetTranslation("RefuelingTool_Name", TranslationHandler.TextType.UI);
    }

    public override void PickUpItem(Transform holdTranform)
    {
        base.PickUpItem(holdTranform);

        Locator.GetPromptManager().AddScreenPrompt(_activatePrompt, PromptPosition.UpperRight, true);

        enabled = true;
    }

    public override void DropItem(Vector3 position, Vector3 normal, Transform parent, Sector sector, IItemDropTarget customDropTarget)
    {
        base.DropItem(position, normal, parent, sector, customDropTarget);

        Locator.GetPromptManager().RemoveScreenPrompt(_activatePrompt, PromptPosition.UpperRight);

        enabled = false;
    }

    public override void SocketItem(Transform socketTransform, Sector sector)
    {
        base.SocketItem(socketTransform, sector);

        Locator.GetPromptManager().RemoveScreenPrompt(_activatePrompt, PromptPosition.UpperRight);

        enabled = false;
    }

    public override bool CheckIsDroppable()
    {
        return !_toolActive;
    }

    public void Update()
    {
        if (OWInput.IsPressed(ActivateKey, InputMode.Character))
        {
            if (!_toolActive)
            {
                ActivateTool();
            }
        }
        else
        {
            if (_toolActive)
            {
                DeactivateTool();
            }
        }
    }

    public void FixedUpdate()
    {
        if (_toolActive)
        {
            float fuel = _shipResources.GetFractionalFuel();

            if (fuel < 1)
            {
                if (_submergedResource != null && !_fillingFuel)
                {
                    StartRefueling(_submergedResource);
                }
                else
                {
                    Transform aim = Locator.GetToolModeSwapper()._firstPersonManipulator.transform;
                    if (Physics.Raycast(aim.position, aim.forward, out RaycastHit hitInfo, Range, OWLayerMask.effectVolumeMask))
                    {
                        RefuelingResource resource = hitInfo.collider.GetComponent<RefuelingResource>();
                        if (resource == null)
                        {
                            StopRefueling();
                        }
                        else if (_currentResource != resource)
                        {
                            StartRefueling(resource);
                        }
                    }
                    else
                    {
                        StopRefueling();
                    }
                }
            }

            if (_fillingFuel)
            {
                if (_blinkTimer == 0)
                {
                    _blinkTimer = BlinkTime * 2;
                }

                if (fuel == 1)
                {
                    StopRefueling();
                    SetIndicator(IndicatorFullColor);
                    _oneShotAudioSource.PlayOneShot(FullAudio);
                }
                else
                {
                    SetIndicator(IndicatorFillingColor);

                    if (_currentResource.Amount > 0)
                    {
                        _shipResources.AddFuel(Speed * _currentResource.Efficiency * Time.fixedDeltaTime);
                    }
                    else
                    {
                        StopRefueling();
                    }

                    if (_currentResource.IsDrainable)
                    {
                        _currentResource.Drain(Speed * Time.fixedDeltaTime);
                    }
                }
            }
            else if (fuel < 1)
            {
                SetIndicator(IndicatorIdleColor);
            }
            else
            {
                SetIndicator(IndicatorFullColor);
            }

            if (_blinkTimer != 0)
            {
                if (_blinkTimer > BlinkTime)
                {
                    SetIndicator(Color.black);
                }
                _blinkTimer = Mathf.Max(0, _blinkTimer - Time.fixedDeltaTime);
            }
        }
    }

    void HidePrompt() => _activatePrompt.SetVisibility(false);
    void ShowPrompt() => _activatePrompt.SetVisibility(true);

    public void ActivateTool()
    {
        _oneShotAudioSource.PlayOneShot(ActivateAudio);
        _vacuumAudioSource.FadeIn(0.25f);

        _toolActive = true;
    }

    public void DeactivateTool()
    {
        Particles.Stop();

        _oneShotAudioSource.PlayOneShot(DeactivateAudio);
        _vacuumAudioSource.FadeOut(0.25f);

        StopRefueling();
        SetIndicator(Color.black);

        _toolActive = false;
    }

    public void SetIndicator(Color c)
    {
        _indicatorRenderer.SetEmissionColor(c);
        _indicatorLight.color = c;
    }

    public void StartRefueling(RefuelingResource resource)
    {
        _fillingFuel = true;
        _currentResource = resource;

        Particles.GetComponent<ParticleSystemRenderer>().material.color = resource.FluidColor;
        if (!Particles.isEmitting)
        {
            Particles.Play();
        }
        if (!_fluidAudioSource.isPlaying)
        {
            _fluidAudioSource.FadeIn(0.25f);
        }

        SetShipThrusterColor(resource.FlameColor, resource.FlameTexture);
    }

    public void StopRefueling()
    {
        _fillingFuel = false;
        _currentResource = null;

        if (Particles.isEmitting)
        {
            Particles.Stop();
        }
        if (_fluidAudioSource.isPlaying)
        {
            _fluidAudioSource.FadeOut(0.25f);
        }
    }

    public void SetShipThrusterColor(Color c, Texture t = null)
    {
        if (_currentThrusterColor != c)
        {
            _currentThrusterColor = c;

            foreach (MeshRenderer r in _thrusterRenderers)
            {
                r.material.mainTexture = c == Color.black ? _defaultThrusterRamp : t;
            }
            foreach (Light l in _thrusterLights)
            {
                l.color = c == Color.black ? _defaultThrusterColor : c;
            }
        }
    }

    public void SubmergeInResource(RefuelingResource resource)
    {
        _submergedResource = resource;
    }

    public void UnsubmergeInResource()
    {
        _submergedResource = null;
    }
}