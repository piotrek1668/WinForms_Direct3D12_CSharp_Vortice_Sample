using System.Runtime.InteropServices;
using Vortice.Direct3D12;

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
    }
}