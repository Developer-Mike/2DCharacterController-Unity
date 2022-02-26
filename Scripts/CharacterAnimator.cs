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

    private void OnEnable() {
        controller.jumpEvent.AddListener(OnJump);
        controller.groundEvent.AddListener(OnGrounded);
    }

    private void OnDisable() {
        controller.jumpEvent.RemoveListener(OnJump);
        controller.groundEvent.RemoveListener(OnGrounded);
    }

    private void Update() {
        anim.SetBool("isWalking", controller.walkSpeed != 0);
        anim.SetBool("isFalling", controller.isFalling);
    }

    void OnJump() {
        anim.SetTrigger("jump");
    }

    void OnGrounded() {
        anim.SetTrigger("grounded");
    }
}
