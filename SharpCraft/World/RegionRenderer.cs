using System.Collections.Generic;
using System;

using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

using SharpCraft.Assets;
using SharpCraft.Utilities;
using SharpCraft.World.Light;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.Persistence;
using SharpCraft.MathUtilities;

namespace SharpCraft.World
{
    static class Cube
    {
        public static VertexPositionTextureLight[][] Faces { get; }

        static Cube()
        {
            // Front face: z = 1.
            VertexPositionTextureLight[] front = new VertexPositionTextureLight[6];
            front[0].Position = new Vector3(0, 0, 1);
            front[1].Position = new Vector3(0, 1, 1);
            front[2].Position = new Vector3(1, 0, 1);
            front[3].Position = front[1].Position;
            front[4].Position = new Vector3(1, 1, 1);
            front[5].Position = front[2].Position;

            // Back face: z = 0.
            VertexPositionTextureLight[] back = new VertexPositionTextureLight[6];
            back[0].Position = new Vector3(1, 0, 0);
            back[1].Position = new Vector3(1, 1, 0);
            back[2].Position = new Vector3(0, 0, 0);
            back[3].Position = back[1].Position;
            back[4].Position = new Vector3(0, 1, 0);
            back[5].Position = back[2].Position;

            // Top face: y = 1.
            VertexPositionTextureLight[] top = new VertexPositionTextureLight[6];
            top[0].Position = new Vector3(0, 1, 1);
            top[1].Position = new Vector3(0, 1, 0);
            top[2].Position = new Vector3(1, 1, 1);
            top[3].Position = top[1].Position;
            top[4].Position = new Vector3(1, 1, 0);
            top[5].Position = top[2].Position;

            // Bottom face: y = 0.
            VertexPositionTextureLight[] bottom = new VertexPositionTextureLight[6];
            bottom[0].Position = new Vector3(0, 0, 0);
            bottom[1].Position = new Vector3(0, 0, 1);
            bottom[2].Position = new Vector3(1, 0, 0);
            bottom[3].Position = bottom[1].Position;
            bottom[4].Position = new Vector3(1, 0, 1);
            bottom[5].Position = bottom[2].Position;

            // Right face: x = 1.
            VertexPositionTextureLight[] right = new VertexPositionTextureLight[6];
            right[0].Position = new Vector3(1, 0, 1);
            right[1].Position = new Vector3(1, 1, 1);
            right[2].Position = new Vector3(1, 0, 0);
            right[3].Position = right[1].Position;
            right[4].Position = new Vector3(1, 1, 0);
            right[5].Position = right[2].Position;

            // Left face: x = 0.
            VertexPositionTextureLight[] left = new VertexPositionTextureLight[6];
            left[0].Position = new Vector3(0, 0, 0);
            left[1].Position = new Vector3(0, 1, 0);
            left[2].Position = new Vector3(0, 0, 1);
            left[3].Position = left[1].Position;
            left[4].Position = new Vector3(0, 1, 1);
            left[5].Position = left[2].Position;


            Faces = [front, back, top, bottom, right, left];

            /*foreach (var face in Faces)
            {
                for (int i = 0; i < face.Length; i++)
                {
                    face[i].Position += new Vector3(halfSize);
                }
            }*/

            // Uv texture coordinates
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
        readonly ScreenshotTaker screenshotHandler;
        readonly BlockSelector blockSelector;
        readonly LightSystem lightSystem;

        readonly Texture2D atlas;
        readonly Effect effect;
        readonly DynamicVertexBuffer buffer;
        readonly Dictionary<Vector3I, VertexPositionTextureLight[]> verticesCache = [];
        readonly Dictionary<Vector3I, VertexPositionTextureLight[]> transparentVerticesCache = [];

        readonly int blockCount;

        public RegionRenderer(GraphicsDevice graphics,
            ScreenshotTaker screenshotTaker, BlockSelector blockSelector,
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

        public void AddMesh(ChunkAdjacency adjacency)
        {
            var (vertices, transparentVertices) = CalculateMesh(adjacency);
            verticesCache.Add(adjacency.Root.Index, vertices);
            transparentVerticesCache.Add(adjacency.Root.Index, transparentVertices);
        }

        public void Update(ChunkAdjacency adjacency)
        {
            var (vertices, transparentVertices) = CalculateMesh(adjacency);
            verticesCache[adjacency.Root.Index] = vertices;
            transparentVerticesCache[adjacency.Root.Index] = transparentVertices;
        }

        public void Remove(Vector3I index)
        {
            verticesCache.Remove(index);
            transparentVerticesCache.Remove(index);
        }

        public void Render(IEnumerable<IChunk> chunks, Player player, Time time)
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
            foreach (IChunk chunk in chunks)
            {
                if (chunk is not Chunk fullChunk || !fullChunk.IsReady) continue;

                BoundingBox chunkBounds = new(fullChunk.Position, chunkMax + fullChunk.Position);

                bool isChunkVisible = false;
                if (player.Camera.Frustum.Intersects(chunkBounds))
                {
                    visibleChunks.Add(fullChunk);
                    isChunkVisible = true;
                }

                if (isChunkVisible && fullChunk.ActiveBlocksCount > 0)
                {
                    effect.Parameters["Alpha"].SetValue(1f);

                    var vertices = verticesCache[fullChunk.Index];

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

        public (VertexPositionTextureLight[], VertexPositionTextureLight[]) CalculateMesh(ChunkAdjacency adjacency)
        {
            IChunk chunk = adjacency.Root;

            List<VertexPositionTextureLight> vertices = [];
            List<VertexPositionTextureLight> transparentVertices = [];

            if (chunk is not Chunk fullChunk) return ([], []);

            foreach (Vector3I index in fullChunk.GetActiveIndexes())
            {
                int x = index.X;
                int y = index.Y;
                int z = index.Z;

                Vector3 blockPosition = new Vector3(x, y, z) + fullChunk.Position;

                FacesState visibleFaces = fullChunk.GetVisibleFaces(index, adjacency);
                FacesData<LightValue> lightValues = lightSystem.GetFacesLight(visibleFaces, y, x, z, adjacency);

                foreach (Faces face in visibleFaces.GetFaces())
                {
                    ushort texture = fullChunk[x, y, z].Value;
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

            fullChunk.RecalculateMesh = false;

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
