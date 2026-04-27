Shader "GameMain/HitFlashWhiteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineSize ("Outline Size", Range(0,4)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _FlashColor;
            fixed4 _OutlineColor;
            float _FlashAmount;
            float _OutlineSize;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, input.texcoord) * input.color;
                float2 offset = _MainTex_TexelSize.xy * _OutlineSize;
                fixed outlineAlpha = 0;
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(offset.x, 0)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(-offset.x, 0)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(0, offset.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(0, -offset.y)).a);

                fixed4 outline = _OutlineColor;
                outline.a *= saturate(outlineAlpha - tex.a) * _FlashAmount;

                tex.rgb = lerp(tex.rgb, _FlashColor.rgb, _FlashAmount);
                return lerp(outline, tex, tex.a);
            }
            ENDCG
        }
    }
}
