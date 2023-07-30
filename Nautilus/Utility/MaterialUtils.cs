using Nautilus.Utility.MaterialModifiers;
using System.Collections;
using UnityEngine;
using UWE;

namespace Nautilus.Utility;

/// <summary>
/// Utilities related to Materials and Shaders.
/// </summary>
public static partial class MaterialUtils
{
    private static readonly int _emissionMap = Shader.PropertyToID("_EmissionMap");

    internal static void Patch()
    {
        CoroutineHost.StartCoroutine(LoadReferences());
    }

    private static IEnumerator LoadReferences()
    {
        yield return LoadMarmosetUBER();
#if SUBNAUTICA
        yield return PatchInternal();
#endif
        IsReady = true;
    }

    // Shader.Find("MarmosetUBER") returns the wrong shader, so we need to get it in this way:
    private static IEnumerator LoadMarmosetUBER()
    {
        var titaniumTask = PrefabDatabase.GetPrefabAsync("c66b5dfa-7fe9-4688-b165-d2e2f4caa8d9");
        yield return titaniumTask;
        titaniumTask.TryGetPrefab(out var titaniumPrefab);
        Shaders.MarmosetUBER = titaniumPrefab.GetComponentInChildren<Renderer>().material.shader;
    }

    /// <summary>
    /// Only returns true once all relevant materials/shaders have been loaded by the MaterialUtils class and are safe to be used.
    /// </summary>
    public static bool IsReady { get; private set; }

    /// <summary>
    /// Contains references to various Shaders.
    /// </summary>
    public static class Shaders
    {
        private static Shader _marmosetUber;

        /// <summary>
        /// The <see cref="Shader"/> that is used for most materials in the game.
        /// </summary>
        public static Shader MarmosetUBER
        {
            get
            {
                if (_marmosetUber == null)
                {
                    InternalLogger.Warn("Attempting to access the MaterialUtils.Shaders.MarmosetUBER property before it is ready! " +
                        "Consider delaying your logic until Nautilus.Utility.MaterialUtils.IsReady equals true.");
                }
                return _marmosetUber;
            }
            internal set
            {
                _marmosetUber = value;
            }
        }

        private static Shader _particlesUber;

        /// <summary>
        /// The <see cref="Shader"/> that is used for most particle systems.
        /// </summary>
        public static Shader ParticlesUBER
        {
            get
            {
                if (_particlesUber == null)
                {
                    _particlesUber = Shader.Find("UWE/Particles/UBER");
                }
                return _particlesUber;
            }
        }

        private static Shader _ionCube;

        /// <summary>
        /// The <see cref="Shader"/> that is used for Ion Cubes.
        /// </summary>
        public static Shader IonCube
        {
            get
            {
                if (_ionCube == null)
                {
                    _ionCube = Shader.Find("UWE/Marmoset/IonCrystal");
                }
                return _ionCube;
            }
        }
    }

    /// <summary>
    /// Enum for some generic types of Materials.
    /// </summary>
    public enum MaterialType
    {
        /// <summary>
        /// Solid material with no transparency.
        /// </summary>
        Opaque,
        /// <summary>
        /// See-through material.
        /// </summary>
        Transparent,
        /// <summary>
        /// Transparent pixels on the texture are not renderered. Useful for decals.
        /// </summary>
        Cutout
    }

    /// <summary>
    /// Render queue used by opaque objects.
    /// </summary>
    public const int kOpaqueRenderQueue = 2000;
    /// <summary>
    /// Render queue used by transparent objects.
    /// </summary>
    public const int kTransparencyRenderQueue = 3101;

    /// <summary>
    /// <para>Applies the necessary settings for Subnautica's standard shader (<see cref="Shaders.MarmosetUBER"/>) to the passed <see cref="GameObject"/>.</para>
    /// <para><b>The specific changes to each material are influenced by certain (case-insensitive) keywords in their asset names:</b>
    /// <br/>"TRANSPARENT": Enables transparency.
    /// <br/>"CUTOUT": Enables alpha clipping.</para>
    /// </summary>
    /// <param name="gameObject">The <see cref="GameObject"/> to apply the shaders to (includes all children).</param>
    /// <param name="shininess">'_Shininess' value of the shader. Recommended range of 1.0f-8.0f.</param>
    /// <param name="specularIntensity">'_SpecularInt' value of the shader. Values around 1f are recommended.</param>
    /// <param name="glowStrength">'_GlowStrength' and '_GlowStrengthNight' value of the shader. Should not be absurdly high.</param>
    /// <param name="modifiers">Optional array of classes that inherit from the <see cref="MaterialModifier"/> class. This allows for extensive customization of the method. Called in ascending order on every material.</param>
    public static void ApplySNShaders(GameObject gameObject, float shininess = 4f, float specularIntensity = 1f, float glowStrength = 1f, params MaterialModifier[] modifiers)
    {
        var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            for (var j = 0; j < renderers[i].materials.Length; j++)
            {
                var material = renderers[i].materials[j];

                var matNameLower = material.name.ToLower();
                bool transparent = matNameLower.Contains("transparent");
                bool alphaClip = matNameLower.Contains("cutout");

                var materialType = MaterialType.Opaque;
                if (transparent)
                    materialType = MaterialType.Transparent;
                else if (alphaClip)
                    materialType = MaterialType.Cutout;

                // check each material modifier first. determine if any want to block the shader conversion
                bool blockShaderConversion = false;
                if (modifiers != null)
                {
                    foreach (var modifier in modifiers)
                    {
                        if (modifier.BlockShaderConversion(material, renderers[i], materialType))
                        {
                            blockShaderConversion = true;
                        }
                    }
                }

                // apply the shader conversions, if possible
                if (!blockShaderConversion)
                {
                    ApplyUBERShader(material, shininess, specularIntensity, glowStrength, materialType);
                }

                // apply all modifiers
                if (modifiers != null)
                {
                    foreach (var modifier in modifiers)
                    {
                        modifier.EditMaterial(material, renderers[i], j, materialType);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Applies the <see cref="Shaders.MarmosetUBER"/> Shader to the given material.
    /// </summary>
    /// <param name="material">The material to apply changes to.</param>
    /// <param name="shininess">'_Shininess' value of the shader. Recommended range of 1.0f-8.0f.</param>
    /// <param name="specularIntensity">'_SpecularInt' value of the shader. Values around 1f are recommended.</param>
    /// <param name="glowStrength">'_GlowStrength' and '_GlowStrengthNight' value of the shader. Should not be absurdly high.</param>
    /// <param name="materialType">Controls various settings including alpha clipping and transparency.</param>
    public static void ApplyUBERShader(Material material, float shininess, float specularIntensity, float glowStrength, MaterialType materialType)
    {
        var specularTexture = material.HasProperty(ShaderPropertyID._SpecGlossMap) ? material.GetTexture(ShaderPropertyID._SpecGlossMap) : null;
        var emissionTexture = material.HasProperty(_emissionMap) ? material.GetTexture(_emissionMap) : null;
        var emissionColor = material.GetColor(ShaderPropertyID._EmissionColor);
        material.shader = Shaders.MarmosetUBER;

        material.DisableKeyword("_SPECGLOSSMAP");
        material.DisableKeyword("_NORMALMAP");
        if (specularTexture != null)
        {
            material.SetTexture(ShaderPropertyID._SpecTex, specularTexture);
        }
        material.SetFloat("_SpecInt", specularIntensity);
        material.SetFloat("_Shininess", shininess);
        material.EnableKeyword("_ZWRITE_ON");
        material.EnableKeyword("MARMO_SPECMAP");
        material.enableInstancing = true;
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack | MaterialGlobalIlluminationFlags.RealtimeEmissive;
        material.SetColor(ShaderPropertyID._SpecColor, new Color(1f, 1f, 1f, 1f));
        material.SetFloat("_Fresnel", 0.24f);
        material.SetVector("_SpecTex_ST", new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
        if (material.IsKeywordEnabled("_EMISSION"))
        {
            material.EnableKeyword("MARMO_EMISSION");
            material.SetFloat(ShaderPropertyID._EnableGlow, 1f);
            material.SetTexture(ShaderPropertyID._Illum, emissionTexture);
            material.SetColor(ShaderPropertyID._GlowColor, emissionColor);
            material.SetFloat(ShaderPropertyID._GlowStrength, glowStrength);
            material.SetFloat(ShaderPropertyID._GlowStrengthNight, glowStrength);
        }

        if (material.GetTexture("_BumpMap"))
        {
            material.EnableKeyword("MARMO_NORMALMAP");
        }

        switch (materialType)
        {
            case MaterialType.Transparent:
                SetMaterialCutout(material, false);
                SetMaterialTransparent(material, true);
                break;
            case MaterialType.Cutout:
                SetMaterialTransparent(material, false);
                SetMaterialCutout(material, true);
                break;
            default:
                SetMaterialTransparent(material, false);
                SetMaterialCutout(material, false);
                break;
        }
    }

    /// <summary>
    /// Toggle the transparency on a material.
    /// </summary>
    /// <param name="material"></param>
    /// <param name="transparent"></param>
    /// <returns></returns>
    public static void SetMaterialTransparent(Material material, bool transparent)
    {
        if (transparent)
        {
            material.EnableKeyword("WBOIT");
            material.SetInt(ShaderPropertyID._ZWrite, 0);
            material.SetInt(ShaderPropertyID._Cutoff, 0);
            material.SetFloat(ShaderPropertyID._SrcBlend, 1f);
            material.SetFloat(ShaderPropertyID._DstBlend, 1f);
            material.SetFloat(ShaderPropertyID._SrcBlend2, 0f);
            material.SetFloat(ShaderPropertyID._DstBlend2, 10f);
            material.SetFloat(ShaderPropertyID._AddSrcBlend, 1f);
            material.SetFloat(ShaderPropertyID._AddDstBlend, 1f);
            material.SetFloat(ShaderPropertyID._AddSrcBlend2, 0f);
            material.SetFloat(ShaderPropertyID._AddDstBlend2, 10f);
        }
        else
        {
            material.SetInt(ShaderPropertyID._ZWrite, 1);
            material.SetFloat(ShaderPropertyID._SrcBlend, 1f);
            material.SetFloat(ShaderPropertyID._DstBlend, 0f);
            material.SetFloat(ShaderPropertyID._SrcBlend2, 1f);
            material.SetFloat(ShaderPropertyID._DstBlend2, 0f);
            material.SetFloat(ShaderPropertyID._AddSrcBlend, 1f);
            material.SetFloat(ShaderPropertyID._AddDstBlend, 0f);
            material.SetFloat(ShaderPropertyID._AddSrcBlend2, 1f);
            material.SetFloat(ShaderPropertyID._AddDstBlend2, 0f);
            material.DisableKeyword("WBOIT");
        }
        material.renderQueue = transparent ? kTransparencyRenderQueue : kOpaqueRenderQueue;
    }

    /// <summary>
    /// Toggle alpha clipping on a material. Incompatbile with transparency.
    /// </summary>
    /// <param name="material"></param>
    /// <param name="cutout"></param>
    /// <returns></returns>
    public static void SetMaterialCutout(Material material, bool cutout)
    {
        if (cutout)
        {
            material.EnableKeyword("MARMO_ALPHA_CLIP");
        }
        else
        {
            material.DisableKeyword("MARMO_ALPHA_CLIP");
        }
    }
}
