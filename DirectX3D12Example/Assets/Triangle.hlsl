#include "Common.hlsli"

// vertex shader main function
PSInput VSMain(VSInput input) {
    PSInput result;
    
    result.Position = input.Position;
    result.Color = input.Color;
    
    return result;
}

/// Pixel shader main function
float4 PSMain(PSInput input) : SV_TARGET {
    return input.Color;
}
