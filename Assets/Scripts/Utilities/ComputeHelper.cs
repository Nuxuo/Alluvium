using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace VoxelTerrain.Utilities
{
    /// <summary>
    /// Depth buffer mode for render textures
    /// </summary>
    public enum DepthMode { None = 0, Depth16 = 16, Depth24 = 24 }

    /// <summary>
    /// Helper utilities for working with compute shaders and GPU buffers.
    /// Simplifies common operations like buffer creation, dispatching, and resource management.
    /// </summary>
    public static class ComputeHelper
    {
        public const FilterMode defaultFilterMode = FilterMode.Bilinear;
        public const GraphicsFormat defaultGraphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;

        #region Dispatch Methods

        /// <summary>
        /// Convenience method for dispatching a compute shader.
        /// Calculates the number of thread groups based on the number of iterations needed.
        /// </summary>
        public static void Dispatch(ComputeShader cs, int numIterationsX, int numIterationsY = 1, int numIterationsZ = 1, int kernelIndex = 0)
        {
            Vector3Int threadGroupSizes = GetThreadGroupSizes(cs, kernelIndex);
            int numGroupsX = Mathf.CeilToInt(numIterationsX / (float)threadGroupSizes.x);
            int numGroupsY = Mathf.CeilToInt(numIterationsY / (float)threadGroupSizes.y);
            int numGroupsZ = Mathf.CeilToInt(numIterationsZ / (float)threadGroupSizes.z);
            cs.Dispatch(kernelIndex, numGroupsX, numGroupsY, numGroupsZ);
        }

        /// <summary>
        /// Dispatches a compute shader based on the size of a RenderTexture
        /// </summary>
        public static void Dispatch(ComputeShader cs, RenderTexture texture, int kernelIndex = 0)
        {
            Dispatch(cs, texture.width, texture.height, texture.volumeDepth, kernelIndex);
        }

        /// <summary>
        /// Dispatches a compute shader based on the size of a Texture2D
        /// </summary>
        public static void Dispatch(ComputeShader cs, Texture2D texture, int kernelIndex = 0)
        {
            Dispatch(cs, texture.width, texture.height, 1, kernelIndex);
        }

        #endregion

        #region Buffer Creation and Management

        /// <summary>
        /// Gets the stride (size in bytes) of a struct type
        /// </summary>
        public static int GetStride<T>()
        {
            return System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        }

        /// <summary>
        /// Creates an append buffer with the specified size
        /// </summary>
        public static ComputeBuffer CreateAppendBuffer<T>(int size = 1)
        {
            int stride = GetStride<T>();
            ComputeBuffer buffer = new ComputeBuffer(size, stride, ComputeBufferType.Append);
            buffer.SetCounterValue(0);
            return buffer;
        }

        /// <summary>
        /// Creates or recreates a structured buffer if needed
        /// </summary>
        public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, int count)
        {
            int stride = GetStride<T>();
            bool createNewBuffer = buffer == null || !buffer.IsValid() || buffer.count != count || buffer.stride != stride;
            if (createNewBuffer)
            {
                Release(buffer);
                buffer = new ComputeBuffer(count, stride);
            }
        }

        /// <summary>
        /// Creates a structured buffer from an array of data
        /// </summary>
        public static ComputeBuffer CreateStructuredBuffer<T>(T[] data)
        {
            var buffer = new ComputeBuffer(data.Length, GetStride<T>());
            buffer.SetData(data);
            return buffer;
        }

        /// <summary>
        /// Creates a structured buffer with the specified count
        /// </summary>
        public static ComputeBuffer CreateStructuredBuffer<T>(int count)
        {
            return new ComputeBuffer(count, GetStride<T>());
        }

        /// <summary>
        /// Creates or recreates a structured buffer and populates it with data
        /// </summary>
        public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, T[] data)
        {
            CreateStructuredBuffer<T>(ref buffer, data.Length);
            buffer.SetData(data);
        }

        #endregion

        #region Buffer Binding

        /// <summary>
        /// Sets a buffer on multiple kernels of a compute shader
        /// </summary>
        public static void SetBuffer(ComputeShader compute, ComputeBuffer buffer, string id, params int[] kernels)
        {
            for (int i = 0; i < kernels.Length; i++)
            {
                compute.SetBuffer(kernels[i], id, buffer);
            }
        }

        /// <summary>
        /// Creates a buffer from data and binds it to a compute shader kernel
        /// </summary>
        public static ComputeBuffer CreateAndSetBuffer<T>(T[] data, ComputeShader cs, string nameID, int kernelIndex = 0)
        {
            ComputeBuffer buffer = null;
            CreateAndSetBuffer<T>(ref buffer, data, cs, nameID, kernelIndex);
            return buffer;
        }

        /// <summary>
        /// Creates a buffer from data and binds it to a compute shader kernel (ref version)
        /// </summary>
        public static void CreateAndSetBuffer<T>(ref ComputeBuffer buffer, T[] data, ComputeShader cs, string nameID, int kernelIndex = 0)
        {
            CreateStructuredBuffer<T>(ref buffer, data.Length);
            buffer.SetData(data);
            cs.SetBuffer(kernelIndex, nameID, buffer);
        }

        /// <summary>
        /// Creates an empty buffer and binds it to a compute shader kernel
        /// </summary>
        public static ComputeBuffer CreateAndSetBuffer<T>(int length, ComputeShader cs, string nameID, int kernelIndex = 0)
        {
            ComputeBuffer buffer = null;
            CreateAndSetBuffer<T>(ref buffer, length, cs, nameID, kernelIndex);
            return buffer;
        }

        /// <summary>
        /// Creates an empty buffer and binds it to a compute shader kernel (ref version)
        /// </summary>
        public static void CreateAndSetBuffer<T>(ref ComputeBuffer buffer, int length, ComputeShader cs, string nameID, int kernelIndex = 0)
        {
            CreateStructuredBuffer<T>(ref buffer, length);
            cs.SetBuffer(kernelIndex, nameID, buffer);
        }

        #endregion

        #region Resource Release

        /// <summary>
        /// Releases supplied buffer(s) if not null
        /// </summary>
        public static void Release(params ComputeBuffer[] buffers)
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                if (buffers[i] != null)
                {
                    buffers[i].Release();
                }
            }
        }

        /// <summary>
        /// Releases supplied render texture(s) if not null
        /// </summary>
        public static void Release(params RenderTexture[] textures)
        {
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] != null)
                {
                    textures[i].Release();
                }
            }
        }

        #endregion

        #region Thread Group Utilities

        /// <summary>
        /// Gets the thread group sizes for a compute shader kernel
        /// </summary>
        public static Vector3Int GetThreadGroupSizes(ComputeShader compute, int kernelIndex = 0)
        {
            uint x, y, z;
            compute.GetKernelThreadGroupSizes(kernelIndex, out x, out y, out z);
            return new Vector3Int((int)x, (int)y, (int)z);
        }

        #endregion

        #region Texture Helpers

        /// <summary>
        /// Creates a render texture from a template
        /// </summary>
        public static RenderTexture CreateRenderTexture(RenderTexture template)
        {
            RenderTexture renderTexture = null;
            CreateRenderTexture(ref renderTexture, template);
            return renderTexture;
        }

        /// <summary>
        /// Creates a render texture with specified parameters
        /// </summary>
        public static RenderTexture CreateRenderTexture(int width, int height, FilterMode filterMode, GraphicsFormat format,
            string name = "Unnamed", DepthMode depthMode = DepthMode.None, bool useMipMaps = false)
        {
            RenderTexture texture = new RenderTexture(width, height, (int)depthMode);
            texture.graphicsFormat = format;
            texture.enableRandomWrite = true;
            texture.autoGenerateMips = false;
            texture.useMipMap = useMipMaps;
            texture.Create();

            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = filterMode;
            return texture;
        }

        /// <summary>
        /// Creates a render texture from a template (ref version)
        /// </summary>
        public static void CreateRenderTexture(ref RenderTexture texture, RenderTexture template)
        {
            if (texture != null)
            {
                texture.Release();
            }
            texture = new RenderTexture(template.descriptor);
            texture.enableRandomWrite = true;
            texture.Create();
        }

        /// <summary>
        /// Creates a render texture with default settings
        /// </summary>
        public static void CreateRenderTexture(ref RenderTexture texture, int width, int height)
        {
            CreateRenderTexture(ref texture, width, height, defaultFilterMode, defaultGraphicsFormat);
        }

        /// <summary>
        /// Creates or updates a render texture with specified parameters
        /// </summary>
        /// <returns>True if a new texture was created</returns>
        public static bool CreateRenderTexture(ref RenderTexture texture, int width, int height, FilterMode filterMode,
            GraphicsFormat format, string name = "Unnamed", DepthMode depthMode = DepthMode.None, bool useMipMaps = false)
        {
            if (texture == null || !texture.IsCreated() || texture.width != width || texture.height != height ||
                texture.graphicsFormat != format || texture.depth != (int)depthMode || texture.useMipMap != useMipMaps)
            {
                if (texture != null)
                {
                    texture.Release();
                }
                texture = CreateRenderTexture(width, height, filterMode, format, name, depthMode, useMipMaps);
                return true;
            }
            else
            {
                texture.name = name;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = filterMode;
            }

            return false;
        }

        /// <summary>
        /// Creates a 3D render texture from a template
        /// </summary>
        public static void CreateRenderTexture3D(ref RenderTexture texture, RenderTexture template)
        {
            CreateRenderTexture(ref texture, template);
        }

        /// <summary>
        /// Creates a 3D render texture with specified parameters
        /// </summary>
        public static void CreateRenderTexture3D(ref RenderTexture texture, int size, GraphicsFormat format,
            TextureWrapMode wrapMode = TextureWrapMode.Repeat, string name = "Untitled", bool mipmaps = false)
        {
            if (texture == null || !texture.IsCreated() || texture.width != size || texture.height != size ||
                texture.volumeDepth != size || texture.graphicsFormat != format)
            {
                if (texture != null)
                {
                    texture.Release();
                }
                const int numBitsInDepthBuffer = 0;
                texture = new RenderTexture(size, size, numBitsInDepthBuffer);
                texture.graphicsFormat = format;
                texture.volumeDepth = size;
                texture.enableRandomWrite = true;
                texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                texture.useMipMap = mipmaps;
                texture.autoGenerateMips = false;
                texture.Create();
            }
            texture.wrapMode = wrapMode;
            texture.filterMode = FilterMode.Bilinear;
            texture.name = name;
        }

        #endregion

        #region Instancing Helpers

        /// <summary>
        /// Creates an args buffer for instanced indirect rendering
        /// </summary>
        public static ComputeBuffer CreateArgsBuffer(Mesh mesh, int numInstances)
        {
            const int subMeshIndex = 0;
            uint[] args = new uint[5];
            args[0] = (uint)mesh.GetIndexCount(subMeshIndex);
            args[1] = (uint)numInstances;
            args[2] = (uint)mesh.GetIndexStart(subMeshIndex);
            args[3] = (uint)mesh.GetBaseVertex(subMeshIndex);
            args[4] = 0; // offset

            ComputeBuffer argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(args);
            return argsBuffer;
        }

        /// <summary>
        /// Creates an args buffer for instanced indirect rendering 
        /// (number of instances comes from size of append buffer)
        /// </summary>
        public static ComputeBuffer CreateArgsBuffer(Mesh mesh, ComputeBuffer appendBuffer)
        {
            var buffer = CreateArgsBuffer(mesh, 0);
            ComputeBuffer.CopyCount(appendBuffer, buffer, sizeof(uint));
            return buffer;
        }

        /// <summary>
        /// Reads the number of elements in an append buffer
        /// </summary>
        public static int ReadAppendBufferLength(ComputeBuffer appendBuffer)
        {
            ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            ComputeBuffer.CopyCount(appendBuffer, countBuffer, 0);

            int[] data = new int[1];
            countBuffer.GetData(data);
            Release(countBuffer);
            return data[0];
        }

        #endregion

        #region Shader Property Setting

        /// <summary>
        /// Sets a texture on multiple kernels of a compute shader
        /// </summary>
        public static void SetTexture(ComputeShader compute, Texture texture, string name, params int[] kernels)
        {
            for (int i = 0; i < kernels.Length; i++)
            {
                compute.SetTexture(kernels[i], name, texture);
            }
        }

        /// <summary>
        /// Sets all values from a settings object on the shader. 
        /// Variable names must match exactly in the shader.
        /// Settings object can be any class/struct containing vectors/ints/floats/bools
        /// </summary>
        public static void SetParams(System.Object settings, ComputeShader shader, string variableNamePrefix = "", string variableNameSuffix = "")
        {
            var fields = settings.GetType().GetFields();
            foreach (var field in fields)
            {
                var fieldType = field.FieldType;
                string shaderVariableName = variableNamePrefix + field.Name + variableNameSuffix;

                if (fieldType == typeof(Vector4) || fieldType == typeof(Vector3) || fieldType == typeof(Vector2))
                {
                    shader.SetVector(shaderVariableName, (Vector4)field.GetValue(settings));
                }
                else if (fieldType == typeof(int))
                {
                    shader.SetInt(shaderVariableName, (int)field.GetValue(settings));
                }
                else if (fieldType == typeof(float))
                {
                    shader.SetFloat(shaderVariableName, (float)field.GetValue(settings));
                }
                else if (fieldType == typeof(bool))
                {
                    shader.SetBool(shaderVariableName, (bool)field.GetValue(settings));
                }
                else
                {
                    Debug.Log($"Type {fieldType} not implemented in ComputeHelper.SetParams");
                }
            }
        }

        #endregion

        #region Miscellaneous

        /// <summary>
        /// Packs floats into a format suitable for compute shader arrays
        /// https://cmwdexint.com/2017/12/04/computeshader-setfloats/
        /// </summary>
        public static float[] PackFloats(params float[] values)
        {
            float[] packed = new float[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
            {
                packed[i * 4] = values[i];
            }
            return values;
        }

        /// <summary>
        /// Loads a compute shader from resources if not already loaded
        /// </summary>
        public static void LoadComputeShader(ref ComputeShader shader, string name)
        {
            if (shader == null)
            {
                shader = LoadComputeShader(name);
            }
        }

        /// <summary>
        /// Loads a compute shader from resources
        /// </summary>
        public static ComputeShader LoadComputeShader(string name)
        {
            return Resources.Load<ComputeShader>(name.Split('.')[0]);
        }

        /// <summary>
        /// Loads a shader from resources if not already loaded
        /// </summary>
        static void LoadShader(ref Shader shader, string name)
        {
            if (shader == null)
            {
                shader = (Shader)Resources.Load(name);
            }
        }

        #endregion
    }
}