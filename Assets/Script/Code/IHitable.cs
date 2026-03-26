using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IHitable
{
    public void Hit(float damage);
    public void TakeDamage(float damage);
}
