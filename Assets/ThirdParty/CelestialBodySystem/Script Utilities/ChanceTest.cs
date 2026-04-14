using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ChanceTest : MonoBehaviour {
    public Chance chance;
    public int total;

    void Update () {
        if (Input.GetKeyDown (KeyCode.Space)) {
            chance = new Chance (new System.Random ());
            total++;
            if (chance.Percent (10)) {
                Debug.Log ("1");
            }
            if (chance.Percent (70)) {
                Debug.Log ("7");
            }
            if (chance.Percent (20)) {
                Debug.Log ("2");
            }

        }
    }
}
