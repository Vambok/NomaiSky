using HarmonyLib;//
using OWML.Common;//
using OWML.ModHelper;//
using System;//?
using System.Collections.Generic;//
using System.Globalization;//?
using System.IO;//
using UnityEngine;//
//using System.Diagnostics;

namespace NomaiSky;
public class NomaiSky : ModBehaviour {
    // CUSTOMIZATION PARAMETERS:
    const int minStarRadius = 1600,
        maxStarRadius = 6400,
        minPlanetSqrtRadius = 7, //Never less than 3!
        maxPlanetSqrtRadius = 31,
        minMoonSqrtRadius = 3, //Never less than 3!
        maxMoonSqrtRadius = 14,
        maxPlanets = 8,
        maxMoons = 5;
    const float tidalLockProba = 0.2f,
        waterProba = 0.1f,
        oxygenProbaIfWater = 0.75f,
        oxygenProbaNoWater = 0.5f,
        //If oxygen:
        treeProbaIfOxygen = 0.5f,
        meanNbBigTrees = 30, //Mean number of trees per square kilometer
        meanNbSmallTrees = 30,
        //
        fogProbaIfAtmBig = 0.25f, //Atmosphere big enough is 50% chance
        cloudsProbaIfAtmBig = 0.2f,
        //If clouds:
        //cloudsGDProba = 0.2f,
        //cloudsQMProba = 0.2f, //If neither GD nor QM clouds: transparent type clouds
        cloudsMaxSpeed = 30, //In degrees per second
        lightningProbaIfClouds = 0.25f,
        rainProbaIfCloudsAndWater = 0.8f,
        rainProbaIfCloudsNoWater = 0.4f,
        //
        ringsProba = 0.1f,
        ringAvgInnerRadius = 2, //Never less than 1.25! (relative to body radius)
        ringAvgOuterRadius = 3, //>ringAvgInnerRadius! (relative to body radius)
        fuelMethaneProba = 0.2f,
        fuelAlcoholProba = 0.25f,
        fuelHydrogenProba = 0.1f,
        fuelStrangerProba = 0.1f; //If neither fuel will be Kerosene
    readonly string[] fuelNames = ["Methane", "Alcohol", "Hydrogen", "Strange Matter"];
    ///<summary>How many more bodies could fit in an orbit span with maxBodies (>0 to prevent constant spacing when maxBodies is hit)</summary>
    const int orbitalMargin = 1;
    ///<summary>A rare prop is this many times more likely to spawn on the planet than on each of its moons</summary>
    const int planetPropWeight = 2;
    const float maxFuel = 10000;
    const float warpDriveEfficiency = 1f; //The more efficient, the less fuel consumption
    const int mapRadius = 5;
    readonly float warpPower = 1f; // min 0.2 to max 1

    // INDUCED PARAMETERS:
    const int minPlanetRadius = minPlanetSqrtRadius * minPlanetSqrtRadius,
        maxPlanetRadius = maxPlanetSqrtRadius * maxPlanetSqrtRadius,
        maxHeightOnPlanet = maxPlanetRadius + maxPlanetSqrtRadius * 8,
        //Moons:
        minMoonRadius = minMoonSqrtRadius * minMoonSqrtRadius,
        maxMoonRadius = maxMoonSqrtRadius * maxMoonSqrtRadius,
        varMoonRadius = (maxMoonRadius - minMoonRadius) / 6,
        avgMoonRadius = minMoonRadius + varMoonRadius * 3,
        maxHeightOnMoon = maxMoonRadius + maxMoonSqrtRadius * 8,
        minMoonOrbit = maxHeightOnPlanet + maxHeightOnMoon,
        moonOrbitSpacing = 2 * maxHeightOnMoon,
        maxMoonOrbit = minMoonOrbit + moonOrbitSpacing * (maxMoons - 1 + orbitalMargin),
        //Planets:
        varPlanetRadius = (maxPlanetRadius - minPlanetRadius) / 6,
        avgPlanetRadius = minPlanetRadius + varPlanetRadius * 3,
        minPlanetOrbit = maxStarRadius + maxMoonOrbit + maxHeightOnMoon,
        planetOrbitSpacing = 2 * (maxMoonOrbit + maxHeightOnMoon),
        maxPlanetOrbit = minPlanetOrbit + planetOrbitSpacing * (maxPlanets - 1 + orbitalMargin),
        //Stars:
        varStarRadius = (maxStarRadius - minStarRadius) / 6,
        avgStarRadius = minStarRadius + varStarRadius * 3,
        //System:
        maxSystemRadius = maxPlanetOrbit + maxMoonOrbit + maxHeightOnMoon;

    // START:
    public static NomaiSky Instance;
    INewHorizons NewHorizons;
    bool hasDLC = false;
    bool hasRM = false;
    // GALACTIC MAP:
    (int x, int y, int z) currentCenter = (0, 0, 0);
    readonly Dictionary<(int x, int y, int z), (string name, string starName, float radius, Color32 color, Vector3 offset)> galacticMap = [];
    readonly Dictionary<(int x, int y, int z), (string name, float radius, Color32 color, string starName)> otherModsSystems = [];
    readonly List<(int x, int y, int z)> known = [];
    (int, int, int) lastVisited;
    GameObject visitedLines, distantLines, visitedRings;
    string travelMarkersConfig = "";
    readonly List<(int x, int y, int z)> visited = [(0, 0, 0)];
    public const int entryRadius = 10000 * (maxSystemRadius / 10000 + 1);
    public const int systemRadius = entryRadius * 2;
    // FLYING & WARPING:
    int numberFlew = 0;
    int numberWarped = 0;
    Vector3 entryPosition;
    Quaternion entryRotation;
    Vector3 entrySpeed;
    (int, int, int)? targetStar;
    bool wasAutopilotOn = false;
    float remainingFuel = maxFuel;
    // FUEL TOOL:
    Color32 jetpackColor;
    string jetpackFuel;
    Color32 thrustersColor;
    string thrustersFuel;
    readonly Dictionary<string, Texture2D> flameTextures = [];
    // GENERATION:
    readonly int galaxyName = 0;
    readonly List<(float esperance, int max, bool canRepeat, string prop)> rareProps = [];
    const string generationVersion = "0.4.0";//Changing this will cause a rebuild of all previously visited systems, increment only when changing the procedural generation!
    // UTILS:
    UnityEngine.UI.Text splashScreenText;
    readonly List<int> seenTexts = [];
    int lastLoad = 0;
    float timeOffset = 0;
    bool isWarping = false;
    (Transform transform, OWRigidbody body, ShipResources resources, ShipCockpitController cockpit, SuitPickupVolume suit, WarpController warp, Autopilot autopilot) ship;

    // START:
    public void Awake() {
        Instance = this;
        // You won't be able to access OWML's mod helper in Awake.
        // So you probably don't want to do anything here.
        // Use Start() instead.
    }
    public void Start() {
        //Starting here, you'll have access to OWML's mod helper.
        ModHelper.Console.WriteLine("Nomai's Sky is loaded!", MessageType.Success);//LOG
        //Get the New Horizons API and load configs
        NewHorizons = ModHelper.Interaction.TryGetModApi<INewHorizons>("xen.NewHorizons");
        NewHorizons.LoadConfigs(this);
        //Harmony
        new Harmony("Vambok.NomaiSky").PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        //Initializations
        //  DLC
        hasDLC = EntitlementsManager.IsDlcOwned() == EntitlementsManager.AsyncOwnershipStatus.Owned;
        //  Procedural generation:
        Heightmaps.Init();
        rareProps.Add((1, 1, false, "fuel"));//Must be first prop
        //rareProps.Add((1, 1, false, "{\"path\": \"" + (hasDLC ? "RingWorld_Body/Sector_RingInterior/Sector_Zone2/Structures_Zone2/EyeTempleRuins_Zone2/Interactables_EyeTempleRuins_Zone2/Prefab_IP_FuelTorch (1)\"" : "CaveTwin_Body/Sector_CaveTwin/Sector_NorthHemisphere/Sector_NorthSurface/Sector_Lakebed/Interactables_Lakebed/Prefab_HEA_FuelTank\", \"rotation\": {\"x\": 30, \"y\": 0, \"z\": 270}") + ", \"count\": 1}"));
        //  Other mods installed:
        otherModsSystems[(0, 0, 0)] = ("SolarSystem", 2000, new Color32(255, 125, 9, 255), "Sun");
        foreach(IModBehaviour mod in ModHelper.Interaction.GetMods()) {
            switch(mod.ModHelper.Manifest.UniqueName) {//+00(story) +0+(Jams) 00+(Owlks) -0+(crossover) -00(systems) -0-(reals) 00-(fun) +0-(fun story)
            case "Stonesword.ResourceManagement":
                hasRM = true;
                break;
            case "GameWyrm.HearthsNeighbor":
                otherModsSystems[(1, 0, 0)] = ("GameWyrm.HearthsNeighbor", 3000, new Color32(150, 150, 255, 255), "Neighbor Sun");
                break;
            case "Etherpod.LuminaTerra":
                otherModsSystems[(1, 1, 0)] = ("Hornfel's Discovery", 800, new Color32(186, 38, 13, 255), "Enduring Flame");
                break;
            case "O32.UnnamedMystery":
                otherModsSystems[(2, 0, 0)] = ("O32.UnnamedMystery", 1600, new Color32(92, 255, 173, 180), "Aetherion");
                break;
            case "O32.TimeDialator":
                otherModsSystems[(3, 0, 0)] = ("O32.TimeDialator", 1, new Color32(0, 0, 0, 255), "Regularity");
                break;
            case "Samster68.FretsQuest":
                otherModsSystems[(2, 0, 1)] = ("Samster68.BanjoGalaxy", 700, new Color32(200, 220, 250, 255), "White Sun");
                break;
            case "xen.ModJam3":
                otherModsSystems[(1, 0, 1)] = ("Jam3", 2000, new Color32(120, 150, 250, 255), "Jam 3 Sun");
                break;
            case "hearth1an.Intervention":
                otherModsSystems[(2, 0, 2)] = ("UnknownDimension", 150, new Color32(200, 220, 250, 0), "Void Star");
                break;
            case "smallbug.NHJam1":
                otherModsSystems[(2, 0, 3)] = ("smallbug.NHJam1", 2000, new Color32(255, 150, 50, 255), "Daylight");
                break;
            case "Echatsum.MisfiredJump":
                otherModsSystems[(3, 0, 3)] = ("Jam4System", 2000, new Color32(130, 250, 240, 255), "Jam4Sun");
                break;
            case "Tetraminus.BrokenBalance":
                otherModsSystems[(3, 0, 1)] = ("tetraminus.BBSystem", 1000, new Color32(200, 220, 250, 0), "Balance");
                break;
            case "AnonymousStrangerOW.TheStrangerTheyAre":
                otherModsSystems[(0, 0, 1)] = ("AnonymousStrangerOW.StrangerSystem", 2000, new Color32(255, 103, 0, 255), "Nearest Neighbor");
                break;
            case "O32.Owlystem":
                otherModsSystems[(0, -1, 1)] = ("O32.Owlystem", 2000, new Color32(199, 67, 58, 255), "Red Dwarf");
                break;
            case "CreativeNameTxt.theirhomeworld":
                otherModsSystems[(0, 1, 1)] = ("CreativeNameTxt.theirhomeworld"/*"CreativeNameTxt.their homeworld"???*/, 6500, new Color32(39, 230, 230, 255), "Suekondox");
                break;
            case "Tandicase.interstellargargantua":
                otherModsSystems[(-2, 0, 2)] = ("tandicase.Gargantua", 1500, new Color32(255, 170, 0, 255), "Gargantua");
                break;
            case "ErroneousCreationist.astroneersolarsystem":
                otherModsSystems[(-3, 0, 2)] = ("Socialist.AstroneerSystem", 1750, new Color32(227, 255, 250, 255), "Sol");
                break;
            case "O32.KSP":
                otherModsSystems[(-2, 0, 3)] = ("O32.KSP", 4000, new Color32(207, 197, 7, 255), "Kerbol (The Sun)");
                break;
            case "bismuthdistrict.9YearOldSystem":
                otherModsSystems[(-2, 0, 1)] = ("bismuthdistrict.9YearOldSystem", 2000, new Color32(207, 184, 54, 255), "Golden Sun");
                break;
            case "Spacepiano.TheSpiralSystem":
                otherModsSystems[(-1, 0, 0)] = ("Spacepiano.SpiralSystem", 202, new Color32(255, 255, 50, 0), "The Core");
                break;
            case "JackFoxtrot.CarsonSystem":
                otherModsSystems[(-3, 0, 0)] = ("JackFoxtrot.CarsonSystem", 5000, new Color32(255, 166, 77, 255), "Carson");
                break;
            case "O32.ssimaak":
                otherModsSystems[(-2, 0, 0)] = ("O32.O32.Kamika", 4500, new Color32(124, 6, 214, 255), "Purple Sun");
                otherModsSystems[(-2, 1, 0)] = ("O32.STS2", 1000, new Color32(255, 255, 255, 255), "Blinding Light");
                otherModsSystems[(-2, 0, -1)] = ("O32.StarSystems", 2048, new Color32(248, 217, 109, 255), "Sol");
                break;
            case "MegaPiggy.UpsilonAndromedae":
                otherModsSystems[(-3, 0, -3)] = ("MegaPiggy.UpsilonAndromedae", 2960, new Color32(181, 158, 25, 255), "Upsilon Andromedae");
                break;
            case "smallbug.trappist-1":
                otherModsSystems[(-4, 0, -4)] = ("smallbug.TRAPPIST-1", 1000, new Color32(255, 100, 0, 255), "TRAPPIST-1");
                break;
            case "xen.RealSolarSystem":
                otherModsSystems[(-5, 0, -5)] = ("xen.RealSolarSystem", 2000, new Color32(255, 158, 45, 255), "Sol");
                break;
            case "O32.Discord":
                otherModsSystems[(-1, 0, -5)] = ("O32.Discord", 1000, new Color32(50, 10, 100, 255), "CIGT(s)U(n)");
                break;
            case "Tlya.Grapefruit":
                otherModsSystems[(0, 0, -4)] = ("tlya.Grapefruit", 1000, new Color32(255, 40, 15, 0), "Grapefruit");
                break;
            case "Roggsy.enterthewarioverse":
                otherModsSystems[(0, 0, -5)] = ("rose.WarioSpace", 1850, new Color32(245, 177, 120, 255), "Wol");
                break;
            case "O32.FunnySystem":
                otherModsSystems[(1, 0, -4)] = ("O32.FunnySystem", 10000, new Color32(150, 0, 0, 0), "RED IMPOSTOR SUS");
                break;
            case "2walker2.Evacuation":
                otherModsSystems[(1, 0, -1)] = ("2walker2.OogaBooga", 700, new Color32(181, 161, 118, 0), "Spark");
                break;
                //case "manifest_uniqueName":
                //    otherModsSystems[(x, y, z)] = ("starSystem", "size", new Color32("tint.r", "tint.g", "tint.b", "tint.a"), "name");
                //    break;
            default:
                break;
            }
        }
        //  Init starting system:
        ModHelper.Events.Unity.RunWhen(PlayerData.IsLoaded, InitSolarSystem);
        GlobalMessenger.AddListener("EnterMapView", TLVisibilityShow);
        GlobalMessenger.AddListener("ExitMapView", TLVisibilityHide);
        //  Spawn into it:
        NewHorizons.GetStarSystemLoadedEvent().AddListener(SpawnIntoSystem);
        OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen); // We start on title screen
        LoadManager.OnStartSceneLoad += OnStartSceneLoad;
        LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
    }
    public void OnStartSceneLoad(OWScene originalScene, OWScene loadScene) {
        if(loadScene == OWScene.SolarSystem) {
            ModHelper.Console.WriteLine("Start OW scene load!", MessageType.Success);//LOG
        }
    }
    public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene) {
        ModHelper.Console.WriteLine("Scene loaded!", MessageType.Success);//LOG
        if(newScene == OWScene.TitleScreen) {
            SubmitActionLoadScene actionLoadScene = GameObject.Find("Button-ResumeGame").GetComponent<SubmitActionLoadScene>();
            actionLoadScene.OnSubmitAction -= LoadCurrentSystem;
            actionLoadScene.OnSubmitAction += LoadCurrentSystem;
            actionLoadScene = GameObject.Find("Button-NewGame").GetComponent<SubmitActionLoadScene>();
            actionLoadScene.OnSubmitAction -= LoadCurrentSystem;
            actionLoadScene.OnSubmitAction += LoadCurrentSystem;
            if(splashScreenText == null) {
                GameObject splashScreenCanva = GameObject.Find("LoadManagerFadeCanvas");
                //splashScreenCanva.DontDestroyOnLoad().layer = Layer.UI;
                //splashScreenCanva.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                GameObject textObj = Instantiate(GameObject.Find("VersionText"));
                textObj.transform.SetParent(splashScreenCanva.transform, false);
                splashScreenText = textObj.GetComponent<UnityEngine.UI.Text>();
                splashScreenText.text = "";
                splashScreenText.alignment = TextAnchor.LowerCenter;
                splashScreenText.horizontalOverflow = HorizontalWrapMode.Wrap;
                splashScreenText.fontSize = 30;
                splashScreenText.GetComponent<RectTransform>().sizeDelta = new Vector2(-100, -100);
                //GameObject.Find("New Game Object").GetComponent<Text>().font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
            }
        } else if(newScene == OWScene.SolarSystem) {
            SubmitActionLoadScene actionLoadScene = GameObject.Find("PauseMenuBlock").transform.Find("PauseMenuItems/PauseMenuItemsLayout/Button-ExitToMainMenu").GetComponent<SubmitActionLoadScene>();
            actionLoadScene.OnSubmitAction -= SaveState;
            actionLoadScene.OnSubmitAction += SaveState;
        }
        /*string toto = Heightmaps.CreateHeightmap(Path.Combine(ModHelper.Manifest.ModFolderPath, "planets/heightmap")); //TEST
        ModHelper.Console.WriteLine("HM done! "+toto, MessageType.Success); //TEST*/
    }
    void SetWaitingText() {//CUSTOMIZATION
        if(ModHelper.Config.GetSettingsValue<bool>("Loading screen tips")) {
            int temp;
            if(numberFlew * numberWarped == 0) {
                temp = UnityEngine.Random.Range(0, 5);
                if(temp + numberFlew == 0) {
                    splashScreenText.text = "You can travel to any star system by flying until you get there!";
                    return;
                } else if(numberWarped - temp == -4) {
                    splashScreenText.text = "You can travel to any star system by warping with the galactic map!";
                    return;
                }
            }
            string[] waitingTexts = [
                "These loading screens are <b>sooo</b> boring",
                "The music in <color=purple>Nomai's Sky</color> was originally composed by <color=teal>Andrew Prahlow</color> for <color=green>Minecraft</color>",
                "NMS systems are big because NMS systems are also big!",
                //"Your warp drive is currently at <b>" + Mathf.CeilToInt(warpDriveEfficiency * 100) + "%</b> of its potential fuel efficiency",
                //"Your warp drive can currently reach <b>" + Mathf.CeilToInt(warpPower * 100) + "%</b> of its potential range",
                "Loading star shader. Please don't stare directly at it",
                "The planets are aligning! <i>It's purely cosmetic, but still exciting</i>",
                "Gravity calculations underway. Please remain grounded",
                "Your ship is... spinning? But that's normal. <i>Probably</i>",
                "Loading textures <i>(97% of them are just flat anyway)</i>",
                "This specific loading tip is <b>not</b> to be trusted",
                "Somewhere, a random number generator is deciding <i>your fate</i>",
                "Planetary naming rights were sold to the lowest bidder",
                "Yes, that tree was supposed to float like that. Definitely. That's... <i>gravity stuff</i>",
                "Each loading screen extends the lifespan of a dying star. <i>You're welcome</i>",
                "Deploying procedural chaos... Success rate: <i>acceptable</i>",
                "The star system you are entering may already contain you",
                "Each system is totally unique! <i>(and yet somehow every tree looks the same)</i>",
                "Reality rendering. Please hold"];
            string[] rareWaitingTexts = [
                "Oh my... this system has an <b>incredibly rare</b> and <b>awesome</b> planet! ... Wait. Oh no sorry it's not <i>that</i> system, nevermind",
                "If you read this, you're officially part of the QA team",
                "If you see <color=#b57edc>Lezzlebit</color> or <color=#cc8899>Trifid</color>, tell them the other one ate their sandwich, not me",
                "<i>I'm trapped in the devs' basement, writing these texts all day! Please send help!</i>",
                "All your feature requests must be sent to <color=#cc8899>Trifid</color>! <i>(they'll be heard... but will they be implemented?)</i>",
                "All your complaints must be sent to Sean Murray <i>(not the one from Bethesda, the one from Hello Games!)</i>",
                //"<color=white>I think Gneiss might need help with something</color>",
                "This message was removed from this build",
                "Some truths cannot be loaded",
                "<i>Ignorance is stability. Bugs are features. Lag is immersion</i>",
                "<color=white>The end is never the end is never the end is never the end is never the end is never</color>",
                "The cake is a <b>lie</b>"];
            string[] ultraRareWaitingTexts = ["",
                "This message was not supposed to load. If you're reading this it's already too late. You mu<i>^st</i><b>$%£@######</b>"];
            ultraRareWaitingTexts[0] = "This text only had a 1 in <b>" + ((waitingTexts.Length + 2) * rareWaitingTexts.Length * ultraRareWaitingTexts.Length) + "</b> chance of appearing!";
            temp = UnityEngine.Random.Range(-3, waitingTexts.Length);
            if(seenTexts.Contains(temp)) {
                temp = UnityEngine.Random.Range(-3, waitingTexts.Length);
                if(seenTexts.Contains(temp)) temp = UnityEngine.Random.Range(-3, waitingTexts.Length);
            }
            if(temp < -2) splashScreenText.text = "The system was generated "
                    + (new[] { "in 0.001s, but we add some waiting time for <i>vibes</i>",
                    "in 0.002s, but we add some waiting time to <i>build tension</i>",
                    "in 0.003s, but we add some time to <i>raise your expectations</i>",
                    "in 0.004s, but we give you some time to read this text",
                    "in 0.005s, but we make you wait for <i>dramatic effect</i>",
                    "in 0.006s, but we slowed it down for <i>style points</i>",
                    "in 0.007s, but we give you some time to <i>feel the moment</i>",
                    "in 0.008s, but the loading screen insisted on being <i>theatrical</i>",
                    "in 0.009s, but we added time to make it more realistic",
                    "faster than light, <i>but your SSD isn't</i>" })[UnityEngine.Random.Range(0, 10)];
            if(temp < -1) splashScreenText.text = (new[] { "This ", "The current " })[UnityEngine.Random.Range(0, 2)]
                        + (new[] { "loading text ", "message ", "text " })[UnityEngine.Random.Range(0, 3)]
                        + (new[] { "was ", "has been ", "might have been ", "is " })[UnityEngine.Random.Range(0, 4)]
                        + (new[] { "procedurally ", "randomly ", "automatically ", "" })[UnityEngine.Random.Range(0, 4)]
                        + (new[] { "generated ", "written ", "made ", "crafted ", "altered " })[UnityEngine.Random.Range(0, 5)]
                        + (new[] { "for ", "to achieve ", "to reach " })[UnityEngine.Random.Range(0, 3)]
                        + (new[] { "absolute fun", "max immersion", "infinite gameplay", "peak satisfaction" })[UnityEngine.Random.Range(0, 4)];
            else if(temp < 0) {
                temp = UnityEngine.Random.Range(-1, rareWaitingTexts.Length);
                splashScreenText.text = (temp < 0 ? ultraRareWaitingTexts[UnityEngine.Random.Range(0, ultraRareWaitingTexts.Length)] : rareWaitingTexts[temp]);
            } else {
                if(!seenTexts.Contains(temp)) seenTexts.Add(temp);
                splashScreenText.text = waitingTexts[temp];
            }
        } else splashScreenText.text = "";
    }
    void Update() {
        if(isWarping) {
            int test = Mathf.FloorToInt(Time.realtimeSinceStartup - timeOffset);
            if(test > lastLoad) {
                lastLoad = test;
                splashScreenText.text += ".";
            }
        }
    }

    // SAVE & CONFIG UTILS:
    public override void Configure(IModConfig config) {
        if(LoadManager.GetCurrentScene() == OWScene.SolarSystem) {
            TravelLinesVisibility(config.GetSettingsValue<string>("Visited systems display type"));
        }
    }
    void LoadState() {
        //Force button promps:
        if(!PlayerData.GetPromptsEnabled()) {
            SettingsSave save = PlayerData.CloneSettingsData();
            save.buttonPromptsEnabled = true;
            PlayerData.SetSettingsData(save);
            PlayerData.SaveSettings();
        }
        //Store flames textures:
        foreach(string fuelName in fuelNames) {
            flameTextures[fuelName] = new Texture2D(1, 1);
            ImageConversion.LoadImage(flameTextures[fuelName], File.ReadAllBytes(Path.Combine(ModHelper.Manifest.ModFolderPath, "assets", "images", "flames", fuelName.ToLower().Replace(' ','_') + ".png")));
        }
        //Loading save data:
        string[] temp;
        ShipLogFactSave saveData = PlayerData.GetShipLogFactSave("NomaiSky_currentCenter");
        if(saveData != null) {
            temp = saveData.id.Substring(1, saveData.id.Length - 2).Split(',');
            lastVisited = (int.Parse(temp[0]), int.Parse(temp[1]), int.Parse(temp[2]));
        } else lastVisited = currentCenter;

        saveData = PlayerData.GetShipLogFactSave("NomaiSky_visitedSystems");
        if(saveData != null) {
            temp = saveData.id.Substring(1, saveData.id.Length - 2).Split([")("], StringSplitOptions.None);
            foreach(string visitedSystem in temp) {
                int[] coords = Array.ConvertAll(visitedSystem.Split(','), int.Parse);
                known.Add((coords[0], coords[1], coords[2]));
            }
        } else known.Add(currentCenter);

        saveData = PlayerData.GetShipLogFactSave("NomaiSky_flewByShip");
        if(saveData != null) numberFlew = int.Parse(saveData.id);
        saveData = PlayerData.GetShipLogFactSave("NomaiSky_warpedWithMap");
        if(saveData != null) numberWarped = int.Parse(saveData.id);

        saveData = PlayerData.GetShipLogFactSave("NomaiSky_remainingFuel");
        if(saveData != null) remainingFuel = float.Parse(saveData.id, CultureInfo.InvariantCulture);

        saveData = PlayerData.GetShipLogFactSave("NomaiSky_jetpackColor");
        if(saveData != null) {
            temp = saveData.id.Substring(5, saveData.id.Length - 6).Split(',');
            jetpackColor = new Color32(byte.Parse(temp[0]), byte.Parse(temp[1]), byte.Parse(temp[2]), byte.Parse(temp[3]));
        }
        saveData = PlayerData.GetShipLogFactSave("NomaiSky_jetpackFuel");
        if(saveData != null) jetpackFuel = saveData.id;

        saveData = PlayerData.GetShipLogFactSave("NomaiSky_thrustersColor");
        if(saveData != null) {
            temp = saveData.id.Substring(5, saveData.id.Length - 6).Split(',');
            thrustersColor = new Color32(byte.Parse(temp[0]), byte.Parse(temp[1]), byte.Parse(temp[2]), byte.Parse(temp[3]));
        }
        saveData = PlayerData.GetShipLogFactSave("NomaiSky_thrustersFuel");
        if(saveData != null) thrustersFuel = saveData.id;
    }
    void SaveState() {
        PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_flewByShip"] = new ShipLogFactSave(numberFlew + "");
        PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_warpedWithMap"] = new ShipLogFactSave(numberWarped + "");
        if(ship.resources != null) {
            remainingFuel = ship.resources.GetFuel();
            PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_remainingFuel"] = new ShipLogFactSave(remainingFuel.ToString(CultureInfo.InvariantCulture));
        }
        if(!otherModsSystems.ContainsKey(currentCenter)) {
            if(jetpackFuel != null) {
                PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_jetpackColor"] = new ShipLogFactSave(jetpackColor.ToString());
                PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_jetpackFuel"] = new ShipLogFactSave(jetpackFuel);
            }
            if(thrustersFuel != null) {
                PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_thrustersColor"] = new ShipLogFactSave(thrustersColor.ToString());
                PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_thrustersFuel"] = new ShipLogFactSave(thrustersFuel);
            }
        }
        //Cleanup of previous versions' keys:
        //PlayerData._currentGameSave.shipLogFactSaves.Remove("NomaiSky_currentCenter");
        //PlayerData._currentGameSave.shipLogFactSaves.Remove("NomaiSky_visitedSystems");
        //PlayerData._currentGameSave.shipLogFactSaves.Remove("NomaiSky_flewByShip");
        //PlayerData._currentGameSave.shipLogFactSaves.Remove("NomaiSky_warpedWithMap");
        //PlayerData._currentGameSave.shipLogFactSaves.Remove("NomaiSky_remainingFuel");
        //PlayerData._currentGameSave.shipLogFactSaves.Remove("NomaiSky_jetpackColor");
        //PlayerData._currentGameSave.shipLogFactSaves.Remove("NomaiSky_jetpackFuel");
        //PlayerData._currentGameSave.shipLogFactSaves.Remove("NomaiSky_thrustersColor");
        //PlayerData._currentGameSave.shipLogFactSaves.Remove("NomaiSky_thrustersFuel");
        PlayerData.SaveCurrentGame();
    }
    void SaveLastVisit((int, int, int) coords) {
        lastVisited = coords;
        PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_currentCenter"] = new ShipLogFactSave(coords.ToString());
    }
    void RecordVisit((int, int, int) coords) {
        known.Add(coords);
        ShipLogFactSave saveData = PlayerData.GetShipLogFactSave("NomaiSky_visitedSystems");
        PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_visitedSystems"] = (saveData != null ? new ShipLogFactSave(saveData.id + coords.ToString()) : new ShipLogFactSave(coords.ToString()));
    }

    // INITIALISATION:
    void InitSolarSystem() {//here currentCenter should be 0,0,0
        LoadState();
        //Generating star neighborhood:
        for(int x = -mapRadius;x <= mapRadius;x++) {
            for(int y = -mapRadius;y <= mapRadius;y++) {
                for(int z = -mapRadius;z <= mapRadius;z++) {
                    galacticMap.Add((x + currentCenter.x, y + currentCenter.y, z + currentCenter.z), GetPlaceholder(x + currentCenter.x, y + currentCenter.y, z + currentCenter.z));
                }
            }
        }
        NewHorizons.CreatePlanet("{\"name\": \"Bel-O-Kan of " + galacticMap[currentCenter].starName + "\",\"$schema\": \"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json\",\"starSystem\": \"" + galacticMap[currentCenter].name + "\",\"Base\": {\"groundSize\": 50, \"surfaceSize\": 50, \"surfaceGravity\": 0},\"Orbit\": {\"showOrbitLine\": false,\"semiMajorAxis\": " + (systemRadius * (1 + 2.83f * mapRadius) / 5f).ToString(CultureInfo.InvariantCulture) + ",\"primaryBody\": \"" + galacticMap[currentCenter].starName + "\"},\"ShipLog\": {\"mapMode\": {\"remove\": true}}}", Instance);
        ModHelper.Console.WriteLine("System initialized succesfully!", MessageType.Success);//LOG
    }
    void LoadCurrentSystem() {
        ModHelper.Console.WriteLine("Loading system...");//LOG
        (int, int, int) newSystem = ModHelper.Config.GetSettingsValue<string>("Star system to start the game in") switch {
            "Last visited system" => lastVisited,
            "Random system" => (UnityEngine.Random.Range(-4 * mapRadius, 4 * mapRadius + 1), UnityEngine.Random.Range(-4 * mapRadius, 4 * mapRadius + 1), UnityEngine.Random.Range(-4 * mapRadius, 4 * mapRadius + 1)),
            "Distant system" => (UnityEngine.Random.Range(4 * mapRadius + 1, 40 * mapRadius + 1) * (int)Mathf.Sign(UnityEngine.Random.Range(-1, 1)), UnityEngine.Random.Range(4 * mapRadius + 1, 40 * mapRadius + 1) * (int)Mathf.Sign(UnityEngine.Random.Range(-1, 1)), UnityEngine.Random.Range(4 * mapRadius + 1, 40 * mapRadius + 1) * (int)Mathf.Sign(UnityEngine.Random.Range(-1, 1))),
            _ => (0, 0, 0),
        };
        if(newSystem != (0, 0, 0)) WarpToSystem(newSystem);
        else SaveLastVisit(newSystem);
    }

    // GALACTIC MAP:
    void TravelLinesVisibility(string config) {
        travelMarkersConfig = config;
        bool inMapView = PlayerState.InMapView();
        switch(config) {
        case "Lines":
            visitedLines?.SetActive(inMapView);
            distantLines?.SetActive(false);
            visitedRings?.SetActive(false);
            break;
        case "All lines":
            visitedLines?.SetActive(inMapView);
            distantLines?.SetActive(inMapView);
            visitedRings?.SetActive(false);
            break;
        case "Rings":
            visitedLines?.SetActive(false);
            visitedRings?.SetActive(inMapView);
            break;
        default:
            visitedLines?.SetActive(false);
            visitedRings?.SetActive(false);
            break;
        }
    }
    void TLVisibilityHide() => TLVisibilitySwitch(false);
    void TLVisibilityShow() => TLVisibilitySwitch(true);
    void TLVisibilitySwitch(bool state) {
        switch(travelMarkersConfig) {
        case "Lines":
            visitedLines?.SetActive(state);
            break;
        case "All lines":
            visitedLines?.SetActive(state);
            distantLines?.SetActive(state);
            break;
        case "Rings":
            visitedRings?.SetActive(state);
            break;
        default:
            break;
        }
    }
    (string, string, float, Color32, Vector3) GetPlaceholder(int x, int y, int z) {
        Random128.Initialize(galaxyName, x, y, z);
        if(otherModsSystems.ContainsKey((x, y, z))) {
            (string name, float radius, Color32 color, string starName) = otherModsSystems[(x, y, z)];
            Random128.Rng.Start("offset");
            return (name, starName, radius, color, new Vector3(Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius)));
        } else {
            StarInitializator(out string starName, out float radius, out Color32 starColor);
            //Random128.Rng.Start("offset");//Offset consistency not needed to be cool?
            return ("NomaiSky_" + galaxyName + "-" + x + "-" + y + "-" + z, starName, radius, starColor, new Vector3(Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius)));
        }
    }
    void DictUpdate(int dx, int dy, int dz) {
        if(dx != 0) {
            int xEnd = currentCenter.x + (dx > 0 ? mapRadius : -mapRadius - dx - 1);
            for(int x = currentCenter.x + (dx > 0 ? mapRadius - dx + 1 : -mapRadius);x <= xEnd;x++)
                for(int y = currentCenter.y - mapRadius;y <= currentCenter.y + mapRadius;y++)
                    for(int z = currentCenter.z - mapRadius;z <= currentCenter.z + mapRadius;z++)
                        if(!galacticMap.ContainsKey((x, y, z)))
                            galacticMap.Add((x, y, z), GetPlaceholder(x, y, z));
        }
        if(dy != 0) {
            int yEnd = currentCenter.y + (dy > 0 ? mapRadius : -mapRadius - dy - 1);
            int xMax = currentCenter.x + mapRadius - Mathf.Max(dx, 0);
            for(int y = currentCenter.y + (dy > 0 ? mapRadius - dy + 1 : -mapRadius);y <= yEnd;y++)
                for(int x = currentCenter.x - mapRadius - Mathf.Min(dx, 0);x <= xMax;x++)
                    for(int z = currentCenter.z - mapRadius;z <= currentCenter.z + mapRadius;z++)
                        if(!galacticMap.ContainsKey((x, y, z)))
                            galacticMap.Add((x, y, z), GetPlaceholder(x, y, z));
        }
        if(dz != 0) {
            int zEnd = currentCenter.z + (dz > 0 ? mapRadius : -mapRadius - dz - 1);
            int xMax = currentCenter.x + mapRadius - Mathf.Max(dx, 0);
            int yMax = currentCenter.y + mapRadius - Mathf.Max(dy, 0);
            for(int z = currentCenter.z + (dz > 0 ? mapRadius - dz + 1 : -mapRadius);z <= zEnd;z++)
                for(int x = currentCenter.x - mapRadius - Mathf.Min(dx, 0);x <= xMax;x++)
                    for(int y = currentCenter.y - mapRadius - Mathf.Min(dy, 0);y <= yMax;y++)
                        if(!galacticMap.ContainsKey((x, y, z)))
                            galacticMap.Add((x, y, z), GetPlaceholder(x, y, z));
        }
    }
    void GenerateNeighborhood() {
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Material mat = new(Shader.Find("Standard"));
        mat.EnableKeyword("_EMISSION");
        s.GetComponent<MeshRenderer>().material = mat;
        OWRigidbody OWRs = s.AddComponent<OWRigidbody>();
        GameObject RFs = new("RFsphere") {
            layer = LayerMask.NameToLayer("ReferenceFrameVolume")
        };
        RFs.SetActive(false);
        RFs.AddComponent<SphereCollider>().isTrigger = true;
        RFs.transform.SetParent(s.transform);
        RFs.transform.localScale = Vector3.one * entryRadius / 6400f;
        RFs.transform.localPosition = Vector3.zero;
        ReferenceFrameVolume RFVs = RFs.AddComponent<ReferenceFrameVolume>();
        RFVs._isPrimaryVolume = true;
        OWRs.GetReferenceFrame()._autopilotArrivalDistance = 1;
        RFVs._referenceFrame = OWRs.GetReferenceFrame();
        RFs.SetActive(true);

        Transform sunTransform = Locator.GetCenterOfTheUniverse().GetStaticReferenceFrame().gameObject.transform;
        Vector3 currentOffset = galacticMap[currentCenter].offset - sunTransform.position;
        string starName;
        float radius;
        Color32 color;
        Vector3 offset;
        (int, int, int) currentCoords;
        Dictionary<(int, int, int), Vector3> systemPositions = [];
        for(int x = -mapRadius;x <= mapRadius;x++) {
            for(int y = -mapRadius;y <= mapRadius;y++) {
                for(int z = -mapRadius;z <= mapRadius;z++) {
                    if((x, y, z) != (0, 0, 0) && (x * x + y * y + z * z <= mapRadius * mapRadius)) {
                        currentCoords = (currentCenter.x + x, currentCenter.y + y, currentCenter.z + z);
                        if(galacticMap.ContainsKey(currentCoords)) {
                            (_, starName, radius, color, offset) = galacticMap[currentCoords];
                            GameObject star = Instantiate(s);
                            star.name = starName;
                            star.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", 2 * (Color)color);
                            Vector3 systemPosition = 2 * systemRadius * new Vector3(x, y, z) + offset - currentOffset;
                            systemPositions.Add(currentCoords, systemPosition);
                            star.transform.position = systemPosition;
                            star.transform.localScale = radius * Vector3.one;
                            star.AddComponent<MVBGalacticMap>().Initializator(currentCoords, starName);
                            MakeProxy(starName, star, radius, color);
                        } else {
                            ModHelper.Console.WriteLine("Galactic key not found: " + currentCoords.ToString(), MessageType.Error);//LOG
                        }
                    }
                }
            }
        }
        Destroy(s);
        //Ring prefab
        s = new("Ring");
        LineRenderer lr = s.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        int segments = 32;
        int ringRadius = 30000;
        lr.widthMultiplier = 300;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = new Color(0, 1, 0, .5f);
        lr.positionCount = segments * 3;
        Vector3[] points = new Vector3[3 * segments];
        float angle;
        for(int i = 0;i < segments;i++) {
            angle = i * 2 * Mathf.PI / segments;
            points[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * ringRadius;
        }
        for(int i = 0;i < segments/4;i++) {
            angle = i * 2 * Mathf.PI / segments;
            points[i + segments] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * ringRadius;
        }
        for(int i = 0;i < segments;i++) {
            angle = i * 2 * Mathf.PI / segments;
            points[i + 5 * segments / 4] = new Vector3(0, Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
        }
        for(int i = segments/4;i < segments;i++) {
            angle = i * 2 * Mathf.PI / segments;
            points[i + 2 * segments] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * ringRadius;
        }
        lr.SetPositions(points);
        //Make visit markers
        visitedRings = new GameObject("VisitedRings");
        visitedRings.transform.SetParent(sunTransform);
        visitedLines = new GameObject("VisitedLines");
        visitedLines.transform.SetParent(sunTransform);
        distantLines = new GameObject("DistantLines");
        distantLines.transform.SetParent(visitedLines.transform);
        Vector3 posB = Vector3.zero;
        bool aInRange, bInRange = false;
        GameObject lineObj;
        for(int i = 0;i < known.Count - 1;i++) {
            aInRange = systemPositions.TryGetValue(known[i], out Vector3 posA);
            bInRange = systemPositions.TryGetValue(known[i + 1], out posB);
            if(!(aInRange && bInRange)) {
                Vector3Int relPosA = new(known[i].x - currentCenter.x, known[i].y - currentCenter.y, known[i].z - currentCenter.z);
                Vector3Int relPosB = new(known[i + 1].x - currentCenter.x, known[i + 1].y - currentCenter.y, known[i + 1].z - currentCenter.z);
                if(!aInRange) posA = relPosA * 2 * systemRadius;
                if(!bInRange) posB = (relPosB == Vector3Int.zero ? entryRadius * posA.normalized : relPosB * 2 * systemRadius);
                if(relPosA == Vector3Int.zero) posA = entryRadius * posB.normalized;
            }
            if(aInRange) {
                lineObj = Instantiate(s);
                lineObj.name = "Ring" + i;
                lineObj.transform.SetParent(visitedRings.transform);
                lineObj.transform.position = posA;
            }
            lineObj = new("Line" + i);
            lineObj.transform.SetParent((aInRange || bInRange) ? visitedLines.transform : distantLines.transform);
            lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.widthMultiplier = 400;
            lr.SetPositions([posA, posB]);
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = (known[i] == currentCenter ? Color.clear : (aInRange ? new Color(1, 1, 1, .3f) : new Color(1, 0, 0, .4f)));
            lr.endColor = (known[i + 1] == currentCenter ? Color.clear : (bInRange ? new Color(1, 1, 1, .3f) : new Color(1, 0, 0, .4f)));
        }
        if(bInRange) {
            lineObj = Instantiate(s);
            lineObj.name = "Ring" + (known.Count - 1);
            lineObj.transform.SetParent(visitedRings.transform);
            lineObj.transform.position = posB;
        }
        Destroy(s);
        TravelLinesVisibility(ModHelper.Config.GetSettingsValue<string>("Visited systems display type"));
    }
    public void MakeProxy(string name, GameObject planetGO, float radius, Color tint) {
        GameObject proxy = new($"{name}_Proxy");
        proxy.SetActive(false);
        NSProxy proxyController = proxy.AddComponent<NSProxy>();
        proxyController.planet = planetGO;
        try {
            GameObject starGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Material mat = new(Shader.Find("Standard"));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", 2 * tint);
            starGO.GetComponent<MeshRenderer>().material = mat;
            starGO.transform.parent = proxy.transform;
            starGO.transform.localPosition = Vector3.zero;
            starGO.transform.localScale = Vector3.one * radius;
            // Remove all collisions if there are any
            foreach(Collider col in proxy.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach(Renderer renderer in proxy.GetComponentsInChildren<Renderer>()) {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = true;
            }
            proxyController._atmosphere = null;
            proxyController._realObjectDiameter = radius;
        } catch(Exception ex) {
            Destroy(proxy);
            ModHelper.Console.WriteLine($"Exception thrown when generating proxy for [{name}]:\n{ex}", MessageType.Error);//LOG
            return;
        }
        proxy.SetActive(true);
    }

    // WARPING:
    public void MapExploration(ReferenceFrame targetReferenceFrame, ScreenPrompt prompt) {
        MVBGalacticMap data = targetReferenceFrame.GetOWRigidBody().GetComponent<MVBGalacticMap>();
        if(data != null && PlayerState.IsInsideShip()) {
            (int x, int y, int z) = (data.coords.x - currentCenter.x, data.coords.y - currentCenter.y, data.coords.z - currentCenter.z);
            if(x * x + y * y + z * z > mapRadius * mapRadius * warpPower)
                prompt.SetText($"{data.mapName} (too far for Warp Drive MK{Mathf.CeilToInt(warpPower * 5)}){Environment.NewLine}[{data.coords.x} : {data.coords.y} : {data.coords.z}]");
            else {
                float warpFuelConsumption = 0.003f * (targetReferenceFrame.GetOWRigidBody().transform.position - ship.transform.position).magnitude / warpDriveEfficiency;//Ship flying consumption = 1.4f * Mathf.Sqrt((targetReferenceFrame.GetOWRigidBody().transform.position - ship.transform.position).magnitude);
                prompt.SetText($"Warp to {data.mapName}{Environment.NewLine}[{data.coords.x} : {data.coords.y} : {data.coords.z}]  (-{Mathf.Ceil(100 * warpFuelConsumption / ship.resources._maxFuel)}% fuel)");
                if(OWInput.IsNewlyPressed(InputLibrary.markEntryOnHUD)) {
                    if(warpFuelConsumption > ship.resources.GetFuel()) {
                        string fuelNotif = "Not enough fuel (" + Mathf.Ceil(100 * ship.resources.GetFractionalFuel()) + "% left)";
                        ship.warp.fuelPrompt.SetText(fuelNotif);
                        if(!ship.warp.fuelPrompt.IsVisible()) {
                            NotificationManager.SharedInstance.PostNotification(new NotificationData(NotificationTarget.Ship, fuelNotif, 1f, true), false);
                            ship.warp.fuelPrompt.SetVisibility(true);
                            ModHelper.Events.Unity.FireInNUpdates(() => ship.warp.fuelPrompt.SetVisibility(false), Mathf.CeilToInt(1 / Time.deltaTime));
                        }
                    } else {
                        //Not working, screen freezes before://TEST
                        ship.warp.fuelPrompt.SetText("Warping...");
                        ship.warp.fuelPrompt.SetVisibility(true);
                        Locator.GetPromptManager().GetScreenPromptList(PromptPosition.Center).RefreshScreenPromptUIElementVisibility(ship.warp.fuelPrompt);
                        ModHelper.Events.Unity.FireInNUpdates(() => {
                            ship.resources.DrainFuel(warpFuelConsumption);
                            numberWarped++;
                            WarpToSystem(data.coords);
                        }, 4);
                    }
                }
            }
            prompt.SetVisibility(true);
        } else {
            prompt.SetVisibility(false);
        }
    }
    public void SpaceExploration(Vector3 currentSystemCubePosition) {
        (int x, int y, int z) actualCube = (Mathf.RoundToInt(-currentSystemCubePosition.x / (2 * systemRadius)), Mathf.RoundToInt(-currentSystemCubePosition.y / (2 * systemRadius)), Mathf.RoundToInt(-currentSystemCubePosition.z / (2 * systemRadius)));
        if(actualCube != (0, 0, 0)) {
            currentSystemCubePosition += new Vector3(actualCube.x * 2 * systemRadius, actualCube.y * 2 * systemRadius, actualCube.z * 2 * systemRadius);
            actualCube.x += currentCenter.x;
            actualCube.y += currentCenter.y;
            actualCube.z += currentCenter.z;
            if(galacticMap.ContainsKey(actualCube)) {
                currentSystemCubePosition += galacticMap[actualCube].offset;
                if(currentSystemCubePosition.magnitude < entryRadius) {
                    entryPosition = -currentSystemCubePosition;
                    entryRotation = ship.transform.rotation;
                    entrySpeed = ship.body.GetVelocity();
                    ReferenceFrame target = Locator.GetPlayerTransform().GetComponent<ReferenceFrameTracker>().GetReferenceFrame();
                    if(target != null) {
                        wasAutopilotOn = ship.autopilot.IsFlyingToDestination();
                        MVBGalacticMap targetCoords = target.GetOWRigidBody().GetComponent<MVBGalacticMap>();
                        if(targetCoords != null) {
                            targetStar = targetCoords.coords;
                            if(targetStar == actualCube) {
                                float slowDown = entrySpeed.magnitude - 2850;
                                if(slowDown > 0) {
                                    slowDown = Mathf.Min(slowDown, 300);
                                    entrySpeed -= entrySpeed.normalized * slowDown;
                                }
                            }
                        } else targetStar = null;
                    } else targetStar = null;
                    numberFlew++;
                    WarpToSystem(actualCube);
                }
            }
        }
    }
    void WarpToSystem((int, int, int) newCoords) {
        if(!isWarping) {
            ModHelper.Console.WriteLine("Warping to " + newCoords);//LOG
            isWarping = true;
            SetWaitingText();
            timeOffset = Time.realtimeSinceStartup;
            SaveLastVisit(newCoords);
            if(ship.resources != null) {
                SaveState();
                Locator.GetToolModeSwapper().GetItemCarryTool().DropItemInstantly(null, Locator.GetShipTransform());
            }
            bool waitForWrite = false;
            (int x, int y, int z) = currentCenter;
            currentCenter = newCoords;
            if(!visited.Contains(newCoords)) {
                DictUpdate(currentCenter.x - x, currentCenter.y - y, currentCenter.z - z);
                if(!otherModsSystems.ContainsKey(newCoords)) {
                    Random128.Initialize(galaxyName, currentCenter.x, currentCenter.y, currentCenter.z);
                    StarInitializator(out string starName, out float radius, out Color32 starColor);
                    string systemPath = Path.Combine(ModHelper.Manifest.ModFolderPath, "systems", "NomaiSky_" + galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z + ".json");
                    waitForWrite = true;
                    if(File.Exists(systemPath)) {
                        string[] split = File.ReadAllText(systemPath).Split(["\"version\":\""], 2, StringSplitOptions.None);
                        if(split.Length > 1 && split[1].Split(['"'], 2)[0] == generationVersion) waitForWrite = false;
                    }
                    if(waitForWrite) {
                        try {
                            File.WriteAllText(systemPath, SystemCreator(starName, radius, starColor));
                        } catch(ArgumentException e) {
                            ModHelper.Console.WriteLine($"Cannot write system file! {e.Message}", MessageType.Error);//LOG
                        } finally {
                            NewHorizons.ClearSystem(galacticMap[newCoords].name);
                            NewHorizons.LoadConfigs(Instance);
                            NewHorizons.ChangeCurrentStarSystem(galacticMap[newCoords].name);
                        }
                    }
                }
                if(!known.Contains(newCoords)) RecordVisit(newCoords);
                NewHorizons.CreatePlanet("{\"name\": \"Bel-O-Kan of " + galacticMap[currentCenter].starName + "\",\"$schema\": \"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json\",\"starSystem\": \"" + galacticMap[currentCenter].name + "\",\"Base\": {\"groundSize\": 50, \"surfaceSize\": 50, \"surfaceGravity\": 0},\"Orbit\": {\"showOrbitLine\": false,\"semiMajorAxis\": " + (systemRadius * (1 + 2.83f * mapRadius) / 5f).ToString(CultureInfo.InvariantCulture) + ",\"primaryBody\": \"" + galacticMap[currentCenter].starName + "\"},\"ShipLog\": {\"mapMode\": {\"remove\": true}}}", Instance);
                visited.Add(newCoords);
            }
            if(!waitForWrite) {
                NewHorizons.ChangeCurrentStarSystem(galacticMap[newCoords].name);
            }
        }
    }
    void SpawnIntoSystem(string systemName) {
        if(!otherModsSystems.ContainsKey(currentCenter)) {
            GameObject star = NewHorizons.GetPlanet(galacticMap[currentCenter].starName);
            if(star != null) {
                Transform shipSpawnPoint = star.transform.Find("ShipSpawnPoint");
                if(entryPosition != Vector3.zero) {
                    shipSpawnPoint.position = entryPosition;
                    shipSpawnPoint.rotation = entryRotation;
                    ModHelper.Events.Unity.FireInNUpdates(() => {
                        ship.transform.GetComponent<ShipBody>().SetVelocity(entrySpeed);
                        if(targetStar != null) {
                            ReferenceFrame target = null;
                            if(targetStar == currentCenter) {
                                target = star.GetComponent<OWRigidbody>().GetReferenceFrame();
                            } else {
                                MVBGalacticMap[] mvbGalacticMaps = FindObjectsOfType<MVBGalacticMap>();
                                foreach(MVBGalacticMap distantStar in mvbGalacticMaps) {
                                    if(distantStar.coords == targetStar) {
                                        target = distantStar.GetComponent<OWRigidbody>().GetReferenceFrame();
                                        break;
                                    }
                                }
                            }
                            if(target != null) {
                                Locator.GetPlayerTransform().GetComponent<ReferenceFrameTracker>().TargetReferenceFrame(target);
                                if(wasAutopilotOn) ship.autopilot.FlyToDestination(target);
                            }
                        }
                    }, 61);
                } else {
                    shipSpawnPoint.position = new Vector3(0, 10000, -34100);
                    shipSpawnPoint.eulerAngles = new Vector3(16.334f, 0, 0);
                }
            }
        }
        entryPosition = Vector3.zero;
        ModHelper.Events.Unity.FireInNUpdates(() => {
            if(ship.transform == null) {
                Transform shipTemp = Locator.GetShipTransform();
                ship = (shipTemp, Locator.GetShipBody(), shipTemp.GetComponent<ShipResources>(), shipTemp.GetComponentInChildren<ShipCockpitController>(), shipTemp.GetComponentInChildren<SuitPickupVolume>(), shipTemp.GetComponent<WarpController>(), shipTemp.GetComponent<Autopilot>());
            }
            //Set custom maxFuel unless Resource Management is enabled:
            if(!hasRM) ship.resources._maxFuel = maxFuel;
            //Keep current fuel level:
            if(currentCenter != (0, 0, 0)) ship.resources.SetFuel(remainingFuel);
            else ship.resources.SetFuel(maxFuel);//Thanks Slate!
            if(!otherModsSystems.ContainsKey(currentCenter)) {
                //Spawn into ship:
                PlayerSpawner playerSpawner = Locator.GetPlayerBody().GetComponent<PlayerSpawner>();
                playerSpawner.DebugWarp(playerSpawner.GetSpawnPoint(SpawnLocation.Ship));
                //Pickup fuel tool:
                RefuelingTool refuelingTool = GameObject.Find("FuelSiphon").GetComponent<RefuelingTool>();
                ToolModeSwapper toolModeSwapper = Locator.GetToolModeSwapper();
                ItemTool itemTool = toolModeSwapper.GetItemCarryTool();
                itemTool.PickUpItemInstantly(refuelingTool);
                itemTool.UpdateState(ItemTool.PromptState.PICK_UP, refuelingTool.GetDisplayName());
                toolModeSwapper.EquipToolMode(ToolMode.Item);
                ModHelper.Events.Unity.FireInNUpdates(() => {
                    Locator.GetPromptManager().TriggerVisibilityRefresh(refuelingTool.GetPrompt());
                }, 3);
                //Equip suit:
                ship.suit.OnPressInteract(ship.suit._interactVolume.GetInteractionAt(ship.suit._pickupSuitCommandIndex).inputCommand);
                //Sit at ship controls:
                ship.cockpit.OnPressInteract();
                //Set flames colors:
                if(jetpackFuel != null && flameTextures.ContainsKey(jetpackFuel)) refuelingTool.SetThrusterColor(false, jetpackColor, flameTextures[jetpackFuel]);
                if(thrustersFuel != null && flameTextures.ContainsKey(thrustersFuel)) refuelingTool.SetThrusterColor(true, thrustersColor, flameTextures[thrustersFuel]);
            }
            //Updates custom warp drive:
            if(ship.warp == null) ship.warp = ship.transform.gameObject.AddComponent<WarpController>();
            ship.warp.currentOffset = galacticMap[currentCenter].offset;
            //Spawn surrounding stars:
            GenerateNeighborhood();
            ModHelper.Console.WriteLine("Loaded into " + galacticMap[currentCenter].starName + " (" + systemName + ")! Current galaxy: " + galaxyName, MessageType.Success);//LOG
            isWarping = false;
            splashScreenText.text = "";
        }, 3);
    }

    // GENERATION:
    void StarInitializator(out string starName, out float radius, out Color32 color) {
        starName = StarNameGen("StarName");
        radius = GaussianDist(avgStarRadius, varStarRadius, "StarRadius");
        color = CGaussianDist(150, "StarColor", 255);
    }
    string SystemCreator(string starName, float radius, Color32 starColor) {
        string path = Path.Combine(ModHelper.Manifest.ModFolderPath, "planets", galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z);
        Directory.CreateDirectory(path + "/" + starName);
        File.WriteAllText(Path.Combine(path, starName, starName + ".json"), StarCreator(starName, radius, starColor));
        int nbPlanets = Mathf.CeilToInt(GaussianDist(maxPlanets / 2f, maxPlanets / 4f, 2, "NbPlanets"));
        List<string>[] props = new List<string>[nbPlanets];
        List<int> chosenInit = [];
        for(int i = 0;i < nbPlanets;i++) {
            props[i] = [];//Each planet get a prop list
            chosenInit.Add(i);//Init list of all planets for next loop
        }
        int index;
        foreach((float esperance, int max, bool repeat, string prop) in rareProps) {
            List<int> chosen = [.. chosenInit];
            for(int i = 0;i < max;i++) {
                if(Random128.Rng.Proba() < esperance / max) {
                    index = Random128.Rng.Range(0, chosen.Count);
                    if(repeat) {
                        props[index].Add(prop);
                    } else {
                        props[chosen[index]].Add(prop);
                        chosen.RemoveAt(index);
                    }
                }
            }
        }
        int[] orbits = new int[nbPlanets];
        int allowedOrbits = (maxPlanets - nbPlanets + orbitalMargin) * planetOrbitSpacing;
        Random128.Rng.Start("PlanetOrbits");
        for(int i = 0;i < nbPlanets;i++) {
            orbits[i] = Random128.Rng.Range(0, allowedOrbits);
        }
        Array.Sort(orbits);
        for(int i = 0;i < nbPlanets;i++) {
            int nbMoons = Random128.Rng.Range(1, 1 << (maxMoons + 1), i + "NbMoons");
            for(int j = 0;j <= maxMoons;j++) {
                if(nbMoons >= (1 << (maxMoons - j))) {
                    nbMoons = j;
                    break;
                }
            }
            string[] moonProps = new string[nbMoons + 1];
            foreach(string prop in props[i]) {
                index = Random128.Rng.Range(-planetPropWeight, nbMoons) + 1;
                if(index < 0) index = 0;
                moonProps[index] = (String.IsNullOrEmpty(moonProps[index]) ? "" : moonProps[index] + ",\n") + "\t\t" + prop;
            }
            string planetName = PlanetNameGen(i + "Name");
            Directory.CreateDirectory(Path.Combine(path, planetName));
            File.WriteAllText(Path.Combine(path, planetName, planetName + ".json"), PlanetCreator(starName, planetName, minPlanetOrbit + i * planetOrbitSpacing + orbits[i], moonProps[0]));
            if(nbMoons > 0) {
                int[] moonOrbits = new int[nbMoons];
                int allowedMoonOrbits = (maxMoons - nbMoons + orbitalMargin) * moonOrbitSpacing;
                Random128.Rng.Start(i + "MoonOrbits");
                for(int j = 0;j < nbMoons;j++) {
                    moonOrbits[j] = Random128.Rng.Range(0, allowedMoonOrbits);
                }
                Array.Sort(moonOrbits);
                for(int j = 0;j < nbMoons;j++) {
                    string moonName = PlanetNameGen(i + "MoonName" + j, true);
                    Directory.CreateDirectory(Path.Combine(path, planetName, moonName.Replace(' ', '_')));
                    File.WriteAllText(Path.Combine(path, planetName, moonName.Replace(' ', '_'), moonName.Replace(' ', '_') + ".json"), PlanetCreator(starName, moonName, minMoonOrbit + j * moonOrbitSpacing + moonOrbits[j], moonProps[j + 1], planetName));
                }
            }
        }
        return "{\"extras\":{\"mod_config\":{" +
            "\"version\":\"" + generationVersion + "\"}}," +
            "\"$schema\":\"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/star_system_schema.json\"," +
            "\"GlobalMusic\":{\"travelAudio\":\"assets/music/otherside.mp3\"}," +
            "\"allowOutsideItems\":false," +
            "\"respawnHere\":true}";
    }
    string StarCreator(string starName, float radius, Color32 starColor) {
        string relativePath = "planets/" + galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z + "/" + starName + "/";
        SpriteGenerator("star", relativePath + "map_star.png", starColor);
        string finalJson = $$"""
            {
                "name": "{{starName}}",
                "$schema": "https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json",
                "starSystem": "NomaiSky_{{galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z}}",
                "canShowOnTitle": false,
                "Base": {
                    "surfaceSize": {{radius.ToString(CultureInfo.InvariantCulture)}},
                    "surfaceGravity": {{GaussianDist(radius * 3 / 500, "StarGravity").ToString(CultureInfo.InvariantCulture)}},
                    "gravityFallOff": "inverseSquared",
                    "centerOfSolarSystem": true
                },
                "Orbit": {
                    "showOrbitLine": false,
                    "isStatic": true
                },
                "Star": {
                    "size": {{radius.ToString(CultureInfo.InvariantCulture)}},
                    "tint": {
                        "r": {{starColor.r}},
                        "g": {{starColor.g}},
                        "b": {{starColor.b}},
                        "a": 255
                    },
                    "lightTint": {
                        "r": {{(starColor.r + 510) / 3}},
                        "g": {{(starColor.g + 510) / 3}},
                        "b": {{(starColor.b + 510) / 3}},
                        "a": 255
                    },
                    "solarLuminosity": {{Random128.Rng.Range(0.5f, 3f, "StarLuminosity").ToString(CultureInfo.InvariantCulture)}},
                    "stellarDeathType": "none"
                },
                "Props": {
                    "details": [
            	        {
            		        "assetBundle": "assets/trifid_nomaisky",
            		        "path": "Assets/NomaiSky/FuelSiphon.prefab",
            		        "keepLoaded": true,
            		        "position": {"x": 0, "y": 10000, "z": -34100}
            	        }
                    ]
                },
                "Spawn": {
                    "shipSpawnPoints": [
                        {
                            "isDefault": true,
                            "position": {"x": 0, "y": 10000, "z": -34100},
                            "rotation": {"x": 16.334, "y": 0, "z": 0}
                        }
                    ]
                },
                "ShipLog": {
                    "mapMode": {
                        "revealedSprite": "{{relativePath}}map_star.png",
                        "scale": {{(radius / 500f).ToString(CultureInfo.InvariantCulture)}},
                        "selectable": false
                    }
                },
                "MapMarker": {"enabled": true}
            }
            """;
        return finalJson;
    }
    string PlanetCreator(string starName, string planetName, int orbit, string props = null, string orbiting = "") {
        string relativePath = "planets/" + galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z + "/" + (orbiting != "" ? orbiting + "/" : "") + planetName.Replace(' ', '_') + "/";
        string characteristics = "A ";
        List<char> vowels = ['a', 'e', 'i', 'o', 'u'];
        string finalJson = "{\n\"name\": \"" + planetName + "\",\n" +
            "\"$schema\": \"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json\",\n";
        finalJson += "\"starSystem\": \"NomaiSky_" + galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z + "\",\n";
        finalJson += "\"canShowOnTitle\": false,\n" +
            "\"Base\": {\n";
        float radius = (orbiting == "") ? GaussianDist(avgPlanetRadius, varPlanetRadius, planetName + "Radius") : GaussianDist(avgMoonRadius, varMoonRadius, planetName + "Radius");
        finalJson += "\t\"surfaceSize\": " + radius.ToString(CultureInfo.InvariantCulture) + ",\n";
        characteristics += (radius * (orbiting == "" ? 1 : avgPlanetRadius / avgMoonRadius)) switch {
            > avgPlanetRadius + 2.6f * varPlanetRadius => "enormous ",
            > avgPlanetRadius + 2 * varPlanetRadius => "huge ",
            > avgPlanetRadius + varPlanetRadius => "big ",
            < avgPlanetRadius - 2.6f * varPlanetRadius => "minuscule ",
            < avgPlanetRadius - 2 * varPlanetRadius => "tiny ",
            < avgPlanetRadius - varPlanetRadius => "small ",
            _ => ""
        };
        float temp = GaussianDist(radius * 12 / 500, planetName + "Gravity");
        finalJson += "\t\"surfaceGravity\": " + temp.ToString(CultureInfo.InvariantCulture) + ",\n";
        characteristics += (temp * 125 / radius) switch {
            > 5.6f => "ultradense ",
            > 5 => "dense ",
            > 4 => "compact ",
            < 0.4f => "ethereal ",
            < 1 => "sparse ",
            < 2 => "light ",
            _ => ""
        };
        finalJson += "\t\"gravityFallOff\": \"inverseSquared\"\n" +
            "},\n" +
            "\"HeightMap\": {\n";
        Color32 color = CGaussianDist(130, 50, 2.5f, planetName + "Color", 255);
        SpriteGenerator("planet", relativePath + "map_planet.png", color);
        Heightmaps.CreateHeightmap(Path.Combine(ModHelper.Manifest.ModFolderPath, relativePath), radius, color, out float hmLow, out float hmHigh, planetName + "Heightmap");
        float sqrtRadius = Mathf.Sqrt(radius);
        hmLow = radius + (hmLow * 11 - 3) * sqrtRadius;
        hmHigh = radius + (hmHigh * 11 - 3) * sqrtRadius;
        finalJson += "\t\"heightMap\": \"" + relativePath + "heightmap.png\",\n" +
            "\t\"minHeight\": " + (radius - 3 * sqrtRadius).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "\t\"maxHeight\": " + (radius + 8 * sqrtRadius).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "\t\"textureMap\": \"" + relativePath + "texture.png\",\n";
        /*finalJson += "\t\"emissionColor\": {\n" +
            "\t\t\"r\": " + colorR + ",\n" +
            "\t\t\"g\": " + colorG + ",\n" +
            "\t\t\"b\": " + colorB + "\n";
        finalJson += "\t},\n";//*/
        string stemp = GetColorName(color) + " ";
        temp = Mathf.Max(GaussianDist(0, 0.2f, 5, planetName + "Smoothness"), 0);
        finalJson += "\t\"smoothness\": " + temp.ToString(CultureInfo.InvariantCulture) + "\n";
        characteristics += temp switch {
            > 0.9f => "mirror ",
            > 0.8f => stemp + "mirror ",
            > 0.7f => stemp + "reflective ",
            > 0.6f => stemp + "polished ",
            > 0.5f => stemp + "shiny ",
            > 0.4f => stemp + "smooth ",
            _ => stemp
        };
        if(vowels.Contains(characteristics[2])) {
            characteristics = characteristics.Insert(1, "n");
        }
        finalJson += "},\n" +
            "\"Orbit\": {\n";
        if(orbiting != "") {
            finalJson += "\t\"isMoon\": true,\n" +
                "\t\"primaryBody\": \"" + orbiting + "\",\n";
            characteristics += "moon";
        } else {
            finalJson += "\t\"primaryBody\": \"" + starName + "\",\n";
            characteristics += "planet";
        }
        finalJson += "\t\"semiMajorAxis\": " + orbit + ",\n" +
            "\t\"inclination\": " + GaussianDist(0, 5, 18, planetName + "Inclination").ToString(CultureInfo.InvariantCulture) + ",\n" +
            "\t\"longitudeOfAscendingNode\": " + Random128.Rng.Range(0, 360, planetName + "LOAN") + ",\n" +
            (Random128.Rng.Proba(planetName + "Lock") < tidalLockProba ? "\t\"isTidallyLocked\": true,\n\t\"alignmentAxis\": {\"x\": 0, \"y\": 0, \"z\": 1},\n"
            : "\t\"axialTilt\": " + GaussianDist(0, 10, 9, planetName + "AxialTilt").ToString(CultureInfo.InvariantCulture) + ",\n") +
            "\t\"trueAnomaly\": " + Random128.Rng.Range(0, 360, planetName + "Anomaly") + "\n" +
            "},\n";
        float ringRadius = 0;
        if(orbiting == "" && Random128.Rng.Proba(planetName + "HasRings") < ringsProba) {
            finalJson += "\"Rings\": [{\n";
            float ringInnerRadius = GaussianDist(radius * ringAvgInnerRadius, radius * (ringAvgInnerRadius * 4 / 5 - 1) / 3, planetName + "RingInner");
            finalJson += "\t\"innerRadius\": " + ringInnerRadius.ToString(CultureInfo.InvariantCulture) + ",\n";
            float ringSpread = (radius * ringAvgOuterRadius - ringInnerRadius) / 2;
            ringRadius = GaussianDist(radius * ringAvgOuterRadius - ringSpread, ringSpread / 3, planetName + "RingOuter");
            finalJson += "\t\"outerRadius\": " + ringRadius.ToString(CultureInfo.InvariantCulture) + ",\n" +
                "\t\"texture\": \"" + relativePath + "rings.png\",\n" +
                "\t\"fluidType\": \"sand\"\n" +
                "}],\n";
            color = CGaussianDist(130, 50, 2.5f, null, BGaussianDist(200, 50, 4, planetName + "RingsColor"));
            Random128.Rng.Start(planetName + "Rings");
            SpriteGenerator("rings", relativePath, color, [(byte)Mathf.CeilToInt(128 * (1 - ringInnerRadius / ringRadius))]);
            characteristics += ", with " + GetColorName(color) + " rings";
            stemp = " and ";
        } else stemp = ", with ";
        bool hasWater = Random128.Rng.Proba(planetName + "Water") < waterProba;
        bool hasFuelOcean = props != null && props.Substring(2, 4) == "fuel";
        int fuelType = 0;
        if(hasFuelOcean) {
            props = props.Substring(6);
            fuelType = Random128.Rng.Proba(planetName + "Fuel") switch {
                < fuelMethaneProba => 1,
                < fuelMethaneProba + fuelAlcoholProba => 2,
                < fuelMethaneProba + fuelAlcoholProba + fuelHydrogenProba => 3,
                < fuelMethaneProba + fuelAlcoholProba + fuelHydrogenProba + fuelStrangerProba => 4,
                _ => 0
            };
        }
        float waterLevel = 0;
        if(hasWater || hasFuelOcean) {
            waterLevel = GaussianDist((hmHigh + hmLow) / 2 + (hmHigh - hmLow) / 8, (hmHigh - hmLow) / 4, 2, planetName + "WaterLevel");
            finalJson += "\"Water\": {\n" +
                "\t\"size\": " + waterLevel.ToString(CultureInfo.InvariantCulture) + ",\n" +
                "\t\"tint\": {\n" +
                (hasFuelOcean ?
                    "\t\t\"r\": " + fuelType switch { 0 => 204, 1 => 64, 2 => 255, 3 => 183, _ => 41 } + ",\n" +
                    "\t\t\"g\": " + fuelType switch { 0 => 103, 1 => 32, 2 => 196, 3 => 195, _ => 204 } + ",\n" +
                    "\t\t\"b\": " + fuelType switch { 0 => 0, 1 => 0, 2 => 44, 3 => 203, _ => 163 } + ",\n" +
                    "\t\t\"a\": 127\n" +
                    "\t},\n" +
                    "\t\"buoyancy\": " + fuelType switch { 0 => "0.8", 1 => "0.42", 2 => "0.79", 3 => "0.07", _ => "2" } + "\n"
                :
                    "\t\t\"r\": " + BGaussianDist(100, 50, 2, planetName + "WaterColor") + ",\n" +
                    "\t\t\"g\": " + BGaussianDist(100, 50, 2) + ",\n" +
                    "\t\t\"b\": " + BGaussianDist(255, 50) + ",\n" +
                    "\t\t\"a\": " + BGaussianDist(105, 50) + "\n" +
                    "\t},\n" +
                    "\t\"buoyancy\": " + GaussianDist(1, 0.2f, 5).ToString(CultureInfo.InvariantCulture) + "\n"
                ) +
                "},\n";
        }
        float atmosphereSize = GaussianDist(radius * 6 / 5, radius / 10, 4, planetName + "AtmSize");
        finalJson += "\"Atmosphere\": {\n" +
            "\t\"size\": " + atmosphereSize.ToString(CultureInfo.InvariantCulture) + ",\n" +
            "\t\"atmosphereTint\": {\n";
        color = CGaussianDist(200, 50, 4, null, BGaussianDist(255, 50, 5, planetName + "AtmColor"));
        SpriteGenerator("atmosphere", relativePath + "map_atmosphere.png", color);
        finalJson += "\t\t\"r\": " + color.r + ",\n" +
            "\t\t\"g\": " + color.g + ",\n" +
            "\t\t\"b\": " + color.b + ",\n" +
            "\t\t\"a\": " + color.a + "\n";
        finalJson += "\t},\n";
        bool hasClouds = false;
        if(atmosphereSize > radius * 6 / 5) {
            characteristics += stemp;
            stemp = GetColorName(color) + " atmosphere";
            if(Random128.Rng.Proba(planetName + "Fog") < fogProbaIfAtmBig) {
                finalJson += "\t\"fogTint\": {\n" +
                    "\t\t\"r\": " + BGaussianDist(130, 50, 2.5f, planetName + "FogColor") + ",\n" +
                    "\t\t\"g\": " + BGaussianDist(130, 50, 2.5f) + ",\n" +
                    "\t\t\"b\": " + BGaussianDist(130, 50, 2.5f) + ",\n" +
                    "\t\t\"a\": " + BGaussianDist(255, 50, 5) + "\n" +
                    "\t},\n" +
                    "\t\"fogSize\": " + Random128.Rng.Range(radius, atmosphereSize, planetName + "FogSize").ToString(CultureInfo.InvariantCulture) + ",\n" +
                    "\t\"fogDensity\": " + Random128.Rng.Proba(planetName + "FogDens").ToString(CultureInfo.InvariantCulture) + ",\n";
            }
            hasClouds = Random128.Rng.Proba(planetName + "Clouds") < cloudsProbaIfAtmBig;
            if(hasClouds) {
                finalJson += "\t\"clouds\": {\n" +
                "\t\t\"texturePath\": \"assets/images/clouds/clouds" + Random128.Rng.Range(0, 3) switch { 0 => "-cap.png", 1 => "-center.png", _ => ".png" } + "\",\n" +
                "\t\t\"tint\": {\"r\": " + color.r + ", \"g\": " + color.g + ", \"b\": " + color.b + "},\n" +
                "\t\t\"cloudsPrefab\": \"" + "transparent\",\n" + //Random128.Rng.Proba() switch { < cloudsGDProba => "giantsDeep", < cloudsGDProba + cloudsQMProba => "quantumMoon", _ => "basic" } + "\",\n" + //TEMP (need GD and QM textures)
                "\t\t\"hasLightning\": " + (Random128.Rng.Proba(planetName + "Lightning") < lightningProbaIfClouds).ToString().ToLower() + ",\n" +
                "\t\t\"innerCloudRadius\": " + atmosphereSize.ToString(CultureInfo.InvariantCulture) + ",\n" +
                "\t\t\"outerCloudRadius\": " + Random128.Rng.Range(atmosphereSize + 1, 2 * atmosphereSize - radius, planetName + "CloudSize").ToString(CultureInfo.InvariantCulture) + ",\n" +
                "\t\t\"rotationSpeed\": " + IGaussianDist(0, cloudsMaxSpeed / 3, planetName + "CloudRotation") + "\n" +
                "\t},\n";
                characteristics += "a cloudy " + stemp;
            } else {
                characteristics += (vowels.Contains(stemp[0]) ? "an " : "a ") + stemp;
            }
        }
        bool hasOxygenTrees;
        if(hasWater) {
            characteristics += ". There's water on the planet";
            hasOxygenTrees = Random128.Rng.Proba(planetName + "Oxygen") < oxygenProbaIfWater;
            stemp = ", and t";
        } else {
            hasOxygenTrees = Random128.Rng.Proba(planetName + "Oxygen") < oxygenProbaNoWater;
            stemp = ". T";
        }
        finalJson += "\t\"hasOxygen\": " + hasOxygenTrees.ToString().ToLower() + ",\n";
        if(hasOxygenTrees) {
            characteristics += stemp + "here seems to be oxygen";
            hasOxygenTrees = Random128.Rng.Proba(planetName + "Trees") < treeProbaIfOxygen;
        }
        finalJson += "\t\"hasTrees\": " + hasOxygenTrees.ToString().ToLower() + ",\n" +
            "\t\"hasRain\": " + (hasClouds ? (Random128.Rng.Proba(planetName + "Rain") < (hasWater ? rainProbaIfCloudsAndWater : rainProbaIfCloudsNoWater)).ToString().ToLower() : "false") + "\n" +
            "},\n";
        if(hasOxygenTrees || props != null) {
            finalJson += "\"Props\": {\n";
            if(hasFuelOcean) {
                finalJson += "\t\"details\": [{\n" +
                    "\t\t\"assetBundle\": \"assets/trifid_nomaisky\",\n" +
                    "\t\t\"path\": \"Assets/NomaiSky/"+fuelType switch { 0 => "Kerosene", 1 => "Methane", 2 => "Alcohol", 3 => "Hydrogen", _ => "StrangerFuel" } + "Ocean.prefab\",\n" +
                    "\t\t\"scale\": " + waterLevel.ToString(CultureInfo.InvariantCulture) +
                    "\n\t}]";
            }
            if(hasOxygenTrees || !String.IsNullOrEmpty(props)) {
                if(hasFuelOcean) finalJson += ",\n";
                finalJson += "\t\"scatter\": [\n" +
                    (!String.IsNullOrEmpty(props) ? props + (hasOxygenTrees ? ",\n" : "\n") : "");
                if(hasOxygenTrees) {
                    finalJson += "\t\t{\"path\": \"" + (hasDLC ? "DreamWorld_Body/Sector_DreamWorld/Sector_Underground/IslandsRoot/IslandPivot_B/Island_B/Props_Island_B/Tree_DW_L (3)" : "QuantumMoon_Body/Sector_QuantumMoon/State_TH/Interactables_THState/Crater_1/Crater_1_QRedwood/QRedwood (2)/Prefab_TH_Redwood") + "\", \"count\": " + IGaussianDist(meanNbBigTrees * Mathf.PI * radius * radius / 250000, planetName + "NbBTrees") + ", \"scale\": " + GaussianDist(1, 0.2f, planetName + "SizeBTrees").ToString(CultureInfo.InvariantCulture) + "},\n" +
                        "\t\t{\"path\": \"" + (hasDLC ? "DreamWorld_Body/Sector_DreamWorld/Sector_DreamZone_4/Props_DreamZone_4_Upper/Tree_DW_S_B" : "QuantumMoon_Body/Sector_QuantumMoon/State_TH/Interactables_THState/Crater_3/Crater_3_Sapling/QSapling/Tree_TH_Sapling") + "\", \"count\": " + IGaussianDist(meanNbSmallTrees * Mathf.PI * radius * radius / 250000, planetName + "NbLTrees") + ", \"scale\": " + GaussianDist(1, 0.2f, planetName + "SizeLTrees").ToString(CultureInfo.InvariantCulture) + "}\n";
                    characteristics += " and trees";
                }
                finalJson += "\t]";
            }
            finalJson += "\n},\n";
        }
        characteristics += ".";
        finalJson += "\"ShipLog\": {\n" +
            "\t\"spriteFolder\": \"" + relativePath + "sprites\",\n" +
            "\t\"xmlFile\": \"" + relativePath + "shiplogs.xml\",\n" +
            "\t\"mapMode\": {\n" +
            "\t\t\"outlineSprite\": \"assets/images/outline.png\",\n" +
            "\t\t\"revealedSprite\": \"" + relativePath + "map_atmosphere.png\",\n" +
            "\t\t\"scale\": " + (atmosphereSize / 500f).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "\t\t\"offset\": " + (atmosphereSize / 500f).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "\t\t\"details\": [\n" +
            "\t\t\t{\"revealedSprite\": \"" + relativePath + "map_planet.png\",\n" +
            "\t\t\t\"scale\": {\"x\": " + (radius / 500f).ToString(CultureInfo.InvariantCulture) + ",\"y\": " + (radius / 500f).ToString(CultureInfo.InvariantCulture) + "},\n";
        if(ringRadius > 0) {
            finalJson += "\t\t\t\"invisibleWhenHidden\": true},\n" +
                "\t\t\t{\"revealedSprite\": \"" + relativePath + "map_rings.png\",\n" +
                "\t\t\t\"scale\": {\"x\": " + (ringRadius / 500f).ToString(CultureInfo.InvariantCulture) + ",\"y\": " + (ringRadius / 500f).ToString(CultureInfo.InvariantCulture) + "},\n";
        }
        finalJson += "\t\t\t\"invisibleWhenHidden\": true}\n" +
            "\t\t]\n" +
            "\t}\n" +
            "},\n" +
            "\"Volumes\": {\n" +
            "\t\"revealVolumes\": [\n" +
            "\t\t{\"radius\": " + (1.6f * (ringRadius > 0 ? ringRadius : radius)).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "\t\t\"reveals\": [\"VAMBOK.NOMAISKY_" + generationVersion + "_" + galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z + "_" + planetName.Replace(' ', '_').ToUpper() + "\"]}\n" +
            "\t]\n" +
            "},\n" +
            "\"MapMarker\": {\"enabled\": true}\n}";
        AssetsMaker(relativePath, planetName, characteristics);
        return finalJson;
    }
    void SpriteGenerator(string mode, string path) { SpriteGenerator(mode, path, Color.clear); }
    void SpriteGenerator(string mode, string path, Color32 color, byte[] ringData = null) {
        path = Path.Combine(ModHelper.Manifest.ModFolderPath, path);
        int width, height;
        byte[] data;
        switch(mode) {
        case "star":
            width = height = 256;
            data = new byte[4 * width * height];
            for(int i = height - 1;i >= 0;i--) {
                for(int j = width - 1;j >= 0;j--) {
                    float radial = (i * 2f / height - 1) * (i * 2f / height - 1) + (j * 2f / width - 1) * (j * 2f / width - 1) + 0.1f;
                    if(radial < 1) {
                        data[i * width * 4 + j * 4 + 3] = color.a;
                        data[i * width * 4 + j * 4 + 2] = (byte)(color.b / (0.9f + radial));
                        data[i * width * 4 + j * 4 + 1] = (byte)(color.g / (0.9f + radial));
                        data[i * width * 4 + j * 4] = (byte)(color.r / (0.9f + radial));
                    } else if(radial < 1.1f) {
                        data[i * width * 4 + j * 4 + 3] = color.a;
                        data[i * width * 4 + j * 4 + 2] = (byte)((color.b + 255) / 2);
                        data[i * width * 4 + j * 4 + 1] = (byte)((color.g + 255) / 2);
                        data[i * width * 4 + j * 4] = (byte)((color.r + 255) / 2);
                    } else {
                        data[i * width * 4 + j * 4] = data[i * width * 4 + j * 4 + 1] = data[i * width * 4 + j * 4 + 2] = data[i * width * 4 + j * 4 + 3] = 0;
                    }
                }
            }
            break;
        case "planet":
            width = height = 256;
            data = new byte[4 * width * height];
            for(int i = height - 1;i >= 0;i--) {
                for(int j = width - 1;j >= 0;j--) {
                    if((i * 2f / height - 1) * (i * 2f / height - 1) + (j * 2f / width - 1) * (j * 2f / width - 1) < 1) {
                        data[i * width * 4 + j * 4 + 3] = color.a;
                        data[i * width * 4 + j * 4 + 2] = color.b;
                        data[i * width * 4 + j * 4 + 1] = color.g;
                        data[i * width * 4 + j * 4] = color.r;
                    } else {
                        data[i * width * 4 + j * 4] = data[i * width * 4 + j * 4 + 1] = data[i * width * 4 + j * 4 + 2] = data[i * width * 4 + j * 4 + 3] = 0;
                    }
                }
            }
            break;
        case "atmosphere":
            width = height = 256;
            data = new byte[4 * width * height];
            for(int i = height - 1;i >= 0;i--) {
                for(int j = width - 1;j >= 0;j--) {
                    float radial = (i * 2f / height - 1) * (i * 2f / height - 1) + (j * 2f / width - 1) * (j * 2f / width - 1);
                    if(radial < 0.97f) {
                        data[i * width * 4 + j * 4 + 3] = color.a;
                        data[i * width * 4 + j * 4 + 2] = color.b;
                        data[i * width * 4 + j * 4 + 1] = color.g;
                        data[i * width * 4 + j * 4] = color.r;
                    } else if(radial < 1) {
                        data[i * width * 4 + j * 4 + 3] = color.a;
                        data[i * width * 4 + j * 4 + 2] = (byte)((color.b + 255) / 2);
                        data[i * width * 4 + j * 4 + 1] = (byte)((color.g + 255) / 2);
                        data[i * width * 4 + j * 4] = (byte)((color.r + 255) / 2);
                    } else {
                        data[i * width * 4 + j * 4] = data[i * width * 4 + j * 4 + 1] = data[i * width * 4 + j * 4 + 2] = data[i * width * 4 + j * 4 + 3] = 0;
                    }
                }
            }
            break;
        case "map_rings":
            width = height = 256;
            data = new byte[4 * width * height];
            for(int i = height - 1;i >= 0;i--) {
                for(int j = width - 1;j >= 0;j--) {
                    data[i * width * 4 + j * 4 + 3] = ringData[Mathf.Min(Mathf.FloorToInt(Mathf.Sqrt((i - height/2) * (i - height/2) + (j - width/2) * (j - width/2))), 128)];
                    data[i * width * 4 + j * 4 + 2] = color.b;
                    data[i * width * 4 + j * 4 + 1] = color.g;
                    data[i * width * 4 + j * 4] = color.r;
                }
            }
            break;
        case "rings":
            width = 1;
            height = 1024;
            byte[] ringDataM = new byte[129];
            data = new byte[4 * width * height];
            for(int i = 0;i < height;i++) {//invert if inner top
                if(Random128.Rng.Range(0, Mathf.RoundToInt(height / 5)) == 0) {//to get ~ 5 changes (tweakable)
                    color.a = (byte)Random128.Rng.Range(0, 256);
                }
                if(i % Mathf.CeilToInt((float)height / ringData[0]) == 0) {
                    ringDataM[128 - ringData[0] + i / Mathf.CeilToInt((float)height / ringData[0])] = color.a;//ringdata[0]=1-69
                }
                data[i * 4] = color.r;
                data[i * 4 + 1] = color.g;
                data[i * 4 + 2] = color.b;
                data[i * 4 + 3] = color.a;
            }
            ringDataM[128] = 0;
            SpriteGenerator("map_rings", path + "map_rings.png", color, ringDataM);
            path += "rings.png";
            break;
        case "fact":
            string[] pathChunks = path.Split('/', '\\');
            File.Copy(path + "map_planet.png", path + "sprites/ENTRY_" + pathChunks[pathChunks.Length - 2].ToUpper() + ".png", true);
            return;
        default:
            return;
        }
        Texture2D tex = new(width, height, TextureFormat.RGBA32, false);
        tex.SetPixelData(data, 0);
        tex.Apply();
        File.WriteAllBytes(path, ImageConversion.EncodeToPNG(tex));
        Destroy(tex);
    }
    void AssetsMaker(string relativePath, string planetName, string characteristics = "A very mysterious planet.") {
        string path = Path.Combine(ModHelper.Manifest.ModFolderPath, relativePath);
        Directory.CreateDirectory(path + "/sprites");
        File.WriteAllText(path + "/shiplogs.xml", "<AstroObjectEntry xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/shiplog_schema.xsd\">\n" +
            "<ID>" + planetName.Replace(' ', '_').ToUpper() + "</ID>\n<Entry>\n<ID>ENTRY_" + planetName.Replace(' ', '_').ToUpper() + "</ID>\n<Name>" + planetName + "</Name>\n" +
            "<ExploreFact>\n<ID>VAMBOK.NOMAISKY_" + generationVersion + "_" + galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z + "_" + planetName.Replace(' ', '_').ToUpper() + "</ID>\n" +
            "<Text>" + characteristics + "</Text>\n" +
            "</ExploreFact>\n</Entry>\n</AstroObjectEntry>");
        SpriteGenerator("fact", relativePath);
    }
    // NAME GENERATION:
    string StarNameGen(string parameter) {
        string[] nm1 = ["a", "e", "i", "o", "u", "", "", "", "", "", "", "", "", "", "", "", "", "", ""];
        string[] nm2 = ["b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "n", "p", "q", "r", "s", "t", "v", "w", "x", "y", "z", "br", "cr", "dr", "gr", "kr", "pr", "sr", "tr", "str", "vr", "zr", "bl", "cl", "fl", "gl", "kl", "pl", "sl", "vl", "zl", "ch", "sh", "ph", "th"];
        string[] nm3 = ["a", "e", "i", "o", "u", "a", "e", "i", "o", "u", "a", "e", "i", "o", "u", "ae", "ai", "ao", "au", "aa", "ea", "ei", "eo", "eu", "ee", "ia", "io", "iu", "oa", "oi", "oo", "ua", "ue"];
        string[] nm4 = ["b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "n", "p", "q", "r", "s", "t", "v", "w", "x", "y", "z", "br", "cr", "dr", "gr", "kr", "pr", "sr", "tr", "str", "vr", "zr", "bl", "cl", "fl", "hl", "gl", "kl", "ml", "nl", "pl", "sl", "tl", "vl", "zl", "ch", "sh", "ph", "th", "bd", "cd", "gd", "kd", "ld", "md", "nd", "pd", "rd", "sd", "zd", "bs", "cs", "ds", "gs", "ks", "ls", "ms", "ns", "ps", "rs", "ts", "ct", "gt", "lt", "nt", "st", "rt", "zt", "bb", "cc", "dd", "gg", "kk", "ll", "mm", "nn", "pp", "rr", "ss", "tt", "zz"];
        string[] nm5 = ["", "", "", "", "", "", "", "", "", "", "", "", "", "b", "c", "d", "f", "g", "h", "k", "l", "m", "n", "p", "r", "s", "t", "x", "y", "b", "c", "d", "f", "g", "h", "k", "l", "m", "n", "p", "r", "s", "t", "x", "y", "cs", "ks", "ls", "ms", "ns", "ps", "rs", "ts", "ys", "ct", "ft", "kt", "lt", "nt", "ph", "sh", "th"];
        string result;

        Random128.Rng.Start(parameter);
        if(Random128.Rng.RandomBool()) {
            int rnd = Random128.Rng.Range(0, nm3.Length);
            result = nm1[Random128.Rng.Range(0, nm1.Length)] + nm2[Random128.Rng.Range(0, nm2.Length)] + nm3[rnd] + nm4[Random128.Rng.Range(0, nm4.Length)] + nm3[(rnd > 14) ? Random128.Rng.Range(0, 15) : Random128.Rng.Range(0, nm3.Length)] + nm5[Random128.Rng.Range(0, nm5.Length)];
        } else {
            result = nm1[Random128.Rng.Range(0, nm1.Length)] + nm2[Random128.Rng.Range(0, nm2.Length)] + nm3[Random128.Rng.Range(0, nm3.Length)] + nm5[Random128.Rng.Range(0, nm5.Length)];
        }
        return char.ToUpper(result[0]) + result.Substring(1);
    }
    string PlanetNameGen(string parameter, bool isMoon = false) {
        string[] nm1 = ["b", "c", "ch", "d", "g", "h", "k", "l", "m", "n", "p", "r", "s", "t", "th", "v", "x", "y", "z", "", "", "", "", ""];
        string[] nm2 = ["a", "e", "i", "o", "u"];
        string[] nm3 = ["b", "bb", "br", "c", "cc", "ch", "cr", "d", "dr", "g", "gn", "gr", "l", "ll", "lr", "lm", "ln", "lv", "m", "n", "nd", "ng", "nk", "nn", "nr", "nv", "nz", "ph", "s", "str", "th", "tr", "v", "z"];
        string[] nm3b = ["b", "br", "c", "ch", "cr", "d", "dr", "g", "gn", "gr", "l", "ll", "m", "n", "ph", "s", "str", "th", "tr", "v", "z"];
        string[] nm4 = ["a", "e", "i", "o", "u", "a", "e", "i", "o", "u", "a", "e", "i", "o", "u", "ae", "ai", "ao", "au", "a", "ea", "ei", "eo", "eu", "e", "ua", "ue", "ui", "u", "ia", "ie", "iu", "io", "oa", "ou", "oi", "o"];
        string[] nm5 = ["turn", "ter", "nus", "rus", "tania", "hiri", "hines", "gawa", "nides", "carro", "rilia", "stea", "lia", "lea", "ria", "nov", "phus", "mia", "nerth", "wei", "ruta", "tov", "zuno", "vis", "lara", "nia", "liv", "tera", "gantu", "yama", "tune", "ter", "nus", "cury", "bos", "pra", "thea", "nope", "tis", "clite"];
        string[] nm6 = ["una", "ion", "iea", "iri", "illes", "ides", "agua", "olla", "inda", "eshan", "oria", "ilia", "erth", "arth", "orth", "oth", "illon", "ichi", "ov", "arvis", "ara", "ars", "yke", "yria", "onoe", "ippe", "osie", "one", "ore", "ade", "adus", "urn", "ypso", "ora", "iuq", "orix", "apus", "ion", "eon", "eron", "ao", "omia"];
        string[] nm7 = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "", "", "", "", "", "", "", "", "", "", "", "", "", ""];
        string result;

        Random128.Rng.Start(parameter);
        if(isMoon) {
            result = nm3b[Random128.Rng.Range(0, nm3b.Length)] + nm6[Random128.Rng.Range(0, nm6.Length)] + " " + nm7[Random128.Rng.Range(0, nm7.Length)] + nm7[Random128.Rng.Range(0, nm7.Length)] + nm7[Random128.Rng.Range(0, nm7.Length)] + nm7[Random128.Rng.Range(0, nm7.Length)];
        } else {
            int rnd2, rnd = Random128.Rng.Range(0, 4);
            switch(rnd) {
            case 0:
                rnd = Random128.Rng.Range(0, nm1.Length);
                do {
                    rnd2 = Random128.Rng.Range(0, nm3.Length);
                } while(nm1[rnd] == nm3[rnd2]);
                result = nm1[rnd] + nm2[Random128.Rng.Range(0, nm2.Length)] + nm3[rnd2] + nm4[Random128.Rng.Range(0, nm4.Length)] + nm5[Random128.Rng.Range(0, nm5.Length)];
                break;
            case 1:
                rnd = Random128.Rng.Range(0, nm1.Length);
                do {
                    rnd2 = Random128.Rng.Range(0, nm3.Length);
                } while(nm1[rnd] == nm3[rnd2]);
                result = nm1[rnd] + nm2[Random128.Rng.Range(0, nm2.Length)] + nm3[rnd2] + nm6[Random128.Rng.Range(0, nm6.Length)];
                break;
            case 2:
                rnd = Random128.Rng.Range(0, nm1.Length);
                do {
                    rnd2 = Random128.Rng.Range(0, nm3b.Length);
                } while(nm1[rnd] == nm3[rnd2]);
                result = nm3b[rnd2] + nm2[Random128.Rng.Range(0, nm2.Length)] + nm1[rnd] + nm2[Random128.Rng.Range(0, nm2.Length)] + nm5[Random128.Rng.Range(0, nm5.Length)];
                break;
            default:
                result = nm1[Random128.Rng.Range(0, nm1.Length)] + nm4[Random128.Rng.Range(0, nm4.Length)] + nm5[Random128.Rng.Range(0, nm5.Length)];
                break;
            }
        }
        return char.ToUpper(result[0]) + result.Substring(1);
    }

    // UTILS:
    Color32 CGaussianDist(float mean, string parameter, byte alpha = 0) => CGaussianDist(mean, 0, 3, parameter, alpha);
    Color32 CGaussianDist(float mean, float sigma, string parameter, byte alpha = 0) => CGaussianDist(mean, sigma, 3, parameter, alpha);
    Color32 CGaussianDist(float mean, float sigma = 0, float limit = 3, string parameter = null, byte alpha = 0) {
        return new Color32(BGaussianDist(mean, sigma, limit, parameter), BGaussianDist(mean, sigma, limit), BGaussianDist(mean, sigma, limit), (alpha == 0 ? BGaussianDist(mean, sigma, limit) : alpha));
    }
    byte BGaussianDist(float mean, string parameter) => BGaussianDist(mean, 0, 3, parameter);
    byte BGaussianDist(float mean, float sigma, string parameter) => BGaussianDist(mean, sigma, 3, parameter);
    byte BGaussianDist(float mean, float sigma = 0, float limit = 3, string parameter = null) {
        return (byte)Mathf.Clamp(IGaussianDist(mean, sigma, limit, parameter), 0, 255);
    }
    int IGaussianDist(float mean, string parameter) => IGaussianDist(mean, 0, 3, parameter);
    int IGaussianDist(float mean, float sigma, string parameter) => IGaussianDist(mean, sigma, 3, parameter);
    int IGaussianDist(float mean, float sigma = 0, float limit = 3, string parameter = null) {
        return Mathf.RoundToInt(GaussianDist(mean, sigma, limit, parameter));
    }
    float GaussianDist(float mean, string parameter) => GaussianDist(mean, 0, 3, parameter);
    float GaussianDist(float mean, float sigma, string parameter) => GaussianDist(mean, sigma, 3, parameter);
    float GaussianDist(float mean, float sigma = 0, float limit = 3, string parameter = null) {
        if(sigma <= 0) sigma = mean / 3;
        if(!String.IsNullOrEmpty(parameter)) Random128.Rng.Start(parameter);
        float x1, x2;
        do {
            do {
                x1 = Random128.Rng.Range(-1f, 1f);
                x2 = Random128.Rng.Range(-1f, 1f);
                x2 = x1 * x1 + x2 * x2;
            } while(x2 >= 1.0 || x2 == 0);
            x2 = mean + x1 * Mathf.Sqrt(-2 * Mathf.Log(x2) / x2) * sigma;
        } while(Mathf.Abs(x2 - mean) >= sigma * limit);
        return x2;
    }
    string GetColorName(Color color) {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        string modifier = "";
        string baseName = Mathf.RoundToInt(h * 12) switch {
            1 => "orange",
            2 => "yellow",
            3 => "lime",
            4 => "green",
            5 => "mint",
            6 => "cyan",
            7 => "azure",
            8 => "blue",
            9 => "purple",
            10 => "magenta",
            11 => "pink",
            _ => "red"
        };
        if(v < 0.05f) return "black";
        if(s < 0.125f) return v switch {
            < 0.3f => "dark gray",
            > 0.9f => "white",
            > 0.7f => "light gray",
            _ => "gray"
        };
        if(s < 0.5f && v < 0.6f) modifier = "dull-";
        else if(v < 0.3f) modifier = "dark-";
        else if(v < 0.5f) modifier = "muted-";
        else if(s < 0.65f) modifier = v < 0.75f ? "pale-" : "light-";
        else if(2 * v + s > 2.95f) modifier = "vivid-";
        else if(2 * v + s > 2.8f) modifier = "bright-";
        return modifier + baseName;
    }
    public void SetFlameColor(Color32 color, string texture, bool ship) {
        if(Array.IndexOf(fuelNames, texture) >= 0) {
            if(ship) {
                thrustersColor = color;
                thrustersFuel = texture;
            } else {
                jetpackColor = color;
                jetpackFuel = texture;
            }
        }
    }
    /*string GetStylizedName(Color color) {
        Dictionary<string, string[]> StylizedColorNames = new() {
            { "deep-blue", ["midnight-hued", "abyssal-tinted"] },
            { "deep-red", ["crimson-depth", "deep-fiery"] },
            { "deep-green", ["chlorocrypt", "deep-viridian"] },
            { "deep-purple", ["void-orchid"] },
            { "deep-pink", ["infra-blush"] },
            { "deep-orange", [] },
            { "deep-yellow", [] },
            { "deep-lime", [] },
            { "deep-mint", [] },
            { "deep-cyan", [] },
            { "deep-azure", [] },
            { "deep-magenta", [] },
            { "dark-blue", ["void-blue", "cosmic-sapphire"] },
            { "dark-red", ["dark-ember", "deep-scarlet"] },
            { "dark-gray", ["ashen", "graphite-toned", "shadow-gray"] },
            { "dark-green", ["shadow-emerald", "chloroshade"] },
            { "dark-purple", ["void-violet", "amethyst-shaded"] },
            //{ "dark-brown", ["char-dusk", "dim-umber"] },
            { "dark-pink", [] },
            { "dark-orange", [] },
            { "dark-yellow", [] },
            { "dark-lime", [] },
            { "dark-mint", [] },
            { "dark-cyan", [] },
            { "dark-azure", [] },
            { "dark-magenta", [] },
            { "light-blue", ["ion-frost", "glacial-tinted"] },
            { "light-red", ["soft-ember", "shimmering-rose"] },
            { "light-gray", ["dust-veil", "moondust", "shimmering-dust", "moonlight"] },
            { "light-green", ["hazy-mint", "mosslight", "blooming"] },
            { "light-purple", ["lilac-glow", "ether-bloom"] },
            { "light-yellow", ["gold-veil", "soft-gold"] },
            { "light-pink", [] },
            { "light-orange", [] },
            { "light-lime", [] },
            { "light-mint", [] },
            { "light-cyan", [] },
            { "light-azure", [] },
            { "light-magenta", [] },
            { "vivid-blue", ["neon-tide", "electric-azure"] },
            { "vivid-red", ["flarecore", "fiery-plasma"] },
            { "vivid-purple", ["pulsing-violet", "ultraviolet", "ion-orchid"] },
            { "vivid-green", ["surging-verdant"] },
            { "vivid-pink", ["silken-stellar", "rose-pulse"] },
            { "vivid-orange", [] },
            { "vivid-yellow", [] },
            { "vivid-lime", [] },
            { "vivid-mint", [] },
            { "vivid-cyan", [] },
            { "vivid-azure", [] },
            { "vivid-magenta", [] },
            { "pale-yellow", ["misty-lemon", "pale-gold", "dawn-light"] },
            { "pale-blue", ["ice-glow", "frost-tinted", "pale-sky"] },
            { "pale-red", [] },
            { "pale-green", [] },
            { "pale-purple", [] },
            { "pale-pink", [] },
            { "pale-orange", [] },
            { "pale-lime", [] },
            { "pale-mint", [] },
            { "pale-cyan", [] },
            { "pale-azure", [] },
            { "pale-magenta", [] },
            { "dull-yellow", [] },
            { "dull-blue", [] },
            { "dull-red", [] },
            { "dull-green", [] },
            { "dull-purple", [] },
            { "dull-pink", [] },
            { "dull-orange", [] },
            { "dull-lime", [] },
            { "dull-mint", [] },
            { "dull-cyan", [] },
            { "dull-azure", [] },
            { "dull-magenta", [] },
            { "black", ["obsidian-tinted", "null-shaded", "void-toned"] },
            { "gray", ["moon-veil", "lunar-toned"] },
            { "white", ["pearl-bright", "luminous", "clear"] },
            //{ "brown", ["dim-bronze", "umber"] },
            { "pink", ["roselight", "pale-red"] },
            { "red", ["fiery-toned", "crimson", "ruby"] },
            { "blue", ["celestial-blue", "skyshard"] },
            { "green", ["emerald", "chloroglow", "verdant-tinted"] },
            { "yellow", ["aurora-gold", "lightburst", "banana-tinted"] },
            { "purple", ["orchid", "plasma-tinted", "nebula-hue"] },
            { "orange", [] },
            { "lime", [] },
            { "mint", [] },
            { "cyan", [] },
            { "azure", [] },
            { "magenta", [] }
        };
        string modifiedName = GetColorName(color);
        if(StylizedColorNames.TryGetValue(modifiedName, out string[] variants) && variants.Length > 0) {
            int result = Random128.Rng.Range(-1, variants.Length);
            if(result >= 0) return variants[result];
        }
        return modifiedName;
    }*/
}


//URGENT:
//TODO:
//      add mysterious artefacts (one / 10 systems) that increase warpPower towards 1
//      add signals to rare scatter
//      Gneiss banjo quest
//          correct scatter function (sample consistency)
//  fix duplicate fuel tool on SolarSystem reentry
//  allow space spawn in SolarSystem (and other mods)
//  fix spawning sometime weird offset?
//MAYBE?:
//  correct textures, big planets gets higher res?
//  fix floating point shaking
//TO TEST:
//DONE:
//  bigger referenceframevolume (entryradius)
//  galactic key not found
//  make shiplog entries sprites
//  correct shiplog mapMode
//  lower map speed close range
//  star colors too light?
//  star light too colored
//  make better global outline sprite
//  add heightmaps
//  increase xorshift near-seeded variability
//  increase sprites resolution
//  increase heightmap amplitude
//  correct player spawn
//  add proxies
//  add textures
//  talk about atmosphere only if big enough
//  add water level sometimes
//  add compat mods
//  change systems names to their coords
//  regen systems in place
//  add respawn in system config
//  allow autopilot on stars
//  reforged Random to allow parametric sampling
//  fix space travel
//  remove shiplog interstellar mode!
//  reduce map furthest zoom
//  no zoom when selecting on map
//  add map indicator for visited systems (with config)
//  rework rare scatter builder (array)
//  toggle on "show button prompts"
//  add fuel management
//  refuel suit from ship
//  add coords to star names
//  add random Color utility
//  add fuel vol to systems (and remove fuel tanks)
//  add clouds (rain only if)
//  fix fuel tool taking (spawn it and hide prompt)
//  fix fuel tool particles when exit map
//  LoadCurrentSystem not called when quit -> resuming???
//  hardcode array of (x, y, Vector3) for heightmaps
//  Load only when Resume button, not death!
//  fix LODcubesphere1 (distant HM)
//  fuel tool brakes when taken in a non NMS system
//  save autopilot state between systems
//  fix travel lines visible outside map
//  fix autopilot spawning too fast
//  make black on cloud textures transparent
//  ArgumentException: An item with the same key has already been added. Key: VAMBOK.NOMAISKY_0.3.1_0--12--5-2_HIUNERTH ; Error : There must be one and only one centerOfSolarSystem! Found [2]
//  fix fuel tool prompt at spawn at flight console
//  save game on exit
//  message on warp loading (to warn for freeze)
//  save player/ship flames state between systems & games
//  add system change wait screen waiting messages trivia (if never travel or warp, proba of "you can also...")
