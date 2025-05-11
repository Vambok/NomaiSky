using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
/*using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;*/

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
    public readonly int entryRadius = 100000; //system max radius = 71400
    // WARPING:
    Vector3 entryPosition;
    Quaternion entryRotation;
    // GENERATION:
    readonly int galaxyName = 0;

    //add mysterious artefacts (one / 10 systems) that increase warpPower towards 1
    //  bigger referenceframevolume (entryradius)
    //  galactic key not found
    //add proxies
    //  correct player spawn
    //  make shiplog entries sprites
    //  correct shiplog mapMode
    //add heightmaps
    //  lower map speed close range
    //  star colors too light?
    //  star light too colored
    //increase sprites resolution
    //  make better global outline sprite

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
        if(otherModsSystems.ContainsKey((x, y, z))) {
            (string name, float radius, Color32 color, string starName) = otherModsSystems[(x, y, z)];
            return (name, starName, radius, color, new Vector3(Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius)));
        } else {
            Random128.Initialize(galaxyName, x, y, z);
            string starName = StarNameGen();
            float radius = GaussianDist(4000, 800);
            byte colorR = (byte)Mathf.Min(IGaussianDist(150), 255);
            byte colorG = (byte)Mathf.Min(IGaussianDist(150), 255);
            byte colorB = (byte)Mathf.Min(IGaussianDist(150), 255);
            return ("NomaiSky_" + starName, starName, radius, new Color32(colorR, colorG, colorB, 255), new Vector3(Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius), Random128.Rng.Range(entryRadius - systemRadius, systemRadius - entryRadius)));
        }
    }
    void LoadCurrentSystem() {
        ShipLogFactSave getCurrentCenter = PlayerData.GetShipLogFactSave("NomaiSky_currentCenter");
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
        mat.SetColor("_EmissionColor", new Color(3, 3, 0));
        mat.SetColor("_Color", Color.black); // Optional
        s.GetComponent<MeshRenderer>().material = mat;
        s.transform.position = new Vector3(100000, 100000, 0);
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
                            star.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", 2 * (Color)color);
                            star.transform.position = 2 * systemRadius * new Vector3(x, y, z) + offset - currentOffset;
                            star.transform.localScale *= radius / 2000;
                            star.GetComponent<MVBGalacticMap>().Initializator((currentCenter.x + x, currentCenter.y + y, currentCenter.z + z), starName);
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
    //PlayerData._currentGameSave.shipLogFactSaves["NomaiSky_currentCenter"] = new ShipLogFactSave("(0,0,0)");
    //PlayerData.GetShipLogFactSave("NomaiSky_currentCenter").id;
    void SpawnIntoSystem(string systemName) {
        if(!otherModsSystems.ContainsKey(currentCenter)) {
            Transform shipSpawnPoint = NewHorizons.GetPlanet(systemName.Substring(9)).transform.Find("ShipSpawnPoint");
            Transform spawnPoint = NewHorizons.GetPlanet(systemName.Substring(9)).transform.Find("PlayerSpawnPoint");
            if(entryPosition != Vector3.zero) {
                shipSpawnPoint.position = entryPosition;
                shipSpawnPoint.rotation = entryRotation;
                spawnPoint.position = entryPosition;
                spawnPoint.rotation = entryRotation;
            } else {
                shipSpawnPoint.position = new Vector3(0, 10000, -34100);
                shipSpawnPoint.eulerAngles = new Vector3(16.334f, 0, 0);
                spawnPoint.position = new Vector3(0, 10000, -34100);
                spawnPoint.eulerAngles = new Vector3(16.334f, 0, 0);
            }
        }
        entryPosition = Vector3.zero;
        ModHelper.Events.Unity.FireInNUpdates(() => {
            if(!otherModsSystems.ContainsKey(currentCenter)) {
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
        int allowedOrbits = 65000 - nbPlanets * 6500;
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
            planetOutputFile.Write(PlanetCreator(systemName, planetName, orbits[i] + 6500 * i + 9700, (i == fuelPlanet && fuelMoon < 0)));//, "", i == Mathf.FloorToInt(nbPlanets / 2f)));
            if(nbMoons > 0) {
                int[] moonOrbits = new int[nbMoons];
                int allowedMoonOrbits = 2200 - (nbMoons * 400);
                for(int j = 0;j < nbMoons;j++) {
                    moonOrbits[j] = Random128.Rng.Range(0, allowedOrbits);
                }
                Array.Sort(moonOrbits);
                for(int j = 0;j < nbMoons;j++) {
                    string moonName = PlanetNameGen(true);
                    Directory.CreateDirectory(Path.Combine(path, planetName, moonName.Replace(' ', '_')));
                    using StreamWriter outputMoonFile = new(Path.Combine(path, planetName, moonName.Replace(' ', '_'), moonName.Replace(' ', '_') + ".json"));
                    outputMoonFile.Write(PlanetCreator(systemName, moonName, moonOrbits[j] + 400 * j + 1200, j == fuelMoon, planetName));
                }
            }
        }
        return """{"$schema":"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/star_system_schema.json","respawnHere": true}""";
    }
    string StarCreator(string solarSystem) {
        string relativePath = "planets/" + solarSystem + "/" + solarSystem + "/";
        string finalJson = "{\n\"name\": \"" + solarSystem + "\",\n";
        finalJson += "\"$schema\": \"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json\",\n";
        finalJson += "\"starSystem\": \"NomaiSky_" + solarSystem + "\",\n";
        finalJson += "\"canShowOnTitle\": false,\n";
        finalJson += "\"Base\": {\n";
        float radius = GaussianDist(4000, 800);
        byte colorR = (byte)Mathf.Min(IGaussianDist(150), 255);
        byte colorG = (byte)Mathf.Min(IGaussianDist(150), 255);
        byte colorB = (byte)Mathf.Min(IGaussianDist(150), 255);
        finalJson += "    \"surfaceSize\": " + radius.ToString(CultureInfo.InvariantCulture) + ",\n";
        finalJson += "    \"surfaceGravity\": " + GaussianDist(radius * 3 / 500).ToString(CultureInfo.InvariantCulture) + ",\n";
        finalJson += "    \"gravityFallOff\": \"inverseSquared\",\n";
        finalJson += "    \"centerOfSolarSystem\": true\n";
        finalJson += "},\n";
        finalJson += "\"Orbit\": {\n";
        finalJson += "    \"showOrbitLine\": false,\n";
        finalJson += "    \"isStatic\": true\n";
        finalJson += "},\n";
        finalJson += "\"Star\": {\n";
        finalJson += "    \"size\": " + radius.ToString(CultureInfo.InvariantCulture) + ",\n";
        finalJson += "    \"tint\": {\n";
        SpriteGenerator("star", relativePath + "map_star.png", colorR, colorG, colorB);
        finalJson += "        \"r\": " + colorR + ",\n";
        finalJson += "        \"g\": " + colorG + ",\n";
        finalJson += "        \"b\": " + colorB + ",\n";
        finalJson += "        \"a\": 255\n";
        finalJson += "    },\n";
        finalJson += "    \"lightTint\": {\n";
        finalJson += "        \"r\": " + (colorR + 510) / 3 + ",\n";
        finalJson += "        \"g\": " + (colorG + 510) / 3 + ",\n";
        finalJson += "        \"b\": " + (colorB + 510) / 3 + ",\n";
        finalJson += "        \"a\": 255\n";
        finalJson += "    },\n";
        finalJson += "    \"solarLuminosity\": " + Random128.Rng.Range(0.3f, 3f).ToString(CultureInfo.InvariantCulture) + ",\n";
        finalJson += "    \"stellarDeathType\": \"none\"\n";
        finalJson += "},\n";
        finalJson += "\"Spawn\": {\n";
        finalJson += "    \"playerSpawnPoints\": [\n";
        finalJson += "        {\"isDefault\": true,\n\"startWithSuit\": true,\n";
        finalJson += "        \"position\": {\"x\": 0, \"y\": 10000, \"z\": -34100},\n";
        finalJson += "        \"rotation\": {\"x\": 16.334, \"y\": 0, \"z\": 0}}\n";
        finalJson += "    ],\n";
        finalJson += "    \"shipSpawnPoints\": [\n";
        finalJson += "        {\"isDefault\": true,\n";
        finalJson += "        \"position\": {\"x\": 0, \"y\": 10000, \"z\": -34100},\n";
        finalJson += "        \"rotation\": {\"x\": 16.334, \"y\": 0, \"z\": 0}}\n";
        finalJson += "    ]\n";
        finalJson += "},\n";
        finalJson += "\"ShipLog\": {\n";
        //finalJson += "    \"spriteFolder\": \"" + relativePath + "sprites\",\n";
        finalJson += "    \"mapMode\": {\n";
        finalJson += "        \"revealedSprite\": \"" + relativePath + "map_star.png\",\n";
        finalJson += "        \"scale\": " + (radius / 500f).ToString(CultureInfo.InvariantCulture) + ",\n";
        finalJson += "        \"selectable\": false\n";
        finalJson += "    }\n";
        return finalJson + "}\n}";
    }
    string PlanetCreator(string solarSystem, string planetName, int orbit, bool fuel, string orbiting = "") {
        string relativePath = "planets/" + solarSystem + "/" + (orbiting != "" ? orbiting + "/" : "") + planetName.Replace(' ', '_') + "/";
        string characteristics = "A ";
        List<char> vowels = ['a', 'e', 'i', 'o', 'u'];
        string finalJson = "{\n\"name\": \"" + planetName + "\",\n";
        finalJson += "\"$schema\": \"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/body_schema.json\",\n";
        if(solarSystem == "SolarSystem") {
            finalJson += "\"starSystem\": \"SolarSystem\",\n";
            solarSystem = "Sun";
        } else {
            finalJson += "\"starSystem\": \"NomaiSky_" + solarSystem + "\",\n";
        }
        finalJson += "\"canShowOnTitle\": false,\n";
        finalJson += "\"Base\": {\n";
        float radius = (orbiting == "") ? GaussianDist(500, 150) : GaussianDist(100, 30);
        finalJson += "    \"groundSize\": " + (radius - 1).ToString(CultureInfo.InvariantCulture) + ",\n";
        finalJson += "    \"surfaceSize\": " + radius.ToString(CultureInfo.InvariantCulture) + ",\n";
        characteristics += (radius * (orbiting == "" ? 1 : 5)) switch {
            > 900 => "enormous ",
            > 800 => "huge ",
            > 650 => "big ",
            < 100 => "minuscule ",
            < 200 => "tiny ",
            < 350 => "small ",
            _ => "",
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
            _ => "",
        };
        finalJson += "    \"gravityFallOff\": \"inverseSquared\"\n";
        finalJson += "},\n";
        finalJson += "\"ProcGen\": {\n";
        finalJson += "    \"color\": {\n";
        byte colorR = (byte)IGaussianDist(130, 50, 2.5f);
        byte colorG = (byte)IGaussianDist(130, 50, 2.5f);
        byte colorB = (byte)IGaussianDist(130, 50, 2.5f);
        SpriteGenerator("planet", relativePath + "map_planet.png", colorR, colorG, colorB);
        finalJson += "        \"r\": " + colorR + ",\n";
        finalJson += "        \"g\": " + colorG + ",\n";
        finalJson += "        \"b\": " + colorB + "\n";
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
            _ => stemp,
        };
        if(vowels.Contains(characteristics[2])) {
            characteristics = characteristics.Insert(1, "n");
        }
        finalJson += "},\n";
        finalJson += "\"Orbit\": {\n";
        if(orbiting != "") {
            finalJson += "    \"isMoon\": true,\n";
            finalJson += "    \"primaryBody\": \"" + orbiting + "\",\n";
            characteristics += "moon, with ";
        } else {
            finalJson += "    \"primaryBody\": \"" + solarSystem + "\",\n";
            characteristics += "planet, with ";
        }
        finalJson += "    \"semiMajorAxis\": " + orbit + ",\n";
        finalJson += "    \"inclination\": " + GaussianDist(0, 10, 9).ToString(CultureInfo.InvariantCulture) + ",\n";
        finalJson += "    \"longitudeOfAscendingNode\": " + Random128.Rng.Range(0, 360) + ",\n";
        finalJson += "    \"trueAnomaly\": " + Random128.Rng.Range(0, 360) + ",\n";//(isSpawn ? 0 : Random128.Rng.Range(0, 360)) + ",\n";
        finalJson += "    \"isTidallyLocked\": " + (Random128.Rng.Range(0, 4) == 0).ToString().ToLower() + "\n";
        finalJson += "},\n";
        float ringRadius = 0;
        if(Random128.Rng.Range(0, 10) == 0) {
            finalJson += "\"Rings\": [{\n";
            float ringInnerRadius = GaussianDist(radius * 2, radius / 5);
            finalJson += "    \"innerRadius\": " + ringInnerRadius.ToString(CultureInfo.InvariantCulture) + ",\n";
            float ringSpread = (radius * 3 - ringInnerRadius) / 2;
            ringRadius = GaussianDist(radius * 3 - ringSpread, ringSpread / 3);
            finalJson += "    \"outerRadius\": " + ringRadius.ToString(CultureInfo.InvariantCulture) + ",\n";
            finalJson += "    \"texture\": \"" + relativePath + "rings.png\",\n";
            finalJson += "    \"fluidType\": \"sand\"\n";
            finalJson += "}],\n";
            colorR = (byte)IGaussianDist(130, 50, 2.5f);
            colorG = (byte)IGaussianDist(130, 50, 2.5f);
            colorB = (byte)IGaussianDist(130, 50, 2.5f);
            SpriteGenerator("rings", relativePath, colorR, colorG, colorB, (byte)Mathf.Min(IGaussianDist(200, 50, 4), 255), [(byte)Mathf.CeilToInt(128 * (1 - ringInnerRadius / ringRadius))]);
            characteristics += GetColorName(new Color32(colorR, colorG, colorB, 255)) + " rings and ";
        }
        finalJson += "\"Atmosphere\": {\n";
        float atmosphereSize = GaussianDist(radius * 6 / 5, radius / 20, 4);
        finalJson += "    \"size\": " + atmosphereSize.ToString(CultureInfo.InvariantCulture) + ",\n";
        finalJson += "    \"atmosphereTint\": {\n";
        colorR = (byte)Mathf.Min(IGaussianDist(200, 50, 4), 255);
        colorG = (byte)Mathf.Min(IGaussianDist(200, 50, 4), 255);
        colorB = (byte)Mathf.Min(IGaussianDist(200, 50, 4), 255);
        byte colorA = (byte)Mathf.Min(IGaussianDist(255, 50, 5), 255);
        SpriteGenerator("atmosphere", relativePath + "map_atmosphere.png", colorR, colorG, colorB, colorA);
        finalJson += "        \"r\": " + colorR + ",\n";
        finalJson += "        \"g\": " + colorG + ",\n";
        finalJson += "        \"b\": " + colorB + ",\n";
        finalJson += "        \"a\": " + colorA + "\n";
        stemp = GetColorName(new Color32(colorR, colorG, colorB, 255));
        characteristics += (vowels.Contains(stemp[0]) ? "an " : "a ") + stemp + " atmosphere";
        finalJson += "    },\n";
        if(Random128.Rng.Range(0, 4) == 0) {
            finalJson += "    \"fogTint\": {\n";
            finalJson += "        \"r\": " + IGaussianDist(130, 50, 2.5f) + ",\n";
            finalJson += "        \"g\": " + IGaussianDist(130, 50, 2.5f) + ",\n";
            finalJson += "        \"b\": " + IGaussianDist(130, 50, 2.5f) + ",\n";
            finalJson += "        \"a\": " + Mathf.Min(IGaussianDist(255, 50, 5), 255) + "\n";
            finalJson += "    },\n";
            finalJson += "    \"fogSize\": " + Random128.Rng.Range(radius, atmosphereSize).ToString(CultureInfo.InvariantCulture) + ",\n";
            finalJson += "    \"fogDensity\": " + Random128.Rng.Range(0f, 1f).ToString(CultureInfo.InvariantCulture) + ",\n";
        }
        bool hasTrees = Random128.Rng.Range(0, 2) == 0;
        finalJson += "    \"hasOxygen\": " + hasTrees.ToString().ToLower() + ",\n";
        if(hasTrees) {
            characteristics += ". There seems to be oxygen";
            hasTrees = Random128.Rng.Range(0, 2) == 0;
        }
        finalJson += "    \"hasTrees\": " + hasTrees.ToString().ToLower() + ",\n";
        finalJson += "    \"hasRain\": " + (Random128.Rng.Range(0, 6) == 0).ToString().ToLower() + "\n";
        finalJson += "},\n";
        if(hasTrees || fuel) {
            finalJson += "\"Props\": {\n";
            finalJson += "    \"scatter\": [\n";
            if(fuel) {
                finalJson += "{\"path\": \"" + (hasDLC ? "RingWorld_Body/Sector_RingInterior/Sector_Zone2/Structures_Zone2/EyeTempleRuins_Zone2/Interactables_EyeTempleRuins_Zone2/Prefab_IP_FuelTorch (1)\"" : "CaveTwin_Body/Sector_CaveTwin/Sector_NorthHemisphere/Sector_NorthSurface/Sector_Lakebed/Interactables_Lakebed/Prefab_HEA_FuelTank\", \"rotation\": {\"x\": 30, \"y\": 0, \"z\": 270}") + ", \"count\": 1}" + (hasTrees ? "," : "") + "\n";
            }
            if(hasTrees) {
                finalJson += "        {\"path\": \"" + (hasDLC ? "DreamWorld_Body/Sector_DreamWorld/Sector_Underground/IslandsRoot/IslandPivot_B/Island_B/Props_Island_B/Tree_DW_L (3)" : "QuantumMoon_Body/Sector_QuantumMoon/State_TH/Interactables_THState/Crater_1/Crater_1_QRedwood/QRedwood (2)/Prefab_TH_Redwood") + "\", \"count\": " + IGaussianDist(radius * radius / 1250) + ", \"scale\": " + GaussianDist(1, 0.2f).ToString(CultureInfo.InvariantCulture) + "},\n";
                finalJson += "        {\"path\": \"" + (hasDLC ? "DreamWorld_Body/Sector_DreamWorld/Sector_DreamZone_4/Props_DreamZone_4_Upper/Tree_DW_S_B" : "QuantumMoon_Body/Sector_QuantumMoon/State_TH/Interactables_THState/Crater_3/Crater_3_Sapling/QSapling/Tree_TH_Sapling") + "\", \"count\": " + IGaussianDist(radius * radius / 1250) + ", \"scale\": " + GaussianDist(1, 0.2f).ToString(CultureInfo.InvariantCulture) + "}\n";
                characteristics += " and trees";
            }
            finalJson += "    ]\n";
            finalJson += "},\n";
        }
        characteristics += ".";
        /*if(isSpawn) {
            finalJson += "\"Spawn\": {\n";
            finalJson += "    \"shipSpawnPoints\": [\n";
            finalJson += "        {\"position\": {\"x\": 0, \"y\": 10000, \"z\": 0},\n";
            finalJson += "        \"isDefault\": true,\n";
            finalJson += "        \"rotation\": {\"x\": 16.334, \"y\": 270, \"z\": 0}}\n";
            finalJson += "    ]\n";
            finalJson += "},\n";
        }*/
        finalJson += "\"ShipLog\": {\n";
        finalJson += "    \"spriteFolder\": \"" + relativePath + "sprites\",\n";
        finalJson += "    \"xmlFile\": \"" + relativePath + "shiplogs.xml\",\n";
        finalJson += "    \"mapMode\": {\n";
        finalJson += "        \"outlineSprite\": \"planets/outline.png\",\n";
        finalJson += "        \"revealedSprite\": \"" + relativePath + "map_atmosphere.png\",\n";
        finalJson += "        \"scale\": " + (atmosphereSize / 500f).ToString(CultureInfo.InvariantCulture) + ",\n";
        finalJson += "        \"offset\": " + (atmosphereSize / 500f).ToString(CultureInfo.InvariantCulture) + ",\n";
        finalJson += "        \"details\": [\n";
        finalJson += "            {\"revealedSprite\": \"" + relativePath + "map_planet.png\",\n";
        finalJson += "            \"scale\": {\"x\": " + (radius / 500f).ToString(CultureInfo.InvariantCulture) + ",\"y\": " + (radius / 500f).ToString(CultureInfo.InvariantCulture) + "},\n";
        if(ringRadius > 0) {
            finalJson += "            \"invisibleWhenHidden\": true},\n";
            finalJson += "            {\"revealedSprite\": \"" + relativePath + "map_rings.png\",\n";
            finalJson += "            \"scale\": {\"x\": " + (ringRadius / 500f).ToString(CultureInfo.InvariantCulture) + ",\"y\": " + (ringRadius / 500f).ToString(CultureInfo.InvariantCulture) + "},\n";
        }
        finalJson += "            \"invisibleWhenHidden\": true}\n";
        finalJson += "        ]\n";
        finalJson += "    }\n";
        finalJson += "},\n";
        finalJson += "\"Volumes\": {\n";
        finalJson += "    \"revealVolumes\": [\n";
        finalJson += "        {\"radius\": " + (1.2f * (ringRadius > 0 ? ringRadius : radius)).ToString(CultureInfo.InvariantCulture) + ",\n";
        finalJson += "        \"reveals\": [\"VAMBOK.NOMAISKY_" + solarSystem.ToUpper() + "_" + planetName.Replace(' ', '_').ToUpper() + "\"]}\n";
        finalJson += "    ]\n";
        finalJson += "},\n";
        AssetsMaker(relativePath, solarSystem, planetName, characteristics);
        return finalJson + "\"MapMarker\": {\"enabled\": true}\n}";
    }
    void SpriteGenerator(string mode, string path) { SpriteGenerator(mode, path, 0, 0, 0); }
    void SpriteGenerator(string mode, string path, byte colorR, byte colorG, byte colorB, byte colorA = 255, byte[] ringData = null) {
        path = Path.Combine(ModHelper.Manifest.ModFolderPath, path);
        int width, height;
        byte[] data;
        switch(mode) {
        case "star":
            width = height = 128;
            data = new byte[65536];
            for(int i = 127;i >= 0;i--) {
                for(int j = 127;j >= 0;j--) {
                    float radial = (i / 64f - 1) * (i / 64f - 1) + (j / 64f - 1) * (j / 64f - 1) + 0.1f;
                    if(radial < 1) {
                        data[i * 512 + j * 4 + 3] = colorA;
                        data[i * 512 + j * 4 + 2] = (byte)(colorB / (0.9f + radial));
                        data[i * 512 + j * 4 + 1] = (byte)(colorG / (0.9f + radial));
                        data[i * 512 + j * 4] = (byte)(colorR / (0.9f + radial));
                    } else if(radial < 1.1f) {
                        data[i * 512 + j * 4 + 3] = colorA;
                        data[i * 512 + j * 4 + 2] = (byte)((colorB + 255) / 2);
                        data[i * 512 + j * 4 + 1] = (byte)((colorG + 255) / 2);
                        data[i * 512 + j * 4] = (byte)((colorR + 255) / 2);
                    } else {
                        data[i * 512 + j * 4] = data[i * 512 + j * 4 + 1] = data[i * 512 + j * 4 + 2] = data[i * 512 + j * 4 + 3] = 0;
                    }
                }
            }
            break;
        case "planet":
            width = height = 128;
            data = new byte[65536];
            for(int i = 127;i >= 0;i--) {
                for(int j = 127;j >= 0;j--) {
                    if((i / 64f - 1) * (i / 64f - 1) + (j / 64f - 1) * (j / 64f - 1) < 1) {
                        data[i * 512 + j * 4 + 3] = colorA;
                        data[i * 512 + j * 4 + 2] = colorB;
                        data[i * 512 + j * 4 + 1] = colorG;
                        data[i * 512 + j * 4] = colorR;
                    } else {
                        data[i * 512 + j * 4] = data[i * 512 + j * 4 + 1] = data[i * 512 + j * 4 + 2] = data[i * 512 + j * 4 + 3] = 0;
                    }
                }
            }
            break;
        case "atmosphere":
            width = height = 128;
            data = new byte[65536];
            for(int i = 127;i >= 0;i--) {
                for(int j = 127;j >= 0;j--) {
                    float radial = (i / 64f - 1) * (i / 64f - 1) + (j / 64f - 1) * (j / 64f - 1);
                    if(radial < 0.97f) {
                        data[i * 512 + j * 4 + 3] = colorA;
                        data[i * 512 + j * 4 + 2] = colorB;
                        data[i * 512 + j * 4 + 1] = colorG;
                        data[i * 512 + j * 4] = colorR;
                    } else if(radial < 1) {
                        data[i * 512 + j * 4 + 3] = colorA;
                        data[i * 512 + j * 4 + 2] = (byte)((colorB + 255) / 2);
                        data[i * 512 + j * 4 + 1] = (byte)((colorG + 255) / 2);
                        data[i * 512 + j * 4] = (byte)((colorR + 255) / 2);
                    } else {
                        data[i * 512 + j * 4] = data[i * 512 + j * 4 + 1] = data[i * 512 + j * 4 + 2] = data[i * 512 + j * 4 + 3] = 0;
                    }
                }
            }
            break;
        case "map_rings":
            width = height = 256;
            data = new byte[262144];
            for(int i = 255;i >= 0;i--) {
                for(int j = 255;j >= 0;j--) {
                    data[i * 1024 + j * 4 + 3] = ringData[Mathf.Min(Mathf.FloorToInt(Mathf.Sqrt((i - 128) * (i - 128) + (j - 128) * (j - 128))), 128)];
                    data[i * 1024 + j * 4 + 2] = colorB;
                    data[i * 1024 + j * 4 + 1] = colorG;
                    data[i * 1024 + j * 4] = colorR;
                }
            }
            break;
        case "rings":
            width = 1;
            height = 1024;
            byte[] ringDataM = new byte[129];
            ringDataM.Clear();
            data = new byte[4096];
            for(int i = 0;i < 1024;i++) {//invert if inner top
                if(Random128.Rng.Range(0, 205) == 0) {//205 to get around 5 changes (tweakable)
                    colorA = (byte)Random128.Rng.Range(0, 256);
                }
                if(i % Mathf.CeilToInt(1024 / ringData[0]) == 0) {
                    ringDataM[128 - ringData[0] + i / Mathf.CeilToInt(1024 / ringData[0])] = colorA;
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
        string finalXml = "<AstroObjectEntry xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"https://raw.githubusercontent.com/Outer-Wilds-New-Horizons/new-horizons/main/NewHorizons/Schemas/shiplog_schema.xsd\">\n";
        finalXml += "<ID>" + planetName.Replace(' ', '_').ToUpper() + "</ID>\n<Entry>\n<ID>ENTRY_" + planetName.Replace(' ', '_').ToUpper() + "</ID>\n<Name>" + planetName + "</Name>\n";
        finalXml += "<ExploreFact>\n<ID>VAMBOK.NOMAISKY_" + starName.ToUpper() + "_" + planetName.Replace(' ', '_').ToUpper() + "</ID>\n";
        finalXml += "<Text>" + characteristics + "</Text>\n";
        finalXml += "</ExploreFact>\n</Entry>\n</AstroObjectEntry>";
        using StreamWriter outputFile = new(path + "/shiplogs.xml");
        outputFile.Write(finalXml);
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

        if(Random128.Rng.Range(0, 2) == 0) {
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
            x2 = mean + x1 * Mathf.Sqrt(-2.0f * Mathf.Log(x2) / x2) * sigma;
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
            _ => "red",
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
        for(int i = (a + b + c + d) % 8 + 4;i > 0;i--) {
            Rng.NextStream().NextUInt();
        }
    }
    public Random128(int seed0, int seed1, int seed2, int seed3) {
        streams[0] = new Xorshift32((uint)seed0);
        streams[1] = new Xorshift32((uint)seed1);
        streams[2] = new Xorshift32((uint)seed2);
        streams[3] = new Xorshift32((uint)seed3);
    }
    private ref Xorshift32 NextStream() {
        cursor = (cursor + 1) % 4;
        return ref streams[cursor];
    }
    public int Range(int minInclusive, int maxExclusive) { return NextStream().Range(minInclusive, maxExclusive); }
    public float Range(float minInclusive, float maxExclusive) { return NextStream().Range(minInclusive, maxExclusive); }
    public bool RandomBool() { return NextStream().RandomBool(); }
}

[HarmonyPatch]
public class MyPatchClass {
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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TimeLoop), nameof(TimeLoop.Start))]
    static void Start_Postfix() {
        TimeLoop._isTimeFlowing = false;
    }
}