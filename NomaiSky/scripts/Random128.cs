namespace NomaiSky;

public class Random128
{
    struct Xorshift32(uint seed) {
        uint firstState = seed != 0 ? seed : 0xCAFEBABE;
        uint state = seed != 0 ? seed : 0xCAFEBABE;
        public uint NextUInt(bool init = false)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            if(init) firstState = state;
            return state;
        }
        public int Range(int minInclusive, int maxExclusive) { return (minInclusive == maxExclusive ? minInclusive : (int)(NextUInt() % (uint)(maxExclusive - minInclusive)) + minInclusive); }
        public float Range(float minInclusive, float maxExclusive) { return minInclusive + ((maxExclusive - minInclusive) * NextUInt() / uint.MaxValue); }
        public bool RandomBool() { return (NextUInt() & 1) == 0; }
        public void InitState(uint id) { state = firstState + id; }
    }

    readonly Xorshift32[] streams = new Xorshift32[4];
    int firstCursor = 3;
    int cursor = 3;
    public static Random128 Rng { get; private set; }
    public static void Initialize(int a, int b, int c, int d) {
        Rng = new Random128(a, b, c, d);
        for (int i = (a + b + c + d) % 4 + 39; i > 0; i--)
        {
            Rng.NextStream().NextUInt();
        }
        Rng.NextStream().NextUInt(true);
        Rng.NextStream().NextUInt(true);
        Rng.NextStream().NextUInt(true);
        Rng.NextStream(true).NextUInt(true);//between 40 and 46 shuffles (at least 10 for each)
    }
    public Random128(int seed0, int seed1, int seed2, int seed3) {
        streams[0] = new Xorshift32((uint)seed0);
        streams[1] = new Xorshift32((uint)seed1);
        streams[2] = new Xorshift32((uint)seed2);
        streams[3] = new Xorshift32((uint)seed3);
    }
    ref Xorshift32 NextStream(bool init = false) {
        cursor = (cursor + 1) % 4;
        if(init) firstCursor = cursor;
        return ref streams[cursor];
    }
    public int Range(int minInclusive, int maxExclusive) { return NextStream().Range(minInclusive, maxExclusive); }
    public float Range(float minInclusive, float maxExclusive) { return NextStream().Range(minInclusive, maxExclusive); }
    public float Proba() { return NextStream().Range(0f, 1f); }
    public bool RandomBool() { return NextStream().RandomBool(); }
    public int Range(int minInclusive, int maxExclusive, string parameter) { Start(parameter); return NextStream().Range(minInclusive, maxExclusive); }
    public float Range(float minInclusive, float maxExclusive, string parameter) { Start(parameter); return NextStream().Range(minInclusive, maxExclusive); }
    public float Proba(string parameter) { Start(parameter); return NextStream().Range(0f, 1f); }
    public bool RandomBool(string parameter) { Start(parameter); return NextStream().RandomBool(); }
    public void Start(string parameter) {
        uint id = 0; int i = 0;
        foreach(char c in parameter) {
            id += (uint)((int)c switch { < 32 => 0, < 48 => 1, < 58 => c - 46, < 65 => 1, < 91 => c - 53, < 97 => 1, < 123 => c - 59, _ => 0 }) << (i % 5 * 6);
            i++;
        }
        streams[0].InitState(id);
        streams[1].InitState(id);
        streams[2].InitState(id);
        streams[3].InitState(id);
        cursor = firstCursor;
    }
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