using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MainCameraController : MonoBehaviour
{

    public float MouseSensitivity = 6;
    public float MoveSpeed = 5.0f;
    public float RotateSpeed = 5.0f;
    public float RotateDeadZone = 0.3f;

    public KeyCode MoveCamForward = KeyCode.W;
    public KeyCode MoveCamBackward = KeyCode.S;
    public KeyCode MoveCamLeft = KeyCode.A;
    public KeyCode MoveCamRight = KeyCode.D;
    public KeyCode MoveCamUp = KeyCode.E;
    public KeyCode MoveCamDown = KeyCode.F;

    private bool isTouching = false;

    private Vector2 firstTouch;
    private Vector2 lastTouch;


    // Start is called before the first frame update
    void Start()
    {
        firstTouch = new Vector2(0, 0);
        lastTouch = new Vector2(0, 0);
    }

    // Update is called once per frame
    void Update()
    {        
        //mouseLook();

        checkMove();
    }

    public void mouseLook()
    {
        float camRotate = 1.0f;

        //Horizontal
        if (Input.GetAxis("Mouse X") != 0.0f)
        {
            Camera.main.transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * MouseSensitivity, Space.World);
        }

        //Vertical
        if (Input.GetAxis("Mouse Y") != 0.0f)
        {
            Camera.main.transform.Rotate(Vector3.right * Input.GetAxis("Mouse Y") * -MouseSensitivity, Space.Self);
        }
    }

    public void checkMove()
    {
        //Debug.Log("Checking move!!!!!!!!!! isTouching " + GvrControllerInput.IsTouching.ToString() + " click button " + GvrControllerInput.ClickButtonDown.ToString() 
         //  + " touchpos " + GvrControllerInput.TouchPos.ToString());

        Vector3 camMove,camPos;
        
        //if ( Input.GetKey(MoveCamForward) || GvrControllerInput.IsTouching || GvrControllerInput.ClickButtonDown)
        if (GvrControllerInput.IsTouching)
        {
            Vector2 touchPos = GvrControllerInput.TouchPos;
            if (isTouching == false)
            {
                firstTouch = touchPos;
                isTouching = true;
            }

            //First, convert the {0.0,1.0} scale to {-1.0,1.0}.
            float forwardVel = 1.0f - (touchPos.y * 2.0f);
            float turnVel = -1.0f + (touchPos.x * 2.0f);
            

            GameObject player = Camera.main.transform.parent.gameObject;
            camMove = Camera.main.transform.forward * MoveSpeed * forwardVel;
            camPos = player.transform.position;
            player.transform.position = camPos + camMove;

            if (Mathf.Abs(turnVel) > RotateDeadZone)
                player.transform.Rotate(Vector3.up, turnVel * RotateSpeed );

        }/*
        else if (Input.GetKey(MoveCamBackward))
        {

        }
        else if (Input.GetKey(MoveCamLeft))
        {
            Camera.main.transform.Translate(Vector3.right * -MoveSpeed);
        }
        else if (Input.GetKey(MoveCamRight))
        {
            Camera.main.transform.Translate(Vector3.right * MoveSpeed);
        }
        else if (Input.GetKey(MoveCamUp))
        {
            Camera.main.transform.Translate(Vector3.up * MoveSpeed);
        }
        else if (Input.GetKey(MoveCamDown))
        {
            Camera.main.transform.Translate(Vector3.up * -MoveSpeed);
        }*/
    }

}
