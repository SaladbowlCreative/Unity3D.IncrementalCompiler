using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;
using UnityEngine;
using GenerationAttributes;

public class TestScript : MonoBehaviour {

	// Use this for initialization
    void Start() {
        var test = new ToStringEnumerableTestClass();
        var enumerable = Enumerable.Repeat(0, 10);
        // Debug.Log(Helpers.EnumerableToString(enumerable));
        Debug.LogWarning(test.ToString());
    }

	// Update is called once per frame
	void Update () {

	}
}
