
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// This class contains some helper functions to make life a little easier working with compute shaders

public enum DepthMode { None = 0, Depth16 = 16, Depth24 = 24 }

public static class ComputeHelper
{

    public const FilterMode defaultFilterMode = FilterMode.Bilinear;
    public const GraphicsFormat defaultGraphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;



    static ComputeShader normalizeTextureCompute;
    static ComputeShader clearTextureCompute;
    static ComputeShader swizzleTextureCompute;
    static ComputeShader copy3DCompute;
    static Shader bicubicUpscale;

  
    ///  calculates the number of thread groups based on the number of iterations needed.
    public static void Dispatch(ComputeShader cs, int numIterationsX, int numIterationsY = 1, int numIterationsZ = 1, int kernelIndex = 0)
    {
        Vector3Int threadGroupSizes = GetThreadGroupSizes(cs, kernelIndex);
        int numGroupsX = Mathf.CeilToInt(numIterationsX / (float)threadGroupSizes.x);
        int numGroupsY = Mathf.CeilToInt(numIterationsY / (float)threadGroupSizes.y);
        int numGroupsZ = Mathf.CeilToInt(numIterationsZ / (float)threadGroupSizes.y);
        cs.Dispatch(kernelIndex, numGroupsX, numGroupsY, numGroupsZ);
    }

    
    /// It calculates the number of thread groups based on the size of the given texture.
    public static void Dispatch(ComputeShader cs, RenderTexture texture, int kernelIndex = 0)
    {
        Vector3Int threadGroupSizes = GetThreadGroupSizes(cs, kernelIndex);
        Dispatch(cs, texture.width, texture.height, texture.volumeDepth, kernelIndex);
    }

    public static void Dispatch(ComputeShader cs, Texture2D texture, int kernelIndex = 0)
    {
        Vector3Int threadGroupSizes = GetThreadGroupSizes(cs, kernelIndex);
        Dispatch(cs, texture.width, texture.height, 1, kernelIndex);
    }

    public static int GetStride<T>()
    {
        return System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
    }

    public static ComputeBuffer CreateAppendBuffer<T>(int size = 1)
    {
        int stride = GetStride<T>();
        ComputeBuffer buffer = new ComputeBuffer(size, stride, ComputeBufferType.Append);
        buffer.SetCounterValue(0);
        return buffer;

    }


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


    public static ComputeBuffer CreateStructuredBuffer<T>(T[] data)
    {
        var buffer = new ComputeBuffer(data.Length, GetStride<T>());
        buffer.SetData(data);
        return buffer;
    }

    public static ComputeBuffer CreateStructuredBuffer<T>(int count)
    {
        return new ComputeBuffer(count, GetStride<T>());
    }

    public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, T[] data)
    {
        CreateStructuredBuffer<T>(ref buffer, data.Length);
        buffer.SetData(data);
    }

    public static void SetBuffer(ComputeShader compute, ComputeBuffer buffer, string id, params int[] kernels)
    {
        for (int i = 0; i < kernels.Length; i++)
        {
            compute.SetBuffer(kernels[i], id, buffer);
        }
    }

    public static ComputeBuffer CreateAndSetBuffer<T>(T[] data, ComputeShader cs, string nameID, int kernelIndex = 0)
    {
        ComputeBuffer buffer = null;
        CreateAndSetBuffer<T>(ref buffer, data, cs, nameID, kernelIndex);
        return buffer;
    }

    public static void CreateAndSetBuffer<T>(ref ComputeBuffer buffer, T[] data, ComputeShader cs, string nameID, int kernelIndex = 0)
    {
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        CreateStructuredBuffer<T>(ref buffer, data.Length);
        buffer.SetData(data);
        cs.SetBuffer(kernelIndex, nameID, buffer);
    }

    public static ComputeBuffer CreateAndSetBuffer<T>(int length, ComputeShader cs, string nameID, int kernelIndex = 0)
    {
        ComputeBuffer buffer = null;
        CreateAndSetBuffer<T>(ref buffer, length, cs, nameID, kernelIndex);
        return buffer;
    }

    public static void CreateAndSetBuffer<T>(ref ComputeBuffer buffer, int length, ComputeShader cs, string nameID, int kernelIndex = 0)
    {
        CreateStructuredBuffer<T>(ref buffer, length);
        cs.SetBuffer(kernelIndex, nameID, buffer);
    }



    /// Releases supplied buffer/s if not null
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

    /// Releases supplied render textures/s if not null
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

    public static Vector3Int GetThreadGroupSizes(ComputeShader compute, int kernelIndex = 0)
    {
        uint x, y, z;
        compute.GetKernelThreadGroupSizes(kernelIndex, out x, out y, out z);
        return new Vector3Int((int)x, (int)y, (int)z);
    }

   
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

    // Create args buffer for instanced indirect rendering (number of instances comes from size of append buffer)
    public static ComputeBuffer CreateArgsBuffer(Mesh mesh, ComputeBuffer appendBuffer)
    {
        var buffer = CreateArgsBuffer(mesh, 0);
        ComputeBuffer.CopyCount(appendBuffer, buffer, sizeof(uint));
        return buffer;
    }

    // Read number of elements in append buffer
    public static int ReadAppendBufferLength(ComputeBuffer appendBuffer)
    {
        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        ComputeBuffer.CopyCount(appendBuffer, countBuffer, 0);

        int[] data = new int[1];
        countBuffer.GetData(data);
        Release(countBuffer);
        return data[0];
    }

    public static void LoadComputeShader(ref ComputeShader shader, string name)
    {
        if (shader == null)
        {
            shader = LoadComputeShader(name);
        }
    }

    public static ComputeShader LoadComputeShader(string name)
    {
        return Resources.Load<ComputeShader>(name.Split('.')[0]);
    }

    static void LoadShader(ref Shader shader, string name)
    {
        if (shader == null)
        {
            shader = (Shader)Resources.Load(name);
        }
    }
}