using System;
using Microsoft.Xna.Framework.Graphics;

namespace TSMapEditor.Rendering;

public static class EffectExtensions
{
    /// <summary>
    /// stride-2048 fork / DesktopGL: finds a texture effect parameter by name across both MonoGame
    /// backends. The WindowsDX backend exposes a texture parameter under the texture variable's name
    /// (e.g. "PaletteTexture"). The DesktopGL / MojoShader backend exposes it under a combined
    /// "SamplerName+TextureName" name (e.g. "PaletteSampler+PaletteTexture"). Looking a texture
    /// parameter up by the bare texture name therefore returns null on DesktopGL, which previously
    /// caused a NullReferenceException on the first map render.
    /// </summary>
    public static EffectParameter FindTextureParameter(this Effect effect, string textureName)
    {
        EffectParameter parameter = effect.Parameters[textureName];
        if (parameter != null)
            return parameter;

        foreach (EffectParameter p in effect.Parameters)
        {
            if (p.Name.EndsWith("+" + textureName, StringComparison.Ordinal))
                return p;
        }

        return null;
    }
}
