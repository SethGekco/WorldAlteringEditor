using MapEditorLibrary.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace TSMapEditor.Rendering.Batching;

public class AbstractBatcher<T> where T : struct, IVertexType
{
    protected readonly GraphicsDevice _graphicsDevice;
    protected Effect _effect;

    /// <summary>
    /// Pool for created batches.
    /// Pooling these means that these big objects don't need to be reconstructed each time a batch is drawn.
    /// </summary>
    protected Queue<TextureBatch<T>> inactiveBatches = new Queue<TextureBatch<T>>();

    /// <summary>
    /// Batches that are currently queued for drawing.
    /// </summary>
    protected List<TextureBatch<T>> queuedBatches = new List<TextureBatch<T>>();

    protected readonly EffectParameter mainTextureParameter;
    protected readonly EffectParameter worldViewProjParameter;
    protected DepthStencilState depthStencilState;

    private bool beginCalled = false;

    public AbstractBatcher(GraphicsDevice graphicsDevice, Effect effect)
    {
        _graphicsDevice = graphicsDevice;
        _effect = effect;

        mainTextureParameter = _effect.FindTextureParameter("MainTexture");
        worldViewProjParameter = _effect.Parameters["WorldViewProj"];
    }

    public void Begin(Effect effect = null, DepthStencilState depthStencilState = null)
    {
        if (beginCalled)
            throw new InvalidOperationException("End needs to be called between two successive calls to Begin.");

        beginCalled = true;

        if (effect != null)
            _effect = effect;

        if (depthStencilState != null)
            this.depthStencilState = depthStencilState;
    }

    /// <summary>
    /// stride-2048 fork: world-space (map pixel) coordinate of the render target's top-left corner.
    /// Zero when the render target covers the whole map; the camera-follow origin when oversized
    /// maps are rendered with viewport-sized targets. Vertices are always in absolute map pixels,
    /// so the projection maps [origin, origin + targetSize] onto the target.
    /// </summary>
    public Microsoft.Xna.Framework.Point WorldRenderOrigin { get; set; }

    public void End()
    {
        // Build orthographic projection covering the world-space region this target represents
        Matrix projection = Matrix.CreateOrthographicOffCenter(
            WorldRenderOrigin.X, WorldRenderOrigin.X + _graphicsDevice.Viewport.Width,
            WorldRenderOrigin.Y + _graphicsDevice.Viewport.Height, WorldRenderOrigin.Y,
            0, -1);

        // Upload it to the shader
        worldViewProjParameter.SetValue(projection);

        _graphicsDevice.DepthStencilState = depthStencilState;

        // stride-2048 fork / DesktopGL: the paletted effects use two texture units (main tile + palette).
        // On OpenGL the palette sampler (unit 1) is left unbound / mis-sampled unless its sampler state
        // is set explicitly, producing grayscale terrain. Point sampling is also required so the palette
        // index lookup is not interpolated.
        _graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
        _graphicsDevice.SamplerStates[1] = SamplerState.PointClamp;

        for (int i = 0; i < queuedBatches.Count; i++)
        {
            var batch = queuedBatches[i];
            mainTextureParameter.SetValue(batch.Texture);

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    batch.Vertices, 0, batch.QuadCount * RenderingConstants.VerticesPerQuad,
                    batch.Indexes, 0, batch.QuadCount * RenderingConstants.TrianglesPerQuad,
                    batch.Vertices[0].VertexDeclaration
                );
            }
        }

        for (int i = 0; i < queuedBatches.Count; i++)
        {
            queuedBatches[i].Clear();
            inactiveBatches.Enqueue(queuedBatches[i]);
        }

        queuedBatches.Clear();
        beginCalled = false;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
    }
}
