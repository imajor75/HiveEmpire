float _WorldScale;
int _GameTime;

sampler2D _Wind;

float3 calculateWindAt( float3 worldPos )
{
    fixed2 uv = float2( worldPos.x / _WorldScale * 2 + worldPos.z / _WorldScale, worldPos.z / _WorldScale * 2 );
    return tex2Dlod( _Wind, fixed4( uv + fixed2( _GameTime * 0.004, _GameTime * 0.0015 ), 0, 0 ) ) - 0.5.xxx;
}
