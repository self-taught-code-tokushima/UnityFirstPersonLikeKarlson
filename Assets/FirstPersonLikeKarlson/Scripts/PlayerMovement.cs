// original code: https://github.com/DaniDevy/FPS_Movement_Rigidbody/blob/master/PlayerMovement.cs
/*
MIT License

Copyright (c) 2020 DaniDevy

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
// Some stupid rigidbody based movement by Dani

using System;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerMovement : MonoBehaviour {

    //Assingables
    public Transform playerCam;
    public Transform orientation;
    
    //Other
    private Rigidbody rb;

    //Rotation and look
    private float xRotation;
    private float sensitivity = 50f;
    private float sensMultiplier = 1f;
    
    //Movement
    public float moveSpeed = 4500;
    public float maxSpeed = 20;
    public bool grounded;
    public LayerMask whatIsGround;
    
    public float counterMovement = 0.175f;
    private float threshold = 0.01f;
    public float maxSlopeAngle = 35f;

    //Crouch & Slide
    private Vector3 crouchScale = new Vector3(1, 0.5f, 1);
    private Vector3 playerScale;
    public float slideForce = 400;
    public float slideCounterMovement = 0.2f;

    //Jumping
    private bool readyToJump = true;
    private float jumpCooldown = 0.25f;
    public float jumpForce = 550f;
    
    //Input
    float x, y;
    bool jumping, sprinting, crouching;
#if ENABLE_INPUT_SYSTEM
    private PlayerInput _playerInput;
#endif
    private PlayerInputs _input;
    
    //Sliding
    private Vector3 normalVector = Vector3.up;
    private Vector3 wallNormalVector;

    void Awake() {
        rb = GetComponent<Rigidbody>();
    }
    
    void Start() {
        playerScale =  transform.localScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        _input = GetComponent<PlayerInputs>();
#if ENABLE_INPUT_SYSTEM
        _playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "missing dependencies");
#endif
    }

    
    private void FixedUpdate() {
        Movement();
    }

    private void Update() {
        MyInput();
        Look();
    }

    /// <summary>
    /// Find user input. Should put this in its own class but im lazy
    /// </summary>
    private void MyInput() {
        x = _input.move.x;
        y = _input.move.y;
        jumping = _input.jump;
        crouching = _input.crouch;
      
        //Crouching
        if (_input.crouchPressed)
            StartCrouch();
        if (_input.crouchReleased)
            StopCrouch();
    }

    /// <summary>
    /// プレイヤーがしゃがみ始めたときの処理
    /// プレイヤーのコライダーを縮小し、移動している場合は前方にブーストを与える
    /// </summary>
    private void StartCrouch() {
        transform.localScale = crouchScale; // プレイヤーモデルを縮小します。
        transform.position = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z); // プレイヤーの位置を下げます。
        
        // 地面を移動している場合は、前方に力を加えてスライドを開始します。
        if (rb.linearVelocity.magnitude > 0.5f) {
            if (grounded) {
                rb.AddForce(orientation.transform.forward * slideForce);
            }
        }
    }

    /// <summary>
    /// プレイヤーがしゃがむのをやめたときの処理
    /// プレイヤーのコライダーのサイズと位置をリセットする
    /// </summary>
    private void StopCrouch() {
        transform.localScale = playerScale;  // プレイヤーモデルの高さを元に戻します。
        transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
    }

    private void Movement() {
        //Extra gravity
        // 重力をシミュレートするために一定の下向きの力を適用している
        // 10 は調整値
        rb.AddForce(Vector3.down * Time.deltaTime * 10);
        
        //Find actual velocity relative to where player is looking
        // プレイヤーが見ている方向に対する速度を求める
        Vector2 mag = FindVelRelativeToLook();
        float xMag = mag.x, yMag = mag.y;

        //Counteract sliding and sloppy movement
        // コントロールをより応答しやすくするために、カウンタームーブメント (逆方向の動作) を適用
        CounterMovement(x, y, mag);
        
        //If holding jump && ready to jump, then jump
        if (readyToJump && jumping) Jump();

        //Set max speed
        float maxSpeed = this.maxSpeed;
        
        //If sliding down a ramp, add force down so player stays grounded and also builds speed
        // 斜道でスライドダウンしている場合は、地面によりよくくっつくように下向きの力を適用
        // 3000 は調整値
        if (crouching && grounded && readyToJump) {
            rb.AddForce(Vector3.down * Time.deltaTime * 3000);
            return;
        }
        
        //If speed is larger than maxspeed, cancel out the input so you don't go over max speed
        // プレイヤーがその方向ですでに最高速度で移動している場合は、移動入力を制限する
        if (x > 0 && xMag > maxSpeed) x = 0;
        if (x < 0 && xMag < -maxSpeed) x = 0;
        if (y > 0 && yMag > maxSpeed) y = 0;
        if (y < 0 && yMag < -maxSpeed) y = 0;

        //Some multipliers
        // プレイヤーの状態（接地、空中、壁走りなど）に基づいて移動力を調整するための乗数
        // 1f = 通常の移動速度
        float multiplier = 1f, multiplierV = 1f;
        
        // Movement in air
        // 空中ではコントロールが低下するということ
        if (!grounded) {
            multiplier = 0.5f;
            multiplierV = 0.5f;
        }
        
        // Movement while sliding
        // スライディング中は前方へのコントロールはできない
        if (grounded && crouching) multiplierV = 0f;

        //Apply forces to move player
        rb.AddForce(orientation.transform.forward * y * moveSpeed * Time.deltaTime * multiplier * multiplierV);
        rb.AddForce(orientation.transform.right * x * moveSpeed * Time.deltaTime * multiplier);
    }

    private void Jump() {
        if (grounded && readyToJump) {
            readyToJump = false;
            _input.jump = false;

            //Add jump forces
            // ジャンプのために強い上向きの力を適用
            rb.AddForce(Vector2.up * jumpForce * 1.5f);
            // 表面の法線から離れる方向に力を適用（例：斜面から押し出す）
            rb.AddForce(normalVector * jumpForce * 0.5f);
            
            //If jumping while falling, reset y velocity.
            // 落下中とジャンプ中に、y軸の速度をリセットします。
            Vector3 vel = rb.linearVelocity;
            if (rb.linearVelocity.y < 0.5f)
                rb.linearVelocity = new Vector3(vel.x, 0, vel.z);
            else if (rb.linearVelocity.y > 0) 
                rb.linearVelocity = new Vector3(vel.x, vel.y / 2, vel.z);
            
            // ジャンプのクールダウンを開始
            Invoke(nameof(ResetJump), jumpCooldown);
        }
    }
    
    private void ResetJump() {
        readyToJump = true;
    }
    
    private float desiredX;
    private void Look() {
        float mouseX = _input.look.x * sensitivity * Time.fixedDeltaTime * sensMultiplier;
        float mouseY = _input.look.y * sensitivity * Time.fixedDeltaTime * sensMultiplier;

        //Find current look rotation
        Vector3 rot = playerCam.transform.localRotation.eulerAngles;
        desiredX = rot.y + mouseX;
        
        //Rotate, and also make sure we dont over- or under-rotate.
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        //Perform the rotations
        playerCam.transform.localRotation = Quaternion.Euler(xRotation, desiredX, 0);
        orientation.transform.localRotation = Quaternion.Euler(0, desiredX, 0);
    }

    /// <summary>
    /// 移動入力がない場合にプレイヤーを減速させるための力 (逆方向の運動)
    /// これにより、「キビキビした」感触が生まれ、余計な滑りを防ぐ
    /// </summary>
    private void CounterMovement(float x, float y, Vector2 mag) {
        // x: 横方向の入力, y: 前後方向の入力, mag: プレイヤーが見ている方向の速度のベクトル
        
        if (!grounded || jumping) return;

        //Slow down sliding
        // しゃがんでいる場合は、カウンタームーブメントの代わりに単純なスライディング**摩擦**を適用
        if (crouching) {
            rb.AddForce(moveSpeed * Time.deltaTime * -rb.linearVelocity.normalized * slideCounterMovement);
            return;
        }

        //Counter movement
        // 入力が停止 (x ≒ 0) するか、速度(mag.x)と反対の場合に水平方向にカウンターフォースを適用
        if (Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) || (mag.x > threshold && x < 0)) {
            rb.AddForce(moveSpeed * orientation.transform.right * Time.deltaTime * -mag.x * counterMovement);
        }
        
        // 入力が停止 (y ≒ 0) するか、速度(mag.y)と反対の場合に垂直方向にカウンターフォースを適用
        if (Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) || (mag.y > threshold && y < 0)) {
            rb.AddForce(moveSpeed * orientation.transform.forward * Time.deltaTime * -mag.y * counterMovement);
        }
        
        //Limit diagonal running. This will also cause a full stop if sliding fast and un-crouching, so not optimal.
        // 斜め移動の速度を制限します。これにより、しゃがんでいないときに速く移動しすぎることを防ぐ
        if (Mathf.Sqrt((Mathf.Pow(rb.linearVelocity.x, 2) + Mathf.Pow(rb.linearVelocity.z, 2))) > maxSpeed) {
            float fallspeed = rb.linearVelocity.y;
            Vector3 n = rb.linearVelocity.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(n.x, fallspeed, n.z);
        }
    }

    /// <summary>
    /// Find the velocity relative to where the player is looking
    /// Useful for vectors calculations regarding movement and limiting movement
    /// プレイヤーが見ている方向に対するプレイヤーの速度を計算します。
    /// </summary>
    /// <returns>xが横方向の速度、yが前方向の速度であるVector2。</returns>
    public Vector2 FindVelRelativeToLook() {
        float lookAngle = orientation.transform.eulerAngles.y;
        float moveAngle = Mathf.Atan2(rb.linearVelocity.x, rb.linearVelocity.z) * Mathf.Rad2Deg; // ラジアンから度へ

        // プレイヤーの移動方向と視線方向の角度差を計算します。
        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;

        // 三角法を使用して、速度の前方成分と横方向成分を求めます。
        float magnitue = rb.linearVelocity.magnitude;
        float yMag = magnitue * Mathf.Cos(u * Mathf.Deg2Rad);
        float xMag = magnitue * Mathf.Cos(v * Mathf.Deg2Rad);
        
        // 速度の前方成分と横方向成分をVector2として返します。
        return new Vector2(xMag, yMag);
    }

    private bool IsFloor(Vector3 v) {
        float angle = Vector3.Angle(Vector3.up, v);
        return angle < maxSlopeAngle;
    }

    private bool cancellingGrounded;
    
    /// <summary>
    /// Handle ground detection
    /// </summary>
    private void OnCollisionStay(Collision other) {
        //Make sure we are only checking for walkable layers
        int layer = other.gameObject.layer;
        if (whatIsGround != (whatIsGround | (1 << layer))) return;

        //Iterate through every collision in a physics update
        for (int i = 0; i < other.contactCount; i++) {
            Vector3 normal = other.contacts[i].normal;
            //FLOOR
            if (IsFloor(normal)) {
                grounded = true;
                cancellingGrounded = false;
                normalVector = normal;
                CancelInvoke(nameof(StopGrounded));
            }
        }

        //Invoke ground/wall cancel, since we can't check normals with CollisionExit
        float delay = 3f;
        if (!cancellingGrounded) {
            cancellingGrounded = true;
            Invoke(nameof(StopGrounded), Time.deltaTime * delay);
        }
    }

    private void StopGrounded() {
        grounded = false;
    }
}