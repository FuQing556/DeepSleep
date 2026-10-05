Shader "DeepSleep/PlayerHitOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #pragma multi_compile_instancing
            Varyings vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS=UnityFlipSprite(input.positionOS,unity_SpriteProps.xy);
                Varyings o=CommonUnlitVertex(input);o.color=input.color*unity_SpriteColor;return o;
            }
            half4 frag(Varyings i) : SV_Target
            { return half4(i.color.rgb, SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a*i.color.a); }
            ENDHLSL
        }
    }
}
