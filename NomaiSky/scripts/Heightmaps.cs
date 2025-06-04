using System;//
using System.IO;//
using UnityEngine;//

static class Heightmaps
{
    static float radius;
    static readonly int baseRes = 204;// = heightmap height = heightmap width / 2
    //static readonly Stopwatch timer = new();
    static (int, int, byte) SetVertex(int x, int y, int z, int hmHeight)
    {
        int hmWidth = hmHeight * 2;
        Vector3 v2 = (new Vector3(x, y, z) - Vector3.one * hmHeight / 8f).normalized;
        float x2 = v2.x * v2.x, y2 = v2.y * v2.y, z2 = v2.z * v2.z;
        Vector3 v = new(v2.x * Mathf.Sqrt(1f - y2 / 2f - z2 / 2f + y2 * z2 / 3f), v2.y * Mathf.Sqrt(1f - x2 / 2f - z2 / 2f + x2 * z2 / 3f), v2.z * Mathf.Sqrt(1f - x2 / 2f - y2 / 2f + x2 * y2 / 3f));
        float dist = Mathf.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        float longitude = Mathf.Rad2Deg * Mathf.Atan2(v.z, v.x);
        float latitude = Mathf.Rad2Deg * Mathf.Acos(-v.y / dist);
        float sampleX = hmWidth * longitude / 360f;
        if (sampleX > hmWidth) sampleX -= hmWidth;
        if (sampleX < 0) sampleX += hmWidth;
        return ((int)sampleX, (int)(hmHeight * latitude / 180f), HeightGenerator(v.normalized * radius));
    }

    public static void CreateHeightmap(string path, float planetRadius, Color32 color, string parameter = "")
    {
        radius = planetRadius / 10;//tweak this till frequences are great
        int hmWidth = baseRes * 2;
        int resolution = baseRes / 4;
        Texture2D tex = new(hmWidth, baseRes, TextureFormat.RGBA32, false);
        int tX, tY; byte hValue;
        byte[] data = new byte[baseRes * hmWidth];
        Array.Fill<byte>(data, 100);

        //timer.Reset(); //TEST
        //Random128.Initialize(1, 5463, 64875, 215);for(int jj = 0;jj < 10;jj++) { //TEST
        if(parameter != "") NomaiSky.Random128.Rng.Start(parameter);
        perm = NomaiSky.Random128.Rng.GeneratePermutations();

        for (int x = 0; x <= resolution; x++)
        {
            for (int y = 0; y <= resolution; y++)
            {
                (tX, tY, hValue) = SetVertex(x, y, 0, baseRes);
                data[tX + tY * hmWidth] = hValue;
                (tX, tY, hValue) = SetVertex(x, y, resolution, baseRes);
                data[tX + tY * hmWidth] = hValue;
            }
        }
        for (int x = 1; x < resolution; x++)
        {
            for (int y = 0; y <= resolution; y++)
            {
                (tX, tY, hValue) = SetVertex(0, y, x, baseRes);
                data[tX + tY * hmWidth] = hValue;
                (tX, tY, hValue) = SetVertex(resolution, y, x, baseRes);
                data[tX + tY * hmWidth] = hValue;
            }
        }
        for (int x = 1; x < resolution; x++)
        {
            for (int y = 1; y < resolution; y++)
            {
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
        byte[] dataTex = new byte[baseRes * hmWidth * 4];
        byte[] finalData = new byte[baseRes * hmWidth * 4];
        for (int i = 0; i < baseRes * hmWidth; i++)
        {
            finalData[i * 4] = data[i];
            finalData[i * 4 + 1] = data[i];
            finalData[i * 4 + 2] = data[i];
            finalData[i * 4 + 3] = 255;
            dataTex[i * 4 + 0] = color.r;
            dataTex[i * 4 + 1] = color.g;
            dataTex[i * 4 + 2] = color.b;
            dataTex[i * 4 + 3] = 255;
        }
        tex.SetPixelData(finalData, 0);
        tex.Apply();
        File.WriteAllBytes(path + "heightmap.png", ImageConversion.EncodeToPNG(tex));
        //Create texture too:
        tex.SetPixelData(dataTex, 0);
        tex.Apply();
        File.WriteAllBytes(path + "texture.png", ImageConversion.EncodeToPNG(tex));
        //File.WriteAllBytes(path + "0-" + jj + ".png", ImageConversion.EncodeToPNG(tex));} //TEST
        UnityEngine.Object.Destroy(tex);
        //return timer.ElapsedTicks + " (" + timer.ElapsedMilliseconds + "ms)"; //TEST
    }
    static byte HeightGenerator(Vector3 position)
    {
        float result, clamp;
        //timer.Start(); //TEST
        result = Noise("large_details", position, -200, 300);
        if ((clamp = Clamp("cubed_mountains", position)) > 0.001f) { result += clamp * (Noise("mountains", position, 0, 750) + Noise("cellular_mountains", position, -1500, 1500)); } //reduced from -2500 2500
        if ((clamp = Clamp("cubed_plateaus", position)) > 0.001f) { result += clamp * (Noise("tall_plateaus", position, 0, 150) + Noise("short_plateaus", position, 0, 75)); }
        //result += Noise("small_details", position, 0, 10); //too small to see
        //timer.Stop(); //TEST
        return (byte)((result + 1700) * 256 / 4475);//(result - sumLows) * 256 / (sumHighs - sumLows)
    }
    static float Clamp(string type, Vector3 position)
    {
        float result;
        switch (type)
        {
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
    static float Noise(string type, Vector3 position, int low, int high)
    {
        float result;
        switch (type)
        {
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
    static float Noise(Vector3 position, int octaves, float frequency, float persistence, string type = null)
    {
        float total = 0;
        float maxAmplitude = 0;
        float amplitude = 1;
        Func<float, float> NoiseFuction = type switch
        {
            "Cellular_Squared" => frq => CellularSquared(position * frq),
            "Ridged_Snoise" => frq => 1 - 2 * Mathf.Abs(Snoise(position * frq)),
            _ => frq => Snoise(position * frq)
        };
        for (int i = 0; i < octaves; i++)
        {
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
    static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;     // Convert low 4 bits of hash code into 12 simple
        float u = h < 8 ? x : y; // gradient directions, and compute dot product.
        float v = h < 4 ? y : h == 12 || h == 14 ? x : z; // Fix repeats at h = 12 to 15
        return ((h & 1) > 0 ? -u : u) + ((h & 2) > 0 ? -v : v);
    }
    /// <summary>3D simplex noise.</summary>
    static float Snoise(Vector3 P)
    {
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
        if (x0 >= y0)
        {
            if (y0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; } // X Y Z order
            else if (x0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; } // X Z Y order
            else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; } // Z X Y order
        }
        else
        { // x0<y0
            if (y0 < z0) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; } // Z Y X order
            else if (x0 < z0) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; } // Y Z X order
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
        if (t0 < 0) n0 = 0;
        else
        {
            t0 *= t0;
            n0 = t0 * t0 * Grad(perm[ii + perm[jj + perm[kk]]], x0, y0, z0);
        }
        float t1 = 0.6f - x1 * x1 - y1 * y1 - z1 * z1;
        if (t1 < 0) n1 = 0;
        else
        {
            t1 *= t1;
            n1 = t1 * t1 * Grad(perm[ii + i1 + perm[jj + j1 + perm[kk + k1]]], x1, y1, z1);
        }
        float t2 = 0.6f - x2 * x2 - y2 * y2 - z2 * z2;
        if (t2 < 0) n2 = 0;
        else
        {
            t2 *= t2;
            n2 = t2 * t2 * Grad(perm[ii + i2 + perm[jj + j2 + perm[kk + k2]]], x2, y2, z2);
        }
        float t3 = 0.6f - x3 * x3 - y3 * y3 - z3 * z3;
        if (t3 < 0) n3 = 0;
        else
        {
            t3 *= t3;
            n3 = t3 * t3 * Grad(perm[ii + 1 + perm[jj + 1 + perm[kk + 1]]], x3, y3, z3);
        }
        // Add contributions from each corner to get the final noise value.
        // The result is scaled to stay just inside [-1,1]
        return 32 * (n0 + n1 + n2 + n3); // TODO: The scale factor is preliminary!
    }

    static float CellularSquared(Vector3 P)
    {
        Vector2 tmp = NewCellular(P);
        tmp.y -= tmp.x;
        return tmp.y * tmp.y;
    }
    /// <summary>Vector floor to int, component-wise.</summary>
    static Vector3Int FloorToInt(Vector3 a) => new(Mathf.FloorToInt(a.x), Mathf.FloorToInt(a.y), Mathf.FloorToInt(a.z));
    static Vector3 GetCellJitter(Vector3Int coord)
    {
        // Create 3 separate hashes to avoid repeated values across axes
        return new Vector3(
            perm[(coord.x + perm[(coord.y + perm[coord.z & 255]) & 255]) & 255] / 256f - 0.5f,
            perm[(coord.x + 19 + perm[(coord.y + 73 + perm[(coord.z + 47) & 255]) & 255]) & 255] / 256f - 0.5f,
            perm[(coord.x + 131 + perm[(coord.y + 251 + perm[(coord.z + 7) & 255]) & 255]) & 255] / 256f - 0.5f
        );
    }
    static Vector2 NewCellular(Vector3 P)
    {
        float F1 = float.MaxValue, F2 = float.MaxValue;
        Vector3Int Pi = FloorToInt(P);
        Vector3 Pf = P - Pi;

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    float diff = (Pf - GetCellJitter(new Vector3Int(Pi.x + dx, Pi.y + dy, Pi.z + dz)) - new Vector3(dx, dy, dz)).sqrMagnitude;
                    if (diff < F1)
                    {
                        F2 = F1;
                        F1 = diff;
                    }
                    else if (diff < F2)
                    {
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