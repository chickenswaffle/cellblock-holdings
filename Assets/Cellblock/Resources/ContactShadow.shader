// Soft contact shadow ellipses for people (FacilityContactShadows.cs, Claude's). Unlit, alpha-blended, no depth write,
// pulled slightly toward the camera so it never fights the floor. SRP-batcher compatible (a UnityPerMaterial buffer), so
// the many small per-person draws stay cheap. Lives in Resources so player builds keep it.
Shader "Cellblock/ContactShadow"
{
    Properties { _Strength ("Strength", Float) = 1 }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ContactShadow"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _Strength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // A soft radial falloff: darkest under the body, gone at the rim of the quad.
                float2 d = input.uv * 2 - 1;
                half fade = saturate(1 - dot(d, d));
                return half4(0, 0, 0, fade * sqrt(fade) * input.color.a * _Strength);
            }
            ENDHLSL
        }
    }
}
