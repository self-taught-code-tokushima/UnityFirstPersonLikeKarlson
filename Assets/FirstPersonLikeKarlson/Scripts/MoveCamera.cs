// original code: https://github.com/DaniDevy/FPS_Movement_Rigidbody/blob/master/MoveCamera.cs
using UnityEngine;

public class MoveCamera : MonoBehaviour {

    public Transform player;

    void Update() {
        transform.position = player.transform.position;
    }
}