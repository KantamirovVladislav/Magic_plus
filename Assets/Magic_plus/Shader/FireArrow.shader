// VFX/FireArrow — пламенный клинок-стрела (Sukuna-style)
// Маски из Blender (vertex color "FireMask"): R = амплитуда шевеления, G = ядро/край, B = градиент сокет->остриё
// UV: x = поперёк ленты, y = вдоль (сокет->остриё / вдоль языка)
Shader "VFX/FireArrow"
{
    Properties
    {
        [HDR] _CoreColor ("Core Color (white-hot)", Color) = (1.0, 0.95, 0.55, 1)
        [HDR] _MidColor  ("Mid Color (orange)",     Color) = (1.0, 0.38, 0.03, 1)
        [HDR] _EdgeColor ("Edge Color (deep red)",  Color) = (0.45, 0.01, 0.0, 1)
        [HDR] _FlickColor("Magenta Flicks",         Color) = (0.85, 0.08, 1.0, 1)
        _Intensity  ("Emission Intensity", Range(0, 12))  = 4.0
        _Draw       ("Draw 0..1 (from script)", Range(0, 1.2)) = 0.0
        _FlowSpeed  ("Flame Flow Speed", Range(0, 6))     = 1.6
        _NoiseScale ("Noise Scale", Range(1, 40))         = 9.0
        _EdgeSoft   ("Edge Softness", Range(0.01, 1.0))   = 0.35
        _Wobble     ("Vertex Wobble", Range(0, 0.2))      = 0.05
        _WobbleSpeed("Wobble Speed", Range(0, 10))        = 6.0
        _FlickAmount("Magenta Flick Amount", Range(0, 1)) = 0.35
        _Alpha      ("Overall Alpha", Range(0, 2))        = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One          // аддитив — как в референсе
        Cull Off               // двусторонний (лента)
        ZWrite Off
        Lighting Off

        Pass
        {
            Name "FireArrow"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;        // FireMask
            };
            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 mask  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)   // SRP Batcher compatible
                half4 _CoreColor, _MidColor, _EdgeColor, _FlickColor;
                half  _Intensity, _Draw, _FlowSpeed, _NoiseScale;
                half  _EdgeSoft, _Wobble, _WobbleSpeed, _FlickAmount, _Alpha;
            CBUFFER_END

            // ---- value noise + fbm ----
            float hash(float2 p){ return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453123); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f*f*(3.0-2.0*f);
                return lerp(lerp(hash(i),            hash(i+float2(1,0)), u.x),
                            lerp(hash(i+float2(0,1)), hash(i+float2(1,1)), u.x), u.y);
            }
            float fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                [unroll] for (int k = 0; k < 3; k++) { v += a*vnoise(p); p = p*2.03 + 17.7; a *= 0.5; }
                return v;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float t = _Time.y;
                float amp = _Wobble * v.color.r * (0.4 + _Draw);      // R-маска: сокет спокойный, остриё/языки пляшут
                float nA  = fbm(v.uv * float2(3.0, 6.0) + t * _WobbleSpeed * 0.30);
                float nB  = fbm(v.uv * float2(5.0, 9.0) - t * _WobbleSpeed * 0.22 + 3.1);
                v.vertex.xyz += v.normal * ((nA - 0.5) * 2.0 * amp);  // флажок по нормали
                v.vertex.z   += (nB - 0.5) * 2.0 * amp * 0.6;         // боковое «лизание» в плоскости
                o.pos  = UnityObjectToClipPos(v.vertex);
                o.uv   = v.uv;
                o.mask = v.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                // поток пламени вдоль V (к острию), скорость растёт с натягом
                float2 flowUV = float2(i.uv.x * 2.0, i.uv.y * 3.0 - t * _FlowSpeed * (0.6 + 0.8 * _Draw));
                float n1 = fbm(flowUV * _NoiseScale * 0.35);
                float n2 = fbm(flowUV * _NoiseScale * 0.70 + n1 * 1.5 + 5.3);   // domain warp = «живые» языки

                float core = i.mask.g;                                   // 1 центр -> 0 кромки
                float body = n2 * 0.55 + core * 0.65;                    // форма силуэта
                float alpha = smoothstep(0.55, 0.55 + _EdgeSoft, body) * _Alpha;

                // тепло: ядро + шум + остриё (B) + натяг
                float heat = saturate(core * 0.7 + n2 * 0.5 + i.mask.b * 0.3 + _Draw * 0.25);
                half3 col = lerp(_EdgeColor, _MidColor,  smoothstep(0.15, 0.55, heat));
                col       = lerp(col,          _CoreColor, smoothstep(0.55, 0.85, heat));

                // редкие пурпурные проблески, как на арте
                float flick = smoothstep(0.86, 0.95, vnoise(i.uv * float2(6.0, 10.0) + t * 2.0));
                col = lerp(col, _FlickColor, flick * _FlickAmount);

                col *= _Intensity * (0.6 + heat + 1.4 * _Draw);          // HDR-разгорание при натяге
                return half4(col * alpha, alpha);                        // Blend One One
            }
            ENDHLSL
        }
    }
}