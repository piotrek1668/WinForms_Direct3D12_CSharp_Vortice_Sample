using System.Numerics;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace DirectX3D12Example
{
    public readonly struct VertexPositionColor
    {
        private readonly Vector3 Position;
        private readonly Color4 Color;

        public static readonly InputElementDescription[] InputElements = {
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0), // see comment below
            new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 12, 0) // offset = 12 = 3 * 32 bit = 3 * 4 byte (R32G32B32_Float)
        };

        public VertexPositionColor(in Vector3 position, in Color4 color)
        {
            Position = position;
            Color = color;
        }
    }
}
