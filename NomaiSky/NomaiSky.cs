using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using System.Diagnostics;

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
    readonly int systemRadius = 200000;
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
            switch(mod.ModHelper.Manifest.UniqueName) {//TODO more
            case "GameWyrm.HearthsNeighbor":
                otherModsSystems[(1, 0, 0)] = ("GameWyrm.HearthsNeighbor", 3000, new Color32(150, 150, 255, 255), "Neighbor Sun");
                break;
            default:
                //otherModsSystems[(x, y, z)] = ("starSystem", "size", new Color32("tint.r", "tint.g", "tint.b", "tint.a"), "name");
                break;
            }
        }
        ModHelper.Events.Unity.RunWhen(PlayerData.IsLoaded, LoadCurrentSystem);
        NewHorizons.GetStarSystemLoadedEvent().AddListener(SpawnIntoSystem);
        // Example of accessing game code.
        OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen); // We start on title screen
        LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
    }
    public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene) {
        //if(newScene != OWScene.SolarSystem) return;
        /*string toto = Heightmaps.CreateHeightmap(Path.Combine(ModHelper.Manifest.ModFolderPath, "planets/heightmap")); //TEST
        ModHelper.Console.WriteLine("HM done! "+toto, MessageType.Success); //TEST*/
    }
    void Update() {
        if(Locator.GetCenterOfTheUniverse() != null) {
            // WARPING:
            Vector3 currentSystemCubePosition = Locator.GetCenterOfTheUniverse().GetOffsetPosition() - galacticMap[currentCenter].offset;
            if(currentSystemCubePosition.magnitude > systemRadius) {
                SpaceExploration(currentSystemCubePosition);
            }
        }
    }

    // GALACTIC MAP:
    (string, string, float, Color32, Vector3) GetPlaceholder(int x, int y, int z) {
        Random128.Initialize(galaxyName, x, y, z);
        if(otherModsSystems.ContainsKey((x, y, z))) {
            (string name, float radius, Color32 color, string starName) = otherModsSystems[(x, y, z)];
            return (name, starName, radius, color, new Vector3(Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius)));
        } else {
            string starName = StarNameGen();
            float radius = GaussianDist(4000, 800);
            byte colorR = (byte)Mathf.Min(IGaussianDist(150), 255);
            byte colorG = (byte)Mathf.Min(IGaussianDist(150), 255);
            byte colorB = (byte)Mathf.Min(IGaussianDist(150), 255);
            return ("NomaiSky_" + starName, starName, radius, new Color32(colorR, colorG, colorB, 255), new Vector3(Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius)));
        }
    }
    void LoadCurrentSystem() {
        ShipLogFactSave getCurrentCenter = null;//PlayerData.GetShipLogFactSave("NomaiSky_currentCenter"); //TEST
        if(getCurrentCenter != null) {
            string s_currentCenter = getCurrentCenter.id;
            s_currentCenter = s_currentCenter.Substring(1, s_currentCenter.Length - 2);
            string[] ts_currentCenter = s_currentCenter.Split(',');
            currentCenter = (Int32.Parse(ts_currentCenter[0]), Int32.Parse(ts_currentCenter[1]), Int32.Parse(ts_currentCenter[2]));
        }
        for(int x = -mapRadius;x <= mapRadius;x++) {
            for(int y = -mapRadius / 2;y <= mapRadius / 2;y++) {
                for(int z = -mapRadius;z <= mapRadius;z++) {
                    galacticMap.Add((x + currentCenter.x, y + currentCenter.y, z + currentCenter.z), GetPlaceholder(x + currentCenter.x, y + currentCenter.y, z + currentCenter.z));
                }
            }
        }
        NewHorizons.CreatePlanet("{\"name\": \"Bel-O-Kan of " + galacticMap[currentCenter].starName + "\",\"$schema\": \"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json\",\"starSystem\": \"" + galacticMap[currentCenter].name + "\",\"Base\": {\"groundSize\": 50, \"surfaceSize\": 50, \"surfaceGravity\": 0},\"Orbit\": {\"showOrbitLine\": false,\"semiMajorAxis\": " + ((1 + 2.83f * mapRadius * warpPower) * systemRadius).ToString(CultureInfo.InvariantCulture) + ",\"primaryBody\": \"" + galacticMap[currentCenter].starName + "\"},\"ShipLog\": {\"mapMode\": {\"remove\": true}}}", Instance);
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
        //mat.SetColor("_EmissionColor", new Color(3, 3, 0));
        //mat.SetColor("_Color", Color.black); // Optional
        s.GetComponent<MeshRenderer>().material = mat;
        s.transform.position = new(100000, 100000, 0);
        s.transform.localScale = 2000 * Vector3.one;
        OWRigidbody OWRs = s.AddComponent<OWRigidbody>();
        s.AddComponent<MVBGalacticMap>().Initializator((1, 1, 0), galacticMap[(1, 1, 0)].starName);
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
                            star.transform.localScale *= radius / 2000;
                            star.GetComponent<MVBGalacticMap>().Initializator((currentCenter.x + x, currentCenter.y + y, currentCenter.z + z), starName);
                            MakeProxy(starName, star, radius, color);
                        } else {
                            ModHelper.Console.WriteLine("Galactic key not found: " + (currentCenter.x + x, currentCenter.y + y, currentCenter.z + z).ToString(), MessageType.Error);
                        }
                        //GameObject gameObject = Instantiate<GameObject>(this._proxies[i].proxyPrefab);
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
    void SpaceExploration(Vector3 currentSystemCubePosition) {
        (int x, int y, int z) actualCube = (Mathf.RoundToInt(currentSystemCubePosition.x / (2 * systemRadius)), Mathf.RoundToInt(currentSystemCubePosition.y / (2 * systemRadius)), Mathf.RoundToInt(currentSystemCubePosition.z / (2 * systemRadius)));
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
                string starName = StarNameGen();
                string systemPath = Path.Combine(ModHelper.Manifest.ModFolderPath, "systems", "NomaiSky_" + starName + ".json");
                if(!File.Exists(systemPath)) {
                    waitForWrite = true;
                    try {
                        using StreamWriter outputFile = new(systemPath);
                        outputFile.Write(SystemCreator(starName));
                    } catch(ArgumentException e) {
                        ModHelper.Console.WriteLine($"Cannot write system file! {e.Message}", MessageType.Error);
                    } finally {
                        NewHorizons.LoadConfigs(Instance);
                        PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_currentCenter"] = new ShipLogFactSave(newCoords.ToString());
                        NewHorizons.ChangeCurrentStarSystem(galacticMap[newCoords].name);
                    }
                }
            }
            NewHorizons.CreatePlanet("{\"name\": \"Bel-O-Kan of " + galacticMap[currentCenter].starName + "\",\"$schema\": \"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json\",\"starSystem\": \"" + galacticMap[currentCenter].name + "\",\"Base\": {\"groundSize\": 50, \"surfaceSize\": 50, \"surfaceGravity\": 0},\"Orbit\": {\"showOrbitLine\": false,\"semiMajorAxis\": " + ((1 + 2.83f * mapRadius * warpPower) * systemRadius).ToString(CultureInfo.InvariantCulture) + ",\"primaryBody\": \"" + galacticMap[currentCenter].starName + "\"},\"ShipLog\": {\"mapMode\": {\"remove\": true}}}", Instance);
            visited.Add(newCoords);
        }
        if(!waitForWrite) {
            PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_currentCenter"] = new ShipLogFactSave(newCoords.ToString());
            NewHorizons.ChangeCurrentStarSystem(galacticMap[newCoords].name);
        }
    }
    void SpawnIntoSystem(string systemName) {
        if(!otherModsSystems.ContainsKey(currentCenter)) {
            GameObject star = NewHorizons.GetPlanet(systemName.Substring(9));
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
                Locator.GetPlayerSuit().SuitUp();
                Locator.GetShipBody().GetComponentInChildren<ShipCockpitController>().OnPressInteract();
            }
            if(Locator.GetShipBody().gameObject.GetComponent<WarpController>() == null) {
                Locator.GetShipBody().gameObject.AddComponent<WarpController>();
            }
            GenerateNeighborhood();
            ModHelper.Console.WriteLine("Loaded into " + NewHorizons.GetCurrentStarSystem() + "! Current galaxy: " + galaxyName, MessageType.Success);
        }, 2);
    }

    // GENERATION:
    string SystemCreator(string systemName) {
        string path = Path.Combine(ModHelper.Manifest.ModFolderPath, "planets", systemName);
        Directory.CreateDirectory(path + "/" + systemName);
        using StreamWriter starOutputFile = new(Path.Combine(path, systemName, systemName + ".json"));
        starOutputFile.Write(StarCreator(systemName));
        int nbPlanets = Mathf.CeilToInt(GaussianDist(4, 2, 2));
        int fuelPlanet = Random128.Rng.Range(0, nbPlanets);
        int fuelMoon = -1;
        int[] orbits = new int[nbPlanets];
        int allowedOrbits = 76500 - nbPlanets * 8500;
        for(int i = 0;i < nbPlanets;i++) {
            orbits[i] = Random128.Rng.Range(0, allowedOrbits);
        }
        Array.Sort(orbits);
        for(int i = 0;i < nbPlanets;i++) {
            int nbMoons = Random128.Rng.Range(0, 63);
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
            string planetName = PlanetNameGen();
            Directory.CreateDirectory(Path.Combine(path, planetName));
            using StreamWriter planetOutputFile = new(Path.Combine(path, planetName, planetName + ".json"));
            planetOutputFile.Write(PlanetCreator(systemName, planetName, orbits[i] + 8500 * i + 10500, (i == fuelPlanet && fuelMoon < 0)));//, "", i == Mathf.FloorToInt(nbPlanets / 2f)));
            if(nbMoons > 0) {
                int[] moonOrbits = new int[nbMoons];
                int allowedMoonOrbits = 3000 - (nbMoons * 500);
                for(int j = 0;j < nbMoons;j++) {
                    moonOrbits[j] = Random128.Rng.Range(0, allowedOrbits);
                }
                Array.Sort(moonOrbits);
                for(int j = 0;j < nbMoons;j++) {
                    string moonName = PlanetNameGen(true);
                    Directory.CreateDirectory(Path.Combine(path, planetName, moonName.Replace(' ', '_')));
                    using StreamWriter outputMoonFile = new(Path.Combine(path, planetName, moonName.Replace(' ', '_'), moonName.Replace(' ', '_') + ".json"));
                    outputMoonFile.Write(PlanetCreator(systemName, moonName, moonOrbits[j] + 500 * j + 1320, j == fuelMoon, planetName));
                }
            }
        }
        return """{"$schema":"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/star_system_schema.json","respawnHere": true}""";
    }
    string StarCreator(string solarSystem) {
        string relativePath = "planets/" + solarSystem + "/" + solarSystem + "/";
        string finalJson = "{\n\"name\": \"" + solarSystem + "\",\n" +
            "\"$schema\": \"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json\",\n" +
            "\"starSystem\": \"NomaiSky_" + solarSystem + "\",\n" +
            "\"canShowOnTitle\": false,\n" +
            "\"Base\": {\n";
        float radius = GaussianDist(4000, 800);
        byte colorR = (byte)Mathf.Min(IGaussianDist(150), 255);
        byte colorG = (byte)Mathf.Min(IGaussianDist(150), 255);
        byte colorB = (byte)Mathf.Min(IGaussianDist(150), 255);
        finalJson += "    \"surfaceSize\": " + radius.ToString(CultureInfo.InvariantCulture) + ",\n" +
            "    \"surfaceGravity\": " + GaussianDist(radius * 3 / 500).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "    \"gravityFallOff\": \"inverseSquared\",\n" +
            "    \"centerOfSolarSystem\": true\n" +
            "},\n" +
            "\"Orbit\": {\n" +
            "    \"showOrbitLine\": false,\n" +
            "    \"isStatic\": true\n" +
            "},\n" +
            "\"Star\": {\n" +
            "    \"size\": " + radius.ToString(CultureInfo.InvariantCulture) + ",\n" +
            "    \"tint\": {\n";
        SpriteGenerator("star", relativePath + "map_star.png", colorR, colorG, colorB);
        finalJson += "        \"r\": " + colorR + ",\n" +
            "        \"g\": " + colorG + ",\n" +
            "        \"b\": " + colorB + ",\n" +
            "        \"a\": 255\n" +
            "    },\n" +
            "    \"lightTint\": {\n" +
            "        \"r\": " + (colorR + 510) / 3 + ",\n" +
            "        \"g\": " + (colorG + 510) / 3 + ",\n" +
            "        \"b\": " + (colorB + 510) / 3 + ",\n" +
            "        \"a\": 255\n" +
            "    },\n" +
            "    \"solarLuminosity\": " + Random128.Rng.Range(0.3f, 2f).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "    \"stellarDeathType\": \"none\"\n" +
            "},\n" +
            "\"Spawn\": {\n" +
            "    \"shipSpawnPoints\": [\n" +
            "        {\"isDefault\": true,\n" +
            "        \"position\": {\"x\": 0, \"y\": 10000, \"z\": -34100},\n" +
            "        \"rotation\": {\"x\": 16.334, \"y\": 0, \"z\": 0}}\n" +
            "    ]\n" +
            "},\n" +
            "\"ShipLog\": {\n" +
            "    \"mapMode\": {\n" +
            "        \"revealedSprite\": \"" + relativePath + "map_star.png\",\n" +
            "        \"scale\": " + (radius / 500f).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "        \"selectable\": false\n" +
            "    }\n" +
            "}\n}";
        return finalJson;
    }
    string PlanetCreator(string solarSystem, string planetName, int orbit, bool fuel, string orbiting = "") {
        string relativePath = "planets/" + solarSystem + "/" + (orbiting != "" ? orbiting + "/" : "") + planetName.Replace(' ', '_') + "/";
        string characteristics = "A ";
        List<char> vowels = ['a', 'e', 'i', 'o', 'u'];
        string finalJson = "{\n\"name\": \"" + planetName + "\",\n" +
            "\"$schema\": \"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json\",\n";
        if(solarSystem == "SolarSystem") {
            finalJson += "\"starSystem\": \"SolarSystem\",\n";
            solarSystem = "Sun";
        } else {
            finalJson += "\"starSystem\": \"NomaiSky_" + solarSystem + "\",\n";
        }
        finalJson += "\"canShowOnTitle\": false,\n" +
            "\"Base\": {\n";
        float radius = (orbiting == "") ? GaussianDist(500, 150) : GaussianDist(100, 30);
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
        float temp = GaussianDist(radius * 12 / 500);
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
            "\"ProcGen\": {\n" +
            "    \"color\": {\n";
        byte colorR = (byte)IGaussianDist(130, 50, 2.5f);
        byte colorG = (byte)IGaussianDist(130, 50, 2.5f);
        byte colorB = (byte)IGaussianDist(130, 50, 2.5f);
        SpriteGenerator("planet", relativePath + "map_planet.png", colorR, colorG, colorB);
        finalJson += "        \"r\": " + colorR + ",\n" +
            "        \"g\": " + colorG + ",\n" +
            "        \"b\": " + colorB + "\n";
        string stemp = GetColorName(new Color32(colorR, colorG, colorB, 255)) + " ";
        finalJson += "    },\n";
        temp = Mathf.Max(GaussianDist(0, 0.2f, 5), 0);
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
            characteristics += "moon, with ";
        } else {
            finalJson += "    \"primaryBody\": \"" + solarSystem + "\",\n";
            characteristics += "planet, with ";
        }
        finalJson += "    \"semiMajorAxis\": " + orbit + ",\n" +
            "    \"inclination\": " + GaussianDist(0, 10, 9).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "    \"longitudeOfAscendingNode\": " + Random128.Rng.Range(0, 360) + ",\n" +
            "    \"trueAnomaly\": " + Random128.Rng.Range(0, 360) + ",\n" +
            "    \"isTidallyLocked\": " + (Random128.Rng.Range(0, 4) == 0).ToString().ToLower() + "\n" +
            "},\n";
        float ringRadius = 0;
        if(Random128.Rng.Range(0, 10) == 0) {
            finalJson += "\"Rings\": [{\n";
            float ringInnerRadius = GaussianDist(radius * 2, radius / 5);
            finalJson += "    \"innerRadius\": " + ringInnerRadius.ToString(CultureInfo.InvariantCulture) + ",\n";
            float ringSpread = (radius * 3 - ringInnerRadius) / 2;
            ringRadius = GaussianDist(radius * 3 - ringSpread, ringSpread / 3);
            finalJson += "    \"outerRadius\": " + ringRadius.ToString(CultureInfo.InvariantCulture) + ",\n" +
                "    \"texture\": \"" + relativePath + "rings.png\",\n" +
                "    \"fluidType\": \"sand\"\n" +
                "}],\n";
            colorR = (byte)IGaussianDist(130, 50, 2.5f);
            colorG = (byte)IGaussianDist(130, 50, 2.5f);
            colorB = (byte)IGaussianDist(130, 50, 2.5f);
            SpriteGenerator("rings", relativePath, colorR, colorG, colorB, (byte)Mathf.Min(IGaussianDist(200, 50, 4), 255), [(byte)Mathf.CeilToInt(128 * (1 - ringInnerRadius / ringRadius))]);
            characteristics += GetColorName(new Color32(colorR, colorG, colorB, 255)) + " rings and ";
        }
        finalJson += "\"Atmosphere\": {\n";
        float atmosphereSize = GaussianDist(radius * 6 / 5, radius / 20, 4);
        finalJson += "    \"size\": " + atmosphereSize.ToString(CultureInfo.InvariantCulture) + ",\n" +
            "    \"atmosphereTint\": {\n";
        colorR = (byte)Mathf.Min(IGaussianDist(200, 50, 4), 255);
        colorG = (byte)Mathf.Min(IGaussianDist(200, 50, 4), 255);
        colorB = (byte)Mathf.Min(IGaussianDist(200, 50, 4), 255);
        byte colorA = (byte)Mathf.Min(IGaussianDist(255, 50, 5), 255);
        SpriteGenerator("atmosphere", relativePath + "map_atmosphere.png", colorR, colorG, colorB, colorA);
        finalJson += "        \"r\": " + colorR + ",\n" +
            "        \"g\": " + colorG + ",\n" +
            "        \"b\": " + colorB + ",\n" +
            "        \"a\": " + colorA + "\n";
        stemp = GetColorName(new Color32(colorR, colorG, colorB, 255));
        characteristics += (vowels.Contains(stemp[0]) ? "an " : "a ") + stemp + " atmosphere";
        finalJson += "    },\n";
        if(Random128.Rng.Range(0, 4) == 0) {
            finalJson += "    \"fogTint\": {\n" +
                "        \"r\": " + IGaussianDist(130, 50, 2.5f) + ",\n" +
                "        \"g\": " + IGaussianDist(130, 50, 2.5f) + ",\n" +
                "        \"b\": " + IGaussianDist(130, 50, 2.5f) + ",\n" +
                "        \"a\": " + Mathf.Min(IGaussianDist(255, 50, 5), 255) + "\n" +
                "    },\n" +
                "    \"fogSize\": " + Random128.Rng.Range(radius, atmosphereSize).ToString(CultureInfo.InvariantCulture) + ",\n" +
                "    \"fogDensity\": " + Random128.Rng.Range(0f, 1f).ToString(CultureInfo.InvariantCulture) + ",\n";
        }
        bool hasTrees = Random128.Rng.RandomBool();
        finalJson += "    \"hasOxygen\": " + hasTrees.ToString().ToLower() + ",\n";
        if(hasTrees) {
            characteristics += ". There seems to be oxygen";
            hasTrees = Random128.Rng.RandomBool();
        }
        finalJson += "    \"hasTrees\": " + hasTrees.ToString().ToLower() + ",\n" +
            "    \"hasRain\": " + (Random128.Rng.Range(0, 6) == 0).ToString().ToLower() + "\n" +
            "},\n" +
            "\"HeightMap\": {\n";
        Heightmaps.CreateHeightmap(Path.Combine(ModHelper.Manifest.ModFolderPath, relativePath, "heightmap.png"), radius);
        //ModHelper.Console.WriteLine(planetName+"'s HM done! " + stemp); //TEST
        finalJson += "    \"heightMap\": \"" + relativePath + "heightmap.png\",\n";
        temp = Mathf.Sqrt(radius);
        finalJson += "    \"minHeight\": " + (radius - 3 * temp).ToString(CultureInfo.InvariantCulture) + ",\n" +
            "    \"maxHeight\": " + (radius + 4 * temp).ToString(CultureInfo.InvariantCulture) + "\n" +
            "},\n";
        if(hasTrees || fuel) {
            finalJson += "\"Props\": {\n" +
                "    \"scatter\": [\n";
            if(fuel) {
                finalJson += "{\"path\": \"" + (hasDLC ? "RingWorld_Body/Sector_RingInterior/Sector_Zone2/Structures_Zone2/EyeTempleRuins_Zone2/Interactables_EyeTempleRuins_Zone2/Prefab_IP_FuelTorch (1)\"" : "CaveTwin_Body/Sector_CaveTwin/Sector_NorthHemisphere/Sector_NorthSurface/Sector_Lakebed/Interactables_Lakebed/Prefab_HEA_FuelTank\", \"rotation\": {\"x\": 30, \"y\": 0, \"z\": 270}") + ", \"count\": 1}" + (hasTrees ? "," : "") + "\n";
            }
            if(hasTrees) {
                finalJson += "        {\"path\": \"" + (hasDLC ? "DreamWorld_Body/Sector_DreamWorld/Sector_Underground/IslandsRoot/IslandPivot_B/Island_B/Props_Island_B/Tree_DW_L (3)" : "QuantumMoon_Body/Sector_QuantumMoon/State_TH/Interactables_THState/Crater_1/Crater_1_QRedwood/QRedwood (2)/Prefab_TH_Redwood") + "\", \"count\": " + IGaussianDist(radius * radius / 1250) + ", \"scale\": " + GaussianDist(1, 0.2f).ToString(CultureInfo.InvariantCulture) + "},\n" +
                    "        {\"path\": \"" + (hasDLC ? "DreamWorld_Body/Sector_DreamWorld/Sector_DreamZone_4/Props_DreamZone_4_Upper/Tree_DW_S_B" : "QuantumMoon_Body/Sector_QuantumMoon/State_TH/Interactables_THState/Crater_3/Crater_3_Sapling/QSapling/Tree_TH_Sapling") + "\", \"count\": " + IGaussianDist(radius * radius / 1250) + ", \"scale\": " + GaussianDist(1, 0.2f).ToString(CultureInfo.InvariantCulture) + "}\n";
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
            "        \"outlineSprite\": \"planets/outline.png\",\n" +
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
            "        \"reveals\": [\"VAMBOK.NOMAISKY_" + solarSystem.ToUpper() + "_" + planetName.Replace(' ', '_').ToUpper() + "\"]}\n" +
            "    ]\n" +
            "},\n" +
            "\"MapMarker\": {\"enabled\": true}\n}";
        AssetsMaker(relativePath, solarSystem, planetName, characteristics);
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
            ringDataM.Clear();
            data = new byte[4 * width * height];
            for(int i = 0;i < height;i++) {//invert if inner top
                if(Random128.Rng.Range(0, Mathf.RoundToInt(height / 5)) == 0) {//to get ~ 5 changes (tweakable)
                    colorA = (byte)Random128.Rng.Range(0, 256);
                }
                if(i % Mathf.CeilToInt(height / ringData[0]) == 0) {
                    ringDataM[128 - ringData[0] + i / Mathf.CeilToInt(height / ringData[0])] = colorA;
                }
                data[i * 4] = colorR;
                data[i * 4 + 1] = colorG;
                data[i * 4 + 2] = colorB;
                data[i * 4 + 3] = colorA;
            }
            SpriteGenerator("map_rings", path + "map_rings.png", colorR, colorG, colorB, 255, ringDataM);
            path += "rings.png";
            break;
        case "fact":
            string[] pathChunks = path.Split('/', '\\');
            File.Copy(path + "map_planet.png", path + "sprites/ENTRY_" + pathChunks[pathChunks.Length - 2].ToUpper() + ".png");
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
    void AssetsMaker(string relativePath, string starName, string planetName, string characteristics = "A very mysterious planet.") {
        string path = Path.Combine(ModHelper.Manifest.ModFolderPath, relativePath);
        Directory.CreateDirectory(path + "/sprites");
        using StreamWriter outputFile = new(path + "/shiplogs.xml");
        outputFile.Write("<AstroObjectEntry xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/shiplog_schema.xsd\">\n" +
            "<ID>" + planetName.Replace(' ', '_').ToUpper() + "</ID>\n<Entry>\n<ID>ENTRY_" + planetName.Replace(' ', '_').ToUpper() + "</ID>\n<Name>" + planetName + "</Name>\n" +
            "<ExploreFact>\n<ID>VAMBOK.NOMAISKY_" + starName.ToUpper() + "_" + planetName.Replace(' ', '_').ToUpper() + "</ID>\n" +
            "<Text>" + characteristics + "</Text>\n" +
            "</ExploreFact>\n</Entry>\n</AstroObjectEntry>");
        SpriteGenerator("fact", relativePath);
    }
    // NAME GENERATION:
    string StarNameGen() {
        string[] nm1 = ["a", "e", "i", "o", "u", "", "", "", "", "", "", "", "", "", "", "", "", "", ""];
        string[] nm2 = ["b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "n", "p", "q", "r", "s", "t", "v", "w", "x", "y", "z", "br", "cr", "dr", "gr", "kr", "pr", "sr", "tr", "str", "vr", "zr", "bl", "cl", "fl", "gl", "kl", "pl", "sl", "vl", "zl", "ch", "sh", "ph", "th"];
        string[] nm3 = ["a", "e", "i", "o", "u", "a", "e", "i", "o", "u", "a", "e", "i", "o", "u", "ae", "ai", "ao", "au", "aa", "ea", "ei", "eo", "eu", "ee", "ia", "io", "iu", "oa", "oi", "oo", "ua", "ue"];
        string[] nm4 = ["b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "n", "p", "q", "r", "s", "t", "v", "w", "x", "y", "z", "br", "cr", "dr", "gr", "kr", "pr", "sr", "tr", "str", "vr", "zr", "bl", "cl", "fl", "hl", "gl", "kl", "ml", "nl", "pl", "sl", "tl", "vl", "zl", "ch", "sh", "ph", "th", "bd", "cd", "gd", "kd", "ld", "md", "nd", "pd", "rd", "sd", "zd", "bs", "cs", "ds", "gs", "ks", "ls", "ms", "ns", "ps", "rs", "ts", "ct", "gt", "lt", "nt", "st", "rt", "zt", "bb", "cc", "dd", "gg", "kk", "ll", "mm", "nn", "pp", "rr", "ss", "tt", "zz"];
        string[] nm5 = ["", "", "", "", "", "", "", "", "", "", "", "", "", "b", "c", "d", "f", "g", "h", "k", "l", "m", "n", "p", "r", "s", "t", "x", "y", "b", "c", "d", "f", "g", "h", "k", "l", "m", "n", "p", "r", "s", "t", "x", "y", "cs", "ks", "ls", "ms", "ns", "ps", "rs", "ts", "ys", "ct", "ft", "kt", "lt", "nt", "ph", "sh", "th"];
        string result;

        if(Random128.Rng.RandomBool()) {
            int rnd = Random128.Rng.Range(0, nm3.Length);
            result = nm1[Random128.Rng.Range(0, nm1.Length)] + nm2[Random128.Rng.Range(0, nm2.Length)] + nm3[rnd] + nm4[Random128.Rng.Range(0, nm4.Length)] + nm3[(rnd > 14) ? Random128.Rng.Range(0, 15) : Random128.Rng.Range(0, nm3.Length)] + nm5[Random128.Rng.Range(0, nm5.Length)];
        } else {
            result = nm1[Random128.Rng.Range(0, nm1.Length)] + nm2[Random128.Rng.Range(0, nm2.Length)] + nm3[Random128.Rng.Range(0, nm3.Length)] + nm5[Random128.Rng.Range(0, nm5.Length)];
        }
        return char.ToUpper(result[0]) + result.Substring(1);
    }
    string PlanetNameGen(bool isMoon = false) {
        string[] nm1 = ["b", "c", "ch", "d", "g", "h", "k", "l", "m", "n", "p", "r", "s", "t", "th", "v", "x", "y", "z", "", "", "", "", ""];
        string[] nm2 = ["a", "e", "i", "o", "u"];
        string[] nm3 = ["b", "bb", "br", "c", "cc", "ch", "cr", "d", "dr", "g", "gn", "gr", "l", "ll", "lr", "lm", "ln", "lv", "m", "n", "nd", "ng", "nk", "nn", "nr", "nv", "nz", "ph", "s", "str", "th", "tr", "v", "z"];
        string[] nm3b = ["b", "br", "c", "ch", "cr", "d", "dr", "g", "gn", "gr", "l", "ll", "m", "n", "ph", "s", "str", "th", "tr", "v", "z"];
        string[] nm4 = ["a", "e", "i", "o", "u", "a", "e", "i", "o", "u", "a", "e", "i", "o", "u", "ae", "ai", "ao", "au", "a", "ea", "ei", "eo", "eu", "e", "ua", "ue", "ui", "u", "ia", "ie", "iu", "io", "oa", "ou", "oi", "o"];
        string[] nm5 = ["turn", "ter", "nus", "rus", "tania", "hiri", "hines", "gawa", "nides", "carro", "rilia", "stea", "lia", "lea", "ria", "nov", "phus", "mia", "nerth", "wei", "ruta", "tov", "zuno", "vis", "lara", "nia", "liv", "tera", "gantu", "yama", "tune", "ter", "nus", "cury", "bos", "pra", "thea", "nope", "tis", "clite"];
        string[] nm6 = ["una", "ion", "iea", "iri", "illes", "ides", "agua", "olla", "inda", "eshan", "oria", "ilia", "erth", "arth", "orth", "oth", "illon", "ichi", "ov", "arvis", "ara", "ars", "yke", "yria", "onoe", "ippe", "osie", "one", "ore", "ade", "adus", "urn", "ypso", "ora", "iuq", "orix", "apus", "ion", "eon", "eron", "ao", "omia"];
        string[] nm7 = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "", "", "", "", "", "", "", "", "", "", "", "", "", ""];
        string result;

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
    int IGaussianDist(float mean, float sigma = 0, float limit = 3) {
        return Mathf.RoundToInt(GaussianDist(mean, sigma, limit));
    }
    float GaussianDist(float mean, float sigma = 0, float limit = 3) {
        if(sigma <= 0) {
            sigma = mean / 3;
        }
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
    string GetStylizedName(Color color) {
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
            if(result >= 0) {
                return variants[result];
            }
        }
        return modifiedName;
    }
}

public class NSProxy : ProxyPlanet {
    public GameObject planet;
    public override void Awake() {
        base.Awake();
        _mieCurveMaxVal = 0.1f;
        _mieCurve = AnimationCurve.EaseInOut(0.0011f, 1, 1, 0);
        // Start off
        _outOfRange = false;
        ToggleRendering(false);
    }
    public override void Initialize() {
        _realObjectTransform = planet.transform;
    }
    public override void Update() {
        if(planet == null || !planet.activeSelf) {
            _outOfRange = false;
            ToggleRendering(false);
            enabled = false;
            return;
        }
        base.Update();
    }
    public override void ToggleRendering(bool on) {
        base.ToggleRendering(on);
        foreach(Transform child in transform) child.gameObject.SetActive(on);
    }
}

public class WarpController : MonoBehaviour {
    ReferenceFrame targetReferenceFrame;
    ScreenPrompt travelPrompt;
    void Awake() {
        travelPrompt = new ScreenPrompt(InputLibrary.markEntryOnHUD, "Warp to star system");
        GlobalMessenger<ReferenceFrame>.AddListener("TargetReferenceFrame", OnTargetReferenceFrame);
        GlobalMessenger.AddListener("UntargetReferenceFrame", OnUntargetReferenceFrame);
        GlobalMessenger.AddListener("EnterMapView", OnEnterMapView);
        GlobalMessenger.AddListener("ExitMapView", OnExitMapView);
    }
    void OnDestroy() {
        GlobalMessenger<ReferenceFrame>.RemoveListener("TargetReferenceFrame", OnTargetReferenceFrame);
        GlobalMessenger.RemoveListener("UntargetReferenceFrame", OnUntargetReferenceFrame);
        GlobalMessenger.RemoveListener("EnterMapView", OnEnterMapView);
        GlobalMessenger.RemoveListener("ExitMapView", OnExitMapView);
    }
    void Update() {
        if(PlayerState.InMapView()) {
            if(targetReferenceFrame != null) {
                NomaiSky.Instance.MapExploration(targetReferenceFrame, travelPrompt);
            } else {
                travelPrompt.SetVisibility(false);
            }
        }
    }
    void OnTargetReferenceFrame(ReferenceFrame referenceFrame) { targetReferenceFrame = referenceFrame; }
    void OnUntargetReferenceFrame() { targetReferenceFrame = null; }
    void OnEnterMapView() { Locator.GetPromptManager().AddScreenPrompt(travelPrompt, PromptPosition.BottomCenter); }
    void OnExitMapView() { Locator.GetPromptManager().RemoveScreenPrompt(travelPrompt, PromptPosition.BottomCenter); }
}

public class MVBGalacticMap : MonoBehaviour {
    public (int x, int y, int z) coords;
    public string mapName;
    public void Initializator((int, int, int) initCoords, string initMapName) {
        coords = initCoords;
        mapName = initMapName;
    }
}

public class Random128 {
    private struct Xorshift32(uint seed) {
        private uint state = seed != 0 ? seed : 0xCAFEBABE;
        public uint NextUInt() {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
        public int Range(int minInclusive, int maxExclusive) { return (minInclusive == maxExclusive ? minInclusive : (int)(NextUInt() % (uint)(maxExclusive - minInclusive)) + minInclusive); }
        public float Range(float minInclusive, float maxExclusive) { return minInclusive + ((maxExclusive - minInclusive) * NextUInt() / (float)uint.MaxValue); }
        public bool RandomBool() { return (NextUInt() & 1) == 0; }
    }

    private readonly Xorshift32[] streams = new Xorshift32[4];
    private int cursor = 3;
    public static Random128 Rng { get; private set; }
    public static void Initialize(int a, int b, int c, int d) {
        Rng = new Random128(a, b, c, d);
        for(int i = (a + b + c + d) % 4 + 3;i > 0;i--) {
            Rng.NextStream().NextUInt();
        }
    }
    public Random128(int seed0, int seed1, int seed2, int seed3) {
        streams[0] = new Xorshift32((uint)seed0);
        streams[1] = new Xorshift32((uint)seed1 + streams[0].NextUInt());
        streams[2] = new Xorshift32((uint)seed2 + streams[1].NextUInt());
        streams[3] = new Xorshift32((uint)seed3 + streams[2].NextUInt());
    }
    private ref Xorshift32 NextStream() {
        cursor = (cursor + 1) % 4;
        return ref streams[cursor];
    }
    public int Range(int minInclusive, int maxExclusive) { return NextStream().Range(minInclusive, maxExclusive); }
    public float Range(float minInclusive, float maxExclusive) { return NextStream().Range(minInclusive, maxExclusive); }
    public bool RandomBool() { return NextStream().RandomBool(); }
    public int[] GeneratePermutations() {
        int[] perm = new int[512];
        int[] baseValues = [151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10, 23, 190, 6, 148, 247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33, 88, 237, 149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134, 139, 48, 27, 166, 77, 146, 158, 231, 83, 111, 229, 122, 60, 211, 133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54, 65, 25, 63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169, 200, 196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64, 52, 217, 226, 250, 124, 123, 5, 202, 38, 147, 118, 126, 255, 82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42, 223, 183, 170, 213, 119, 248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9, 129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112, 104, 218, 246, 97, 228, 251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241, 81, 51, 145, 235, 249, 14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157, 184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93, 222, 114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180];
        // Fisher-Yates shuffle
        for(int i = 255;i > 0;i--) {
            int j = NextStream().Range(0, i + 1);
            (baseValues[i], baseValues[j]) = (baseValues[j], baseValues[i]);
        }
        // Fill perm[0-255] and perm[256-511]
        for(int i = 0;i < 256;i++) {
            perm[i] = perm[i + 256] = baseValues[i];
        }
        return perm;
    }
}

static class Heightmaps {
    static float radius;
    static readonly int baseRes = 204;// = heightmap height = heightmap width / 2
    //static readonly Stopwatch timer = new();
    static (int, int, byte) SetVertex(int x, int y, int z, int hmHeight) {
        int hmWidth = hmHeight * 2;
        Vector3 v2 = (new Vector3(x, y, z) - Vector3.one * hmHeight / 8f).normalized;
        float x2 = v2.x * v2.x, y2 = v2.y * v2.y, z2 = v2.z * v2.z;
        Vector3 v = new(v2.x * Mathf.Sqrt(1f - y2 / 2f - z2 / 2f + y2 * z2 / 3f), v2.y * Mathf.Sqrt(1f - x2 / 2f - z2 / 2f + x2 * z2 / 3f), v2.z * Mathf.Sqrt(1f - x2 / 2f - y2 / 2f + x2 * y2 / 3f));
        float dist = Mathf.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        float longitude = Mathf.Rad2Deg * Mathf.Atan2(v.z, v.x);
        float latitude = Mathf.Rad2Deg * Mathf.Acos(-v.y / dist);
        float sampleX = hmWidth * longitude / 360f;
        if(sampleX > hmWidth) sampleX -= hmWidth;
        if(sampleX < 0) sampleX += hmWidth;
        return ((int)sampleX, (int)(hmHeight * latitude / 180f), HeightGenerator(v.normalized * radius));
    }

    public static void CreateHeightmap(string path, float planetRadius = 500) {
        radius = planetRadius / 10;//tweak this till frequences are great
        int hmWidth = baseRes * 2;
        int resolution = baseRes / 4;
        Texture2D tex = new(hmWidth, baseRes, TextureFormat.RGBA32, false);
        int tX, tY; byte hValue;
        byte[] data = new byte[baseRes * hmWidth];

        //timer.Reset(); //TEST
        //Random128.Initialize(1, 5463, 64875, 215);for(int jj = 0;jj < 10;jj++) { //TEST
        perm = Random128.Rng.GeneratePermutations();

        for(int x = 0;x <= resolution;x++) {
            for(int y = 0;y <= resolution;y++) {
                (tX, tY, hValue) = SetVertex(x, y, 0, baseRes);
                data[tX + tY * hmWidth] = hValue;
                (tX, tY, hValue) = SetVertex(x, y, resolution, baseRes);
                data[tX + tY * hmWidth] = hValue;
            }
        }
        for(int x = 1;x < resolution;x++) {
            for(int y = 0;y <= resolution;y++) {
                (tX, tY, hValue) = SetVertex(0, y, x, baseRes);
                data[tX + tY * hmWidth] = hValue;
                (tX, tY, hValue) = SetVertex(resolution, y, x, baseRes);
                data[tX + tY * hmWidth] = hValue;
            }
        }
        for(int x = 1;x < resolution;x++) {
            for(int y = 1;y < resolution;y++) {
                (tX, tY, hValue) = SetVertex(x, 0, y, baseRes);
                data[tX + tY * hmWidth] = hValue;
                (tX, tY, hValue) = SetVertex(x, resolution, y, baseRes);
                data[tX + tY * hmWidth] = hValue;
            }
        }
        /*resolution /= 2;
        for(int x = 0;x <= resolution;x++) {
            for(int y = 0;y <= resolution;y++) {
                (tX, tY, hValue) = SetVertex(x, y, 0, baseRes / 2);
                data[tX * 2 + tY * 2 * hmWidth] = hValue;
                (tX, tY, hValue) = SetVertex(x, y, resolution, baseRes / 2);
                data[tX * 2 + tY * 2 * hmWidth] = hValue;
            }
        }
        for(int x = 1;x < resolution;x++) {
            for(int y = 0;y <= resolution;y++) {
                (tX, tY, hValue) = SetVertex(0, y, x, baseRes / 2);
                data[tX * 2 + tY * 2 * hmWidth] = hValue;
                (tX, tY, hValue) = SetVertex(resolution, y, x, baseRes / 2);
                data[tX * 2 + tY * 2 * hmWidth] = hValue;
            }
        }
        for(int x = 1;x < resolution;x++) {
            for(int y = 1;y < resolution;y++) {
                (tX, tY, hValue) = SetVertex(x, 0, y, baseRes / 2);
                data[tX * 2 + tY * 2 * hmWidth] = hValue;
                (tX, tY, hValue) = SetVertex(x, resolution, y, baseRes / 2);
                data[tX * 2 + tY * 2 * hmWidth] = hValue;
            }
        }//*/
        byte[] finalData = new byte[baseRes * hmWidth * 4];
        for(int i = 0;i < baseRes * hmWidth;i++) {
            finalData[i * 4] = data[i];
            finalData[i * 4 + 1] = data[i];
            finalData[i * 4 + 2] = data[i];
            finalData[i * 4 + 3] = 255;
        }
        tex.SetPixelData(finalData, 0);
        tex.Apply();
        File.WriteAllBytes(path, ImageConversion.EncodeToPNG(tex));
        //File.WriteAllBytes(path + "0-" + jj + ".png", ImageConversion.EncodeToPNG(tex));} //TEST
        UnityEngine.Object.Destroy(tex);
        //return timer.ElapsedTicks + " (" + timer.ElapsedMilliseconds + "ms)"; //TEST
    }
    static byte HeightGenerator(Vector3 position) {
        float result, clamp;
        //timer.Start(); //TEST
        result = Noise("large_details", position, -200, 300);
        if((clamp = Clamp("cubed_mountains", position)) > 0.001f) { result += clamp * (Noise("mountains", position, 0, 750) + Noise("cellular_mountains", position, -1500, 1500)); } //reduced from -2500 2500
        if((clamp = Clamp("cubed_plateaus", position)) > 0.001f) { result += clamp * (Noise("tall_plateaus", position, 0, 150) + Noise("short_plateaus", position, 0, 75)); }
        //result += Noise("small_details", position, 0, 10); //too small to see
        //timer.Stop(); //TEST
        return (byte)((result + 1700) * 256 / 4475);//(result - sumLows) * 256 / (sumHighs - sumLows)
    }
    static float Clamp(string type, Vector3 position) {
        float result;
        switch(type) {
        case "cubed_mountains":
            result = Noise(position, 7, 0.002f, 0.7f);
            return Mathf.Clamp01(result * result * result * 13);
        case "cubed_plateaus":
            result = Noise(position, 6, 0.003f, 0.6f);
            return Mathf.Clamp01(result * result * result * 25);
        default:
            return 0;
        }
    }
    static float Noise(string type, Vector3 position, int low, int high) {
        float result;
        switch(type) {
        case "large_details":
            result = Noise(position, 8, 0.003f, 0.8f);
            break;
        case "small_details":
            result = Noise(position, 6, 0.05f, 0.8f);
            break;
        case "mountains":
            result = Noise(position, 11, 0.03f, 0.5f, "Ridged_Snoise");
            break;
        case "cellular_mountains":
            result = Noise(position, 3, 0.05f, 0.6f, "Cellular_Squared");
            break;
        case "tall_plateaus":
            result = Noise(position, 5, 0.08f, 0.55f);
            result = Mathf.Clamp(result * 14 - 13f / 3, -1, 1);
            break;
        case "short_plateaus":
            result = Noise(position, 5, 0.1f, 0.6f);
            result = Mathf.Clamp(result * 16 - 11f / 3, -1, 1);
            break;
        default:
            return 0;
        }
        return (result * (high - low) + high + low) / 2;
    }
    static float Noise(Vector3 position, int octaves, float frequency, float persistence, string type = null) {
        float total = 0;
        float maxAmplitude = 0;
        float amplitude = 1;
        Func<float, float> NoiseFuction = type switch {
            "Cellular_Squared" => frq => CellularSquared(position * frq),
            "Ridged_Snoise" => frq => 1 - 2 * Mathf.Abs(Snoise(position * frq)),
            _ => frq => Snoise(position * frq)
        };
        for(int i = 0;i < octaves;i++) {
            total += NoiseFuction(frequency) * amplitude;
            frequency *= 2;
            maxAmplitude += amplitude;
            amplitude *= persistence;
        }
        return total / maxAmplitude;//returns [-1,1]
    }
    /*Vector3[] Fibonacci_sphere(int samples = 1000) {
        Vector3[] points = [];
        float y, radius, phi = (float)Math.PI * ((float)Math.Sqrt(5) - 1f); //golden angle in radians

        for(int i = 0;i < samples;i++) {
            y = 1 - 2 * i / (float)(samples - 1); //from 1 to -1
            radius = (float)Math.Sqrt(1 - y * y); //radius at y
            points.Add(new Vector3((float)Math.Cos(phi * i) * radius, y, (float)Math.Sin(phi * i) * radius));
        }
        return points;
    }*/

    /** \file
		\brief Implements the SimplexNoise123 class for producing Perlin simplex noise.
		\author Stefan Gustavson (stegu76@liu.se) */
    static int[] perm = [151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10, 23, 190, 6, 148, 247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33, 88, 237, 149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134, 139, 48, 27, 166, 77, 146, 158, 231, 83, 111, 229, 122, 60, 211, 133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54, 65, 25, 63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169, 200, 196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64, 52, 217, 226, 250, 124, 123, 5, 202, 38, 147, 118, 126, 255, 82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42, 223, 183, 170, 213, 119, 248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9, 129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112, 104, 218, 246, 97, 228, 251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241, 81, 51, 145, 235, 249, 14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157, 184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93, 222, 114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180, 151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10, 23, 190, 6, 148, 247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33, 88, 237, 149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134, 139, 48, 27, 166, 77, 146, 158, 231, 83, 111, 229, 122, 60, 211, 133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54, 65, 25, 63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169, 200, 196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64, 52, 217, 226, 250, 124, 123, 5, 202, 38, 147, 118, 126, 255, 82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42, 223, 183, 170, 213, 119, 248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9, 129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112, 104, 218, 246, 97, 228, 251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241, 81, 51, 145, 235, 249, 14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157, 184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93, 222, 114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180];
    /*---------------------------------------------------------------------
     * Helper functions to compute gradients-dot-residualvectors (1D to 3D)
     * Note that these generate gradients of more than unit length. To make
     * a close match with the value range of classic Perlin noise, the final
     * noise values need to be rescaled to fit nicely within [-1,1].
     * (The simplex noise functions as such also have different scaling.)
     * Note also that these noise functions are the most practical and useful
     * signed version of Perlin noise. To return values according to the
     * RenderMan specification from the SL noise() and pnoise() functions,
     * the noise values need to be scaled and offset to [0,1], like this:
     * float SLnoise = (Snoise(x,y,z) + 1.0) * 0.5;*/
    /// <summary>Gradients-dot-residualvectors 3D.</summary>
    static float Grad(int hash, float x, float y, float z) {
        int h = hash & 15;     // Convert low 4 bits of hash code into 12 simple
        float u = h < 8 ? x : y; // gradient directions, and compute dot product.
        float v = h < 4 ? y : h == 12 || h == 14 ? x : z; // Fix repeats at h = 12 to 15
        return ((h & 1) > 0 ? -u : u) + ((h & 2) > 0 ? -v : v);
    }
    /// <summary>3D simplex noise.</summary>
    static float Snoise(Vector3 P) {
        // Simple skewing factors for the 3D case
        const float F3 = 0.333333333f;
        const float G3 = 0.166666667f;
        float n0, n1, n2, n3; // Noise contributions from the four corners
                              // Skew the input space to determine which simplex cell we're in
        float s = (P.x + P.y + P.z) * F3; // Very nice and simple skew factor for 3D
        float xs = P.x + s;
        float ys = P.y + s;
        float zs = P.z + s;
        int i = Mathf.FloorToInt(xs);
        int j = Mathf.FloorToInt(ys);
        int k = Mathf.FloorToInt(zs);
        float t = (i + j + k) * G3;
        float X0 = i - t; // Unskew the cell origin back to (x,y,z) space
        float Y0 = j - t;
        float Z0 = k - t;
        float x0 = P.x - X0; // The x,y,z distances from the cell origin
        float y0 = P.y - Y0;
        float z0 = P.z - Z0;
        // For the 3D case, the simplex shape is a slightly irregular tetrahedron.
        // Determine which simplex we are in.
        int i1, j1, k1; // Offsets for second corner of simplex in (i,j,k) coords
        int i2, j2, k2; // Offsets for third corner of simplex in (i,j,k) coords
        // This code would benefit from a backport from the GLSL version!
        if(x0 >= y0) {
            if(y0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; } // X Y Z order
              else if(x0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; } // X Z Y order
              else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; } // Z X Y order
        } else { // x0<y0
            if(y0 < z0) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; } // Z Y X order
            else if(x0 < z0) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; } // Y Z X order
            else { i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0; } // Y X Z order
        }
        // A step of (1,0,0) in (i,j,k) means a step of (1-c,-c,-c) in (x,y,z),
        // a step of (0,1,0) in (i,j,k) means a step of (-c,1-c,-c) in (x,y,z), and
        // a step of (0,0,1) in (i,j,k) means a step of (-c,-c,1-c) in (x,y,z), where c = 1/6.
        float x1 = x0 - i1 + G3; // Offsets for second corner in (x,y,z) coords
        float y1 = y0 - j1 + G3;
        float z1 = z0 - k1 + G3;
        float x2 = x0 - i2 + 2 * G3; // Offsets for third corner in (x,y,z) coords
        float y2 = y0 - j2 + 2 * G3;
        float z2 = z0 - k2 + 2 * G3;
        float x3 = x0 - 1 + 3 * G3; // Offsets for last corner in (x,y,z) coords
        float y3 = y0 - 1 + 3 * G3;
        float z3 = z0 - 1 + 3 * G3;
        // Wrap the integer indices at 256, to avoid indexing perm[] out of bounds
        int ii = i & 0xff;
        int jj = j & 0xff;
        int kk = k & 0xff;
        // Calculate the contribution from the four corners
        float t0 = 0.6f - x0 * x0 - y0 * y0 - z0 * z0;
        if(t0 < 0) n0 = 0;
        else {
            t0 *= t0;
            n0 = t0 * t0 * Grad(perm[ii + perm[jj + perm[kk]]], x0, y0, z0);
        }
        float t1 = 0.6f - x1 * x1 - y1 * y1 - z1 * z1;
        if(t1 < 0) n1 = 0;
        else {
            t1 *= t1;
            n1 = t1 * t1 * Grad(perm[ii + i1 + perm[jj + j1 + perm[kk + k1]]], x1, y1, z1);
        }
        float t2 = 0.6f - x2 * x2 - y2 * y2 - z2 * z2;
        if(t2 < 0) n2 = 0;
        else {
            t2 *= t2;
            n2 = t2 * t2 * Grad(perm[ii + i2 + perm[jj + j2 + perm[kk + k2]]], x2, y2, z2);
        }
        float t3 = 0.6f - x3 * x3 - y3 * y3 - z3 * z3;
        if(t3 < 0) n3 = 0;
        else {
            t3 *= t3;
            n3 = t3 * t3 * Grad(perm[ii + 1 + perm[jj + 1 + perm[kk + 1]]], x3, y3, z3);
        }
        // Add contributions from each corner to get the final noise value.
        // The result is scaled to stay just inside [-1,1]
        return 32 * (n0 + n1 + n2 + n3); // TODO: The scale factor is preliminary!
    }

    static float CellularSquared(Vector3 P) {
        Vector2 tmp = NewCellular(P);
        tmp.y -= tmp.x;
        return tmp.y * tmp.y;
    }
    /// <summary>Vector floor to int, component-wise.</summary>
    static Vector3Int FloorToInt(Vector3 a) => new(Mathf.FloorToInt(a.x), Mathf.FloorToInt(a.y), Mathf.FloorToInt(a.z));
    static Vector3 GetCellJitter(Vector3Int coord) {
        // Create 3 separate hashes to avoid repeated values across axes
        return new Vector3(
            perm[(coord.x + perm[(coord.y + perm[coord.z & 255]) & 255]) & 255] / 256f - 0.5f,
            perm[(coord.x + 19 + perm[(coord.y + 73 + perm[(coord.z + 47) & 255]) & 255]) & 255] / 256f - 0.5f,
            perm[(coord.x + 131 + perm[(coord.y + 251 + perm[(coord.z + 7) & 255]) & 255]) & 255] / 256f - 0.5f
        );
    }
    static Vector2 NewCellular(Vector3 P) {
        float F1 = float.MaxValue, F2 = float.MaxValue;
        Vector3Int Pi = FloorToInt(P);
        Vector3 Pf = P - Pi;

        for(int dz = -1;dz <= 1;dz++) {
            for(int dy = -1;dy <= 1;dy++) {
                for(int dx = -1;dx <= 1;dx++) {
                    float diff = (Pf - GetCellJitter(new Vector3Int(Pi.x + dx, Pi.y + dy, Pi.z + dz)) - new Vector3(dx, dy, dz)).sqrMagnitude;
                    if(diff < F1) {
                        F2 = F1;
                        F1 = diff;
                    } else if(diff < F2) {
                        F2 = diff;
                    }
                }
            }
        }
        return new Vector2(Mathf.Sqrt(F1), Mathf.Sqrt(F2));
    }

    /*/ Cellular noise ("Worley noise") in 3D in GLSL. Copyright (c) Stefan Gustavson 2011-04-19. https://github.com/stegu/webgl-noise
    /// <summary>Vector offset.<br/>=> (v.x + a, v.y + a, v.z + a)</summary>
    static Vector3 Vop(Vector3 a, float b) => new (a.x + b, a.y + b, a.z + b);
    /// <summary>Vector multiplication, component-wise.</summary>
    static Vector3 Vop(Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);
    /// <summary>Decimal part.</summary>
    static float Frac(float x) => x - (int)x;
    /// <summary>Vector decimal part, component-wise.</summary>
    static Vector3 Frac(Vector3 a) => new(Frac(a.x), Frac(a.y), Frac(a.z));
    /// <summary>Vector floor, component-wise.</summary>
    static Vector3 Floor(Vector3 a) => new(Mathf.Floor(a.x), Mathf.Floor(a.y), Mathf.Floor(a.z));
    /// <summary>Modulo 7 without a division.</summary>
    static float Mod7(float x) => x - Mathf.Floor(x * (1f / 7)) * 7;
    /// <summary>Component-wise modulo 7 of floors.</summary>
    static Vector3 Mod7(Vector3 a) => new(Mod7(Mathf.Floor(a.x)), Mod7(Mathf.Floor(a.y)), Mod7(Mathf.Floor(a.z)));
    /// <summary>Modulo 289 without a division.</summary>
    static float Mod289(float x) => x - Mathf.Floor(x * (1f / 289)) * 289;
    /// <summary>Component-wise modulo 289 of floors.</summary>
    static Vector3 Mod289(Vector3 a) => new(Mod289(Mathf.Floor(a.x)), Mod289(Mathf.Floor(a.y)), Mod289(Mathf.Floor(a.z)));
    /// <summary>Permutation polynomial.<br/>=> (34 x^2 + x) % 289</summary>
    static float Prm(float x) => Mod289((34 * x + 1) * x);
    /// <summary>Vector permutation polynomial with offset.<br/>=> Prm(vector + offset), component-wise</summary>
    static Vector3 Prm(Vector3 a, float b = 0) => new(Prm(a.x + b), Prm(a.y + b), Prm(a.z + b));
    /// <summary>3D Cellular noise ("Worley noise") with 3x3x3 search region for good F2 everywhere.</summary>
    static Vector2 Cellular(Vector3 P) {
        timer.Start();
        const float K = 0.142857142857f; // 1/7
        const float Ko = 0.428571428571f; // 1/2-K/2
        const float K2 = 0.020408163265306f; // 1/(7*7)
        const float Kz = 0.166666666667f; // 1/6
        const float Kzo = 0.416666666667f; // 1/2-1/6*2
        const float jitter = 1; // smaller jitter gives more regular pattern

        Vector3 Pi = Mod289(P);
        Vector3 Pf = new(Frac(P.x) - 0.5f, Frac(P.y) - 0.5f, Frac(P.z) - 0.5f);
        Vector3 Pfx = new(Pf.x + 1, Pf.x, Pf.x - 1);
        Vector3 Pfy = new(Pf.y + 1, Pf.y, Pf.y - 1);
        Vector3 Pfz = new(Pf.z + 1, Pf.z, Pf.z - 1);

        Vector3 p = Prm(new Vector3(-1, 0, 1), Pi.x);
        Vector3 p1 = Prm(p, Pi.y - 1);
        Vector3 p2 = Prm(p, Pi.y);
        Vector3 p3 = Prm(p, Pi.y + 1);
        Vector3 p11 = Prm(p1, Pi.z - 1);
        Vector3 p12 = Prm(p1, Pi.z);
        Vector3 p13 = Prm(p1, Pi.z + 1);
        Vector3 p21 = Prm(p2, Pi.z - 1);
        Vector3 p22 = Prm(p2, Pi.z);
        Vector3 p23 = Prm(p2, Pi.z + 1);
        Vector3 p31 = Prm(p3, Pi.z - 1);
        Vector3 p32 = Prm(p3, Pi.z);
        Vector3 p33 = Prm(p3, Pi.z + 1);

        Vector3 dx11 = jitter * Vop(Frac(p11 * K), -Ko) + Pfx;
        Vector3 dy11 = Vop(jitter * Vop(Mod7(p11 * K) * K, -Ko), Pfy.x);
        Vector3 dz11 = Vop(jitter * Vop(Floor(p11 * K2) * Kz, -Kzo), Pfz.x);
        Vector3 dx12 = jitter * Vop(Frac(p12 * K), -Ko) + Pfx;
        Vector3 dy12 = Vop(jitter * Vop(Mod7(p12 * K) * K, -Ko), Pfy.x);
        Vector3 dz12 = Vop(jitter * Vop(Floor(p12 * K2) * Kz, -Kzo), Pfz.y);
        Vector3 dx13 = jitter * Vop(Frac(p13 * K), -Ko) + Pfx;
        Vector3 dy13 = Vop(jitter * Vop(Mod7(p13 * K) * K, -Ko), Pfy.x);
        Vector3 dz13 = Vop(jitter * Vop(Floor(p13 * K2) * Kz, -Kzo), Pfz.z);
        Vector3 dx21 = jitter * Vop(Frac(p21 * K), -Ko) + Pfx;
        Vector3 dy21 = Vop(jitter * Vop(Mod7(p21 * K) * K, -Ko), Pfy.y);
        Vector3 dz21 = Vop(jitter * Vop(Floor(p21 * K2) * Kz, -Kzo), Pfz.x);
        Vector3 dx22 = jitter * Vop(Frac(p22 * K), -Ko) + Pfx;
        Vector3 dy22 = Vop(jitter * Vop(Mod7(p22 * K) * K, -Ko), Pfy.y);
        Vector3 dz22 = Vop(jitter * Vop(Floor(p22 * K2) * Kz, -Kzo), Pfz.y);
        Vector3 dx23 = jitter * Vop(Frac(p23 * K), -Ko) + Pfx;
        Vector3 dy23 = Vop(jitter * Vop(Mod7(p23 * K) * K, -Ko), Pfy.y);
        Vector3 dz23 = Vop(jitter * Vop(Floor(p23 * K2) * Kz, -Kzo), Pfz.z);
        Vector3 dx31 = jitter * Vop(Frac(p31 * K), -Ko) + Pfx;
        Vector3 dy31 = Vop(jitter * Vop(Mod7(p31 * K) * K, -Ko), Pfy.z);
        Vector3 dz31 = Vop(jitter * Vop(Floor(p31 * K2) * Kz, -Kzo), Pfz.x);
        Vector3 dx32 = jitter * Vop(Frac(p32 * K), -Ko) + Pfx;
        Vector3 dy32 = Vop(jitter * Vop(Mod7(p32 * K) * K, -Ko), Pfy.z);
        Vector3 dz32 = Vop(jitter * Vop(Floor(p32 * K2) * Kz, -Kzo), Pfz.y);
        Vector3 dx33 = jitter * Vop(Frac(p33 * K), -Ko) + Pfx;
        Vector3 dy33 = Vop(jitter * Vop(Mod7(p33 * K) * K, -Ko), Pfy.z);
        Vector3 dz33 = Vop(jitter * Vop(Floor(p33 * K2) * Kz, -Kzo), Pfz.z);

        dx11 = Vop(dx11, dx11) + Vop(dy11, dy11) + Vop(dz11, dz11);
        dx12 = Vop(dx12, dx12) + Vop(dy12, dy12) + Vop(dz12, dz12);
        dx13 = Vop(dx13, dx13) + Vop(dy13, dy13) + Vop(dz13, dz13);
        dx21 = Vop(dx21, dx21) + Vop(dy21, dy21) + Vop(dz21, dz21);
        dx22 = Vop(dx22, dx22) + Vop(dy22, dy22) + Vop(dz22, dz22);
        dx23 = Vop(dx23, dx23) + Vop(dy23, dy23) + Vop(dz23, dz23);
        dx31 = Vop(dx31, dx31) + Vop(dy31, dy31) + Vop(dz31, dz31);
        dx32 = Vop(dx32, dx32) + Vop(dy32, dy32) + Vop(dz32, dz32);
        dx33 = Vop(dx33, dx33) + Vop(dy33, dy33) + Vop(dz33, dz33);

        // Sort out the two smallest distances (F1, F2)
        // Do it right and sort out both F1 and F2
        Vector3 da = Vector3.Min(dx11, dx12);
        dx12 = Vector3.Max(dx11, dx12);
        dx11 = Vector3.Min(da, dx13); // Smallest now not in dx12 or dx13
        dx13 = Vector3.Max(da, dx13);
        dx12 = Vector3.Min(dx12, dx13); // 2nd smallest now not in dx13
        da = Vector3.Min(dx21, dx22);
        dx22 = Vector3.Max(dx21, dx22);
        dx21 = Vector3.Min(da, dx23); // Smallest now not in dx22 or dx23
        dx23 = Vector3.Max(da, dx23);
        dx22 = Vector3.Min(dx22, dx23); // 2nd smallest now not in dx23
        da = Vector3.Min(dx31, dx32);
        dx32 = Vector3.Max(dx31, dx32);
        dx31 = Vector3.Min(da, dx33); // Smallest now not in dx32 or dx33
        dx33 = Vector3.Max(da, dx33);
        dx32 = Vector3.Min(dx32, dx33); // 2nd smallest now not in dx33
        da = Vector3.Min(dx11, dx21);
        dx21 = Vector3.Max(dx11, dx21);
        dx11 = Vector3.Min(da, dx31); // Smallest now in dx11
        dx31 = Vector3.Max(da, dx31); // 2nd smallest now not in dx31
        if(dx11.x > dx11.y) (dx11.x, dx11.y) = (dx11.y, dx11.x);
        if(dx11.x > dx11.z) (dx11.x, dx11.z) = (dx11.z, dx11.x); // dx11.x now smallest
        dx12 = Vector3.Min(dx12, dx21); // 2nd smallest now not in dx21
        dx12 = Vector3.Min(dx12, dx22); // nor in dx22
        dx12 = Vector3.Min(dx12, dx31); // nor in dx31
        dx12 = Vector3.Min(dx12, dx32); // nor in dx32
        (dx11.y, dx11.z) = (Mathf.Min(dx11.y, dx12.x), Mathf.Min(dx11.z, dx12.y)); // nor in dx12.xy!
        dx11.y = Mathf.Min(dx11.y, dx12.z); // Only two more to go
        dx11.y = Mathf.Min(dx11.y, dx11.z); // Done! (Phew!)
        timer.Stop();
        return new Vector2(Mathf.Sqrt(dx11.x), Mathf.Sqrt(dx11.y)); // F1, F2
    }//*/
}

[HarmonyPatch]
public static class MyPatchClass {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ReferenceFrame), nameof(ReferenceFrame.GetHUDDisplayName))]
    static bool GetHUDDisplayName_Prefix(ReferenceFrame __instance, ref string __result) {
        MVBGalacticMap mapParameters = __instance._attachedOWRigidbody.GetComponent<MVBGalacticMap>();
        if(mapParameters != null) {
            __result = mapParameters.mapName;
            return false;
        }
        return true;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ReferenceFrameTracker), nameof(ReferenceFrameTracker.FindReferenceFrameInMapView))]
    static IEnumerable<CodeInstruction> FindReferenceFrameInMapView_Transpiler(IEnumerable<CodeInstruction> instructions) {
        return new CodeMatcher(instructions).MatchForward(false,
            new CodeMatch(i => i.opcode == System.Reflection.Emit.OpCodes.Ldc_R4 && Convert.ToInt32(i.operand) == 100000)
        ).Repeat(match => match.SetOperandAndAdvance(7000000f)).InstructionEnumeration();
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MapController), nameof(MapController.LateUpdate))]
    static IEnumerable<CodeInstruction> LateUpdate_Transpiler(IEnumerable<CodeInstruction> instructions) {
        return new CodeMatcher(instructions).MatchForward(false,
            new CodeMatch(i => i.opcode == System.Reflection.Emit.OpCodes.Ldfld && ((System.Reflection.FieldInfo)i.operand).Name == "_zoomSpeed")
        ).Advance(1).InsertAndAdvance(
            new CodeInstruction(System.Reflection.Emit.OpCodes.Mul),
            new CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_R4, 4f),
            new CodeInstruction(System.Reflection.Emit.OpCodes.Mul),
            new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0),
            CodeInstruction.LoadField(typeof(MapController),"_zoom"),
            new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0),
            CodeInstruction.LoadField(typeof(MapController), "_maxZoomDistance"),
            new CodeInstruction(System.Reflection.Emit.OpCodes.Div)
        ).InstructionEnumeration();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TimeLoop), nameof(TimeLoop.IsTimeFlowing))]
    static bool IsTimeFlowing_Prefix(ref bool __result) {
        __result = true;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TimeLoop), nameof(TimeLoop.Start))]
    static void Start_Postfix() {
        TimeLoop._isTimeFlowing = false;
    }
}

//TODO:
//  add mysterious artefacts (one / 10 systems) that increase warpPower towards 1
//  correct player suit spawn duplicate in ship
//(NEED for V1):
//  add heightmaps mipmap1
//  add textures
//  add water level sometimes
//TO TEST:
//  HM without cellular
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
