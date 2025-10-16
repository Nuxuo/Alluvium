Shader "Custom/TerrainWithGrid" {
    Properties {
        _GrassColour ("Grass Colour", Color) = (0,1,0,1)
        _RockColour ("Rock Colour", Color) = (1,1,1,1)
        _GrassSlopeThreshold ("Grass Slope Threshold", Range(0,1)) = .5
        _GrassBlendAmount ("Grass Blend Amount", Range(0,1)) = .5
        
        [Header(Grid Settings)]
        [Toggle] _ShowGrid ("Show Grid", Float) = 1
        _GridSpacing ("Grid Spacing", Float) = 0.05
        _GridColor ("Grid Color", Color) = (0,0,0,0.5)
        _GridWidth ("Grid Line Width", Range(0.0001, 0.01)) = 0.002
        _GridOffset ("Grid Offset", Float) = 0.0005
        _GridFadeStart ("Grid Fade Start Distance", Float) = 10
        _GridFadeEnd ("Grid Fade End Distance", Float) = 30
        
        [Header(Highlight Settings)]
        _HighlightCenter ("Highlight Center", Vector) = (0,0,0,0)
        _HighlightRadius ("Highlight Radius (cells)", Float) = 3
        _HighlightColor ("Highlight Color (Valid)", Color) = (0,1,1,0.8)
        _InvalidColor ("Highlight Color (Invalid)", Color) = (1,0,0,0.8)
        _HighlightIntensity ("Highlight Intensity", Range(0,1)) = 0.8
        _IsValidPlacement ("Is Valid Placement", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        struct Input {
            float3 worldPos;
            float3 worldNormal;
        };

        half _MaxHeight;
        half _GrassSlopeThreshold;
        half _GrassBlendAmount;
        fixed4 _GrassColour;
        fixed4 _RockColour;
        
        // Grid properties
        float _ShowGrid;
        float _GridSpacing;
        fixed4 _GridColor;
        float _GridWidth;
        float _GridOffset;
        float _GridFadeStart;
        float _GridFadeEnd;
        
        // Highlight properties
        float3 _HighlightCenter;
        float _HighlightRadius;
        fixed4 _HighlightColor;
        fixed4 _InvalidColor;
        float _HighlightIntensity;
        float _IsValidPlacement;

        // Function to calculate grid lines
        float GetGrid(float2 pos, float spacing, float width) {
            pos += _GridOffset;
            float2 gridPos = frac(pos / spacing) * spacing;
            float2 grid = smoothstep(width, 0, gridPos) + 
                         smoothstep(width, 0, spacing - gridPos);
            return saturate(grid.x + grid.y);
        }
        
        // Function to get grid cell coordinates
        float2 GetGridCell(float2 pos, float spacing) {
            pos += _GridOffset;
            return floor(pos / spacing);
        }
        
        // Function to check if a cell should be highlighted (square selection)
        bool IsInHighlightArea(float2 worldPos) {
            float2 currentCell = GetGridCell(worldPos, _GridSpacing);
            float2 centerCell = GetGridCell(float2(_HighlightCenter.x, _HighlightCenter.z), _GridSpacing);
            
            // Use Chebyshev distance for square selection (max of x and y difference)
            float distX = abs(currentCell.x - centerCell.x);
            float distY = abs(currentCell.y - centerCell.y);
            float maxDist = max(distX, distY);
            
            return maxDist <= _HighlightRadius;
        }

        void surf (Input IN, inout SurfaceOutputStandard o) {
            // Calculate slope-based color
            float slope = 1 - IN.worldNormal.y;
            float grassBlendHeight = _GrassSlopeThreshold * (1 - _GrassBlendAmount);
            float grassWeight = 1 - saturate((slope - grassBlendHeight) / (_GrassSlopeThreshold - grassBlendHeight));
            fixed4 terrainColor = _GrassColour * grassWeight + _RockColour * (1 - grassWeight);

            // Apply grid if enabled
            if (_ShowGrid > 0.5) {
                float2 gridPos = float2(IN.worldPos.x, IN.worldPos.z);
                float gridIntensity = GetGrid(gridPos, _GridSpacing, _GridWidth);
                
                // Calculate distance-based fade
                float distToCamera = distance(IN.worldPos, _WorldSpaceCameraPos);
                float fadeFactor = 1 - saturate((distToCamera - _GridFadeStart) / (_GridFadeEnd - _GridFadeStart));
                
                gridIntensity *= fadeFactor;
                
                // Check if we're in the highlight area
                bool inHighlight = IsInHighlightArea(gridPos);
                
                // Choose color based on validity
                fixed4 gridColorToUse = _GridColor;
                if (inHighlight) {
                    gridColorToUse = _IsValidPlacement > 0.5 ? _HighlightColor : _InvalidColor;
                }
                
                float intensityToUse = inHighlight ? gridIntensity * _HighlightIntensity : gridIntensity * _GridColor.a;
                
                terrainColor = lerp(terrainColor, gridColorToUse, intensityToUse);
            }

            o.Albedo = terrainColor.rgb;
        }
        ENDCG
    }
}