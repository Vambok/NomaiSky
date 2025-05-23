using UnityEngine;

namespace NomaiSky;

public class MVBGalacticMap : MonoBehaviour
{
    public (int x, int y, int z) coords;
    public string mapName;
    public void Initializator((int, int, int) initCoords, string initMapName)
    {
        coords = initCoords;
        mapName = initMapName;
    }
}