Shader "Custom/VoxelTerrain"
{
    Properties
    {
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.3
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            float _Smoothness;
            float _AmbientStrength;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Normalize the normal
                float3 normal = normalize(i.worldNormal);
                
                // Main light direction
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                
                // Diffuse lighting
                float ndotl = max(0, dot(normal, lightDir));
                
                // Ambient lighting
                float ambient = _AmbientStrength;
                
                // Combine lighting
                float lighting = ambient + (1.0 - ambient) * ndotl;
                
                // Apply lighting to vertex color
                fixed4 finalColor = i.color * lighting;
                finalColor.a = 1.0;
                
                return finalColor;
            }
            ENDCG
        }
        
        // Shadow casting pass
        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            
            struct v2f {
                V2F_SHADOW_CASTER;
            };
            
            v2f vert(appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}