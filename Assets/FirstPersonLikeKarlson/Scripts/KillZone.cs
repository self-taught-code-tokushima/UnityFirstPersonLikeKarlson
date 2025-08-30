using UnityEngine;

public class KillZone : MonoBehaviour
{
    public GameObject respawnPosition;

    private void OnTriggerEnter(Collider other)
    {
        // Player Tag は Player Capsule についているものとしている
        if (other.CompareTag("Player"))
        {
            other.transform.position = respawnPosition.transform.position;
            other.gameObject.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        }
    }
}