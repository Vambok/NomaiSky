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
    bool atRemoteFlightConsole = false, inRoastingMode = false;

    private bool _toolActive;
    private bool _fillingFuel;
    private bool _fillingPlayer;
    private RefuelingResource _currentResource;
    private RefuelingResource _submergedResource;

    public float Range = 5f;
    public float Speed = 500f;

    public ShipFuelGauge FuelGauge;
    private ShipResources _shipResources;
    private PlayerResources _playerResources;
    private MeshRenderer[] _thrusterRenderers;
    private MeshRenderer[] _playerThrusterRenderers;
    private Light[] _thrusterLights;
    private Light[] _playerThrusterLights;
    private Color _currentThrusterColor = Color.black;
    private Color _currentPlayerThrusterColor = Color.black;
    private Color _defaultThrusterColor;
    private Color _defaultPlayerThrusterColor;
    private Texture _defaultThrusterRamp;
    private Texture _defaultPlayerThrusterRamp;

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

        Listeners(true);
        base.Awake();
    }

    public void Start()
    {
        _activatePrompt = new ScreenPrompt(ActivateKey, TranslationHandler.GetTranslation("RefuelingTool_Prompt", TranslationHandler.TextType.UI) + "   <CMD>");

        _shipResources = Locator.GetShipTransform().GetComponent<ShipResources>();
        _playerResources = Locator.GetPlayerTransform().GetComponent<PlayerResources>();
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
        try {
            ThrusterFlameController[] flames = Locator.GetPlayerTransform().GetComponentsInChildren<ThrusterFlameController>(true);
            _playerThrusterRenderers = new MeshRenderer[flames.Length];
            _playerThrusterLights = new Light[flames.Length];
            for(int i = 0;i < flames.Length;i++) {
                _playerThrusterRenderers[i] = flames[i].GetComponent<MeshRenderer>();
                _playerThrusterLights[i] = flames[i].GetComponentInChildren<Light>();
            }
            _defaultPlayerThrusterColor = _playerThrusterLights[0].color;
            _defaultPlayerThrusterRamp = _playerThrusterRenderers[0].material.mainTexture;
        } catch {
            Debug.LogError("Failed to locate jetpack thrusters");
        }

        enabled = false;
    }

    public override void OnDestroy()
    {
        Listeners(false);
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

        if(PlayerState.UsingShipComputer() || PlayerState.IsViewingProjector() || OWTime.IsPaused() || PlayerState.InMapView() || PlayerState.AtFlightConsole() || PlayerState.InConversation() || PlayerState.UsingNomaiRemoteCamera() || PlayerState.IsPeeping() || atRemoteFlightConsole || inRoastingMode)
            _activatePrompt.SetVisibility(false);
        else
            _activatePrompt.SetVisibility(true);
    }

    public void FixedUpdate()
    {
        RefuelingResource resource = _submergedResource;
        if(_activatePrompt.IsVisible())
        {
            if(resource == null)
            {
                Transform aim = Locator.GetToolModeSwapper()._firstPersonManipulator.transform;
                if(Physics.Raycast(aim.position, aim.forward, out RaycastHit hitInfo, Range, OWLayerMask.effectVolumeMask))
                {
                    resource = hitInfo.collider.GetComponent<RefuelingResource>();
                }
            }
            if(resource == null || PlayerState.IsInsideShip())
                _activatePrompt.SetText(TranslationHandler.GetTranslation("RefuelingTool_Prompt", TranslationHandler.TextType.UI) + "   <CMD>");
            else
                _activatePrompt.SetText(TranslationHandler.GetTranslation("RefuelingTool_Prompt", TranslationHandler.TextType.UI) + " " + resource.Name + "   <CMD>");
        }
        if(_toolActive)
        {
            float playerFuel = _playerResources.GetFuelFraction();
            float fuel = _shipResources.GetFractionalFuel() * playerFuel;

            if(fuel < 1)
            {
                if(resource == null || PlayerState.IsInsideShip())
                {
                    StopRefueling();
                }
                else if(_currentResource != resource || (playerFuel < 1) != _fillingPlayer)
                {
                    StartRefueling(resource, playerFuel < 1);
                }
            }

            if(_fillingFuel)
            {
                if(_blinkTimer == 0)
                {
                    _blinkTimer = BlinkTime * 2;
                }

                if(fuel == 1)
                {
                    StopRefueling();
                    SetIndicator(IndicatorFullColor);
                    _oneShotAudioSource.PlayOneShot(FullAudio);
                }
                else
                {
                    SetIndicator(IndicatorFillingColor);

                    if(_currentResource.Amount > 0)
                    {
                        if(!_fillingPlayer) _shipResources.AddFuel(Speed * _currentResource.Efficiency * Time.fixedDeltaTime);
                    }
                    else
                    {
                        StopRefueling();
                    }

                    if(_currentResource.IsDrainable)
                    {
                        _currentResource.Drain(Speed * Time.fixedDeltaTime);
                    }
                }
            }
            else if(fuel < 1)
            {
                SetIndicator(IndicatorIdleColor);
            }
            else
            {
                SetIndicator(IndicatorFullColor);
            }

            if(_blinkTimer != 0)
            {
                if(_blinkTimer > BlinkTime)
                {
                    SetIndicator(Color.black);
                }
                _blinkTimer = Mathf.Max(0, _blinkTimer - Time.fixedDeltaTime);
            }
        }
    }

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

    public void StartRefueling(RefuelingResource resource, bool fillingPlayer)
    {
        _fillingFuel = true;
        _fillingPlayer = fillingPlayer;
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

        NomaiSky.Instance.SetFlameColor(resource.FlameColor, resource.Name, !fillingPlayer);
        SetThrusterColor(!fillingPlayer, resource.FlameColor, resource.FlameTexture);
        if(fillingPlayer)
            _playerResources.StartRefillResources(true, false);
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

    public void SetThrusterColor(bool ship, Color c, Texture t = null)
    {
        if (ship)
        {
            if (_currentThrusterColor != c)
            {
                _currentThrusterColor = c;

                foreach (MeshRenderer r in _thrusterRenderers)
                {
                    r.material.mainTexture = t ?? _defaultThrusterRamp;
                }
                foreach (Light l in _thrusterLights)
                {
                    l.color = c == Color.black ? _defaultThrusterColor : c;
                }
            }
        }
        else
        {
            if(_currentPlayerThrusterColor != c)
            {
                _currentPlayerThrusterColor = c;

                foreach(MeshRenderer r in _playerThrusterRenderers)
                {
                    r.material.mainTexture = t ?? _defaultPlayerThrusterRamp;
                }
                foreach(Light l in _playerThrusterLights)
                {
                    l.color = c == Color.black ? _defaultPlayerThrusterColor : c;
                }
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

    void Listeners(bool add)
    {
        if(add)
        {
            GlobalMessenger<OWRigidbody>.AddListener("EnterRemoteFlightConsole", HidePrompt);
            GlobalMessenger.AddListener("ExitRemoteFlightConsole", ShowPromptRFC);
            GlobalMessenger<Campfire>.AddListener("EnterRoastingMode", HidePrompt);
            GlobalMessenger.AddListener("ExitRoastingMode", ShowPromptRM);
            GlobalMessenger.AddListener("ExitMapView", StopDaParticlesDamnit);
        }
        else
        {
            GlobalMessenger<OWRigidbody>.RemoveListener("EnterRemoteFlightConsole", HidePrompt);
            GlobalMessenger.RemoveListener("ExitRemoteFlightConsole", ShowPromptRFC);
            GlobalMessenger<Campfire>.RemoveListener("EnterRoastingMode", HidePrompt);
            GlobalMessenger.RemoveListener("ExitRoastingMode", ShowPromptRM);
            GlobalMessenger.RemoveListener("ExitMapView", StopDaParticlesDamnit);
        }
    }
    public ScreenPrompt GetPrompt() => _activatePrompt;
    void HidePrompt(OWRigidbody _) => atRemoteFlightConsole = true;
    void HidePrompt(Campfire _) => inRoastingMode = true;
    void ShowPromptRFC() => atRemoteFlightConsole = false;
    void ShowPromptRM() => inRoastingMode = false;
    void StopDaParticlesDamnit() => NomaiSky.Instance.ModHelper.Events.Unity.FireInNUpdates(Particles.Stop, 1);
}