using System.Runtime.InteropServices;
using Vortice.Direct3D12;
using Vortice.Dxc;

namespace DirectX3D12Example
{
    public sealed partial class D3D12GraphicsDevice
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct PipelineStateStream
        {
            public PipelineStateSubObjectTypeRootSignature RootSignature;
            public PipelineStateSubObjectTypeVertexShader VertexShader;
            public PipelineStateSubObjectTypePixelShader PixelShader;
            public PipelineStateSubObjectTypeInputLayout InputLayout;
            public PipelineStateSubObjectTypeSampleMask SampleMask;
            public PipelineStateSubObjectTypePrimitiveTopology PrimitiveTopology;
            public PipelineStateSubObjectTypeRasterizer RasterizerState;
            public PipelineStateSubObjectTypeBlend BlendState;
            public PipelineStateSubObjectTypeDepthStencil DepthStencilState;
            public PipelineStateSubObjectTypeRenderTargetFormats RenderTargetFormats;
            public PipelineStateSubObjectTypeDepthStencilFormat DepthStencilFormat;
            public PipelineStateSubObjectTypeSampleDescription SampleDescription;
        }

        private static byte[] CompileBytecode(DxcShaderStage stage, string shaderName, string entryPoint)
        {
            string assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
            string shaderSource = File.ReadAllText(Path.Combine(assetsPath, shaderName));

            using var includeHandler = new ShaderIncludeHandler(assetsPath);
            using IDxcResult results = DxcCompiler.Compile(stage, shaderSource, entryPoint, includeHandler: includeHandler);
            if (results.GetStatus().Failure)
            {
                throw new Exception(results.GetErrors());
            }

            return results.GetObjectBytecodeArray();
        }

        public void Dispose()
        {
            WaitForPreviousFrame();

            vertexBufferTriangle?.Dispose();

            for (int i = 0; i < D3D12GraphicsDevice.BufferCount; i++)
            {
                commandAllocators?[i].Dispose();
                renderTargets?[i].Dispose();
            }
            commandList?.Dispose();

            rtvHeap?.Dispose();
            pipelineState?.Dispose();
            rootSignature?.Dispose();
            SwapChain?.Dispose();
            frameFence?.Dispose();
            CommandQueue?.Dispose();
            device?.Dispose();
            debugDevice?.Dispose();
        }
    }
}