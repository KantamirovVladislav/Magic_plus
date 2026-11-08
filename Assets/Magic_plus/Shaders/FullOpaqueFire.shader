// Standalone version of the FullOpaqueFire particle colour graph.
// Uses the existing UV + Custom1XY streams (temperature in TEXCOORD0.w).
// No Shader Graph / URP runtime passes or external textures are required.
Shader "MagicPlus/FullOpaqueFire"
{
    Properties
    {
        _Intensity ("Fire intensity", Range(0, 4)) = 1
        _Temperature ("Fallback temperature", Float) = 6000
        _HueOffset ("Hue offset in degrees", Range(-180, 180)) = -10
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "IgnoreProjector"="True" "MagicPlusBuiltin"="True" }
        Cull [_Cull]
        ZWrite On
        ZTest LEqual
        Pass
        {
            // An untagged unlit pass is usable by Built-in and URP's
            // SRPDefaultUnlit path, so the asset also previews in this project.
            Name "MagicPlusFire"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float _Intensity, _Temperature, _HueOffset;
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
                float temperature : TEXCOORD0;
            };
            float3 rgbToHsv(float3 value)
            {
                float4 k = float4(0, -1.0 / 3.0, 2.0 / 3.0, -1);
                float4 p = lerp(float4(value.bg, k.wz), float4(value.gb, k.xy), step(value.b, value.g));
                float4 q = lerp(float4(p.xyw, value.r), float4(value.r, p.yzx), step(p.x, value.r));
                float d = q.x - min(q.w, q.y);
                return float3(abs(q.z + (q.w - q.y) / (6 * d + 1e-10)), d / (q.x + 1e-10), q.x);
            }
            float3 hsvToRgb(float3 value)
            {
                float3 p = abs(frac(value.xxx + float3(1, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3);
                return value.z * lerp(float3(1, 1, 1), saturate(p - 1), value.y);
            }
            float3 blackbody(float temperature)
            {
                // Match Shader Graph's Blackbody node, clamping the logarithm
                // and power inputs to keep fading particles finite at zero.
                float t = max(temperature, 1.0);
                float invPower = pow(t, -1.5);
                float3 rgb;
                rgb.r = 56100000 * invPower + 148;
                rgb.g = t > 6500 ? 35200000 * invPower + 184 : 100.04 * log(t) - 623.6;
                rgb.b = 194.18 * log(t) - 1448.6;
                return saturate(rgb / 255) * saturate(temperature / 1000);
            }
            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.temperature = input.uv.w > 0 ? input.uv.w : _Temperature;
                return output;
            }
            float4 frag(v2f input) : SV_Target
            {
                float3 tint = rgbToHsv(max(input.color.rgb, 0));
                float3 heat = rgbToHsv(blackbody(tint.z * input.temperature));
                heat.x = frac(heat.x + tint.x + _HueOffset / 360);
                float3 rgb = hsvToRgb(heat);
                float luminance = dot(rgb, float3(0.2126, 0.7152, 0.0722));
                rgb = lerp(luminance.xxx, rgb, tint.y);
                return float4(max(rgb, 0) * _Intensity, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
