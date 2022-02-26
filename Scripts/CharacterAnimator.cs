using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1)]
[RequireComponent(typeof(Animator))]
public class CharacterAnimator : MonoBehaviour
{
    public CharacterController2D controller;

    Animator anim;

    private void Awake() {
        anim = GetComponent<Animator>();
    }

    private void Update() {
        anim.SetBool("isWalking", controller.walkSpeed != 0);
    }
}
