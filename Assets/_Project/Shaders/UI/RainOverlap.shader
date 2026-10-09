Shader "DeepSleep/UI/Rain Overlap"
{
    Properties
    {
        [PerRendererData] _MainTex ("Rain", 2D) = "white" {}
        _Overlap ("Vertical overlap", Range(0.01,0.45)) = 0.12
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            sampler2D _MainTex;
            float _Overlap;
            Output vert(Input v)
            {
                Output o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            float4 premultiply(float4 c) { c.rgb *= c.a; return c; }
            float4 frag(Output i) : SV_Target
            {
                float stride = 1 - _Overlap;
                float y = frac(i.uv.y / stride) * stride;
                float x = 1 - abs(frac(i.uv.x * 0.5) * 2 - 1);
                float4 current = premultiply(tex2D(_MainTex, float2(x,y)));
                float4 previous = premultiply(tex2D(_MainTex, float2(x,min(1,y+stride))));
                float4 c = lerp(previous,current,smoothstep(0,_Overlap,y));
                c.rgb *= i.color.rgb * i.color.a;
                c.a *= i.color.a;
                return c;
            }
            ENDHLSL
        }
    }
}
