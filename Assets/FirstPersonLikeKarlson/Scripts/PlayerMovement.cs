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

public partial class PlayerMovement : MonoBehaviour
{

    //Assingables
    public Transform playerCam;
    public Transform orientation;

    //Other
    private Rigidbody rb;

    //Rotation and look
    private float xRotation;
    public float sensitivity = 50f;
    public float sensMultiplier = 1f;

    //Movement
    public float moveSpeed = 4500;
    public float maxSpeed = 20;
    public bool grounded;
    public LayerMask whatIsGround;
    public LayerMask whatIsWallRunnable; // 壁走り可能な壁のレイヤーマスクを追加

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

    #region WallRun

    private bool isWallRunning;
    private bool readyToWallRun = true;
    private float wallRunGravity = 1f;

    // 壁ジャンプ後のクールダウン管理
    private bool wallJumpCooldownActive = false;
    public float wallJumpCooldownDuration = 0.3f; // 壁ジャンプ後の壁走り無効時間

    private float currentWallRotation;
    private float wallRotationVelocity;
    private float desiredWallRunRotation;

    public float maxCameraRollWhenRunningWall = 15f;
    public float upForceWhenStartingWallRun = 20f;

    #endregion // WallRun

    public Action<bool> OnPause;
    private bool paused;

    private bool goalReached;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        playerScale = transform.localScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _input = GetComponent<PlayerInputs>();
#if ENABLE_INPUT_SYSTEM
        _playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "missing dependencies");
#endif
    }


    private void FixedUpdate()
    {
        Movement();
        StickToWallWhileWallRunning(); // 壁走りのための力を適用
    }

    private void Update()
    {
        MyInput();
        Look();
    }

    /// <summary>
    /// Find user input. Should put this in its own class but im lazy
    /// </summary>
    private void MyInput()
    {
        x = _input.move.x;
        y = _input.move.y;
        jumping = _input.jump;
        crouching = _input.crouch;

        //Crouching
        if (_input.crouchPressed)
            StartCrouch();
        if (_input.crouchReleased)
            StopCrouch();

        if (_input.pause && !goalReached)
        {
            Pause();
        }
    }

    public void GoalReached()
    {
        if (!paused)
        {
            goalReached = true;
        }
    }

    private void Pause()
    {
        paused = !paused;
        OnPause.Invoke(paused);
        _input.pause = false;
    }

    /// <summary>
    /// プレイヤーがしゃがみ始めたときの処理
    /// プレイヤーのコライダーを縮小し、移動している場合は前方にブーストを与える
    /// </summary>
    private void StartCrouch()
    {
        transform.localScale = crouchScale; // プレイヤーモデルを縮小します。
        transform.position =
            new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z); // プレイヤーの位置を下げます。

        // 地面を移動している場合は、前方に力を加えてスライドを開始します。
        if (rb.linearVelocity.magnitude > 0.5f)
        {
            if (grounded)
            {
                rb.AddForce(orientation.transform.forward * slideForce);
            }
        }
    }

    /// <summary>
    /// プレイヤーがしゃがむのをやめたときの処理
    /// プレイヤーのコライダーのサイズと位置をリセットする
    /// </summary>
    private void StopCrouch()
    {
        transform.localScale = playerScale; // プレイヤーモデルの高さを元に戻します。
        transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
    }

    private void Movement()
    {
        if (paused || goalReached) return;

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
        if (crouching && grounded && readyToJump)
        {
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
        if (!grounded)
        {
            multiplier = 0.5f;
            multiplierV = 0.5f;
        }

        // Movement while sliding
        // スライディング中は前方へのコントロールはできない
        if (grounded && crouching) multiplierV = 0f;

        // 壁走り中はコントロールが低下する
        if (isWallRunning)
        {
            multiplier = 0.3f;
            multiplierV = 0.3f;
        }

        //Apply forces to move player
        rb.AddForce(orientation.transform.forward * y * moveSpeed * Time.deltaTime * multiplier * multiplierV);
        rb.AddForce(orientation.transform.right * x * moveSpeed * Time.deltaTime * multiplier);
    }

    private void Jump()
    {
        // 接地しているか、壁走り中で、ジャンプのクールダウンが終了していればジャンプできる
        if ((grounded || isWallRunning) && readyToJump)
        {
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
            {
                rb.linearVelocity = new Vector3(vel.x, 0, vel.z);
            }
            else if (rb.linearVelocity.y > 0)
            {
                rb.linearVelocity = new Vector3(vel.x, vel.y / 2, vel.z);
            }

            // 壁からジャンプする場合は、壁の法線方向 (壁から離れる方向) に強い力を加える
            if (isWallRunning)
            {
                rb.AddForce(wallNormalVector * jumpForce * 3f);

                // 壁ジャンプすれば壁走りを停止
                StopWallRun();

                // 壁ジャンプ後のクールダウンを開始
                StartWallJumpCooldown();
            }

            // ジャンプのクールダウンを開始
            Invoke(nameof(ResetJump), jumpCooldown);
        }
    }

    private void ResetJump()
    {
        readyToJump = true;
    }

    private float desiredX;

    private void Look()
    {
        if (paused || goalReached) return;
        
        float mouseX = _input.look.x * sensitivity * Time.fixedDeltaTime * sensMultiplier;
        float mouseY = _input.look.y * sensitivity * Time.fixedDeltaTime * sensMultiplier;

        //Find current look rotation
        Vector3 rot = playerCam.transform.localRotation.eulerAngles;
        desiredX = rot.y + mouseX;

        //Rotate, and also make sure we dont over- or under-rotate.
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // 壁走りのためのカメラロールを計算して適用
        UpdateWallRunCameraRoll();
        currentWallRotation =
            Mathf.SmoothDamp(currentWallRotation, desiredWallRunRotation, ref wallRotationVelocity, 0.2f);

        //Perform the rotations
        playerCam.transform.localRotation = Quaternion.Euler(xRotation, desiredX, currentWallRotation);
        orientation.transform.localRotation = Quaternion.Euler(0, desiredX, 0);
    }

    /// <summary>
    /// 移動入力がない場合にプレイヤーを減速させるための力 (逆方向の運動)
    /// これにより、「キビキビした」感触が生まれ、余計な滑りを防ぐ
    /// </summary>
    private void CounterMovement(float x, float y, Vector2 mag)
    {
        // x: 横方向の入力, y: 前後方向の入力, mag: プレイヤーが見ている方向の速度のベクトル

        if (!grounded || jumping) return;

        //Slow down sliding
        // しゃがんでいる場合は、カウンタームーブメントの代わりに単純なスライディング**摩擦**を適用
        if (crouching)
        {
            rb.AddForce(moveSpeed * Time.deltaTime * -rb.linearVelocity.normalized * slideCounterMovement);
            return;
        }

        //Counter movement
        // 入力が停止 (x ≒ 0) するか、速度(mag.x)と反対の場合に水平方向にカウンターフォースを適用
        if (Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) ||
            (mag.x > threshold && x < 0))
        {
            rb.AddForce(moveSpeed * orientation.transform.right * Time.deltaTime * -mag.x * counterMovement);
        }

        // 入力が停止 (y ≒ 0) するか、速度(mag.y)と反対の場合に垂直方向にカウンターフォースを適用
        if (Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) ||
            (mag.y > threshold && y < 0))
        {
            rb.AddForce(moveSpeed * orientation.transform.forward * Time.deltaTime * -mag.y * counterMovement);
        }

        //Limit diagonal running. This will also cause a full stop if sliding fast and un-crouching, so not optimal.
        // 斜め移動の速度を制限します。これにより、しゃがんでいないときに速く移動しすぎることを防ぐ
        if (Mathf.Sqrt((Mathf.Pow(rb.linearVelocity.x, 2) + Mathf.Pow(rb.linearVelocity.z, 2))) > maxSpeed)
        {
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
    private Vector2 FindVelRelativeToLook()
    {
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

    private bool IsFloor(Vector3 v)
    {
        float angle = Vector3.Angle(Vector3.up, v);
        // 角度が最大傾斜角度より小さい場合、床と見なす
        return angle < maxSlopeAngle;
    }

    /// <summary>
    /// 表面の法線が壁（垂直軸から約90度）を表しているかどうかをチェック
    /// </summary>
    private static bool IsWall(Vector3 v)
    {
        return Math.Abs(90f - Vector3.Angle(Vector3.up, v)) < 0.1f;
    }

    /// <summary>
    /// cancellingGrounded は、プレイヤーが地面に接触しているかどうかの状態を管理するためのフラグです。
    /// OnCollisionStay 内で、地面から離れたときに StopGrounded を一度だけ遅延実行するために使われています。
    /// これにより、地面との接触が一瞬途切れてもすぐに「空中」判定にならず、滑らかな接地判定が実現されています。
    /// </summary>
    private bool cancellingGrounded;

    /// <summary>
    /// Handle ground detection and wall detection
    /// </summary>
    private void OnCollisionStay(Collision other)
    {
        // 地面判定: whatIsGroundレイヤーマスクをチェック
        bool isOnGroundLayer = whatIsGround.Contains(other.gameObject);
        // 壁走り判定: whatIsWallRunnableレイヤーマスクをチェック
        bool isOnWallRunnableLayer = whatIsWallRunnable.Contains(other.gameObject);

        // 地面・壁走り可能な壁のどちらのレイヤーにも属していない場合は、何もしない
        if (!isOnGroundLayer && !isOnWallRunnableLayer) return;

        //Iterate through every collision in a physics update
        // 衝突ごとに接触点を確認し、接地や壁走りの状態を更新
        for (int i = 0; i < other.contactCount; i++)
        {
            // 接触点の法線 (normal) を取得
            Vector3 normal = other.contacts[i].normal;

            //FLOOR - 地面レイヤーの床判定
            // IsFloorで、接触点の法線が床を表しているかどうかを確認
            if (IsFloor(normal) && isOnGroundLayer)
            {
                // 地面にいるとなった場合は壁走り状態は停止
                if (isWallRunning)
                {
                    StopWallRun();
                }

                grounded = true;
                normalVector = normal;

                cancellingGrounded = false;

                CancelInvoke(nameof(StopGrounded)); // groundedをfalseに設定するタイマーをキャンセル
            }

            //WALL - 壁走り可能レイヤーの壁判定
            if (IsWall(normal) && isOnWallRunnableLayer)
            {
                StartWallRun(normal);

                // 壁からジャンプしない場合や、壁から即着地しない場合に、壁走りを停止するタイミングが無いので
                // 常に壁走りをキャンセルするタイマーを起動させ、壁にいる間はそれを再起動させ続ける。
                // もともとの grounded の方法とほぼ同じにしている
                CancelInvoke(nameof(StopWallRun));
                const float wallRunCancelDelay = 3f;
                Invoke(nameof(StopWallRun), Time.deltaTime * wallRunCancelDelay);
            }
        }

        //Invoke ground/wall cancel, since we can't check normals with CollisionExit
        float delay = 3f;
        if (!cancellingGrounded)
        {
            cancellingGrounded = true;
            Invoke(nameof(StopGrounded), Time.deltaTime * delay);
        }
    }

    private void StopGrounded()
    {
        grounded = false;
    }

    private void StartWallRun(Vector3 normal)
    {
        // 地上にいる or 壁走りの準備ができていない or 壁ジャンプクールダウン中は壁走りを開始しない
        if (grounded || !readyToWallRun || wallJumpCooldownActive) return;

        // 壁の法線ベクトルを設定 (壁張り付き、壁からのジャンプなどに使用)
        wallNormalVector = normal;

        if (!isWallRunning)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * upForceWhenStartingWallRun, ForceMode.Impulse);
        }

        isWallRunning = true;
    }

    /// <summary>
    /// 壁走り状態を停止
    /// </summary>
    private void StopWallRun()
    {
        isWallRunning = false;
    }

    /// <summary>
    /// 壁ジャンプ後のクールダウンを開始 - 一定時間壁走りを無効化
    /// </summary>
    private void StartWallJumpCooldown()
    {
        wallJumpCooldownActive = true;
        Invoke(nameof(EndWallJumpCooldown), wallJumpCooldownDuration);
    }

    /// <summary>
    /// 壁ジャンプクールダウンを終了し、再び壁走りを可能にする
    /// </summary>
    private void EndWallJumpCooldown()
    {
        wallJumpCooldownActive = false;
    }

    /// <summary>
    /// 壁走り中に壁にくっつき、重力に逆らうための力を適用
    /// </summary>
    private void StickToWallWhileWallRunning()
    {
        if (!isWallRunning) return;

        // プレイヤーを壁に押し込む力 (壁の法線の反対方向、つまり壁に向かう力を与える)
        rb.AddForce(-wallNormalVector * Time.deltaTime * moveSpeed);
        // 重力に逆らう力 (up 方向、つまり重力の反対方向に力を加える)
        rb.AddForce(Vector3.up * Time.deltaTime * rb.mass * 100f * wallRunGravity);
    }

    /// <summary>
    /// 壁走りの方向に基づいて目標のカメラロール角を決定
    /// プレイヤーが上または下を見すぎた場合に自動的に壁から離れるロジックも処理
    /// </summary>
    private void UpdateWallRunCameraRoll()
    {
        if (!isWallRunning)
        {
            desiredWallRunRotation = 0f;
            return;
        }

        // 世界の前方方向に対する壁の角度を決定
        float cameraYaw = playerCam.transform.rotation.eulerAngles.y;
        float wallAngle = Vector3.SignedAngle(new Vector3(0f, 0f, 1f), wallNormalVector, Vector3.up);

        // カメラのヨーと壁の角度の差を求める
        float angleDifference = Mathf.DeltaAngle(cameraYaw, wallAngle);
        // カメラロールを計算。90度の差は15度のロールになる
        desiredWallRunRotation = (0f - angleDifference / 90f) * maxCameraRollWhenRunningWall;
    }
}