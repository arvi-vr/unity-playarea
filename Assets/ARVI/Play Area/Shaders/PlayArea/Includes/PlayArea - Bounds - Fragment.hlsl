void ConstructGrid_float(float2 UV, float CellSize, float HandMask, out float2 UVIcon1, out float2 UVIcon2, out float2 UVCellBig, out float2 UVCellSmall, out float2 UVGrid, out float AlphaInteraction)
{
    AlphaInteraction = 1 - (1 - CellSize) * HandMask;
    clip(AlphaInteraction - 0.01);

    float remapCellSize = 10 - CellSize * 9.28;
    float2 uvTilling = UV * float2(32, 30);

    //UV modified for filled grid cells
    float2 uvCell = uvTilling * 2;
    uvCell.x += 0.5;
    uvCell.y += fmod(floor(uvCell.x), 2) * 0.5;
    uvCell = frac(uvCell) - 0.5;

    UVIcon1 = (frac(float2(uvTilling.x, uvTilling.y + 0.5)) - 0.3) * 2.5; //UV for Hand icons
    UVIcon2 = (frac(uvTilling) - 0.3) * 2.5; //UV for STOP icons
    UVCellBig = uvCell * remapCellSize + 0.5;
    UVCellSmall = uvCell * (remapCellSize + 0.05) + 0.5; //Cell Big and Small used for outline
    UVGrid = uvTilling;
}

void CalculateBaseColor_float(float Icon1, float Icon2, float CellBig, float CellSmall, float3 BaseColor, float InsideCellAlpha, float2 UV, out float3 NewBaseColor)
{
    float2 grid = floor(UV);
    float notEvenRow = fmod(grid.x, 2);
    float evenRowColumn = (1 - notEvenRow) * (1 - fmod(grid.y, 2));
    float icons = evenRowColumn * Icon2 + notEvenRow * Icon1; // Placed icons to nessesary cells
    float3 cellColor = CellBig - CellSmall + CellSmall * BaseColor; //Filled grid cells

    NewBaseColor = min(1, cellColor + icons * icons * InsideCellAlpha + BaseColor);
}

void CalculateAlpha_float(float CellAlpha, float GridAlpha, float3 HeadsetDirection, float3 SideDirection, float CellSize, float AlphaInteraction, float4 PositionSS, float Depth,
    out float NewAlpha)
{
    float aCell = min(CellSize, 1 - dot(HeadsetDirection, SideDirection)) * CellAlpha; //Filled grid cells depending of camera direction //0.8 - bad value
    float aGrid = GridAlpha + aCell * aCell;
    NewAlpha = min(1, aGrid * (AlphaInteraction + AlphaInteraction)) * min(1, (Depth - PositionSS.w) * 10);
}

void HandMask_float(float3 WPosition, float3 LCPosition, float LCRadius, float3 RCPosition, float RCRadius, out float HandMask)
{
    HandMask = 16 * min(0.25, (distance(WPosition, LCPosition) - LCRadius)) * min(0.25, (distance(WPosition, RCPosition) - RCRadius));
}