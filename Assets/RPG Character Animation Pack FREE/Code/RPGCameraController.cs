using UnityEngine;
using System.Collections;

public class RPGCameraController : MonoBehaviour
{
	public GameObject cameraTarget;
	public float rotateSpeed;
	float rotate;
	public float offsetDistance;
	public float offsetHeight;
	public float smoothing;
    public float lookHeight;
	Vector3 offset;
    public bool following = true;
	Vector3 lastPosition;
    static bool camExists = false;

	void Start()
	{
		cameraTarget = GameObject.FindGameObjectWithTag("Player");
		lastPosition = new Vector3(cameraTarget.transform.position.x, cameraTarget.transform.position.y + offsetHeight, cameraTarget.transform.position.z - offsetDistance);
        //offset = new Vector3(cameraTarget.transform.position.x, cameraTarget.transform.position.y + offsetHeight, cameraTarget.transform.position.z - offsetDistance);
        offset = new Vector3(0, offsetHeight, -offsetDistance);//Wut? How did the above ever work? Only works if player is standing at the origin.
        camExists = true;

        Cursor.visible = false;//Have to turn this back on when we fire up a UI, then turn off again afterward.
        Cursor.lockState = CursorLockMode.Confined;
        
	}

	void Update()
	{
        /*
		if(Input.GetKeyDown(KeyCode.F))
		{
			if(following)
			{
				following = false;
			} 
			else
			{
				following = true;
			}
		} 
		if(Input.GetKey(KeyCode.A))
		{
			rotate = -1;
		} 
		else if(Input.GetKey(KeyCode.D))
		{
			rotate = 1;
		} 
		else
		{
			rotate = 0;
		}*/


		if(following)
		{
            //Debug.Log("Camera rotate: " + rotate + " speed " + rotateSpeed + " offset " + offset.ToString());
            //offset = Quaternion.AngleAxis(rotate * rotateSpeed, Vector3.up) * offset;
            //transform.position = cameraTarget.transform.position + offset;

            Vector3 behindTarget = -1 * cameraTarget.transform.forward;
            behindTarget *= offsetDistance;
            behindTarget.y = offsetHeight;

            //Debug.Log("target forward vector: " + cameraTarget.transform.forward.ToString() + "  behind target: " + behindTarget.ToString());
            transform.position = cameraTarget.transform.position + behindTarget;

            //transform.position = new Vector3(Mathf.Lerp(lastPosition.x, cameraTarget.transform.position.x + offset.x, smoothing * Time.deltaTime), 
			//	Mathf.Lerp(lastPosition.y, cameraTarget.transform.position.y + offset.y, smoothing * Time.deltaTime), 
			//	Mathf.Lerp(lastPosition.z, cameraTarget.transform.position.z + offset.z, smoothing * Time.deltaTime));
		} 
		else
		{
			transform.position = lastPosition; 
		}
        Vector3 lookAt = cameraTarget.transform.position;
        lookAt.y += lookHeight;
        transform.LookAt(lookAt);
	}

	void LateUpdate()
	{
		lastPosition = transform.position;
	}
}