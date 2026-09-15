using System;
using Microsoft.Xna.Framework.Graphics;
using MapEditorLibrary.Graphics;

namespace TSMapEditor.Rendering;

/// <summary>
/// Wraps the full-map render targets used by <see cref="MapView"/>.
///
/// stride-2048 fork, renderer Step 1: today this is a thin wrapper around a single
/// <see cref="RenderTarget2D"/> that is the size of the whole map. It exists so that every place
/// that creates, sizes or disposes a full-map render target goes through one type. A later step
/// replaces the single backing texture with a lazily-resident grid of chunk textures so that maps
/// larger than <see cref="RenderingConstants.MaximumDX11TextureSize"/> pixels in a dimension can be
/// rendered (a single Direct3D 11 texture is capped at 16384 px).
///
/// The implicit conversion to <see cref="RenderTarget2D"/> keeps this step a pure, behavior-neutral
/// refactor: existing call sites (Renderer.PushRenderTarget / DrawTexture, GraphicsDevice.SetRenderTarget,
/// effect parameters, etc.) compile unchanged. When the backing store becomes chunked, that operator
/// is removed so the compiler surfaces every call site that still assumes a single texture, and those
/// are migrated to explicit chunk-aware draw/blit methods.
/// </summary>
public sealed class MapRenderTarget : IDisposable
{
    public MapRenderTarget(GraphicsDevice graphicsDevice, int width, int height,
        SurfaceFormat surfaceFormat, DepthFormat depthFormat)
    {
        Width = width;
        Height = height;
        SurfaceFormat = surfaceFormat;
        DepthFormat = depthFormat;

        renderTarget = new RenderTarget2D(graphicsDevice, width, height, false, surfaceFormat,
            depthFormat, 0, RenderTargetUsage.PreserveContents);
    }

    private RenderTarget2D renderTarget;

    /// <summary>The logical (whole-map) width of this render target in pixels.</summary>
    public int Width { get; }

    /// <summary>The logical (whole-map) height of this render target in pixels.</summary>
    public int Height { get; }

    public SurfaceFormat SurfaceFormat { get; }

    public DepthFormat DepthFormat { get; }

    /// <summary>
    /// The underlying render target. During the chunked-renderer migration this is the single
    /// backing texture; call sites that need the raw texture should prefer this over the implicit
    /// conversion so they are easy to find and migrate later.
    /// </summary>
    public RenderTarget2D RenderTarget => renderTarget;

    /// <summary>
    /// Transitional bridge so existing call sites keep compiling unchanged while the wrapper is
    /// introduced. Removed once the backing store becomes chunked.
    /// </summary>
    public static implicit operator RenderTarget2D(MapRenderTarget mapRenderTarget)
        => mapRenderTarget?.renderTarget;

    public void Dispose()
    {
        renderTarget?.Dispose();
        renderTarget = null;
    }
}
