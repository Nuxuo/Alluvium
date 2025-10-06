Shader "Custom/Terrain" {
    Properties {
        _GrassColour ("Grass Colour", Color) = (0,1,0,1)
        _RockColour ("Rock Colour", Color) = (1,1,1,1)
        _GrassSlopeThreshold ("Grass Slope Threshold", Range(0,1)) = .5
        _GrassBlendAmount ("Grass Blend Amount", Range(0,1)) = .5
        
        _CraterColour ("Crater Colour", Color) = (0.2,0.15,0.1,1)
        _CraterMask ("Crater Mask", 2D) = "black" {}
        _TerrainScale ("Terrain Scale", Float) = 20
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
        fixed4 _CraterColour;
        sampler2D _CraterMask;
        float _TerrainScale;

        void surf (Input IN, inout SurfaceOutputStandard o) {
            // Calculate slope-based color
            float slope = 1 - IN.worldNormal.y;
            float grassBlendHeight = _GrassSlopeThreshold * (1 - _GrassBlendAmount);
            float grassWeight = 1 - saturate((slope - grassBlendHeight) / (_GrassSlopeThreshold - grassBlendHeight));
            fixed4 terrainColor = _GrassColour * grassWeight + _RockColour * (1 - grassWeight);
            
            // Sample crater mask
            float2 uv = (IN.worldPos.xz / _TerrainScale + 1.0) * 0.5;
            float craterMask = tex2D(_CraterMask, uv).r;
            
            // Blend crater color
            o.Albedo = lerp(terrainColor.rgb, _CraterColour.rgb, craterMask);
        }
        ENDCG
    }
}