using System.Collections.Generic;
using System;

using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

using SharpCraft.Assets;
using SharpCraft.Handlers;
using SharpCraft.Utility;

namespace SharpCraft.World
{
    static class Cube
    {
        public static VertexPositionTextureLight[][] Faces { get; set; }

        static Cube()
        {
            float size = 0.5f;

            VertexPositionTextureLight[] front = new VertexPositionTextureLight[6];
            front[0].Position = new Vector3(-size, -size, size);
            front[1].Position = new Vector3(-size, size, size);
            front[2].Position = new Vector3(size, -size, size);
            front[3].Position = front[1].Position;
            front[4].Position = new Vector3(size, size, size);
            front[5].Position = front[2].Position;

            VertexPositionTextureLight[] back = new VertexPositionTextureLight[6];
            back[0].Position = new Vector3(size, -size, -size);
            back[1].Position = new Vector3(size, size, -size);
            back[2].Position = new Vector3(-size, -size, -size);
            back[3].Position = back[1].Position;
            back[4].Position = new Vector3(-size, size, -size);
            back[5].Position = back[2].Position;

            VertexPositionTextureLight[] top = new VertexPositionTextureLight[6];
            top[0].Position = new Vector3(-size, size, size);
            top[1].Position = new Vector3(-size, size, -size);
            top[2].Position = new Vector3(size, size, size);
            top[3].Position = top[1].Position;
            top[4].Position = new Vector3(size, size, -size);
            top[5].Position = top[2].Position;

            VertexPositionTextureLight[] bottom = new VertexPositionTextureLight[6];
            bottom[0].Position = new Vector3(-size, -size, -size);
            bottom[1].Position = new Vector3(-size, -size, size);
            bottom[2].Position = new Vector3(size, -size, -size);
            bottom[3].Position = bottom[1].Position;
            bottom[4].Position = new Vector3(size, -size, size);
            bottom[5].Position = bottom[2].Position;

            VertexPositionTextureLight[] right = new VertexPositionTextureLight[6];
            right[0].Position = new Vector3(size, -size, size);
            right[1].Position = new Vector3(size, size, size);
            right[2].Position = new Vector3(size, -size, -size);
            right[3].Position = right[1].Position;
            right[4].Position = new Vector3(size, size, -size);
            right[5].Position = right[2].Position;

            VertexPositionTextureLight[] left = new VertexPositionTextureLight[6];
            left[0].Position = new Vector3(-size, -size, -size);
            left[1].Position = new Vector3(-size, size, -size);
            left[2].Position = new Vector3(-size, -size, size);
            left[3].Position = left[1].Position;
            left[4].Position = new Vector3(-size, size, size);
            left[5].Position = left[2].Position;

            Faces = [front, back, top, bottom, right, left];

            for (int i = 0; i < 6; i++)
            {
                Faces[i][0].TextureCoordinate = new Vector2(1, 1);
                Faces[i][1].TextureCoordinate = new Vector2(1, 0);
                Faces[i][2].TextureCoordinate = new Vector2(0, 1);
                Faces[i][3].TextureCoordinate = Faces[i][1].TextureCoordinate;
                Faces[i][4].TextureCoordinate = new Vector2(0, 0);
                Faces[i][5].TextureCoordinate = Faces[i][2].TextureCoordinate;
            }
        }
    }

    class RegionRenderer
    {
        readonly GraphicsDevice graphics;
        readonly BlockMetadataProvider blockMetadata;
        readonly ScreenshotHandler screenshotHandler;
        readonly BlockSelector blockSelector;
        readonly LightSystem lightSystem;

        readonly Texture2D atlas;
        readonly Effect effect;
        readonly DynamicVertexBuffer buffer;
        readonly Dictionary<Vector3I, VertexPositionTextureLight[]> verticesCache = [];
        readonly Dictionary<Vector3I, VertexPositionTextureLight[]> transparentVerticesCache = [];

        readonly int blockCount;

        public RegionRenderer(GraphicsDevice graphics,
            ScreenshotHandler screenshotTaker, BlockSelector blockSelector,
            AssetServer assetServer, BlockMetadataProvider blockMetadata, LightSystem lightSystem)
        {
            this.graphics = graphics;
            this.blockMetadata = blockMetadata;
            screenshotHandler = screenshotTaker;
            this.blockSelector = blockSelector;
            this.lightSystem = lightSystem;

            blockCount = blockMetadata.BlockCount;

            effect = assetServer.GetEffect;

            blockSelector.SetEffect(effect);

            atlas = new Texture2D(graphics, 64, blockMetadata.BlockCount * 64);
            Color[] atlasData = new Color[atlas.Width * atlas.Height];

            for (int i = 0; i < blockMetadata.BlockCount; i++)
            {
                Color[] textureColor = new Color[64 * 64];
                assetServer.GetBlockTexture((ushort)i).GetData(textureColor);

                for (int j = 0; j < textureColor.Length; j++)
                {
                    atlasData[(i * textureColor.Length) + j] = textureColor[j];
                }
            }
            atlas.SetData(atlasData);

            buffer = new DynamicVertexBuffer(graphics, typeof(VertexPositionTextureLight),
                        (int)2e4, BufferUsage.WriteOnly);
        }

        public void Add(ChunkNeighbors neighbors)
        {
            var (vertices, transparentVertices) = CalculateMesh(neighbors);
            verticesCache.Add(neighbors.Chunk.Index, vertices);
            transparentVerticesCache.Add(neighbors.Chunk.Index, transparentVertices);
        }

        public void Update(ChunkNeighbors neighbors)
        {
            var (vertices, transparentVertices) = CalculateMesh(neighbors);
            verticesCache[neighbors.Chunk.Index] = vertices;
            transparentVerticesCache[neighbors.Chunk.Index] = transparentVertices;
        }

        public void Remove(Vector3I index)
        {
            verticesCache.Remove(index);
            transparentVerticesCache.Remove(index);
        }

        public void Render(IEnumerable<Chunk> chunks, Player player, Time time)
        {
            Vector3 chunkMax = new(Chunk.Size);

            float lightIntensity = time.LightIntensity;

            HashSet<Chunk> visibleChunks = [];

            effect.Parameters["World"].SetValue(Matrix.Identity);
            effect.Parameters["View"].SetValue(player.Camera.View);
            effect.Parameters["Projection"].SetValue(player.Camera.Projection);
            effect.Parameters["Texture"].SetValue(atlas);
            effect.Parameters["LightIntensity"].SetValue(lightIntensity);

            graphics.Clear(Color.Lerp(Color.SkyBlue, Color.Black, 1f - lightIntensity));

            //Drawing opaque blocks
            foreach (Chunk chunk in chunks)
            {
                if (!chunk.IsReady) continue;

                BoundingBox chunkBounds = new(chunk.Position, chunkMax + chunk.Position);

                bool isChunkVisible = false;
                if (player.Camera.Frustum.Intersects(chunkBounds))
                {
                    visibleChunks.Add(chunk);
                    isChunkVisible = true;
                }

                if (isChunkVisible && chunk.ActiveBlocksCount > 0)
                {
                    effect.Parameters["Alpha"].SetValue(1f);

                    var vertices = verticesCache[chunk.Index];

                    if (vertices.Length == 0) continue;

                    buffer.SetData(vertices);
                    graphics.SetVertexBuffer(buffer);

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        graphics.DrawPrimitives(PrimitiveType.TriangleList, 0, vertices.Length / 3);
                    }
                }
            }

            //Drawing transparent blocks
            foreach (Chunk chunk in visibleChunks)
            {
                var vertices = transparentVerticesCache[chunk.Index];
                if (vertices.Length == 0) continue;

                effect.Parameters["Alpha"].SetValue(0.7f);

                buffer.SetData(vertices);
                graphics.SetVertexBuffer(buffer);

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    graphics.DrawPrimitives(PrimitiveType.TriangleList, 0, vertices.Length / 3);
                }

            }

            if (screenshotHandler.TakeScreenshot)
            {
                screenshotHandler.Screenshot(DateTime.Now.ToString());
            }

            blockSelector.Draw();
        }

        public (VertexPositionTextureLight[], VertexPositionTextureLight[]) CalculateMesh(ChunkNeighbors neighbors)
        {
            Chunk chunk = neighbors.Chunk;

            List<VertexPositionTextureLight> vertices = [];
            List<VertexPositionTextureLight> transparentVertices = [];

            foreach (Vector3I index in chunk.GetActiveIndexes())
            {
                int x = index.X;
                int y = index.Y;
                int z = index.Z;

                Vector3 blockPosition = new Vector3(x, y, z) + chunk.Position;

                FacesState visibleFaces = chunk.GetVisibleFaces(y, x, z, neighbors);
                FacesData<LightValue> lightValues = lightSystem.GetFacesLight(visibleFaces, y, x, z, neighbors);

                foreach (Faces face in visibleFaces.GetFaces())
                {
                    ushort texture = chunk[x, y, z].Value;
                    LightValue light = lightValues.GetValue(face);

                    if (blockMetadata.IsBlockTransparent(texture))
                    {
                        var blockVerticesTransparent = GetBlockVertices(face, light, blockPosition, texture);
                        transparentVertices.AddRange(blockVerticesTransparent);
                    }
                    else
                    {
                        var blockVertices = GetBlockVertices(face, light, blockPosition,
                            blockMetadata.IsBlockMultiface(texture) ? blockMetadata.GetMultifaceBlockFace(texture, face) : texture);
                        vertices.AddRange(blockVertices);
                    }
                }
            }

            chunk.RecalculateMesh = false;

            return ([.. vertices], [.. transparentVertices]);
        }

        VertexPositionTextureLight[] GetBlockVertices(Faces face, LightValue light, Vector3 position, ushort texture)
        {
            const int nVertices = 6;
            VertexPositionTextureLight[] vertices = new VertexPositionTextureLight[nVertices];
            for (int i = 0; i < nVertices; i++)
            {
                VertexPositionTextureLight vertex = Cube.Faces[(byte)face][i];
                vertex.Position += position;

                int skylight = light.SkyValue;
                int blockLight = light.BlockValue;

                vertex.Light = skylight + 397 * blockLight;

                if (vertex.TextureCoordinate.Y == 0)
                {
                    vertex.TextureCoordinate.Y = (float)texture / blockCount;
                }
                else
                {
                    vertex.TextureCoordinate.Y = (float)(texture + 1) / blockCount;
                }

                vertices[i] = vertex;
            }

            return vertices;
        }
    }
}
