using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController2D))]
public class CharacterInput : MonoBehaviour
{
    CharacterController2D controller;

    private void Awake() {
        controller = GetComponent<CharacterController2D>();
    }

    void Update() {
        controller.movementInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")); // Get horizontal movement input
        if (Input.GetButtonDown("Jump")) controller.OnJumpButton(); // Register jump button pressed
        if (Input.GetKeyDown(KeyCode.C)) controller.OnDashButton(); // Register dash button pressed
    }
}
