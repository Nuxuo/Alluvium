Shader "Custom/TerrainWithGridTextured" {
    Properties {
        [Header(Terrain Textures)]
        _GrassTexture ("Grass Albedo", 2D) = "white" {}
        _GrassNormal ("Grass Normal", 2D) = "bump" {}
        _GrassScale ("Grass Texture Scale", Float) = 5
        
        _RockTexture ("Rock Albedo", 2D) = "white" {}
        _RockNormal ("Rock Normal", 2D) = "bump" {}
        _RockScale ("Rock Texture Scale", Float) = 5
        
        [Header(Material Properties)]
        _GrassSlopeThreshold ("Grass Slope Threshold", Range(0,1)) = .5
        _GrassBlendAmount ("Grass Blend Amount", Range(0,1)) = .5
        _Smoothness ("Smoothness", Range(0,1)) = 0.2
        
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

        sampler2D _GrassTexture;
        sampler2D _GrassNormal;
        sampler2D _RockTexture;
        sampler2D _RockNormal;

        struct Input {
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        float _GrassScale;
        float _RockScale;
        half _MaxHeight;
        half _GrassSlopeThreshold;
        half _GrassBlendAmount;
        half _Smoothness;
        
        float _ShowGrid;
        float _GridSpacing;
        fixed4 _GridColor;
        float _GridWidth;
        float _GridOffset;
        float _GridFadeStart;
        float _GridFadeEnd;
        
        float3 _HighlightCenter;
        float _HighlightRadius;
        fixed4 _HighlightColor;
        fixed4 _InvalidColor;
        float _HighlightIntensity;
        float _IsValidPlacement;

        fixed4 TriplanarTexture(sampler2D tex, float3 worldPos, float3 normal, float scale) {
            float3 blend = abs(normal);
            blend = pow(blend, 4);
            blend = normalize(max(blend, 0.00001));
            float b = (blend.x + blend.y + blend.z);
            blend /= float3(b, b, b);
            
            fixed4 xaxis = tex2D(tex, worldPos.zy * scale);
            fixed4 yaxis = tex2D(tex, worldPos.xz * scale);
            fixed4 zaxis = tex2D(tex, worldPos.xy * scale);
            
            return xaxis * blend.x + yaxis * blend.y + zaxis * blend.z;
        }

        float GetGrid(float2 pos, float spacing, float width) {
            pos += _GridOffset;
            float2 gridPos = frac(pos / spacing) * spacing;
            float2 grid = smoothstep(width, 0, gridPos) + 
                         smoothstep(width, 0, spacing - gridPos);
            return saturate(grid.x + grid.y);
        }
        
        float2 GetGridCell(float2 pos, float spacing) {
            pos += _GridOffset;
            return floor(pos / spacing);
        }
        
        bool IsInHighlightArea(float2 worldPos) {
            float2 currentCell = GetGridCell(worldPos, _GridSpacing);
            float2 centerCell = GetGridCell(float2(_HighlightCenter.x, _HighlightCenter.z), _GridSpacing);
            
            float distX = abs(currentCell.x - centerCell.x);
            float distY = abs(currentCell.y - centerCell.y);
            float maxDist = max(distX, distY);
            
            return maxDist <= _HighlightRadius;
        }

        void surf (Input IN, inout SurfaceOutputStandard o) {
            // Calculate slope (0 = flat, 1 = vertical)
            float slope = 1 - IN.worldNormal.y;
            
            // Blend grass and rock based on slope
            float grassBlendHeight = _GrassSlopeThreshold * (1 - _GrassBlendAmount);
            float grassWeight = 1 - saturate((slope - grassBlendHeight) / (_GrassSlopeThreshold - grassBlendHeight));
            
            // Sample textures
            fixed4 grassAlbedo = TriplanarTexture(_GrassTexture, IN.worldPos, IN.worldNormal, _GrassScale);
            fixed4 rockAlbedo = TriplanarTexture(_RockTexture, IN.worldPos, IN.worldNormal, _RockScale);
            
            // Blend
            fixed4 terrainColor = grassAlbedo * grassWeight + rockAlbedo * (1 - grassWeight);

            // Apply grid
            if (_ShowGrid > 0.5) {
                float2 gridPos = float2(IN.worldPos.x, IN.worldPos.z);
                float gridIntensity = GetGrid(gridPos, _GridSpacing, _GridWidth);
                
                float distToCamera = distance(IN.worldPos, _WorldSpaceCameraPos);
                float fadeFactor = 1 - saturate((distToCamera - _GridFadeStart) / (_GridFadeEnd - _GridFadeStart));
                
                gridIntensity *= fadeFactor;
                
                bool inHighlight = IsInHighlightArea(gridPos);
                fixed4 gridColorToUse = _GridColor;
                if (inHighlight) {
                    gridColorToUse = _IsValidPlacement > 0.5 ? _HighlightColor : _InvalidColor;
                }
                
                float intensityToUse = inHighlight ? gridIntensity * _HighlightIntensity : gridIntensity * _GridColor.a;
                terrainColor = lerp(terrainColor, gridColorToUse, intensityToUse);
            }

            o.Albedo = terrainColor.rgb;
            o.Smoothness = _Smoothness;
            o.Metallic = 0;
        }
        ENDCG
    }
}