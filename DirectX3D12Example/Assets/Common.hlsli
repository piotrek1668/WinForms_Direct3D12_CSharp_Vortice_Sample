struct VSInput {
    float4 Position : POSITION;
    float4 Color : COLOR;
};

struct PSInput {
    float4 Position : SV_POSITION; // SV steht fuer system value
    float4 Color : COLOR;
};
