// https://discussions.unity.com/t/extension-methods-for-layermask/950780

using UnityEngine;

public static class LayerMaskExtensions
{
    public static bool Contains(this LayerMask mask, GameObject gob)
    {
        if (gob == null)
            return false;
        return (0 != (mask & (1 << gob.layer)));
    }
}