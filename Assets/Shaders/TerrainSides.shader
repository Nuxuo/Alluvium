Shader "Custom/TerrainSides" {
    Properties {
        _Color ("Side Color", Color) = (0.3, 0.25, 0.2, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        struct Input {
            float3 worldPos;
        };

        fixed4 _Color;

        void surf (Input IN, inout SurfaceOutputStandard o) {
            o.Albedo = _Color.rgb;
        }
        ENDCG
    }
}