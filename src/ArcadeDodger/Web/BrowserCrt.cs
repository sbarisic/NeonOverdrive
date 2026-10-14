using System.Numerics;
using Raylib_cs;

namespace ArcadeDodger.Web;

// One bounded world-only pass; HUD and DOM text keep their original rasterization.
internal sealed class BrowserCrt
{
    private Shader shader;
    private RenderTexture2D target;
    private bool attempted,
        ready;
    private int amountLocation,
        pixelLocation;
    public int Style { get; set; } = 1;

    public bool Begin(bool reduced)
    {
        if (Style == 0)
            return false;
        if (!attempted)
        {
            attempted = true;
            try
            {
                shader = Raylib.LoadShaderFromMemory(
                    null,
                    """
                    #version 100
                    precision mediump float;
                    varying vec2 fragTexCoord;
                    varying vec4 fragColor;
                    uniform sampler2D texture0;
                    uniform vec4 colDiffuse;
                    uniform vec2 pixel;
                    uniform float amount;
                    void main() {
                        vec2 uv = fragTexCoord;
                        vec3 c = texture2D(texture0, uv).rgb;
                        vec3 glow = (texture2D(texture0, uv + vec2(pixel.x,0.)).rgb
                                   + texture2D(texture0, uv - vec2(pixel.x,0.)).rgb
                                   + texture2D(texture0, uv + vec2(0.,pixel.y)).rgb
                                   + texture2D(texture0, uv - vec2(0.,pixel.y)).rgb) * .25;
                        float scan = .5 + .5 * sin(uv.y / pixel.y * 3.14159265);
                        vec2 edge = uv * 2. - 1.;
                        float vignette = 1. - .10 * dot(edge,edge) * amount;
                        c = (c + max(glow - .25, 0.) * .12 * amount)
                            * (1. - .035 * scan * amount) * vignette;
                        gl_FragColor = vec4(c, 1.) * colDiffuse * fragColor;
                    }
                    """
                );
                amountLocation = Raylib.GetShaderLocation(shader, "amount");
                pixelLocation = Raylib.GetShaderLocation(shader, "pixel");
                ready = Raylib.IsShaderValid(shader) && amountLocation >= 0 && pixelLocation >= 0;
            }
            catch
            {
                ready = false;
            }
        }
        if (!ready)
            return false;
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        if (target.Texture.Width != width || target.Texture.Height != height)
        {
            if (target.Id != 0)
                Raylib.UnloadRenderTexture(target);
            target = Raylib.LoadRenderTexture(width, height);
        }
        if (target.Id == 0)
            return false;
        Raylib.SetShaderValue(
            shader,
            amountLocation,
            reduced ? .4f
                : Style == 2 ? 1.5f
                : .65f,
            ShaderUniformDataType.Float
        );
        Raylib.SetShaderValue(
            shader,
            pixelLocation,
            new Vector2(1f / width, 1f / height),
            ShaderUniformDataType.Vec2
        );
        Raylib.BeginTextureMode(target);
        Raylib.ClearBackground(new Color(6, 8, 23, 255));
        return true;
    }

    public void End()
    {
        Raylib.EndTextureMode();
        Raylib.BeginShaderMode(shader);
        Raylib.DrawTextureRec(
            target.Texture,
            new(0, 0, target.Texture.Width, -target.Texture.Height),
            Vector2.Zero,
            Color.White
        );
        Raylib.EndShaderMode();
    }

    public void Release()
    {
        if (target.Id != 0)
            Raylib.UnloadRenderTexture(target);
        if (ready)
            Raylib.UnloadShader(shader);
        target = default;
        ready = false;
    }
}
