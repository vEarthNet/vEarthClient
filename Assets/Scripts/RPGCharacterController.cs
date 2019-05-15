
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

//Uncomment for Photon
//using Photon.Pun;
//using Photon.Realtime;


public class RPGCharacterController : MonoBehaviour //MonoBehaviorPun, IPunObservable  //Uncomment for Photon
{
    Animator animator;
    public GameObject TargetObject = null;
    public GameObject CameraObject;
    public GameObject MenuCanvas = null;
    public GameObject USimInterface = null;
    NavMeshAgent agent;

    [Tooltip("The local player instance. Use this to know if the local player is represented in the Scene")]
    public static GameObject LocalPlayerInstance;

    public Transform rightHandObj = null;//obsolete?

    public float rotateSpeed = 1;
    public float rotate = 0;
    
    public int IdleAnim = 0;
    public int WalkAnim = 1;
    public int RunAnim = 2;
    public int BackupAnim = 3;
    public int LeftAttackAnim = 4;
    public int RightAttackAnim = 5;

    public bool isAttacking;

    [Tooltip("The current Health of our player")]
    public float health;

    [Tooltip("The Player's UI GameObject Prefab")]
    [SerializeField]
    public GameObject PlayerUiPrefab;

    Vector2 mousePos;
    
    void Awake()
    {
        //if (photonView.IsMine)  //Uncomment for Photon
        //{
        RPGCharacterController.LocalPlayerInstance = this.gameObject;

        GameObject TPObj = GameObject.Find("TerrainPager");
        if (TPObj != null)
        {
            TerrainPager theTP = TPObj.GetComponent<TerrainPager>();
            if (theTP != null)
                theTP.PlayerObject = this.gameObject;
        }
        //}
        //else  //well, this plan didn't work...
        //{ //If not our instance, then turn off the camera, so we don't follow the wrong player.
        //if (CameraObject != null)
        //    CameraObject.SetActive(false);
        //}
        // #Critical
        // we flag as don't destroy on load so that instance survives level synchronization, thus giving a seamless experience when levels load.
        DontDestroyOnLoad(this.gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        //agent = GetComponent<NavMeshAgent>();        

        animator.SetInteger("anim", IdleAnim);
        isAttacking = false;
        mousePos = new Vector2(0, 0);
        health = 1.0f;

        /*
        if (MenuCanvas == null)
        {
            MenuCanvas = GameObject.FindGameObjectWithTag("MainMenu");
            if (MenuCanvas != null)
                MenuCanvas.SetActive(false);
        }

        USimInterface = GameObject.Find("_uSim_PlayerInterface");
        */

        /*
        if (PlayerUiPrefab != null)
        {
            GameObject _uiGo = Instantiate(PlayerUiPrefab);
            _uiGo.SendMessage("SetTarget", this, SendMessageOptions.RequireReceiver);
        }
        else
        {
            Debug.LogWarning("<Color=Red><a>Missing</a></Color> PlayerUiPrefab reference on player Prefab.", this);
        }*/
    }

    // Update is called once per frame
    void Update()
    {
        //Uncomment for Photon
        //if (photonView.IsMine == false && PhotonNetwork.IsConnected == true)
        //{
        //    return;
        //}

        //Hm, above method did not work, how about this? (
        if (this.gameObject != LocalPlayerInstance)
            return;

        int action;
        animator.SetBool("moving", false);
        animator.SetFloat("Velocity X", 0.0f);
        animator.SetFloat("Velocity Z", 0.0f);

        if (Input.GetKey(KeyCode.Escape))
        {
            if (MenuCanvas != null)
            {
                MenuCanvas.SetActive(true);
                Cursor.visible = true;
            }
        }

        if (health <= 0f)
        {
            animator.SetTrigger("Death1Trigger");
            return;
        }
        if (Input.GetKey(KeyCode.W))
        {
            animator.SetBool("moving", true);
            if (Input.GetKey(KeyCode.LeftShift))
                animator.SetFloat("Velocity Z", 4.0f);
            else
                animator.SetFloat("Velocity Z", 12.0f);
        }
        else if (Input.GetKey(KeyCode.S))
        {
            animator.SetBool("moving", true);
            animator.SetFloat("Velocity Z", -4.0f);
        }

        if (Input.GetKey(KeyCode.A))
        {
            animator.SetBool("moving", true);
            animator.SetFloat("Velocity X", -4.0f);
        }
        else if (Input.GetKey(KeyCode.D))
        {
            animator.SetBool("moving", true);
            animator.SetFloat("Velocity X", 4.0f);
            //transform.position += transform.forward * -2.0f;
        }

        if (Input.GetMouseButtonDown(0))
        {
            mousePos = Vector2.zero;
        }
        else if (Input.GetMouseButton(0))
        {
            mousePos.x += Input.GetAxisRaw("Mouse X");
            mousePos.y += Input.GetAxisRaw("Mouse Y");
        }
        else if (Input.GetMouseButtonUp(0))
        {
            animator.SetBool("moving", false);
            animator.SetInteger("Jumping", 0);
            animator.ResetTrigger("JumpTrigger");//maybe?? Why are we going to jump tree??
            animator.SetInteger("Weapon", 1);//FIX: do this when you pick up a weapon, or at start.
            if (Input.GetKey(KeyCode.LeftShift))
            {
                animator.SetInteger("Action", 1);//1 + Random.Range(0, 2));
                animator.SetTrigger("AttackKickTrigger");
            }
            else
            {
                //HERE: it would be nice to normalize the vector and turn this into an angle comparison, to get eight or more
                //options, but for now just doing a simple, quick and dirty four way test.
                if (Mathf.Abs(mousePos.x) > Mathf.Abs(mousePos.y))
                {
                    if (mousePos.x > 0) action = 1;
                    else action = 2;
                }
                else
                {
                    if (mousePos.y > 0) action = 3;
                    else action = 4;
                }
                animator.SetInteger("Action", action);//1 + Random.Range(0, 4));
                animator.SetTrigger("AttackTrigger");
                isAttacking = true;
                Invoke("clearAttacking", 3);//FIX: Need a much better system for this, at least to find the actual length of the attaack anim, but more 
                //importantly to determine if we're in the right phase of this particular attack anim, and if we are the right distance away to make this
                //a lethal hit, ie toward the tip of the blade, not the hilt. Hit position should be able to tell us that.



            }
        }
        /*
        if (Input.GetMouseButtonDown(1))
        {
            animator.SetBool("moving", false);
            animator.SetInteger("Jumping", 0);
            animator.ResetTrigger("JumpTrigger");
            if (Input.GetKey(KeyCode.LeftShift))
            {
                animator.SetInteger("Action", 2);// 3 + Random.Range(0, 2));
                animator.SetTrigger("AttackKickTrigger");
            }
            else
            {
                animator.SetInteger("Action", 4 + Random.Range(0, 3));
                animator.SetTrigger("AttackTrigger");
            }
        }*/

        if (Input.GetKey(KeyCode.Space))
        {
            animator.SetTrigger("JumpTrigger");
            animator.SetInteger("Jumping", 1);
        }
        /*

        if (Input.GetMouseButton(0))
            animator.SetInteger("anim", LeftAttackAnim);

        if (Input.GetMouseButton(1))
            animator.SetInteger("anim", RightAttackAnim);
     

        if (Input.GetKey(KeyCode.A))
            rotate = -1.0f;
        else if (Input.GetKey(KeyCode.D))
            rotate = 1.0f;
        else 
            rotate = 0;
        */

        if (!Input.GetMouseButton(0))
        {
            rotate = Input.GetAxisRaw("Mouse X");
            transform.Rotate(transform.up, rotate * rotateSpeed);
        }
    }
    /*
    //a callback for calculating IK
    void OnAnimatorIK()
    {
        if (animator)
        {
            if (rightHandObj != null)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
                animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandObj.position);
                animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandObj.rotation);
            }
        }
    }*/

    void clearAttacking()
    {
        isAttacking = false;
    }

    /*      //Uncomment for Photon
    #region IPunObservable implementation

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // We own this player: send the others our data
                stream.SendNext(health);
            }
            else
            {
                // Network player, receive data
                this.health = (float)stream.ReceiveNext();
            }
        }
    #endregion


        [PunRPC]
        void SetPlayerActive(bool b)
        {
            this.gameObject.SetActive(b);
        }
    */

    /*            //Uncomment for uSim
void MountVehicle(GameObject v)
{
    AircraftControl ac = v.GetComponent<AircraftControl>();
    //if (ac) ac.enabled = true;

    PhotonView apv = v.GetComponent<PhotonView>();
    if (apv != null)
        apv.RPC("giveOwnershipToClient",RpcTarget.MasterClient,this.photonView.ViewID);

    //AircraftSounds as = v.GetComponent<AircraftSounds>();
    //if (as) as.enabled = true;

    InputsManager im = v.GetComponent<InputsManager>();
    if (im) im.enabled = true;
    im.thePlayer = this.gameObject;

    GameObject tpObj = GameObject.Find("TerrainPager");
    TerrainPager tp = tpObj.GetComponent<TerrainPager>();
    Component[] cams = v.GetComponentsInChildren<Camera>(true);

    foreach (Camera cam in cams)
    {
        Debug.Log("Camera " + cam.gameObject.name + " tag " + cam.gameObject.tag);
        if (cam.gameObject.tag.Equals("MainCamera"))
        {
            cam.gameObject.SetActive(true);
            if (tp != null)
                tp.mCamera = cam.gameObject;
        }
    }

    if (USimInterface != null)
    {
        MouseJoystick ms = USimInterface.GetComponent<MouseJoystick>();
        if (ms)
            ms.inputs = im;
        StandardInputs si = USimInterface.GetComponent<StandardInputs>();
        if (si)
            si.inputs = im;
    }

    SimBase sb = GetComponent<SimBase>();
    sb.mTerrainObject = null;
    this.gameObject.SetActive(false);
    this.photonView.RPC("SetPlayerActive", RpcTarget.Others, false);
}
*/
    void OnCollisionEnter(Collision col)
    {
        //if (photonView.IsMine) //For Photon
        //{
        GameObject v = col.transform.root.gameObject;
            Debug.Log("Player collided with:  " + v.name);

            //if (v.tag.Equals("Vehicle")) //Uncomment for uSim
            //{
            //    MountVehicle(v);
            //}
        //}
    }

    //public void CalledOnLevelWasLoaded() // ?? - This was not documented  - ?? 
    //{
    //    GameObject _uiGo = Instantiate(this.PlayerUiPrefab);
    //    _uiGo.SendMessage("SetTarget", this, SendMessageOptions.RequireReceiver);
    //}
}
