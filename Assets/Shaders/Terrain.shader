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

        // Function to calculate grid lines
        float GetGrid(float2 pos, float spacing, float width) {
            // Offset to align with vertex positions (centered terrain grid)
            // This ensures grid lines fall on vertex boundaries, not in cell centers
            pos += _GridOffset;
            
            // Calculate position within grid cell (0 to spacing)
            float2 gridPos = frac(pos / spacing) * spacing;
            
            // Create grid lines at cell boundaries
            float2 grid = smoothstep(width, 0, gridPos) + 
                         smoothstep(width, 0, spacing - gridPos);
            
            // Combine x and z grid lines
            return saturate(grid.x + grid.y);
        }

        void surf (Input IN, inout SurfaceOutputStandard o) {
            // Calculate slope-based color
            float slope = 1 - IN.worldNormal.y;
            float grassBlendHeight = _GrassSlopeThreshold * (1 - _GrassBlendAmount);
            float grassWeight = 1 - saturate((slope - grassBlendHeight) / (_GrassSlopeThreshold - grassBlendHeight));
            fixed4 terrainColor = _GrassColour * grassWeight + _RockColour * (1 - grassWeight);

            // Apply grid if enabled
            if (_ShowGrid > 0.5) {
                // Calculate grid intensity
                float2 gridPos = float2(IN.worldPos.x, IN.worldPos.z);
                float gridIntensity = GetGrid(gridPos, _GridSpacing, _GridWidth);
                
                // Calculate distance-based fade
                float3 cameraPos = _WorldSpaceCameraPos;
                float distToCamera = distance(IN.worldPos, cameraPos);
                float fadeFactor = 1 - saturate((distToCamera - _GridFadeStart) / (_GridFadeEnd - _GridFadeStart));
                
                // Apply grid with fade
                gridIntensity *= fadeFactor;
                terrainColor = lerp(terrainColor, _GridColor, gridIntensity * _GridColor.a);
            }

            // Apply the final color
            o.Albedo = terrainColor.rgb;
        }
        ENDCG
    }
}