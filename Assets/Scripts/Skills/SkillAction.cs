using UnityEngine;

/// <summary>
/// Extensión opcional para skills con comportamiento propio. El controlador valida y cobra una vez.
/// No modificar el asset durante Play: guardar el estado de partida en GameManager u otro componente.
/// </summary>
public abstract class SkillAction : ScriptableObject
{
    public virtual bool CanApply(GameManager game, SkillDefinition skill) => game != null;
    public abstract void Apply(GameManager game, SkillDefinition skill);
}
