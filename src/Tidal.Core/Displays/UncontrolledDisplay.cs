namespace Tidal.Core.Displays;

/// <summary>A display the operating system reports but gives Tidal no hardware channel to.</summary>
public sealed class UncontrolledDisplay : Display
{
    public UncontrolledDisplay(DisplayInfo info, string note) : base(info) => Note = note;

    public override ControlMethod Method => ControlMethod.None;

    protected internal override Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override Task<bool> RefreshAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

    protected override void WriteBrightness(int percent) => throw new NotSupportedException();
}
