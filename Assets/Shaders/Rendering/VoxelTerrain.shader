Shader "Custom/VoxelTerrain" {
    Properties {
        [Header(Terrain Colors)]
        _GrassColor ("Grass", Color) = (0.4, 0.6, 0.3, 1)
        _SandColor ("Sand", Color) = (0.9, 0.8, 0.6, 1)
        _RockColor ("Rock", Color) = (0.45, 0.4, 0.4, 1)
        _SnowColor ("Snow", Color) = (0.95, 0.95, 1.0, 1)
        _DirtColor ("Dirt", Color) = (0.5, 0.4, 0.3, 1)
        _ClayColor ("Clay", Color) = (0.6, 0.5, 0.4, 1)
        _GravelColor ("Gravel", Color) = (0.5, 0.5, 0.55, 1)
        _TundraColor ("Tundra", Color) = (0.4, 0.5, 0.4, 1)
        
        [Header(Shading)]
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.3
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        
        [Header(Variation)]
        _ColorVariation ("Color Variation", Range(0, 1)) = 0.15
    }
    
    SubShader {
        Tags { "RenderType"="Opaque" }
        
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        
        struct Input {
            float3 worldPos;
            float3 worldNormal;
            float4 color : COLOR;
            float2 uv_MainTex;
        };
        
        // Terrain type colors
        fixed4 _GrassColor;
        fixed4 _SandColor;
        fixed4 _RockColor;
        fixed4 _SnowColor;
        fixed4 _DirtColor;
        fixed4 _ClayColor;
        fixed4 _GravelColor;
        fixed4 _TundraColor;
        
        half _AmbientStrength;
        half _Smoothness;
        half _ColorVariation;
        
        // Terrain type enum matches C#:
        // Grass=0, Sand=1, Rock=2, Snow=3, Dirt=4, Clay=5, Gravel=6, Tundra=7
        
        // Simple noise function for variation
        float hash(float3 p) {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }
        
        void vert(inout appdata_full v) {
            // Pass through
        }
        
        void surf(Input IN, inout SurfaceOutputStandard o) {
            // Decode terrain type from vertex color
            // Using R channel to store terrain type index (0-7)
            int terrainType = round(IN.color.r * 10.0); // Simple encoding
            
            fixed4 baseColor;
            half smoothness = _Smoothness;
            
            // Select color based on terrain type
            if (terrainType == 0) {
                // Grass
                baseColor = _GrassColor;
                smoothness *= 0.6;
            }
            else if (terrainType == 1) {
                // Sand
                baseColor = _SandColor;
                smoothness *= 0.4;
            }
            else if (terrainType == 2) {
                // Rock
                baseColor = _RockColor;
                smoothness *= 0.3;
                
                // Add rocky texture
                float rockNoise = hash(IN.worldPos * 2.0);
                baseColor.rgb *= (0.85 + rockNoise * 0.3);
            }
            else if (terrainType == 3) {
                // Snow
                baseColor = _SnowColor;
                smoothness *= 0.2;
            }
            else if (terrainType == 4) {
                // Dirt
                baseColor = _DirtColor;
                smoothness *= 0.5;
            }
            else if (terrainType == 5) {
                // Clay
                baseColor = _ClayColor;
                smoothness *= 0.4;
            }
            else if (terrainType == 6) {
                // Gravel
                baseColor = _GravelColor;
                smoothness *= 0.3;
                
                // Add gravel texture
                float gravelNoise = hash(IN.worldPos * 3.0);
                baseColor.rgb *= (0.9 + gravelNoise * 0.2);
            }
            else {
                // Tundra (7) or default
                baseColor = _TundraColor;
                smoothness *= 0.5;
            }
            
            // Add subtle world-position based variation
            float variation = hash(IN.worldPos * 0.5);
            baseColor.rgb *= (1.0 - _ColorVariation * 0.5 + variation * _ColorVariation);
            
            // Add some height-based shading
            float heightFactor = saturate(IN.worldPos.y * 0.05);
            baseColor.rgb *= lerp(0.8, 1.1, heightFactor);
            
            // Output
            o.Albedo = baseColor.rgb;
            o.Smoothness = smoothness;
            o.Metallic = 0.0;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    
    FallBack "Diffuse"
}