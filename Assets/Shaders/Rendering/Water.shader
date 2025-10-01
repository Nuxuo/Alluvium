Shader "Custom/Water" {
    Properties {
        _WaterColor ("Water Color", Color) = (0.0, 0.4, 0.7, 0.8)
        _DeepWaterColor ("Deep Water Color", Color) = (0.0, 0.2, 0.5, 1.0)
        _FresnelPower ("Fresnel Power", Range(0.1, 5.0)) = 1.5
        _Transparency ("Transparency", Range(0.0, 1.0)) = 0.8
        _ReflectionStrength ("Reflection Strength", Range(0.0, 1.0)) = 0.5
        
        [Header(Wave Settings)]
        _WaveSpeed ("Wave Speed", Range(0.1, 2.0)) = 0.5
        _WaveScale ("Wave Scale", Range(0.1, 5.0)) = 1.0
        _WaveHeight ("Wave Height", Range(0.0, 1.0)) = 0.2
        _WaveFrequency ("Wave Frequency", Range(0.1, 10.0)) = 2.0
        
        [Header(Foam Settings)]
        _FoamColor ("Foam Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _FoamDistance ("Foam Distance", Range(0.0, 2.0)) = 0.5
        _FoamPower ("Foam Power", Range(0.1, 5.0)) = 2.0
        
        _WaterLevel ("Water Level", Float) = 0.0
    }
    
    SubShader {
        Tags { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }
        
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };
            
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                float depth : TEXCOORD5;
            };
            
            fixed4 _WaterColor;
            fixed4 _DeepWaterColor;
            fixed4 _FoamColor;
            float _FresnelPower;
            float _Transparency;
            float _ReflectionStrength;
            float _WaveSpeed;
            float _WaveScale;
            float _WaveHeight;
            float _WaveFrequency;
            float _FoamDistance;
            float _FoamPower;
            float _WaterLevel;
            
            sampler2D _CameraDepthTexture;
            
            // Simple noise function
            float noise(float2 p) {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            // Smooth noise
            float smoothNoise(float2 p) {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                
                float a = noise(i);
                float b = noise(i + float2(1.0, 0.0));
                float c = noise(i + float2(0.0, 1.0));
                float d = noise(i + float2(1.0, 1.0));
                
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            
            // Generate wave height
            float getWaveHeight(float2 pos, float time) {
                float wave1 = sin((pos.x + pos.y) * _WaveFrequency + time * _WaveSpeed) * 0.5;
                float wave2 = cos((pos.x - pos.y) * _WaveFrequency * 0.7 + time * _WaveSpeed * 1.3) * 0.3;
                float noise1 = smoothNoise(pos * 2.0 + time * 0.1) * 0.2;
                
                return (wave1 + wave2 + noise1) * _WaveHeight;
            }
            
            // Generate wave normal
            float3 getWaveNormal(float2 pos, float time) {
                float epsilon = 0.1;
                float heightL = getWaveHeight(pos - float2(epsilon, 0), time);
                float heightR = getWaveHeight(pos + float2(epsilon, 0), time);
                float heightD = getWaveHeight(pos - float2(0, epsilon), time);
                float heightU = getWaveHeight(pos + float2(0, epsilon), time);
                
                float3 normal = normalize(float3(heightL - heightR, 2.0 * epsilon, heightD - heightU));
                return normal;
            }
            
            v2f vert(appdata v) {
                v2f o;
                
                float time = _Time.y;
                float2 worldXZ = mul(unity_ObjectToWorld, v.vertex).xz;
                
                // Apply wave displacement
                float waveHeight = getWaveHeight(worldXZ * _WaveScale, time);
                v.vertex.y += waveHeight;
                
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                
                // Calculate wave normal
                float3 waveNormal = getWaveNormal(worldXZ * _WaveScale, time);
                o.worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, waveNormal));
                
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                o.screenPos = ComputeScreenPos(o.pos);
                o.depth = COMPUTE_DEPTH_01;
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target {
                // Sample depth texture to detect shoreline
                float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
                float waterDepth = sceneDepth - i.screenPos.w;
                
                // Fresnel effect
                float fresnel = pow(1.0 - saturate(dot(i.worldNormal, i.viewDir)), _FresnelPower);
                
                // Water color based on depth
                fixed4 waterColor = lerp(_DeepWaterColor, _WaterColor, saturate(waterDepth / 5.0));
                
                // Foam effect near shoreline
                float foamFactor = 1.0 - saturate(waterDepth / _FoamDistance);
                foamFactor = pow(foamFactor, _FoamPower);
                
                // Add some animation to foam
                float time = _Time.y;
                float foamNoise = smoothNoise(i.uv * 10.0 + time * 2.0);
                foamFactor *= foamNoise;
                
                // Mix foam with water color
                fixed4 finalColor = lerp(waterColor, _FoamColor, foamFactor);
                
                // Apply fresnel to transparency
                float alpha = lerp(_Transparency, 1.0, fresnel * _ReflectionStrength);
                finalColor.a = alpha;
                
                // Enhance foam visibility
                finalColor.a = saturate(finalColor.a + foamFactor);
                
                return finalColor;
            }
            ENDCG
        }
    }
    
    FallBack "Transparent/Diffuse"
}