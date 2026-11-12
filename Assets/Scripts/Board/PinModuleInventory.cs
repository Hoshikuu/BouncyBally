using System.Collections.Generic;
using UnityEngine;

/// <summary>Colección única de modificadores. Equipar reserva una unidad; retirar la devuelve.</summary>
public class PinModuleInventory : MonoBehaviour
{
    private readonly List<PinModifierDefinition> owned = new List<PinModifierDefinition>();
    private readonly Dictionary<Pin, PinModifierDefinition> equipped = new Dictionary<Pin, PinModifierDefinition>();
    public IReadOnlyList<PinModifierDefinition> OwnedModules => owned;
    public event System.Action Changed;
    public int EquippedCount => equipped.Count;
    public bool Grant(PinModifierDefinition modifier)
    {
        if (modifier == null || owned.Exists(x => x != null && x.id == modifier.id)) return false;
        owned.Add(modifier);
        Changed?.Invoke();
        return true;
    }
    public int Available(PinModifierDefinition modifier)
    {
        if (modifier == null || !owned.Contains(modifier)) return 0;
        foreach (var pair in equipped) if (pair.Value == modifier) return 0;
        return 1;
    }
    public bool Equip(Pin pin, PinModifierDefinition modifier)
    {
        GameManager game = GameManager.Instance;
        if (game == null || !game.WorkshopUnlocked || pin == null || pin.Modifier == modifier) return false;
        if (modifier != null && (Available(modifier) == 0 ||
            (pin.Modifier == null && EquippedCount >= game.ModuleSlots))) return false;
        equipped.Remove(pin);
        if (modifier != null) equipped[pin] = modifier;
        pin.SetModifier(modifier);
        Changed?.Invoke();
        return true;
    }
    public void ReleaseAll()
    {
        foreach (var pair in equipped) if (pair.Key != null) pair.Key.SetModifier(null);
        equipped.Clear();
        Changed?.Invoke();
    }
}
