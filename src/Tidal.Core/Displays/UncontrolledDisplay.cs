namespace Tidal.Core.Displays;

/// <summary>A display Windows reports but gives Tidal no hardware channel to.</summary>
public sealed class UncontrolledDisplay : Display
{
    internal UncontrolledDisplay(DisplayInfo info, string note) : base(info) => Note = note;

    public override ControlMethod Method => ControlMethod.None;

    internal override Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override Task<bool> RefreshAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

    private protected override void WriteBrightness(int percent) => throw new NotSupportedException();
}
