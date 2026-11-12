using System.Collections;
using UnityEngine;

/// <summary>Puntos de extensión del recorrido; no realiza pagos ni altera el árbol.</summary>
public class BallSpecial : BallBase
{
    public virtual float SpeedFactor => 1f;
    public virtual float SizeFactor => 1f;
    public virtual float BounceFactor => 1f;
    public virtual int Contacts => 1;
    public virtual bool Direction(int row, bool right) => right;
    public virtual void Contact(Pin pin) { }
    public virtual IEnumerator Resolve(BallController controller, System.Action<double> done)
    { done(1d); yield break; }
}
