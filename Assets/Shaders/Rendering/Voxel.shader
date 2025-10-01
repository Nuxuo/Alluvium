Shader "Custom/Voxel" {
    Properties {
        [Header(Block Colors)]
        _DirtColor ("Dirt Color", Color) = (0.4, 0.3, 0.2, 1)
        _SandColor ("Sand Color", Color) = (0.85, 0.75, 0.55, 1)
        _SnowColor ("Snow Color", Color) = (0.95, 0.95, 1.0, 1)
        _RockColor ("Rock Color", Color) = (0.3, 0.3, 0.35, 1)
        
        [Header(Shading)]
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.3
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
    }
    
    SubShader {
        Tags { "RenderType"="Opaque" }
        
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        
        struct Input {
            float3 worldPos;
            float3 worldNormal;
            float4 color : COLOR; // Vertex colors contain block type
        };
        
        fixed4 _DirtColor;
        fixed4 _SandColor;
        fixed4 _SnowColor;
        fixed4 _RockColor;
        half _AmbientStrength;
        half _Smoothness;
        
        void vert(inout appdata_full v) {
            // Pass through vertex colors
        }
        
        void surf(Input IN, inout SurfaceOutputStandard o) {
            fixed4 blockColor;
            half smoothness;
            
            // Decode block type from vertex color
            // R=Sand, G=Snow, B=Dirt, Black=Rock
            if (IN.color.r > 0.5) {
                // Sand
                blockColor = _SandColor;
                smoothness = _Smoothness * 0.5; // Sand is less smooth
            }
            else if (IN.color.g > 0.5) {
                // Snow
                blockColor = _SnowColor;
                smoothness = _Smoothness * 0.3; // Snow is rough
            }
            else if (IN.color.r < 0.1 && IN.color.g < 0.1 && IN.color.b < 0.1) {
                // Rock (black color)
                blockColor = _RockColor;
                smoothness = _Smoothness * 0.2; // Rock is very rough
                
                // Add some rocky texture variation
                float noise = frac(sin(dot(IN.worldPos * 2.0, float3(12.9898, 78.233, 45.164))) * 43758.5453);
                blockColor.rgb *= (0.85 + noise * 0.3);
            }
            else {
                // Dirt (default)
                blockColor = _DirtColor;
                smoothness = _Smoothness;
            }
            
            // Add subtle shading variation based on world position
            float noise = frac(sin(dot(IN.worldPos.xz, float2(12.9898, 78.233))) * 43758.5453);
            blockColor.rgb *= (0.95 + noise * 0.1);
            
            o.Albedo = blockColor.rgb;
            o.Smoothness = smoothness;
            o.Metallic = 0.0;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    
    FallBack "Diffuse"
}