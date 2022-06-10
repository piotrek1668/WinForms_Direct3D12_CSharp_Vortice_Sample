struct VSInput
{
    float4 Position : POSITION;
    float4 Color : COLOR;
};

struct PSInput
{
    float4 Position : SV_POSITION; // SV steht fuer system value
    float4 Color : COLOR;
};


// input layout -> vertex shader
// vertex shader main function
PSInput VSMain(VSInput input) {
    PSInput vertexOut;
    
    vertexOut.Position = input.Position;
    vertexOut.Color = input.Color;
    
    return vertexOut;
}

// rasterizer -> pixel shader
// Pixel shader main function
float4 PSMain(PSInput input) : SV_TARGET {
    return input.Color;
}
