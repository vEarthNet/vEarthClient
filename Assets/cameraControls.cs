using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cameraControls : MonoBehaviour
{
    public float MouseSensitivity = 6;
    public float MoveSpeed = 100.0f;

    public KeyCode MoveCamForward = KeyCode.W;
    public KeyCode MoveCamBackward = KeyCode.S;
    public KeyCode MoveCamLeft = KeyCode.A;
    public KeyCode MoveCamRight = KeyCode.D;
    public KeyCode MoveCamUp = KeyCode.E;
    public KeyCode MoveCamDown = KeyCode.F;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        mouseLook();

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

        Vector3 camPos;


        if (Input.GetKey(MoveCamForward))
        {
            Camera.main.transform.Translate(Vector3.forward * MoveSpeed);
            Vector3 newPos = Camera.main.transform.position;
            Camera.main.transform.position = newPos;
            //Debug.Log("Camera Pos: " + Camera.main.transform.position.ToString());
        }
        else if (Input.GetKey(MoveCamBackward))
        {
            Camera.main.transform.Translate(Vector3.forward * -MoveSpeed);
            Vector3 newPos = Camera.main.transform.position;
            Camera.main.transform.position = newPos;
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
        }
    }
}
