using UnityEngine;

public class SyrupStickHelper : MonoBehaviour
{
    private Rigidbody rb;
    private bool hasStuck = false;

    public void Init(Rigidbody rigidbody)
    {
        rb = rigidbody;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasStuck) return;

        if (collision.gameObject.CompareTag("Pancake"))
        {
            hasStuck = true;

            transform.position = collision.contacts[0].point + Vector3.up * 0.001f;

            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            transform.localScale *= 1.1f;
        }
    }
}