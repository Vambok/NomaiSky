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
    // START:
    public static NomaiSky Instance;
    INewHorizons NewHorizons;
    bool hasDLC;
    // GALACTIC MAP:
    const int mapRadius = 5;
    readonly float warpPower = 1f; // min 0.2 to max 1
    (int x, int y, int z) currentCenter = (0, 0, 0);
    readonly Dictionary<(int x, int y, int z), (string name, string starName, float radius, Color32 color, Vector3 offset)> galacticMap = [];
    readonly Dictionary<(int x, int y, int z), (string name, float radius, Color32 color, string starName)> otherModsSystems = [];
    readonly List<(int x, int y, int z)> visited = [(0, 0, 0)];
    public readonly int systemRadius = 200000;
    public readonly int entryRadius = 100000; /*system max radius = 91065.5 ; because:
    star radius: 1600 - 6400
    planet orbits: (every 8500) 10500 - 87000
    planet radius: 50 - 950 (w relief: 28.5 - 1073.5)
    moon orbits: (every 500) 1320 - 3820 (w relief, max: 4065.5)
    moon radius: 10 - 190 (w relief: .5 - 245.5) */
    // WARPING:
    Vector3 entryPosition;
    Quaternion entryRotation;
    // GENERATION:
    readonly int galaxyName = 0;
    public const string version = "0.3.1";//Changing this will cause a rebuild of all previously visited systems, increment only when changing the procedural generation!

    // START:
    public void Awake() {
        Instance = this;
        // You won't be able to access OWML's mod helper in Awake.
        // So you probably don't want to do anything here.
        // Use Start() instead.
    }
    public void Start() {
        // Starting here, you'll have access to OWML's mod helper.
        ModHelper.Console.WriteLine("Nomai's Sky is loaded!", MessageType.Success);
        // Get the New Horizons API and load configs
        NewHorizons = ModHelper.Interaction.TryGetModApi<INewHorizons>("xen.NewHorizons");
        NewHorizons.LoadConfigs(this);
        // Harmony
        new Harmony("Vambok.NomaiSky").PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        // Initializations
        hasDLC = EntitlementsManager.IsDlcOwned() == EntitlementsManager.AsyncOwnershipStatus.Owned;
        otherModsSystems[(0, 0, 0)] = ("SolarSystem", 2000, new Color32(255, 125, 9, 255), "Sun");
        foreach(IModBehaviour mod in ModHelper.Interaction.GetMods()) {
            switch(mod.ModHelper.Manifest.UniqueName) {//+00(story) +0+(Jams) 00+(Owlks) -0+(crossover) -00(systems) -0-(reals) 00-(fun) +0-(fun story)
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
            default:
            //case "manifest_uniqueName":
            //    otherModsSystems[(x, y, z)] = ("starSystem", "size", new Color32("tint.r", "tint.g", "tint.b", "tint.a"), "name");
                break;
            }
        }
        ModHelper.Events.Unity.RunWhen(PlayerData.IsLoaded, InitSolarSystem);
        NewHorizons.GetStarSystemLoadedEvent().AddListener(SpawnIntoSystem);
        // Example of accessing game code.
        //OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen); // We start on title screen
        LoadManager.OnStartSceneLoad += OnStartSceneLoad;
        LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
    }
    public void OnStartSceneLoad(OWScene originalScene, OWScene loadScene) {
        if(loadScene == OWScene.SolarSystem) {
            ModHelper.Console.WriteLine("Start OW scene load!", MessageType.Success);//TEST
            LoadCurrentSystem();
        }
    }
    public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene) {
        ModHelper.Console.WriteLine("Scene loaded!", MessageType.Success);//TEST
        /*string toto = Heightmaps.CreateHeightmap(Path.Combine(ModHelper.Manifest.ModFolderPath, "planets/heightmap")); //TEST
        ModHelper.Console.WriteLine("HM done! "+toto, MessageType.Success); //TEST*/
    }
    void Update() {
        /*if(Locator.GetCenterOfTheUniverse() != null) {
            // WARPING:
            Vector3 currentSystemCubePosition = Locator.GetCenterOfTheUniverse().GetOffsetPosition() - galacticMap[currentCenter].offset;
            if(currentSystemCubePosition.magnitude > systemRadius) {
                SpaceExploration(currentSystemCubePosition);
            }
        }*/
    }

    // GALACTIC MAP:
    (string, string, float, Color32, Vector3) GetPlaceholder(int x, int y, int z) {
        Random128.Initialize(galaxyName, x, y, z);
        if(otherModsSystems.ContainsKey((x, y, z))) {
            (string name, float radius, Color32 color, string starName) = otherModsSystems[(x, y, z)];
            Random128.Rng.Start("offset");
            return (name, starName, radius, color, new Vector3(Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius)));
        } else {
            StarInitializator(out string starName, out float radius, out byte colorR, out byte colorG, out byte colorB);
            //Random128.Rng.Start("offset");//Offset consistency not needed to be cool
            return ("NomaiSky_" + galaxyName + "-" + x + "-" + y + "-" + z, starName, radius, new Color32(colorR, colorG, colorB, 255), new Vector3(Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius)));
        }
    }
    void InitSolarSystem() {//here currentCenter should be 0,0,0
        ModHelper.Console.WriteLine("Init system!", MessageType.Success);//TEST
        for(int x = -mapRadius;x <= mapRadius;x++) {
            for(int y = -mapRadius / 2;y <= mapRadius / 2;y++) {
                for(int z = -mapRadius;z <= mapRadius;z++) {
                    galacticMap.Add((x + currentCenter.x, y + currentCenter.y, z + currentCenter.z), GetPlaceholder(x + currentCenter.x, y + currentCenter.y, z + currentCenter.z));
                }
            }
        }
        NewHorizons.CreatePlanet("{\"name\": \"Bel-O-Kan of " + galacticMap[currentCenter].starName + "\",\"$schema\": \"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json\",\"starSystem\": \"" + galacticMap[currentCenter].name + "\",\"Base\": {\"groundSize\": 50, \"surfaceSize\": 50, \"surfaceGravity\": 0},\"Orbit\": {\"showOrbitLine\": false,\"semiMajorAxis\": " + (systemRadius * (1 + 2.83f * mapRadius * warpPower) / 3.5f).ToString(CultureInfo.InvariantCulture) + ",\"primaryBody\": \"" + galacticMap[currentCenter].starName + "\"},\"ShipLog\": {\"mapMode\": {\"remove\": true}}}", Instance);
    }
    void LoadCurrentSystem() {
        ModHelper.Console.WriteLine("Load system!", MessageType.Success);//TEST
        (int, int, int) newSystem = (0, 0, 0);
        switch(ModHelper.Config.GetSettingsValue<string>("Star system to start the game in")) {
        case "Last visited system":
            ShipLogFactSave getCurrentCenter = PlayerData.GetShipLogFactSave("NomaiSky_currentCenter");
            if(getCurrentCenter != null) {
                string[] ts_currentCenter = getCurrentCenter.id.Substring(1, getCurrentCenter.id.Length - 2).Split(',');
                newSystem = (Int32.Parse(ts_currentCenter[0]), Int32.Parse(ts_currentCenter[1]), Int32.Parse(ts_currentCenter[2]));
            }
            break;
        case "Random system":
            newSystem = (UnityEngine.Random.Range(-4 * mapRadius, 4 * mapRadius + 1), UnityEngine.Random.Range(-2 * mapRadius, 2 * mapRadius + 1), UnityEngine.Random.Range(-4 * mapRadius, 4 * mapRadius + 1));
            break;
        case "Distant system":
            newSystem = (UnityEngine.Random.Range(4 * mapRadius + 1, 40 * mapRadius + 1) * (int)Mathf.Sign(UnityEngine.Random.Range(-1, 1)), UnityEngine.Random.Range(2 * mapRadius + 1, 20 * mapRadius + 1) * (int)Mathf.Sign(UnityEngine.Random.Range(-1, 1)), UnityEngine.Random.Range(4 * mapRadius + 1, 40 * mapRadius + 1) * (int)Mathf.Sign(UnityEngine.Random.Range(-1, 1)));
            break;
        default:
            PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_currentCenter"] = new ShipLogFactSave("(0,0,0)");
            break;
        }
        if(newSystem != (0, 0, 0)) {
            ModHelper.Console.WriteLine("Warp!", MessageType.Success);//TEST
            WarpToSystem(newSystem);
        }
    }
    void DictUpdate(int dx, int dy, int dz) {
        if(dx != 0) {
            int xEnd = currentCenter.x + (dx > 0 ? mapRadius : -mapRadius - dx - 1);
            for(int x = currentCenter.x + (dx > 0 ? mapRadius - dx + 1 : -mapRadius);x <= xEnd;x++)
                for(int y = currentCenter.y - mapRadius / 2;y <= currentCenter.y + mapRadius / 2;y++)
                    for(int z = currentCenter.z - mapRadius;z <= currentCenter.z + mapRadius;z++)
                        if(!galacticMap.ContainsKey((x, y, z)))
                            galacticMap.Add((x, y, z), GetPlaceholder(x, y, z));
        }
        if(dy != 0) {
            int yEnd = currentCenter.y + (dy > 0 ? mapRadius / 2 : -mapRadius / 2 - dy - 1);
            int xMax = currentCenter.x + mapRadius - Mathf.Max(dx, 0);
            for(int y = currentCenter.y + (dy > 0 ? mapRadius / 2 - dy + 1 : -mapRadius / 2);y <= yEnd;y++)
                for(int x = currentCenter.x - mapRadius - Mathf.Min(dx, 0);x <= xMax;x++)
                    for(int z = currentCenter.z - mapRadius;z <= currentCenter.z + mapRadius;z++)
                        if(!galacticMap.ContainsKey((x, y, z)))
                            galacticMap.Add((x, y, z), GetPlaceholder(x, y, z));
        }
        if(dz != 0) {
            int zEnd = currentCenter.z + (dz > 0 ? mapRadius : -mapRadius - dz - 1);
            int xMax = currentCenter.x + mapRadius - Mathf.Max(dx, 0);
            int yMax = currentCenter.y + mapRadius / 2 - Mathf.Max(dy, 0);
            for(int z = currentCenter.z + (dz > 0 ? mapRadius - dz + 1 : -mapRadius);z <= zEnd;z++)
                for(int x = currentCenter.x - mapRadius - Mathf.Min(dx, 0);x <= xMax;x++)
                    for(int y = currentCenter.y - mapRadius / 2 - Mathf.Min(dy, 0);y <= yMax;y++)
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

        Vector3 currentOffset = galacticMap[currentCenter].offset - Locator.GetCenterOfTheUniverse().GetStaticReferenceFrame().gameObject.transform.position;
        int mapWarpPower = (int)(mapRadius * warpPower);
        string starName;
        float radius;
        Color32 color;
        Vector3 offset;
        for(int x = -mapWarpPower;x <= mapWarpPower;x++) {
            for(int y = -mapWarpPower / 2;y <= mapWarpPower / 2;y++) {
                for(int z = -mapWarpPower;z <= mapWarpPower;z++) {
                    if((x, y, z) != (0, 0, 0)) {
                        if(galacticMap.ContainsKey((currentCenter.x + x, currentCenter.y + y, currentCenter.z + z))) {
                            (_, starName, radius, color, offset) = galacticMap[(currentCenter.x + x, currentCenter.y + y, currentCenter.z + z)];
                            GameObject star = Instantiate(s);
                            star.name = starName;
                            star.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", 2 * (Color)color);
                            star.transform.position = 2 * systemRadius * new Vector3(x, y, z) + offset - currentOffset;
                            star.transform.localScale = radius * Vector3.one;
                            star.AddComponent<MVBGalacticMap>().Initializator((currentCenter.x + x, currentCenter.y + y, currentCenter.z + z), starName);
                            MakeProxy(starName, star, radius, color);
                        } else {
                            ModHelper.Console.WriteLine("Galactic key not found: " + (currentCenter.x + x, currentCenter.y + y, currentCenter.z + z).ToString(), MessageType.Error);
                        }
                    }
                }
            }
        }
        Destroy(s);
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
            ModHelper.Console.WriteLine($"Exception thrown when generating proxy for [{name}]:\n{ex}", MessageType.Error);
            return;
        }
        proxy.SetActive(true);
    }

    // WARPING:
    public void MapExploration(ReferenceFrame targetReferenceFrame, ScreenPrompt prompt) {
        MVBGalacticMap data = targetReferenceFrame.GetOWRigidBody().GetComponent<MVBGalacticMap>();
        if(data != null) {
            prompt.SetText("Warp to " + data.mapName);
            prompt.SetVisibility(true);
            if(OWInput.IsNewlyPressed(InputLibrary.markEntryOnHUD)) {
                WarpToSystem(data.coords);
            }
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
                    entryRotation = Locator.GetShipTransform().rotation;
                    WarpToSystem(actualCube);
                }
            }
        }
    }
    void WarpToSystem((int, int, int) newCoords) {
        bool waitForWrite = false;
        (int x, int y, int z) = currentCenter;
        currentCenter = newCoords;
        if(!visited.Contains(newCoords)) {
            DictUpdate(currentCenter.x - x, currentCenter.y - y, currentCenter.z - z);
            if(!otherModsSystems.ContainsKey(newCoords)) {
                Random128.Initialize(galaxyName, currentCenter.x, currentCenter.y, currentCenter.z);
                StarInitializator(out string starName, out float radius, out byte colorR, out byte colorG, out byte colorB);
                string systemPath = Path.Combine(ModHelper.Manifest.ModFolderPath, "systems", "NomaiSky_" + galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z + ".json");
                waitForWrite = true;
                if(File.Exists(systemPath)) {
                    string[] split = File.ReadAllText(systemPath).Split(["\"version\":\""], 2, StringSplitOptions.None);
                    if(split.Length > 1 && split[1].Split(['"'], 2)[0] == version) waitForWrite = false;
                }
                if(waitForWrite) {
                    try {
                        File.WriteAllText(systemPath, SystemCreator(starName, radius, colorR, colorG, colorB));
                    } catch(ArgumentException e) {
                        ModHelper.Console.WriteLine($"Cannot write system file! {e.Message}", MessageType.Error);
                    } finally {
                        NewHorizons.ClearSystem(galacticMap[newCoords].name);
                        NewHorizons.LoadConfigs(Instance);
                        PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_currentCenter"] = new ShipLogFactSave(newCoords.ToString());
                        NewHorizons.ChangeCurrentStarSystem(galacticMap[newCoords].name);
                    }
                }
            }
            NewHorizons.CreatePlanet("{\"name\": \"Bel-O-Kan of " + galacticMap[currentCenter].starName + "\",\"$schema\": \"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json\",\"starSystem\": \"" + galacticMap[currentCenter].name + "\",\"Base\": {\"groundSize\": 50, \"surfaceSize\": 50, \"surfaceGravity\": 0},\"Orbit\": {\"showOrbitLine\": false,\"semiMajorAxis\": " + (systemRadius * (1 + 2.83f * mapRadius * warpPower) / 3.5f).ToString(CultureInfo.InvariantCulture) + ",\"primaryBody\": \"" + galacticMap[currentCenter].starName + "\"},\"ShipLog\": {\"mapMode\": {\"remove\": true}}}", Instance);
            visited.Add(newCoords);
        }
        if(!waitForWrite) {
            PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_currentCenter"] = new ShipLogFactSave(newCoords.ToString());
            NewHorizons.ChangeCurrentStarSystem(galacticMap[newCoords].name);
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
                } else {
                    shipSpawnPoint.position = new Vector3(0, 10000, -34100);
                    shipSpawnPoint.eulerAngles = new Vector3(16.334f, 0, 0);
                }
            }
        }
        entryPosition = Vector3.zero;
        ModHelper.Events.Unity.FireInNUpdates(() => {
            if(!otherModsSystems.ContainsKey(currentCenter)) {
                PlayerSpawner playerSpawner = Locator.GetPlayerBody().GetComponent<PlayerSpawner>();
                playerSpawner.DebugWarp(playerSpawner.GetSpawnPoint(SpawnLocation.Ship));
                SuitPickupVolume mySuit = Locator.GetShipBody().GetComponentInChildren<SuitPickupVolume>();
                mySuit.OnPressInteract(mySuit._interactVolume.GetInteractionAt(mySuit._pickupSuitCommandIndex).inputCommand);
                Locator.GetShipBody().GetComponentInChildren<ShipCockpitController>().OnPressInteract();
            }
            if(Locator.GetShipBody().GetComponent<WarpController>() == null) {
                Locator.GetShipBody().gameObject.AddComponent<WarpController>().currentOffset = galacticMap[currentCenter].offset;
            } else {
                Locator.GetShipBody().gameObject.GetComponent<WarpController>().currentOffset = galacticMap[currentCenter].offset;
            }
            GenerateNeighborhood();
            ModHelper.Console.WriteLine("Loaded into " + galacticMap[currentCenter].starName + " (" + systemName + ")! Current galaxy: " + galaxyName, MessageType.Success);
        }, 2);
    }

    // GENERATION:
    void StarInitializator(out string starName, out float radius, out byte colorR, out byte colorG, out byte colorB) {
        starName = StarNameGen("StarName");
        radius = GaussianDist(4000, 800, "StarRadius");
        colorR = BGaussianDist(150, "StarColor");
        colorG = BGaussianDist(150);
        colorB = BGaussianDist(150);
    }
    string SystemCreator(string starName, float radius, byte colorR, byte colorG, byte colorB) {
        string path = Path.Combine(ModHelper.Manifest.ModFolderPath, "planets", galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z);
        Directory.CreateDirectory(path + "/" + starName);
        File.WriteAllText(Path.Combine(path, starName, starName + ".json"), StarCreator(starName, radius, colorR, colorG, colorB));
        int nbPlanets = Mathf.CeilToInt(GaussianDist(4, 2, 2, "NbPlanets"));
        int fuelPlanet = Random128.Rng.Range(0, nbPlanets);
        int fuelMoon = -1;
        int[] orbits = new int[nbPlanets];
        int allowedOrbits = 76500 - nbPlanets * 8500;
        Random128.Rng.Start("PlanetOrbits");
        for(int i = 0;i < nbPlanets;i++) {
            orbits[i] = Random128.Rng.Range(0, allowedOrbits);
        }
        Array.Sort(orbits);
        for(int i = 0;i < nbPlanets;i++) {
            int nbMoons = Random128.Rng.Range(0, 63, i + "NbMoons");
            if(nbMoons < 32) {
                nbMoons = 0;
            } else if(nbMoons < 48) {
                nbMoons = 1;
            } else if(nbMoons < 56) {
                nbMoons = 2;
            } else if(nbMoons < 60) {
                nbMoons = 3;
            } else if(nbMoons < 62){
                nbMoons = 4;
            } else {
                nbMoons = 5;
            }
            if(i == fuelPlanet) {
                fuelMoon = Random128.Rng.Range(-1, nbMoons);
            }
            string planetName = PlanetNameGen(i + "Name");
            Directory.CreateDirectory(Path.Combine(path, planetName));
            File.WriteAllText(Path.Combine(path, planetName, planetName + ".json"), PlanetCreator(starName, planetName, orbits[i] + 8500 * i + 10500, (i == fuelPlanet && fuelMoon < 0)));
            if(nbMoons > 0) {
                int[] moonOrbits = new int[nbMoons];
                int allowedMoonOrbits = 3000 - (nbMoons * 500);
                Random128.Rng.Start(i + "MoonOrbits");
                for(int j = 0;j < nbMoons;j++) {
                    moonOrbits[j] = Random128.Rng.Range(0, allowedMoonOrbits);
                }
                Array.Sort(moonOrbits);
                for(int j = 0;j < nbMoons;j++) {
                    string moonName = PlanetNameGen(i + "MoonName" + j, true);
                    Directory.CreateDirectory(Path.Combine(path, planetName, moonName.Replace(' ', '_')));
                    File.WriteAllText(Path.Combine(path, planetName, moonName.Replace(' ', '_'), moonName.Replace(' ', '_') + ".json"), PlanetCreator(starName, moonName, moonOrbits[j] + 500 * j + 1320, j == fuelMoon, planetName));
                }
            }
        }
        return "{\"extras\":{\"mod_config\":{\"version\":\"" + version + "\"}},\"$schema\":\"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/star_system_schema.json\",\"respawnHere\":true}";
    }
    string StarCreator(string starName, float radius, byte colorR, byte colorG, byte colorB) {
        string relativePath = "planets/" + galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z + "/" + starName + "/";
        SpriteGenerator("star", relativePath + "map_star.png", colorR, colorG, colorB);
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
                        "r": {{colorR}},
                        "g": {{colorG}},
                        "b": {{colorB}},
                        "a": 255
                    },
                    "lightTint": {
                        "r": {{(colorR + 510) / 3}},
                        "g": {{(colorG + 510) / 3}},
                        "b": {{(colorB + 510) / 3}},
                        "a": 255
                    },
                    "solarLuminosity": {{Random128.Rng.Range(0.3f, 2f, "StarLuminosity").ToString(CultureInfo.InvariantCulture)}},
                    "stellarDeathType": "none"
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
                }
            }
            """;
        return finalJson;
    }
    string PlanetCreator(string starName, string planetName, int orbit, bool fuel, string orbiting = "") {
        string relativePath = "planets/" + galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z + "/" + (orbiting != "" ? orbiting + "/" : "") + planetName.Replace(' ', '_') + "/";
        string characteristics = "A ";
        List<char> vowels = ['a', 'e', 'i', 'o', 'u'];
        string finalJson = "{\n\"name\": \"" + planetName + "\",\n" +
            "\"$schema\": \"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json\",\n";
        /*if(starName == "SolarSystem") {
            finalJson += "\"starSystem\": \"SolarSystem\",\n";
            starName = "Sun";
        } else {*/
        finalJson += "\"starSystem\": \"NomaiSky_" + galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z + "\",\n";
        //}
        finalJson += "\"canShowOnTitle\": false,\n" +
            "\"Base\": {\n";
        float radius = (orbiting == "") ? GaussianDist(500, 150, planetName + "Radius") : GaussianDist(100, 30, planetName + "Radius");
        //finalJson += "    \"groundSize\": " + (radius - 1).ToString(CultureInfo.InvariantCulture) + ",\n";
        finalJson += "    \"surfaceSize\": " + radius.ToString(CultureInfo.InvariantCulture) + ",\n";
        characteristics += (radius * (orbiting == "" ? 1 : 5)) switch {
            > 900 => "enormous ",
            > 800 => "huge ",
            > 650 => "big ",
            < 100 => "minuscule ",
            < 200 => "tiny ",
            < 350 => "small ",
            _ => ""
        };
        float temp = GaussianDist(radius * 12 / 500, planetName + "Gravity");
        finalJson += "    \"surfaceGravity\": " + temp.ToString(CultureInfo.InvariantCulture) + ",\n";
        characteristics += (temp * 125 / radius) switch {
            > 5.6f => "ultradense ",
            > 5 => "dense ",
            > 4 => "compact ",
            < 0.4f => "ethereal ",
            < 1 => "sparse ",
            < 2 => "light ",
            _ => ""
        };
        finalJson += "    \"gravityFallOff\": \"inverseSquared\"\n" +
            "},\n" +
            "\"HeightMap\": {\n";
        byte colorR = BGaussianDist(130, 50, 2.5f, planetName + "Color");
        byte colorG = BGaussianDist(130, 50, 2.5f);
        byte colorB = BGaussianDist(130, 50, 2.5f);
        SpriteGenerator("planet", relativePath + "map_planet.png", colorR, colorG, colorB);
        Heightmaps.CreateHeightmap(Path.Combine(ModHelper.Manifest.ModFolderPath, relativePath), radius, new Color32(colorR, colorG, colorB, 255));
        //ModHelper.Console.WriteLine(planetName+"'s HM done! " + stemp); //TEST
        temp = Mathf.Sqrt(radius);
        finalJson += "    \"heightMap\": \"" + relativePath + "heightmap.png\",\n" +
            "    \"minHeight\": " + (radius - 3 * temp).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "    \"maxHeight\": " + (radius + 4 * temp).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "    \"textureMap\": \"" + relativePath + "texture.png\",\n";
        /*finalJson += "    \"emissionColor\": {\n" +
            "        \"r\": " + colorR + ",\n" +
            "        \"g\": " + colorG + ",\n" +
            "        \"b\": " + colorB + "\n";
        finalJson += "    },\n";//*/
        string stemp = GetColorName(new Color32(colorR, colorG, colorB, 255)) + " ";
        temp = Mathf.Max(GaussianDist(0, 0.2f, 5, planetName + "Smoothness"), 0);
        finalJson += "    \"smoothness\": " + temp.ToString(CultureInfo.InvariantCulture) + "\n";
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
            finalJson += "    \"isMoon\": true,\n" +
                "    \"primaryBody\": \"" + orbiting + "\",\n";
            characteristics += "moon";
        } else {
            finalJson += "    \"primaryBody\": \"" + starName + "\",\n";
            characteristics += "planet";
        }
        finalJson += "    \"semiMajorAxis\": " + orbit + ",\n" +
            "    \"inclination\": " + GaussianDist(0, 10, 9, planetName + "Inclination").ToString(CultureInfo.InvariantCulture) + ",\n" +
            "    \"longitudeOfAscendingNode\": " + Random128.Rng.Range(0, 360, planetName + "LOAN") + ",\n" +
            "    \"trueAnomaly\": " + Random128.Rng.Range(0, 360, planetName + "Anomaly") + ",\n" +
            "    \"isTidallyLocked\": " + (Random128.Rng.Range(0, 4, planetName + "Lock") == 0).ToString().ToLower() + "\n" +
            "},\n";
        float ringRadius = 0;
        if(Random128.Rng.Range(0, 10, planetName + "HasRings") == 0) {
            finalJson += "\"Rings\": [{\n";
            float ringInnerRadius = GaussianDist(radius * 2, radius / 5, planetName + "RingInner");
            finalJson += "    \"innerRadius\": " + ringInnerRadius.ToString(CultureInfo.InvariantCulture) + ",\n";
            float ringSpread = (radius * 3 - ringInnerRadius) / 2;
            ringRadius = GaussianDist(radius * 3 - ringSpread, ringSpread / 3, planetName + "RingOuter");
            finalJson += "    \"outerRadius\": " + ringRadius.ToString(CultureInfo.InvariantCulture) + ",\n" +
                "    \"texture\": \"" + relativePath + "rings.png\",\n" +
                "    \"fluidType\": \"sand\"\n" +
                "}],\n";
            colorR = BGaussianDist(130, 50, 2.5f, planetName + "RingsColor");
            colorG = BGaussianDist(130, 50, 2.5f);
            colorB = BGaussianDist(130, 50, 2.5f);
            SpriteGenerator("rings", relativePath, colorR, colorG, colorB, BGaussianDist(200, 50, 4, planetName + "Rings"), [(byte)Mathf.CeilToInt(128 * (1 - ringInnerRadius / ringRadius))]);
            characteristics += ", with " + GetColorName(new Color32(colorR, colorG, colorB, 255)) + " rings";
            stemp = " and ";
        } else stemp = ", with ";
        bool hasWaterOxygenTrees = Random128.Rng.Range(0, 5, planetName + "Water") == 0;
        if(hasWaterOxygenTrees) {
            finalJson += "\"Water\": {\n" +
                "    \"size\": " + GaussianDist(radius, 0.4f * Mathf.Sqrt(radius), 5, planetName + "WaterLevel").ToString(CultureInfo.InvariantCulture) + ",\n" +
                "    \"tint\": {\n" +
                "        \"r\": " + BGaussianDist(100, 50, 2, planetName + "WaterColor") + ",\n" +
                "        \"g\": " + BGaussianDist(100, 50, 2) + ",\n" +
                "        \"b\": " + BGaussianDist(255, 50) + ",\n" +
                "        \"a\": " + BGaussianDist(105, 50) + "\n" +
                "    }\n" +
                "},\n";
        }
        float atmosphereSize = GaussianDist(radius * 6 / 5, radius / 10, 4, planetName + "AtmSize");
        finalJson += "\"Atmosphere\": {\n" +
            "    \"size\": " + atmosphereSize.ToString(CultureInfo.InvariantCulture) + ",\n" +
            "    \"atmosphereTint\": {\n";
        colorR = BGaussianDist(200, 50, 4, planetName + "AtmColor");
        colorG = BGaussianDist(200, 50, 4);
        colorB = BGaussianDist(200, 50, 4);
        byte colorA = BGaussianDist(255, 50, 5);
        SpriteGenerator("atmosphere", relativePath + "map_atmosphere.png", colorR, colorG, colorB, colorA);
        finalJson += "        \"r\": " + colorR + ",\n" +
            "        \"g\": " + colorG + ",\n" +
            "        \"b\": " + colorB + ",\n" +
            "        \"a\": " + colorA + "\n";
        finalJson += "    },\n";
        if(atmosphereSize > radius * 6 / 5) {
            characteristics += stemp;
            stemp = GetColorName(new Color32(colorR, colorG, colorB, 255));
            characteristics += (vowels.Contains(stemp[0]) ? "an " : "a ") + stemp + " atmosphere";
            if(Random128.Rng.Range(0, 4, planetName + "Fog") == 0) {
                finalJson += "    \"fogTint\": {\n" +
                    "        \"r\": " + BGaussianDist(130, 50, 2.5f, planetName + "FogColor") + ",\n" +
                    "        \"g\": " + BGaussianDist(130, 50, 2.5f) + ",\n" +
                    "        \"b\": " + BGaussianDist(130, 50, 2.5f) + ",\n" +
                    "        \"a\": " + BGaussianDist(255, 50, 5) + "\n" +
                    "    },\n" +
                    "    \"fogSize\": " + Random128.Rng.Range(radius, atmosphereSize, planetName + "FogSize").ToString(CultureInfo.InvariantCulture) + ",\n" +
                    "    \"fogDensity\": " + Random128.Rng.Range(0f, 1f, planetName + "FogDens").ToString(CultureInfo.InvariantCulture) + ",\n";
            }
        }
        if(hasWaterOxygenTrees) {
            characteristics += ". There's water on the planet";
            hasWaterOxygenTrees = Random128.Rng.Range(0, 4, planetName + "Oxygen") != 0;
            stemp = ", and t";
        } else {
            hasWaterOxygenTrees = Random128.Rng.RandomBool(planetName + "Oxygen");
            stemp = ". T";
        }
        finalJson += "    \"hasOxygen\": " + hasWaterOxygenTrees.ToString().ToLower() + ",\n";
        if(hasWaterOxygenTrees) {
            characteristics += stemp + "here seems to be oxygen";
            hasWaterOxygenTrees = Random128.Rng.RandomBool(planetName + "Trees");
        }
        finalJson += "    \"hasTrees\": " + hasWaterOxygenTrees.ToString().ToLower() + ",\n" +
            "    \"hasRain\": " + (Random128.Rng.Range(0, 6, planetName + "Rain") == 0).ToString().ToLower() + "\n" +
            "},\n";
        if(hasWaterOxygenTrees || fuel) {
            finalJson += "\"Props\": {\n" +
                "    \"scatter\": [\n";
            if(fuel) {
                finalJson += "        {\"path\": \"" + (hasDLC ? "RingWorld_Body/Sector_RingInterior/Sector_Zone2/Structures_Zone2/EyeTempleRuins_Zone2/Interactables_EyeTempleRuins_Zone2/Prefab_IP_FuelTorch (1)\"" : "CaveTwin_Body/Sector_CaveTwin/Sector_NorthHemisphere/Sector_NorthSurface/Sector_Lakebed/Interactables_Lakebed/Prefab_HEA_FuelTank\", \"rotation\": {\"x\": 30, \"y\": 0, \"z\": 270}") + ", \"count\": 1}" + (hasWaterOxygenTrees ? "," : "") + "\n";
            }
            if(hasWaterOxygenTrees) {
                finalJson += "        {\"path\": \"" + (hasDLC ? "DreamWorld_Body/Sector_DreamWorld/Sector_Underground/IslandsRoot/IslandPivot_B/Island_B/Props_Island_B/Tree_DW_L (3)" : "QuantumMoon_Body/Sector_QuantumMoon/State_TH/Interactables_THState/Crater_1/Crater_1_QRedwood/QRedwood (2)/Prefab_TH_Redwood") + "\", \"count\": " + IGaussianDist(radius * radius / 1250, planetName + "NbBTrees") + ", \"scale\": " + GaussianDist(1, 0.2f, planetName + "SizeBTrees").ToString(CultureInfo.InvariantCulture) + "},\n" +
                    "        {\"path\": \"" + (hasDLC ? "DreamWorld_Body/Sector_DreamWorld/Sector_DreamZone_4/Props_DreamZone_4_Upper/Tree_DW_S_B" : "QuantumMoon_Body/Sector_QuantumMoon/State_TH/Interactables_THState/Crater_3/Crater_3_Sapling/QSapling/Tree_TH_Sapling") + "\", \"count\": " + IGaussianDist(radius * radius / 1250, planetName + "NbLTrees") + ", \"scale\": " + GaussianDist(1, 0.2f, planetName + "SizeLTrees").ToString(CultureInfo.InvariantCulture) + "}\n";
                characteristics += " and trees";
            }
            finalJson += "    ]\n" +
                "},\n";
        }
        characteristics += ".";
        finalJson += "\"ShipLog\": {\n" +
            "    \"spriteFolder\": \"" + relativePath + "sprites\",\n" +
            "    \"xmlFile\": \"" + relativePath + "shiplogs.xml\",\n" +
            "    \"mapMode\": {\n" +
            "        \"outlineSprite\": \"outline.png\",\n" +
            "        \"revealedSprite\": \"" + relativePath + "map_atmosphere.png\",\n" +
            "        \"scale\": " + (atmosphereSize / 500f).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "        \"offset\": " + (atmosphereSize / 500f).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "        \"details\": [\n" +
            "            {\"revealedSprite\": \"" + relativePath + "map_planet.png\",\n" +
            "            \"scale\": {\"x\": " + (radius / 500f).ToString(CultureInfo.InvariantCulture) + ",\"y\": " + (radius / 500f).ToString(CultureInfo.InvariantCulture) + "},\n";
        if(ringRadius > 0) {
            finalJson += "            \"invisibleWhenHidden\": true},\n" +
                "            {\"revealedSprite\": \"" + relativePath + "map_rings.png\",\n" +
                "            \"scale\": {\"x\": " + (ringRadius / 500f).ToString(CultureInfo.InvariantCulture) + ",\"y\": " + (ringRadius / 500f).ToString(CultureInfo.InvariantCulture) + "},\n";
        }
        finalJson += "            \"invisibleWhenHidden\": true}\n" +
            "        ]\n" +
            "    }\n" +
            "},\n" +
            "\"Volumes\": {\n" +
            "    \"revealVolumes\": [\n" +
            "        {\"radius\": " + (1.2f * (ringRadius > 0 ? ringRadius : radius)).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "        \"reveals\": [\"VAMBOK.NOMAISKY_" + version + "_" + galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z + "_" + planetName.Replace(' ', '_').ToUpper() + "\"]}\n" +
            "    ]\n" +
            "},\n" +
            "\"MapMarker\": {\"enabled\": true}\n}";
        AssetsMaker(relativePath, planetName, characteristics);
        return finalJson;
    }
    void SpriteGenerator(string mode, string path) { SpriteGenerator(mode, path, 0, 0, 0); }
    void SpriteGenerator(string mode, string path, byte colorR, byte colorG, byte colorB, byte colorA = 255, byte[] ringData = null) {
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
                        data[i * width * 4 + j * 4 + 3] = colorA;
                        data[i * width * 4 + j * 4 + 2] = (byte)(colorB / (0.9f + radial));
                        data[i * width * 4 + j * 4 + 1] = (byte)(colorG / (0.9f + radial));
                        data[i * width * 4 + j * 4] = (byte)(colorR / (0.9f + radial));
                    } else if(radial < 1.1f) {
                        data[i * width * 4 + j * 4 + 3] = colorA;
                        data[i * width * 4 + j * 4 + 2] = (byte)((colorB + 255) / 2);
                        data[i * width * 4 + j * 4 + 1] = (byte)((colorG + 255) / 2);
                        data[i * width * 4 + j * 4] = (byte)((colorR + 255) / 2);
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
                        data[i * width * 4 + j * 4 + 3] = colorA;
                        data[i * width * 4 + j * 4 + 2] = colorB;
                        data[i * width * 4 + j * 4 + 1] = colorG;
                        data[i * width * 4 + j * 4] = colorR;
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
                        data[i * width * 4 + j * 4 + 3] = colorA;
                        data[i * width * 4 + j * 4 + 2] = colorB;
                        data[i * width * 4 + j * 4 + 1] = colorG;
                        data[i * width * 4 + j * 4] = colorR;
                    } else if(radial < 1) {
                        data[i * width * 4 + j * 4 + 3] = colorA;
                        data[i * width * 4 + j * 4 + 2] = (byte)((colorB + 255) / 2);
                        data[i * width * 4 + j * 4 + 1] = (byte)((colorG + 255) / 2);
                        data[i * width * 4 + j * 4] = (byte)((colorR + 255) / 2);
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
                    data[i * width * 4 + j * 4 + 2] = colorB;
                    data[i * width * 4 + j * 4 + 1] = colorG;
                    data[i * width * 4 + j * 4] = colorR;
                }
            }
            break;
        case "rings":
            width = 1;
            height = 1024;
            byte[] ringDataM = new byte[129];
            //ringDataM.Clear();
            data = new byte[4 * width * height];
            for(int i = 0;i < height;i++) {//invert if inner top
                if(Random128.Rng.Range(0, Mathf.RoundToInt(height / 5)) == 0) {//to get ~ 5 changes (tweakable)
                    colorA = (byte)Random128.Rng.Range(0, 256);
                }
                if(i % Mathf.CeilToInt((float)height / ringData[0]) == 0) {
                    ModHelper.Console.WriteLine(128 - ringData[0] + i / Mathf.CeilToInt((float)height / ringData[0]) + " i:" + i + " rd0:" + ringData[0]);
                    ringDataM[128 - ringData[0] + i / Mathf.CeilToInt((float)height / ringData[0])] = colorA;//ringdata[0]=1-69
                }
                data[i * 4] = colorR;
                data[i * 4 + 1] = colorG;
                data[i * 4 + 2] = colorB;
                data[i * 4 + 3] = colorA;
            }
            ringDataM[128] = 0;
            SpriteGenerator("map_rings", path + "map_rings.png", colorR, colorG, colorB, 255, ringDataM);
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
            "<ExploreFact>\n<ID>VAMBOK.NOMAISKY_" + version + "_" + galaxyName + "-" + currentCenter.x + "-" + currentCenter.y + "-" + currentCenter.z + "_" + planetName.Replace(' ', '_').ToUpper() + "</ID>\n" +
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
    byte BGaussianDist(float mean, string parameter) => BGaussianDist(mean, 0, 3, parameter);
    byte BGaussianDist(float mean, float sigma, string parameter) => BGaussianDist(mean, sigma, 3, parameter);
    byte BGaussianDist(float mean, float sigma = 0, float limit = 3, string parameter = "") {
        return (byte)Mathf.Clamp(IGaussianDist(mean, sigma, limit, parameter), 0, 255);
    }
    int IGaussianDist(float mean, string parameter) => IGaussianDist(mean, 0, 3, parameter);
    int IGaussianDist(float mean, float sigma, string parameter) => IGaussianDist(mean, sigma, 3, parameter);
    int IGaussianDist(float mean, float sigma = 0, float limit = 3, string parameter = "") {
        return Mathf.RoundToInt(GaussianDist(mean, sigma, limit, parameter));
    }
    float GaussianDist(float mean, string parameter) => GaussianDist(mean, 0, 3, parameter);
    float GaussianDist(float mean, float sigma, string parameter) => GaussianDist(mean, sigma, 3, parameter);
    float GaussianDist(float mean, float sigma = 0, float limit = 3, string parameter = "") {
        if(sigma <= 0) sigma = mean / 3;
        if(parameter != "") Random128.Rng.Start(parameter);
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
//  ArgumentException: An item with the same key has already been added. Key: VAMBOK.NOMAISKY_0.3.1_0--12--5-2_HIUNERTH ; Error : There must be one and only one centerOfSolarSystem! Found [2]
//TODO:
//  add mysterious artefacts (one / 10 systems) that increase warpPower towards 1
//  warp loading black (not freeze)
//  add map indicator for visited systems
//MAYBE?:
//  add heightmaps mipmap1
//  correct textures, big planets gets higher res?
//  add random Color utility
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
//  change system names to its coords
//  regen systems in place
//  add respawn in system config
//  allow autopilot on stars
//  reforged Random to allow parametric sampling
//  fix space travel
//  remove shiplog interstellar mode!
//  reduce map furthest zoom
//  no zoom when selecting on map
