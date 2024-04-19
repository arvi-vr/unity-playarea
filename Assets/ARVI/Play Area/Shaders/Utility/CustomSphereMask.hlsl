
void CustomSphereMaskSimple_float(float4 Coords, float4 Center, float Radius, float Hardness, out float4 Out)
{
    Out = saturate((distance(Coords, Center) - Radius) / (1 - Hardness));
}
