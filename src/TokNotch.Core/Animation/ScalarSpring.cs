namespace TokNotch.Core.Animation;

/// <summary>Exact damped spring. Retargeting preserves position and velocity; damping below one permits recoil.</summary>
public sealed class ScalarSpring
{
    public double Value { get; private set; }
    public double Velocity { get; private set; }
    public double Target { get; private set; }
    public double AngularFrequency { get; private set; } = 24;
    public double DampingRatio {get;private set;}=1;
    public bool IsSettled => Math.Abs(Value - Target) < .0005 && Math.Abs(Velocity) < .004;
    public void Retarget(double target, double angularFrequency = 24,double dampingRatio=1)
    {
        if (!double.IsFinite(target)) throw new ArgumentOutOfRangeException(nameof(target));
        if (!double.IsFinite(angularFrequency) || angularFrequency <= 0) throw new ArgumentOutOfRangeException(nameof(angularFrequency));
        if(!double.IsFinite(dampingRatio)||dampingRatio<=0||dampingRatio>1)throw new ArgumentOutOfRangeException(nameof(dampingRatio));
        Target = target; AngularFrequency = angularFrequency;DampingRatio=dampingRatio;
    }
    public void Snap(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Value = Target = value; Velocity = 0;
    }
    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        var offset = Value - Target;
        if(DampingRatio<.99999)
        {
            var decayRate=DampingRatio*AngularFrequency;
            var frequency=AngularFrequency*Math.Sqrt(1-DampingRatio*DampingRatio);
            var decayFactor=Math.Exp(-decayRate*seconds);var cosine=Math.Cos(frequency*seconds);var sine=Math.Sin(frequency*seconds);
            var previousVelocity=Velocity;
            Value=Target+decayFactor*(offset*cosine+(previousVelocity+decayRate*offset)/frequency*sine);
            Velocity=decayFactor*(previousVelocity*cosine-(decayRate*previousVelocity+AngularFrequency*AngularFrequency*offset)/frequency*sine);
            return;
        }
        var coefficient = Velocity + AngularFrequency * offset;
        var decay = Math.Exp(-AngularFrequency * seconds);
        Value = Target + (offset + coefficient * seconds) * decay;
        Velocity = (Velocity - AngularFrequency * coefficient * seconds) * decay;
    }
}
