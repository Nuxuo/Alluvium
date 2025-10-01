Shader "Custom/Voxel" {
    Properties {
        [Header(Block Colors)]
        _DirtColor ("Dirt Color", Color) = (0.4, 0.3, 0.2, 1)
        _SandColor ("Sand Color", Color) = (0.85, 0.75, 0.55, 1)
        _SnowColor ("Snow Color", Color) = (0.95, 0.95, 1.0, 1)
        
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
        half _AmbientStrength;
        half _Smoothness;
        
        void vert(inout appdata_full v) {
            // Pass through vertex colors
        }
        
        void surf(Input IN, inout SurfaceOutputStandard o) {
            // Decode block type from vertex color
            fixed4 blockColor;
            
            // Check which channel is dominant (R=Sand, G=Snow, B=Dirt)
            if (IN.color.r > 0.5) {
                // Sand
                blockColor = _SandColor;
                o.Smoothness = _Smoothness * 0.5; // Sand is less smooth
            }
            else if (IN.color.g > 0.5) {
                // Snow
                blockColor = _SnowColor;
                o.Smoothness = _Smoothness * 0.3; // Snow is rough
            }
            else {
                // Dirt (default)
                blockColor = _DirtColor;
                o.Smoothness = _Smoothness;
            }
            
            // Add some subtle shading variation based on world position
            float noise = frac(sin(dot(IN.worldPos.xz, float2(12.9898, 78.233))) * 43758.5453);
            blockColor.rgb *= (0.95 + noise * 0.1);
            
            o.Albedo = blockColor.rgb;
            o.Metallic = 0.0;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    
    FallBack "Diffuse"
}