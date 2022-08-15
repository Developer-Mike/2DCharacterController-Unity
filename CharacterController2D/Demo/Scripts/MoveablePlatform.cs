using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveablePlatform : MonoBehaviour
{
    public float speed;
    public float radius;
    public Vector3[] nodes;

    private void Start() {
        transform.position = nodes[0];
        StartCoroutine(Move(0));
    }

    IEnumerator Move(int index) {
        while (Vector3.Distance(transform.position, nodes[index]) > radius) {
            transform.position += (nodes[index] - transform.position).normalized * speed * Time.fixedDeltaTime;

            yield return new WaitForFixedUpdate();
        }

        StartCoroutine(Move((index >= nodes.Length - 1) ? 0 : (index + 1)));
    }

    private void OnDrawGizmosSelected() {
        foreach (Vector3 node in nodes) {
            Gizmos.DrawWireSphere(node, radius);
        }
    }
}
