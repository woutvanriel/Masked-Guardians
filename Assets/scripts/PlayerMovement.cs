using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // Todo: animation in another script for more clarity
    // Todo: tweak movements for terrain
    // Todo: add/create pixel art animation

    [Header("Components")]
    private Rigidbody rb;
    private Collisions coll;
    private Particles fx;
    public Animator anim;
    
    private float xDirection;
    private float yDirection;

    public bool active = true;
    private Vector3 currentOffset = Vector3.zero;
    public bool facingRight = true;

    private void Start() {
        rb = GetComponent<Rigidbody>();
        coll = GetComponent<Collisions>();
        fx = GetComponent<Particles>();
        anim = GetComponent<Animator>();
        setRespawnPoint(transform.position);
    }

    private void Update() {
        xDirection = getInput().x;
        yDirection = getInput().y;
        if (Input.GetButtonDown("Jump")) jumpBufferThreshold = jumpBuffer;
        else jumpBufferThreshold -= Time.deltaTime;
        if (Input.GetButtonDown("Dash")) dashBufferCounter = dashBufferThreshold;
        else dashBufferCounter -= Time.deltaTime;
        animation();
    }

    private void FixedUpdate() {
        coll.checkCollisions();
        if ((xDirection < 0f && facingRight || xDirection > 0f && !facingRight) && !canWallGrab && !canWallSlide) {
            flip();
        }
        if (canDash) StartCoroutine(dash(xDirection, yDirection));
        if (!isDashing) {
            if (canMove) move();
            else {
                rb.velocity = Vector3.Lerp(rb.velocity, new Vector3(xDirection * topSpeed, rb.velocity.y), .5f * Time.deltaTime);
            }
            
            if (coll.onGround) {
                currentOffset = idleOffset;
                applyDeAcceleration();
                coyoteTimeThreshold = coyoteTime;
                hasDashed = false;
            }
            else {
                applyAirLinearDrag();
                gravityMultiplier();
                coyoteTimeThreshold -= Time.fixedDeltaTime;
                if (!coll.onWall || rb.velocity.y < 0f || canWallClimb) isJumping = false;
            }
            if (canJump) {
                if (coll.onWall && !coll.onGround) {
                    if (!canWallClimb && (coll.onRightWall && xDirection > 0f || !coll.onRightWall && xDirection < 0f)) {
                        StartCoroutine(neutralWallJump());
                    }
                    else {
                        wallJump();
                    }
                    flip(); 
                }
                else {
                    jump(Vector3.up, false);
                }
            }
            if (!isJumping) {
                if (canWallSlide) wallSlide();
                if (canWallGrab) wallGrab();
                if (canWallClimb) wallClimb();
                if (coll.onWall) stickToWall();
            }
        }
        updateHairOffset();
        if (coll.canEdgeCorrect) coll.edgeCorrect(rb.velocity.y);
    }

    private static Vector3 getInput() {
        return new Vector3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    #region Walk

    [Header("Walk")]
    [SerializeField] private float acceleration = 75f;
    [SerializeField] private float topSpeed = 8f;
    [SerializeField] private float deAcceleration = 20f;
    private bool canMove => !canWallGrab && !isUpRoom;
    private bool changingDirection => rb.velocity.x > 0f && xDirection < 0f || rb.velocity.x < 0f && xDirection > 0f;

    private void move() {
        rb.AddForce(new Vector3(xDirection, 0f) * acceleration);
        if (Mathf.Abs(rb.velocity.x) > topSpeed)
            rb.velocity = new Vector3(Mathf.Sign(rb.velocity.x) * topSpeed, rb.velocity.y);
    }

    private void applyDeAcceleration() {
        if (Mathf.Abs(xDirection) < 0.4f || changingDirection) {
            rb.drag = deAcceleration;
        }
        else {
            rb.drag = 0f;
        }
    }

    #endregion

    #region Jump
    
    [Header("Jump")]
    [SerializeField] private float jumpHeight = 12f;
    [SerializeField] private float airLinearDrag = 2.5f;
    [SerializeField] private float downMultiplier = 12f;
    [SerializeField] private float fallMultiplier = 8f;
    [SerializeField] private float lowJumpFallMultiplier = 5f;
    [SerializeField][Range(0, 0.3f)] private float coyoteTime = .1f;
    [SerializeField][Range(0, 0.3f)] private float jumpBuffer = .1f;
    private float coyoteTimeThreshold;
    private float jumpBufferThreshold;
    private bool canJump => jumpBufferThreshold > 0f && (coyoteTimeThreshold > 0f || coll.onWall) && !isUpRoom;
    private bool isJumping;
    
    private void jump(Vector3 direction, bool wall) {
        applyAirLinearDrag();
        rb.velocity = new Vector3(rb.velocity.x, 0f);
        rb.AddForce(direction * jumpHeight, ForceMode.Impulse);
        coyoteTimeThreshold = 0f;
        jumpBufferThreshold = 0f;
        isJumping = true;
    }

    private void wallJump() {
        Vector3 jumpDirection = coll.onRightWall ? Vector3.left : Vector3.right;
        jump(Vector3.up + jumpDirection, true);
    }

    private IEnumerator neutralWallJump() {
        Vector3 jumpDirection = coll.onRightWall ? Vector3.left : Vector3.right;
        jump(Vector3.up + jumpDirection, true);
        yield return new WaitForSeconds(wallJumpXVelocityHaltDelay);
        rb.velocity = new Vector2(0f, rb.velocity.y);
    }
    
    private void gravityMultiplier() {
        switch (rb.velocity.y) {
            case < 0:
                // Apply fallMultiplier when falling
                rb.velocity += Vector3.up * (Physics.gravity.y * (fallMultiplier - 1) * Time.deltaTime);
                break;
            case > 0 when !Input.GetButton("Jump"):
                // Apply lowJumpFallMultiplier when ascending without holding the jump button
                rb.velocity += Vector3.up * (Physics.gravity.y * (lowJumpFallMultiplier - 1) * Time.deltaTime);
                break;
        }
    }
    private void applyAirLinearDrag() {
        rb.drag = airLinearDrag;
    }

    #endregion
    
    #region Wall Interaction

    [Header("Wall action")]
    [SerializeField] private float wallSlideModifier = 0.5f;
    [SerializeField] private float wallClimbModifier = 0.7f;
    [SerializeField] private float wallJumpXVelocityHaltDelay = 0.2f;
    public bool canWallGrab => coll.onWall && !coll.onGround && Input.GetButton("WallGrab") && !canWallClimb && !isUpRoom;
    public bool canWallSlide => coll.onWall && !coll.onGround && !Input.GetButton("WallGrab") && rb.velocity.y < 0f && !canWallClimb && (xDirection > 0 || xDirection < 0);
    private bool canWallClimb => coll.onWall && !coll.onGround && (yDirection > 0f || yDirection < 0f) && Input.GetButton("WallGrab") && !isUpRoom;

    private void wallGrab() {
        rb.drag = 0f;
        rb.velocity = Vector3.zero;
    }
    private void wallSlide() => rb.velocity = new Vector3(rb.velocity.x, -topSpeed * wallSlideModifier);

    private void wallClimb() => rb.velocity = new Vector3(rb.velocity.x, yDirection * topSpeed * wallClimbModifier);
    
    private void stickToWall() {
        //Push player towards wall
        rb.velocity = coll.onRightWall switch {
            true when xDirection >= 0f => new Vector3(1f, rb.velocity.y),
            false when xDirection <= 0f => new Vector3(-1f, rb.velocity.y),
            _ => rb.velocity
        };
        
        switch (coll.onRightWall) {
            case true when !facingRight:
            case false when facingRight:
                flip();
                break;
        }
    }

    #endregion
    
    #region Dash

    [Header("Dash")]
    [SerializeField][Range(10, 50)] private float dashSpeed = 15f;
    [SerializeField][Range(0.1f, 0.5f)] private float dashLength = .3f;
    [SerializeField][Range(0, 0.3f)] private float dashBufferThreshold = .1f;
    public ParticleSystem sweatParticle;
    
    private float dashBufferCounter;
    public bool isDashing;
    public bool hasDashed;
    private bool canDash => dashBufferCounter > 0f && !hasDashed;
    
    private IEnumerator dash(float x, float y) {
        float dashStartTime = Time.time;
        hasDashed = true;
        isDashing = true;
        isJumping = false;

        rb.velocity = Vector2.zero;
        rb.drag = 0f;
        rb.drag = 0f;

        Vector2 dir;
        if (x != 0f || y != 0f) dir = new Vector2(x,y);
        else {
            dir = facingRight ? new Vector2(1f, 0f) : new Vector2(-1f, 0f);
        }

        while (Time.time < dashStartTime + dashLength) {
            rb.velocity = dir.normalized * dashSpeed;
            yield return null;
        }
        isDashing = false;
    }

    #endregion

    #region respawn / checkpoint

    private Vector2 respawnPoint;
    private const float jumpBoost = 30f;
    private void setRespawnPoint(Vector2 position) => respawnPoint = position;
    public void die() {
        active = false;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        StartCoroutine(respawn());
    }

    private IEnumerator respawn() {
        yield return new WaitForSeconds(1.4f);
        hasDashed = false;
        anim.SetBool("isFalling", true);
        rb.constraints = RigidbodyConstraints.FreezePositionX;
        transform.position = respawnPoint;
        active = true;
        
        StartCoroutine(unFreeze());
    }

    private IEnumerator unFreeze() {
        if (isUpRoom) {
            yield return new WaitForSeconds(0.1f);
            rb.constraints = RigidbodyConstraints.FreezePositionY;
        }
        yield return new WaitForSeconds(0.5f);
        isUpRoom = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private bool isUpRoom;

    private void OnTriggerEnter2D(Collider2D other) {
        if(other.CompareTag("Checkpoint")) {
            respawnPoint = transform.position;
        }

        if (other.CompareTag("RoomUp")) {
        
            isUpRoom = true;
            rb.AddForce(Vector2.up * jumpBoost, ForceMode.Impulse);
        }

        if (!other.CompareTag("Room")) return;
        rb.constraints = isUpRoom switch {
            false => RigidbodyConstraints.FreezeAll,
            true => RigidbodyConstraints.FreezePositionX
        };
        StartCoroutine(unFreeze());
    }

    #endregion

    #region Animation

    private void flip() {
        facingRight = !facingRight;
        transform.Rotate(0f, 180f, 0f);
    }
    
    private new void animation() {
        if (isDashing) {
            anim.SetBool("isGrounded", false);
            anim.SetBool("isFalling", false);
            anim.SetBool("WallGrab", false);
            anim.SetBool("isJumping", false);
            anim.SetFloat("horizontalDirection", 0f);
            anim.SetFloat("verticalDirection", 0f);
        }
        else {
            if ((xDirection < 0f && facingRight || xDirection > 0f && !facingRight) && !canWallGrab && !canWallSlide) {
                flip();  
            }
            if ((coll.onGround || rb.velocity.y == 0) && !Input.GetButton("WallGrab")) { //idle
                anim.SetBool(isGrounded, true);
                anim.SetBool("isFalling", false);
                anim.SetBool("WallGrab", false);
                anim.SetFloat(horizontalDirection, Mathf.Abs(xDirection));
            }
            else {
                anim.SetBool("isGrounded", false);
            }
            if (isJumping) {
                anim.SetBool(jumping, true);
                anim.SetBool("isFalling", false);
                anim.SetBool("WallGrab", false);
                anim.SetFloat("verticalDirection", 0f);
            }
            else {
                anim.SetBool("isJumping", false);

                if (canWallGrab || canWallSlide) {
                    anim.SetBool("WallGrab", true);
                    anim.SetBool("isFalling", false);
                    anim.SetFloat("verticalDirection", 0f);
                }
                else if (rb.velocity.y < 0f && !coll.onGround) {
                    anim.SetBool("isFalling", true);
                    anim.SetBool("WallGrab", false);
                    anim.SetFloat("verticalDirection", 0f);
                }
                if (canWallClimb) {
                    anim.SetBool("isFalling", false);
                    anim.SetBool("WallGrab", false);
                    anim.SetFloat("verticalDirection", Mathf.Abs(yDirection));
                }
            }
        }
    }



    [Header("Hair")] 
    
    [SerializeField] private Vector2 idleOffset;
    [SerializeField] private Vector2 runOffset;
    [SerializeField] private Vector2 jumpOffset;
    [SerializeField] private Vector2 fallOffset;
    [SerializeField] private Vector2 dashStraightOffset;
    [SerializeField] private Vector2 dashSlantedUpOffset;
    [SerializeField] private Vector2 dashSlantedDownOffset;
    private static readonly int isGrounded = Animator.StringToHash("isGrounded");
    private static readonly int horizontalDirection = Animator.StringToHash("horizontalDirection");
    private static readonly int jumping = Animator.StringToHash("isJumping");

    private void updateHairOffset() {
        //idle
        if (Input.GetButton("WallGrab") || rb.velocity.y >= 0) {
            currentOffset = idleOffset;
        }

        //falling
        if (rb.velocity.y < 0f && !coll.onGround) {
            currentOffset = fallOffset;
        }

        //vertical jump
        if (rb.velocity.y > 0f && xDirection == 0 && !coll.onGround) {
            currentOffset = idleOffset;
        }

        //jump
        if (rb.velocity.y > 0f && (xDirection > 0 || xDirection < 0) && !coll.onGround && !coll.onWall) {
            currentOffset = jumpOffset;
        }

        //dash/run
        currentOffset = isDashing switch {
            true when !coll.onWall && (rb.velocity.x > 0 || rb.velocity.x < 0) => dashStraightOffset,
            false when coll.onGround && !coll.onWall && (xDirection > 0 || xDirection < 0) => runOffset,
            _ => currentOffset
        };
        if (isDashing && !coll.onWall && (rb.velocity.x > 0 || rb.velocity.x < 0) && rb.velocity.y > 0) {
            currentOffset = dashSlantedUpOffset;
        }
        if (isDashing && !coll.onWall && (rb.velocity.x > 0 || rb.velocity.x < 0) && rb.velocity.y < 0) {
            currentOffset = dashSlantedDownOffset;
        }
        
        if (!facingRight) {
            currentOffset.x *= -1;
        }
    }

    #endregion
}
