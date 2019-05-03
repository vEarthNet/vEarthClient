using System.Collections;
using System.Collections.Generic;
using UnityEngine;

struct collidingObject
{
    Vector3 lastPos;
    Vector3 lastVel;
    float lastRot;
    float lastDist;
}
//Base script that attaches to every world object that exists in the DB.
public class Agent : SimBase
{
    //[HideInInspector]
    //public BoidFlocking bF = null;
    //[HideInInspector]
    //public BoidController bFC = null;
    [HideInInspector]
    public Animator mAnimator = null;
    [HideInInspector]
    public float lastTime = 0.0f;
    [HideInInspector]
    List<GameObject> mCollidingObjs;

    public Agent() : base()
    {
    }

    // Start is called before the first frame update
    public void Start()
    {
        base.Start();
        mCollidingObjs = new List<GameObject>();
        mAnimator = GetComponent<Animator>();
        //bF = GetComponent<BoidFlocking>();
    }

    // Update is called once per frame
    public void Update()
    {
        base.Update();

        //if ((bFC == null)&&(bF != null))
        //    bFC = bF.controller;

        //Debug.Log(name + " has " + collidingObjs.Count + " colliding objects.");
    }

    void OnAnimatorMove()
    {
        mAnimator.ApplyBuiltinRootMotion();
    }

    public void OnTriggerEnter(Collider col)
    {
        Vector3 closePoint = col.ClosestPoint(transform.position);
        Vector3 diffVec = Vector3.Normalize(closePoint - transform.position);
        //float dotProd = Math3d.SignedDotProduct(transform.forward, diffVec, transform.up);
        mCollidingObjs.Add(col.gameObject);
        //if (collidingObjs.Find(col.gameObject))
    }

    //HERE is where you get a regular update for every currently colliding object.
    public void OnTriggerStay(Collider col)
    {
        //Debug.Log(name + " collides with " + col.gameObject.name );
        //Vector3 closePoint = col.ClosestPoint(transform.position);
        //Vector3 diffVec = Vector3.Normalize(closePoint - transform.position);
        //float dotProd = Math3d.SignedDotProduct(transform.forward, diffVec, transform.up);
    }

    public void OnTriggerExit(Collider col)
    {
        mCollidingObjs.Remove(col.gameObject);
        //Debug.Log("Agent " + name + " exiting collision with " + col.gameObject.name + " colliding Objects " + collidingObjs.Count);

    }

}
