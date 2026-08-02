using UnityEngine;

namespace TPBR
{
    /// Render-pipeline agnostic material helpers.
    /// The project has to look the same whether it is dropped into a Built-in RP
    /// project or a URP one, so every colour is written to both property names and
    /// shaders are looked up with fallbacks instead of being referenced as assets.
    public static class Mat
    {
        static Shader lit, unlit;

        public static Shader LitShader
        {
            get
            {
                if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) lit = Shader.Find("Standard");
                if (lit == null) lit = Shader.Find("Legacy Shaders/Diffuse");
                return lit;
            }
        }

        public static Shader UnlitShader
        {
            get
            {
                if (unlit == null) unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlit == null) unlit = Shader.Find("Unlit/Color");
                if (unlit == null) unlit = LitShader;
                return unlit;
            }
        }

        public static Material Lit(Color c, float smoothness = 0.15f, float metallic = 0f)
        {
            var m = new Material(LitShader);
            Tint(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return m;
        }

        public static Material Unlit(Color c)
        {
            var m = new Material(UnlitShader);
            Tint(m, c);
            return m;
        }

        public static Material Glow(Color c, float strength = 2f)
        {
            var m = Lit(c);
            Emissive(m, c * strength);
            return m;
        }

        /// Alpha-blended variant. Both pipelines need different switches flipped,
        /// so set everything that exists and let the missing ones no-op.
        public static Material Transparent(Color c, bool unlitShader = true)
        {
            var m = new Material(unlitShader ? UnlitShader : LitShader);
            Tint(m, c);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);           // URP: transparent
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3f);                 // Built-in: transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);               // URP: alpha
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        public static void Tint(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static void Emissive(Material m, Color c)
        {
            if (m == null) return;
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        public static void Alpha(Material m, float a)
        {
            if (m == null) return;
            Color c = Color.white;
            if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
            else if (m.HasProperty("_Color")) c = m.GetColor("_Color");
            c.a = a;
            Tint(m, c);
        }
    }

    /// Sixteen colours that stay distinct at a distance on a dark floor.
    public static class Palette
    {
        public static readonly Color[] Players =
        {
            new Color(0.95f, 0.26f, 0.26f), // 0  red
            new Color(0.30f, 0.62f, 1.00f), // 1  blue
            new Color(0.35f, 0.87f, 0.42f), // 2  green
            new Color(1.00f, 0.79f, 0.20f), // 3  gold
            new Color(0.76f, 0.42f, 0.98f), // 4  violet
            new Color(0.20f, 0.90f, 0.86f), // 5  cyan
            new Color(1.00f, 0.51f, 0.20f), // 6  orange
            new Color(0.98f, 0.44f, 0.75f), // 7  pink
            new Color(0.62f, 0.82f, 0.24f), // 8  lime
            new Color(0.45f, 0.48f, 0.95f), // 9  indigo
            new Color(0.92f, 0.92f, 0.86f), // 10 bone
            new Color(0.85f, 0.34f, 0.42f), // 11 rose
            new Color(0.24f, 0.71f, 0.60f), // 12 teal
            new Color(0.79f, 0.66f, 0.35f), // 13 sand
            new Color(0.58f, 0.36f, 0.80f), // 14 grape
            new Color(0.44f, 0.75f, 0.87f), // 15 sky
        };

        public static readonly Color Floor      = new Color(0.10f, 0.13f, 0.17f);
        public static readonly Color TileDark   = new Color(0.06f, 0.08f, 0.11f);
        public static readonly Color Lava       = new Color(1.00f, 0.34f, 0.08f);
        public static readonly Color LavaDeep   = new Color(0.55f, 0.09f, 0.02f);
        public static readonly Color Danger     = new Color(1.00f, 0.18f, 0.20f);
        public static readonly Color Safe       = new Color(0.30f, 1.00f, 0.55f);
        public static readonly Color Gold       = new Color(1.00f, 0.83f, 0.32f);
        public static readonly Color Ink        = new Color(0.05f, 0.06f, 0.09f);
        public static readonly Color Paper      = new Color(0.94f, 0.96f, 0.99f);

        public static Color Of(int playerIndex) => Players[playerIndex % Players.Length];

        public static Color Dim(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

        public static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);
    }
}
