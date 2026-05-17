using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

[Title( "Ultimate Light Manager" )]
[Category( "Light" )]
[Icon( "tungsten" )]
public class UltimateLightManager : Component, Component.ExecuteInEditor
{
    public static List<UltimateLightManager> AllLights = new();

    // =========================================================
    // 0. SETUP & NETWORKING
    // =========================================================
    public enum LightTypeEnum { Point, Spot }

    [Property, Order( 0 ), Group( "Setup" )] 
    public LightTypeEnum TargetLightType { get; set; } = LightTypeEnum.Point;

    [Property, Group( "General" ), Sync] 
    public bool IsEnabled { get; set; } = true;

    [Property, Group( "General" ), Sync] 
    public Color LightColor { get; set; } = Color.White;

    [Property, Group( "General" ), Range( 0, 100 ), Sync] 
    public float Brightness { get; set; } = 1.0f;

    [Property, Group( "General" ), Range( 0, 10 )]
    [Description("Intensité des God Rays (Brouillard volumétrique)")]
    public float VolumetricBoost { get; set; } = 1.0f;

    [Property, Group( "General" )] 
    public bool CastShadows { get; set; } = true;

    // =========================================================
    // 1. OPTIMIZATIONS
    // =========================================================
    [Property, Group( "Optimization" )] public float MaxDistance { get; set; } = 2500.0f;
    [Property, Group( "Optimization" )] public float ShadowMaxDistance { get; set; } = 800.0f;
    [Property, Group( "Optimization" )] public bool EnableCulling { get; set; } = true;

    private int _lastKelvin = -1;
    private Color _cachedKelvinColor;
    private Rotation _baseRotation;
    private bool _isInitialized = false;

    // =========================================================
    // 2. FEATURES
    // =========================================================

    // --- PATTERN (QUAKE STYLE) ---
    [Property, FeatureEnabled( "Flicker Pattern" )] public bool EnablePattern { get; set; } = false;
    [Property, Feature( "Flicker Pattern" )] 
    [Description("a = 0% (éteint), m = 100% (normal), z = 200% (surcharge)")]
    public string Pattern { get; set; } = "mmnmmommommnonmmonqnmmo";
    [Property, Feature( "Flicker Pattern" )] public float PatternSpeed { get; set; } = 10.0f;

    // --- PROXIMITY SENSOR ---
    [Property, FeatureEnabled( "Proximity Sensor" )] public bool EnableSensor { get; set; } = false;
    [Property, Feature( "Proximity Sensor" )] public float SensorRange { get; set; } = 300.0f;
    [Property, Feature( "Proximity Sensor" )] public bool InvertSensor { get; set; } = false;

    // --- MOTION SWAY ---
    [Property, FeatureEnabled( "Motion Sway" )] public bool EnableSway { get; set; } = false;
    [Property, Feature( "Motion Sway" )] public float SwaySpeed { get; set; } = 1.0f;
    [Property, Feature( "Motion Sway" )] public float SwayIntensity { get; set; } = 4.0f;

    // --- RAINBOW ---
    [Property, FeatureEnabled( "Rainbow Cycle" )] public bool EnableRainbow { get; set; } = false;
    [Property, Feature( "Rainbow Cycle" )] public float RainbowSpeed { get; set; } = 1.0f;

    // --- PULSE & STROBE ---
    [Property, FeatureEnabled( "Pulse" )] public bool EnablePulse { get; set; } = false;
    [Property, Feature( "Pulse" )] public float PulseSpeed { get; set; } = 1.0f;
    [Property, FeatureEnabled( "Strobe" )] public bool EnableStrobe { get; set; } = false;
    [Property, Feature( "Strobe" )] public float StrobeSpeed { get; set; } = 10.0f;

    // --- OTHERS ---
    [Property, FeatureEnabled( "Kelvin" )] public bool EnableKelvin { get; set; } = false;
    [Property, Feature( "Kelvin" ), Range( 1000, 12000 )] public int KelvinTemperature { get; set; } = 4500;
    [Property, FeatureEnabled( "Fire" )] public bool EnableFire { get; set; } = false;
    [Property, FeatureEnabled( "Broken Bulb" )] public bool EnableBrokenBulb { get; set; } = false;
    [Property, FeatureEnabled( "Curve" )] public bool EnableCurve { get; set; } = false;
    [Property, Feature( "Curve" )] public Curve FlickerCurve { get; set; }

    private PointLight _pointLight;
    private SpotLight _spotLight;
    private Light ActiveLight => (TargetLightType == LightTypeEnum.Point) ? (Light)_pointLight : (Light)_spotLight;

    private float _brokenMultiplier = 1.0f;
    private float _nextFlicker;
    private float _sensorWeight = 1.0f;

    protected override void OnStart()
    {
        if ( !AllLights.Contains( this ) ) AllLights.Add( this );
        _baseRotation = LocalRotation;
        _isInitialized = true;
        SyncComponents();
    }

    protected override void OnUpdate()
    {
        if ( !_isInitialized ) return;
        SyncComponents();
        
        var light = ActiveLight;
        if ( light == null ) return;

        bool isPlaying = Game.IsPlaying;
        float currentTime = isPlaying ? Time.Now : RealTime.Now;

        // --- 1. SENSOR & DISTANCE ---
        var viewerCam = Scene.Camera ?? Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
        float distSq = viewerCam != null ? WorldPosition.DistanceSquared( viewerCam.WorldPosition ) : 0;

        if ( EnableSensor && viewerCam != null )
        {
            float ratio = Math.Clamp( 1.0f - (MathF.Sqrt(distSq) / SensorRange), 0, 1 );
            _sensorWeight = InvertSensor ? 1.0f - ratio : ratio;
        }
        else _sensorWeight = 1.0f;

        if ( isPlaying && EnableCulling && viewerCam != null && distSq > MaxDistance * MaxDistance ) 
        { 
            light.Enabled = false; 
            return; 
        }

        // --- 2. TRANSFORM (SWAY) ---
        if ( EnableSway )
        {
            float p = MathF.Sin( currentTime * SwaySpeed ) * SwayIntensity;
            float r = MathF.Cos( currentTime * SwaySpeed * 0.73f ) * SwayIntensity;
            LocalRotation = _baseRotation * Rotation.From( p, 0, r );
        }

        // --- 3. INTENSITY CALCULATION ---
        float fx = 1.0f;
        
        if ( EnableStrobe ) fx = (MathF.Sin( currentTime * StrobeSpeed * 25f ) > 0) ? 1.0f : 0.0f;
        else if ( EnablePulse ) fx = (MathF.Sin( currentTime * PulseSpeed * 2.5f ) + 1.2f) / 2.2f;

        if ( EnablePattern && !string.IsNullOrEmpty( Pattern ) )
        {
            int index = (int)(currentTime * PatternSpeed) % Pattern.Length;
            char c = char.ToLower(Pattern[index]);
            // 'm' correspond à 100%. L'écart entre 'a' et 'm' est de 12.
            float val = Math.Max(0, (c - 'a') / 12.0f);
            fx *= val;
        }

        if ( EnableFire ) fx += MathF.Sin( currentTime * 12f ) * 0.15f;
        if ( EnableCurve ) fx *= FlickerCurve.Evaluate( currentTime % 1.0f );

        if ( EnableBrokenBulb )
        {
            if ( currentTime > _nextFlicker )
            {
                _brokenMultiplier = (Game.Random.Float( 0, 1 ) > 0.85f) ? Game.Random.Float( 0, 0.2f ) : 1.0f;
                _nextFlicker = currentTime + Game.Random.Float( 0.05f, 0.2f );
            }
            fx *= _brokenMultiplier;
        }

        // --- 4. FINAL APPLICATION ---
        float finalBrightness = Brightness * fx * _sensorWeight * (IsEnabled ? 1 : 0);
        
        if ( light.Enabled != (finalBrightness > 0.001f) ) light.Enabled = finalBrightness > 0.001f;

        Color col = LightColor;
        if ( EnableRainbow ) col = new ColorHsv( (currentTime * RainbowSpeed * 40f) % 360f, 1, 1 ).ToColor();
        else if ( EnableKelvin )
        {
            if ( KelvinTemperature != _lastKelvin ) { _cachedKelvinColor = KelvinToColor( KelvinTemperature ); _lastKelvin = KelvinTemperature; }
            col = _cachedKelvinColor;
        }

        light.LightColor = col * finalBrightness;
        light.Shadows = CastShadows && (!isPlaying || distSq < ShadowMaxDistance * ShadowMaxDistance);
        light.FogStrength = VolumetricBoost;
    }

    private void SyncComponents()
    {
        if ( TargetLightType == LightTypeEnum.Point )
        {
            if ( _pointLight == null ) _pointLight = Components.GetOrCreate<PointLight>();
            if ( _spotLight != null ) _spotLight.Enabled = false;
        }
        else
        {
            if ( _spotLight == null ) _spotLight = Components.GetOrCreate<SpotLight>();
            if ( _pointLight != null ) _pointLight.Enabled = false;
        }
    }

    private Color KelvinToColor( int k )
    {
        float t = k / 100.0f;
        float r, g, b;
        if ( t <= 66 ) r = 255; else r = Math.Clamp( 329.698f * MathF.Pow( t - 60, -0.133f ), 0, 255 );
        if ( t <= 66 ) g = Math.Clamp( 99.47f * MathF.Log( t ) - 161.11f, 0, 255 ); else g = Math.Clamp( 288.12f * MathF.Pow( t - 60, -0.075f ), 0, 255 );
        if ( t >= 66 ) b = 255; else if ( t <= 19 ) b = 0; else b = Math.Clamp( 138.51f * MathF.Log( t - 10 ) - 305.04f, 0, 255 );
        return new Color( r / 255f, g / 255f, b / 255f );
    }

    protected override void OnDestroy() { if ( AllLights.Contains( this ) ) AllLights.Remove( this ); }
}