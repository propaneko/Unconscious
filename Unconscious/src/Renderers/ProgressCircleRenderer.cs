using Unconscious.src.Player;
using Vintagestory.API.Client;

public class ProgressCircleRenderer : IRenderer
{
    private readonly ReviveBehavior behavior;
    private readonly ICoreClientAPI capi;

    public double RenderOrder => 1.0;
    public int RenderRange => 100;

    public ProgressCircleRenderer(ReviveBehavior behavior, ICoreClientAPI capi)
    {
        this.behavior = behavior;
        this.capi = capi;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Ortho || !behavior.entity.WatchedAttributes.GetBool("unconscious"))
        {
            return;
        }

        float reviveProgress = behavior.entity.WatchedAttributes.GetFloat("reviveProgress", 0f);
        float carryProgress = behavior.entity.WatchedAttributes.GetFloat("carryProgress", 0f);

        if (reviveProgress > 0 || carryProgress > 0)
        {
            behavior.RenderProgressCircle(reviveProgress > 0 ? reviveProgress : carryProgress);
        }
    }

    public void Dispose()
    {
        // No additional cleanup needed here
    }
}